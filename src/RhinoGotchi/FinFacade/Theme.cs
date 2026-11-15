using System;
using System.Linq;
using Eto.Drawing;
using SD = System.Drawing;
using RhinoGotchi.Fin;

namespace GotchiFins
{
    /// <summary>
    /// The RhinoGotchi look: near-black ink on white, soft greys, a yellow "do this now"
    /// accent, an orange level accent and a green "pass". Eto colours are for the panel and
    /// the viewport cards; System.Drawing colours are for Rhino's own viewport drawing.
    /// </summary>
    internal static class Palette
    {
        // Panel and cards (Eto)
        public static readonly Color Paper = Color.FromArgb(0xFF, 0xFF, 0xFF);
        public static readonly Color Ink = Color.FromArgb(0x12, 0x12, 0x12);
        public static readonly Color Body = Color.FromArgb(0x44, 0x44, 0x44);
        public static readonly Color Muted = Color.FromArgb(0x6B, 0x6B, 0x6B);
        public static readonly Color Faint = Color.FromArgb(0x9A, 0x9A, 0x9A);
        public static readonly Color Rule = Color.FromArgb(0xD6, 0xD6, 0xD6);
        public static readonly Color Wash = Color.FromArgb(0xF6, 0xF6, 0xF6);
        public static readonly Color Light = Color.FromArgb(0xE6, 0xE6, 0xE6);    // mid face shade in the demo
        public static readonly Color Shadow = Color.FromArgb(0xC4, 0xC4, 0xC4);   // dark face shade in the demo
        public static readonly Color Accent = Color.FromArgb(0xFF, 0xD3, 0x4D);   // yellow: click this, do this now
        public static readonly Color Orange = Color.FromArgb(0xE8, 0x81, 0x2C);   // level accent
        public static readonly Color OrangeText = Color.FromArgb(0xA3, 0x48, 0x08);
        public static readonly Color Cream = Color.FromArgb(0xFF, 0xF7, 0xEC);    // lesson card
        public static readonly Color CreamRule = Color.FromArgb(0xF3, 0xDC, 0xC0);
        public static readonly Color Green = Color.FromArgb(0x1E, 0x7A, 0x3A);
        public static readonly Color GreenSoft = Color.FromArgb(0x5F, 0xD3, 0x8A);
        public static readonly Color MissSoft = Color.FromArgb(0xFF, 0x9A, 0x4D);
        public static readonly Color Night = Color.FromArgb(0x15, 0x15, 0x15);     // check card
        public static readonly Color NightRule = Color.FromArgb(0x2A, 0x2A, 0x2A);
        public static readonly Color GhostFill = Color.FromArgb(0x12, 0x12, 0x12, 0x10);
        public static readonly Color Disabled = Color.FromArgb(0x12, 0x12, 0x12, 0x40);

        // Rhino viewport (System.Drawing)
        public static readonly SD.Color RhinoInk = SD.Color.FromArgb(0x12, 0x12, 0x12);
        public static readonly SD.Color RhinoPaper = SD.Color.FromArgb(0xFF, 0xFF, 0xFF);
        public static readonly SD.Color RhinoGrey = SD.Color.FromArgb(0x9A, 0x9A, 0x9A);
        public static readonly SD.Color RhinoShadow = SD.Color.FromArgb(0x9C, 0x9C, 0x9C);
        public static readonly SD.Color RhinoAccent = SD.Color.FromArgb(0xFF, 0xD3, 0x4D);
    }

    /// <summary>
    /// The RhinoGotchi type: a slab serif for headlines, Libre Franklin for text, and a mono
    /// for command names. The first installed family from each list is used.
    /// </summary>
    internal static class Typeface
    {
        private static readonly string[] SerifNames = { "Zilla Slab", "Roboto Slab", "Rockwell", "Georgia", "Times New Roman", "Times" };
        private static readonly string[] SansNames = { "Libre Franklin", "Franklin Gothic", "Segoe UI", "Helvetica Neue", "Helvetica", "Arial" };
        private static readonly string[] MonoNames = { "JetBrains Mono", "Cascadia Mono", "Consolas", "Menlo", "Courier New" };

        private static FontFamily? _serif, _sans, _mono;

        public static FontFamily SerifFamily => _serif ??= Find(SerifNames);
        public static FontFamily SansFamily => _sans ??= Find(SansNames);
        public static FontFamily MonoFamily => _mono ??= Find(MonoNames);

        /// <summary>Name to use for text drawn in Rhino viewports.</summary>
        public static string ViewportFace => SansFamily.Name;

        public static Font Serif(float size, bool bold = true, bool italic = false) =>
            new Font(SerifFamily, size, Style(bold, italic));

        public static Font Sans(float size, bool bold = false, bool italic = false) =>
            new Font(SansFamily, size, Style(bold, italic));

        public static Font Mono(float size, bool bold = true) =>
            new Font(MonoFamily, size, Style(bold, false));

        private static FontStyle Style(bool bold, bool italic) =>
            (bold ? FontStyle.Bold : FontStyle.None) | (italic ? FontStyle.Italic : FontStyle.None);

        private static FontFamily Find(string[] names)
        {
            try
            {
                var families = Fonts.AvailableFontFamilies.ToList();
                foreach (string name in names)
                {
                    FontFamily? match = families
                        .Where(f => f.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(f => f.Name.Length) // prefer "Georgia" over "Georgia Pro Condensed"
                        .FirstOrDefault();
                    if (match != null)
                        return match;
                }
            }
            catch
            {
                // Font enumeration can fail on some systems; fall back below.
            }
            return SystemFonts.Default().Family;
        }
    }
}
