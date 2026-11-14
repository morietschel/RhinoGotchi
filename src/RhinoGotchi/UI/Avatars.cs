using System;
using System.Collections.Generic;
using Eto.Drawing;
using Eto.Forms;
using RhinoGotchi.Core;

namespace RhinoGotchi.UI
{
    /// <summary>Turns a picture into a small leaderboard icon and back, and caches other players' icons.</summary>
    internal static class Avatars
    {
        /// <summary>Icons are stored as square images this many pixels wide.</summary>
        public const int Pixels = 48;

        private static readonly Dictionary<string, Image?> Cache = new Dictionary<string, Image?>();

        /// <summary>
        /// Loads an image file, crops it to a centered square, shrinks it and returns it as base64.
        /// Returns null if the file can't be read as an image or won't fit the size limit.
        /// </summary>
        public static string? Encode(string path)
        {
            try
            {
                using var source = new Bitmap(path);
                int side = Math.Min(source.Width, source.Height);
                var crop = new RectangleF((source.Width - side) / 2f, (source.Height - side) / 2f, side, side);

                using var small = new Bitmap(Pixels, Pixels, PixelFormat.Format32bppRgba);
                using (var g = new Graphics(small))
                {
                    g.ImageInterpolation = ImageInterpolation.High;
                    g.DrawImage(source, crop, new RectangleF(0, 0, Pixels, Pixels));
                }

                // PNG keeps transparency; a noisy photo can come out too big, so fall back to JPEG.
                foreach (var format in new[] { ImageFormat.Png, ImageFormat.Jpeg })
                {
                    string data = Convert.ToBase64String(small.ToByteArray(format));
                    if (data.Length <= LeaderboardClient.MaxIconChars)
                        return data;
                }
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Base64 image data as an image, or null if it is empty or not an image.</summary>
        public static Image? Decode(string base64)
        {
            if (string.IsNullOrEmpty(base64) || base64.Length > LeaderboardClient.MaxIconChars)
                return null;
            try
            {
                return new Bitmap(Convert.FromBase64String(base64));
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static bool TryGetCached(string playerId, string iconVersion, out Image? image) =>
            Cache.TryGetValue(playerId + ":" + iconVersion, out image);

        public static void Store(string playerId, string iconVersion, Image? image) =>
            Cache[playerId + ":" + iconVersion] = image;
    }

    /// <summary>A round player icon. Without an image it shows the first letter of the name on a color.</summary>
    internal sealed class AvatarView : Drawable
    {
        private readonly string _seed;
        private string _name;
        private Image? _image;

        /// <param name="seed">Picks the fallback color; use the player id so it stays the same.</param>
        public AvatarView(string name, string seed, int size, Image? image = null)
        {
            _name = name;
            _seed = seed;
            _image = image;
            Size = new Size(size, size);
        }

        public Image? Image
        {
            get => _image;
            set { _image = value; Invalidate(); }
        }

        public string PlayerName
        {
            get => _name;
            set { _name = value ?? ""; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var p = Palette.Current;
            g.AntiAlias = true;
            float w = Width - 1, h = Height - 1;

            if (_image != null)
            {
                var circle = new GraphicsPath();
                circle.AddEllipse(0, 0, w, h);
                g.SetClip(circle);
                g.ImageInterpolation = ImageInterpolation.High;
                g.DrawImage(_image, 0, 0, w, h);
                g.ResetClip();
                g.DrawEllipse(new Pen(p.Border, 1.5f), 0.75f, 0.75f, w - 1.5f, h - 1.5f);
                return;
            }

            var colors = new[] { p.Blue, p.Purple, p.Orange, p.Green, p.Gold, p.Red };
            int pick = 0;
            foreach (char c in _seed)
                pick = unchecked(pick * 31 + c);
            g.FillEllipse(colors[(pick & 0x7FFFFFFF) % colors.Length], 0, 0, w, h);

            string initial = _name.Trim().Length > 0 ? _name.Trim().Substring(0, 1).ToUpperInvariant() : "?";
            var font = new Font(SystemFont.Bold, Math.Max(7, Height * 0.34f));
            var size = font.MeasureString(initial);
            g.DrawText(font, Colors.White, (w - size.Width) / 2, (h - size.Height) / 2, initial);
        }
    }
}
