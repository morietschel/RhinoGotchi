// 3D viewer for the compressed .glb models: white-on-blue, chunky pixels, drag to orbit.
import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { MeshoptDecoder } from 'three/addons/libs/meshopt_decoder.module.js';

const BLUE = 0x0a22c9;
const loader = new GLTFLoader();
loader.setMeshoptDecoder(MeshoptDecoder);

const surface = new THREE.MeshLambertMaterial({ color: 0xffffff, side: THREE.DoubleSide });
const wire = new THREE.LineBasicMaterial({ color: 0xffffff });
const dots = new THREE.PointsMaterial({ color: 0xffffff, size: 3, sizeAttenuation: false });

// Parse a model into a restyled, framed THREE.Group. onProgress gets 0..1 while downloading.
export function loadModel(url, onProgress) {
  return new Promise((resolve, reject) => {
    loader.load(url, (gltf) => {
      const obj = gltf.scene;
      let meshes = 0;
      obj.traverse((o) => {
        if (o.isMesh) { o.material = surface; if (!o.geometry.attributes.normal) o.geometry.computeVertexNormals(); } // normals are rebuilt here, not stored
        else if (o.isLine) o.material = wire;
        else if (o.isPoints) o.material = dots;
      });
      obj.updateMatrixWorld(true);
      // Frame on the bulk of the geometry (2nd..98th percentile of sampled points), so strays don't shrink the view.
      const pts = { mesh: [[], [], []], other: [[], [], []] };
      const v = new THREE.Vector3();
      obj.traverse((o) => {
        const pos = o.geometry?.attributes?.position;
        if (!pos || !o.visible) return;
        for (let p = o.parent; p; p = p.parent) if (!p.visible) return;
        if (o.isMesh) meshes++;
        const to = o.isMesh ? pts.mesh : pts.other;
        const step = Math.max(1, Math.floor(pos.count / 400));
        for (let i = 0; i < pos.count; i += step) {
          v.fromBufferAttribute(pos, i).applyMatrix4(o.matrixWorld);
          if (isFinite(v.x + v.y + v.z)) { to[0].push(v.x); to[1].push(v.y); to[2].push(v.z); }
        }
      });
      const src = pts.mesh[0].length ? pts.mesh : pts.other;
      if (!src[0].length) return reject(new Error('nothing to show'));
      const lo = [], hi = [];
      for (const a of src) { a.sort((x, y) => x - y); lo.push(a[Math.floor(a.length * 0.02)]); hi.push(a[Math.ceil(a.length * 0.98) - 1]); }
      const box = new THREE.Box3(new THREE.Vector3(...lo), new THREE.Vector3(...hi));
      resolve({ object: obj, box, meshes });
    }, (e) => { if (onProgress && e.total) onProgress(e.loaded / e.total); }, (e) => reject(e instanceof Error ? e : new Error('could not load model')));
  });
}

export function createViewer(host) {
  const renderer = new THREE.WebGLRenderer({ antialias: false, preserveDrawingBuffer: true });
  renderer.domElement.className = 'gl';
  host.prepend(renderer.domElement);
  const scene = new THREE.Scene();
  scene.background = new THREE.Color(BLUE);
  const camera = new THREE.PerspectiveCamera(35, 1, 0.1, 1000);
  camera.up.set(0, 0, 1);
  const fill = new THREE.AmbientLight(0x1c3cff, 0.6);
  const key = new THREE.DirectionalLight(0xffffff, 2.9);
  camera.add(key); key.position.set(-0.6, 0.9, 1); // headlight, so shading follows the view
  scene.add(camera, fill);
  const controls = new OrbitControls(camera, renderer.domElement);
  controls.enableDamping = true; controls.dampingFactor = 0.12;
  controls.autoRotate = true; controls.autoRotateSpeed = 1.2;
  controls.addEventListener('start', () => { controls.autoRotate = false; });

  let current = null, running = false, px = 0.5;
  const size = () => {
    const w = host.clientWidth || 2, h = host.clientHeight || 2;
    renderer.setPixelRatio(px); renderer.setSize(w, h, false);
    camera.aspect = w / h; camera.updateProjectionMatrix();
  };
  new ResizeObserver(size).observe(host); size();
  const loop = () => { if (!running) return; controls.update(); renderer.render(scene, camera); requestAnimationFrame(loop); };

  function clear() {
    if (!current) return;
    scene.remove(current.object);
    current.object.traverse((o) => o.geometry?.dispose());
    current = null;
  }
  function show(model) {
    clear(); current = model; scene.add(model.object);
    const c = model.box.getCenter(new THREE.Vector3());
    const r = Math.max(model.box.getSize(new THREE.Vector3()).length() / 2, 1e-6);
    const d = r / Math.sin(THREE.MathUtils.degToRad(camera.fov / 2)) * 0.9;
    controls.target.copy(c);
    camera.position.copy(c).add(new THREE.Vector3(-0.62, -0.72, 0.42).normalize().multiplyScalar(d));
    camera.near = d / 200; camera.far = d * 20; camera.updateProjectionMatrix();
    controls.autoRotate = true; controls.update();
    if (!running) { running = true; requestAnimationFrame(loop); }
  }
  // Neutral gray render of the current view for the judge (the on-screen look is too stylised).
  function capture(width = 1280) {
    const w = host.clientWidth || 2, h = host.clientHeight || 2;
    renderer.setPixelRatio(1); renderer.setSize(width, Math.round(width * h / w), false);
    const bg = scene.background; scene.background = new THREE.Color(0xa3a3a3);
    fill.color.set(0xffffff); fill.intensity = 0.45; wire.color.set(0x111111); dots.color.set(0x111111);
    renderer.render(scene, camera);
    const url = renderer.domElement.toDataURL('image/jpeg', 0.85);
    scene.background = bg; fill.color.set(0x1c3cff); fill.intensity = 0.6; wire.color.set(0xffffff); dots.color.set(0xffffff);
    size(); renderer.render(scene, camera);
    return url;
  }
  return { show, clear, capture, stop() { running = false; }, setPixels(p) { px = p; size(); } };
}
