using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;

namespace GotchiFins
{
    /// <summary>
    /// The RhinoGotchi cards, drawn with Eto so the panel and the viewport (via bitmaps) look
    /// the same: the lesson card, the dark check card, the yellow "type this" callout, the
    /// pet's speech bubble and the recorded-steps pill. Each method draws at (x, y) in a
    /// given width, scaled by <c>s</c>, and returns the height it used.
    /// </summary>
    internal static class Cards
    {
        // ---------------------------------------------------------------- text helpers

        public static float H(Graphics g, Font font) => g.MeasureString(font, "Ag").Height;

        public static IEnumerable<string> Wrap(Graphics g, Font font, string text, float maxWidth)
        {
            foreach (string paragraph in text.Split('\n'))
            {
                string line = "";
                foreach (string word in paragraph.Split(' '))
                {
                    string candidate = line.Length == 0 ? word : line + " " + word;
                    if (line.Length > 0 && g.MeasureString(font, candidate).Width > maxWidth)
                    {
                        yield return line;
                        line = word;
                    }
                    else
                    {
                        line = candidate;
                    }
                }
                yield return line;
            }
        }

        /// <summary>Letter-spaced small capitals, the way the RhinoGotchi cards set kickers.</summary>
        public static float Spaced(Graphics g, Font font, Color color, string text, float x, float y, float spacing, bool centered = false)
        {
            float[] widths = text.Select(c => g.MeasureString(font, c.ToString()).Width).ToArray();
            float total = widths.Sum() + spacing * (text.Length - 1);
            float at = centered ? x - total / 2 : x;
            for (int i = 0; i < text.Length; i++)
            {
                g.DrawText(font, color, at, y, text[i].ToString());
                at += widths[i] + spacing;
            }
            return total;
        }

        /// <summary>
        /// Draws text where words inside *stars* are set in the command (mono, bold) font, wrapping
        /// at <paramref name="width"/>. Returns the height used.
        /// </summary>
        public static float Rich(Graphics g, string text, float x, float y, float width, Font plain, Font command, Color color)
        {
            float lineH = Math.Max(H(g, plain), H(g, command)) * 1.25f;
            float cx = x, cy = y, space = g.MeasureString(plain, " ").Width;
            if (space < 1)
                space = g.MeasureString(plain, "a a").Width - g.MeasureString(plain, "aa").Width;
            foreach (string raw in text.Split(' '))
            {
                bool isCommand = raw.StartsWith("*") && raw.TrimEnd('.', ',', '!', '?').EndsWith("*");
                string word = raw.Replace("*", "");
                Font f = isCommand ? command : plain;
                float w = g.MeasureString(f, word).Width;
                if (cx > x && cx + w > x + width)
                {
                    cx = x;
                    cy += lineH;
                }
                g.DrawText(f, color, cx, cy + (lineH - H(g, f)) / 2, word);
                cx += w + space;
            }
            return cy + lineH - y;
        }

        private static void Round(Graphics g, RectangleF r, float radius, Color fill, Color? stroke = null, float strokeWidth = 1, float hardShadow = 0)
        {
            if (hardShadow > 0)
            {
                var sh = r;
                sh.Offset(hardShadow, hardShadow);
                g.FillPath(Palette.Ink, GraphicsPath.GetRoundRect(sh, radius));
            }
            g.FillPath(fill, GraphicsPath.GetRoundRect(r, radius));
            if (stroke.HasValue)
                g.DrawPath(new Pen(stroke.Value, strokeWidth), GraphicsPath.GetRoundRect(r, radius));
        }

        /// <summary>A soft drop shadow under a floating card.</summary>
        private static void Soft(Graphics g, RectangleF r, float radius, float s)
        {
            for (int i = 3; i >= 1; i--)
            {
                var sh = r;
                sh.Inflate(i * 1.5f * s, i * 1.5f * s);
                sh.Offset(0, 3 * s);
                g.FillPath(Color.FromArgb(0, 0, 0, 10), GraphicsPath.GetRoundRect(sh, radius + i * 1.5f * s));
            }
        }

        // ---------------------------------------------------------------- lesson card

