using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Rhino.Display;
using Rhino.Geometry;
using static RhinoGotchi.Tutorials.RhinoOnTheStreet.StreetLessons;
using static RhinoGotchi.Tutorials.RhinoOnTheStreet.StreetScene;

namespace RhinoGotchi.Tutorials.RhinoOnTheStreet
{
    /// <summary>
    /// Draws the tutorial: the ghost street and arch in every view, and in the
    /// RhinoOnTheStreet view the coloured targets, arrows, lesson card, captions,
    /// the rhino and its speech bubble, the full-view cards and the blue horizon.
    /// Note: helper methods must not reuse a DisplayConduit method name (DrawOverlay,
    /// DrawForeground, PostDrawObjects...). Enabling the conduit looks overrides up by
    /// name and throws AmbiguousMatchException on a clash.
    /// </summary>
    internal sealed class StreetConduit : DisplayConduit
    {
        private const string Font = "Arial";
        private static readonly Color Ghost = Color.FromArgb(150, 190, 255);
        private static readonly Color Paper = Color.White;
        private static readonly Color Muted = Color.FromArgb(85, 85, 85);

        private readonly StreetTutorial _t;
        private readonly DisplayMaterial _ghostMat = new DisplayMaterial(Ghost, 0.78);
        private readonly DisplayMaterial _paveMat = new DisplayMaterial(Color.FromArgb(239, 234, 223));
        private readonly DisplayMaterial _stoneMat = new DisplayMaterial(Color.FromArgb(242, 242, 242));
        private readonly Dictionary<Mood, DisplayBitmap?> _sprites = new Dictionary<Mood, DisplayBitmap?>();
        private readonly (float x, float delay, float speed, Color color, int size)[] _confetti;

        public StreetConduit(StreetTutorial tutorial)
        {
            _t = tutorial;
            _sprites[Mood.Win] = LoadSprite("win.png");
            _sprites[Mood.Standby] = LoadSprite("standby.png");
            _sprites[Mood.Lose] = LoadSprite("lose.png");
            _sprites[Mood.Final] = LoadSprite("final.png");
            var rnd = new Random(7);
            Color[] cols = { Red, Green, Blue, Orange };
            _confetti = new (float, float, float, Color, int)[50];
            for (int i = 0; i < _confetti.Length; i++)
                _confetti[i] = ((float)rnd.NextDouble(), (float)(rnd.NextDouble() * 0.9), 160 + (float)rnd.NextDouble() * 160, cols[i % 4], 7 + rnd.Next(4));
        }

        private static DisplayBitmap? LoadSprite(string file)
        {
            try
            {
                string dir = Path.GetDirectoryName(typeof(StreetConduit).Assembly.Location) ?? "";
                string path = Path.Combine(dir, "Assets", "RhinoOnTheStreet", file);
                return File.Exists(path) ? DisplayBitmap.Load(path) : null;
            }
            catch { return null; }
        }

        protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e)
        {
            var box = new BoundingBox(new Point3d(-6, -1, 0), new Point3d(6, ArchY + 1, ArchSpring + ArchRadius + 1));
            e.IncludeBoundingBox(box);
        }

        // ================= 3D =================

