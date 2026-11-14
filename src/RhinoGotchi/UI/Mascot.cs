using System;
using Eto.Drawing;
using RhinoGotchi.Lessons;

namespace RhinoGotchi.UI
{
    internal enum Mood
    {
        Neutral,
        Happy,
        Sad,
        /// <summary>Closed happy eyes, open smile and sparkles: a win.</summary>
        Celebrate,
        /// <summary>Closed eyes and a little "z": nothing played today yet.</summary>
        Sleepy,
        /// <summary>Eyes looking up: waiting for an answer.</summary>
        Thinking,
    }

    /// <summary>
    /// Gotchi, the little gray rhino, with a hat on. Drawn in a 100x100 box with the same chunky
    /// shading as the buttons (a darker lip under the head). Every RhinoGotchi screen uses this.
    /// </summary>
    internal static class Mascot
    {
        private static Color C(int rgb, int a = 255) => Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF, a);

        private static readonly Color Skin = C(0xA9B0B9);
        private static readonly Color SkinDark = C(0x868D96);
        private static readonly Color SkinLight = C(0xC4CAD1);
        private static readonly Color Belly = C(0xF3EAD6);
        private static readonly Color Horn = C(0xFFF4D6);
        private static readonly Color HornEdge = C(0xE0C27A);
        private static readonly Color Eye = C(0x222228);
        private static readonly Color Blush = C(0xF2A59B);
        private static readonly Color Gold = C(0xF5B700);
        private static readonly Color GoldDark = C(0xD49A00);

        /// <summary>The hat to draw when a caller doesn't pick one: whatever the player put on.</summary>
        public static Func<Hat> WornHat { get; set; } = () => Hat.None;

        /// <param name="bob">Vertical hop in box units (negative = up).</param>
        /// <param name="hat">Hat to wear; null wears <see cref="WornHat"/>.</param>
        /// <param name="breathe">0..1, a slow idle squash so Gotchi looks alive.</param>
        public static void Draw(Graphics g, RectangleF rect, Mood mood, float bob = 0, Hat? hat = null, float breathe = 0)
        {
            float size = Math.Min(rect.Width, rect.Height);
            g.SaveTransform();
            g.TranslateTransform(rect.X + (rect.Width - size) / 2, rect.Y + (rect.Height - size) / 2);
            g.ScaleTransform(size / 100f);
            g.AntiAlias = true;

            // Shadow stays on the ground while Gotchi hops.
            float shrink = Math.Min(10, Math.Abs(bob) * 0.6f);
            g.FillEllipse(C(0x000000, 30), 20 + shrink, 89, 60 - 2 * shrink, 9);

            g.TranslateTransform(0, bob);
            if (breathe != 0)
            {
                // Squash from the feet up.
                g.TranslateTransform(50, 92);
                g.ScaleTransform(1 - 0.012f * breathe, 1 + 0.03f * breathe);
                g.TranslateTransform(-50, -92);
            }

            Body(g);
            Face(g, mood);
            DrawHat(g, hat ?? WornHat());
            Extras(g, mood);

            g.RestoreTransform();
        }

        private static void Body(Graphics g)
        {
            // Ears
            g.FillEllipse(SkinDark, 14, 14, 20, 26);
            g.FillEllipse(SkinDark, 66, 14, 20, 26);
            g.FillEllipse(Blush, 19, 20, 10, 14);
            g.FillEllipse(Blush, 71, 20, 10, 14);

            // Head on a darker lip (the Duolingo-style chunky edge), plus a soft shine.
            g.FillEllipse(SkinDark, 12, 25, 76, 68);
            g.FillEllipse(Skin, 12, 22, 76, 68);
            g.FillEllipse(SkinLight, 24, 27, 26, 11);

            // Snout
            g.FillEllipse(Belly, 26, 56, 48, 32);

            // Horn
            var horn = new GraphicsPath();
            horn.MoveTo(40, 62);
            horn.AddBezier(new PointF(40, 62), new PointF(44, 42), new PointF(52, 34), new PointF(60, 30));
            horn.AddBezier(new PointF(60, 30), new PointF(58, 42), new PointF(60, 54), new PointF(60, 62));
            horn.CloseFigure();
            g.FillPath(Horn, horn);
            g.DrawPath(new Pen(HornEdge, 2f) { LineJoin = PenLineJoin.Round }, horn);

            // Cheeks and nostrils
            g.FillEllipse(C(0xF2A59B, 150), 17, 55, 12, 8);
            g.FillEllipse(C(0xF2A59B, 150), 71, 55, 12, 8);
            g.FillEllipse(SkinDark, 38, 70, 5, 4);
            g.FillEllipse(SkinDark, 57, 70, 5, 4);
        }

