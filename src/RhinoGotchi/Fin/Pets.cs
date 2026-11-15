using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;

namespace RhinoGotchi.Fin
{
    /// <summary>How the rhino pet feels, which picks the picture and its animation.</summary>
    public enum PetMood
    {
        /// <summary>Playing along: the happy rhino, standing still, breathing.</summary>
        Awake,
        /// <summary>A level was solved (1 - winning streak).</summary>
        Win,
        /// <summary>No click for a while (2 - standby).</summary>
        Standby,
        /// <summary>Three misses on a level (3 - losing).</summary>
        Lose,
        /// <summary>Every level solved first try (4 - final).</summary>
        Final,
        /// <summary>The game is finished: the winning rhino dances under falling confetti.</summary>
        Dance,
    }

    /// <summary>
    /// The rhino pet, cut from the four RhinoGotchi pictures into layers (the rhino, and its
    /// ground line, Z's, ghost or sparkles) so each part can move on its own. Each mood is a
    /// short loop; <see cref="Draw"/> paints one moment of it with Eto, so the panel draws it
    /// directly and the viewport draws it from cached frames.
    /// </summary>
    internal static class Pet
    {
        /// <summary>One layer image: which picture, which part, the image size and where its pixels are.</summary>
        private readonly struct LayerInfo
        {
            public LayerInfo(int pet, int index, int width, int height, int x, int y, int w, int h)
            {
                Picture = pet;
                Index = index;
                Size = new SizeF(width, height);
                Box = new RectangleF(x, y, w, h);
            }

            public int Picture { get; }
            public int Index { get; }
            public SizeF Size { get; }
            public RectangleF Box { get; }
            public PointF Center => new PointF(Box.X + Box.Width / 2, Box.Y + Box.Height / 2);
            public PointF Foot => new PointF(Box.X + Box.Width / 2, Box.Bottom);
        }

        // Generated from the cut layers: image size, then the box of visible pixels.
        private static readonly LayerInfo[] Layers =
        {
            // 1 - winning streak: rhino, ground line, three motion ticks
            new LayerInfo(1, 0, 199, 229, 5, 5, 190, 169),
            new LayerInfo(1, 1, 199, 229, 55, 213, 83, 12),
            new LayerInfo(1, 2, 199, 229, 68, 189, 16, 16),
            new LayerInfo(1, 3, 199, 229, 107, 189, 16, 16),
            new LayerInfo(1, 4, 199, 229, 93, 182, 6, 17),
            // 2 - standby: rhino, big, middle and small Z
            new LayerInfo(2, 0, 205, 186, 5, 54, 185, 128),
            new LayerInfo(2, 1, 205, 186, 178, 5, 23, 28),
            new LayerInfo(2, 2, 205, 186, 157, 26, 18, 22),
            new LayerInfo(2, 3, 205, 186, 142, 47, 13, 15),
            // 3 - losing: rhino, ghost, two dashes, two ghost eyes
            new LayerInfo(3, 0, 215, 190, 5, 89, 206, 97),
            new LayerInfo(3, 1, 215, 190, 139, 21, 42, 69),
            new LayerInfo(3, 2, 215, 190, 185, 11, 15, 15),
            new LayerInfo(3, 3, 215, 190, 176, 5, 10, 13),
            new LayerInfo(3, 4, 215, 190, 153, 34, 6, 11),
            new LayerInfo(3, 5, 215, 190, 163, 34, 5, 11),
            // 4 - final: rhino, big star, left sparkle (4 pieces), right sparkle (4 pieces)
            new LayerInfo(4, 0, 199, 181, 5, 7, 190, 170),
            new LayerInfo(4, 1, 199, 181, 151, 5, 35, 30),
            new LayerInfo(4, 2, 199, 181, 39, 32, 6, 7),
            new LayerInfo(4, 3, 199, 181, 31, 39, 7, 6),
            new LayerInfo(4, 4, 199, 181, 45, 39, 7, 6),
            new LayerInfo(4, 5, 199, 181, 39, 45, 6, 7),
            new LayerInfo(4, 6, 199, 181, 173, 124, 7, 6),
            new LayerInfo(4, 7, 199, 181, 180, 118, 6, 6),
            new LayerInfo(4, 8, 199, 181, 186, 124, 7, 6),
            new LayerInfo(4, 9, 199, 181, 180, 130, 6, 7),
        };

