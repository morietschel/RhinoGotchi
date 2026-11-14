using System;
using System.Collections.Generic;
using System.IO;
using Eto.Drawing;
using RhinoGotchi.Lessons;

namespace RhinoGotchi.UI
{
    internal enum Mood
    {
        Neutral,
        Happy,
        Sad,
        /// <summary>Hop with sparkles: a win.</summary>
        Celebrate,
        /// <summary>Curled up asleep: nothing played today yet.</summary>
        Sleepy,
        /// <summary>Waiting for an answer.</summary>
        Thinking,
    }

    internal enum Food
    {
        Grass,
        TreeLeaf,
        ShrubBranch,
        Twig,
        Apple,
        Banana,
        Watermelon,
    }

    /// <summary>
    /// Gotchi, the 1-bit pixel rhino from the RhinoGotchi canvas. Four poses (idle, celebrate, sleep,
    /// faint) drawn from PNG layers, with a pixel hat on the pose's anchor, clothes cut from the
    /// clothes sheet, and an LCD shadow under every black pixel. Also plays the 12-frame eating
    /// animations shown after a solve.
    /// </summary>
    internal static partial class Mascot
    {
        // The canvas shows a 1120px window of each 1254px pose image.
        private const float WinX = 140, WinY = -20, WinSize = 1120, ImageSize = 1254;
        // Clothes sheet cells (320px) map onto the pose images with this scale and offset.
        private const float SheetScale = 0.3509f, SheetOffset = -63.6f, SheetCell = 320;

        private static readonly Dictionary<string, Bitmap?> Images = new Dictionary<string, Bitmap?>();

        private sealed class Pose
        {
            public string Name = "";
            public float AnchorX, AnchorY, Cell, ShadowCell;
            public bool HasEar;
        }

        private static readonly Pose Idle = new Pose { Name = "idle", AnchorX = 706, AnchorY = 356, Cell = 27, ShadowCell = 27, HasEar = true };
        private static readonly Pose Celebrate = new Pose { Name = "celebrate", AnchorX = 708, AnchorY = 359, Cell = 27, ShadowCell = 27, HasEar = true };
        private static readonly Pose Sleep = new Pose { Name = "sleep", AnchorX = 710, AnchorY = 610, Cell = 27, ShadowCell = 27, HasEar = true };
        private static readonly Pose Faint = new Pose { Name = "faint", AnchorX = 830, AnchorY = 312, Cell = 11, ShadowCell = 21, HasEar = false };

        /// <summary>The hat to draw when a caller doesn't pick one: whatever the player put on.</summary>
        public static Func<Hat> WornHat { get; set; } = () => Hat.None;

        /// <summary>The clothes Gotchi is wearing.</summary>
        public static Func<Clothes> WornClothes { get; set; } = () => Clothes.None;

