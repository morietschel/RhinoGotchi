// DEEPCUT — name the rarest Rhino command. Zero-dependency Node server.
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');

const ROOT = __dirname;
const PORT = Number(process.env.PORT) || 4173;
const DATA = path.join(ROOT, 'data');
const IMG = path.join(ROOT, 'models');
const MODELS_FILE = path.join(ROOT, 'models.json');
const UA = 'Mozilla/5.0 (deepcut-game)';

const COMMANDS = fs.readFileSync(path.join(ROOT, 'commands.txt'), 'utf8')
  .split('\n').map((s) => s.trim()).filter(Boolean);
const CANON = new Map(COMMANDS.map((c) => [c.toLowerCase(), c]));

let models = [];
let loadState = { done: false, loaded: 0, error: null };
let runtimeKey = null; // key pasted in the UI: memory only, never written to disk

function apiKey() {
  if (runtimeKey) return runtimeKey;
  if (process.env.OPENAI_API_KEY) return process.env.OPENAI_API_KEY;
  try {
    const m = fs.readFileSync(path.join(ROOT, '.env'), 'utf8').match(/^\s*OPENAI_API_KEY\s*=\s*"?([^"\s]+)"?/m);
    if (m) return m[1];
  } catch {}
  return null;
}

// ---------- model images ----------
async function loadModels() {
  // models.json lists 50 Rhino models from McNeel help forum topics. Each was converted from its
  // .3dm (see "url") to a compressed .glb of the visible render meshes and curves, kept in models/.
  try {
    const list = JSON.parse(fs.readFileSync(MODELS_FILE, 'utf8'));
    models = list.filter((m) => fs.existsSync(path.join(IMG, m.file)));
    loadState = { done: true, loaded: models.length, error: models.length ? null : 'no models found' };
  } catch (e) {
    loadState = { done: true, loaded: 0, error: String(e.message || e) };
  }
}

// ---------- judging ----------
function tierFor(rarity) {
  if (rarity >= 0.85) return 'ARCANE';
  if (rarity >= 0.65) return 'DEEP MENU';
  if (rarity >= 0.45) return 'OFF THE TOOLBAR';
  if (rarity >= 0.25) return 'DAILY DRIVER';
  return 'TOOLBAR STAPLE';
}

// Prompts sent to the decision model. Editable from the UI; saved to prompts.json.
const PROMPTS_FILE = path.join(ROOT, 'prompts.json');
const DEFAULT_PROMPTS = {
  input: 'command: "{command}"',
  decision1: 'is the command in this list:\n{commands}\n',
  decision2: 'This is a niche and rare rhino command. Few have used it.',
  decision3: 'Was this RhinoCommand used directly for modeling the displayed geometry? It must be actively involved in the specific model.',
};
let prompts = { ...DEFAULT_PROMPTS };
try {
  const saved = JSON.parse(fs.readFileSync(PROMPTS_FILE, 'utf8'));
  for (const k of Object.keys(DEFAULT_PROMPTS)) if (typeof saved[k] === 'string' && saved[k].trim()) prompts[k] = saved[k];
} catch {}
const fill = (tpl, command) => tpl.replaceAll('{command}', command).replaceAll('{commands}', COMMANDS.join('\n'));

function buildRequest(command, dataUrl) {
  return {
    model: 'gpt-6-luna',
    input: [{
      role: 'user',
      content: [
        { type: 'input_text', text: fill(prompts.input, command) },
        { type: 'input_image', image_url: dataUrl },
      ],
    }],
    questions: [
      { type: 'choice', instructions: fill(prompts.decision1, command), choices: [{ value: 'Yes' }, { value: 'No' }], name: 'Decision 1' },
      { type: 'predicate', instructions: fill(prompts.decision2, command), name: 'Decision 2' },
      {
        type: 'choice', instructions: fill(prompts.decision3, command),
        choices: [{ value: 'This command was used' }, { value: 'This command was not used' }],
        name: 'Decision 3',
      },
    ],
  };
}

