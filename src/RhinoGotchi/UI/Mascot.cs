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
        Leaf,
        Branch,
        Twig,
        Apple,
        Banana,
        Watermelon,
    }

    /// <summary>
    /// Gotchi, the 1-bit pixel rhino, built the Wardrobe Lab way: every frame is the pose plus the
    /// outfit (one face item, one hat, one clothing item) drawn at the pose's placement points and
    /// moved by the frame's body offset, so the outfit stays on through hops and the eating animation.
    /// Layer order: pose, jeans, jacket, nails, ninja mask, outlines with LCD shadow, ninja tails,
    /// bow + pearls, face, chain, hat, near ear, then the food.
    /// </summary>
    internal static partial class Mascot
    {
        private static readonly Dictionary<string, Bitmap?> Images = new Dictionary<string, Bitmap?>();
        private static readonly Color InkColor = Color.FromArgb(0x12, 0x12, 0x12);
        private static readonly Color ShadowColor = Color.FromArgb(0, 0, 0, 102);

        private sealed class Pose
        {
            public string Name = "";
            public float Cell;
            public (float X, float Y) Eye, Neck, BowAt, NinjaTails;
            public float HatX, HatY, HatCell;
            public bool HasEar;
        }

        private sealed class GridItem
        {
            public string[] Grid = new string[0];
            public Dictionary<char, int> Palette = new Dictionary<char, int>();
            /// <summary>Characters drawn as outline (with the LCD shadow).</summary>
            public string Ink = "K";
            public (float X, float Y) Anchor;
            public int Width => Grid.Length == 0 ? 0 : Grid[0].Length;
        }

        /// <summary>The outfit to draw when a caller doesn't pick one: whatever the player put on.</summary>
        public static Func<Hat> WornHat { get; set; } = () => Hat.None;
        public static Func<Clothes> WornClothes { get; set; } = () => Clothes.None;
        public static Func<Face> WornFace { get; set; } = () => Face.None;

        /// <summary>Number of frames in an eating animation (120 ms each).</summary>
        public const int EatFrames = 12;

        /// <param name="bob">Vertical hop in box units of 100 (negative = up).</param>
        /// <param name="breathe">0..1, a slow idle squash so Gotchi looks alive.</param>
        public static void Draw(Graphics g, RectangleF rect, Mood mood, float bob = 0, Hat? hat = null, float breathe = 0,
            Clothes? clothes = null, Face? face = null)
        {
            string pose = mood switch
            {
                Mood.Celebrate => "celebrate",
                Mood.Sleepy => "sleep",
                Mood.Sad => "faint",
                _ => "idle",
            };
            Render(g, rect, pose, 0, bob, breathe, hat ?? WornHat(), clothes ?? WornClothes(), face ?? WornFace(), null, 0);
        }

        /// <summary>One frame of "feeding time": Gotchi, in its outfit, catches and eats a food.</summary>
        public static void DrawEating(Graphics g, RectangleF rect, Food food, int frame, Hat? hat = null, Clothes? clothes = null, Face? face = null)
        {
            frame = Math.Max(0, Math.Min(EatFrames - 1, frame));
            var (pose, dy) = EatTimeline[frame];
            Render(g, rect, pose, dy, 0, 0, hat ?? WornHat(), clothes ?? WornClothes(), face ?? WornFace(), food, frame);
        }

        private static void Render(Graphics g, RectangleF rect, string poseName, float dy, float bob, float breathe,
            Hat hat, Clothes clothes, Face face, Food? food, int frame)
        {
            var P = Poses[poseName];
            float size = Math.Min(rect.Width, rect.Height);
            float k = size / (WinX1 - WinX0); // display px per image px
            float left = rect.X + (rect.Width - size) / 2, top = rect.Y + (rect.Height - size) / 2;
            float ox = left - WinX0 * k;
            float oy = top - WinY0 * k + bob * size / 100f;

            g.SaveTransform();
            g.ImageInterpolation = ImageInterpolation.None;
            g.AntiAlias = false;
            if (breathe != 0)
            {
                float feet = top + size;
                g.TranslateTransform(0, feet);
                g.ScaleTransform(1, 1 + 0.025f * breathe);
                g.TranslateTransform(0, -feet);
            }

            void Image(string layer, float dx = 0, float sy = 0)
            {
                var img = Load($"{poseName}_{layer}");
                if (img != null)
                    g.DrawImage(img, new RectangleF(0, 0, img.Width, img.Height), new RectangleF(ox + dx * k, oy + (dy + sy) * k, ImageSize * k, ImageSize * k));
            }

            // The idle pose keeps its ground marks still while the body moves.
            void PoseLayer(string layer, float shadow = 0)
            {
                var img = Load($"{poseName}_{layer}");
                if (img == null)
                    return;
                if (poseName == "idle" && dy != 0)
                {
                    g.DrawImage(img, new RectangleF(0, 0, ImageSize, SplitY), new RectangleF(ox + shadow * k, oy + (dy + shadow) * k, ImageSize * k, SplitY * k));
                    g.DrawImage(img, new RectangleF(0, SplitY, ImageSize, ImageSize - SplitY), new RectangleF(ox + shadow * k, oy + (SplitY + shadow) * k, ImageSize * k, (ImageSize - SplitY) * k));
                }
                else
                    Image(layer, shadow, shadow);
            }

            void Grid(GridItem item, float x, float y, float cell)
            {
                float X(float c) => ox + (x + c * cell) * k;
                float Y(float r) => oy + (y + r * cell) * k;
                float shadow = ShadowCells * cell * k;
                // Fill, then the outline's LCD shadow, then the outline.
                for (int pass = 0; pass < 3; pass++)
                {
                    for (int r = 0; r < item.Grid.Length; r++)
                    {
                        string row = item.Grid[r];
                        int c0 = 0;
                        while (c0 < row.Length)
                        {
                            char ch = row[c0];
                            int c1 = c0;
                            while (c1 < row.Length && row[c1] == ch) c1++;
                            if (ch != '.')
                            {
                                bool ink = item.Ink.IndexOf(ch) >= 0;
                                if ((pass == 0 && !ink) || (pass > 0 && ink))
                                {
                                    Color color = pass == 1 ? ShadowColor
                                        : ch == 'K' ? InkColor
                                        : item.Palette.TryGetValue(ch, out int rgb) ? Rgb(rgb) : InkColor;
                                    float off = pass == 1 ? shadow : 0;
                                    g.FillRectangle(color, X(c0) + off, Y(r) + off, (c1 - c0) * cell * k + 0.6f, cell * k + 0.6f);
                                }
                            }
                            c0 = c1;
                        }
                    }
                }
            }

            float cellPx = P.Cell;
            PoseLayer("fill");
            if (clothes == Clothes.JeansA) Image("jeansA");
            if (clothes == Clothes.JeansB) Image("jeansB");
            if (clothes == Clothes.Jacket) Image("jacket");
            if (clothes == Clothes.Bow) Image("nails");
            if (face == Face.Ninja) Image("ninja");
            PoseLayer("inkShadow", ShadowCells * cellPx);
            PoseLayer("ink");
            if (face == Face.Ninja)
                Grid(NinjaTails, P.NinjaTails.X - NinjaTails.Width * cellPx + cellPx, P.NinjaTails.Y - 3 * cellPx + dy, cellPx);
            if (clothes == Clothes.Bow)
            {
                Grid(Bow, P.BowAt.X - Bow.Anchor.X * cellPx, P.BowAt.Y - Bow.Anchor.Y * cellPx + dy, cellPx);
                Grid(Pearls, P.Neck.X - Pearls.Width * cellPx / 2, P.Neck.Y + dy, cellPx);
            }
            if (face != Face.None && face != Face.Ninja && FaceGrids.TryGetValue(face, out var f))
                Grid(f, P.Eye.X - f.Anchor.X * cellPx, P.Eye.Y - f.Anchor.Y * cellPx + dy, cellPx);
            if (clothes == Clothes.BigChain)
                Grid(BigChain, P.Neck.X - BigChain.Width * cellPx / 2, P.Neck.Y - cellPx + dy, cellPx);
            if (hat != Hat.None && HatGrids.TryGetValue(hat, out var h))
            {
                Grid(h, P.HatX - h.Width * P.HatCell / 2, P.HatY - h.Grid.Length * P.HatCell + dy, P.HatCell);
                if (P.HasEar)
                {
                    // The near ear goes over the hat brim.
                    Image("earFill");
                    Image("earInkShadow", ShadowCells * cellPx, ShadowCells * cellPx);
                    Image("earInk");
                }
            }

            if (food is Food fd && Load("food_" + fd.ToString().ToLowerInvariant()) is Bitmap scene)
                g.DrawImage(scene, new RectangleF(frame * SceneSize, 0, SceneSize, SceneSize), new RectangleF(left, top, size, size));

            g.RestoreTransform();
            g.AntiAlias = true;
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