        /// <summary>
        /// White card with an orange top edge: "FIN FACADE · LEVEL n OF 5", the headline, the
        /// command chip, the steps (the one at <paramref name="activeStep"/> highlighted, -1 for
        /// none), and how many misses so far.
        /// </summary>
        public static float Lesson(Graphics g, float x, float y, float width, float s, GamePage page, int activeStep, int misses, bool solved)
        {
            using Font kicker = Typeface.Sans(7.2f * s, true);
            using Font headline = Typeface.Serif(17 * s);
            using Font chipFont = Typeface.Mono(9 * s);
            using Font step = Typeface.Sans(9 * s);
            using Font stepBold = Typeface.Sans(9 * s, true);
            using Font num = Typeface.Sans(7 * s, true);
            float pad = 14 * s, inner = width - 2 * pad;

            // Measure first
            List<string> head = Wrap(g, headline, page.Headline, inner).ToList();
            float h = pad + 4 * s + H(g, kicker) + 4 * s + head.Count * H(g, headline) + 6 * s + H(g, chipFont) + 6 * s + 8 * s;
            float rowH = Math.Max(H(g, step), 17 * s) + 4 * s;
            int rows = solved ? 1 : page.Steps.Length;
            h += rows * rowH + (misses > 0 && !solved ? H(g, step) * 1.2f : 0) + pad - 6 * s;

            var card = new RectangleF(x, y, width, h);
            Soft(g, card, 14 * s, s);
            Round(g, card, 14 * s, Palette.Paper, Palette.Light, 1);
            g.SaveTransform();
            g.SetClip(new RectangleF(x, y, width, 4 * s));
            Round(g, card, 14 * s, Palette.Orange);
            g.ResetClip();
            g.RestoreTransform();

            float cy = y + pad + 4 * s;
            Spaced(g, kicker, Palette.Muted, page.Kicker.Replace("LEVEL", "FIN FACADE · LEVEL"), x + pad, cy, 1.4f * s);
            cy += H(g, kicker) + 4 * s;
            foreach (string line in head)
            {
                g.DrawText(headline, Palette.Ink, x + pad, cy, line);
                cy += H(g, headline);
            }
            cy += 6 * s;
            if (page.Command != null)
            {
                SizeF cs = g.MeasureString(chipFont, page.Command);
                var chip = new RectangleF(x + pad, cy - 1 * s, cs.Width + 14 * s, cs.Height + 2 * s);
                Round(g, chip, 4 * s, Palette.Ink);
                g.DrawText(chipFont, Palette.Paper, chip.X + 7 * s, chip.Y + 1 * s, page.Command);
            }
            cy += H(g, chipFont) + 14 * s;

            if (solved)
            {
                Badge(g, x + pad, cy, 17 * s, "✓", true, Palette.GreenSoft);
                g.DrawText(stepBold, Palette.Ink, x + pad + 25 * s, cy + (17 * s - H(g, stepBold)) / 2, "Solved!");
                return h;
            }
            for (int i = 0; i < page.Steps.Length; i++)
            {
                bool on = activeStep == i, dim = activeStep >= 0 && !on;
                Badge(g, x + pad, cy, 17 * s, (i + 1).ToString(), on, Palette.Accent, dim);
                g.DrawText(on ? stepBold : step, dim ? Palette.Faint : Palette.Ink, x + pad + 25 * s, cy + (17 * s - H(g, step)) / 2, page.Steps[i]);
                cy += rowH;
            }
            if (misses > 0)
                g.DrawText(step, Palette.OrangeText, x + pad, cy + 2 * s, $"Misses {misses} / {FinSession.MissesToLose}");
            return h;
        }

        /// <summary>A numbered circle: filled when on, outlined otherwise.</summary>
        public static void Badge(Graphics g, float x, float y, float d, string text, bool on, Color fill, bool dim = false)
        {
            g.FillEllipse(on ? fill : Palette.Paper, x, y, d, d);
            g.DrawEllipse(new Pen(dim ? Palette.Rule : Palette.Ink, Math.Max(1, d / 14)), x, y, d, d);
            using Font f = Typeface.Sans(d * 0.42f, true);
            SizeF size = g.MeasureString(f, text);
            g.DrawText(f, dim ? Palette.Faint : Palette.Ink, x + (d - size.Width) / 2, y + (d - size.Height) / 2, text);
        }

        // ---------------------------------------------------------------- check card

