using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using RhinoGotchi.Lessons;

namespace RhinoGotchi.UI
{
    // The screens outside a lesson: welcome, the level map (home), and the placement result.
    internal sealed partial class LessonCanvas
    {
        private const float NodeR = 34f;
        private const float NodeSpacing = 124f;

        private UITimer? _toastTimer;

        // ------------------------------------------------------------------ welcome (first run)

        private float PaintWelcome(Graphics g, float w, float h)
        {
            bool returning = !_game.Progress.IsNew;
            if (returning)
            {
                // Back arrow to the level map
                var back = new RectangleF(Pad - 4, 10, 30, 30);
                if (_hover == "back")
                    g.FillPath(_t.TileHover, GraphicsPath.GetRoundRect(back, 8));
                var pen = new Pen(_hover == "back" ? _t.Text : _t.SubtleText, 2.8f) { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
                g.DrawLines(pen, new PointF(back.X + 18, back.Y + 8), new PointF(back.X + 11, back.Y + 15), new PointF(back.X + 18, back.Y + 22));
                _hits.Add((back, "back", _game.BackToMap));
            }

            float y = Pad + 8;
            float bob = (float)Math.Sin(AnimProgress * Math.PI) * -8;
            Mascot.Draw(g, new RectangleF(w / 2 - 60, y, 120, 120), Mood.Happy, bob);
            y += 128;
            y += Centered(g, "Hi, I'm Gotchi!", Bold(24), _t.Green, w, y) + 4;
            y += Wrapped(g, Pad + 8, y, w - 2 * Pad - 16, Regular(14), _t.SubtleText,
                "Let's learn Rhino, one command at a time. Where do you want to start?", center: true) + 18;

            y = ChoiceCard(g, w, y, "scratch", "Line", "New to Rhino?", "Start from scratch at Level 1.", _game.StartFromScratch) + Gap;
            y = ChoiceCard(g, w, y, "place", "Sweep2", "Already know some Rhino?", $"Take a quick placement test ({LessonCatalog.PlacementTest().Steps.Count} questions max) to find your level.", _game.StartPlacement);

            if (returning)
            {
                y += Gap + 4;
                Progress p = _game.Progress;
                string where = p.CurrentLevel < 0 ? "all levels done" : $"Level {p.CurrentLevel + 1} · {LessonCatalog.All[p.CurrentLevel].Title}";
                y += Wrapped(g, Pad, y, w - 2 * Pad, Regular(12), _t.SubtleText,
                    $"Your progress is saved ({where}). Starting from scratch or retaking the test never erases finished levels.", center: true) + 10;
                ChunkyButton(g, new RectangleF(Pad, y, w - 2 * Pad, ButtonH), "BACK TO MY PATH", _t.Blue, _t.BlueShadow, Colors.White, "primary", _game.BackToMap);
                y += ButtonH + 4;
            }
            return y + Pad + 4;
        }

        private float ChoiceCard(Graphics g, float w, float y, string id, string icon, string title, string body, Action action)
        {
            float textX = Pad + 84, textW = w - Pad - textX - 12;
            float bodyH = Wrap(Regular(12), body, textW).Count * Regular(12).LineHeight;
            float cardH = Math.Max(86, 16 + 22 + bodyH + 14);
            bool pressed = _pressed == id;
            float lift = pressed ? 3 : 0;
            var rect = new RectangleF(Pad, y, w - 2 * Pad, cardH);
            var top = new RectangleF(rect.X, rect.Y + lift, rect.Width, rect.Height);
            g.FillPath(_t.Border, GraphicsPath.GetRoundRect(new RectangleF(rect.X, rect.Y + 4, rect.Width, rect.Height), 14));
            g.FillPath(_t.Border, GraphicsPath.GetRoundRect(top, 14));
            g.FillPath(_hover == id ? _t.TileHover : _t.TileFace, GraphicsPath.GetRoundRect(new RectangleF(top.X + 2, top.Y + 2, top.Width - 4, top.Height - 4), 12));
            LessonIcons.Draw(g, icon, new RectangleF(top.X + 10, top.Y + (cardH - 66) / 2, 66, 66));
            g.DrawText(Bold(15), _t.Text, textX, top.Y + 14, title);
            Wrapped(g, textX, top.Y + 38, textW, Regular(12), _t.SubtleText, body);
            _hits.Add((new RectangleF(rect.X, rect.Y, rect.Width, rect.Height + 4), id, action));
            return y + cardH + 4;
        }

        // ------------------------------------------------------------------ level map (home)

        private float PaintMap(Graphics g, float w, float h)
        {
            Progress progress = _game.Progress;
            IReadOnlyList<Lesson> levels = LessonCatalog.All;
            int current = progress.CurrentLevel;
            float y = 14;

            // Header: home button (back to the welcome screen), name, XP
            var home = new RectangleF(Pad - 4, y - 2, 30, 30);
            if (_hover == "home")
                g.FillPath(_t.TileHover, GraphicsPath.GetRoundRect(home, 8));
            House(g, new PointF(home.X + home.Width / 2, home.Y + home.Height / 2), _hover == "home" ? _t.Text : _t.SubtleText);
            _hits.Add((home, "home", _game.ShowWelcome));
            g.DrawText(Bold(16), _t.Green, home.Right + 6, y + 2, "RhinoGotchi");
            string xp = $"{progress.TotalXp} XP";
            SizeF xs = Bold(14).MeasureString(xp);
            g.DrawText(Bold(14), _t.Gold, w - Pad - xs.Width, y + 4, xp);
            Bolt(g, new PointF(w - Pad - xs.Width - 12, y + 12), _t.Gold);
            y += 36;

            // "Your level" banner: the answer to "where am I?"
            y = LevelBanner(g, w, y, current, progress.DoneCount, levels.Count) + 22;

            // The path
            float firstCy = y + NodeR + 46; // room for the START bubble above the first node
            for (int i = 0; i < levels.Count; i++)
            {
                float cx = NodeX(w, i);
                float cy = firstCy + i * NodeSpacing;
                LevelNode(g, w, i, cx, cy, progress.StateOf(i), levels[i], current);
            }
            y = firstCy + (levels.Count - 1) * NodeSpacing + NodeR + 30;

            // Toast for locked levels
            if (_toast != null && DateTime.Now < _toastUntil)
            {
                Font f = Regular(12);
                List<string> lines = Wrap(f, _toast, w - 2 * Pad - 24);
                var r = new RectangleF(Pad, y - 8, w - 2 * Pad, lines.Count * f.LineHeight + 16);
                g.FillPath(_t.Text, GraphicsPath.GetRoundRect(r, 10));
                for (int i = 0; i < lines.Count; i++)
                    g.DrawText(f, _t.Background, r.X + 12, r.Y + 8 + i * f.LineHeight, lines[i]);
                y = r.Bottom + Gap;
            }

            OutlineButton(g, new RectangleF(Pad, y, w - 2 * Pad, 42), "TAKE PLACEMENT TEST", "placement", _game.StartPlacement);
            return y + 42 + 3 + Pad;
        }

        private float LevelBanner(Graphics g, float w, float y, int current, int done, int total)
        {
            bool allDone = current < 0;
            Color face = allDone ? _t.Gold : _t.Green;
            Color shadow = allDone ? Darken(_t.Gold, 0.12f) : _t.GreenShadow;
            string label = allDone ? "ALL LEVELS DONE" : "YOUR LEVEL";
            string title = allDone ? "RhinoGotchi master!" : $"Level {current + 1} · {LessonCatalog.All[current].Title}";
            string sub = allDone ? "Replay any level to keep your skills sharp." : LessonCatalog.All[current].Subtitle;

            string count = $"{done}/{total}";
            float countW = Bold(18).MeasureString(count).Width;
            float textW = w - 2 * Pad - 28 - countW - 12;
            float titleH = Wrap(Bold(18), title, textW).Count * Bold(18).LineHeight;
            float subH = Wrap(Regular(12), sub, textW).Count * Regular(12).LineHeight;
            float bh = 12 + 16 + titleH + 2 + subH + 14;

            var rect = new RectangleF(Pad, y, w - 2 * Pad, bh);
            g.FillPath(shadow, GraphicsPath.GetRoundRect(new RectangleF(rect.X, rect.Y + 4, rect.Width, rect.Height), 16));
            g.FillPath(face, GraphicsPath.GetRoundRect(rect, 16));
            g.DrawText(Bold(11), Color.FromArgb(255, 255, 255, 210), rect.X + 14, rect.Y + 12, label);
            float ty = rect.Y + 28;
            ty += Wrapped(g, rect.X + 14, ty, textW, Bold(18), Colors.White, title) + 2;
            Wrapped(g, rect.X + 14, ty, textW, Regular(12), Color.FromArgb(255, 255, 255, 230), sub);

            g.DrawText(Bold(18), Colors.White, rect.Right - 14 - countW, rect.Y + bh / 2 - 16, count);
            SizeF ds = Regular(11).MeasureString("done");
            g.DrawText(Regular(11), Color.FromArgb(255, 255, 255, 210), rect.Right - 14 - countW / 2 - ds.Width / 2, rect.Y + bh / 2 + 6, "done");
            return y + bh + 4;
        }

        private static float NodeX(float w, int i) => w / 2 + (float)Math.Sin(i * 1.15 + 0.4) * Math.Min(66, w * 0.19f);

        private void LevelNode(Graphics g, float w, int index, float cx, float cy, LevelState state, Lesson lesson, int current)
        {
            string id = $"level{index}";
            bool pressed = _pressed == id && state != LevelState.Locked;
            bool hover = _hover == id && state != LevelState.Locked;

            Color face = state switch { LevelState.Done => _t.Gold, LevelState.Current => _t.Green, _ => _t.Disabled };
            Color shadow = state switch { LevelState.Done => Darken(_t.Gold, 0.12f), LevelState.Current => _t.GreenShadow, _ => Darken(_t.Disabled, 0.1f) };
            if (hover)
                face = Brighten(face, 0.05f);

            if (state == LevelState.Current)
            {
                float rr = NodeR + 10;
                g.DrawEllipse(new Pen(_t.Track, 7), cx - rr, cy - rr + 3, rr * 2, rr * 2);
            }

            float lift = pressed ? 5 : 0;
            g.FillEllipse(shadow, cx - NodeR, cy - NodeR + 6, NodeR * 2, NodeR * 2);
            g.FillEllipse(face, cx - NodeR, cy - NodeR + lift, NodeR * 2, NodeR * 2);
            g.FillEllipse(Color.FromArgb(255, 255, 255, 50), cx - NodeR * 0.55f, cy - NodeR * 0.75f + lift, NodeR * 0.7f, NodeR * 0.35f);

            var c = new PointF(cx, cy + lift);
            switch (state)
            {
                case LevelState.Done: Check(g, c, Colors.White); break;
                case LevelState.Current: Star(g, c, Colors.White); break;
                default: Lock(g, c, _t.DisabledText); break;
            }

            // Label on the outer side of the zig-zag
            bool right = cx <= w / 2;
            float lx = right ? cx + NodeR + 14 : Pad;
            float lw = right ? w - Pad - lx : cx - NodeR - 14 - Pad;
            Color titleColor = state == LevelState.Locked ? _t.DisabledText : _t.Text;
            Font tf = Bold(14);
            List<string> titleLines = Wrap(tf, lesson.Title, lw);
            float blockH = 16 + titleLines.Count * tf.LineHeight;
            float ly = cy - blockH / 2;
            string small = $"LEVEL {index + 1}";
            float sx = right ? lx : lx + lw - Bold(10).MeasureString(small).Width;
            g.DrawText(Bold(10), state == LevelState.Locked ? _t.DisabledText : _t.SubtleText, sx, ly, small);
            for (int i = 0; i < titleLines.Count; i++)
            {
                float tx = right ? lx : lx + lw - tf.MeasureString(titleLines[i]).Width;
                g.DrawText(tf, titleColor, tx, ly + 16 + i * tf.LineHeight, titleLines[i]);
            }

            // Bouncy START bubble over the current level
            if (state == LevelState.Current)
            {
                float bob = (float)Math.Sin(AnimProgress * Math.PI) * -6;
                string text = _game.Progress.DoneCount == 0 ? "START" : "CONTINUE";
                Font bf = Bold(13);
                SizeF bs = bf.MeasureString(text);
                var b = new RectangleF(cx - bs.Width / 2 - 12, cy - NodeR - 50 + bob, bs.Width + 24, 30);
                var path = GraphicsPath.GetRoundRect(b, 9);
                g.FillPath(_t.Background, path);
                g.DrawPath(new Pen(_t.Border, 2), path);
                var tail = new[] { new PointF(cx - 7, b.Bottom - 1), new PointF(cx, b.Bottom + 7), new PointF(cx + 7, b.Bottom - 1) };
                g.FillPolygon(_t.Background, tail);
                g.DrawLines(new Pen(_t.Border, 2) { LineJoin = PenLineJoin.Round }, tail);
                g.DrawText(bf, _t.Green, b.X + 12, b.Y + (b.Height - bs.Height) / 2, text);
            }

            var hit = new RectangleF(cx - NodeR - 4, cy - NodeR - 4, NodeR * 2 + 8, NodeR * 2 + 12);
            if (state == LevelState.Locked)
            {
                string msg = current >= 0
                    ? $"Level {index + 1} is locked. Finish Level {current + 1} first, or take the placement test to skip ahead."
                    : "Locked.";
                _hits.Add((hit, id, () => ShowToast(msg)));
            }
            else
            {
                int level = index;
                _hits.Add((hit, id, () => _game.StartLesson(level)));
            }
        }

        private void ShowToast(string text)
        {
            _toast = text;
            _toastUntil = DateTime.Now.AddSeconds(2.5);
            if (_toastTimer == null)
            {
                _toastTimer = new UITimer { Interval = 2.6 };
                _toastTimer.Elapsed += (s, e) =>
                {
                    _toastTimer.Stop();
                    Invalidate();
                };
            }
            _toastTimer.Stop();
            _toastTimer.Start();
            Invalidate();
        }

        // ------------------------------------------------------------------ placement result

        private float PaintPlacementResult(Graphics g, float w, float h)
        {
            int count = LessonCatalog.All.Count;
            int placed = _game.PlacedLevel;
            bool all = placed >= count;

            float y = Pad + 8;
            Confetti(g, w, y);
            Mascot.Draw(g, new RectangleF(w / 2 - 55, y + 4, 110, 110), Mood.Happy, (float)Math.Sin(AnimProgress * Math.PI) * -12);
            y += 122;

            if (all)
            {
                y += Centered(g, "Wow, you aced it!", Bold(24), _t.Gold, w, y) + 6;
                y += Wrapped(g, Pad + 8, y, w - 2 * Pad - 16, Regular(14), _t.SubtleText,
                    "You tested out of every level. Replay any of them from the map whenever you like.", center: true) + 14;
            }
            else
            {
                y += Centered(g, "You placed into", Bold(15), _t.SubtleText, w, y) + 2;
                y += Centered(g, $"Level {placed + 1}", Bold(30), _t.Green, w, y) + 0;
                y += Centered(g, LessonCatalog.All[placed].Title, Bold(20), _t.Text, w, y) + 14;
            }

            // Mini ladder: one dot per level
            float dot = 26, gap = Math.Min(18, (w - 2 * Pad - count * dot) / Math.Max(1, count - 1));
            float rowW = count * dot + (count - 1) * gap;
            float x0 = (w - rowW) / 2;
            for (int i = 0; i < count; i++)
            {
                float cx = x0 + i * (dot + gap) + dot / 2, cy = y + dot / 2;
                if (i < count - 1)
                    g.DrawLine(new Pen(i < placed ? _t.Gold : _t.Track, 4), cx + dot / 2, cy, cx + dot / 2 + gap, cy);
                LevelState st = i < placed ? LevelState.Done : i == placed ? LevelState.Current : LevelState.Locked;
                Color f = st == LevelState.Done ? _t.Gold : st == LevelState.Current ? _t.Green : _t.Disabled;
                if (st == LevelState.Current)
                    g.DrawEllipse(new Pen(_t.Track, 4), cx - dot / 2 - 5, cy - dot / 2 - 5, dot + 10, dot + 10);
                g.FillEllipse(f, cx - dot / 2, cy - dot / 2, dot, dot);
                string n = (i + 1).ToString();
                SizeF ns = Bold(11).MeasureString(n);
                g.DrawText(Bold(11), st == LevelState.Locked ? _t.DisabledText : Colors.White, cx - ns.Width / 2, cy - ns.Height / 2, n);
            }
            y += dot + 18;

            if (!all)
            {
                string skipped = placed == 0
                    ? "Let's start from the beginning. You'll be modeling like a pro in no time!"
                    : $"{string.Join(", ", LessonCatalog.All.Take(placed).Select(l => l.Title))} {(placed == 1 ? "is" : "are")} marked done. You can still replay {(placed == 1 ? "it" : "them")} any time.";
                y += Wrapped(g, Pad + 8, y, w - 2 * Pad - 16, Regular(13), _t.SubtleText, skipped, center: true) + 8;
            }

            return Footer(g, w, h, y + Gap, () =>
                ChunkyButton(g, FooterButtonRect(w, h, y + Gap), "SEE MY PATH", _t.Green, _t.GreenShadow, Colors.White, "primary", _game.BackToMap));
        }

        // ------------------------------------------------------------------ symbols

        private static void Check(Graphics g, PointF c, Color color)
        {
            var pen = new Pen(color, 6) { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            g.DrawLines(pen, new PointF(c.X - 12, c.Y + 1), new PointF(c.X - 3, c.Y + 10), new PointF(c.X + 13, c.Y - 9));
        }

        private static void Star(Graphics g, PointF c, Color color)
        {
            var pts = new PointF[10];
            for (int i = 0; i < 10; i++)
            {
                double a = -Math.PI / 2 + i * Math.PI / 5;
                float r = i % 2 == 0 ? 17 : 7.5f;
                pts[i] = new PointF(c.X + (float)Math.Cos(a) * r, c.Y + 1 + (float)Math.Sin(a) * r);
            }
            g.FillPolygon(color, pts);
        }

        private static void Lock(Graphics g, PointF c, Color color)
        {
            g.DrawArc(new Pen(color, 4), c.X - 8, c.Y - 15, 16, 18, 180, 180);
            g.DrawLine(new Pen(color, 4), c.X - 8, c.Y - 6, c.X - 8, c.Y - 1);
            g.DrawLine(new Pen(color, 4), c.X + 8, c.Y - 6, c.X + 8, c.Y - 1);
            g.FillPath(color, GraphicsPath.GetRoundRect(new RectangleF(c.X - 13, c.Y - 3, 26, 19), 4));
        }

        private static void House(Graphics g, PointF c, Color color)
        {
            var pen = new Pen(color, 2.4f) { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            g.DrawLines(pen, new PointF(c.X - 8, c.Y - 1), new PointF(c.X, c.Y - 8), new PointF(c.X + 8, c.Y - 1));
            g.DrawLines(pen, new PointF(c.X - 5.5f, c.Y - 3), new PointF(c.X - 5.5f, c.Y + 7), new PointF(c.X + 5.5f, c.Y + 7), new PointF(c.X + 5.5f, c.Y - 3));
            g.FillRectangle(color, c.X - 1.5f, c.Y + 2, 3, 5);
        }

        private static void Bolt(Graphics g, PointF c, Color color)
        {
            g.FillPolygon(color,
                new PointF(c.X + 2, c.Y - 9), new PointF(c.X - 6, c.Y + 1), new PointF(c.X - 0.5f, c.Y + 1),
                new PointF(c.X - 2, c.Y + 9), new PointF(c.X + 6, c.Y - 1), new PointF(c.X + 0.5f, c.Y - 1));
        }

        private static Color Darken(Color c, float amount) =>
            new Color(Math.Max(0, c.R - amount), Math.Max(0, c.G - amount), Math.Max(0, c.B - amount), c.A);
    }
}
