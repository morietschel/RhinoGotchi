using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;
using RhinoGotchi.Fin;
using RhinoGotchi.Lessons;
using RhinoGotchi.Levels;
using RhinoGotchi.Tutorials;

namespace RhinoGotchi.UI
{
    // A chapter level (Klosterneuburg), laid out like FinGotchi's level page: the chapter title with its
    // orange bar, the level tiles, the animated demo (the pet takes over when you win or run out of
    // misses), the cream lesson card, the yellow Run card, a hint, the steps lit in time with the demo,
    // the ways to run the command, your commands, and ‹ Map / Next › at the bottom. No quiz cards.
    internal sealed partial class LessonCanvas
    {
        private const int MissesToLose = 3;

        // Fonts the shared GetFont cache doesn't make (mono, italic serif), created once.
        private readonly Dictionary<string, Font> _finFonts = new Dictionary<string, Font>();

        private Font FinFont(string key, Func<Font> make)
        {
            if (!_finFonts.TryGetValue(key, out Font? f))
                _finFonts[key] = f = make();
            return f;
        }

        private float PaintTutorialBuild(Graphics g, float w, float h, TutorialBuildStep step)
        {
            Tutorial t = step.Tutorial;
            Lesson lesson = _session.Lesson;
            LevelResult? result = _session.BuildResult;
            bool running = _session.BuildRunning;
            bool solved = _session.BuildSolved;
            bool doneBefore = _game.Progress.IsDone(lesson.Id);
            int misses = _session.BuildMisses;
            double time = PetActivity.Time;

            var chapter = Enumerable.Range(0, LessonCatalog.All.Count).Where(i => LessonCatalog.All[i].Chapter == lesson.Chapter).ToList();
            int number = chapter.IndexOf(_game.PlayingLevel) + 1;

            float col = w - 2 * Pad;
            float y = 14;
            y = TitleBar(g, Pad, y, col, "Klosterneuburg", $"{chapter.Count} commands, one building") + 12;
            y = TileRow(g, w, y, "LEVELS", $"{_game.Progress.ChapterDone(lesson.Chapter)} of {chapter.Count} done", chapter, -1) + 14;

            // The demo, or the pet when it has something to say about this level
            var art = new RectangleF(Pad, y, col, Math.Min(col * 0.56f, 240));
            int activeStep = -1;
            if (solved)
                PetMoment(g, art, PetMood.Win, misses == 0 ? "First try!" : "Solved!");
            else if (misses >= MissesToLose)
                PetMoment(g, art, PetMood.Lose, $"{MissesToLose} misses");
            else
                activeStep = TutorialDemos.Draw(g, art, t, time);
            y = art.Bottom + 12;

            // Lesson card: level, first try or misses, the command, one line
            {
                Font kicker = Bold(10), mono = FinFont("mono18", () => FinType.Mono(18)), body = Regular(13);
                float pad = 13;
                List<string> lines = Wrap(body, t.Intro, col - 2 * pad);
                float ch = pad + LineH(kicker) + 4 + LineH(mono) + 2 + lines.Count * LineH(body) + pad;
                var card = new RectangleF(Pad, y, col, ch);
                g.FillPath(_t.Warm, GraphicsPath.GetRoundRect(card, 10));
                g.DrawPath(new Pen(_t.WarmBorder, 1), GraphicsPath.GetRoundRect(card, 10));
                float cy = y + pad;
                g.DrawText(kicker, _t.AccentText, card.X + pad, cy, $"LEVEL {number} OF {chapter.Count}");
                string status = solved ? (misses == 0 ? "✓ first try" : "✓ solved")
                    : doneBefore ? "✓ solved before"
                    : misses > 0 ? $"misses {Math.Min(misses, MissesToLose)} / {MissesToLose}" : "★ first try possible";
                SizeF ss = kicker.MeasureString(status);
                g.DrawText(kicker, _t.AccentText, card.Right - pad - ss.Width, cy, status);
                cy += LineH(kicker) + 4;
                g.DrawText(mono, _t.Text, card.X + pad, cy, t.MainCommand);
                cy += LineH(mono) + 2;
                foreach (string line in lines)
                {
                    g.DrawText(body, _t.SubtleText, card.X + pad, cy, line);
                    cy += LineH(body);
                }
                y = card.Bottom + 12;
            }

            // Run: the yellow card with the command's icon. It starts the command with everything picked.
            if (!solved)
            {
                var run = new RectangleF(Pad, y, col - 3, 54);
                bool hover = _hover == "run";
                bool pressed = _pressed == "run";
                var shadow = run;
                shadow.Offset(3, 3);
                g.FillPath(_t.Ink, GraphicsPath.GetRoundRect(shadow, 10));
                if (pressed)
                    run.Offset(2, 2);
                g.FillPath(hover ? Color.FromArgb(0xFF, 0xDD, 0x70) : _t.Gold, GraphicsPath.GetRoundRect(run, 10));
                g.DrawPath(new Pen(_t.Ink, 1.5f), GraphicsPath.GetRoundRect(run, 10));
                var tile = new RectangleF(run.X + 9, run.Y + 7, 40, 40);
                g.FillPath(_t.Background, GraphicsPath.GetRoundRect(tile, 8));
                g.DrawPath(new Pen(_t.Ink, 1.3f), GraphicsPath.GetRoundRect(tile, 8));
                LessonIcons.Draw(g, t.MainCommand, new RectangleF(tile.X + 4, tile.Y + 4, 32, 32));
                float tx = tile.Right + 11, tw = run.Right - tx - 8;
                g.DrawText(Bold(15), _t.Ink, tx, run.Y + 9, Ellipsize(Bold(15), "Run " + t.MainCommand, tw));
                g.DrawText(Regular(11), _t.Text, tx, run.Y + 30, Ellipsize(Regular(11), running ? "Starts it with everything picked" : "Sets up the level and starts it", tw));
                _hits.Add((run, "run", () =>
                {
                    if (!_session.BuildRunning)
                        _session.StartBuild();
                    TutorialLevels.Run(t);
                }));
                y = run.Bottom + 16;
            }

            // A hint from the game, set off with a yellow bar
            string? hint = HintFor(t, result, running, solved, misses);
            if (hint != null)
            {
                Font italic = FinFont("italic13", () => FinType.Serif(13, false, true));
                List<string> lines = Wrap(italic, hint, col - 14);
                g.FillRectangle(_t.Gold, Pad, y, 3, lines.Count * LineH(italic));
                foreach (string line in lines)
                {
                    g.DrawText(italic, _t.Text, Pad + 12, y, line);
                    y += LineH(italic);
                }
                y += 12;
            }

            // Steps, lit up in time with the demo
            y = Section(g, y, "STEPS");
            for (int i = 0; i < t.Steps.Count; i++)
            {
                bool on = activeStep == i, dim = activeStep >= 0 && !on;
                float d = 22;
                Cards.Badge(g, Pad, y, d, (i + 1).ToString(), on, _t.Gold, dim);
                float hgt = Wrapped(g, Pad + d + 10, y + 2, col - d - 10, on ? Bold(13) : Regular(13), dim ? _t.DisabledText : _t.Text, t.Steps[i].Text);
                y += Math.Max(d, hgt) + 7;
            }
            y += 8;

            // The live check (also on the viewport's dark card) and the bonus star
            if (result != null && result.Checks.Count > 0)
            {
                y = Section(g, y, "LIVE CHECK");
                foreach (LevelCheck c in result.Checks)
                    y = CheckLine(g, w, y, c.Pass, c.Label, c.Pass ? null : c.Hint);
                y += 4;
            }
            if (t.BonusText != null)
                y = BonusLine(g, w, y, result?.BonusEarned == true, t.BonusText) + 6;

            // Ways to run it
            y = Section(g, y, "WAYS TO DO IT");
            string[] ways =
            {
                $"Type *{t.MainCommand}* and press Enter",
                "Press Run above: everything gets picked for you",
                "Snap to the yellow points in the viewport",
            };
            for (int i = 0; i < ways.Length; i++)
            {
                float rowH = 32;
                var row = new RectangleF(Pad, y, col, rowH);
                g.FillPath(_t.TileHover, GraphicsPath.GetRoundRect(row, 8));
                float d = 19;
                g.FillEllipse(_t.Ink, Pad + 9, y + (rowH - d) / 2, d, d);
                string n = (i + 1).ToString();
                SizeF ns = Bold(10).MeasureString(n);
                g.DrawText(Bold(10), _t.OnInk, Pad + 9 + (d - ns.Width) / 2, y + (rowH - ns.Height) / 2, n);
                RichLine(g, Pad + d + 19, y + (rowH - LineH(Bold(12))) / 2, col - d - 24, ways[i]);
                y += rowH + 5;
            }
            y += 10;

            // Your commands: the chapter's twelve, done ones green, this one yellow
            y = CommandRow(g, w, y, chapter, t) + 16;

            // ‹ Map / Next ›
            float bh = 38, gap = 10, half = (col - gap) / 2;
            float top = Math.Max(y, h - 14 - bh);
            OutlineButton(g, new RectangleF(Pad, top, half, bh), "‹ Map", "map", _game.BackToMap);
            var next = new RectangleF(Pad + half + gap, top, half, bh);
            if (solved || doneBefore)
                OutlineButton(g, next, number == chapter.Count ? "Finish ›" : "Next ›", "next", _session.Continue);
            else
                DisabledPill(g, next, "Next ›");
            return top + bh + 14;
        }

