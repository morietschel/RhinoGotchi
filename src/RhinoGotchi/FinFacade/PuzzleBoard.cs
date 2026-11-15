using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.Geometry;
using RhinoGotchi.Fin;

namespace GotchiFins
{
    /// <summary>
    /// The RhinoGotchi sidebar for Fin Facade, laid out like the RhinoGotchi mock-up: an orange
    /// title bar, five level tiles, a key axon that demos the current step on a loop (the rhino
    /// pet takes over when it celebrates or loses), a cream lesson card with the command, a
    /// yellow Run button, the steps (lit up in time with the demo), the ways to run the
    /// command, a row of the five command icons, and Back / Next.
    /// Every button is a rectangle with an action, rebuilt on each paint.
    /// </summary>
    internal sealed class PuzzleBoard : Drawable
    {
        private static readonly string[] StageNames = { "MASS", "PROFILE", "FIN", "ARRAY", "SUN", "FINISH" };
        private static readonly string[] Commands = { "Box", "Polyline", "ExtrudeCrv", "ArrayCrv", "Sun", "ApplyBoxMapping" };

        private int _page;
        private int _lastCurrentPage;
        private PointF _mouse = new PointF(-1, -1);
        private readonly List<(RectangleF Rect, Action Act)> _hits = new List<(RectangleF, Action)>();

        // Animation clock for the demo
        private readonly UITimer _timer = new UITimer { Interval = 1.0 / 30 };
        private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();

        public PuzzleBoard()
        {
            BackgroundColor = Palette.Paper;
            MinimumSize = new Size(200, 200); // pages fit themselves to any height (see FitFor)
            _page = _lastCurrentPage = FinSession.CurrentPageIndex;
            FinSession.Changed += OnSessionChanged;
            SizeChanged += (s, e) => Invalidate();
            _timer.Elapsed += (s, e) => Invalidate();
        }

        protected override void OnLoadComplete(EventArgs e)
        {
            base.OnLoadComplete(e);
            _timer.Start();
        }

        protected override void OnUnLoad(EventArgs e)
        {
            _timer.Stop();
            base.OnUnLoad(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                FinSession.Changed -= OnSessionChanged;
                _timer.Stop();
                _timer.Dispose();
            }
            base.Dispose(disposing);
        }

        private GamePage Page => GamePage.All[_page];

        /// <summary>The player can browse back, but not past the level they're on.</summary>
        private static int MaxPage => FinSession.CurrentPageIndex;

        private void OnSessionChanged()
        {
            int current = FinSession.CurrentPageIndex;
            if (current != _lastCurrentPage)
            {
                _page = current; // progress moved: jump to the new level
                _lastCurrentPage = current;
                _clock.Restart(); // and start its demo from the beginning
            }
            _page = Math.Min(_page, MaxPage);
            Invalidate();
        }

        // ---------------------------------------------------------------- input

        private void Hit(RectangleF rect, Action act) => _hits.Add((rect, act));

        private bool Hovered(RectangleF rect) => rect.Contains(_mouse);

        protected override void OnMouseMove(MouseEventArgs e)
        {
            _mouse = e.Location;
            Cursor = _hits.Any(h => h.Rect.Contains(_mouse)) ? Cursors.Pointer : Cursors.Default;
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            _mouse = new PointF(-1, -1);
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            FinSession.Touch();
            foreach (var (rect, act) in _hits.ToArray())
            {
                if (rect.Contains(e.Location))
                {
                    act();
                    e.Handled = true;
                    Invalidate();
                    break;
                }
            }
            base.OnMouseDown(e);
        }

        private void GoTo(int page)
        {
            _page = Math.Max(0, Math.Min(MaxPage, page));
            _clock.Restart();
        }

        private static void Start()
        {
            if (RhinoDoc.ActiveDoc is RhinoDoc doc)
                FinSession.Start(doc);
        }

        // ---------------------------------------------------------------- layout

        protected override void OnPaint(PaintEventArgs e) => Render(e.Graphics, Width, Height);

        /// <summary>
        /// Draws the whole sidebar into a width x height area, with the demo at
        /// <paramref name="time"/> seconds (default: the live clock).
        /// </summary>
        internal void Render(Graphics g, float width, float height, double? time = null)
        {
            double t = time ?? _clock.Elapsed.TotalSeconds;
            g.AntiAlias = true;
            g.FillRectangle(Palette.Paper, 0, 0, width, height);
            var (fit, artShare) = FitFor(width, height, t, time);
            _hits.Clear();
            Layout(g, width, height, t, time, fit, artShare);
        }

        // ---------------------------------------------------------------- fitting

        /// <summary>The demo's height as a share of the column width: normal, and squeezed.</summary>
        private const float ArtTall = 0.56f, ArtShort = 0.34f;

        /// <summary>Smallest text scale used to make a page fit; below this the panel is simply too short.</summary>
        private const float MinFit = 0.55f;

        private string? _fitKey;
        private (float Fit, float Art) _fitted = (1, ArtTall);

        /// <summary>Short panel: "ways to do it" goes on one line.</summary>
        private bool _compact, _fittedCompact;

        /// <summary>
        /// The panel can't scroll, so every page must fit its height. Measure the page; if it is too
        /// tall, first squeeze the demo picture, then scale everything down just enough. The answer
        /// is kept until something that changes the page's height changes.
        /// </summary>
        private (float Fit, float Art) FitFor(float width, float height, double t, double? time)
        {
            PetMood mood = FinSession.Mood;
            string key = string.Join("|", width, height, _page, FinSession.Started, FinSession.Completed,
                FinSession.Hint ?? "", mood == PetMood.Awake || mood == PetMood.Standby, FinSession.ChosenFinish?.ToString() ?? "");
            if (key == _fitKey)
            {
                _compact = _fittedCompact;
                return _fitted;
            }

            float Need(float fit, float art, bool compact)
            {
                _compact = compact;
                using var bmp = new Bitmap(Math.Max(1, (int)width), Math.Max(1, (int)height), PixelFormat.Format32bppRgba);
                using var g = new Graphics(bmp);
                return Layout(g, width, height, t, time, fit, art);
            }

            // In order, until it fits: as designed; a shorter demo; "ways to do it" on one line;
            // then everything scaled down (heights shrink about in step, so two passes settle it).
            (float, float) result = (1, ArtTall);
            bool compact = false;
            float tallNeed = Need(1, ArtTall, false);
            if (tallNeed > height)
            {
                float shortNeed = Need(1, ArtShort, false);
                if (shortNeed <= height)
                {
                    float share = (tallNeed - height) / Math.Max(1, tallNeed - shortNeed);
                    result = (1, ArtTall - (ArtTall - ArtShort) * Math.Min(1, share + 0.02f));
                }
                else
                {
                    compact = true;
                    float compactNeed = Need(1, ArtShort, true);
                    float fit = compactNeed <= height ? 1 : Math.Max(MinFit, height / compactNeed);
                    for (int i = 0; i < 2 && fit < 1; i++)
                    {
                        float need = Need(fit, ArtShort, true);
                        if (need <= height)
                            break;
                        fit = Math.Max(MinFit, fit * height / need * 0.99f);
                    }
                    result = (fit, ArtShort);
                }
            }
            _hits.Clear();
            _fitKey = key;
            _fitted = result;
            _compact = _fittedCompact = compact;
            return result;
        }

