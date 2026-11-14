using System.Linq;
using Eto.Drawing;
using SD = System.Drawing;

namespace GotchiHouse
{
    /// <summary>
    /// The GotchiHouse colours: green, white and black. Eto colours are for the
    /// panel; System.Drawing colours are for Rhino's viewports.
    /// </summary>
    internal static class Palette
    {
        // Panel (Eto)
        public static readonly Color Shell = Color.FromArgb(0x6B, 0xBF, 0x3B);      // device body
        public static readonly Color ShellDark = Color.FromArgb(0x4E, 0x9A, 0x2A);  // body shading
        public static readonly Color Ink = Color.FromArgb(0x14, 0x14, 0x14);        // outlines, text
        public static readonly Color Paper = Color.FromArgb(0xFA, 0xFA, 0xF5);      // white
        public static readonly Color Screen = Color.FromArgb(0xF2, 0xF8, 0xEC);     // LCD, white with a hint of green
        public static readonly Color Leaf = Color.FromArgb(0x3F, 0x8F, 0x2B);       // readable green for text
        public static readonly Color Mint = Color.FromArgb(0xDC, 0xEE, 0xCF);       // light green face shade
        public static readonly Color Moss = Color.FromArgb(0xB5, 0xD9, 0x9C);       // mid green face shade
        public static readonly Color Disabled = Color.FromArgb(0x14, 0x14, 0x14, 0x55);     // greyed-out ink
        public static readonly Color GhostFill = Color.FromArgb(0x6B, 0xBF, 0x3B, 0x30);

        // Viewport (System.Drawing)
        public static readonly SD.Color RhinoGreen = SD.Color.FromArgb(0x6B, 0xBF, 0x3B);
        public static readonly SD.Color RhinoDeepGreen = SD.Color.FromArgb(0x1F, 0x5F, 0x2A);
        public static readonly SD.Color RhinoInk = SD.Color.FromArgb(0x14, 0x14, 0x14);
        public static readonly SD.Color RhinoPaper = SD.Color.FromArgb(0xFA, 0xFA, 0xF5);
    }

    /// <summary>Korolev when Rhino can see it, otherwise the system font.</summary>
    internal static class GotchiFonts
    {
        private static FontFamily? _family;

        public static FontFamily Family => _family ??= FindFamily();

        public static bool IsKorolev => Family.Name.IndexOf("Korolev", System.StringComparison.OrdinalIgnoreCase) >= 0;

        public static Font Get(float size, bool bold = false) =>
            new Font(Family, size, bold ? FontStyle.Bold : FontStyle.None);

        private static FontFamily FindFamily()
        {
            try
            {
                FontFamily? korolev = Fonts.AvailableFontFamilies
                    .Where(f => f.Name.IndexOf("Korolev", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(f => f.Name.Length) // prefer plain "Korolev" over "Korolev Condensed" etc.
                    .FirstOrDefault();
                if (korolev != null)
                    return korolev;
            }
            catch
            {
                // Font enumeration can fail on some systems; fall back below.
            }
            return SystemFonts.Default().Family;
        }
    }
}