        private static string? HintFor(Tutorial t, LevelResult? result, bool running, bool solved, int misses)
        {
            if (solved)
                return result?.BonusEarned == true || t.BonusText == null
                    ? "Splendid. Press Next for the next part of the building."
                    : $"Solved! Still time for the bonus star: {t.BonusText}.";
            LevelCheck? failing = result?.Checks.FirstOrDefault(c => !c.Pass && !string.IsNullOrEmpty(c.Hint));
            if (failing != null)
                return failing.Hint;
            if (misses >= MissesToLose)
                return "Three misses. Undo, then press Run: it picks the right objects for you.";
            if (!running)
                return "Press Run: the level is set up on its own layers, the model stays untouched.";
            return t.Steps.FirstOrDefault(s => s.ViewHint != null)?.ViewHint ?? "Snap to the yellow points in the viewport.";
        }

        /// <summary>The pet celebrating or losing, big, in place of the demo.</summary>
        private void PetMoment(Graphics g, RectangleF area, PetMood mood, string text)
        {
            g.FillPath(mood == PetMood.Lose ? _t.TileHover : _t.Warm, GraphicsPath.GetRoundRect(area, 12));
            var inner = area;
            inner.Inflate(-area.Width * 0.18f, -area.Height * 0.12f);
            inner.Y += area.Height * 0.06f;
            Pet.Draw(g, inner, mood, PetActivity.Time);
            Font font = Bold(11);
            SizeF size = font.MeasureString(text);
            var r = new RectangleF(area.X + area.Width / 2 - size.Width / 2 - 14, area.Y + 8, size.Width + 28, size.Height + 10);
            g.FillPath(_t.Ink, GraphicsPath.GetRoundRect(r, r.Height / 2));
            g.DrawText(font, _t.OnInk, r.X + 14, r.Y + 5, text);
        }