        /// <summary>
        /// Lays out (and draws) the page at text scale <paramref name="fit"/> with the demo
        /// <paramref name="artShare"/> of the column tall. Returns the height the page needs.
        /// </summary>
        private float Layout(Graphics g, float width, float height, double t, double? time, float fit, float artShare)
        {
            float col = Math.Max(200, Math.Min(width - 28, 360));
            float s = col / 300f * fit;
            float x = (width - col) / 2;
            GamePage page = Page;
            Demo demo = Demo.For(page, t);

            float y = DrawTitle(g, x, 14 * s, col, s);
            y = DrawTiles(g, x, y + 12 * s, col, s, page, demo) + 12 * s;

            // The key axon, or the pet when it has something to say about this level
            PetMood mood = FinSession.Mood;
            bool petMoment = mood == PetMood.Win || mood == PetMood.Lose || mood == PetMood.Final || (mood == PetMood.Dance && page.Kind == PageKind.Done);
            var art = new RectangleF(x, y, col, col * artShare);
            if (petMoment && page.Kind != PageKind.Welcome)
                DrawPetMoment(g, art, s, mood, time ?? FinSession.MoodTime);
            else
                DrawArt(g, art, s, page, demo);
            if (!petMoment && page.Stage.HasValue && FinSession.IsDone(page.Stage.Value))
                Toast(g, "Solved", x + col / 2, art.Y + 4 * s, s);
            y = art.Bottom + 10 * s;

            switch (page.Kind)
            {
                case PageKind.Welcome:
                    y = DrawWelcome(g, x, y, col, s, t);
                    break;
                case PageKind.Done:
                    y = DrawDone(g, x, y, col, s, t);
                    break;
                default:
                    y = DrawLevel(g, x, y, col, s, page, demo);
                    break;
            }

            // Back / Next at the bottom when there's room
            float bh = 30 * s;
            DrawFooter(g, x, Math.Max(y + 12 * s, height - 14 * s - bh), col, bh, s, page);
            return y + 12 * s + bh + 14 * s;
        }

        /// <summary>"Fin Facade" with the orange bar, and its subtitle.</summary>
        private static float DrawTitle(Graphics g, float x, float y, float col, float s)
        {
            using Font title = Typeface.Serif(19 * s);
            using Font sub = Typeface.Sans(8.5f * s);
            float th = Cards.H(g, title), sh = Cards.H(g, sub);
            g.FillPath(Palette.Orange, GraphicsPath.GetRoundRect(new RectangleF(x, y + 2 * s, 4.5f * s, th + sh - 2 * s), 2 * s));
            g.DrawText(title, Palette.Ink, x + 12 * s, y, "Fin Facade");
            g.DrawText(sub, Palette.Muted, x + 12 * s, y + th, "6 commands, one building");
            return y + th + sh;
        }

        /// <summary>Five rounded tiles: black when solved, orange-edged when current, grey when still to come.</summary>
        private float DrawTiles(Graphics g, float x, float y, float col, float s, GamePage page, Demo demo)
        {
            using Font numeral = Typeface.Serif(13 * s);
            using Font label = Typeface.Sans(6.3f * s, true);
            int n = GamePage.LevelCount;
            float size = 30 * s, gap = Math.Min(14 * s, (col - n * size) / (n - 1));
            for (int i = 0; i < n; i++)
            {
                var stage = (Stage)i;
                float tx = x + i * (size + gap);
                var r = new RectangleF(tx, y, size, size);
                bool done, current;
                if (page.Kind == PageKind.Welcome)
                {
                    done = demo.StageShown > i;
                    current = demo.StageShown == i;
                }
                else
                {
                    done = FinSession.IsDone(stage);
                    current = page.Stage == stage;
                }

                IGraphicsPath path = GraphicsPath.GetRoundRect(r, 6 * s);
                g.FillPath(done ? Palette.Ink : current && page.Kind == PageKind.Welcome ? Palette.Accent : Palette.Paper, path);
                if (!done)
                    g.DrawPath(new Pen(current ? Palette.Orange : Palette.Rule, (current ? 2.2f : 1.2f) * s), path);
                Color ink = done ? Palette.Paper : current ? Palette.Ink : Palette.Faint;
                SizeF ns = g.MeasureString(numeral, (i + 1).ToString());
                g.DrawText(numeral, ink, tx + (size - ns.Width) / 2, y + (size - ns.Height) / 2, (i + 1).ToString());
                SizeF ls = g.MeasureString(label, StageNames[i]);
                g.DrawText(label, current ? Palette.Ink : Palette.Muted, tx + (size - ls.Width) / 2, y + size + 3 * s, StageNames[i]);

                // Solved levels can be revisited
                if (FinSession.Started && (int)stage < FinSession.Completed + 1)
                {
                    int target = GamePage.IndexOf(stage);
                    Hit(r, () => GoTo(target));
                }
            }
            return y + size + 3 * s + Cards.H(g, label);
        }

        // ---------------------------------------------------------------- pages

        private float DrawWelcome(Graphics g, float x, float y, float col, float s, double t)
        {
            using Font kicker = Typeface.Sans(7.5f * s, true);
            using Font headline = Typeface.Serif(17 * s);
            using Font body = Typeface.Sans(9.5f * s);
            Cards.Spaced(g, kicker, Palette.Muted, "HOW TO PLAY", x, y, 1.5f * s);
            y += Cards.H(g, kicker) + 3 * s;
            g.DrawText(headline, Palette.Ink, x, y, "Fin Facade");
            y += Cards.H(g, headline) + 6 * s;

            string[] how =
            {
                "Each level teaches one Rhino command.",
                "Type it, click its icon, or press Run. Click the yellow points.",
                "Solve all six first try for a perfect run.",
            };
            for (int i = 0; i < how.Length; i++)
            {
                float d = 18 * s;
                g.FillEllipse(Palette.Ink, x, y, d, d);
                using Font num = Typeface.Sans(8 * s, true);
                SizeF ns = g.MeasureString(num, (i + 1).ToString());
                g.DrawText(num, Palette.Paper, x + (d - ns.Width) / 2, y + (d - ns.Height) / 2, (i + 1).ToString());
                float lineY = y;
                foreach (string line in Cards.Wrap(g, body, how[i], col - d - 10 * s))
                {
                    g.DrawText(body, Palette.Ink, x + d + 10 * s, lineY + (d - Cards.H(g, body)) / 2, line);
                    lineY += Cards.H(g, body) * 1.2f;
                }
                y = Math.Max(y + d, lineY) + 6 * s;
            }

            // The pet says hello, then the big Start button
            y += 4 * s;
            y = DrawPetRow(g, x, y, col, s, PetMood.Awake, t, FinSession.Speech) + 8 * s;
            var start = new RectangleF(x, y, col, 34 * s);
            Pill(g, start, "Start", primary: true, s);
            Hit(start, Start);
            return start.Bottom;
        }

        private float DrawDone(Graphics g, float x, float y, float col, float s, double t)
        {
            using Font kicker = Typeface.Sans(7.5f * s, true);
            using Font headline = Typeface.Serif(20 * s);
            using Font body = Typeface.Sans(9.5f * s);
            bool perfect = FinSession.Perfect;
            Cards.Spaced(g, kicker, Palette.OrangeText, perfect ? "PERFECT RUN" : "SOLVED", x, y, 1.5f * s);
            y += Cards.H(g, kicker) + 3 * s;
            g.DrawText(headline, Palette.Ink, x, y, "Splendid.");
            y += Cards.H(g, headline) + 4 * s;
            string line = $"{FinSession.FirstTryCount} of {GamePage.LevelCount} levels first try. Box, Polyline, ExtrudeCrv, ArrayCrv, Sun, ApplyBoxMapping: one facade.";
            foreach (string l in Cards.Wrap(g, body, line, col))
            {
                g.DrawText(body, Palette.Body, x, y, l);
                y += Cards.H(g, body) * 1.25f;
            }
            y += 8 * s;
            if (!FinSession.Started)
                y = DrawPetRow(g, x, y, col, s, PetMood.Dance, t, "Finish all six levels and I'll dance for you!") + 10 * s;
            y = DrawCommandRow(g, x, y, col, s) + 6 * s;
            var zoom = new RectangleF(x, y, col, 32 * s);
            Pill(g, zoom, "Zoom to the facade", primary: true, s);
            Hit(zoom, () => { if (RhinoDoc.ActiveDoc is RhinoDoc doc) FinSession.ZoomToAll(doc); });
            return zoom.Bottom;
        }