        private static void Face(Graphics g, Mood mood)
        {
            var line = new Pen(Eye, 4f) { LineCap = PenLineCap.Round };
            var mouth = new Pen(Eye, 2.6f) { LineCap = PenLineCap.Round };

            switch (mood)
            {
                case Mood.Celebrate:
                    g.DrawArc(line, 24, 40, 14, 12, 200, 140);
                    g.DrawArc(line, 62, 40, 14, 12, 200, 140);
                    // Open smile
                    var open = new GraphicsPath();
                    open.MoveTo(42, 76);
                    open.AddBezier(new PointF(42, 76), new PointF(44, 86), new PointF(56, 86), new PointF(58, 76));
                    open.CloseFigure();
                    g.FillPath(Eye, open);
                    g.FillEllipse(C(0xF07C7C), 46, 80, 8, 4);
                    return;
                case Mood.Sleepy:
                    g.DrawArc(line, 24, 38, 14, 12, 20, 140);
                    g.DrawArc(line, 62, 38, 14, 12, 20, 140);
                    g.FillEllipse(Eye, 47, 77, 6, 5);
                    return;
            }

            // Open eyes
            g.FillEllipse(Colors.White, 23, 36, 17, 19);
            g.FillEllipse(Colors.White, 60, 36, 17, 19);
            float px = mood == Mood.Thinking ? -2 : 0;
            float py = mood == Mood.Sad ? 45 : mood == Mood.Thinking ? 38 : 42;
            g.FillEllipse(Eye, 28 + px, py, 10, 11);
            g.FillEllipse(Eye, 64 + px, py, 10, 11);
            g.FillEllipse(Colors.White, 31 + px, py + 2, 4, 4);
            g.FillEllipse(Colors.White, 67 + px, py + 2, 4, 4);

            switch (mood)
            {
                case Mood.Sad:
                    var brow = new Pen(SkinDark, 3f) { LineCap = PenLineCap.Round };
                    g.DrawLine(brow, 23, 34, 37, 31);
                    g.DrawLine(brow, 77, 34, 63, 31);
                    g.DrawArc(mouth, 42, 79, 16, 8, 200, 140);
                    break;
                case Mood.Thinking:
                    g.DrawLine(mouth, 45, 79, 55, 78);
                    break;
                case Mood.Happy:
                    g.DrawArc(mouth, 40, 72, 20, 11, 20, 140);
                    break;
                default:
                    g.DrawArc(mouth, 42, 74, 16, 9, 20, 140);
                    break;
            }
        }