        private static readonly Dictionary<(int, int), Bitmap?> _images = new Dictionary<(int, int), Bitmap?>();

        /// <summary>Seconds per loop of each mood. Frames are cached per loop, so these stay short.</summary>
        public static double LoopSeconds(PetMood mood) => mood switch
        {
            PetMood.Win => 0.8,
            PetMood.Dance => 2.4,
            PetMood.Lose => 3.0,
            PetMood.Final => 2.0,
            _ => 2.4,
        };

        /// <summary>Which picture a mood shows.</summary>
        private static int PictureOf(PetMood mood) => mood switch
        {
            PetMood.Standby => 2,
            PetMood.Lose => 3,
            PetMood.Final => 4,
            _ => 1,
        };

        /// <summary>Width / height of a mood's picture.</summary>
        public static float Aspect(PetMood mood)
        {
            LayerInfo first = Layers.First(l => l.Picture == PictureOf(mood));
            return first.Size.Width / first.Size.Height;
        }

        private static Bitmap? Image(int picture, int index)
        {
            if (_images.TryGetValue((picture, index), out Bitmap? cached))
                return cached;
            Bitmap? bitmap = null;
            try
            {
                using var stream = typeof(Pet).Assembly.GetManifestResourceStream($"RhinoGotchi.Fin.Pets.pet{picture}-{index}.png");
                if (stream != null)
                    bitmap = new Bitmap(stream);
            }
            catch
            {
                // A missing picture just isn't drawn
            }
            _images[(picture, index)] = bitmap;
            return bitmap;
        }

        /// <summary>
        /// Draws the pet in <paramref name="mood"/> at <paramref name="time"/> seconds, fitted into
        /// <paramref name="area"/> and standing on its bottom edge.
        /// </summary>
        public static void Draw(Graphics g, RectangleF area, PetMood mood, double time)
        {
            int picture = PictureOf(mood);
            LayerInfo[] layers = Layers.Where(l => l.Picture == picture).ToArray();
            SizeF size = layers[0].Size;
            float k = Math.Min(area.Width / size.Width, area.Height / size.Height);
            float ox = area.X + (area.Width - size.Width * k) / 2, oy = area.Bottom - size.Height * k;

            foreach (var (layer, motion) in Animate(mood, layers, time))
            {
                if (motion.Scale <= 0.02f && motion.ScaleY <= 0.02f)
                    continue;
                Bitmap? image = Image(picture, layer.Index);
                if (image == null)
                    continue;
                g.SaveTransform();
                g.TranslateTransform(ox, oy);
                g.ScaleTransform(k, k);
                g.TranslateTransform(motion.Anchor.X + motion.Dx, motion.Anchor.Y + motion.Dy);
                if (Math.Abs(motion.Rotate) > 0.01f)
                    g.RotateTransform(motion.Rotate);
                g.ScaleTransform(motion.Scale, motion.ScaleY);
                g.TranslateTransform(-motion.Anchor.X, -motion.Anchor.Y);
                g.DrawImage(image, 0, 0, size.Width, size.Height);
                g.RestoreTransform();
            }

            if (mood == PetMood.Dance)
                Confetti(g, area, time);
        }

        private static readonly Color[] ConfettiColors =
        {
            Color.FromArgb(0xFF, 0xD3, 0x4D), Color.FromArgb(0xE8, 0x81, 0x2C), Color.FromArgb(0x2A, 0x5C, 0xB8),
            Color.FromArgb(0x12, 0x12, 0x12), Color.FromArgb(0x1E, 0x7A, 0x3A),
        };

