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

        /// <summary>"Try it in Rhino" and "Help page" buttons for a command.</summary>
        public static StackLayout CommandButtons(string command) => Row(
            Button($"Try {command}", () => RhinoActions.Run(command)),
            Button("Help page", () => RhinoActions.OpenHelp(command)));

        /// <summary>Gives a view the standard padding and a header with a back button.</summary>
        public static void SetUpPage(StackLayout page, RhinoGotchiPanel host, string title, string subtitle)
        {
            page.Padding = 12;
            page.Spacing = 8;
            page.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            page.Items.Add(new StackLayoutItem(Button("← Games", host.ShowHome), HorizontalAlignment.Left));
            page.Items.Add(Title(title));
            page.Items.Add(Muted(subtitle));
        }
    }
}
