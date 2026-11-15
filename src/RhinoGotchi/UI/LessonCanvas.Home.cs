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
            // Back arrow: to the level map, or (first run) to the sidebar home.
            Action? backAction = returning ? _game.BackToMap : ExitHome;
            if (backAction != null)
                BackArrow(g, new RectangleF(Pad - 4, 10, 30, 30), backAction);

            float y = Pad + 8;
            float bob = (float)Math.Sin(AnimProgress * Math.PI) * -8;
            PetDraw(g, new RectangleF(w / 2 - 60, y, 120, 120), Mood.Happy);
            y += 128;
            y += Centered(g, "Hi, I'm Gotchi!", Bold(24), _t.Text, w, y) + 4;
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
                ChunkyButton(g, new RectangleF(Pad, y, w - 2 * Pad, ButtonH), "BACK TO MY PATH", _t.Ink, _t.InkShadow, _t.OnInk, "primary", _game.BackToMap);
                y += ButtonH + 4;
            }
            return y + Pad + 4;
        }

        private float ChoiceCard(Graphics g, float w, float y, string id, string icon, string title, string body, Action action)
        {
            float textX = Pad + 84, textW = w - Pad - textX - 12;
            float bodyH = Wrap(Regular(12), body, textW).Count * LineH(Regular(12));
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

            // Header: back, the title with FinGotchi's orange bar, start screen (house), XP
            float titleX = Pad;
            if (ExitHome != null)
            {
                var back = new RectangleF(Pad - 4, y - 2, 30, 30);
                BackArrow(g, back, ExitHome);
                titleX = back.Right + 4;
            }
            string xp = $"{progress.TotalXp} XP";
            var home = new RectangleF(w - Pad - Bold(13).MeasureString(xp).Width - 10 - 34, y - 2, 30, 30);
            if (_hover == "home")
                g.FillPath(_t.TileHover, GraphicsPath.GetRoundRect(home, 8));
            House(g, new PointF(home.X + home.Width / 2, home.Y + home.Height / 2), _hover == "home" ? _t.Text : _t.SubtleText);
            _hits.Add((home, "home", _game.ShowWelcome));
            SizeF xs = Bold(13).MeasureString(xp);
            g.DrawText(Bold(13), _t.AccentText, w - Pad - xs.Width, y + 5, xp);
            y = TitleBar(g, titleX, y, home.X - titleX - 6, "Learning Campaign", "one command at a time") + 12;

            // The pet says where you are
            string say = current < 0 ? "Every level done! Replay any, or build Klosterneuburg."
                : progress.DoneCount == 0 ? "Press a tile to start. Level 1 is waiting!"
                : $"Next up: Level {current + 1}, {levels[current].Title}.";
            y = MascotRow(g, w, y, Mood.Happy, say, 0) + 12;

            // "Your level": FinGotchi's cream lesson card
            y = LevelBanner(g, w, y, current, progress.DoneCount, LessonCatalog.MainCount) + 16;

            // The main path, then each chapter, as rows of numbered tiles
            var main = Enumerable.Range(0, levels.Count).Where(i => !levels[i].IsProject).ToList();
            y = TileRow(g, w, y, "LEARNING CAMPAIGN", $"{progress.DoneCount} of {main.Count} done", main, current) + 18;
            foreach (string chapter in levels.Where(l => l.IsProject).Select(l => l.Chapter).Distinct())
            {
                var indices = Enumerable.Range(0, levels.Count).Where(i => levels[i].Chapter == chapter).ToList();
                int done = progress.ChapterDone(chapter);
                string note = chapter == "KLOSTERNEUBURG" ? $"12 commands, one building · {done} of {indices.Count}" : $"{done} of {indices.Count}";
                y = TileRow(g, w, y, chapter, note, indices, current) + 18;
            }

            // Toast for locked levels
            if (_toast != null && DateTime.Now < _toastUntil)
            {
                Font f = Regular(12);
                List<string> lines = Wrap(f, _toast, w - 2 * Pad - 24);
                var r = new RectangleF(Pad, y - 8, w - 2 * Pad, lines.Count * LineH(f) + 16);
                g.FillPath(_t.Text, GraphicsPath.GetRoundRect(r, 10));
                for (int i = 0; i < lines.Count; i++)
                    g.DrawText(f, _t.Background, r.X + 12, r.Y + 8 + i * LineH(f), lines[i]);
                y = r.Bottom + Gap;
            }

            float half = (w - 2 * Pad - 8) / 2;
            OutlineButton(g, new RectangleF(Pad, y, half, 34), "Placement test", "placement", _game.StartPlacement);
            OutlineButton(g, new RectangleF(Pad + half + 8, y, half, 34), "Start screen", "welcome", _game.ShowWelcome);
            return y + 34 + Pad;
        }

        /// <summary>FinGotchi's title: an orange bar beside a slab-serif title and a small subtitle. Returns the y below.</summary>
        private float TitleBar(Graphics g, float x, float y, float width, string title, string subtitle)
        {
            Font tf = Bold(19), sf = Regular(11);
            float th = LineH(tf), sh = LineH(sf);
            g.FillPath(_t.Orange, GraphicsPath.GetRoundRect(new RectangleF(x, y + 2, 5, th + sh - 2), 2));
            g.DrawText(tf, _t.Text, x + 13, y, Ellipsize(tf, title, width - 13));
            g.DrawText(sf, _t.SubtleText, x + 13, y + th, subtitle);
            return y + th + sh;
        }

        /// <summary>
        /// A chapter's levels as FinGotchi tiles: black when done, orange-edged when you can play it next,
        /// grey when locked; the level number inside and a short name below. Wraps to as many rows as needed.
        /// </summary>
        private float TileRow(Graphics g, float w, float y, string heading, string note, IList<int> indices, int current)
        {
            Font kicker = Bold(10), noteFont = Regular(11);
            g.DrawText(kicker, _t.SubtleText, Pad, y, heading);
            SizeF ns = noteFont.MeasureString(note);
            g.DrawText(noteFont, _t.SubtleText, w - Pad - ns.Width, y, note);
            y += LineH(kicker) + 8;

            float size = 40, minGap = 8;
            int cols = Math.Max(4, Math.Min(6, (int)((w - 2 * Pad + minGap) / (size + minGap))));
            float gap = (w - 2 * Pad - cols * size) / Math.Max(1, cols - 1);
            Font num = Bold(16), label = Bold(8);
            float rowH = size + 4 + LineH(label) + 10;
            for (int k = 0; k < indices.Count; k++)
            {
                int index = indices[k];
                Lesson lesson = LessonCatalog.All[index];
                LevelState state = _game.Progress.StateOf(index);
                bool next = state == LevelState.Current && (lesson.IsProject || index == current);
                float tx = Pad + (k % cols) * (size + gap), ty = y + (k / cols) * rowH;
                string id = $"level{index}";
                bool hover = _hover == id && state != LevelState.Locked;
                var r = new RectangleF(tx, ty - (hover ? 1 : 0), size, size);
                var path = GraphicsPath.GetRoundRect(r, 8);
                bool done = state == LevelState.Done;
                g.FillPath(done ? _t.Ink : hover ? _t.TileHover : _t.Background, path);
                if (!done)
                    g.DrawPath(new Pen(next ? _t.Orange : _t.Border, next ? 2.2f : 1.2f), path);
                string n = lesson.IsProject ? (k + 1).ToString() : (index + 1).ToString();
                SizeF nsz = num.MeasureString(n);
                Color ink = done ? _t.OnInk : state == LevelState.Locked ? _t.DisabledText : _t.Text;
                g.DrawText(num, ink, tx + (size - nsz.Width) / 2, r.Y + (size - nsz.Height) / 2, n);
                string shortName = (lesson.ShortName.Length > 0 ? lesson.ShortName : lesson.Title.Split(' ', ':')[0]).ToUpperInvariant();
                shortName = Ellipsize(label, shortName, size + gap - 2);
                SizeF ls = label.MeasureString(shortName);
                g.DrawText(label, next ? _t.Text : _t.SubtleText, tx + (size - ls.Width) / 2, ty + size + 4, shortName);

                var hit = new RectangleF(tx - 2, ty - 2, size + 4, size + 4 + LineH(label));
                if (state == LevelState.Locked)
                {
                    string msg = lesson.IsProject
                        ? $"{lesson.Title} is locked: finish the {lesson.Chapter.ToLowerInvariant()} levels before it first."
                        : current >= 0
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
            int rows = (indices.Count + cols - 1) / cols;
            return y + rows * rowH - 10;
        }

        private float LevelBanner(Graphics g, float w, float y, int current, int done, int total)
        {
            bool allDone = current < 0;
            string label = allDone ? "ALL LEVELS DONE" : "YOUR LEVEL";
            string title = allDone ? "RhinoGotchi master!" : $"Level {current + 1} · {LessonCatalog.All[current].Title}";
            string sub = allDone ? "Replay any level to keep your skills sharp." : LessonCatalog.All[current].Subtitle;

            string count = $"{done}/{total}";
            Font countFont = Bold(17), titleFont = Bold(17), subFont = Regular(12), labelFont = Bold(10);
            float countW = countFont.MeasureString(count).Width;
            float textW = w - 2 * Pad - 28 - countW - 12;
            float titleH = Wrap(titleFont, title, textW).Count * LineH(titleFont);
            float subH = Wrap(subFont, sub, textW).Count * LineH(subFont);
            float bh = 12 + 16 + titleH + 2 + subH + 14;

            // FinGotchi's cream lesson card
            var rect = new RectangleF(Pad, y, w - 2 * Pad, bh);
            g.FillPath(_t.Warm, GraphicsPath.GetRoundRect(rect, 10));
            g.DrawPath(new Pen(_t.WarmBorder, 1), GraphicsPath.GetRoundRect(rect, 10));
            g.DrawText(labelFont, _t.AccentText, rect.X + 14, rect.Y + 12, label);
            float ty = rect.Y + 28;
            ty += Wrapped(g, rect.X + 14, ty, textW, titleFont, _t.Text, title) + 2;
            Wrapped(g, rect.X + 14, ty, textW, subFont, _t.SubtleText, sub);

            g.DrawText(countFont, _t.Text, rect.Right - 14 - countW, rect.Y + bh / 2 - 16, count);
            SizeF ds = Regular(11).MeasureString("done");
            g.DrawText(Regular(11), _t.SubtleText, rect.Right - 14 - countW / 2 - ds.Width / 2, rect.Y + bh / 2 + 4, "done");
            return y + bh + 4;
        }

        // ------------------------------------------------------------------ chapter done (FinGotchi's end page)

        private float PaintChapterDone(Graphics g, float w, float h)
        {
            string chapter = _game.DoneChapter;
            var levels = LessonCatalog.All.Where(l => l.Chapter == chapter).ToList();
            int firstTry = levels.Count(l => Tutorials.TutorialProgress.Get(TutorialIdOf(l)).FirstTry);
            bool perfect = firstTry == levels.Count;

            float y = 14;
            y = TitleBar(g, Pad, y, w - 2 * Pad, chapter == "KLOSTERNEUBURG" ? "Klosterneuburg" : chapter, $"{levels.Count} commands, one building") + 12;

            // The pet dances under confetti
            var art = new RectangleF(Pad, y, w - 2 * Pad, (w - 2 * Pad) * 0.56f);
            g.FillPath(_t.Warm, GraphicsPath.GetRoundRect(art, 12));
            var inner = art;
            inner.Inflate(-art.Width * 0.18f, -art.Height * 0.12f);
            inner.Y += art.Height * 0.06f;
            PetDraw(g, inner, Fin.PetMood.Dance);
            y = art.Bottom + 12;

            g.DrawText(Bold(10), _t.AccentText, Pad, y, perfect ? "PERFECT RUN" : "SOLVED");
            y += LineH(Bold(10)) + 4;
            y += Wrapped(g, Pad, y, w - 2 * Pad, Bold(24), _t.Text, "Splendid.") + 4;
            string commands = string.Join(", ", levels.Select(l => l.Subtitle));
            y += Wrapped(g, Pad, y, w - 2 * Pad, Regular(13), _t.SubtleText,
                $"{firstTry} of {levels.Count} levels first try. {commands}: one building.") + 14;

            return Footer(g, w, h, y + Gap, () =>
                ChunkyButton(g, FooterButtonRect(w, h, y + Gap), "Back to the campaign", _t.Ink, _t.InkShadow, _t.OnInk, "primary", _game.BackToMap));
        }

        private static string TutorialIdOf(Lesson lesson) =>
            lesson.Steps.OfType<TutorialBuildStep>().FirstOrDefault()?.Tutorial.Id ?? lesson.Id;

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
            int count = LessonCatalog.MainCount;
            int placed = _game.PlacedLevel;
            bool all = placed >= count;

            float y = Pad + 8;
            Confetti(g, w, y);
            PetDraw(g, new RectangleF(w / 2 - 55, y + 4, 110, 110), Mood.Celebrate);
            y += 122;

            if (all)
            {
                y += Centered(g, "Wow, you aced it!", Bold(24), _t.Text, w, y) + 6;
                y += Wrapped(g, Pad + 8, y, w - 2 * Pad - 16, Regular(14), _t.SubtleText,
                    "You tested out of every level. Replay any of them from the map whenever you like.", center: true) + 14;
            }
            else
            {
                y += Centered(g, "You placed into", Bold(15), _t.SubtleText, w, y) + 2;
                y += Centered(g, $"Level {placed + 1}", Bold(30), _t.Accent, w, y) + 0;
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
                    g.DrawLine(new Pen(i < placed ? _t.Green : _t.Track, 4), cx + dot / 2, cy, cx + dot / 2 + gap, cy);
                LevelState st = i < placed ? LevelState.Done : i == placed ? LevelState.Current : LevelState.Locked;
                Color f = st == LevelState.Done ? _t.Green : st == LevelState.Current ? _t.Accent : _t.Disabled;
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
            y = UnlockCards(g, w, y + 4);

            return Footer(g, w, h, y + Gap, () =>
                ChunkyButton(g, FooterButtonRect(w, h, y + Gap), "SEE MY PATH", _t.Ink, _t.InkShadow, _t.OnInk, "primary", _game.BackToMap));
        }

        // ------------------------------------------------------------------ symbols

        private void BackArrow(Graphics g, RectangleF back, Action action)
        {
            if (_hover == "back")
                g.FillPath(_t.TileHover, GraphicsPath.GetRoundRect(back, 8));
            var pen = new Pen(_hover == "back" ? _t.Text : _t.SubtleText, 2.8f) { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            g.DrawLines(pen, new PointF(back.X + 18, back.Y + 8), new PointF(back.X + 11, back.Y + 15), new PointF(back.X + 18, back.Y + 22));
            _hits.Add((back, "back", action));
        }

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