        /// <summary>The dark check card: "LEVEL CHECK", a big match percentage, a meter, and a row per check.</summary>
        public static float Check(Graphics g, float x, float y, float width, float s, IReadOnlyList<Check> checks, double match, bool solved)
        {
            using Font kicker = Typeface.Sans(7.2f * s, true);
            using Font pill = Typeface.Sans(6.5f * s, true);
            using Font big = Typeface.Serif(24 * s);
            using Font row = Typeface.Sans(9 * s, true);
            using Font detail = Typeface.Mono(8 * s, false);
            float pad = 12 * s, rowH = H(g, row) + 8 * s;
            float h = pad + H(g, kicker) + 6 * s + H(g, big) + 14 * s + checks.Count * rowH + pad - 4 * s;

            var card = new RectangleF(x, y, width, h);
            Soft(g, card, 14 * s, s);
            Round(g, card, 14 * s, Palette.Night);

            float cy = y + pad;
            Spaced(g, kicker, Palette.Paper, "LEVEL CHECK", x + pad, cy, 1.4f * s);
            string state = solved ? "✓ SOLVED" : "● CHECKING";
            SizeF ps = g.MeasureString(pill, state);
            var pr = new RectangleF(x + width - pad - ps.Width - 12 * s, cy - 2 * s, ps.Width + 12 * s, ps.Height + 4 * s);
            g.DrawPath(new Pen(Color.FromArgb(0x55, 0x55, 0x55), 1), GraphicsPath.GetRoundRect(pr, pr.Height / 2));
            g.DrawText(pill, solved ? Palette.GreenSoft : Palette.Accent, pr.X + 6 * s, pr.Y + 2 * s, state);
            cy += H(g, kicker) + 6 * s;

            string pct = $"{Math.Round(match * 100):0}%";
            g.DrawText(big, Palette.Paper, x + pad, cy, pct);
            cy += H(g, big) + 2 * s;
            var meter = new RectangleF(x + pad, cy, width - 2 * pad, 5 * s);
            Round(g, meter, 2.5f * s, Color.FromArgb(0x33, 0x33, 0x33));
            if (match > 0)
                Round(g, new RectangleF(meter.X, meter.Y, (float)(meter.Width * Math.Min(1, match)), meter.Height), 2.5f * s, Palette.Paper);
            cy += 12 * s;

            foreach (Check c in checks)
            {
                g.FillRectangle(Palette.NightRule, x + pad, cy, width - 2 * pad, Math.Max(1, s * 0.8f));
                float ty = cy + 4 * s;
                g.DrawText(row, c.Pass ? Palette.GreenSoft : Palette.MissSoft, x + pad, ty, (c.Pass ? "✓ " : "✕ ") + c.Label);
                SizeF ds = g.MeasureString(detail, c.Detail);
                g.DrawText(detail, Color.FromArgb(0xBB, 0xBB, 0xBB), x + width - pad - ds.Width, ty + (H(g, row) - H(g, detail)) / 2, c.Detail);
                cy += rowH;
            }
            return h;
        }

        // ---------------------------------------------------------------- callout

        /// <summary>The yellow "type this up here" callout, with an up arrow and a hard black shadow.</summary>
        public static SizeF Callout(Graphics g, float x, float y, float s, string text, float maxWidth)
        {
            using Font plain = Typeface.Sans(9.5f * s);
            using Font cmd = Typeface.Mono(9.5f * s);
            using Font arrow = Typeface.Sans(13 * s, true);
            float pad = 11 * s, arrowW = g.MeasureString(arrow, "↑").Width + 8 * s;
            float textW = Math.Min(maxWidth - 2 * pad - arrowW, MeasureRich(g, text, plain, cmd));
            float textH = Rich(g, text, -10000, -10000, textW, plain, cmd, Palette.Ink); // measure only
            var r = new RectangleF(x, y, textW + 2 * pad + arrowW, textH + 2 * pad - 4 * s);
            Round(g, r, 10 * s, Palette.Accent, Palette.Ink, 1.5f * s, 3 * s);
            g.DrawText(arrow, Palette.Ink, r.X + pad, r.Y + (r.Height - H(g, arrow)) / 2, "↑");
            Rich(g, text, r.X + pad + arrowW, r.Y + pad - 2 * s, textW + 1, plain, cmd, Palette.Ink);
            return r.Size + new SizeF(3 * s, 3 * s);
        }