        private float Section(Graphics g, float y, string title)
        {
            g.DrawText(Bold(10), _t.SubtleText, Pad, y, title);
            return y + LineH(Bold(10)) + 7;
        }

        /// <summary>One line where *Command* is drawn in mono, like FinGotchi's "ways" rows.</summary>
        private void RichLine(Graphics g, float x, float y, float width, string text)
        {
            Font plain = Bold(12), mono = FinFont("mono12", () => FinType.Mono(12));
            float end = x + width;
            string[] parts = text.Split('*');
            for (int i = 0; i < parts.Length && end - x > 10; i++)
            {
                if (parts[i].Length == 0)
                    continue;
                Font f = i % 2 == 1 ? mono : plain;
                string s = Ellipsize(f, parts[i], end - x);
                g.DrawText(f, _t.Text, x, y, s);
                x += f.MeasureString(s).Width;
            }
        }

        private void DisabledPill(Graphics g, RectangleF rect, string text)
        {
            float radius = Math.Min(rect.Height / 2, 18);
            g.DrawPath(new Pen(_t.Border, 1.4f), GraphicsPath.GetRoundRect(rect, radius));
            Font font = Bold(13);
            SizeF s = font.MeasureString(text);
            g.DrawText(font, _t.DisabledText, rect.X + (rect.Width - s.Width) / 2, rect.Y + (rect.Height - s.Height) / 2, text);
        }

