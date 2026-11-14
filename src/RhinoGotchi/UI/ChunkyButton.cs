using System;
using Eto.Drawing;
using Eto.Forms;

namespace RhinoGotchi.UI
{
    internal enum ButtonKind
    {
        /// <summary>Green: the main action on a screen (Play, Check, Try it!).</summary>
        Primary,
        /// <summary>Blue: secondary actions (Next, Help, Show me).</summary>
        Secondary,
        /// <summary>White with a gray border: quiet actions (Back, Undo, Clear).</summary>
        Outline,
        /// <summary>Light blue: a selected option or tab.</summary>
        Selected,
        /// <summary>A tile you can't click: slots and feedback chips. Colors are set directly.</summary>
        Tile,
    }

    /// <summary>
    /// The RhinoGotchi button: a rounded, chunky face sitting on a darker base, with a gloss
    /// stripe and bold text. Drawn by hand so it looks the same on Windows and Mac.
    /// Works like Eto's Button for the parts we use: Text, Click and Enabled.
    /// </summary>
    internal sealed class ChunkyButton : Drawable
    {
        private const int Depth = 4;
        private const float Radius = 12f;

        private string _text;
        private ButtonKind _kind;
        private bool _hover;
        private bool _pressed;
        private Font _font = new Font(SystemFont.Bold, 10);
        private Color? _face, _shadow, _ink;

        public ChunkyButton(string text = "", ButtonKind kind = ButtonKind.Outline, int height = 36)
        {
            _text = text;
            _kind = kind;
            Height = height;
            UpdateWidth();

            MouseEnter += (s, e) => { _hover = true; Invalidate(); };
            MouseLeave += (s, e) => { _hover = false; _pressed = false; Invalidate(); };
            MouseDown += (s, e) =>
            {
                if (!Enabled || _kind == ButtonKind.Tile) return;
                _pressed = true;
                Invalidate();
                e.Handled = true;
            };
            MouseUp += (s, e) =>
            {
                bool fire = _pressed && Enabled;
                _pressed = false;
                Invalidate();
                if (fire)
                    Click?.Invoke(this, EventArgs.Empty);
            };
            EnabledChanged += (s, e) => Invalidate();
        }

        public event EventHandler<EventArgs>? Click;

        public string Text
        {
            get => _text;
            set { _text = value ?? ""; UpdateWidth(); Invalidate(); }
        }

        public ButtonKind Kind
        {
            get => _kind;
            set { _kind = value; Invalidate(); }
        }

        public Font Font
        {
            get => _font;
            set { _font = value; UpdateWidth(); Invalidate(); }
        }

        /// <summary>Custom colors, used by tiles (slot and feedback chips).</summary>
        public void SetColors(Color face, Color shadow, Color ink)
        {
            _face = face;
            _shadow = shadow;
            _ink = ink;
            Invalidate();
        }

        /// <summary>Wide enough for the text; layouts that stretch can make it wider.</summary>
        private void UpdateWidth()
        {
            float textWidth = _font.MeasureString(_text).Width;
            Width = (int)Math.Ceiling(textWidth) + 28;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var p = Palette.Current;
            g.AntiAlias = true;
            g.Clear(p.Background);

            Colors(p, out var face, out var shadow, out var ink);
            if (!Enabled && _kind != ButtonKind.Tile)
            {
                face = p.Disabled;
                shadow = p.Disabled;
                ink = p.DisabledText;
            }

            float w = Width, h = Height - Depth;
            if (w <= 0 || h <= 0)
                return;
            float lift = _pressed ? Depth : 0;

            // Base (the darker "shadow" the face sits on), then the face.
            g.FillPath(shadow, GraphicsPath.GetRoundRect(new RectangleF(0, Depth, w - 1, h), Radius));
            var top = new RectangleF(0, lift, w - 1, h);
            bool outline = _kind == ButtonKind.Outline || _kind == ButtonKind.Selected;
            if (outline)
            {
                g.FillPath(shadow, GraphicsPath.GetRoundRect(top, Radius));
                var inner = new RectangleF(top.X + 2, top.Y + 2, top.Width - 4, top.Height - 4);
                g.FillPath(_hover && Enabled ? Blend(face, p.TileHover) : face, GraphicsPath.GetRoundRect(inner, Radius - 2));
            }
            else
            {
                g.FillPath(_hover && Enabled && !_pressed ? Brighten(face, 0.06f) : face, GraphicsPath.GetRoundRect(top, Radius));
                // Gloss stripe.
                if (top.Width > 24)
                    g.FillPath(Color.FromArgb(255, 255, 255, 40), GraphicsPath.GetRoundRect(new RectangleF(top.X + 10, top.Y + 4, top.Width - 20, 5), 2.5f));
            }

            var size = _font.MeasureString(_text);
            g.DrawText(_font, ink, top.X + (top.Width - size.Width) / 2, top.Y + (top.Height - size.Height) / 2, _text);
        }

        private void Colors(Palette p, out Color face, out Color shadow, out Color ink)
        {
            if (_face.HasValue && _shadow.HasValue && _ink.HasValue)
            {
                face = _face.Value;
                shadow = _shadow.Value;
                ink = _ink.Value;
                return;
            }
            switch (_kind)
            {
                case ButtonKind.Primary:
                    face = p.Green; shadow = p.GreenShadow; ink = Eto.Drawing.Colors.White; break;
                case ButtonKind.Secondary:
                    face = p.Blue; shadow = p.BlueShadow; ink = Eto.Drawing.Colors.White; break;
                case ButtonKind.Selected:
                    face = p.SelectedFace; shadow = p.SelectedBorder; ink = p.SelectedText; break;
                case ButtonKind.Tile:
                    face = p.TileFace; shadow = p.Border; ink = p.Text; break;
                default:
                    face = p.Background; shadow = p.Border; ink = p.SubtleText; break;
            }
        }

        private static Color Brighten(Color c, float amount) =>
            new Color(Math.Min(1, c.R + amount), Math.Min(1, c.G + amount), Math.Min(1, c.B + amount), c.A);

        private static Color Blend(Color a, Color b) =>
            new Color((a.R + b.R) / 2, (a.G + b.G) / 2, (a.B + b.B) / 2, 1);
    }
}