        private float DrawLevel(Graphics g, float x, float y, float col, float s, GamePage page, Demo demo)
        {
            Stage stage = page.Stage!.Value;
            bool done = FinSession.IsDone(stage);
            bool current = FinSession.Current == stage;
            int misses = FinSession.Misses(stage);

            // Lesson card: level, misses or first try, the command, one line
            using (Font kicker = Typeface.Sans(7.5f * s, true))
            using (Font mono = Typeface.Mono(17 * s))
            using (Font body = Typeface.Sans(9.5f * s))
            {
                float pad = 11 * s;
                List<string> lines = Cards.Wrap(g, body, page.Intro, col - 2 * pad).ToList();
                float h = pad + Cards.H(g, kicker) + 3 * s + Cards.H(g, mono) + 2 * s + lines.Count * Cards.H(g, body) * 1.2f + pad - 2 * s;
                var card = new RectangleF(x, y, col, h);
                g.FillPath(Palette.Cream, GraphicsPath.GetRoundRect(card, 10 * s));
                g.DrawPath(new Pen(Palette.CreamRule, 1), GraphicsPath.GetRoundRect(card, 10 * s));
                float cy = y + pad;
                Cards.Spaced(g, kicker, Palette.OrangeText, page.Kicker, x + pad, cy, 1.4f * s);
                string status = done ? (FinSession.FirstTry(stage) ? "✓ first try" : "✓ solved")
                    : misses > 0 ? $"misses {misses} / {FinSession.MissesToLose}" : "★ first try possible";
                SizeF ss = g.MeasureString(kicker, status);
                g.DrawText(kicker, Palette.OrangeText, x + col - pad - ss.Width, cy, status);
                cy += Cards.H(g, kicker) + 3 * s;
                g.DrawText(mono, Palette.Ink, x + pad, cy, page.Command ?? "");
                cy += Cards.H(g, mono) + 2 * s;
                foreach (string line in lines)
                {
                    g.DrawText(body, Palette.Body, x + pad, cy, line);
                    cy += Cards.H(g, body) * 1.2f;
                }
                y = card.Bottom + 10 * s;
            }

            // Level 6: the three finishes as swatches (Rhino's own way: Properties → Material)
            if (stage == Stage.Material)
                y = DrawFinishes(g, x, y, col, s) + 10 * s;

            // Run button: the yellow card with the command's icon
            if (!done && page.Command != null)
            {
                var run = new RectangleF(x, y, col - 3 * s, 46 * s);
                bool hover = Hovered(run);
                var shadow = run;
                shadow.Offset(3 * s, 3 * s);
                g.FillPath(Palette.Ink, GraphicsPath.GetRoundRect(shadow, 10 * s));
                g.FillPath(hover ? Color.FromArgb(0xFF, 0xDD, 0x70) : Palette.Accent, GraphicsPath.GetRoundRect(run, 10 * s));
                g.DrawPath(new Pen(Palette.Ink, 1.5f * s), GraphicsPath.GetRoundRect(run, 10 * s));
                var tile = new RectangleF(run.X + 8 * s, run.Y + 6 * s, 34 * s, 34 * s);
                g.FillPath(Palette.Paper, GraphicsPath.GetRoundRect(tile, 7 * s));
                g.DrawPath(new Pen(Palette.Ink, 1.3f * s), GraphicsPath.GetRoundRect(tile, 7 * s));
                Icons.Draw(g, page.Command, new RectangleF(tile.X + 5 * s, tile.Y + 5 * s, 24 * s, 24 * s), Palette.Ink);
                using Font bold = Typeface.Sans(10.5f * s, true);
                using Font small = Typeface.Sans(7.8f * s);
                g.DrawText(bold, Palette.Ink, tile.Right + 10 * s, run.Y + 7 * s, "Run " + page.Command);
                g.DrawText(small, Palette.Body, tile.Right + 10 * s, run.Y + 8 * s + Cards.H(g, bold), "Click here, or its yellow icon in the toolbar");
                string command = page.Command;
                Hit(run, () => FinSession.RunCommand(command));
                y = run.Bottom + 12 * s;
            }

            // A hint from the game, set off with a yellow bar
            string? hint = current ? FinSession.Hint : null;
            if (hint != null)
            {
                using Font italic = Typeface.Serif(9.5f * s, false, true);
                List<string> lines = Cards.Wrap(g, italic, hint, col - 12 * s).ToList();
                g.FillRectangle(Palette.Accent, x, y, 3 * s, lines.Count * Cards.H(g, italic) * 1.25f);
                foreach (string line in lines)
                {
                    g.DrawText(italic, Palette.Ink, x + 10 * s, y, line);
                    y += Cards.H(g, italic) * 1.25f;
                }
                y += 8 * s;
            }

            // Steps, lit up in time with the demo
            y = Section(g, x, y, s, "STEPS");
            using (Font step = Typeface.Sans(9.3f * s))
            using (Font stepBold = Typeface.Sans(9.3f * s, true))
            {
                for (int i = 0; i < page.Steps.Length; i++)
                {
                    bool on = demo.ActiveStep == i, dim = demo.ActiveStep >= 0 && !on;
                    float d = 17 * s;
                    Cards.Badge(g, x, y, d, (i + 1).ToString(), on, Palette.Accent, dim);
                    g.DrawText(on ? stepBold : step, dim ? Palette.Faint : Palette.Ink, x + d + 8 * s, y + (d - Cards.H(g, step)) / 2, page.Steps[i]);
                    y += d + 5 * s;
                }
            }
            y += 6 * s;

            // Three ways to run the command: on a short panel, one line instead of three rows
            if (page.Command != null && _compact)
            {
                y = Section(g, x, y, s, "WAYS TO DO IT");
                using Font plain = Typeface.Sans(8.6f * s, true);
                using Font cmd = Typeface.Mono(8.6f * s);
                y += Cards.Rich(g, $"Type *{page.Command}* · its toolbar icon · Run", x, y, col, plain, cmd, Palette.Ink) + 6 * s;
            }
            else if (page.Command != null)
            {
                y = Section(g, x, y, s, "WAYS TO DO IT");
                string[] ways =
                {
                    $"Type *{page.Command}* and press Enter",
                    "Click its yellow icon in the RhinoGotchi toolbar",
                    "Press Run above",
                };
                using Font plain = Typeface.Sans(8.8f * s, true);
                using Font cmd = Typeface.Mono(8.8f * s);
                for (int i = 0; i < ways.Length; i++)
                {
                    float rowH = 26 * s;
                    var row = new RectangleF(x, y, col, rowH);
                    g.FillPath(Palette.Wash, GraphicsPath.GetRoundRect(row, 8 * s));
                    float d = 16 * s;
                    g.FillEllipse(Palette.Ink, x + 8 * s, y + (rowH - d) / 2, d, d);
                    using Font num = Typeface.Sans(7 * s, true);
                    SizeF ns = g.MeasureString(num, (i + 1).ToString());
                    g.DrawText(num, Palette.Paper, x + 8 * s + (d - ns.Width) / 2, y + (rowH - ns.Height) / 2, (i + 1).ToString());
                    Cards.Rich(g, ways[i], x + d + 16 * s, y + (rowH - Cards.H(g, plain) * 1.25f) / 2, col - d - 20 * s, plain, cmd, Palette.Ink);
                    y += rowH + 4 * s;
                }
                y += 6 * s;
            }

            y = DrawCommandRow(g, x, y, col, s);
            return y;
        }

        // ---------------------------------------------------------------- pieces