        /// <summary>
        /// Paper confetti falling, tumbling and swaying across <paramref name="area"/>. Each piece falls a
        /// whole number of times per dance loop, so cached frames loop seamlessly.
        /// </summary>
        private static void Confetti(Graphics g, RectangleF area, double time)
        {
            const int pieces = 22;
            double loop = LoopSeconds(PetMood.Dance);
            float unit = Math.Max(2f, area.Width / 60f);
            var random = new Random(11);
            for (int i = 0; i < pieces; i++)
            {
                double x0 = random.NextDouble(), offset = random.NextDouble();
                int falls = 1 + random.Next(2);
                double sway = random.NextDouble() * Math.PI * 2;
                Color color = ConfettiColors[i % ConfettiColors.Length];

                double p = (time / loop * falls + offset) % 1;
                float x = area.X + (float)(x0 * area.Width + Math.Sin(p * Math.PI * 4 + sway) * unit * 2);
                float y = area.Y - unit * 2 + (float)(p * (area.Height + unit * 4));
                float w = unit * 1.6f, h = unit * (0.6f + 0.9f * (float)Math.Abs(Math.Cos(p * Math.PI * 6 + sway))); // tumbling

                g.SaveTransform();
                g.TranslateTransform(x, y);
                g.RotateTransform((float)(p * 360 * falls + sway * 57));
                g.FillRectangle(color, -w / 2, -h / 2, w, h);
                g.RestoreTransform();
            }
        }

        /// <summary>How one layer is moved at one moment: offset, scale about an anchor, and a turn in degrees.</summary>
        private struct Motion
        {
            public float Dx, Dy, Scale, ScaleY, Rotate;
            public PointF Anchor;
        }

        private static Motion Still(LayerInfo l) => new Motion { Scale = 1, ScaleY = 1, Anchor = l.Center };

        private static float Tri(double x) => (float)(1 - Math.Abs((((x % 1) + 1) % 1) * 2 - 1)); // 0..1..0

