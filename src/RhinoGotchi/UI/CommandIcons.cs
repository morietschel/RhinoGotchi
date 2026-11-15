using System;
using System.Collections.Generic;
using System.IO;
using Eto.Drawing;
using Rhino;
using Rhino.Runtime;
using Rhino.UI;
using RhinoGotchi.Content;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// Renders command icons from the SVGs copied next to the plugin (Icons/*.svg) and caches them.
    /// Rhino recolors them for dark mode.
    /// </summary>
    internal static class CommandIcons
    {
        private static readonly Dictionary<string, Image?> Cache = new Dictionary<string, Image?>();
        private static readonly string Folder = Path.Combine(
            Path.GetDirectoryName(typeof(CommandIcons).Assembly.Location) ?? "", "Icons");

        public static Image? Get(CommandEntry entry, int size) => GetFile(entry.IconFile, size);

        /// <summary>Renders an icon by file name (without .svg), or null if there is no such icon.</summary>
        public static Image? GetFile(string iconFile, int size)
        {
            string key = iconFile + "@" + size;
            if (Cache.TryGetValue(key, out var cached))
                return cached;

            Image? image = null;
            try
            {
                string path = Path.Combine(Folder, iconFile + ".svg");
                if (File.Exists(path))
                {
                    var bitmap = DrawingUtilities.BitmapFromSvg(File.ReadAllText(path), size, size, false); // the panel is always light
                    image = bitmap?.ToEto();
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine("RhinoGotchi: could not load icon {0} ({1}).", iconFile, ex.Message);
            }
            Cache[key] = image;
            return image;
        }
    }
}
