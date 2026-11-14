using System;
using System.Linq;
using Eto.Drawing;
using RhinoGotchi.Lessons;
using RhinoGotchi.Levels;
using RhinoGotchi.Tutorials;

namespace RhinoGotchi.UI
{
    // A Klosterneuburg tutorial card, played like FinGotchi: the kicker and headline, one line, at most
    // three numbered steps, a yellow RUN button that starts the command with the right objects picked,
    // and a live check list (✓/✗ per check, the bonus star, misses) that updates after every command.
    internal sealed partial class LessonCanvas
    {
        private float PaintTutorialBuild(Graphics g, float w, float h, TutorialBuildStep step)
        {
            Tutorial t = step.Tutorial;
            LevelResult? result = _session.BuildResult;
            bool running = _session.BuildRunning;
            int misses = _session.BuildMisses;

            float y = TopBar(g, w);
            g.DrawText(Bold(12), _t.Orange, Pad, y, t.Kicker);
            y += 22;
            y += Wrapped(g, Pad, y, w - 2 * Pad, Bold(21), _t.Text, t.Title) + 4;
            y += Wrapped(g, Pad, y, w - 2 * Pad, Regular(13), _t.SubtleText, t.Intro) + 10;

            Mood mood = _session.BuildSolved ? Mood.Celebrate : misses >= 3 ? Mood.Sad : Mood.Thinking;
            string bubble = _session.BuildSolved ? (result?.BonusEarned == true ? "Splendid, bonus star too!" : "Splendid! That's it.")
                : misses >= 3 ? "Tricky one! Read the red line below, or press RUN again."
                : running ? "Snap to the yellow points."
                : $"Press START, then RUN: {t.MainCommand} starts with everything picked.";
            y = MascotRow(g, w, y, mood, bubble, 0) + 12;

            // Command card: icon, name and what it does.
            float cardH = 84;
            var card = new RectangleF(Pad, y, w - 2 * Pad, cardH);
            Card(g, card, _t.TileFace, _t.Border);
            var iconRect = new RectangleF(card.X + 8, card.Y + 8, cardH - 16, cardH - 16);
            LessonIcons.Draw(g, t.MainCommand, iconRect);
            float tx = iconRect.Right + 8, tW = card.Right - tx - 12;
            g.DrawText(Bold(16), _t.Text, tx, card.Y + 12, t.MainCommand);
            Wrapped(g, tx, card.Y + 34, tW, Regular(12), _t.SubtleText, CommandInfo.Tip(t.MainCommand), maxLines: 3);
            y += cardH + Gap;

            // The steps, numbered.
            for (int i = 0; i < t.Steps.Count; i++)
            {
                var badge = new RectangleF(Pad, y, 22, 22);
                g.FillEllipse(_t.Gold, badge);
                string n = (i + 1).ToString();
                SizeF ns = Bold(12).MeasureString(n);
                g.DrawText(Bold(12), _t.Ink, badge.X + (22 - ns.Width) / 2, badge.Y + (22 - ns.Height) / 2, n);
                float hgt = Wrapped(g, Pad + 30, y + 2, w - 2 * Pad - 30, Regular(13), _t.Text, t.Steps[i].Text);
                y += Math.Max(24, hgt) + 6;
            }
            y += 4;

            // The live check list.
            if (running || result != null)
            {
                Pill(g, Pad, y, running ? "LIVE CHECK" : "CHECK", running ? _t.Green : _t.SubtleText);
                if (misses > 0)
                {
                    string m = misses == 1 ? "1 miss" : $"{misses} misses";
                    SizeF ms = Regular(12).MeasureString(m);
                    g.DrawText(Regular(12), _t.SubtleText, w - Pad - ms.Width, y + 3, m);
                }
                y += 30;
                if (result == null || result.Checks.Count == 0)
                {
                    y += Wrapped(g, Pad, y, w - 2 * Pad, Regular(13), _t.SubtleText, "Waiting for your first command...") + 6;
                }
                else
                {
                    foreach (LevelCheck c in result.Checks)
                    {
                        y = CheckLine(g, w, y, c.Pass, c.Label, c.Pass ? null : c.Hint);
                    }
                }
                if (t.BonusText != null)
                    y = BonusLine(g, w, y, result?.BonusEarned == true, t.BonusText);
                y += 4;
            }
            else if (t.BonusText != null)
            {
                y = BonusLine(g, w, y, false, t.BonusText) + 4;
            }

            if (_session.BuildSolved)
            {
                int xp = (int)Math.Round(step.Xp * (result?.Score ?? 0));
                string body = result?.BonusEarned == true ? "Every check passes, and you earned the bonus star."
                    : t.BonusText != null ? $"Every check passes. Still time for the bonus: {t.BonusText}."
                    : "Every check passes.";
                return Verdict(g, w, h, y + Gap, true, "Card solved!", body, xp > 0 ? $"+{xp} XP" : "", "CONTINUE", _session.Continue);
            }

            // RUN (or START) + RESTART / SKIP
            float footerTop = y + Gap;
            float footerH = Pad + ButtonH + 4 + 10 + 40 + Pad;
            float top = Math.Max(footerTop, h - footerH);
            g.DrawLine(new Pen(_t.Border, 2), 0, top, w, top);
            var main = new RectangleF(Pad, top + Pad, w - 2 * Pad, ButtonH);
            if (running)
                ChunkyButton(g, main, $"RUN {t.MainCommand.ToUpperInvariant()}", _t.Gold, _t.GoldShadow, _t.Ink, "primary", () => TutorialLevels.Run(t));
            else
                ChunkyButton(g, main, "START", _t.Ink, _t.InkShadow, _t.OnInk, "primary", _session.StartBuild);
            float half = (w - 2 * Pad - 10) / 2;
            var left = new RectangleF(Pad, main.Bottom + 4 + 10, half, 40);
            var right = new RectangleF(left.Right + 10, left.Y, half, 40);
            if (running)
                OutlineButton(g, left, "START OVER", "restart", _session.StartBuild);
            else
                OutlineButton(g, left, "MAP", "map", _game.BackToMap);
            OutlineButton(g, right, "SKIP", "skip", _session.Continue);
            return footerTop + footerH;
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
            float hgt = Wrapped(g, Pad + 28, y + 1, w - 2 * Pad - 28, Regular(13), earned ? _t.Text : _t.SubtleText, "Bonus: " + text);
            return y + Math.Max(22, hgt) + 6;
        }
    }
}