        private static IEnumerable<(LayerInfo, Motion)> Animate(PetMood mood, LayerInfo[] l, double t)
        {
            LayerInfo rhino = l[0];
            switch (mood)
            {
                case PetMood.Awake:
                {
                    // Just the rhino, breathing gently
                    float breathe = (float)Math.Sin(t * Math.PI * 2 / 2.4);
                    yield return (rhino, new Motion { Scale = 1, ScaleY = 1 + 0.02f * breathe, Anchor = rhino.Foot });
                    break;
                }

                case PetMood.Win:
                {
                    // Hop: the ground line shrinks as it rises, the ticks flash when it lands
                    double u = t % 0.8 / 0.8;
                    float air = (float)Math.Sin(Math.PI * Math.Min(1, u / 0.75));
                    bool landed = u > 0.75;
                    yield return (l[1], new Motion { Scale = landed ? 1.08f : 1 - 0.25f * air, ScaleY = 1, Anchor = l[1].Center });
                    bool ticks = landed || air < 0.35f;
                    for (int i = 2; i < l.Length; i++)
                        yield return (l[i], new Motion { Scale = ticks ? 1 : 0, ScaleY = ticks ? 1 : 0, Anchor = l[i].Center });
                    yield return (rhino, new Motion { Dy = -38 * air, Scale = landed ? 1.06f : 1, ScaleY = landed ? 0.93f : 1, Anchor = rhino.Foot });
                    break;
                }

                case PetMood.Standby:
                {
                    // Slow breathing; the Z's float up one after another, growing, then pop
                    float breathe = (float)Math.Sin(t * Math.PI * 2 / 2.4);
                    yield return (rhino, new Motion { Scale = 1, ScaleY = 1 + 0.025f * breathe, Anchor = rhino.Foot });
                    for (int i = 1; i < l.Length; i++)
                    {
                        int order = l.Length - 1 - i; // the small Z first
                        double p = (t / 2.4 + order / 3.0) % 1;
                        float grow = (float)(p < 0.85 ? 0.6 + 0.6 * p : (1 - p) / 0.15 * 1.1);
                        yield return (l[i], new Motion { Dx = (float)(10 * p), Dy = (float)(-40 * p), Scale = grow, ScaleY = grow, Anchor = l[i].Center });
                    }
                    break;
                }

                case PetMood.Lose:
                {
                    // The rhino shivers once; the ghost (with its eyes and dashes) rises, sways and goes
                    double p = t % 3 / 3;
                    yield return (rhino, new Motion { Dx = (float)(Math.Sin(t * 40) * (p < 0.15 ? 2 : 0)), Scale = 1, ScaleY = 1, Anchor = rhino.Center });
                    float size = (float)Math.Min(1, Math.Min(p * 5, (1 - p) * 3));
                    var ghost = new PointF(l[1].Center.X, l[1].Box.Bottom);
                    for (int i = 1; i < l.Length; i++)
                        yield return (l[i], new Motion { Dx = (float)(8 * Math.Sin(t * 3)), Dy = (float)(-30 * p), Scale = size, ScaleY = size, Anchor = ghost });
                    break;
                }

                case PetMood.Dance:
                {
                    // Eight beats: hop-hop side to side, turn around, hop-hop, a big spinning jump, land.
                    double beats = t % 2.4 / 0.3;
                    int b = (int)beats;
                    double p = beats - b;
                    float arc = (float)Math.Sin(Math.PI * p);
                    bool big = b == 6;
                    float facing = b < 4 ? 1 : -1;
                    float spin = big ? (float)Math.Cos(2 * Math.PI * p) : 1;
                    bool landing = p > 0.85;
                    float side = b % 2 == 0 ? -1 : 1;

                    // The ground line shrinks as the rhino rises; the ticks flash on every landing
                    yield return (l[1], new Motion { Scale = 1 - (big ? 0.45f : 0.25f) * arc, ScaleY = 1, Anchor = l[1].Center });
                    bool ticks = p < 0.15 || landing;
                    for (int i = 2; i < l.Length; i++)
                        yield return (l[i], new Motion { Scale = ticks ? 1 : 0, ScaleY = ticks ? 1 : 0, Anchor = l[i].Center });

                    yield return (rhino, new Motion
                    {
                        Dx = 12 * side * arc,
                        Dy = -(big ? 60 : 28) * arc,
                        Rotate = 9 * side * arc,
                        Scale = facing * spin * (landing ? 1.05f : 1),
                        ScaleY = landing ? 0.92f : 1,
                        Anchor = rhino.Foot,
                    });
                    break;
                }

                case PetMood.Final:
                {
                    // A happy bounce with a wiggle; the big star pulses and spins; the sparkles twinkle in turn
                    yield return (rhino, new Motion { Dy = -14 * Tri(t / 0.5), Rotate = (float)(4 * Math.Sin(t * Math.PI * 2)), Scale = 1, ScaleY = 1, Anchor = rhino.Foot });
                    float star = 0.85f + 0.25f * Tri(t / 0.5);
                    yield return (l[1], new Motion { Rotate = (float)(t * 45 % 360), Scale = star, ScaleY = star, Anchor = l[1].Center });
                    RectangleF left = l.Skip(2).Take(4).Select(x => x.Box).Aggregate(RectangleF.Union);
                    RectangleF right = l.Skip(6).Take(4).Select(x => x.Box).Aggregate(RectangleF.Union);
                    for (int i = 2; i < l.Length; i++)
                    {
                        bool isLeft = i < 6;
                        float tw = 0.4f + 0.8f * Tri(t / 1.0 + (isLeft ? 0 : 0.5));
                        RectangleF group = isLeft ? left : right;
                        yield return (l[i], new Motion { Scale = tw, ScaleY = tw, Anchor = new PointF(group.X + group.Width / 2, group.Y + group.Height / 2) });
                    }
                    break;
                }
            }
        }
    }
}
