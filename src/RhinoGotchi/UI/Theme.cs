using System;
using Eto.Drawing;
using Eto.Forms;
using RhinoGotchi.Core;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// Shared building blocks in the RhinoGotchi style (see <see cref="Palette"/>): bold text,
    /// chunky buttons, rounded cards and colored tiles. Colors follow Rhino's light or dark mode.
    /// </summary>
    internal static class Theme
    {
        public static Palette P => Palette.Current;

        // Meaning-based colors used by the games.
        public static Color Hit => P.Green;              // right answer / right place
        public static Color Near => P.Gold;              // close: used, but elsewhere / highlighted
        public static Color Miss => P.DisabledText;      // not part of the answer
        public static Color Tile => P.TileFace;          // neutral tile or card
        public static Color TileText => P.Text;
        public static Color Selected => P.SelectedFace;  // a filled slot
        public static Color Light => Colors.White;

        public static Font TitleFont => new Font(SystemFont.Bold, 16);
        public static Font HeadingFont => new Font(SystemFont.Bold, 11);
        public static Font BodyFont => new Font(SystemFont.Default, 10);

        // Text is drawn by hand (see Words): Eto's wrapping Label misbehaves on the Mac.
        public static Words Title(string text) => new Words(text, TitleFont, P.Text);

        public static Words Heading(string text) => new Words(text, HeadingFont, P.Text);

        public static Words Body(string text) => new Words(text, BodyFont, P.Text);

        public static Words Muted(string text) => new Words(text, BodyFont, P.SubtleText);

        /// <summary>Small print: notes and links.</summary>
        public static Words Small(string text) => new Words(text, new Font(SystemFont.Default, 8.5f), P.SubtleText);

        /// <summary>A tile with centered bold text, e.g. a Build Order slot. Width 0 stretches to fill its cell.</summary>
        public static ChunkyButton Chip(string text, Color back, Color fore, int width = 0, int height = 34)
        {
            var chip = new ChunkyButton(text, ButtonKind.Tile, height) { Font = new Font(SystemFont.Bold, 9) };
            chip.SetColors(back, back == P.TileFace || back == P.SelectedFace ? (back == P.SelectedFace ? P.SelectedBorder : P.Border) : Darken(back, 0.12f), fore);
            if (width > 0)
                chip.Width = width;
            return chip;
        }

        /// <summary>
        /// A rounded card for explanations and results. A colored accent turns it into a verdict banner
        /// (green for right, gold/red for close or wrong); the neutral tile color gives a plain card.
        /// </summary>
        public static StyledCard Card(Color accent, params Control[] rows)
        {
            var stack = new StackLayout { Spacing = 4, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            bool neutral = accent == P.TileFace;
            bool first = true;
            foreach (var row in rows)
            {
                if (row is Words text && first && !neutral)
                    text.TextColor = Darken(accent, 0.15f);
                first = false;
                stack.Items.Add(row);
            }
            return neutral
                ? new StyledCard(stack)
                : new StyledCard(stack, Mix(accent, P.Background, 0.82f), accent);
        }

        /// <summary>A plain rounded card around any content.</summary>
        public static StyledCard Box(Control content, int padding = 10) => new StyledCard(content, padding: padding);

        public static ChunkyButton Button(string text, Action onClick, ButtonKind kind = ButtonKind.Outline)
        {
            var b = new ChunkyButton(text, kind);
            b.Click += (s, e) => onClick();
            return b;
        }

        public static ChunkyButton Primary(string text, Action onClick) => Button(text, onClick, ButtonKind.Primary);

        public static ChunkyButton Secondary(string text, Action onClick) => Button(text, onClick, ButtonKind.Secondary);

        /// <summary>Controls side by side, wrapping onto more lines when the panel is narrow.</summary>
        public static FlowRow Row(params Control[] controls) => new FlowRow(controls);

        /// <summary>Lays controls out left to right, wrapping every <paramref name="columns"/>.</summary>
        /// <summary>
        /// A grid of up to <paramref name="columns"/> equal columns that drops to fewer columns when the
        /// panel is squished, so its buttons never push the page wider than the panel.
        /// </summary>
        public static Flow Grid(int columns, params Control[] controls) => new Flow(controls, columns, stretch: true);

        /// <summary>Like <see cref="Grid(int, Control[])"/>, but a column never gets narrower than <paramref name="minCell"/>.</summary>
        public static Flow Grid(int columns, int minCell, params Control[] controls) => new Flow(controls, columns, stretch: true, minCell: minCell, gap: 10);

        /// <summary>Small items (pills) in a line that wraps onto the next line when it runs out of room.</summary>
        public static Flow Wrap(params Control[] controls) => new Flow(controls, int.MaxValue, stretch: false);

        /// <summary>A full-width item, e.g. a single button under a card.</summary>
        public static StackLayoutItem Wide(Control control)
        {
            if (control is ChunkyButton b)
                b.Fill = true;
            return new StackLayoutItem(control, HorizontalAlignment.Stretch);
        }

        /// <summary>"Try it in Rhino" and "Help page" buttons for a command.</summary>
        public static FlowRow CommandButtons(string command) => Row(
            Primary($"Try {command}", () => RhinoActions.Run(command)),
            Secondary("Help page", () => RhinoActions.OpenHelp(command)));

        /// <summary>Gives a view the standard padding, background and a header with a back button.</summary>
        public static void SetUpPage(StackLayout page, RhinoGotchiPanel host, string title, string subtitle,
            string backText = "← Home", Action? back = null)
        {
            page.Padding = 14;
            page.Spacing = 10;
            page.BackgroundColor = P.Background;
            page.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            page.Items.Add(new PageHeader(title, back ?? host.ShowHome));
            if (subtitle.Length > 0)
                page.Items.Add(Muted(subtitle));
        }

        public static Color Darken(Color c, float amount) =>
            new Color(Math.Max(0, c.R - amount), Math.Max(0, c.G - amount), Math.Max(0, c.B - amount), c.A);

        /// <summary>Blends <paramref name="a"/> toward <paramref name="b"/> by <paramref name="t"/> (0 = a, 1 = b).</summary>
        public static Color Mix(Color a, Color b, float t) =>
            new Color(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, 1);
    }
}