        /// <summary>
        /// "YOUR COMMANDS": the chapter's command icons. Solved ones have a green edge and a tick, the one
        /// you're on is yellow (click it to run), later ones grey.
        /// </summary>
        private float CommandRow(Graphics g, float w, float y, IList<int> chapter, Tutorial current)
        {
            y = Section(g, y, "YOUR COMMANDS");
            int cols = 6;
            float gap = 6, cw = (w - 2 * Pad - gap * (cols - 1)) / cols, chh = 50;
            Font label = Bold(8);
            for (int k = 0; k < chapter.Count; k++)
            {
                Lesson lesson = LessonCatalog.All[chapter[k]];
                Tutorial? t = lesson.Steps.OfType<TutorialBuildStep>().FirstOrDefault()?.Tutorial;
                if (t == null)
                    continue;
                bool now = t.Id == current.Id, done = _game.Progress.IsDone(lesson.Id);
                var r = new RectangleF(Pad + (k % cols) * (cw + gap), y + (k / cols) * (chh + gap), cw, chh);
                string id = $"cmd{k}";
                Color fill = now ? (_hover == id ? Color.FromArgb(0xFF, 0xDD, 0x70) : _t.Gold) : _t.Background;
                Color edge = now ? _t.Ink : done ? Color.FromArgb(0xBF, 0xE0, 0xCA) : _t.Border;
                g.FillPath(fill, GraphicsPath.GetRoundRect(r, 8));
                g.DrawPath(new Pen(edge, now ? 1.8f : 1.2f), GraphicsPath.GetRoundRect(r, 8));
                float icon = Math.Min(24, cw - 10);
                LessonIcons.Draw(g, t.MainCommand, new RectangleF(r.X + (cw - icon) / 2, r.Y + 5, icon, icon));
                if (!now && !done)
                {
                    g.FillPath(Color.FromArgb(255, 255, 255, 150), GraphicsPath.GetRoundRect(r, 8)); // greyed out
                    g.DrawPath(new Pen(edge, 1.2f), GraphicsPath.GetRoundRect(r, 8));
                }
                string text = Ellipsize(label, t.MainCommand + (done ? " ✓" : ""), cw - 4);
                SizeF ts = label.MeasureString(text);
                g.DrawText(label, now ? _t.Ink : done ? _t.Green : _t.DisabledText, r.X + (cw - ts.Width) / 2, r.Bottom - ts.Height - 5, text);
                if (now && !_session.BuildSolved)
                    _hits.Add((r, id, () =>
                    {
                        if (!_session.BuildRunning)
                            _session.StartBuild();
                        TutorialLevels.Run(current);
                    }));
            }
            int rows = (chapter.Count + cols - 1) / cols;
            return y + rows * (chh + gap) - gap;
        }

        private float CheckLine(Graphics g, float w, float y, bool pass, string label, string? hint)
        {
            var dot = new RectangleF(Pad, y + 1, 20, 20);
            g.FillEllipse(pass ? _t.Green : _t.Red, dot);
            var pen = new Pen(Colors.White, 2.6f) { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            if (pass)
                g.DrawLines(pen, new PointF(dot.X + 5, dot.Y + 10.5f), new PointF(dot.X + 8.5f, dot.Y + 14), new PointF(dot.X + 15, dot.Y + 6.5f));
            else
            {
                g.DrawLine(pen, dot.X + 6.5f, dot.Y + 6.5f, dot.Right - 6.5f, dot.Bottom - 6.5f);
                g.DrawLine(pen, dot.Right - 6.5f, dot.Y + 6.5f, dot.X + 6.5f, dot.Bottom - 6.5f);
            }
            float hgt = Wrapped(g, Pad + 28, y + 1, w - 2 * Pad - 28, Bold(13), _t.Text, label);
            y += Math.Max(22, hgt) + 2;
            if (!string.IsNullOrEmpty(hint))
                y += Wrapped(g, Pad + 28, y, w - 2 * Pad - 28, Regular(12), _t.RedText, hint!) + 4;
            return y + 4;
        }

        private float BonusLine(Graphics g, float w, float y, bool earned, string text)
        {
            Color c = earned ? _t.Gold : _t.Quiet;
            var star = new RectangleF(Pad, y, 20, 20);
            var pts = Enumerable.Range(0, 10).Select(i =>
            {
                double a = -Math.PI / 2 + i * Math.PI / 5;
                float r = i % 2 == 0 ? 10f : 4.2f;
                return new PointF(star.X + 10 + r * (float)Math.Cos(a), star.Y + 10 + r * (float)Math.Sin(a));
            }).ToArray();
            g.FillPolygon(c, pts);
            if (earned)
                g.DrawPolygon(new Pen(_t.Ink, 1), pts);
            float hgt = Wrapped(g, Pad + 28, y + 1, w - 2 * Pad - 28, Regular(13), earned ? _t.Text : _t.SubtleText, "Bonus: " + text);
            return y + Math.Max(22, hgt) + 6;
        }
    }
}
