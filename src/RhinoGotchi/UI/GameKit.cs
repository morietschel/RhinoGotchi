using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// Wrapped text, drawn by hand. Eto's own Label loses its font and alignment when it wraps on the Mac
    /// and reports the wrong height, so every paragraph in the panel is one of these instead.
    /// It fills the width it is given and sets its own height.
    /// </summary>
    internal sealed class Words : Drawable
    {
        private string _text;
        private Font _font;
        private Color _color;
        private bool _centered;

        public Words(string text, Font font, Color color)
        {
            _text = text ?? "";
            _font = font;
            _color = color;
            Height = HeightFor(280);
            Visible = _text.Length > 0;
            SizeChanged += (s, e) => { Relayout(); Invalidate(); };
        }

        public string Text
        {
            get => _text;
            set
            {
                _text = value ?? "";
                Visible = _text.Length > 0; // an empty line should not leave a gap
                Relayout();
                Invalidate();
            }
        }

        public Color TextColor
        {
            get => _color;
            set { _color = value; Invalidate(); }
        }

        public Font Font
        {
            get => _font;
            set { _font = value; Relayout(); Invalidate(); }
        }

        public bool Centered
        {
            get => _centered;
            set { _centered = value; Invalidate(); }
        }

        private List<string> Lines(float width) =>
            _text.Split('\n').SelectMany(part => Ui.Wrap(_font, part, Math.Max(20, width))).ToList();

        // Font.LineHeight leaves out the part of g, j, p and y that hangs below the line, so measure a full
        // line instead and keep a little air: otherwise the last line is clipped at the bottom.
        private float LineStep => Math.Max(_font.LineHeight, _font.MeasureString("Agjpy").Height);

        private int HeightFor(float width) =>
            (int)Math.Ceiling(Math.Max(1, Lines(width).Count) * LineStep) + 4;

        private void Relayout()
        {
            if (Width <= 0)
                return;
            int h = HeightFor(Width);
            if (h != Height)
                Application.Instance.AsyncInvoke(() => Height = h);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.AntiAlias = true;
            float y = 1;
            foreach (string line in Lines(Width))
            {
                float x = _centered ? (Width - _font.MeasureString(line).Width) / 2 : 0;
                g.DrawText(_font, _color, x, y, line);
                y += LineStep;
            }
        }
    }

    /// <summary>The top of a page: a round back button and the page name on one line.</summary>
    internal sealed class PageHeader : Drawable
    {
        private const float Button = 32;
        private readonly string _title;
        private bool _hover;

        public PageHeader(string title, Action back)
        {
            _title = title;
            Height = 38;
            Cursor = Cursors.Pointer;
            ToolTip = "Back";
            MouseEnter += (s, e) => { _hover = true; Invalidate(); };
            MouseLeave += (s, e) => { _hover = false; Invalidate(); };
            MouseUp += (s, e) => back();
            SizeChanged += (s, e) => Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var p = Palette.Current;
            g.AntiAlias = true;

            Ui.Chunky(g, new RectangleF(0, 1, Button, Button + 3), Button / 2, _hover ? p.TileHover : p.TileFace, p.Border, lip: 3, stroke: 1.5f);
            var pen = new Pen(p.Text, 2.2f) { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            float cx = Button / 2, cy = 1 + Button / 2;
            g.DrawLines(pen, new PointF(cx + 3, cy - 6), new PointF(cx - 3, cy), new PointF(cx + 3, cy + 6));

            var font = Ui.Bold(15);
            string title = Ui.Ellipsize(font, _title, Width - Button - 12);
            g.DrawText(font, p.Text, Button + 10, cy - font.LineHeight / 2, title);
        }
    }

    internal enum Pip { Todo, Now, Good, Bad }

    /// <summary>
    /// The status card at the top of a game: a small caption, a figure on the right, one pip per round
    /// or try, and optionally a countdown that turns red for the last ten seconds.
    /// </summary>
    internal sealed class GameHud : Drawable
    {
        private string _caption = "", _figure = "";
        private Pip[] _pips = new Pip[0];
        private bool _clock;
        private double _left, _seconds = 1;

        public GameHud(bool clock = false)
        {
            _clock = clock;
            Height = clock ? 84 : 58;
            SizeChanged += (s, e) => Invalidate();
        }

        public bool Clock
        {
            get => _clock;
            set { _clock = value; Height = value ? 84 : 58; Invalidate(); }
        }

        public void Set(string caption, string figure, IEnumerable<Pip> pips)
        {
            _caption = caption;
            _figure = figure;
            _pips = pips.ToArray();
            Invalidate();
        }

        public void SetTime(double left, double seconds)
        {
            _left = left;
            _seconds = Math.Max(1, seconds);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var p = Palette.Current;
            g.AntiAlias = true;
            float w = Width - 1;
            if (w < 80)
                return;
            Ui.Chunky(g, new RectangleF(0, 0, w, Height - 1), 12, p.TileFace, p.Border, lip: 3);

            const float pad = 12;
            float inner = w - 2 * pad;

            var figureFont = Ui.Bold(12);
            var figureSize = figureFont.MeasureString(_figure);
            g.DrawText(figureFont, p.Text, w - pad - figureSize.Width, 9, _figure);
            Ui.Eyebrow(g, _caption, pad, 12, p.SubtleText);

            const float gap = 4, pipHeight = 6;
            int count = Math.Max(1, _pips.Length);
            float pip = (inner - gap * (count - 1)) / count;
            for (int i = 0; i < _pips.Length; i++)
            {
                Color color = _pips[i] == Pip.Good ? p.Green : _pips[i] == Pip.Bad ? p.Red : _pips[i] == Pip.Now ? p.Ink : p.Track;
                g.FillPath(color, GraphicsPath.GetRoundRect(new RectangleF(pad + i * (pip + gap), 34, pip, pipHeight), pipHeight / 2));
            }
            if (!_clock)
                return;

            bool hurry = _left <= 10;
            string seconds = Math.Ceiling(_left).ToString("0") + "s";
            var clockFont = Ui.Bold(11);
            float barWidth = inner - clockFont.MeasureString("30s").Width - 8, barHeight = 10, y = 54;
            g.FillPath(p.Track, GraphicsPath.GetRoundRect(new RectangleF(pad, y, barWidth, barHeight), barHeight / 2));
            float fill = (float)(barWidth * Math.Max(0, Math.Min(1, _left / _seconds)));
            if (fill >= barHeight)
                g.FillPath(hurry ? p.Red : p.Ink, GraphicsPath.GetRoundRect(new RectangleF(pad, y, fill, barHeight), barHeight / 2));
            var size = clockFont.MeasureString(seconds);
            g.DrawText(clockFont, hurry ? p.Red : p.Text, w - pad - size.Width, y + (barHeight - size.Height) / 2, seconds);
        }
    }

    internal enum TileState { Normal, Right, Wrong, Faded }

    /// <summary>A big answer tile: the command's toolbar icon with its name underneath.</summary>
    internal sealed class OptionTile : Drawable
    {
        private readonly string _name;
        private readonly Image? _icon;
        private TileState _state;
        private bool _hover, _pressed;

        public OptionTile(string name, Image? icon)
        {
            _name = name;
            _icon = icon;
            Height = 86;
            Cursor = Cursors.Pointer;
            MouseEnter += (s, e) => { _hover = true; Invalidate(); };
            MouseLeave += (s, e) => { _hover = false; _pressed = false; Invalidate(); };
            MouseDown += (s, e) => { if (Clickable) { _pressed = true; Invalidate(); } };
            MouseUp += (s, e) =>
            {
                bool fire = _pressed && Clickable;
                _pressed = false;
                Invalidate();
                if (fire)
                    Click?.Invoke(this, EventArgs.Empty);
            };
            SizeChanged += (s, e) => Invalidate();
        }

        public event EventHandler<EventArgs>? Click;

        public string CommandName => _name;

        /// <summary>Once any state is set the tile stops reacting to the mouse.</summary>
        public bool Locked { get; set; }

        public TileState State
        {
            get => _state;
            set { _state = value; Invalidate(); }
        }

        private bool Clickable => !Locked && _state == TileState.Normal;

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var p = Palette.Current;
            g.AntiAlias = true;
            float w = Width - 1, h = Height - 1;
            if (w < 30)
                return;

            Color face = p.TileFace, border = p.Border, ink = p.Text;
            switch (_state)
            {
                case TileState.Right: face = p.GreenBanner; border = p.Green; ink = p.GreenText; break;
                case TileState.Wrong: face = p.RedBanner; border = p.Red; ink = p.RedText; break;
                case TileState.Faded: ink = p.DisabledText; break;
                default:
                    if (_hover && Clickable) { face = p.TileHover; border = p.Ink; }
                    break;
            }
            Ui.Chunky(g, new RectangleF(0, 0, w, h), 14, face, border, lip: 4, lift: _pressed ? 3 : 0);

            float top = _pressed ? 3 : 0;
            const float iconSize = 34;
            if (_icon != null)
            {
                g.ImageInterpolation = ImageInterpolation.High;
                g.DrawImage(_icon, (w - iconSize) / 2, top + 11, iconSize, iconSize);
            }
            var font = Ui.Bold(10);
            string name = Ui.Ellipsize(font, _name, w - 16);
            var size = font.MeasureString(name);
            float textY = _icon != null ? top + 11 + iconSize + 6 : top + (h - 4 - size.Height) / 2;
            g.DrawText(font, ink, (w - size.Width) / 2, textY, name);

            if (_state == TileState.Right)
                Ui.Check(g, new RectangleF(w - 26, top + 8, 16, 16), p.Green);
        }
    }

    /// <summary>"● start   ● result": what the viewport colors mean, as dots instead of a sentence.</summary>
    internal sealed class Legend : Drawable
    {
        private readonly (string Text, Color Color)[] _items;

        public Legend(params (string Text, Color Color)[] items)
        {
            _items = items;
            Height = 20;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.AntiAlias = true;
            var font = Ui.Regular(9.5f);
            float x = 0;
            foreach (var (text, color) in _items)
            {
                g.FillEllipse(color, x, 5, 10, 10);
                g.DrawText(font, Palette.Current.SubtleText, x + 15, (Height - font.LineHeight) / 2, text);
                x += 15 + font.MeasureString(text).Width + 16;
            }
        }
    }
}
