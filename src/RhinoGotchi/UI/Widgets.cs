using System;
using System.Collections.Generic;
using Eto.Drawing;
using Eto.Forms;

namespace RhinoGotchi.UI
{
    /// <summary>The mascot with a speech bubble. The bubble wraps its text to the panel width.</summary>
    internal sealed class MascotHeader : Drawable
    {
        private const float MascotSize = 78;
        private string _says;
        private MascotMood _mood;
        private readonly Font _font = new Font(SystemFont.Bold, 10);

        public MascotHeader(string says, MascotMood mood = MascotMood.Happy)
        {
            _says = says;
            _mood = mood;
            Height = 92;
            SizeChanged += (s, e) => Invalidate();
        }

        public void Say(string text, MascotMood mood)
        {
            _says = text;
            _mood = mood;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var p = Palette.Current;
            g.AntiAlias = true;

            Doodles.Mascot(g, new RectangleF(0, 6, MascotSize, MascotSize), _mood);

            float x = MascotSize + 10, w = Width - x - 2;
            if (w < 60)
                return;
            var lines = Wrap(_says, w - 24);
            float lineH = _font.LineHeight;
            float h = Math.Max(40, lines.Count * lineH + 20);
            float y = Math.Max(4, (Height - h) / 2 - 2);

            // Bubble with a little tail pointing at the mascot.
            var tail = new GraphicsPath();
            tail.AddLines(new PointF(x + 2, y + h / 2 - 6), new PointF(x - 9, y + h / 2 + 4), new PointF(x + 6, y + h / 2 + 6));
            tail.CloseFigure();
            g.FillPath(p.Border, GraphicsPath.GetRoundRect(new RectangleF(x, y + 3, w, h), 14));
            g.FillPath(p.Border, GraphicsPath.GetRoundRect(new RectangleF(x, y, w, h), 14));
            g.FillPath(p.Border, tail);
            g.FillPath(p.TileFace, GraphicsPath.GetRoundRect(new RectangleF(x + 2, y + 2, w - 4, h - 4), 12));
            g.TranslateTransform(1.5f, 0);
            g.FillPath(p.TileFace, tail);
            g.TranslateTransform(-1.5f, 0);

            float ty = y + (h - lines.Count * lineH) / 2;
            foreach (var line in lines)
            {
                g.DrawText(_font, p.Text, x + 12, ty, line);
                ty += lineH;
            }
        }

        private List<string> Wrap(string text, float maxWidth)
        {
            var lines = new List<string>();
            string line = "";
            foreach (var word in text.Split(' '))
            {
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && _font.MeasureString(candidate).Width > maxWidth)
                {
                    lines.Add(line);
                    line = word;
                }
                else
                    line = candidate;
            }
            if (line.Length > 0)
                lines.Add(line);
            return lines;
        }
    }

    /// <summary>A rounded segmented toggle: one pill per tab, each with a glyph and a label.</summary>
    internal sealed class TabBar : Drawable
    {
        private readonly (string Text, Glyph Glyph, Color Color)[] _tabs;
        private readonly Font _font = new Font(SystemFont.Bold, 11);
        private int _selected;
        private int _hover = -1;

        public TabBar(int selected, params (string Text, Glyph Glyph, Color Color)[] tabs)
        {
            _tabs = tabs;
            _selected = selected;
            Height = 50;
            MouseMove += (s, e) => { int h = IndexAt(e.Location.X); if (h != _hover) { _hover = h; Invalidate(); } };
            MouseLeave += (s, e) => { _hover = -1; Invalidate(); };
            MouseDown += (s, e) =>
            {
                int i = IndexAt(e.Location.X);
                if (i >= 0 && i != _selected)
                {
                    _selected = i;
                    Invalidate();
                    SelectedChanged?.Invoke(this, EventArgs.Empty);
                }
            };
            SizeChanged += (s, e) => Invalidate();
        }

        public event EventHandler<EventArgs>? SelectedChanged;

        public int Selected => _selected;

        private int IndexAt(float x) => Width <= 0 ? -1 : Math.Max(0, Math.Min(_tabs.Length - 1, (int)(x / (Width / (float)_tabs.Length))));

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var p = Palette.Current;
            g.AntiAlias = true;

            float w = Width - 1, h = Height - 5;
            g.FillPath(p.Track, GraphicsPath.GetRoundRect(new RectangleF(0, 0, w, h), h / 2));
            float seg = w / _tabs.Length;
            for (int i = 0; i < _tabs.Length; i++)
            {
                var (text, glyph, color) = _tabs[i];
                var rect = new RectangleF(i * seg + 4, 4, seg - 8, h - 8);
                bool on = i == _selected;
                if (on)
                {
                    g.FillPath(Theme.Darken(color, 0.15f), GraphicsPath.GetRoundRect(new RectangleF(rect.X, rect.Y + 3, rect.Width, rect.Height), rect.Height / 2));
                    g.FillPath(color, GraphicsPath.GetRoundRect(rect, rect.Height / 2));
                    g.FillPath(Color.FromArgb(255, 255, 255, 45), GraphicsPath.GetRoundRect(new RectangleF(rect.X + 14, rect.Y + 4, rect.Width - 28, 5), 2.5f));
                }
                else if (i == _hover)
                {
                    g.FillPath(p.TileHover, GraphicsPath.GetRoundRect(rect, rect.Height / 2));
                }

                var ink = on ? Colors.White : p.SubtleText;
                var size = _font.MeasureString(text);
                float iconSize = 20, total = iconSize + 6 + size.Width;
                float x0 = rect.X + (rect.Width - total) / 2;
                Doodles.Icon(g, new RectangleF(x0, rect.Y + (rect.Height - iconSize) / 2, iconSize, iconSize), glyph, ink);
                g.DrawText(_font, ink, x0 + iconSize + 6, rect.Y + (rect.Height - size.Height) / 2, text);
            }
        }
    }

    /// <summary>A round colored badge with a white glyph.</summary>
    internal sealed class Badge : Drawable
    {
        private readonly Glyph _glyph;
        private readonly Color _color;

        public Badge(Glyph glyph, Color color, int size = 48)
        {
            _glyph = glyph;
            _color = color;
            Size = new Size(size, size);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Doodles.Badge(e.Graphics, new RectangleF(0, 0, Width, Height), _glyph, _color, Theme.Darken(_color, 0.15f));
        }
    }

    /// <summary>A small tinted pill with a dot or glyph and colored text, e.g. "New today!" or "3-day streak".</summary>
    internal sealed class Pill : Drawable
    {
        private readonly string _text;
        private readonly Color _color;
        private readonly Glyph? _glyph;
        private readonly Font _font = new Font(SystemFont.Bold, 8.5f);

        public Pill(string text, Color color, Glyph? glyph = null)
        {
            _text = text;
            _color = color;
            _glyph = glyph;
            Size = new Size((int)Math.Ceiling(_font.MeasureString(text).Width) + 34, 22);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var p = Palette.Current;
            g.AntiAlias = true;
            g.FillPath(Theme.Mix(_color, p.Background, 0.82f), GraphicsPath.GetRoundRect(new RectangleF(0, 0, Width - 1, Height - 1), (Height - 1) / 2f));
            if (_glyph.HasValue)
                Doodles.Icon(g, new RectangleF(6, 3, 15, 15), _glyph.Value, _color);
            else
                g.FillEllipse(_color, 9, 7, 8, 8);
            var size = _font.MeasureString(_text);
            g.DrawText(_font, Theme.Darken(_color, 0.1f), 24, (Height - size.Height) / 2, _text);
        }
    }
}
