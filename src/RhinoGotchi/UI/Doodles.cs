using System;
using Eto.Drawing;

namespace RhinoGotchi.UI
{
    internal enum MascotMood { Happy, Excited, Thinking, Sleepy }

    internal enum Glyph { Blocks, Eye, Curve, Cube, Arrows, Chain, Bulb, Flame, Star, Gamepad, Book, Chart, Trophy }

    /// <summary>
    /// Little hand-drawn pictures for the panel: the mascot (drawn by Mascot) and simple white glyphs for badges. Everything is drawn in a 100x100 box.
    /// </summary>
    internal static class Doodles
    {
        private static Color C(int rgb, int a = 255) => Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF, a);


        private static void Begin(Graphics g, RectangleF rect)
        {
            float size = Math.Min(rect.Width, rect.Height);
            g.SaveTransform();
            g.TranslateTransform(rect.X + (rect.Width - size) / 2, rect.Y + (rect.Height - size) / 2);
            g.ScaleTransform(size / 100f);
            g.AntiAlias = true;
        }

        /// <summary>The shared Gotchi mascot (see <see cref="UI.Mascot"/>), wearing the player's hat.</summary>
        public static void Mascot(Graphics g, RectangleF rect, MascotMood mood, float breathe = 0) =>
            UI.Mascot.Draw(g, rect, mood switch
            {
                MascotMood.Excited => Mood.Celebrate,
                MascotMood.Thinking => Mood.Thinking,
                MascotMood.Sleepy => Mood.Sleepy,
                _ => Mood.Happy,
            }, breathe: breathe);

        /// <summary>A round colored badge with a white glyph inside.</summary>
        public static void Badge(Graphics g, RectangleF rect, Glyph glyph, Color color, Color shadow)
        {
            Begin(g, rect);
            g.FillEllipse(shadow, 4, 8, 92, 90);
            g.FillEllipse(color, 4, 2, 92, 90);
            g.FillEllipse(C(0xFFFFFF, 45), 20, 10, 50, 22);
            DrawGlyph(g, glyph, Colors.White, 1f);
            g.RestoreTransform();
        }

        /// <summary>A glyph alone (no badge), e.g. inside a tab or pill. Drawn in the 100x100 box.</summary>
        public static void Icon(Graphics g, RectangleF rect, Glyph glyph, Color color)
        {
            Begin(g, rect);
            DrawGlyph(g, glyph, color, 1.25f);
            g.RestoreTransform();
        }