        protected override void PostDrawObjects(DrawEventArgs e)
        {
            DisplayPipeline d = e.Display;

            // street: ghost until paved
            if (!_t.StreetBuilt)
            {
                if (_t.Pave > 0)
                    d.DrawMeshShaded(RibbonMesh((int)(StreetSamples.Length * _t.Pave)), _paveMat);
                d.DrawMeshShaded(StreetMesh, _ghostMat);
                d.DrawPolyline(StreetLeft, Ghost, 2);
                d.DrawPolyline(StreetRight, Ghost, 2);
                d.DrawDottedPolyline(StreetSamples, Ghost, false);
                if (_t.Lesson?.Number == 1)
                    d.DrawDottedPolyline(StreetControlPoints, Ghost, false);
            }

            // arch: ghost until extruded
            if (!_t.ArchBuilt)
            {
                if (_t.ArchPreview != null)
                {
                    d.DrawBrepShaded(_t.ArchPreview, _stoneMat);
                    d.DrawBrepWires(_t.ArchPreview, Ink, 1);
                }
                else if (ArchGhost != null)
                {
                    d.DrawBrepShaded(ArchGhost, _ghostMat);
                    d.DrawBrepWires(ArchGhost, Ghost, 1);
                }
            }

            // targets
            Lesson? l = _t.Lesson;
            Step? cur = _t.Step;
            if (l == null || !(_t.Interactive || _t.Phase == Phase.Lose)) return;
            for (int i = _t.StepIndex + 1; i < l.Steps.Count; i++)
            {
                Step s = l.Steps[i];
                if (s.Kind == StepKind.Point)
                    d.DrawPoint(s.Target, PointStyle.RoundControlPoint, 5, ColorOf(s.Group));
            }
            if (cur != null && cur.Kind == StepKind.Point)
            {
                Color c = ColorOf(cur.Group);
                double pulse = (Math.Sin(_t.Now * 5) + 1) / 2;
                d.DrawPoint(cur.Target, PointStyle.Circle, (int)(12 + 9 * pulse), c);
                d.DrawPoint(cur.Target, PointStyle.RoundSimple, 7, c);
                // pillar tops: show the upward direction
                if (cur.Label == "Pillar top")
                {
                    Point3d b = new Point3d(cur.Target.X, cur.Target.Y, 0);
                    d.DrawArrow(new Line(b, cur.Target + new Vector3d(0, 0, -0.35)), c);
                }
            }
        }

        // ================= 2D overlay =================

        protected override void DrawForeground(DrawEventArgs e)
        {
            if (e.Viewport.Id != _t.ViewportId) return;
            DisplayPipeline d = e.Display;
            Rectangle vb = e.Viewport.Bounds;
            int w = vb.Width, h = vb.Height;
            double now = _t.Now;

            if (_t.Horizon > 0) DrawHorizon(d, e.Viewport, w, h, _t.Horizon);
            if (_t.RhinoWalk.HasValue) DrawWalker(d, e.Viewport, _t.RhinoWalk.Value, now);

            if (_t.BigCaption != null) DrawBigCaption(d, w, _t.BigCaption, _t.BigSub ?? "");

            if (_t.Lesson != null && (_t.Phase == Phase.Lesson || _t.Phase == Phase.Building || _t.Phase == Phase.Lose))
                DrawCard(d, _t.Lesson);

            Step? s = _t.Step;
            if (_t.Interactive && s != null)
            {
                DrawCaption(d, w, h, s);
                if (s.Kind == StepKind.Point) DrawCallout(d, e.Viewport, w, h, s, now);
            }

            foreach (var m in _t.MissMarks)
            {
                double k = (now - m.t) / 0.9;
                int r = (int)(10 + 8 * k);
                Color c = Color.FromArgb((int)(255 * (1 - k)), Red);
                d.Draw2dLine(new PointF(m.at.X - r, m.at.Y - r), new PointF(m.at.X + r, m.at.Y + r), c, 4);
                d.Draw2dLine(new PointF(m.at.X + r, m.at.Y - r), new PointF(m.at.X - r, m.at.Y + r), c, 4);
            }

            if (_t.Phase != Phase.Finale && _t.Overlay == null) DrawPet(d, h, now);
            if (_t.Overlay != null) DrawMessageCard(d, w, h, _t.Overlay, now);
        }

        private void DrawPet(DisplayPipeline d, int h, double now)
        {
            DisplayBitmap? bmp = Sprite(_t.Mood);
            const int ph = 100;
            float bob = _t.Mood == Mood.Win ? (float)(Math.Abs(Math.Sin(now * 4.5)) * 6) : 0;
            float pw = bmp != null ? ph * Aspect(bmp) : ph;
            if (bmp != null)
                d.DrawSprite(bmp, new Point2d(18 + pw / 2, h - 18 - ph / 2 - bob), pw, ph);
            if (!string.IsNullOrEmpty(_t.Bubble))
            {
                var at = new Point2d(30 + pw, h - 90);
                Rectangle r = Box(d, _t.Bubble!, at, 14, 12, 8);
                d.Draw2dRectangle(r, Ink, 2, Paper);
                d.Draw2dText(_t.Bubble, Ink, new Point2d(r.X + 12, r.Y + 7), false, 14, Font);
            }
        }