        /// <summary>
        /// "PICK A FINISH": birch wood, matte aluminium, painted blue. Clicking one gives every fin
        /// that material; the one most fins have is ticked.
        /// </summary>
        private float DrawFinishes(Graphics g, float x, float y, float col, float s)
        {
            y = Section(g, x, y, s, "PICK A FINISH");
            using Font label = Typeface.Sans(7.6f * s, true);
            using Font note = Typeface.Sans(7.6f * s);
            float gap = 6 * s, w = (col - 2 * gap) / 3, h = 54 * s;
            Finish? chosen = FinSession.ChosenFinish;
            for (int i = 0; i < Finishes.All.Length; i++)
            {
                Finish f = Finishes.All[i];
                var r = new RectangleF(x + i * (w + gap), y, w, h);
                bool on = chosen == f;
                IGraphicsPath path = GraphicsPath.GetRoundRect(r, 9 * s);
                g.FillPath(Hovered(r) ? Palette.Wash : Palette.Paper, path);
                g.DrawPath(new Pen(on ? Palette.Ink : Palette.Rule, (on ? 2f : 1.2f) * s), path);

                // The swatch: the finish's colour, with grain lines for birch and a sheen for aluminium
                var chip = new RectangleF(r.X + 7 * s, r.Y + 7 * s, w - 14 * s, 22 * s);
                g.FillPath(Finishes.EtoColor(f), GraphicsPath.GetRoundRect(chip, 5 * s));
                g.SaveTransform();
                g.SetClip(chip);
                if (f == Finish.Birch)
                {
                    var grain = new Pen(Color.FromArgb(0xB0, 0x8E, 0x5C, 140), 0.8f * s);
                    for (int k = 0; k < 6; k++)
                    {
                        float gy = chip.Y + (k + 0.5f) * chip.Height / 6;
                        g.DrawLine(grain, chip.X, gy, chip.Right, gy + (float)Math.Sin(k * 1.7) * 2 * s);
                    }
                }
                else if (f == Finish.Aluminium)
                {
                    g.FillRectangle(Color.FromArgb(255, 255, 255, 70), chip.X, chip.Y, chip.Width, chip.Height * 0.35f);
                }
                g.ResetClip();
                g.RestoreTransform();
                g.DrawPath(new Pen(Palette.Ink, 1 * s), GraphicsPath.GetRoundRect(chip, 5 * s));

                string text = (on ? "✓ " : "") + Finishes.Label(f);
                SizeF ts = g.MeasureString(label, text);
                using Font fit = ts.Width > w - 6 * s ? Typeface.Sans(7.6f * s * (w - 6 * s) / ts.Width, true) : Typeface.Sans(7.6f * s, true);
                ts = g.MeasureString(fit, text);
                g.DrawText(fit, Palette.Ink, r.X + (w - ts.Width) / 2, chip.Bottom + (r.Bottom - chip.Bottom - ts.Height) / 2, text);

                Finish pick = f;
                Hit(r, () => FinSession.ApplyFinish(pick));
            }
            y += h + 4 * s;
            g.DrawText(note, Palette.Muted, x, y, "Or in Rhino: select the fins, Properties → Material.");
            return y + Cards.H(g, note);
        }

        private static float Section(Graphics g, float x, float y, float s, string title)
        {
            using Font kicker = Typeface.Sans(7.2f * s, true);
            Cards.Spaced(g, kicker, Palette.Muted, title, x, y, 1.4f * s);
            return y + Cards.H(g, kicker) + 5 * s;
        }

        /// <summary>
        /// "YOUR COMMANDS": the five command icons. Solved ones are green with a tick, the current
        /// one is yellow, later ones grey. Solved and current ones can be clicked to run.
        /// </summary>
        private float DrawCommandRow(Graphics g, float x, float y, float col, float s)
        {
            y = Section(g, x, y, s, "YOUR COMMANDS");
            using Font label = Typeface.Sans(6.6f * s, true);
            int n = Commands.Length;
            float gap = 5 * s, w = (col - gap * (n - 1)) / n, h = 44 * s;
            for (int i = 0; i < n; i++)
            {
                var stage = (Stage)i;
                var r = new RectangleF(x + i * (w + gap), y, w, h);
                bool done = FinSession.IsDone(stage), now = FinSession.Started && FinSession.Current == stage;
                bool enabled = done || now || !FinSession.Started;
                Color fill = now ? Palette.Accent : Hovered(r) && enabled ? Palette.Wash : Palette.Paper;
                Color edge = now ? Palette.Ink : done ? Color.FromArgb(0xBF, 0xE0, 0xCA) : Palette.Rule;
                Color ink = now ? Palette.Ink : done ? Palette.Green : Palette.Faint;
                IGraphicsPath path = GraphicsPath.GetRoundRect(r, 8 * s);
                g.FillPath(fill, path);
                g.DrawPath(new Pen(edge, (now ? 1.8f : 1.2f) * s), path);
                float icon = Math.Min(20 * s, w - 8 * s);
                Icons.Draw(g, Commands[i], new RectangleF(r.X + (w - icon) / 2, r.Y + 5 * s, icon, icon), ink);
                string text = (Commands[i] == "ApplyBoxMapping" ? "Mapping" : Commands[i]) + (done ? " ✓" : "");
                SizeF ts = g.MeasureString(label, text);
                using Font fit = ts.Width > w - 2 * s ? Typeface.Sans(6.6f * s * (w - 2 * s) / ts.Width, true) : Typeface.Sans(6.6f * s, true);
                ts = g.MeasureString(fit, text);
                g.DrawText(fit, ink, r.X + (w - ts.Width) / 2, r.Bottom - ts.Height - 4 * s, text);
                if (enabled)
                {
                    string command = Commands[i];
                    Hit(r, () => FinSession.RunCommand(command));
                }
            }
            return y + h;
        }

        /// <summary>The pet with its speech bubble beside it.</summary>
        private static float DrawPetRow(Graphics g, float x, float y, float col, float s, PetMood mood, double t, string speech)
        {
            float petW = 78 * s, petH = petW / Pet.Aspect(mood);
            SizeF bubble = Cards.Bubble(g, x + petW + 4 * s, y + 2 * s, s, speech, col - petW - 6 * s);
            float rowH = Math.Max(petH + 10 * s, bubble.Height + 4 * s);
            Pet.Draw(g, new RectangleF(x, y + rowH - petH, petW, petH), mood, t);
            return y + rowH;
        }

        /// <summary>The pet celebrating or losing, big, in place of the demo.</summary>
        private static void DrawPetMoment(Graphics g, RectangleF area, float s, PetMood mood, double t)
        {
            g.FillPath(mood == PetMood.Lose ? Palette.Wash : Palette.Cream, GraphicsPath.GetRoundRect(area, 12 * s));
            var inner = area;
            inner.Inflate(-area.Width * 0.18f, -area.Height * 0.12f);
            inner.Y += area.Height * 0.06f;
            Pet.Draw(g, inner, mood, t);
            string text = mood == PetMood.Lose ? $"{FinSession.MissesToLose} misses" : mood == PetMood.Final ? "Perfect run!" : mood == PetMood.Dance ? (FinSession.Perfect ? "Perfect run!" : "You did it!") : "Solved!";
            Toast(g, text, area.X + area.Width / 2, area.Y + 6 * s, s);
        }

        private static void Toast(Graphics g, string text, float cx, float y, float s)
        {
            using Font font = Typeface.Sans(8.5f * s, true);
            SizeF size = g.MeasureString(font, text);
            var r = new RectangleF(cx - size.Width / 2 - 12 * s, y, size.Width + 24 * s, size.Height + 10 * s);
            g.FillPath(Palette.Ink, GraphicsPath.GetRoundRect(r, r.Height / 2));
            g.DrawText(font, Palette.Paper, r.X + 12 * s, r.Y + 5 * s, text);
        }

        private void Pill(Graphics g, RectangleF r, string text, bool primary, float s, bool enabled = true)
        {
            IGraphicsPath path = GraphicsPath.GetRoundRect(r, r.Height / 2);
            bool hover = enabled && Hovered(r);
            if (primary)
                g.FillPath(!enabled ? Palette.Rule : hover ? Palette.Body : Palette.Ink, path);
            else
            {
                g.FillPath(hover ? Palette.Wash : Palette.Paper, path);
                g.DrawPath(new Pen(enabled ? Palette.Ink : Palette.Rule, 1.4f * s), path);
            }
            using Font font = Typeface.Sans(9.5f * s, true);
            SizeF size = g.MeasureString(font, text);
            Color ink = primary ? Palette.Paper : enabled ? Palette.Ink : Palette.Disabled;
            g.DrawText(font, ink, r.X + (r.Width - size.Width) / 2, r.Y + (r.Height - size.Height) / 2, text);
        }