        private static void DrawGlyph(Graphics g, Glyph glyph, Color ink, float weight)
        {
            var pen = new Pen(ink, 7 * weight) { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            switch (glyph)
            {
                case Glyph.Blocks: // three stacked blocks, the middle one offset
                    g.FillPath(ink, GraphicsPath.GetRoundRect(new RectangleF(26, 60, 48, 16), 4));
                    g.FillPath(ink, GraphicsPath.GetRoundRect(new RectangleF(34, 41, 40, 16), 4));
                    g.FillPath(ink, GraphicsPath.GetRoundRect(new RectangleF(26, 22, 34, 16), 4));
                    break;
                case Glyph.Eye:
                    var eye = new GraphicsPath();
                    eye.MoveTo(18, 48);
                    eye.AddBezier(new PointF(18, 48), new PointF(34, 24), new PointF(66, 24), new PointF(82, 48));
                    eye.AddBezier(new PointF(82, 48), new PointF(66, 72), new PointF(34, 72), new PointF(18, 48));
                    g.DrawPath(pen, eye);
                    g.FillEllipse(ink, 38, 36, 24, 24);
                    break;
                case Glyph.Curve:
                    var s = new GraphicsPath();
                    s.MoveTo(20, 66);
                    s.AddBezier(new PointF(20, 66), new PointF(30, 16), new PointF(54, 80), new PointF(80, 28));
                    g.DrawPath(pen, s);
                    g.FillEllipse(ink, 13, 59, 14, 14);
                    g.FillEllipse(ink, 73, 21, 14, 14);
                    break;
                case Glyph.Cube:
                    var top = new GraphicsPath();
                    top.AddLines(new PointF(50, 18), new PointF(78, 32), new PointF(50, 46), new PointF(22, 32));
                    top.CloseFigure();
                    g.FillPath(ink, top);
                    var cube = new GraphicsPath();
                    cube.AddLines(new PointF(22, 32), new PointF(22, 64), new PointF(50, 78), new PointF(78, 64), new PointF(78, 32));
                    g.DrawPath(pen, cube);
                    g.DrawLine(pen, 50, 46, 50, 78);
                    break;
                case Glyph.Arrows: // move: four-way arrows
                    g.DrawLine(pen, 50, 20, 50, 78);
                    g.DrawLine(pen, 21, 49, 79, 49);
                    g.DrawLines(pen, new PointF(40, 29), new PointF(50, 19), new PointF(60, 29));
                    g.DrawLines(pen, new PointF(40, 69), new PointF(50, 79), new PointF(60, 69));
                    g.DrawLines(pen, new PointF(30, 39), new PointF(20, 49), new PointF(30, 59));
                    g.DrawLines(pen, new PointF(70, 39), new PointF(80, 49), new PointF(70, 59));
                    break;
                case Glyph.Chain:
                    g.DrawPath(pen, GraphicsPath.GetRoundRect(new RectangleF(16, 38, 40, 22), 11));
                    g.DrawPath(pen, GraphicsPath.GetRoundRect(new RectangleF(44, 38, 40, 22), 11));
                    break;
                case Glyph.Bulb:
                    g.FillEllipse(ink, 30, 18, 40, 40);
                    g.FillPath(ink, GraphicsPath.GetRoundRect(new RectangleF(38, 50, 24, 16), 3));
                    g.FillPath(ink, GraphicsPath.GetRoundRect(new RectangleF(40, 70, 20, 7), 3));
                    break;
                case Glyph.Flame:
                    var flame = new GraphicsPath();
                    flame.MoveTo(50, 16);
                    flame.AddBezier(new PointF(50, 16), new PointF(78, 42), new PointF(76, 80), new PointF(50, 80));
                    flame.AddBezier(new PointF(50, 80), new PointF(24, 80), new PointF(24, 52), new PointF(40, 40));
                    flame.AddBezier(new PointF(40, 40), new PointF(40, 52), new PointF(46, 54), new PointF(50, 16));
                    g.FillPath(ink, flame);
                    break;
                case Glyph.Star:
                    var star = new GraphicsPath();
                    for (int i = 0; i < 10; i++)
                    {
                        double a = -Math.PI / 2 + i * Math.PI / 5;
                        float r = i % 2 == 0 ? 34 : 15;
                        var p = new PointF(50 + r * (float)Math.Cos(a), 50 + r * (float)Math.Sin(a));
                        if (i == 0) star.MoveTo(p); else star.LineTo(p);
                    }
                    star.CloseFigure();
                    g.FillPath(ink, star);
                    break;
                case Glyph.Gamepad:
                    g.FillPath(ink, GraphicsPath.GetRoundRect(new RectangleF(12, 32, 76, 40), 20));
                    var hole = Colors.White == ink ? Color.FromArgb(0, 0, 0, 70) : Color.FromArgb(255, 255, 255, 230);
                    g.FillRectangle(hole, 26, 48, 18, 6);
                    g.FillRectangle(hole, 32, 42, 6, 18);
                    g.FillEllipse(hole, 60, 42, 8, 8);
                    g.FillEllipse(hole, 70, 50, 8, 8);
                    break;
                case Glyph.Book:
                    var left = new GraphicsPath();
                    left.AddLines(new PointF(50, 30), new PointF(16, 24), new PointF(16, 72), new PointF(50, 78));
                    left.CloseFigure();
                    var right = new GraphicsPath();
                    right.AddLines(new PointF(50, 30), new PointF(84, 24), new PointF(84, 72), new PointF(50, 78));
                    right.CloseFigure();
                    g.FillPath(ink, left);
                    g.FillPath(ink, right);
                    g.DrawLine(new Pen(Color.FromArgb(0, 0, 0, 60), 3), 50, 30, 50, 78);
                    break;
                case Glyph.Chart:
                    g.FillPath(ink, GraphicsPath.GetRoundRect(new RectangleF(20, 52, 14, 26), 3));
                    g.FillPath(ink, GraphicsPath.GetRoundRect(new RectangleF(43, 34, 14, 44), 3));
                    g.FillPath(ink, GraphicsPath.GetRoundRect(new RectangleF(66, 20, 14, 58), 3));
                    break;
                case Glyph.Trophy:
                    var cup = new GraphicsPath();
                    cup.MoveTo(30, 20);
                    cup.LineTo(70, 20);
                    cup.AddBezier(new PointF(70, 20), new PointF(70, 48), new PointF(60, 56), new PointF(50, 56));
                    cup.AddBezier(new PointF(50, 56), new PointF(40, 56), new PointF(30, 48), new PointF(30, 20));
                    cup.CloseFigure();
                    g.FillPath(ink, cup);
                    var handles = new GraphicsPath();
                    handles.AddBezier(new PointF(31, 26), new PointF(14, 26), new PointF(14, 44), new PointF(34, 44));
                    handles.StartFigure();
                    handles.AddBezier(new PointF(69, 26), new PointF(86, 26), new PointF(86, 44), new PointF(66, 44));
                    g.DrawPath(new Pen(ink, 5 * weight) { LineCap = PenLineCap.Round }, handles);
                    g.FillRectangle(ink, 46, 54, 8, 14);
                    g.FillPath(ink, GraphicsPath.GetRoundRect(new RectangleF(32, 66, 36, 11), 4));
                    break;
            }
        }
    }
}
