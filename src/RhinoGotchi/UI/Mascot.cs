using System;
using Eto.Drawing;

namespace RhinoGotchi.UI
{
    internal enum Mood
    {
        Neutral,
        Happy,
        Sad,
    }

    /// <summary>Gotchi, the little rhino who asks the questions. Drawn in a 100x100 box.</summary>
    internal static class Mascot
    {
        private static readonly Color Skin = Color.FromArgb(0xA8, 0xB8, 0xCC);
        private static readonly Color SkinDark = Color.FromArgb(0x7E, 0x90, 0xA8);
        private static readonly Color Belly = Color.FromArgb(0xD6, 0xE0, 0xEC);
        private static readonly Color Horn = Color.FromArgb(0xFF, 0xF4, 0xD6);
        private static readonly Color HornEdge = Color.FromArgb(0xE8, 0xC9, 0x7A);
        private static readonly Color Eye = Color.FromArgb(0x2B, 0x2B, 0x33);
        private static readonly Color Blush = Color.FromArgb(0xFF, 0x9E, 0xB0);

        public static void Draw(Graphics g, RectangleF rect, Mood mood, float bob = 0)
        {
            float size = Math.Min(rect.Width, rect.Height);
            g.SaveTransform();
            g.TranslateTransform(rect.X + (rect.Width - size) / 2, rect.Y + (rect.Height - size) / 2 + bob);
            g.ScaleTransform(size / 100f);
            g.AntiAlias = true;

            // Shadow
            g.FillEllipse(Color.FromArgb(0, 0, 0, 28), 18, 88, 64, 10);

            // Ears
            g.FillEllipse(SkinDark, 14, 14, 20, 26);
            g.FillEllipse(SkinDark, 66, 14, 20, 26);
            g.FillEllipse(Blush, 19, 20, 10, 15);
            g.FillEllipse(Blush, 71, 20, 10, 15);

            // Head + snout
            g.FillEllipse(Skin, 12, 22, 76, 70);
            g.FillEllipse(Belly, 26, 56, 48, 34);

            // Horn on the snout
            var horn = new GraphicsPath();
            horn.MoveTo(40, 62);
            horn.AddBezier(new PointF(40, 62), new PointF(44, 42), new PointF(52, 34), new PointF(60, 30));
            horn.AddBezier(new PointF(60, 30), new PointF(58, 42), new PointF(60, 54), new PointF(60, 62));
            horn.CloseFigure();
            g.FillPath(Horn, horn);
            g.DrawPath(new Pen(HornEdge, 2f) { LineJoin = PenLineJoin.Round }, horn);

            // Eyes
            if (mood == Mood.Happy)
            {
                var pen = new Pen(Eye, 4f) { LineCap = PenLineCap.Round };
                g.DrawArc(pen, 24, 40, 14, 12, 200, 140);
                g.DrawArc(pen, 62, 40, 14, 12, 200, 140);
            }
            else
            {
                g.FillEllipse(Colors.White, 23, 36, 17, 19);
                g.FillEllipse(Colors.White, 60, 36, 17, 19);
                float py = mood == Mood.Sad ? 45 : 42;
                g.FillEllipse(Eye, 28, py, 10, 11);
                g.FillEllipse(Eye, 64, py, 10, 11);
                g.FillEllipse(Colors.White, 31, py + 2, 4, 4);
                g.FillEllipse(Colors.White, 67, py + 2, 4, 4);
                if (mood == Mood.Sad)
                {
                    var brow = new Pen(SkinDark, 3f) { LineCap = PenLineCap.Round };
                    g.DrawLine(brow, 23, 34, 37, 31);
                    g.DrawLine(brow, 77, 34, 63, 31);
                }
            }

            // Cheeks
            g.FillEllipse(Color.FromArgb(Blush.Rb, Blush.Gb, Blush.Bb, 140), 18, 54, 12, 8);
            g.FillEllipse(Color.FromArgb(Blush.Rb, Blush.Gb, Blush.Bb, 140), 70, 54, 12, 8);

            // Nostrils + mouth
            g.FillEllipse(SkinDark, 38, 70, 5, 4);
            g.FillEllipse(SkinDark, 57, 70, 5, 4);
            var mouth = new Pen(Eye, 2.6f) { LineCap = PenLineCap.Round };
            if (mood == Mood.Sad)
                g.DrawArc(mouth, 42, 79, 16, 8, 200, 140);
            else
                g.DrawArc(mouth, 42, 74, 16, 9, 20, 140);

            g.RestoreTransform();
        }
    }
}