        private void DrawFooter(Graphics g, float x, float y, float col, float h, float s, GamePage page)
        {
            float gap = 8 * s, w = (col - gap) / 2;
            var back = new RectangleF(x, y, w, h);
            var next = new RectangleF(x + w + gap, y, w, h);
            bool canBack = _page > 0, canNext = _page < MaxPage;
            Pill(g, back, "‹ Back", primary: false, s, canBack);
            Pill(g, next, "Next ›", primary: false, s, canNext);
            if (canBack)
                Hit(back, () => GoTo(_page - 1));
            if (canNext)
                Hit(next, () => GoTo(_page + 1));
        }

        // ---------------------------------------------------------------- the demo

        /// <summary>What the key axon shows at one moment: the state of each part, the cursor, the step.</summary>
        private sealed class Demo
        {
            /// <summary>Loop lengths in seconds.</summary>
            private const double WelcomeLoop = 20, MassLoop = 5, ProfileLoop = 7, FinLoop = 4.5, ArrayLoop = 7, SunLoop = 7;

            public int StageShown = -1;            // Welcome: the level the demo is on (0..4, 5 = finished)
            public int ActiveStep = -1;            // the step the demo is acting out, or -1

            public Solid? Mass;                    // the mass while it grows (null: use session state)
            public Point3d[]? MassBase;            // the flat base while the box is dragged out
            public bool? MassBuilt, ProfileBuilt, FinBuilt;
            public List<Point3d>? ProfilePath;     // the polyline being drawn
            public bool ProfileClosed;
            public double FinThickness = -1;       // < 0: none; else the growing fin
            public int FinsShown = -1;             // < 0: use session state
            public bool PathHighlight;
            public string? Typed;                  // a value being typed, e.g. "0.3"
            public Point3d? Cursor;
            public List<(Point3d At, double Since, bool Clicked)> Clicks = new List<(Point3d, double, bool)>();
            public bool CloseUp;
            public double Shadow = -1;             // < 0: no sun; 0..1 shadows fading in
            public bool SunSwitch;                 // show the Sun panel's On switch
            public bool SunSwitchOn;
            public double Turn;                    // degrees the camera has orbited
            public Finish? Tint;                   // the fins' finish
            public double MappingBox = -1;         // < 0: none; 0..1 the 2 × 2 × 2 texture box growing in
            public bool MappingLabel;

            private const double MaterialLoop = 7;

            public static Demo For(GamePage page, double t)
            {
                var d = new Demo();
                switch (page.Kind)
                {
                    case PageKind.Welcome:
                        d.Welcome(t % WelcomeLoop / WelcomeLoop);
                        break;
                    case PageKind.Done:
                        d.Shadow = 1;
                        d.Tint = FinSession.ChosenFinish ?? Finish.Birch;
                        d.Turn = 12 * Math.Sin(t * 0.6); // a slow, gentle sway
                        break;
                    case PageKind.Level when page.Stage.HasValue:
                        Stage stage = page.Stage.Value;
                        d.CloseUp = stage == Stage.Profile || stage == Stage.Fin;
                        if (FinSession.IsDone(stage))
                        {
                            if (stage >= Stage.Sun)
                                d.Shadow = 1;
                            if (stage == Stage.Material)
                                d.Tint = FinSession.ChosenFinish;
                            break; // browsing back: just show the result
                        }
                        switch (stage)
                        {
                            case Stage.Mass: d.MassDemo(t % MassLoop / MassLoop, clicks: true); break;
                            case Stage.Profile: d.ProfileDemo(t % ProfileLoop / ProfileLoop, clicks: true); break;
                            case Stage.Fin: d.FinDemo(t % FinLoop / FinLoop); break;
                            case Stage.Array: d.ArrayDemo(t % ArrayLoop / ArrayLoop, clicks: true); break;
                            case Stage.Sun: d.SunDemo(t % SunLoop / SunLoop); break;
                            case Stage.Material: d.MaterialDemo(t % MaterialLoop / MaterialLoop); break;
                        }
                        break;
                }
                return d;
            }

            /// <summary>The whole facade, start to finish: mass, profile, fin, array, sun, finish, hold.</summary>
            private void Welcome(double u)
            {
                double[] at = { 0, 0.15, 0.30, 0.39, 0.56, 0.72, 0.90 };
                StageShown = Enumerable.Range(0, 6).Count(i => u >= at[i + 1]);
                double Local(int i) => (u - at[i]) / (at[i + 1] - at[i]);

                MassBuilt = StageShown > 0;
                ProfileBuilt = StageShown > 1;
                FinBuilt = StageShown > 2;
                FinsShown = StageShown > 3 ? FinModel.FinCount : 0;
                if (StageShown > 4)
                    Shadow = 1;
                if (StageShown > 5)
                    Tint = Finish.Birch;

                switch (StageShown)
                {
                    case 0: MassDemo(Local(0), clicks: false); break;
                    case 1: ProfileDemo(Local(1), clicks: false); break;
                    case 2: FinDemo(Local(2)); break;
                    case 3: ArrayDemo(Local(3), clicks: false); break;
                    case 4: SunDemo(Local(4)); break;
                    case 5: MaterialDemo(Local(5)); break;
                }
                ActiveStep = -1; // the tutorial has no steps to light up
            }

            /// <summary>The fins try on each finish, then a 2 × 2 × 2 texture box sets the grain's scale.</summary>
            private void MaterialDemo(double u)
            {
                MassBuilt = ProfileBuilt = FinBuilt = true;
                FinsShown = FinModel.FinCount;
                Shadow = 1;
                Tint = u < 0.36 ? Finishes.All[(int)(u / 0.12) % 3] : Finish.Birch;
                MappingBox = u < 0.42 ? -1 : Ease((u - 0.42) / 0.2);
                MappingLabel = u >= 0.66;
                ActiveStep = u < 0.38 ? 0 : u < 0.66 ? 1 : u < 0.95 ? 2 : -1;
            }

            private void MassDemo(double u, bool clicks)
            {
                double[] clickAt = { 0.12, 0.45, 0.77 };
                Point3d p0 = FinModel.MassCornerA, p1 = FinModel.MassCornerB, p2 = FinModel.MassTop;
                double grow = Ease((u - clickAt[0]) / (clickAt[1] - clickAt[0]));
                double rise = Ease((u - clickAt[1] - 0.05) / (clickAt[2] - clickAt[1] - 0.05));

                Point3d q = p0 + (p1 - p0) * grow;
                Point3d[] ring =
                {
                    p0, new Point3d(q.X, p0.Y, 0), new Point3d(q.X, q.Y, 0), new Point3d(p0.X, q.Y, 0),
                };
                MassBuilt = false;
                if (rise > 0.01)
                    Mass = Solid.Extrude(ring, new Vector3d(0, 0, FinModel.Height * rise));
                else if (grow > 0.01)
                    MassBase = ring;

                Point3d top = new Point3d(q.X, q.Y, FinModel.Height * rise);
                if (u < clickAt[0])
                {
                    Point3d start = p0 + new Vector3d(-6, -8, 6);
                    Cursor = start + (p0 - start) * Ease(u / clickAt[0]);
                }
                else
                {
                    Cursor = u < clickAt[1] + 0.05 ? q : top;
                }
                if (u > 0.92)
                    Cursor = null;
                ActiveStep = u < clickAt[0] + 0.02 ? 0 : u < clickAt[1] + 0.02 ? 1 : u < 0.92 ? 2 : -1;
                if (clicks)
                    AddClicks(new[] { p0, p1, p2 }, clickAt, u);
            }

