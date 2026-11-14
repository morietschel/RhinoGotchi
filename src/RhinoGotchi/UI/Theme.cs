using System;
using Eto.Drawing;
using Eto.Forms;
using RhinoGotchi.Core;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// Colors and small building blocks shared by every view. Tiles always set both background and
    /// text color so they read the same in Rhino's light and dark themes.
    /// </summary>
    internal static class Theme
    {
        public static readonly Color Hit = Color.FromArgb(46, 158, 143);     // right answer
        public static readonly Color Near = Color.FromArgb(222, 160, 40);    // wrong answer
        public static readonly Color Tile = Color.FromArgb(226, 228, 224);   // neutral card
        public static readonly Color TileText = Color.FromArgb(30, 34, 38);

        public static Font TitleFont => new Font(SystemFont.Bold, 14);
        public static Font HeadingFont => new Font(SystemFont.Bold, 10);

        public static Label Title(string text) => new Label { Text = text, Font = TitleFont };

        public static Label Heading(string text) => new Label { Text = text, Font = HeadingFont };

        public static Label Body(string text) => new Label { Text = text, Wrap = WrapMode.Word };

        public static Label Muted(string text) =>
            new Label { Text = text, Wrap = WrapMode.Word, TextColor = SystemColors.DisabledText };

        public static readonly Color Miss = Color.FromArgb(105, 110, 116);   // not part of the answer
        public static readonly Color Selected = Color.FromArgb(58, 71, 80);  // a filled slot
        public static readonly Color Light = Colors.White;

        /// <summary>A colored tile with centered text, e.g. a slot in Build Order. Width 0 stretches to fill its cell.</summary>
        public static Panel Chip(string text, Color back, Color fore, int width = 0, int height = 30)
        {
            var label = new Label
            {
                Text = text,
                TextColor = fore,
                Font = new Font(SystemFont.Bold, 8),
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Wrap = WrapMode.Character,
            };
            return new Panel { BackgroundColor = back, Size = new Size(width > 0 ? width : -1, height), Content = label };
        }

        /// <summary>A colored card with padding for explanations and results.</summary>
        public static Panel Card(Color back, params Control[] rows)
        {
            var stack = new StackLayout
            {
                Spacing = 4,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
            };
            foreach (var row in rows)
            {
                if (row is Label l)
                    l.TextColor = TileText;
                stack.Items.Add(row);
            }
            return new Panel { BackgroundColor = back, Padding = 8, Content = stack };
        }

        public static Button Button(string text, Action onClick)
        {
            var b = new Button { Text = text };
            b.Click += (s, e) => onClick();
            return b;
        }

        public static StackLayout Row(params Control[] controls)
        {
            var row = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 6 };
            foreach (var c in controls)
                row.Items.Add(c);
            return row;
        }

        /// <summary>Lays controls out left to right, wrapping every <paramref name="columns"/>.</summary>
        public static TableLayout Grid(int columns, params Control[] controls)
        {
            var table = new TableLayout { Spacing = new Size(4, 4) };
            for (int i = 0; i < controls.Length; i += columns)
            {
                var row = new TableRow();
                for (int j = i; j < System.Math.Min(i + columns, controls.Length); j++)
                    row.Cells.Add(new TableCell(controls[j], true));
                for (int j = controls.Length; j < i + columns; j++)
                    row.Cells.Add(new TableCell(null, true));
                table.Rows.Add(row);
            }
            return table;
        }

        /// <summary>"Try it in Rhino" and "Help page" buttons for a command.</summary>
        public static StackLayout CommandButtons(string command) => Row(
            Button($"Try {command}", () => RhinoActions.Run(command)),
            Button("Help page", () => RhinoActions.OpenHelp(command)));

        /// <summary>Gives a view the standard padding and a header with a back button.</summary>
        public static void SetUpPage(StackLayout page, RhinoGotchiPanel host, string title, string subtitle,
            string backText = "← Home", Action? back = null)
        {
            page.Padding = 12;
            page.Spacing = 8;
            page.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            page.Items.Add(new StackLayoutItem(Button(backText, back ?? host.ShowHome), HorizontalAlignment.Left));
            page.Items.Add(Title(title));
            page.Items.Add(Muted(subtitle));
        }
    }
}