        private void DrawCard(DisplayPipeline d, Lesson l)
        {
            const int x = 16, y = 16, cw = 330;
            int rows = 3, ch = 108 + rows * 40 + 26;
            d.Draw2dRectangle(new Rectangle(x + 3, y + 4, cw, ch), Color.Transparent, 0, Ink);
            d.Draw2dRectangle(new Rectangle(x, y, cw, ch), Ink, 2, Paper);
            d.Draw2dText($"LESSON {l.Number} OF 2  -  {l.Name.ToUpperInvariant()}", Orange, new Point2d(x + 14, y + 12), false, 12, Font);
            d.Draw2dText(l.Title, Ink, new Point2d(x + 14, y + 30), false, 26, Font);
            d.Draw2dText(l.Description, Muted, new Point2d(x + 14, y + 66), false, 12, Font);

            Step? cur = _t.Step;
            for (int g = 0; g < 3; g++)
            {
                var grp = (StepGroup)g;
                bool done = _t.Phase == Phase.Building && cur == null || IsGroupDone(l, grp);
                bool now = cur != null && cur.Group == grp && !done;
                int ry = y + 96 + g * 40;
                if (now) d.Draw2dRectangle(new Rectangle(x + 8, ry - 4, cw - 16, 38), Ink, 2, Color.FromArgb(250, 250, 250));
                Color c = ColorOf(grp);
                d.Draw2dRectangle(new Rectangle(x + 16, ry + 4, 16, 16), c, 1, done ? Color.FromArgb(120, c) : c);
                if (done) d.Draw2dText("v", Paper, new Point2d(x + 24, ry + 12), true, 12, Font);
                Color tc = done ? Color.FromArgb(150, 150, 150) : Ink;
                d.Draw2dText(l.GroupTitles[g], tc, new Point2d(x + 42, ry + 1), false, 14, Font);
                d.Draw2dText(l.GroupHints[g], done ? tc : Muted, new Point2d(x + 42, ry + 18), false, 11, Font);
            }
            int my = y + ch - 24;
            d.Draw2dLine(new PointF(x + 12, my - 6), new PointF(x + cw - 12, my - 6), Color.FromArgb(215, 215, 215), 1);
            d.Draw2dText($"MISSES {_t.MissesThisLesson}   |   TOTAL {_t.Misses}   |   {(_t.Flawless ? "perfect so far" : "not perfect")}", Muted, new Point2d(x + 14, my), false, 11, Font);
        }

        private bool IsGroupDone(Lesson l, StepGroup g)
        {
            for (int i = 0; i < l.Steps.Count; i++)
                if (l.Steps[i].Group == g && i >= _t.StepIndex) return false;
            return true;
        }

        private void DrawCaption(DisplayPipeline d, int w, int h, Step s)
        {
            Lesson l = _t.Lesson!;
            string badge = $"STEP {_t.StepIndex + 1}/{l.Steps.Count}";
            Rectangle say = d.Measure2dText(s.Say, new Point2d(0, 0), false, 0, 17, Font);
            Rectangle hint = d.Measure2dText(s.Hint, new Point2d(0, 0), false, 0, 13, Font);
            int tw = Math.Max(say.Width, string.IsNullOrEmpty(s.Hint) ? 0 : hint.Width);
            int bw = 92, cw = bw + tw + 40, ch = string.IsNullOrEmpty(s.Hint) ? 54 : 70;
            int x = (w - cw) / 2, y = h - ch - 20;
            d.Draw2dRectangle(new Rectangle(x + 3, y + 4, cw, ch), Color.Transparent, 0, Ink);
            d.Draw2dRectangle(new Rectangle(x, y, cw, ch), Ink, 2, Paper);
            Color c = ColorOf(s.Group);
            d.Draw2dRectangle(new Rectangle(x + 10, y + 10, bw - 10, ch - 20), c, 1, c);
            d.Draw2dText(badge, Paper, new Point2d(x + 10 + (bw - 10) / 2, y + ch / 2), true, 13, Font);
            d.Draw2dText(s.Say, Ink, new Point2d(x + bw + 16, y + 14), false, 17, Font);
            if (!string.IsNullOrEmpty(s.Hint))
                d.Draw2dText(s.Hint, Muted, new Point2d(x + bw + 16, y + 42), false, 13, Font);
        }

