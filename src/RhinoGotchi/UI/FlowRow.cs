using System;
using System.Collections.Generic;
using Eto.Drawing;
using Eto.Forms;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// A row of controls that wraps onto more lines when the panel is too narrow for one,
    /// so a row of buttons never makes the page wider than the panel.
    /// </summary>
    internal sealed class FlowRow : PixelLayout
    {
        private const int Gap = 6;
        private readonly List<(Control Control, int Width, int Height)> _items = new List<(Control, int, int)>();
        private bool _arranging;

        public FlowRow(params Control[] controls)
        {
            foreach (var c in controls)
                AddItem(c);
            Arrange();
            SizeChanged += (s, e) => Arrange();
        }

        public void Add(Control control)
        {
            AddItem(control);
            Arrange();
        }

        private void AddItem(Control control)
        {
            // Remember the size the control asked for, so it can shrink in a narrow panel and grow back.
            var preferred = control.GetPreferredSize();
            int w = control.Width > 0 ? control.Width : (int)Math.Ceiling(preferred.Width);
            int h = control.Height > 0 ? control.Height : (int)Math.Ceiling(preferred.Height);
            _items.Add((control, w, h));
            Add(control, 0, 0);
        }

        private void Arrange()
        {
            if (_arranging)
                return;
            _arranging = true;
            try
            {
                int available = Width > 0 ? Width : int.MaxValue;
                int x = 0, y = 0, rowHeight = 0;
                foreach (var (control, natural, height) in _items)
                {
                    int w = Math.Min(natural, available);
                    if (x > 0 && x + w > available)
                    {
                        x = 0;
                        y += rowHeight + Gap;
                        rowHeight = 0;
                    }
                    if (control.Width != w)
                        control.Width = w;
                    Move(control, x, y);
                    x += w + Gap;
                    rowHeight = Math.Max(rowHeight, height);
                }
                int total = y + rowHeight;
                if (Height != total)
                    Height = total;
            }
            finally
            {
                _arranging = false;
            }
        }
    }
}
