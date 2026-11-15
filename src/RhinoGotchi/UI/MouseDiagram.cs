using System;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using RhinoGotchi.Drone;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// A friendly picture of what to press: key caps (like "Shift +") and a mouse with the button or
    /// wheel to use lit up in orange. Centered, and it scales down in a narrow panel.
    /// </summary>
    internal sealed class MouseDiagram : Drawable
    {
        private readonly MouseParts _parts;
        private readonly string[] _keys;

        public MouseDiagram(MouseParts parts, string[] keys)
        {
            _parts = parts;
            _keys = keys;
            Height = 112;
            SizeChanged += (s, e) => Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var p = Palette.Current;
            g.AntiAlias = true;

            var keyFont = Ui.Bold(11);
            var plusFont = Ui.Bold(14);
            const float keyHeight = 30, gap = 8, mouseW = 64, mouseH = 96;
            bool hasMouse = _parts != MouseParts.None;

            // Measure everything to center it, and shrink to fit if the panel is narrow.
            var keyWidths = _keys.Select(k => Math.Max(keyHeight, keyFont.MeasureString(k).Width + 20)).ToList();
            float plusW = plusFont.MeasureString("+").Width;
            float total = keyWidths.Sum() + Math.Max(0, _keys.Length - 1) * (plusW + 2 * gap)
                          + (hasMouse ? mouseW + (_keys.Length > 0 ? plusW + 2 * gap : 0) : 0);
            float scale = Math.Min(1f, (Width - 8) / Math.Max(1, total));
            g.SaveTransform();
            g.TranslateTransform((Width - total * scale) / 2, (Height - mouseH * scale) / 2);
            g.ScaleTransform(scale);

            float x = 0, keyY = (mouseH - keyHeight) / 2;
            for (int i = 0; i < _keys.Length; i++)
            {
                if (i > 0)
                    x = Plus(g, plusFont, p.SubtleText, x, gap, mouseH);
                float w = keyWidths[i];
                Ui.Chunky(g, new RectangleF(x, keyY, w, keyHeight + 3), 7, p.AccentSoft, p.Accent, lip: 3, stroke: 1.5f);
                var size = keyFont.MeasureString(_keys[i]);
                g.DrawText(keyFont, p.AccentText, x + (w - size.Width) / 2, keyY + (keyHeight - size.Height) / 2, _keys[i]);
                x += w;
            }

            if (hasMouse)
            {
                if (_keys.Length > 0)
                    x = Plus(g, plusFont, p.SubtleText, x, gap, mouseH);
                DrawMouse(g, p, x, 0, mouseW, mouseH);
            }
            g.RestoreTransform();
        }

        private static float Plus(Graphics g, Font font, Color color, float x, float gap, float height)
        {
            var size = font.MeasureString("+");
            g.DrawText(font, color, x + gap, (height - size.Height) / 2, "+");
            return x + gap + size.Width + gap;
        }

        private void DrawMouse(Graphics g, Palette p, float x, float y, float w, float h)
        {
            var lit = p.Accent;
            var body = GraphicsPath.GetRoundRect(new RectangleF(x, y, w, h), w / 2);
            g.FillPath(p.TileFace, body);

            // Left and right buttons: the top part of the body, split down the middle.
            float split = y + h * 0.42f;
            if (_parts.HasFlag(MouseParts.Left))
                FillButton(g, x, y, w / 2, split - y, left: true, lit);
            if (_parts.HasFlag(MouseParts.Right))
                FillButton(g, x + w / 2, y, w / 2, split - y, left: false, lit);

            var pen = new Pen(p.Text, 2.2f) { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            g.DrawPath(pen, body);
            g.DrawLine(pen, x + 4, split, x + w - 4, split);
            g.DrawLine(pen, x + w / 2, y + 2, x + w / 2, split);

            // Wheel.
            var wheel = GraphicsPath.GetRoundRect(new RectangleF(x + w / 2 - 6, y + h * 0.13f, 12, h * 0.2f), 6);
            g.FillPath(_parts.HasFlag(MouseParts.Wheel) ? lit : p.TileFace, wheel);
            g.DrawPath(new Pen(p.Text, 2f), wheel);
            if (_parts.HasFlag(MouseParts.Wheel))
            {
                // Little arrows: roll it.
                var arrow = new Pen(lit, 2.2f) { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
                float ax = x + w + 8, top = y + h * 0.1f, bottom = y + h * 0.38f;
                g.DrawLine(arrow, ax, top, ax, bottom);
                g.DrawLines(arrow, new PointF(ax - 4, top + 5), new PointF(ax, top), new PointF(ax + 4, top + 5));
                g.DrawLines(arrow, new PointF(ax - 4, bottom - 5), new PointF(ax, bottom), new PointF(ax + 4, bottom - 5));
            }
        }

        private static void FillButton(Graphics g, float x, float y, float w, float h, bool left, Color color)
        {
            // The button is the top quarter of the body on its side: square bottom, rounded outer top.
            var path = new GraphicsPath();
            float r = w;
            if (left)
            {
                path.MoveTo(x + w, y + h);
                path.LineTo(x, y + h);
                path.LineTo(x, y + r);
                path.AddArc(x, y, 2 * r, 2 * r, 180, 90);
                path.LineTo(x + w, y);
            }
            else
            {
                path.MoveTo(x, y + h);
                path.LineTo(x + w, y + h);
                path.LineTo(x + w, y + r);
                path.AddArc(x - w, y, 2 * r, 2 * r, 0, -90);
                path.LineTo(x, y);
            }
            path.CloseFigure();
            g.FillPath(color, path);
        }
    }
}