        private void DrawCallout(DisplayPipeline d, RhinoViewport vp, int w, int h, Step s, double now)
        {
            Point2d p = vp.WorldToClient(s.Target);
            if (!InFront(vp, s.Target)) return;
            Lesson l = _t.Lesson!;
            int n = 0, tot = 0;
            for (int i = 0; i < l.Steps.Count; i++)
            {
                if (l.Steps[i].Kind != StepKind.Point) continue;
                tot++;
                if (i <= _t.StepIndex) n++;
            }
            Color c = ColorOf(s.Group);
            string l1 = $"CLICK {n} OF {tot}  -  {NameOf(s.Group)}";
            Rectangle m1 = d.Measure2dText(l1, new Point2d(0, 0), false, 0, 11, Font);
            Rectangle m2 = d.Measure2dText(s.Label, new Point2d(0, 0), false, 0, 15, Font);
            int bw = Math.Max(m1.Width, m2.Width) + 30, bh = 48;
            double dx = p.X < w / 2.0 ? 150 : -150 - bw, dy = -110;
            if (s.Label == "Pillar top" || s.Label == "Point on arc") { dx = p.X < w / 2.0 ? -170 - bw : 150; dy = -40; }
            int bx = (int)StreetTutorial.Clamp(p.X + dx, 10, w - bw - 10);
            int by = (int)StreetTutorial.Clamp(p.Y + dy, 10, h - bh - 110);
            if (bx < 360 && by < 300) bx = 360;

            // dashed arrow from the box to the ring
            var from = new PointF(bx + bw / 2f, by + bh / 2f);
            var dir = new Vector2d(p.X - from.X, p.Y - from.Y);
            double len = dir.Length;
            if (len > 40)
            {
                dir /= len;
                var start = new PointF((float)(from.X + dir.X * (bh / 2.0 + 6)), (float)(from.Y + dir.Y * (bh / 2.0 + 6)));
                var end = new PointF((float)(p.X - dir.X * 24), (float)(p.Y - dir.Y * 24));
                Dashed(d, start, end, c, 3, now * 28);
                ArrowHead(d, end, dir, c);
            }
            d.Draw2dRectangle(new Rectangle(bx, by, bw, bh), c, 3, Paper);
            d.Draw2dRectangle(new Rectangle(bx, by, 7, bh), c, 1, c);
            d.Draw2dText(l1, c, new Point2d(bx + 16, by + 8), false, 11, Font);
            d.Draw2dText(s.Label, Ink, new Point2d(bx + 16, by + 24), false, 15, Font);
        }

        private void DrawBigCaption(DisplayPipeline d, int w, string title, string sub)
        {
            Rectangle t = d.Measure2dText(title, new Point2d(0, 0), false, 0, 30, Font);
            Rectangle s = d.Measure2dText(sub, new Point2d(0, 0), false, 0, 14, Font);
            int cw = Math.Max(t.Width, s.Width) + 48, ch = 86, x = (w - cw) / 2, y = 24;
            d.Draw2dRectangle(new Rectangle(x + 3, y + 4, cw, ch), Color.Transparent, 0, Ink);
            d.Draw2dRectangle(new Rectangle(x, y, cw, ch), Ink, 2, Paper);
            d.Draw2dText(title, Ink, new Point2d(w / 2.0, y + 30), true, 30, Font);
            d.Draw2dText(sub, Muted, new Point2d(w / 2.0, y + 64), true, 14, Font);
        }

