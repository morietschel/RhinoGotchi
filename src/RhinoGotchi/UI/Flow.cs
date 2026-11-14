using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// A control whose layout depends on the width it gets. The panel drives it when the window is
    /// resized: first <see cref="Collapse"/> (narrowest layout, so it can't hold the page wide),
    /// then, once layout has settled, <see cref="Reflow"/> (best layout for the real width).
    /// Controls don't react to their own size changes, which avoids resize feedback loops.
    /// </summary>
    internal interface IResponsive
    {
        void Collapse();
        void Reflow();
    }

    /// <summary>
    /// A container that re-flows its children to the panel width, so screens fit whether the panel
    /// is squished or wide.
    /// Stretch mode: a grid with as many equal columns as fit (up to a maximum); children fill their cells.
    /// Wrap mode: children keep their own width and wrap onto new lines, like words in a sentence.
    /// </summary>
    internal sealed class Flow : Panel, IResponsive
    {
        private readonly List<Control> _items;
        private readonly int _maxColumns;
        private readonly bool _stretch;
        private readonly int _minCell;
        private readonly int _gap;
        private string _layoutKey = "";
        private Control? _built;

        public Flow(IEnumerable<Control> items, int maxColumns, bool stretch, int minCell = 0, int gap = 6)
        {
            _items = items.ToList();
            _maxColumns = Math.Max(1, maxColumns);
            _stretch = stretch;
            _minCell = minCell;
            _gap = gap;
            foreach (var b in _items.OfType<ChunkyButton>())
                if (stretch)
                    b.Fill = true;
            Build(Arrange(280));
            // When added to a page that's already showing, fit once the real width is known.
            LoadComplete += (s, e) => Application.Instance.AsyncInvoke(Reflow);
        }

        public void Add(Control control)
        {
            if (_stretch && control is ChunkyButton b)
                b.Fill = true;
            _items.Add(control);
            Build(Arrange(Width > 0 ? Width : 280));
        }

        /// <summary>Enables or disables every child (Eto doesn't pass Enabled down to drawn controls).</summary>
        public void SetEnabled(bool enabled)
        {
            foreach (var c in _items)
                c.Enabled = enabled;
        }

        public void Collapse() => Build(Arrange(0));

        public void Reflow()
        {
            if (Width > 0)
                Build(Arrange(Width));
        }

        /// <summary>The width an item needs. Cards and other stretchy content report 0 (they fit anything).</summary>
        private static int NaturalWidth(Control c) => c switch
        {
            ChunkyButton b => b.NaturalWidth,
            Pill p => p.Width,
            Badge g => g.Width,
            _ => 0,
        };

        /// <summary>Splits the items into rows for a given width (0 = narrowest: one per row).</summary>
        private List<List<Control>> Arrange(int width)
        {
            var rows = new List<List<Control>>();
            if (_stretch)
            {
                int columns = 1;
                if (width > 0)
                {
                    int cell = Math.Max(_minCell, _items.Count == 0 ? 0 : _items.Max(NaturalWidth));
                    columns = cell <= 0 ? _maxColumns : (width + _gap) / (cell + _gap);
                    columns = Math.Max(1, Math.Min(Math.Min(_maxColumns, Math.Max(1, _items.Count)), columns));
                }
                for (int i = 0; i < _items.Count; i += columns)
                    rows.Add(_items.Skip(i).Take(columns).ToList());
                return rows;
            }

            rows.Add(new List<Control>());
            int used = 0;
            foreach (var c in _items)
            {
                int w = NaturalWidth(c);
                var row = rows[rows.Count - 1];
                if (row.Count > 0 && (width <= 0 || used + _gap + w > width))
                {
                    row = new List<Control>();
                    rows.Add(row);
                    used = 0;
                }
                used += (row.Count > 0 ? _gap : 0) + w;
                row.Add(c);
            }
            return rows;
        }

        private void Build(List<List<Control>> rows)
        {
            string key = string.Join(",", rows.Select(r => r.Count));
            if (key == _layoutKey && _built != null)
                return;
            _layoutKey = key;
            DetachChildren();

            if (_stretch)
            {
                int columns = rows.Count == 0 ? 1 : rows.Max(r => r.Count);
                var table = new TableLayout { Spacing = new Size(_gap, _gap) };
                foreach (var row in rows)
                {
                    var tr = new TableRow();
                    foreach (var c in row)
                        tr.Cells.Add(new TableCell(c, true));
                    for (int i = row.Count; i < columns; i++)
                        tr.Cells.Add(new TableCell(null, true));
                    table.Rows.Add(tr);
                }
                _built = table;
            }
            else
            {
                var stack = new StackLayout { Spacing = _gap };
                foreach (var row in rows)
                {
                    var line = new StackLayout { Orientation = Orientation.Horizontal, Spacing = _gap };
                    foreach (var c in row)
                        line.Items.Add(c);
                    stack.Items.Add(line);
                }
                _built = stack;
            }
            Content = _built;
        }

        /// <summary>Takes the children out of the previous layout so they can be placed again.</summary>
        private void DetachChildren()
        {
            Content = null;
            switch (_built)
            {
                case TableLayout table:
                    foreach (var row in table.Rows)
                        foreach (var cell in row.Cells)
                            cell.Control = null;
                    break;
                case StackLayout stack:
                    foreach (var line in stack.Items.Select(i => i.Control).OfType<StackLayout>())
                        line.Items.Clear();
                    stack.Items.Clear();
                    break;
            }
            _built = null;
        }
    }
}