            private void ProfileDemo(double u, bool clicks)
            {
                Point3d[] p = FinModel.Profile;
                var targets = p.Concat(new[] { p[0] }).ToArray(); // the last click closes it
                double first = 0.08, last = 0.82;
                double[] clickAt = Enumerable.Range(0, targets.Length)
                    .Select(i => first + i * (last - first) / (targets.Length - 1)).ToArray();

                int clicked = clickAt.Count(c => u >= c);
                ProfileBuilt = false;
                ProfilePath = targets.Take(clicked).ToList();
                ProfileClosed = clicked == targets.Length;

                if (clicked == 0)
                {
                    Point3d start = p[0] + new Vector3d(1, -3, -1.5);
                    Cursor = start + (p[0] - start) * Ease(u / first);
                }
                else if (clicked < targets.Length)
                {
                    double k = (u - clickAt[clicked - 1]) / (clickAt[clicked] - clickAt[clicked - 1]);
                    Cursor = targets[clicked - 1] + (targets[clicked] - targets[clicked - 1]) * Ease(k / 0.75);
                    ProfilePath.Add(Cursor.Value); // the rubber band to the cursor
                }
                if (u > 0.9)
                    Cursor = null;
                ActiveStep = clicked < p.Length ? 0 : u < 0.9 ? 1 : -1;
                if (clicks)
                    AddClicks(p, clickAt.Take(p.Length).ToArray(), u);
            }

            private void FinDemo(double u)
            {
                ProfileBuilt = true;
                FinBuilt = false;
                Typed = u < 0.18 ? null : u < 0.26 ? "0" : u < 0.32 ? "0." : FinModel.ThicknessText;
                FinThickness = FinModel.Thickness * Ease((u - 0.38) / 0.35);
                if (u > 0.85)
                    Typed = null;
                ActiveStep = u < 0.16 ? 0 : u < 0.85 ? 1 : -1;
            }

            private void ArrayDemo(double u, bool clicks)
            {
                FinBuilt = true;
                PathHighlight = true;
                const double clickAt = 0.15;
                Point3d pick = new Point3d(FinModel.Width * 0.55, 0, 0);
                if (u < clickAt)
                {
                    Point3d start = pick + new Vector3d(4, -10, 6);
                    Cursor = start + (pick - start) * Ease(u / clickAt);
                }
                else if (u < 0.3)
                {
                    Cursor = pick;
                }
                FinsShown = (int)Math.Round(FinModel.FinCount * Ease((u - 0.3) / 0.55));
                ActiveStep = u < clickAt + 0.03 ? 0 : u < 0.3 ? 1 : u < 0.9 ? 2 : -1;
                if (clicks)
                    AddClicks(new[] { pick }, new[] { clickAt }, u);
            }

            /// <summary>Flip the Sun on, shadows fade in, then orbit out and back.</summary>
            private void SunDemo(double u)
            {
                MassBuilt = ProfileBuilt = FinBuilt = true;
                FinsShown = FinModel.FinCount;
                SunSwitch = true;
                SunSwitchOn = u >= 0.15;
                Shadow = u < 0.15 ? -1 : Ease((u - 0.15) / 0.15);
                Turn = 55 * Math.Sin(Math.PI * Ease((u - 0.38) / 0.52));
                ActiveStep = u < 0.3 ? 0 : u < 0.92 ? 1 : -1;
            }

            private void AddClicks(Point3d[] points, double[] clickAt, double u)
            {
                for (int i = 0; i < points.Length; i++)
                    Clicks.Add((points[i], u - clickAt[i], u >= clickAt[i]));
            }

            public static double Ease(double x)
            {
                x = Math.Max(0, Math.Min(1, x));
                return x * x * (3 - 2 * x);
            }
        }

        // ---------------------------------------------------------------- key axon