        private void DrawMessageCard(DisplayPipeline d, int w, int h, Overlay o, double now)
        {
            double age = now - o.Since;
            d.Draw2dRectangle(new Rectangle(0, 0, w, h), Color.Transparent, 0, Color.FromArgb(70, 30, 30, 30));
            int cw = 500, ch = 380, x = (w - cw) / 2, y = (h - ch) / 2;
            d.Draw2dRectangle(new Rectangle(x + 4, y + 6, cw, ch), Color.Transparent, 0, Ink);
            d.Draw2dRectangle(new Rectangle(x, y, cw, ch), Ink, 2, Paper);
            if (o.Orange) d.Draw2dRectangle(new Rectangle(x + 1, y + 1, cw - 2, 7), Orange, 1, Orange);
            else
            {
                int third = (cw - 2) / 3;
                d.Draw2dRectangle(new Rectangle(x + 1, y + 1, third, 7), Red, 1, Red);
                d.Draw2dRectangle(new Rectangle(x + 1 + third, y + 1, third, 7), Green, 1, Green);
                d.Draw2dRectangle(new Rectangle(x + 1 + 2 * third, y + 1, cw - 2 - 2 * third, 7), Blue, 1, Blue);
            }

            // the rhino: hops, squashes and sways while dancing; rocks gently when sad
            DisplayBitmap? bmp = Sprite(o.Mood);
            if (bmp != null)
            {
                const float baseH = 180;
                float hop = 0, sx = 1, sy = 1, sway = 0;
                if (o.Dance)
                {
                    double beat = age / 0.575;
                    double ph = beat - Math.Floor(beat);
                    hop = (float)(Math.Sin(ph * Math.PI) * 42);
                    double land = Math.Max(0, 1 - Math.Abs(ph) * 6) + Math.Max(0, 1 - Math.Abs(1 - ph) * 6);
                    sx = (float)(1 + 0.08 * land);
                    sy = (float)(1 - 0.1 * land);
                    sway = (float)(Math.Sin(age * Math.PI / 1.15) * 14);
                }
                else sway = (float)(Math.Sin(age * 2.6) * 3);
                float bw = baseH * Aspect(bmp) * sx, bh = baseH * sy;
                float cx = x + cw / 2f + sway, bottom = y + 26 + baseH;
                // shadow
                float shw = 150 * (1 - hop / 84f);
                d.Draw2dRectangle(new Rectangle((int)(x + cw / 2f - shw / 2), (int)bottom - 3, (int)shw, 6), Color.Transparent, 0, Color.FromArgb(35, 0, 0, 0));
                d.DrawSprite(bmp, new Point2d(cx, bottom - bh / 2 - hop), bw, bh);
            }

            d.Draw2dText(o.Title, Ink, new Point2d(w / 2.0, y + 232), true, 32, Font);
            DrawWrapped(d, o.Sub, w / 2.0, y + 262, cw - 60, 14, Muted);
            d.Draw2dText(o.Stats, Ink, new Point2d(w / 2.0, y + 318), true, 13, Font);
            d.Draw2dText(o.Footer, Orange, new Point2d(w / 2.0, y + 346), true, 13, Font);

            if (o.Confetti)
            {
                foreach (var c in _confetti)
                {
                    double t = age - c.delay;
                    if (t < 0) continue;
                    float py = (float)(y - 10 + t * c.speed);
                    if (py > y + ch) continue;
                    float px = x + 10 + c.x * (cw - 20) + (float)Math.Sin(t * 5 + c.x * 20) * 8;
                    d.Draw2dRectangle(new Rectangle((int)px, (int)py, c.size, c.size), c.color, 1, c.color);
                }
            }
        }

        private void DrawHorizon(DisplayPipeline d, RhinoViewport vp, int w, int h, double k)
        {
            Vector3d f = vp.CameraDirection;
            var hf = new Vector3d(f.X, f.Y, 0);
            hf.Unitize();
            double hy = vp.WorldToClient(vp.CameraLocation + hf * 5000).Y;
            hy = StreetTutorial.Clamp(hy, 0, h);
            int a = (int)(255 * k);
            Color[] sky = { Color.FromArgb(6, 32, 92), Color.FromArgb(47, 111, 208), Color.FromArgb(169, 216, 255) };
            Color[] sea = { Color.FromArgb(191, 227, 255), Color.FromArgb(59, 130, 216), Color.FromArgb(11, 45, 110) };
            Bands(d, 0, hy, w, sky, a);
            Bands(d, hy, h, w, sea, a);
            d.Draw2dRectangle(new Rectangle(0, (int)hy - 1, w, 3), Color.Transparent, 0, Color.FromArgb((int)(220 * k), 255, 255, 255));
        }