        private static float MeasureRich(Graphics g, string text, Font plain, Font cmd)
        {
            float space = Math.Max(1, g.MeasureString(plain, " ").Width);
            return text.Split(' ').Sum(w => g.MeasureString(w.StartsWith("*") ? cmd : plain, w.Replace("*", "")).Width + space) - space + 2;
        }

        // ---------------------------------------------------------------- speech bubble

        /// <summary>The pet's speech bubble: white, rounded, black border and hard shadow, tail on the left.</summary>
        public static SizeF Bubble(Graphics g, float x, float y, float s, string text, float maxWidth)
        {
            using Font plain = Typeface.Sans(9.5f * s);
            using Font cmd = Typeface.Mono(9.5f * s);
            float pad = 10 * s;
            float textW = Math.Min(maxWidth - 2 * pad - 13 * s, MeasureRich(g, text, plain, cmd)); // room for the tail and shadow
            float textH = Rich(g, text, -10000, -10000, textW, plain, cmd, Palette.Ink);
            var r = new RectangleF(x + 9 * s, y, textW + 2 * pad, textH + 2 * pad - 4 * s);
            Round(g, r, 12 * s, Palette.Paper, Palette.Ink, 1.5f * s, 2 * s);
            float ty = r.Bottom - 16 * s;
            g.FillPolygon(Palette.Ink, new PointF(r.X + 1, ty - 8 * s), new PointF(x, ty), new PointF(r.X + 1, ty + 8 * s));
            g.FillPolygon(Palette.Paper, new PointF(r.X + 1.5f * s + 1, ty - 6 * s), new PointF(x + 3 * s, ty), new PointF(r.X + 1.5f * s + 1, ty + 6 * s));
            Rich(g, text, r.X + pad, r.Y + pad - 2 * s, textW + 1, plain, cmd, Palette.Ink);
            return new SizeF(r.Right + 2 * s - x, r.Height + 2 * s);
        }

        // ---------------------------------------------------------------- recorded steps

        /// <summary>The "RECORDED STEPS" pill: the commands that changed the model, as chips.</summary>
        public static SizeF Recorded(Graphics g, float x, float y, float s, IReadOnlyList<string> commands)
        {
            using Font kicker = Typeface.Sans(6.8f * s, true);
            using Font chip = Typeface.Mono(8.5f * s, false);
            using Font text = Typeface.Sans(8.5f * s);
            float pad = 12 * s, h = H(g, chip) + 14 * s;
            string none = "None yet. Use the command line, a RhinoGotchi icon, or the panel.";
            float w = pad + g.MeasureString(kicker, "RECORDED STEPS").Width + 12 * s + 8 * s;
            if (commands.Count == 0)
                w += g.MeasureString(text, none).Width;
            else
                w += commands.Sum(c => g.MeasureString(chip, c).Width + 10 * s) + (commands.Count - 1) * 18 * s;
            w += pad;

            var r = new RectangleF(x, y, w, h);
            Soft(g, r, h / 2, s);
            Round(g, r, h / 2, Palette.Paper, Palette.Light, 1);
            float cx = x + pad, my = y + h / 2;
            cx += Spaced(g, kicker, Palette.Muted, "RECORDED STEPS", cx, my - H(g, kicker) / 2, 1.2f * s) + 12 * s;
            if (commands.Count == 0)
            {
                g.DrawText(text, Palette.Body, cx, my - H(g, text) / 2, none);
            }
            else
            {
                for (int i = 0; i < commands.Count; i++)
                {
                    SizeF cs = g.MeasureString(chip, commands[i]);
                    var cr = new RectangleF(cx, my - cs.Height / 2 - 1 * s, cs.Width + 10 * s, cs.Height + 2 * s);
                    Round(g, cr, 3 * s, Palette.Wash);
                    g.DrawText(chip, Palette.Ink, cr.X + 5 * s, cr.Y + 1 * s, commands[i]);
                    cx = cr.Right;
                    if (i + 1 < commands.Count)
                    {
                        g.DrawText(text, Palette.Muted, cx + 4 * s, my - H(g, text) / 2, "→");
                        cx += 18 * s;
                    }
                }
            }
            return new SizeF(w, h);
        }
    }
}
