using Eto.Drawing;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// The RhinoGotchi look: clean black and white with a muted green for "right", orange for
    /// "selected / streak / start here", and Duolingo-style chunky shading (every button and card
    /// sits on a darker lip). Follows Rhino's light or dark mode; dark mode inverts the ink.
    /// </summary>
    internal sealed class Palette
    {
        public Color Background, Text, SubtleText, Border, TileFace, TileHover;
        public Color SelectedFace, SelectedBorder, SelectedText;
        public Color Green, GreenShadow, GreenBanner, GreenText;
        public Color Red, RedShadow, RedBanner, RedText;
        public Color Blue, BlueShadow;
        public Color Disabled, DisabledText, Track;
        public Color Purple, Orange, Gold, GoldShadow;

        /// <summary>Primary buttons and strong outlines: near-black (light mode) or near-white (dark mode).</summary>
        public Color Ink, InkShadow, OnInk;

        /// <summary>Orange accent: selection, streaks, "start here".</summary>
        public Color Accent, AccentShadow, AccentSoft, AccentText;

        /// <summary>Warm card behind the pet, and a quiet gray card for tips.</summary>
        public Color Warm, WarmBorder, Quiet;

        /// <summary>Pastel tiles behind list icons (peach, butter, lilac, sky, rose, mint).</summary>
        public Color[] Tints = new Color[0];

        public static Palette Current => IsDark() ? Dark : Light;

        private static bool IsDark()
        {
            try { return Rhino.Runtime.HostUtils.RunningInDarkMode; }
            catch { return false; }
        }

        private static Color C(int rgb) => Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);

        /// <summary>
        /// FinGotchi's look, used by the Learning Campaign: white paper, near-black ink, a yellow
        /// "do this now" accent, orange for levels, green for pass, cream cards. Light only, like FinGotchi.
        /// </summary>
        public static readonly Palette Fin = new Palette
        {
            Background = C(0xFFFFFF),
            Text = C(0x121212),
            SubtleText = C(0x6B6B6B),
            Border = C(0xD6D6D6),
            TileFace = C(0xFFFFFF),
            TileHover = C(0xF6F6F6),
            SelectedFace = C(0xFFD34D),
            SelectedBorder = C(0x121212),
            SelectedText = C(0x121212),
            Green = C(0x1E7A3A),
            GreenShadow = C(0x125226),
            GreenBanner = C(0xEAF6EE),
            GreenText = C(0x1E7A3A),
            Red = C(0xE8812C),
            RedShadow = C(0xA34808),
            RedBanner = C(0xFFF1E4),
            RedText = C(0xA34808),
            Blue = C(0x2A5CB8),
            BlueShadow = C(0x1D427F),
            Disabled = C(0xE6E6E6),
            DisabledText = C(0x9A9A9A),
            Track = C(0xEDEDED),
            Purple = C(0x7564C4),
            Orange = C(0xE8812C),
            Gold = C(0xFFD34D),
            GoldShadow = C(0x121212),
            Ink = C(0x121212),
            InkShadow = C(0x121212),
            OnInk = C(0xFFFFFF),
            Accent = C(0xE8812C),
            AccentShadow = C(0xA34808),
            AccentSoft = C(0xFFF7EC),
            AccentText = C(0xA34808),
            Warm = C(0xFFF7EC),
            WarmBorder = C(0xF3DCC0),
            Quiet = C(0xF6F6F6),
            Tints = new[] { C(0xF9D6A8), C(0xFDE68A), C(0xDCD3F7), C(0xCFE6F7), C(0xF6D1CC), C(0xCFEEDB) },
        };

        public static readonly Palette Light = new Palette
        {
            Background = C(0xFFFFFF),
            Text = C(0x121212),
            SubtleText = C(0x555555),
            Border = C(0xE2E2E2),
            TileFace = C(0xFFFFFF),
            TileHover = C(0xF6F6F6),
            SelectedFace = C(0xFFF3E6),
            SelectedBorder = C(0xE8590C),
            SelectedText = C(0xC2410C),
            Green = C(0x2F9A4F),
            GreenShadow = C(0x237A3C),
            GreenBanner = C(0xE6F4EA),
            GreenText = C(0x237A3C),
            Red = C(0xD6453D),
            RedShadow = C(0xAE342D),
            RedBanner = C(0xFDECEA),
            RedText = C(0xAE342D),
            Blue = C(0x3F6FA6),
            BlueShadow = C(0x2F5683),
            Disabled = C(0xE6E6E6),
            DisabledText = C(0x8C8C8C),
            Track = C(0xF0F0F0),
            Purple = C(0x7564C4),
            Orange = C(0xE8590C),
            Gold = C(0xE0A800),
            GoldShadow = C(0xB78900),
            Ink = C(0x222222),
            InkShadow = C(0x000000),
            OnInk = C(0xFFFFFF),
            Accent = C(0xE8590C),
            AccentShadow = C(0xB8460A),
            AccentSoft = C(0xFFF3E6),
            AccentText = C(0xC2410C),
            Warm = C(0xFBF6EF),
            WarmBorder = C(0xEFE3D2),
            Quiet = C(0xF6F6F6),
            Tints = new[] { C(0xF9D6A8), C(0xFDE68A), C(0xDCD3F7), C(0xCFE6F7), C(0xF6D1CC), C(0xCFEEDB) },
        };

        public static readonly Palette Dark = new Palette
        {
            Background = C(0x121212),
            Text = C(0xF2F2F2),
            SubtleText = C(0xA3A3A3),
            Border = C(0x333333),
            TileFace = C(0x1A1A1A),
            TileHover = C(0x242424),
            SelectedFace = C(0x2E1F14),
            SelectedBorder = C(0xF0702A),
            SelectedText = C(0xFF9A5C),
            Green = C(0x4CB26A),
            GreenShadow = C(0x378550),
            GreenBanner = C(0x1A2A1F),
            GreenText = C(0x72C98C),
            Red = C(0xE5625A),
            RedShadow = C(0xB04A44),
            RedBanner = C(0x2E1C1B),
            RedText = C(0xF08A83),
            Blue = C(0x6E9BD0),
            BlueShadow = C(0x4F79AA),
            Disabled = C(0x2C2C2C),
            DisabledText = C(0x6E6E6E),
            Track = C(0x262626),
            Purple = C(0x9C8DE0),
            Orange = C(0xF0702A),
            Gold = C(0xF2C14E),
            GoldShadow = C(0xC29A3A),
            Ink = C(0xF2F2F2),
            InkShadow = C(0xA6A6A6),
            OnInk = C(0x121212),
            Accent = C(0xF0702A),
            AccentShadow = C(0xB8541F),
            AccentSoft = C(0x2E1F14),
            AccentText = C(0xFF9A5C),
            Warm = C(0x221E19),
            WarmBorder = C(0x3A3229),
            Quiet = C(0x1E1E1E),
            Tints = new[] { C(0x5A4228), C(0x5A4E1E), C(0x3E3760), C(0x28465C), C(0x5A3430), C(0x234A33) },
        };
    }
}
