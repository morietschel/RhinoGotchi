using System;
using System.Collections.Generic;
using Eto.Drawing;
using Eto.Forms;
using RhinoGotchi.Content;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// Learn Commands: the commands of one category as small cards in a grid, three columns (two on a narrow
    /// panel). Each card has the command's icon, its name, a short description (two lines at most) and a
    /// green ▶ when it has a demo. The whole card is the button. Drawn by hand, so the text always shows and
    /// is never clipped.
    /// </summary>
    internal sealed class CommandGrid : Drawable
    {
        private const float Gap = 8, CardH = 92, Pad = 10;

        private readonly IReadOnlyList<CommandEntry> _entries;
        private readonly Action<CommandEntry> _open;
        private int _hover = -1, _pressed = -1;

        public CommandGrid(IReadOnlyList<CommandEntry> entries, Action<CommandEntry> open)
        {
            _entries = entries;
            _open = open;
            Height = (int)CardH;
            SizeChanged += (s, e) => { FitHeight(); Invalidate(); };
            MouseMove += (s, e) =>
            {
                int i = IndexAt(e.Location);
                if (i != _hover)
                {
                    _hover = i;
                    Cursor = i >= 0 ? Cursors.Pointer : Cursors.Default;
                    Invalidate();
                }
            };
            MouseLeave += (s, e) => { _hover = _pressed = -1; Invalidate(); };
            MouseDown += (s, e) => { _pressed = IndexAt(e.Location); Invalidate(); e.Handled = true; };
            MouseUp += (s, e) =>
            {
                int i = IndexAt(e.Location), pressed = _pressed;
                _pressed = -1;
                Invalidate();
                if (i >= 0 && i == pressed)
                    _open(_entries[i]);
            };
        }

        /// <summary>Three columns, two when the panel is too narrow for three readable cards.</summary>
        private int Columns => Width >= 3 * 110 + 2 * Gap ? 3 : 2;

        private int Rows => (_entries.Count + Columns - 1) / Columns;

        private void FitHeight()
        {
            if (Width <= 0)
                return;
            int h = (int)Math.Ceiling(Math.Max(1, Rows) * (CardH + Gap));
            if (Math.Abs(Height - h) > 1)
                Application.Instance.AsyncInvoke(() => Height = h);
        }

        private RectangleF CardRect(int i)
        {
            int cols = Columns;
            float w = (Width - 1 - (cols - 1) * Gap) / cols;
            return new RectangleF(i % cols * (w + Gap), i / cols * (CardH + Gap), w, CardH);
        }

        private int IndexAt(PointF p)
        {
            for (int i = 0; i < _entries.Count; i++)
                if (CardRect(i).Contains(p))
                    return i;
            return -1;
        }

        private static float LineH(Font f) => Math.Max(f.LineHeight, f.MeasureString("Agjpy").Height);

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var p = Palette.Current;
            g.AntiAlias = true;
            g.FillRectangle(p.Background, 0, 0, Width, Height);
            if (Width < 60)
                return;

            Font name = Ui.Bold(10.5f), about = Ui.Regular(8.5f), demo = Ui.Bold(8.5f);
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                var r = CardRect(i);
                float lift = i == _pressed ? 3 : 0;
                Ui.Chunky(g, new RectangleF(r.X, r.Y, r.Width, r.Height - 4), 12,
                    i == _hover ? p.TileHover : p.TileFace, p.Border, lip: 3, lift: lift, stroke: 1.5f);

                float x = r.X + Pad, y = r.Y + Pad + lift, inner = r.Width - 2 * Pad;
                var icon = CommandIcons.Get(entry, 22);
                if (icon != null)
                    g.DrawImage(icon, x, y, 22, 22);
                if (entry.HasDemo)
                {
                    SizeF ds = demo.MeasureString("▶");
                    g.DrawText(demo, p.GreenText, r.Right - Pad - ds.Width, y + 2, "▶");
                }
                y += 26;

                g.DrawText(name, p.Text, x, y, Ui.Ellipsize(name, entry.Command, inner));
                y += LineH(name);

                var lines = Ui.Wrap(about, entry.Description, inner);
                for (int k = 0; k < lines.Count && k < 2; k++)
                {
                    string line = k == 1 && lines.Count > 2 ? Ui.Ellipsize(about, lines[k] + " " + lines[2], inner) : lines[k];
                    g.DrawText(about, p.SubtleText, x, y, line);
                    y += LineH(about);
                }
            }
        }
    }
}