// Deterministic stand-in used only when MOCK_JUDGE=1 (UI testing without a key).
function mockAnswers(command) {
  let h = 0;
  for (const ch of command.toLowerCase()) h = (h * 31 + ch.charCodeAt(0)) >>> 0;
  const rarity = Math.min(0.97, 0.15 + (command.length / 22) * 0.6 + (h % 20) / 100);
  const pUsed = 0.35 + (h % 60) / 100;
  const used = pUsed >= 0.5;
  return [
    { type: 'choice', name: 'Decision 1', choice: CANON.has(command.toLowerCase()) ? 'Yes' : 'No', confidence: 0.99 },
    { type: 'predicate', name: 'Decision 2', probability: rarity },
    {
      type: 'choice', name: 'Decision 3', confidence: used ? pUsed : 1 - pUsed,
      choice: used ? 'This command was used' : 'This command was not used',
      probabilities: [
        { value: 'This command was used', probability: pUsed },
        { value: 'This command was not used', probability: 1 - pUsed },
      ],
    },
  ];
}

async function judge(modelId, rawCommand, image) {
  const model = models.find((m) => m.id === modelId);
  if (!model) throw Object.assign(new Error('unknown model'), { status: 400 });
  const typed = String(rawCommand || '').trim().replace(/^_+/, '').slice(0, 60);
  if (!typed) throw Object.assign(new Error('empty command'), { status: 400 });
  const canonical = CANON.get(typed.toLowerCase()) || null;
  const command = canonical || typed;

  let answers;
  const mock = process.env.MOCK_JUDGE === '1';
  if (mock) {
    await new Promise((r) => setTimeout(r, 700));
    answers = mockAnswers(command);
  } else {
    const key = apiKey();
    if (!key) throw Object.assign(new Error('No OpenAI API key set'), { status: 401, code: 'no_key' });
    // The browser sends a render of the 3D view the player was looking at.
    if (typeof image !== 'string' || !/^data:image\/(jpeg|png);base64,[A-Za-z0-9+/=]+$/.test(image)) {
      throw Object.assign(new Error('missing view snapshot'), { status: 400 });
    }
    const res = await fetch('https://api.openai.com/v1/decisions', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${key}` },
      body: JSON.stringify(buildRequest(command, image)),
      signal: AbortSignal.timeout(45_000),
    });
    const text = await res.text();
    let body; try { body = JSON.parse(text); } catch { body = null; }
    if (!res.ok) {
      const msg = body?.error?.message || text.slice(0, 300) || `HTTP ${res.status}`;
      throw Object.assign(new Error(`OpenAI: ${msg}`), {
        status: res.status === 401 ? 401 : 502, code: res.status === 401 ? 'bad_key' : 'upstream',
      });
    }
    answers = body?.answers || [];
  }

  const by = (name, i) => answers.find((a) => a.name === name) || answers[i] || {};
  const d1 = by('Decision 1', 0), d2 = by('Decision 2', 1), d3 = by('Decision 3', 2);
  const refused = [d1, d2, d3].some((a) => a.type === 'refusal');

  // The list is known locally, so membership is settled exactly; the model's answer is reported alongside.
  const inList = Boolean(canonical);
  const rarity = typeof d2.probability === 'number' ? d2.probability : 0;
  const usedP = d3.probabilities?.find((p) => /was used/i.test(String(p.value)))?.probability;
  const used = /was used/i.test(String(d3.choice || ''));
  const pUsed = typeof usedP === 'number' ? usedP : used ? (d3.confidence ?? 1) : 1 - (d3.confidence ?? 1);

  let points = 0, verdict = 'scored';
  if (!inList) verdict = 'not_a_command';
  else if (refused) verdict = 'refused';
  else if (!used) verdict = 'not_used';
  else points = Math.max(25, Math.round(1000 * rarity));

  return {
    command, verdict, points, rarity, pUsed, tier: tierFor(rarity), mock,
    decisions: {
      inList: { local: inList, model: d1.choice ?? null, confidence: d1.confidence ?? null },
      rare: { probability: d2.probability ?? null },
      used: { choice: d3.choice ?? null, confidence: d3.confidence ?? null },
    },
  };
}

// ---------- http ----------
const MIME = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.css': 'text/css', '.jpg': 'image/jpeg', '.png': 'image/png', '.webp': 'image/webp', '.svg': 'image/svg+xml' };
const json = (res, code, obj) => { res.writeHead(code, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' }); res.end(JSON.stringify(obj)); };
const readBody = (req) => new Promise((resolve, reject) => {
  let s = ''; req.on('data', (c) => { s += c; if (s.length > 8e6) reject(new Error('too large')); });
  req.on('end', () => { try { resolve(s ? JSON.parse(s) : {}); } catch (e) { reject(e); } });
});
function sendFile(res, file, cache) {
  fs.readFile(file, (err, buf) => {
    if (err) { res.writeHead(404); return res.end('not found'); }
    res.writeHead(200, { 'Content-Type': MIME[path.extname(file)] || 'application/octet-stream', 'Cache-Control': cache || 'no-store' });
    res.end(buf);
  });
}

http.createServer(async (req, res) => {
  const url = new URL(req.url, 'http://x');
  try {
    if (url.pathname === '/api/status') {
      return json(res, 200, { hasKey: Boolean(apiKey()), mock: process.env.MOCK_JUDGE === '1', models: loadState });
    }
    if (url.pathname === '/api/models') {
      return json(res, 200, { ready: loadState.done, models: models.map(({ id, title, name, file, source }) => ({ id, title, name, source, url: `/models/${file}` })) });
    }
    if (url.pathname === '/api/commands') return json(res, 200, COMMANDS);
    if (url.pathname === '/api/prompts' && req.method === 'GET') {
      return json(res, 200, { prompts, defaults: DEFAULT_PROMPTS });
    }
    if (url.pathname === '/api/prompts' && req.method === 'POST') {
      const body = await readBody(req);
      const next = { ...DEFAULT_PROMPTS };
      if (!body.reset) for (const k of Object.keys(DEFAULT_PROMPTS)) if (typeof body[k] === 'string' && body[k].trim()) next[k] = body[k].slice(0, 20000);
      prompts = next;
      fs.writeFileSync(PROMPTS_FILE, JSON.stringify(prompts, null, 1));
      return json(res, 200, { prompts, defaults: DEFAULT_PROMPTS });
    }
    if (url.pathname === '/api/key' && req.method === 'POST') {
      const { key } = await readBody(req);
      runtimeKey = typeof key === 'string' && key.trim() ? key.trim() : null;
      return json(res, 200, { hasKey: Boolean(apiKey()) });
    }
    if (url.pathname === '/api/judge' && req.method === 'POST') {
      const { modelId, command, image } = await readBody(req);
      return json(res, 200, await judge(String(modelId), command, image));
    }
    if (url.pathname.startsWith('/models/')) {
      const file = path.join(IMG, path.basename(url.pathname));
      const st = fs.existsSync(file) && fs.statSync(file);
      if (!st) { res.writeHead(404); return res.end('not found'); }
      res.writeHead(200, { 'Content-Type': 'model/gltf-binary', 'Content-Length': st.size, 'Cache-Control': 'public, max-age=86400' });
      return fs.createReadStream(file).pipe(res);
    }
    const p = url.pathname === '/' ? 'index.html' : path.normalize(url.pathname).replace(/^([/\\]|\.\.)+/, '');
    return sendFile(res, path.join(ROOT, 'public', p));
  } catch (e) {
    return json(res, e.status || 500, { error: e.message || 'server error', code: e.code || 'error' });
  }
}).listen(PORT, '127.0.0.1', () => {
  console.log(`DEEPCUT running at http://localhost:${PORT}`);
  loadModels().then(() => console.log(`models: ${loadState.loaded}${loadState.error ? ' (' + loadState.error + ')' : ''}`));
});