        private static void Extras(Graphics g, Mood mood)
        {
            if (mood == Mood.Celebrate)
            {
                Sparkle(g, 8, 26, 7);
                Sparkle(g, 92, 18, 6);
                Sparkle(g, 94, 56, 4.5f);
                Sparkle(g, 4, 60, 4);
            }
            else if (mood == Mood.Sleepy)
            {
                var z = new Pen(SkinDark, 2.4f) { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
                ZLetter(g, z, 80, 8, 9);
                ZLetter(g, z, 91, 0, 6);
            }
        }

        private static void Sparkle(Graphics g, float cx, float cy, float r)
        {
            g.FillPolygon(Gold,
                new PointF(cx, cy - r), new PointF(cx + r * 0.28f, cy - r * 0.28f), new PointF(cx + r, cy),
                new PointF(cx + r * 0.28f, cy + r * 0.28f), new PointF(cx, cy + r), new PointF(cx - r * 0.28f, cy + r * 0.28f),
                new PointF(cx - r, cy), new PointF(cx - r * 0.28f, cy - r * 0.28f));
        }

        private static void ZLetter(Graphics g, Pen pen, float x, float y, float s) =>
            g.DrawLines(pen, new PointF(x, y), new PointF(x + s, y), new PointF(x, y + s), new PointF(x + s, y + s));

        // ------------------------------------------------------------------ hats

        private static void DrawHat(Graphics g, Hat hat)
        {
            switch (hat)
            {
                case Hat.HardHat:
                {
                    var dome = new GraphicsPath();
                    dome.AddArc(29, 5, 42, 38, 180, 180);
                    dome.CloseFigure();
                    g.FillPath(C(0xFFD34D), dome);
                    g.FillRectangle(C(0xFFE58A), 47, 6, 6, 18);
                    g.FillPath(C(0xE0B100), GraphicsPath.GetRoundRect(new RectangleF(23, 21, 54, 7), 3.5f));
                    break;
                }
                case Hat.Beret:
                    g.FillRectangle(C(0x7E2621), 49, 4, 3, 6);
                    g.FillEllipse(C(0xAE342D), 25, 12, 52, 16);
                    g.FillEllipse(C(0xD6453D), 25, 9, 52, 16);
                    g.FillEllipse(C(0xE87169), 34, 11, 18, 5);
                    break;
                case Hat.Grad:
                    g.FillPath(C(0x1E1E1E), GraphicsPath.GetRoundRect(new RectangleF(35, 14, 30, 12), 3));
                    g.FillPolygon(C(0x111111), new PointF(50, 3), new PointF(80, 11), new PointF(50, 19), new PointF(20, 11));
                    g.FillPolygon(C(0x3A3A3A), new PointF(50, 3), new PointF(80, 11), new PointF(50, 15), new PointF(20, 11));
                    var tassel = new Pen(Gold, 2f) { LineCap = PenLineCap.Round };
                    g.DrawLines(tassel, new PointF(50, 10), new PointF(73, 13), new PointF(73, 25));
                    g.FillEllipse(Gold, 70, 24, 6, 7);
                    break;
                case Hat.Flame:
                {
                    var outer = new GraphicsPath();
                    outer.MoveTo(50, -2);
                    outer.AddBezier(new PointF(50, -2), new PointF(70, 10), new PointF(70, 24), new PointF(60, 27));
                    outer.AddLine(new PointF(60, 27), new PointF(40, 27));
                    outer.AddBezier(new PointF(40, 27), new PointF(30, 24), new PointF(32, 12), new PointF(42, 6));
                    outer.AddBezier(new PointF(42, 6), new PointF(42, 12), new PointF(46, 14), new PointF(50, -2));
                    outer.CloseFigure();
                    g.FillPath(C(0xF97316), outer);
                    var inner = new GraphicsPath();
                    inner.MoveTo(51, 9);
                    inner.AddBezier(new PointF(51, 9), new PointF(62, 16), new PointF(60, 26), new PointF(50, 26));
                    inner.AddBezier(new PointF(50, 26), new PointF(41, 26), new PointF(42, 17), new PointF(51, 9));
                    inner.CloseFigure();
                    g.FillPath(C(0xFFD34D), inner);
                    break;
                }
                case Hat.Cowboy:
                {
                    var crown = new GraphicsPath();
                    crown.MoveTo(31, 22);
                    crown.AddBezier(new PointF(31, 22), new PointF(29, 8), new PointF(34, 2), new PointF(42, 4));
                    crown.AddBezier(new PointF(42, 4), new PointF(46, 6), new PointF(54, 6), new PointF(58, 4));
                    crown.AddBezier(new PointF(58, 4), new PointF(66, 2), new PointF(71, 8), new PointF(69, 22));
                    crown.CloseFigure();
                    g.FillPath(C(0x9B6A37), crown);
                    g.FillRectangle(C(0x5E3B19), 31, 16, 38, 5);
                    var brim = new GraphicsPath();
                    brim.MoveTo(10, 16);
                    brim.AddBezier(new PointF(10, 16), new PointF(20, 30), new PointF(80, 30), new PointF(90, 16));
                    brim.AddBezier(new PointF(90, 16), new PointF(80, 22), new PointF(20, 22), new PointF(10, 16));
                    brim.CloseFigure();
                    g.FillPath(C(0x7A4E24), brim);
                    break;
                }
                case Hat.Propeller:
                {
                    g.FillRectangle(C(0x555555), 49, 0, 2.4f, 9);
                    g.FillEllipse(C(0xD6453D), 34, -1, 16, 4.5f);
                    g.FillEllipse(C(0x3F6FA6), 50, -1, 16, 4.5f);
                    var left = new GraphicsPath();
                    left.AddArc(31, 7, 38, 34, 180, 90);
                    left.AddLine(new PointF(50, 7), new PointF(50, 24));
                    left.CloseFigure();
                    g.FillPath(C(0xD6453D), left);
                    var right = new GraphicsPath();
                    right.AddArc(31, 7, 38, 34, 270, 90);
                    right.AddLine(new PointF(69, 24), new PointF(50, 24));
                    right.CloseFigure();
                    g.FillPath(C(0x3F6FA6), right);
                    g.FillRectangle(C(0xFFD34D), 47, 7.5f, 6, 16.5f);
                    g.FillEllipse(C(0xFFD34D), 46.5f, 5, 7, 5);
                    break;
                }
                case Hat.Tree:
                    g.FillRectangle(C(0x8A5A2B), 47, 21, 6, 6);
                    g.FillPolygon(C(0x237A3C), new PointF(50, 9), new PointF(66, 23), new PointF(34, 23));
                    g.FillPolygon(C(0x2F9A4F), new PointF(50, 3), new PointF(62, 15), new PointF(38, 15));
                    g.FillPolygon(C(0x4CB26A), new PointF(50, -2), new PointF(58, 7), new PointF(42, 7));
                    g.FillEllipse(C(0xD6453D), 43, 17, 3.5f, 3.5f);
                    g.FillEllipse(Gold, 55, 11, 3.5f, 3.5f);
                    Sparkle(g, 50, -3, 3.6f);
                    break;
                case Hat.Wizard:
                {
                    var cone = new GraphicsPath();
                    cone.MoveTo(33, 22);
                    cone.AddBezier(new PointF(33, 22), new PointF(42, 12), new PointF(48, 0), new PointF(62, -3));
                    cone.AddBezier(new PointF(62, -3), new PointF(56, 4), new PointF(60, 14), new PointF(67, 22));
                    cone.CloseFigure();
                    g.FillPath(C(0x4C3FA8), cone);
                    g.FillEllipse(C(0x3A2F86), 22, 18, 56, 9);
                    Sparkle(g, 50, 13, 3.5f);
                    Sparkle(g, 57, 4, 2.5f);
                    break;
                }
                case Hat.Crown:
                    g.FillPolygon(GoldDark, new PointF(32, 26), new PointF(32, 11), new PointF(41, 18), new PointF(50, 5),
                        new PointF(59, 18), new PointF(68, 11), new PointF(68, 26));
                    g.FillPolygon(Gold, new PointF(32, 24), new PointF(32, 11), new PointF(41, 18), new PointF(50, 5),
                        new PointF(59, 18), new PointF(68, 11), new PointF(68, 24));
                    g.FillEllipse(C(0xD6453D), 46.5f, 16, 7, 7);
                    g.FillEllipse(C(0x3F6FA6), 35, 18, 5, 5);
                    g.FillEllipse(C(0x3F6FA6), 60, 18, 5, 5);
                    break;
            }
        }
    }
}