        /// <param name="bob">Vertical hop in box units of 100 (negative = up).</param>
        /// <param name="hat">Hat to wear; null wears <see cref="WornHat"/>.</param>
        /// <param name="breathe">0..1, a slow idle squash so Gotchi looks alive.</param>
        /// <param name="clothes">Clothes to wear; null wears <see cref="WornClothes"/>.</param>
        public static void Draw(Graphics g, RectangleF rect, Mood mood, float bob = 0, Hat? hat = null, float breathe = 0, Clothes? clothes = null)
        {
            Pose pose = mood switch
            {
                Mood.Celebrate => Celebrate,
                Mood.Sleepy => Sleep,
                Mood.Sad => Faint,
                _ => Idle,
            };
            Hat h = hat ?? WornHat();
            Clothes c = clothes ?? WornClothes();

            float size = Math.Min(rect.Width, rect.Height);
            float k = size / WinSize; // display px per image px
            float ox = rect.X + (rect.Width - size) / 2 - WinX * k;
            float oy = rect.Y + (rect.Height - size) / 2 - WinY * k + bob * size / 100f;

            g.SaveTransform();
            g.ImageInterpolation = ImageInterpolation.None;
            g.AntiAlias = false;
            if (breathe != 0)
            {
                // Squash from the feet up.
                float feet = rect.Y + (rect.Height + size) / 2;
                g.TranslateTransform(0, feet);
                g.ScaleTransform(1, 1 + 0.025f * breathe);
                g.TranslateTransform(0, -feet);
            }

            void Layer(string name, float dx = 0, float dy = 0)
            {
                var img = Load(name);
                if (img != null)
                    g.DrawImage(img, new RectangleF(0, 0, img.Width, img.Height), new RectangleF(ox + dx * k, oy + dy * k, ImageSize * k, ImageSize * k));
            }

            void Garment(string name)
            {
                var img = Load($"clothes_{name}_{pose.Name}");
                if (img == null)
                    return;
                float s = 1 / SheetScale;
                g.DrawImage(img, new RectangleF(0, 0, img.Width, img.Height),
                    new RectangleF(ox - SheetOffset * s * k, oy - SheetOffset * s * k, SheetCell * s * k, SheetCell * s * k));
            }

            float shadow = 0.35f * pose.ShadowCell;
            Layer($"{pose.Name}_fill");
            if (c.HasFlag(Clothes.Jeans)) Garment("jeans");
            if (c.HasFlag(Clothes.Tracksuit)) Garment("tracksuit");
            else if (c.HasFlag(Clothes.Bomber)) Garment("bomber");
            Layer($"{pose.Name}_inkShadow", shadow, shadow);
            Layer($"{pose.Name}_ink");
            if (c.HasFlag(Clothes.Chain)) Garment("chain");

            if (h != Hat.None && HatSprites.TryGetValue(h, out var sprite))
            {
                float cell = pose.Cell * k;
                float left = ox + (pose.AnchorX - sprite.W * pose.Cell / 2f) * k;
                float top = oy + (pose.AnchorY - sprite.H * pose.Cell) * k;
                var shadowColor = Color.FromArgb(0, 0, 0, 102);
                foreach (var r in sprite.Rects)
                    if (r.Shadow)
                        g.FillRectangle(shadowColor, left + (r.X + 0.35f) * cell, top + (r.Y + 0.35f) * cell, r.W * cell, r.H * cell);
                foreach (var r in sprite.Rects)
                    g.FillRectangle(Rgb(r.Rgb), left + r.X * cell, top + r.Y * cell, r.W * cell + 0.5f, r.H * cell + 0.5f);
                // The near ear goes over the hat brim.
                if (pose.HasEar)
                {
                    Layer($"{pose.Name}_earFill");
                    Layer($"{pose.Name}_earInkShadow", shadow, shadow);
                    Layer($"{pose.Name}_earInk");
                }
            }

            g.RestoreTransform();
            g.AntiAlias = true;
            g.ImageInterpolation = ImageInterpolation.Default;
        }

        /// <summary>Number of frames in an eating animation (120 ms each).</summary>
        public const int EatFrames = 12;

        /// <summary>One frame of the "feeding time" animation: Gotchi catches and eats a food.</summary>
        public static void DrawEating(Graphics g, RectangleF rect, Food food, int frame)
        {
            var strip = Load("eat_" + food.ToString().ToLowerInvariant());
            if (strip == null)
            {
                Draw(g, rect, Mood.Celebrate);
                return;
            }
            float fw = strip.Width / (float)EatFrames;
            float size = Math.Min(rect.Width, rect.Height);
            g.ImageInterpolation = ImageInterpolation.None;
            g.DrawImage(strip, new RectangleF(Math.Max(0, Math.Min(EatFrames - 1, frame)) * fw, 0, fw, strip.Height),
                new RectangleF(rect.X + (rect.Width - size) / 2, rect.Y + (rect.Height - size) / 2, size, size));
            g.ImageInterpolation = ImageInterpolation.Default;
        }

        private static Color Rgb(int rgb) => Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);

        private static Bitmap? Load(string name)
        {
            if (Images.TryGetValue(name, out var cached))
                return cached;
            Bitmap? bmp = null;
            try
            {
                using Stream? s = typeof(Mascot).Assembly.GetManifestResourceStream("RhinoGotchi.Pet." + name + ".png");
                if (s != null)
                    bmp = new Bitmap(s);
            }
            catch (Exception)
            {
                bmp = null;
            }
            Images[name] = bmp;
            return bmp;
        }
    }
}