        private static void Bands(DisplayPipeline d, double y0, double y1, int w, Color[] stops, int alpha)
        {
            const int n = 28;
            double step = (y1 - y0) / n;
            if (step <= 0) return;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)(n - 1) * (stops.Length - 1);
                int j = Math.Min(stops.Length - 2, (int)t);
                double f = t - j;
                Color c = Color.FromArgb(alpha,
                    (int)(stops[j].R + (stops[j + 1].R - stops[j].R) * f),
                    (int)(stops[j].G + (stops[j + 1].G - stops[j].G) * f),
                    (int)(stops[j].B + (stops[j + 1].B - stops[j].B) * f));
                d.Draw2dRectangle(new Rectangle(0, (int)(y0 + i * step), w, (int)Math.Ceiling(step) + 1), Color.Transparent, 0, c);
            }
        }

        private void DrawWalker(DisplayPipeline d, RhinoViewport vp, double s, double now)
        {
            DisplayBitmap? bmp = Sprite(Mood.Win);
            Point3d p = WalkAt(s);
            if (bmp == null || !InFront(vp, p)) return;
            Point2d b = vp.WorldToClient(p), top = vp.WorldToClient(p + new Vector3d(0, 0, 1.25));
            double hh = b.Y - top.Y;
            if (hh < 3) return;
            double hop = Math.Abs(Math.Sin(now * 9)) * 0.28 * hh;
            float ww = (float)(hh * Aspect(bmp));
            int alpha = (int)(255 * _t.RhinoFade);
            d.Draw2dRectangle(new Rectangle((int)(b.X - ww * 0.35), (int)(b.Y - hh * 0.05), (int)(ww * 0.7), (int)Math.Max(2, hh * 0.1)), Color.Transparent, 0, Color.FromArgb(alpha / 6, 0, 0, 0));
            // 2D sprites with a size can't be tinted, so the rhino shrinks away instead of fading.
            float f = (float)_t.RhinoFade;
            if (f > 0.03f) d.DrawSprite(bmp, new Point2d(b.X, b.Y - hh * f / 2 - hop), ww * f, (float)hh * f);
        }

        // ================= helpers =================

        private DisplayBitmap? Sprite(Mood m) => _sprites.TryGetValue(m, out DisplayBitmap? b) ? b : null;

        private static float Aspect(DisplayBitmap b) => b.Size.Height > 0 ? (float)b.Size.Width / b.Size.Height : 1f;

        private static bool InFront(RhinoViewport vp, Point3d p) => (p - vp.CameraLocation) * vp.CameraDirection > 0.2;

        private static Rectangle Box(DisplayPipeline d, string text, Point2d at, int height, int padX, int padY)
        {
            Rectangle m = d.Measure2dText(text, new Point2d(0, 0), false, 0, height, Font);
            return new Rectangle((int)at.X, (int)at.Y, m.Width + 2 * padX, m.Height + 2 * padY);
        }

        private static void Dashed(DisplayPipeline d, PointF a, PointF b, Color c, float thick, double offset)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y, len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1) return;
            dx /= len; dy /= len;
            const double on = 10, off = 7;
            double pos = -(offset % (on + off));
            while (pos < len)
            {
                double s0 = Math.Max(0, pos), s1 = Math.Min(len, pos + on);
                if (s1 > s0)
                    d.Draw2dLine(new PointF((float)(a.X + dx * s0), (float)(a.Y + dy * s0)), new PointF((float)(a.X + dx * s1), (float)(a.Y + dy * s1)), c, thick);
                pos += on + off;
            }
        }

        private static void ArrowHead(DisplayPipeline d, PointF tip, Vector2d dir, Color c)
        {
            var back = new Vector2d(-dir.X, -dir.Y) * 14;
            var side = new Vector2d(-dir.Y, dir.X) * 8;
            d.Draw2dLine(tip, new PointF((float)(tip.X + back.X + side.X), (float)(tip.Y + back.Y + side.Y)), c, 3);
            d.Draw2dLine(tip, new PointF((float)(tip.X + back.X - side.X), (float)(tip.Y + back.Y - side.Y)), c, 3);
        }

        private static void DrawWrapped(DisplayPipeline d, string text, double cx, double y, int maxWidth, int height, Color c)
        {
            var words = text.Split(' ');
            var line = "";
            foreach (string wd in words)
            {
                string tryLine = line.Length == 0 ? wd : line + " " + wd;
                if (d.Measure2dText(tryLine, new Point2d(0, 0), false, 0, height, Font).Width > maxWidth && line.Length > 0)
                {
                    d.Draw2dText(line, c, new Point2d(cx, y), true, height, Font);
                    y += height + 6;
                    line = wd;
                }
                else line = tryLine;
            }
            if (line.Length > 0) d.Draw2dText(line, c, new Point2d(cx, y), true, height, Font);
        }
    }
}
