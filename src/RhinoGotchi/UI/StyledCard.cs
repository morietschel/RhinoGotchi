using Eto.Drawing;
using Eto.Forms;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// A rounded card with a 2px border and a slightly thicker bottom edge, holding any content.
    /// Optional accent colors make it a verdict banner (green = right, red = wrong).
    /// Set <see cref="Hoverable"/> for list rows that highlight under the mouse.
    /// </summary>
    internal sealed class StyledCard : Drawable
    {
        private const float Radius = 12f;
        private const int Depth = 3;
        private bool _hover;

        public StyledCard(Control content, Color? face = null, Color? border = null, int padding = 10)
        {
            Face = face;
            BorderColor = border;
            Padding = new Padding(padding, padding, padding, padding + Depth);
            Content = content;
            MouseEnter += (s, e) => { if (Hoverable) { _hover = true; Invalidate(); } };
            MouseLeave += (s, e) => { if (_hover) { _hover = false; Invalidate(); } };
        }

        public Color? Face { get; set; }
        public Color? BorderColor { get; set; }
        public bool Hoverable { get; set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var p = Palette.Current;
            g.AntiAlias = true;
            g.Clear(p.Background);

            float w = Width - 1, h = Height - 1 - Depth;
            if (w <= 4 || h <= 4)
                return;
            var border = BorderColor ?? (_hover ? p.SelectedBorder : p.Border);
            var face = Face ?? (_hover ? p.TileHover : p.TileFace);

            g.FillPath(border, GraphicsPath.GetRoundRect(new RectangleF(0, Depth, w, h), Radius));
            g.FillPath(border, GraphicsPath.GetRoundRect(new RectangleF(0, 0, w, h), Radius));
            g.FillPath(face, GraphicsPath.GetRoundRect(new RectangleF(2, 2, w - 4, h - 4), Radius - 2));
        }
    }
}
