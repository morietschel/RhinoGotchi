using System;
using System.Collections.Generic;
using System.IO;
using Eto.Drawing;
using Rhino.Display;

namespace GotchiFins
{
    /// <summary>
    /// Viewport overlays as bitmaps. Each card or pet frame is drawn once with Eto (so it looks
    /// exactly like the panel, with rounded corners and the RhinoGotchi fonts), turned into a
    /// DisplayBitmap, and cached under a key that changes when its content does.
    /// </summary>
    internal static class Hud
    {
        private const int MaxCached = 160;

        private static readonly Dictionary<string, (DisplayBitmap Bitmap, int Width, int Height)> _cache =
            new Dictionary<string, (DisplayBitmap, int, int)>();

        /// <summary>
        /// The cached bitmap for <paramref name="key"/>, drawing it with <paramref name="draw"/> on a
        /// canvas of at most <paramref name="maxWidth"/> × <paramref name="maxHeight"/> pixels if needed.
        /// <paramref name="draw"/> returns the size it actually used; the bitmap is cropped to it.
        /// </summary>
        public static (DisplayBitmap Bitmap, int Width, int Height)? Get(string key, int maxWidth, int maxHeight, Func<Graphics, SizeF> draw)
        {
            if (_cache.TryGetValue(key, out var hit))
                return hit;
            try
            {
                if (_cache.Count > MaxCached)
                    Clear();

                using var canvas = new Bitmap(maxWidth, maxHeight, PixelFormat.Format32bppRgba);
                SizeF used;
                using (var g = new Graphics(canvas))
                {
                    g.AntiAlias = true;
                    g.Clear(Colors.Transparent);
                    used = draw(g);
                }
                int w = Math.Max(1, Math.Min(maxWidth, (int)Math.Ceiling(used.Width)));
                int h = Math.Max(1, Math.Min(maxHeight, (int)Math.Ceiling(used.Height)));
                using Bitmap cropped = canvas.Clone(new Rectangle(0, 0, w, h));

                // Eto → PNG → System.Drawing → DisplayBitmap keeps the transparency on every platform
                byte[] png = cropped.ToByteArray(ImageFormat.Png);
                using var stream = new MemoryStream(png);
                using var sd = new System.Drawing.Bitmap(stream);
                var display = new DisplayBitmap(sd);
                var entry = (display, w, h);
                _cache[key] = entry;
                return entry;
            }
            catch
            {
                return null; // a card that can't be drawn is simply left out
            }
        }

        public static void Clear()
        {
            foreach (var entry in _cache.Values)
                entry.Bitmap.Dispose();
            _cache.Clear();
        }
    }
}