        /// <summary>
        /// Draws the facade as a key axon from the Axon view's direction (turned by the demo's
        /// orbit). Built parts are solid (white and grey faces, black edges); parts still to
        /// build are a faint ghost and the current one is dashed. With the sun on, every solid
        /// casts a grey shadow on the ground. Profile and fin levels are a close-up of the corner.
        /// </summary>
        private static void DrawArt(Graphics g, RectangleF area, float s, GamePage page, Demo demo)
        {
            Vector3d dir = FinModel.ViewDirectionTurned(demo.Turn);
            Vector3d right = FinModel.Normalized(Vector3d.CrossProduct(dir, Vector3d.ZAxis));
            Vector3d up = FinModel.Normalized(Vector3d.CrossProduct(right, dir));

            // Fit the frame. While orbiting, fit a cylinder around the building so it doesn't pump.
            BoundingBox frame = demo.CloseUp ? FinModel.FinCloseUp : FinModel.Bounds;
            float scale, ox, oy;
            if (demo.CloseUp)
            {
                Point3d[] corners = frame.GetCorners();
                float minX = corners.Min(p => Dot(p, right)), maxX = corners.Max(p => Dot(p, right));
                float minY = corners.Min(p => -Dot(p, up)), maxY = corners.Max(p => -Dot(p, up));
                scale = Math.Min(area.Width / (maxX - minX), area.Height / (maxY - minY)) * 0.94f;
                ox = area.X + area.Width / 2 - (minX + maxX) / 2 * scale;
                oy = area.Y + area.Height / 2 - (minY + maxY) / 2 * scale;
            }
            else
            {
                Point3d c = frame.Center;
                double radius = Math.Sqrt(Math.Pow(frame.Diagonal.X, 2) + Math.Pow(frame.Diagonal.Y, 2)) / 2;
                double halfH = Math.Abs(up.Z) * frame.Diagonal.Z / 2 + Math.Sqrt(1 - up.Z * up.Z) * radius;
                scale = (float)Math.Min(area.Width / (2 * radius), area.Height / (2 * halfH)) * 0.96f;
                ox = area.X + area.Width / 2 - Dot(c, right) * scale;
                oy = area.Y + area.Height / 2 + Dot(c, up) * scale;
            }
            PointF P(Point3d p) => new PointF(ox + Dot(p, right) * scale, oy - Dot(p, up) * scale);

            g.SaveTransform();
            g.SetClip(area);

            bool started = FinSession.Started || page.Kind == PageKind.Welcome;
            bool massBuilt = demo.MassBuilt ?? FinSession.IsDone(Stage.Mass);
            bool profileBuilt = demo.ProfileBuilt ?? FinSession.IsDone(Stage.Profile);
            bool finBuilt = demo.FinBuilt ?? FinSession.IsDone(Stage.Fin);
            int finsShown = demo.FinsShown >= 0 ? demo.FinsShown : FinSession.IsDone(Stage.Array) ? FinModel.FinCount : 0;
            Stage? target = page.Kind == PageKind.Level ? page.Stage : null;
            float lineW = (demo.CloseUp ? 1.4f : 1.0f) * s;

            // Which solids are shown solid
            var solids = new List<(Solid Solid, int Layer)>();
            const int Behind = 0, MassLayer = 1, InFront = 2;
            if (massBuilt)
                solids.Add((FinModel.Mass, MassLayer));
            else if (demo.Mass != null)
                solids.Add((demo.Mass, MassLayer));
            if (finsShown > 0)
            {
                for (int i = 0; i < finsShown; i++)
                {
                    // Fins on the far sides of the mass are drawn before it, the rest after
                    Vector3d side = FinModel.SideNormal(i * FinModel.Perimeter / FinModel.FinCount);
                    solids.Add((FinModel.ArrayedFins[i], side * dir > 0 ? Behind : InFront));
                }
            }
            else if (finBuilt)
            {
                solids.Add((FinModel.Fin, InFront));
            }
            if (demo.FinThickness > 0.005)
                solids.Add((Solid.Extrude(FinModel.Profile, new Vector3d(demo.FinThickness, 0, 0)), InFront));

            // 0. Shadows on the ground, under everything
            if (demo.Shadow > 0)
            {
                Vector3d sun = FinModel.DemoSun;
                Color shade = Blend(Palette.Paper, Palette.Shadow, (float)demo.Shadow);
                foreach (var (solid, _) in solids)
                {
                    PointF[] hull = ConvexHull(solid.Vertices.Select(v => P(v - sun * (v.Z / sun.Z))).ToList());
                    if (hull.Length > 2)
                        g.FillPolygon(shade, hull);
                }
            }

            // 1. Ghosts: the goal, faint; the current level's target, dashed
            var ghostEdge = new Pen(Palette.Rule, 0.8f * s);
            var targetEdge = new Pen(Palette.Ink, 1.1f * s) { DashStyle = DashStyles.Dash };
            void Ghost(Solid solid, bool isTarget)
            {
                foreach (int[] face in solid.Faces.Where(f => Facing(solid.Vertices, f, dir)))
                    g.FillPolygon(Palette.GhostFill, face.Select(i => P(solid.Vertices[i])).ToArray());
                foreach (var (a, b) in solid.Edges)
                    g.DrawLine(isTarget ? targetEdge : ghostEdge, P(solid.Vertices[a]), P(solid.Vertices[b]));
            }

            if (!massBuilt && demo.Mass == null)
                Ghost(FinModel.Mass, target == Stage.Mass);
            if (finsShown < FinModel.FinCount && (started || page.Kind == PageKind.Done))
            {
                for (int i = Math.Max(finsShown, finBuilt ? 1 : 0); i < FinModel.FinCount; i++)
                    Ghost(FinModel.ArrayedFins[i], false);
            }
            if (!finBuilt && demo.FinThickness < 0 && target == Stage.Fin)
                Ghost(FinModel.Fin, true);

            // The path sits on the ground, so the mass and fins cover it where they should
            if (demo.PathHighlight)
            {
                var path = new Pen(Palette.Accent, 4 * s) { LineJoin = PenLineJoin.Round, LineCap = PenLineCap.Round };
                PointF[] ring = FinModel.PathCorners.Select(P).ToArray();
                for (int i = 0; i + 1 < ring.Length; i++)
                    g.DrawLine(path, ring[i], ring[i + 1]);
            }

            // 2. Solids in three layers, each far to near: fins behind the mass, the mass, then
            //    fins in front of it. Sorting single faces by depth alone fails here, because the
            //    mass's faces are so much bigger than a fin's.
            var faces = new List<(int Layer, double Depth, PointF[] Points, Color Fill)>();
            foreach (var (solid, layer) in solids)
            {
                foreach (int[] face in solid.Faces.Where(f => Facing(solid.Vertices, f, dir)))
                {
                    Vector3d normal = FinModel.Newell(face.Select(i => solid.Vertices[i]).ToArray());
                    Color fill = demo.Tint.HasValue && layer != MassLayer
                        ? Tinted(Finishes.EtoColor(demo.Tint.Value), normal, demo.Shadow > 0)
                        : Shade(normal, demo.Shadow > 0);
                    faces.Add((layer, Dot(Centroid(solid.Vertices, face), dir), face.Select(i => P(solid.Vertices[i])).ToArray(), fill));
                }
            }

            var edgePen = new Pen(Palette.Ink, lineW) { LineJoin = PenLineJoin.Round };
            foreach (var f in faces.OrderBy(f => f.Layer).ThenByDescending(f => f.Depth))
            {
                g.FillPolygon(f.Fill, f.Points);
                g.DrawPolygon(edgePen, f.Points);
            }

            // 3. Flat things on top: the mass base while it's dragged, the profile
            if (demo.MassBase != null)
            {
                PointF[] flat = demo.MassBase.Select(P).ToArray();
                g.FillPolygon(Palette.Light, flat);
                g.DrawPolygon(edgePen, flat);
            }

            var profilePen = new Pen(Palette.Ink, 1.6f * s) { LineJoin = PenLineJoin.Round, LineCap = PenLineCap.Round };
            bool profileIsTarget = target == Stage.Profile && !profileBuilt;
            bool profileShown = profileBuilt && !finBuilt && demo.FinThickness <= 0.005;
            if (profileIsTarget || profileShown)
            {
                Pen pen = profileIsTarget ? targetEdge : profilePen;
                PointF[] outline = FinModel.Profile.Select(P).ToArray();
                for (int i = 0; i < outline.Length; i++)
                    g.DrawLine(pen, outline[i], outline[(i + 1) % outline.Length]);
            }
            if (demo.ProfilePath != null && demo.ProfilePath.Count > 1)
            {
                PointF[] drawn = demo.ProfilePath.Select(P).ToArray();
                for (int i = 0; i + 1 < drawn.Length; i++)
                    g.DrawLine(profilePen, drawn[i], drawn[i + 1]);
                if (demo.ProfileClosed)
                    g.FillPolygon(Color.FromArgb(0x12, 0x12, 0x12, 0x18), FinModel.Profile.Select(P).ToArray());
            }

            // 4. Click points, ripples, cursor, typed value, sun switch, orbit arrow
            foreach (var (at, since, clicked) in demo.Clicks)
            {
                PointF p = P(at);
                float r = (clicked ? 4.5f : 3.5f) * s;
                g.FillEllipse(clicked ? Palette.Accent : Palette.Paper, p.X - r, p.Y - r, 2 * r, 2 * r);
                g.DrawEllipse(new Pen(Palette.Ink, 1.1f * s), p.X - r, p.Y - r, 2 * r, 2 * r);
                if (since >= 0 && since < 0.08)
                {
                    float rr = (float)(4 + since / 0.08 * 12) * s;
                    int alpha = (int)(255 * (1 - since / 0.08));
                    g.DrawEllipse(new Pen(Color.FromArgb(0x12, 0x12, 0x12, alpha), 1.3f * s), p.X - rr, p.Y - rr, 2 * rr, 2 * rr);
                }
            }
            if (demo.Cursor.HasValue)
                DrawCursor(g, P(demo.Cursor.Value), s);
            if (demo.Typed != null)
                DrawTyped(g, P(new Point3d(FinModel.Thickness, FinModel.Profile[5].Y, FinModel.Height)), demo.Typed, s);
            if (demo.SunSwitch)
                DrawSunSwitch(g, area, demo.SunSwitchOn, s);
            if (Math.Abs(demo.Turn) > 1 && page.Kind == PageKind.Level)
                DrawOrbitArrow(g, area, s);

            // The texture box: a dashed 2 × 2 × 2 cube on the first fin, growing in
            if (demo.MappingBox > 0)
            {
                double half = Finishes.MappingSize / 2 * demo.MappingBox;
                var c = new Point3d(FinModel.Thickness / 2, -1.25, FinModel.Height / 2);
                var cube = new BoundingBox(c - new Vector3d(half, half, half), c + new Vector3d(half, half, half));
                Point3d[] k = cube.GetCorners();
                var dash = new Pen(Palette.Ink, 1.4f * s) { DashStyle = DashStyles.Dash };
                int[][] edges = { new[] { 0, 1 }, new[] { 1, 2 }, new[] { 2, 3 }, new[] { 3, 0 }, new[] { 4, 5 }, new[] { 5, 6 }, new[] { 6, 7 }, new[] { 7, 4 }, new[] { 0, 4 }, new[] { 1, 5 }, new[] { 2, 6 }, new[] { 3, 7 } };
                foreach (int[] e in edges)
                    g.DrawLine(dash, P(k[e[0]]), P(k[e[1]]));
                if (demo.MappingLabel)
                {
                    using Font label = Typeface.Mono(8 * s);
                    const string text = "2 × 2 × 2";
                    SizeF ts = g.MeasureString(label, text);
                    PointF at = P(c + new Vector3d(0, 0, half));
                    var r = new RectangleF(at.X - ts.Width / 2 - 5 * s, at.Y - ts.Height - 12 * s, ts.Width + 10 * s, ts.Height + 4 * s);
                    g.FillPath(Palette.Ink, GraphicsPath.GetRoundRect(r, 3 * s));
                    g.DrawText(label, Palette.Paper, r.X + 5 * s, r.Y + 2 * s, text);
                }
            }

            g.RestoreTransform();
            g.ResetClip();
        }

