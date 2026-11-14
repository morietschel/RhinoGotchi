using Eto.Drawing;

namespace RhinoGotchi.UI
{
    /// <summary>Colors for the lesson panel, light and dark (follows Rhino's appearance).</summary>
    internal sealed class Theme
    {
        public Color Background, Text, SubtleText, Border, TileFace, TileHover;
        public Color SelectedFace, SelectedBorder, SelectedText;
        public Color Green, GreenShadow, GreenBanner, GreenText;
        public Color Red, RedShadow, RedBanner, RedText;
        public Color Blue, BlueShadow;
        public Color Disabled, DisabledText, Track;
        public Color Purple, Orange, Gold, Heart;

        public static Theme Current => IsDark() ? Dark : Light;

        private static bool IsDark()
        {
            try { return Rhino.Runtime.HostUtils.RunningInDarkMode; }
            catch { return false; }
        }

        private static Color C(int rgb) => Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);

        public static readonly Theme Light = new Theme
        {
            Background = C(0xFFFFFF),
            Text = C(0x4B4B4B),
            SubtleText = C(0x777777),
            Border = C(0xE5E5E5),
            TileFace = C(0xFFFFFF),
            TileHover = C(0xF7F7F7),
            SelectedFace = C(0xDDF4FF),
            SelectedBorder = C(0x84D8FF),
            SelectedText = C(0x1899D6),
            Green = C(0x58CC02),
            GreenShadow = C(0x58A700),
            GreenBanner = C(0xD7FFB8),
            GreenText = C(0x58A700),
            Red = C(0xFF4B4B),
            RedShadow = C(0xEA2B2B),
            RedBanner = C(0xFFDFE0),
            RedText = C(0xEA2B2B),
            Blue = C(0x1CB0F6),
            BlueShadow = C(0x1899D6),
            Disabled = C(0xE5E5E5),
            DisabledText = C(0xAFAFAF),
            Track = C(0xE5E5E5),
            Purple = C(0xCE82FF),
            Orange = C(0xFF9600),
            Gold = C(0xFFC800),
            Heart = C(0xFF4B4B),
        };

        public static readonly Theme Dark = new Theme
        {
            Background = C(0x131F24),
            Text = C(0xF1F7FB),
            SubtleText = C(0x8FA6B2),
            Border = C(0x37464F),
            TileFace = C(0x131F24),
            TileHover = C(0x1B2B32),
            SelectedFace = C(0x1B3A4B),
            SelectedBorder = C(0x3F85A7),
            SelectedText = C(0x49C0F8),
            Green = C(0x93D333),
            GreenShadow = C(0x79B933),
            GreenBanner = C(0x202F36),
            GreenText = C(0x93D333),
            Red = C(0xEE5555),
            RedShadow = C(0xD84848),
            RedBanner = C(0x202F36),
            RedText = C(0xEE5555),
            Blue = C(0x49C0F8),
            BlueShadow = C(0x3F85A7),
            Disabled = C(0x37464F),
            DisabledText = C(0x52656D),
            Track = C(0x37464F),
            Purple = C(0xCE82FF),
            Orange = C(0xFF9600),
            Gold = C(0xFFC800),
            Heart = C(0xEE5555),
        };
    }
}
