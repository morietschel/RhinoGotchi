using System;
using Eto.Drawing;
using Eto.Forms;
using RhinoGotchi.Content;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// A copy of a Rhino toolbar: its button icons in order with one highlighted. Wraps to as many
    /// icons per row as fit the panel width, and re-wraps when the panel is resized.
    /// </summary>
    internal sealed class ToolbarStrip : Panel
    {
        private const int IconSize = 20;
        private const int CellSize = IconSize + 6;
        private const int Gap = 2;

        private readonly string[] _tips;
        private readonly int _highlight;
        private int _perRow;

        public ToolbarStrip(string toolbar, int highlight)
        {
            _tips = ToolbarMap.Buttons(toolbar);
            _highlight = highlight;
            Padding = 4;
            Rebuild(10);
            SizeChanged += (s, e) =>
            {
                int fit = Math.Max(4, (Width - 8 + Gap) / (CellSize + Gap));
                if (Width > 0 && fit != _perRow)
                    Rebuild(fit);
            };
        }

        private void Rebuild(int perRow)
        {
            _perRow = perRow;
            var rows = new StackLayout { Spacing = Gap };
            for (int i = 0; i < _tips.Length; i += perRow)
            {
                var row = new StackLayout { Orientation = Orientation.Horizontal, Spacing = Gap };
                for (int j = i; j < Math.Min(i + perRow, _tips.Length); j++)
                {
                    var image = CommandIcons.GetFile(ToolbarMap.IconFileForTip(_tips[j]), IconSize);
                    row.Items.Add(new Panel
                    {
                        Size = new Size(CellSize, CellSize),
                        Padding = 3,
                        BackgroundColor = j == _highlight ? Theme.Near : Colors.Transparent,
                        Content = image != null ? new ImageView { Image = image } : (Control)new Label { Text = "·", TextAlignment = TextAlignment.Center },
                        ToolTip = $"{j + 1}. {_tips[j].Trim()}",
                    });
                }
                rows.Items.Add(row);
            }
            Content = rows;
        }
    }
}