        /// <summary>A small text box with a caret, as if typing on the command line.</summary>
        private static void DrawTyped(Graphics g, PointF at, string text, float s)
        {
            using (Font typed = Typeface.Sans(Math.Max(6f, 9 * s), true))
            {
                text += "|";
                SizeF size = g.MeasureString(typed, text);
                var box = new RectangleF(at.X + 10 * s, at.Y - size.Height - 8 * s, size.Width + 12 * s, size.Height + 6 * s);
                g.FillRectangle(Palette.Paper, box);
                g.DrawRectangle(new Pen(Palette.Ink, 1 * s), box);
                g.DrawText(typed, Palette.Ink, box.X + 6 * s, box.Y + 3 * s, text);
            }
        }

        /// <summary>The Sun panel's On switch in miniature, top-right, with a sun icon.</summary>
        private static void DrawSunSwitch(Graphics g, RectangleF area, bool on, float s)
        {
            float x = area.Right - 74 * s, y = area.Y + 8 * s;
            var card = new RectangleF(x, y, 66 * s, 24 * s);
            g.FillRectangle(Palette.Paper, card);
            g.DrawRectangle(new Pen(Palette.Ink, 1 * s), card);

            // Sun icon
            float sx = x + 13 * s, sy = y + 12 * s, r = 4.5f * s;
            g.FillEllipse(on ? Palette.Accent : Palette.Paper, sx - r, sy - r, 2 * r, 2 * r);
            g.DrawEllipse(new Pen(Palette.Ink, 1.1f * s), sx - r, sy - r, 2 * r, 2 * r);
            var ray = new Pen(Palette.Ink, 1.1f * s) { LineCap = PenLineCap.Round };
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4;
                g.DrawLine(ray, sx + (float)Math.Cos(a) * r * 1.5f, sy + (float)Math.Sin(a) * r * 1.5f,
                           sx + (float)Math.Cos(a) * r * 2.1f, sy + (float)Math.Sin(a) * r * 2.1f);
            }

            // Toggle
            var track = new RectangleF(x + 30 * s, y + 6 * s, 28 * s, 12 * s);
            g.FillPath(on ? Palette.Ink : Palette.Rule, GraphicsPath.GetRoundRect(track, 6 * s));
            float knob = on ? track.Right - 11 * s : track.X + 1 * s;
            g.FillEllipse(Palette.Paper, knob, track.Y + 1 * s, 10 * s, 10 * s);
        }

        /// <summary>A curved double arrow under the building: drag to orbit.</summary>
        private static void DrawOrbitArrow(Graphics g, RectangleF area, float s)
        {
            float cx = area.X + area.Width / 2, cy = area.Bottom - 14 * s;
            float rx = area.Width * 0.3f, ry = 7 * s;
            PointF At(double a) => new PointF(cx + (float)(rx * Math.Cos(a)), cy + (float)(ry * Math.Sin(a)));
            var pen = new Pen(Palette.Ink, 1.6f * s) { LineCap = PenLineCap.Round };
            const double start = 0.1 * Math.PI, end = 0.9 * Math.PI;
            for (int i = 0; i < 24; i++)
                g.DrawLine(pen, At(start + (end - start) * i / 24), At(start + (end - start) * (i + 1) / 24));
            foreach (var (tip, back) in new[] { (At(start), At(start + 0.15)), (At(end), At(end - 0.15)) })
            {
                float dx = tip.X - back.X, dy = tip.Y - back.Y, len = (float)Math.Sqrt(dx * dx + dy * dy);
                dx /= len; dy /= len;
                g.FillPolygon(Palette.Ink,
                    tip,
                    new PointF(tip.X - 7 * s * dx + 4 * s * dy, tip.Y - 7 * s * dy - 4 * s * dx),
                    new PointF(tip.X - 7 * s * dx - 4 * s * dy, tip.Y - 7 * s * dy + 4 * s * dx));
            }
        }

        /// <summary>A small arrow pointer with its tip at <paramref name="tip"/>.</summary>
        private static void DrawCursor(Graphics g, PointF tip, float s)
        {
            float k = 0.9f * s;
            PointF[] shape =
            {
                new PointF(0, 0), new PointF(0, 15), new PointF(4, 11.5f), new PointF(7, 17.5f),
                new PointF(9.5f, 16.5f), new PointF(6.5f, 10.5f), new PointF(11, 10.5f),
            };
            PointF[] points = shape.Select(p => new PointF(tip.X + p.X * k, tip.Y + p.Y * k)).ToArray();
            g.FillPolygon(Palette.Paper, points);
            g.DrawPolygon(new Pen(Palette.Ink, 1.1f * s) { LineJoin = PenLineJoin.Round }, points);
        }

        // ---------------------------------------------------------------- geometry helpers

        private static float Dot(Point3d p, Vector3d v) => (float)(p.X * v.X + p.Y * v.Y + p.Z * v.Z);

        private static Point3d Centroid(Point3d[] vertices, int[] face)
        {
            double x = 0, y = 0, z = 0;
            foreach (int i in face)
            {
                x += vertices[i].X;
                y += vertices[i].Y;
                z += vertices[i].Z;
            }
            return new Point3d(x / face.Length, y / face.Length, z / face.Length);
        }

        private static bool Facing(Point3d[] vertices, int[] face, Vector3d dir) =>
            FinModel.Newell(face.Select(i => vertices[i]).ToArray()) * dir < 0;

        private static readonly Vector3d Light = FinModel.Normalized(new Vector3d(-0.3, -0.8, 0.9));

        /// <summary>A finish's colour, darkened on faces turned away from the light like <see cref="Shade"/>.</summary>
        private static Color Tinted(Color finish, Vector3d normal, bool sunOn)
        {
            double lit = sunOn ? -(normal * FinModel.DemoSun) : normal * Light;
            float k = sunOn ? (lit > 0.6 ? 1f : lit > 0.1 ? 0.86f : 0.68f) : (lit > 0.7 ? 1f : lit > 0.3 ? 0.86f : 0.68f);
            return Color.FromArgb((int)(finish.Rb * k), (int)(finish.Gb * k), (int)(finish.Bb * k));
        }

        /// <summary>Three greys from a fixed light; with the sun on, faces turned away from it go darker.</summary>
        private static Color Shade(Vector3d normal, bool sunOn)
        {
            if (sunOn)
            {
                double toSun = -(normal * FinModel.DemoSun);
                return toSun > 0.6 ? Palette.Paper : toSun > 0.1 ? Palette.Light : Palette.Shadow;
            }
            double lit = normal * Light;
            return lit > 0.7 ? Palette.Paper : lit > 0.3 ? Palette.Light : Palette.Shadow;
        }

        private static Color Blend(Color a, Color b, float t) =>
            Color.FromArgb((int)(a.Rb + (b.Rb - a.Rb) * t), (int)(a.Gb + (b.Gb - a.Gb) * t), (int)(a.Bb + (b.Bb - a.Bb) * t));

        /// <summary>Monotone chain convex hull, for the outline of a shadow.</summary>
        private static PointF[] ConvexHull(List<PointF> points)
        {
            var p = points.Distinct().OrderBy(q => q.X).ThenBy(q => q.Y).ToList();
            if (p.Count < 3)
                return p.ToArray();
            float Cross(PointF o, PointF a, PointF b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
            var hull = new List<PointF>();
            for (int pass = 0; pass < 2; pass++)
            {
                int start = hull.Count;
                foreach (PointF q in pass == 0 ? p : Enumerable.Reverse(p))
                {
                    while (hull.Count >= start + 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], q) <= 0)
                        hull.RemoveAt(hull.Count - 1);
                    hull.Add(q);
                }
                hull.RemoveAt(hull.Count - 1);
            }
            return hull.ToArray();
        }

        // ---------------------------------------------------------------- text

        private static float H(Graphics g, Font font) => Cards.H(g, font);
    }
}
