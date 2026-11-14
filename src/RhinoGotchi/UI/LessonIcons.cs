using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;
using Rhino.Geometry;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// Hand-drawn vector icons for Rhino commands, so the quiz needs no image files.
    /// Each icon is a tiny isometric scene: blue for the result, orange for rails
    /// and paths, purple for profiles. Drawn in a 100x100 box, then scaled.
    /// </summary>
    public static partial class LessonIcons
    {
        public static readonly Color Rail = Color.FromArgb(0xFF, 0x96, 0x00);
        public static readonly Color Profile = Color.FromArgb(0xCE, 0x82, 0xFF);
        public static readonly Color SurfLight = Color.FromArgb(0xE3, 0xF6, 0xFF);
        public static readonly Color SurfDark = Color.FromArgb(0x5C, 0xC6, 0xF8);
        public static readonly Color Edge = Color.FromArgb(0x18, 0x99, 0xD6);
        public static readonly Color Ink = Color.FromArgb(0x4B, 0x4B, 0x4B);
        public static readonly Color Grid = Color.FromArgb(0xE5, 0xE5, 0xE5);

        private static readonly Vector3d Light = Unit(new Vector3d(0.3, 0.6, 1.0));
        private const float S = 22f;      // iso scale: one model unit in icon pixels
        private const float Ox = 50f;     // iso origin in the 100x100 box
        private const float Oy = 54f;

        static LessonIcons()
        {
            RegisterMore(Painters);
        }

        public static bool Has(string command) => Painters.ContainsKey(command);

        /// <summary>Draws the icon for <paramref name="command"/> inside <paramref name="rect"/>.</summary>
        public static void Draw(Graphics g, string command, RectangleF rect)
        {
            float size = Math.Min(rect.Width, rect.Height);
            g.SaveTransform();
            g.TranslateTransform(rect.X + (rect.Width - size) / 2, rect.Y + (rect.Height - size) / 2);
            g.ScaleTransform(size / 100f);
            g.AntiAlias = true;
            if (Painters.TryGetValue(command, out Action<Graphics>? paint))
                paint(g);
            else
                Unknown(g);
            g.RestoreTransform();
        }

        private static readonly Dictionary<string, Action<Graphics>> Painters = new Dictionary<string, Action<Graphics>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Box"] = DrawBox,
            ["Sphere"] = DrawSphere,
            ["Cylinder"] = DrawCylinder,
            ["Cone"] = DrawCone,
            ["Loft"] = DrawLoft,
            ["Sweep1"] = DrawSweep1,
            ["Sweep2"] = DrawSweep2,
            ["Revolve"] = DrawRevolve,
            ["ExtrudeCrv"] = DrawExtrudeCrv,
            ["Pipe"] = DrawPipe,
            ["NetworkSrf"] = DrawNetworkSrf,
            ["FilletEdge"] = DrawFilletEdge,
            ["BooleanUnion"] = DrawBooleanUnion,
            ["BooleanDifference"] = DrawBooleanDifference,
            ["Polyline"] = DrawPolyline,
            ["Circle"] = DrawCircle,
            ["Line"] = DrawLine,
            ["Rectangle"] = DrawRectangle,
        };

        // ------------------------------------------------------------------ solids

        private static void DrawBox(Graphics g)
        {
            var scene = new Scene();
            scene.Box(new Point3d(-1, -1, -0.7), new Point3d(1, 1, 0.7));
            scene.Flush(g);
            // The two corners of Box > Diagonal.
            Dot(g, Iso(-1, -1, -0.7), Rail, 4.5f);
            Dot(g, Iso(1, 1, 0.7), Rail, 4.5f);
            DashedLine(g, Iso(-1, -1, -0.7), Iso(1, 1, 0.7), Rail);
        }

        private static void DrawSphere(Graphics g)
        {
            var scene = new Scene();
            scene.Surface((u, v) =>
            {
                double a = u * Math.PI * 2, b = (v - 0.5) * Math.PI;
                return new Point3d(Math.Cos(a) * Math.Cos(b) * 1.15, Math.Sin(a) * Math.Cos(b) * 1.15, Math.Sin(b) * 1.15 + 0.1);
            }, 24, 12);
            scene.Flush(g);
            Polyline3(g, t => new Point3d(Math.Cos(t * Math.PI * 2) * 1.15, Math.Sin(t * Math.PI * 2) * 1.15, 0.1), Edge, 1.6f);
        }

        private static void DrawCylinder(Graphics g)
        {
            var scene = new Scene();
            scene.Surface((u, v) => new Point3d(Math.Cos(u * Math.PI * 2) * 0.9, Math.Sin(u * Math.PI * 2) * 0.9, -0.9 + v * 1.8), 24, 2);
            scene.Disc(new Point3d(0, 0, 0.9), 0.9);
            scene.Flush(g);
            Polyline3(g, t => new Point3d(Math.Cos(t * Math.PI * 2) * 0.9, Math.Sin(t * Math.PI * 2) * 0.9, 0.9), Edge, 1.6f);
        }

        private static void DrawCone(Graphics g)
        {
            var scene = new Scene();
            scene.Surface((u, v) =>
            {
                double r = 1.0 * (1 - v);
                return new Point3d(Math.Cos(u * Math.PI * 2) * r, Math.Sin(u * Math.PI * 2) * r, -0.9 + v * 2.0);
            }, 24, 4);
            scene.Flush(g);
            Polyline3(g, t => new Point3d(Math.Cos(t * Math.PI * 2), Math.Sin(t * Math.PI * 2), -0.9), Edge, 1.6f);
        }

        // ------------------------------------------------------------------ surfaces

        private static void DrawLoft(Graphics g)
        {
            // Three different wavy profiles in a row; the skin passes through all of them.
            Func<double, double, Point3d> profile = (k, t) =>
            {
                double x = -1.1 + t * 2.2;
                double amp = k switch { 0 => 0.25, 1 => 0.55, _ => 0.2 };
                double phase = k * 1.3;
                return new Point3d(x, -1.1 + k * 1.1, Math.Sin(t * Math.PI * 2 + phase) * amp);
            };
            var scene = new Scene();
            scene.Surface((u, v) =>
            {
                double k = v * 2;
                int k0 = Math.Min(1, (int)k);
                double f = k - k0;
                Point3d a = profile(k0, u), b = profile(k0 + 1, u);
                f = f * f * (3 - 2 * f); // smooth blend between neighbors
                return a + (b - a) * f;
            }, 18, 12);
            scene.Flush(g);
            for (int k = 0; k < 3; k++)
            {
                int kk = k;
                Polyline3(g, t => profile(kk, t), Profile, 3.2f);
            }
        }

        private static void DrawSweep1(Graphics g)
        {
            // One rail (orange) curving across the floor, a U-shaped profile (purple) carried along it.
            Func<double, Point3d> rail = t => new Point3d(-1.2 + t * 2.4, Math.Sin(t * Math.PI) * 0.9 - 0.45, -0.5);
            Func<double, Point3d, Point3d> section = (t, s) =>
            {
                Point3d c = rail(t);
                Vector3d tan = Unit(rail(Math.Min(1, t + 0.01)) - rail(Math.Max(0, t - 0.01)));
                Vector3d side = Unit(Vector3d.CrossProduct(Vector3d.ZAxis, tan));
                return c + side * s.X + Vector3d.ZAxis * s.Y;
            };
            // U-shaped profile: X across the rail, Y up.
            Func<double, Point3d> u = a => new Point3d(Math.Cos(Math.PI * (1 - a)) * 0.45, (1 - Math.Sin(Math.PI * a)) * 0.9, 0);
            var scene = new Scene();
            scene.Surface((a, t) => section(t, u(a)), 10, 18);
            scene.Flush(g);
            Polyline3(g, rail, Rail, 3.4f);
            Polyline3(g, a => section(0, u(a)), Profile, 3.2f);
        }

        private static void DrawSweep2(Graphics g)
        {
            // Two rails (orange) with different shapes; arched profiles (purple) span between them.
            Func<double, Point3d> railA = t => new Point3d(-1.2 + t * 2.4, -0.95, -0.5 + Math.Sin(t * Math.PI) * 0.25);
            Func<double, Point3d> railB = t => new Point3d(-1.2 + t * 2.4, 0.95 - Math.Sin(t * Math.PI) * 0.45, -0.5);
            Func<double, double, Point3d> surf = (s, t) =>
            {
                Point3d a = railA(t), b = railB(t);
                Point3d p = a + (b - a) * s;
                double h = 0.55 + 0.35 * Math.Cos(t * Math.PI * 2); // arch height varies along the rails
                return p + Vector3d.ZAxis * (Math.Sin(s * Math.PI) * h);
            };
            var scene = new Scene();
            scene.Surface(surf, 12, 18);
            scene.Flush(g);
            Polyline3(g, s => surf(s, 0), Profile, 3.0f);
            Polyline3(g, s => surf(s, 1), Profile, 3.0f);
            Polyline3(g, railA, Rail, 3.6f);
            Polyline3(g, railB, Rail, 3.6f);
        }

        private static void DrawRevolve(Graphics g)
        {
            // A vase profile spun three quarters of the way around a dashed axis.
            Func<double, double> radius = v => 0.45 + 0.35 * Math.Sin(v * Math.PI * 1.4 + 0.3);
            Func<double, double, Point3d> surf = (u, v) =>
            {
                double a = Math.PI * 0.5 + u * Math.PI * 1.5;
                double r = radius(v);
                return new Point3d(Math.Cos(a) * r, Math.Sin(a) * r, -1.0 + v * 2.0);
            };
            var scene = new Scene();
            scene.Surface(surf, 20, 12);
            scene.Flush(g);
            DashedLine(g, Iso(0, 0, -1.4), Iso(0, 0, 1.45), Ink);
            Polyline3(g, v => surf(0, v), Profile, 3.4f);
            // Arrow showing the spin.
            Polyline3(g, t => new Point3d(Math.Cos(t * 4.2 + 1.2) * 0.35, Math.Sin(t * 4.2 + 1.2) * 0.35, 1.3), Rail, 2.6f);
            PointF tip = Iso(Math.Cos(5.4) * 0.35, Math.Sin(5.4) * 0.35, 1.3);
            Arrowhead(g, Iso(Math.Cos(5.2) * 0.35, Math.Sin(5.2) * 0.35, 1.3), tip, Rail);
        }

        private static void DrawExtrudeCrv(Graphics g)
        {
            Func<double, Point3d> curve = t => new Point3d(-1.1 + t * 2.2, Math.Sin(t * Math.PI * 2) * 0.55, -0.9);
            var scene = new Scene();
            scene.Surface((t, v) => curve(t) + Vector3d.ZAxis * (v * 1.5), 20, 3);
            scene.Flush(g);
            Polyline3(g, t => curve(t) + Vector3d.ZAxis * 1.5, Edge, 1.6f);
            Polyline3(g, curve, Profile, 3.4f);
            PointF a = Iso(1.45, 0.25, -0.6), b = Iso(1.45, 0.25, 0.9);
            Stroke(g, Rail, 2.8f, a, b);
            Arrowhead(g, a, b, Rail);
        }

        private static void DrawPipe(Graphics g)
        {
            Func<double, Point3d> rail = t => new Point3d(-1.2 + t * 2.4, Math.Sin(t * Math.PI * 1.5) * 0.7, Math.Cos(t * Math.PI) * 0.4);
            Func<double, double, Point3d> tube = (a, t) =>
            {
                Point3d c = rail(t);
                Vector3d tan = Unit(rail(Math.Min(1, t + 0.01)) - rail(Math.Max(0, t - 0.01)));
                Vector3d n = Unit(Vector3d.CrossProduct(tan, Vector3d.ZAxis));
                Vector3d b = Unit(Vector3d.CrossProduct(n, tan));
                double ang = a * Math.PI * 2;
                return c + n * (Math.Cos(ang) * 0.3) + b * (Math.Sin(ang) * 0.3);
            };
            var scene = new Scene();
            scene.Surface(tube, 14, 24);
            scene.Flush(g);
            // The rail pokes out of both ends.
            Polyline3(g, t => rail(-0.12 + t * 0.12), Rail, 3.2f);
            Polyline3(g, t => rail(1 + t * 0.12), Rail, 3.2f);
            Polyline3(g, a => tube(a, 1), Edge, 1.4f);
        }

        private static void DrawNetworkSrf(Graphics g)
        {
            Func<double, double, Point3d> surf = (u, v) =>
            {
                double x = -1.1 + u * 2.2, y = -1.1 + v * 2.2;
                return new Point3d(x, y, Math.Sin(u * Math.PI * 1.6) * Math.Cos(v * Math.PI) * 0.45);
            };
            var scene = new Scene();
            scene.Surface(surf, 16, 16);
            scene.Flush(g);
            for (int i = 0; i < 3; i++)
            {
                double k = i / 2.0;
                Polyline3(g, t => surf(k, t), Rail, 2.8f);
                Polyline3(g, t => surf(t, k), Profile, 2.8f);
            }
        }

        private static void DrawFilletEdge(Graphics g)
        {
            // A block whose front-top edge is rounded over (the fillet is orange-tinted).
            const double x0 = -1.0, x1 = 1.0, y0 = -1.0, y1 = 1.0, z0 = -0.8, z1 = 0.7, r = 0.6;
            var scene = new Scene();
            scene.Polygon(new[] { new Point3d(x0, y0, z1), new Point3d(x1 - r, y0, z1), new Point3d(x1 - r, y1, z1), new Point3d(x0, y1, z1) });
            scene.Polygon(new[] { new Point3d(x1, y0, z0), new Point3d(x1, y1, z0), new Point3d(x1, y1, z1 - r), new Point3d(x1, y0, z1 - r) });
            scene.Surface((a, v) =>
            {
                double ang = a * Math.PI / 2;
                return new Point3d(x1 - r + Math.Sin(ang) * r, y0 + v * (y1 - y0), z1 - r + Math.Cos(ang) * r);
            }, 8, 2, Color.FromArgb(0xFF, 0xE7, 0xC2), Color.FromArgb(0xFF, 0xB8, 0x4D));
            var side = new List<Point3d> { new Point3d(x0, y1, z0), new Point3d(x1, y1, z0), new Point3d(x1, y1, z1 - r) };
            for (int i = 0; i <= 8; i++)
            {
                double ang = (8 - i) / 8.0 * Math.PI / 2;
                side.Add(new Point3d(x1 - r + Math.Sin(ang) * r, y1, z1 - r + Math.Cos(ang) * r));
            }
            side.Add(new Point3d(x0, y1, z1));
            scene.Polygon(side.ToArray());
            scene.Flush(g);
            Polyline3(g, a => new Point3d(x1 - r + Math.Sin(a * Math.PI / 2) * r, y1, z1 - r + Math.Cos(a * Math.PI / 2) * r), Rail, 3.0f);
            Polyline3(g, a => new Point3d(x1 - r + Math.Sin(a * Math.PI / 2) * r, y0, z1 - r + Math.Cos(a * Math.PI / 2) * r), Rail, 3.0f);
        }

        private static void DrawBooleanUnion(Graphics g)
        {
            // Two overlapping balls drawn as one shape: one shared outline, no inner seam.
            var a = new RectangleF(14, 22, 48, 48);
            var b = new RectangleF(40, 30, 48, 48);
            var outline = new Pen(Edge, 5f);
            g.DrawEllipse(outline, a);
            g.DrawEllipse(outline, b);
            g.FillEllipse(SurfDark, a);
            g.FillEllipse(SurfDark, b);
            g.FillEllipse(SurfLight, new RectangleF(20, 26, 30, 30));
            g.FillEllipse(SurfLight, new RectangleF(46, 34, 30, 30));
            Plus(g, new PointF(50, 86), Rail);
        }

        private static void DrawBooleanDifference(Graphics g)
        {
            var scene = new Scene();
            scene.Box(new Point3d(-1, -1, -0.7), new Point3d(1, 1, 0.7));
            scene.Flush(g);
            // The scoop where the ball was taken out of the top corner.
            PointF c = Iso(1, 1, 0.7);
            var hole = new RectangleF(c.X - 17, c.Y - 17, 34, 34);
            g.FillEllipse(Color.FromArgb(0x1C, 0x7F, 0xB8), hole);
            g.FillEllipse(Color.FromArgb(0x2B, 0x9C, 0xD8), new RectangleF(c.X - 13, c.Y - 9, 26, 24));
            var dashed = new Pen(Profile, 2.4f) { DashStyle = DashStyles.Dash };
            g.DrawEllipse(dashed, new RectangleF(c.X - 21, c.Y - 21, 42, 42));
            Minus(g, new PointF(84, 16), Rail);
        }

        // ------------------------------------------------------------------ curves

        private static void DrawPolyline(Graphics g)
        {
            CPlane(g);
            var pts = new[] { Iso(-1.2, 0.6, 0), Iso(-0.5, -0.9, 0), Iso(0.2, 0.5, 0), Iso(0.8, -0.6, 0), Iso(1.2, 0.9, 0) };
            Stroke(g, Rail, 3.4f, pts);
            foreach (PointF p in pts)
                Dot(g, p, Ink, 3.2f);
        }

        private static void DrawCircle(Graphics g)
        {
            CPlane(g);
            Polyline3(g, t => new Point3d(Math.Cos(t * Math.PI * 2) * 1.05, Math.Sin(t * Math.PI * 2) * 1.05, 0), Rail, 3.4f);
            Stroke(g, Ink, 1.6f, Iso(0, 0, 0), Iso(1.05 * Math.Cos(-0.6), 1.05 * Math.Sin(-0.6), 0));
            Dot(g, Iso(0, 0, 0), Ink, 3.2f);
        }

        private static void DrawLine(Graphics g)
        {
            CPlane(g);
            PointF a = Iso(-1.1, 0.4, 0), b = Iso(1.0, -0.7, 0);
            Stroke(g, Rail, 3.6f, a, b);
            Dot(g, a, Ink, 3.6f);
            Dot(g, b, Ink, 3.6f);
        }

        private static void DrawRectangle(Graphics g)
        {
            CPlane(g);
            var pts = new[] { Iso(-1, -0.8, 0), Iso(1, -0.8, 0), Iso(1, 0.8, 0), Iso(-1, 0.8, 0), Iso(-1, -0.8, 0) };
            Stroke(g, Rail, 3.4f, pts);
            Dot(g, pts[0], Ink, 3.4f);
            Dot(g, pts[2], Ink, 3.4f);
        }

        private static void Unknown(Graphics g)
        {
            var font = new Font(SystemFont.Bold, 40);
            SizeF size = g.MeasureString(font, "?");
            g.DrawText(font, Grid, 50 - size.Width / 2, 50 - size.Height / 2, "?");
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Isometric projection, viewer up and to the front-right.</summary>
        private static PointF Iso(double x, double y, double z) =>
            new PointF(Ox + (float)((x - y) * 0.866) * S, Oy + (float)((x + y) * 0.5 - z) * S);

        private static PointF Iso(Point3d p) => Iso(p.X, p.Y, p.Z);

        private static Vector3d Unit(Vector3d v)
        {
            v.Unitize();
            return v;
        }

        private static Pen RoundPen(Color color, float width) =>
            new Pen(color, width) { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };

        private static void Stroke(Graphics g, Color color, float width, params PointF[] pts)
        {
            if (pts.Length >= 2)
                g.DrawLines(RoundPen(color, width), pts);
        }

        private static void Polyline3(Graphics g, Func<double, Point3d> curve, Color color, float width, int samples = 40)
        {
            var pts = new PointF[samples + 1];
            for (int i = 0; i <= samples; i++)
                pts[i] = Iso(curve(i / (double)samples));
            Stroke(g, color, width, pts);
        }

        private static void DashedLine(Graphics g, PointF a, PointF b, Color color)
        {
            g.DrawLine(new Pen(color, 1.6f) { DashStyle = DashStyles.Dash }, a, b);
        }

        private static void Dot(Graphics g, PointF p, Color color, float r)
        {
            g.FillEllipse(Colors.White, p.X - r - 1.2f, p.Y - r - 1.2f, (r + 1.2f) * 2, (r + 1.2f) * 2);
            g.FillEllipse(color, p.X - r, p.Y - r, r * 2, r * 2);
        }

        private static void Arrowhead(Graphics g, PointF from, PointF tip, Color color)
        {
            float dx = tip.X - from.X, dy = tip.Y - from.Y;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-3f)
                return;
            dx /= len;
            dy /= len;
            const float l = 8f, w = 5f;
            var a = new PointF(tip.X - dx * l - dy * w, tip.Y - dy * l + dx * w);
            var b = new PointF(tip.X - dx * l + dy * w, tip.Y - dy * l - dx * w);
            g.FillPolygon(color, tip, a, b);
        }

        private static void Plus(Graphics g, PointF c, Color color)
        {
            g.FillEllipse(color, c.X - 9, c.Y - 9, 18, 18);
            Stroke(g, Colors.White, 3f, new PointF(c.X - 4.5f, c.Y), new PointF(c.X + 4.5f, c.Y));
            Stroke(g, Colors.White, 3f, new PointF(c.X, c.Y - 4.5f), new PointF(c.X, c.Y + 4.5f));
        }

        private static void Minus(Graphics g, PointF c, Color color)
        {
            g.FillEllipse(color, c.X - 9, c.Y - 9, 18, 18);
            Stroke(g, Colors.White, 3f, new PointF(c.X - 4.5f, c.Y), new PointF(c.X + 4.5f, c.Y));
        }

        /// <summary>A faint construction-plane grid under the curve icons.</summary>
        private static void CPlane(Graphics g)
        {
            var pen = new Pen(Grid, 1.2f);
            for (int i = -3; i <= 3; i++)
            {
                double k = i * 0.5;
                g.DrawLine(pen, Iso(k, -1.5, 0), Iso(k, 1.5, 0));
                g.DrawLine(pen, Iso(-1.5, k, 0), Iso(1.5, k, 0));
            }
        }

        private static Color Lerp(Color a, Color b, double t)
        {
            float f = (float)Math.Max(0, Math.Min(1, t));
            return new Color(a.R + (b.R - a.R) * f, a.G + (b.G - a.G) * f, a.B + (b.B - a.B) * f, 1f);
        }

        /// <summary>Collects shaded faces from several surfaces and paints them back to front.</summary>
        private sealed class Scene
        {
            private readonly List<(double depth, PointF[] pts, Color color)> _faces = new List<(double, PointF[], Color)>();

            public void Surface(Func<double, double, Point3d> f, int nu, int nv) => Surface(f, nu, nv, SurfLight, SurfDark);

            public void Surface(Func<double, double, Point3d> f, int nu, int nv, Color light, Color dark)
            {
                for (int i = 0; i < nu; i++)
                {
                    for (int j = 0; j < nv; j++)
                    {
                        double u0 = i / (double)nu, u1 = (i + 1) / (double)nu;
                        double v0 = j / (double)nv, v1 = (j + 1) / (double)nv;
                        Add(new[] { f(u0, v0), f(u1, v0), f(u1, v1), f(u0, v1) }, light, dark);
                    }
                }
            }

            public void Polygon(Point3d[] pts) => Add(pts, SurfLight, SurfDark);

            public void Polygon(Point3d[] pts, Color light, Color dark) => Add(pts, light, dark);

            public void Disc(Point3d center, double radius)
            {
                var pts = Enumerable.Range(0, 32)
                    .Select(i => center + new Vector3d(Math.Cos(i * Math.PI / 16) * radius, Math.Sin(i * Math.PI / 16) * radius, 0))
                    .ToArray();
                Add(pts, SurfLight, SurfDark, depthBias: 0.5);
            }

            public void Box(Point3d min, Point3d max)
            {
                // Only the three faces the viewer can see.
                Polygon(new[] { new Point3d(min.X, min.Y, max.Z), new Point3d(max.X, min.Y, max.Z), new Point3d(max.X, max.Y, max.Z), new Point3d(min.X, max.Y, max.Z) });
                Polygon(new[] { new Point3d(max.X, min.Y, min.Z), new Point3d(max.X, max.Y, min.Z), new Point3d(max.X, max.Y, max.Z), new Point3d(max.X, min.Y, max.Z) });
                Polygon(new[] { new Point3d(min.X, max.Y, min.Z), new Point3d(max.X, max.Y, min.Z), new Point3d(max.X, max.Y, max.Z), new Point3d(min.X, max.Y, max.Z) });
            }

            private void Add(Point3d[] pts, Color light, Color dark, double depthBias = 0)
            {
                Vector3d n = Vector3d.CrossProduct(pts[2] - pts[0], pts[pts.Length - 1] - pts[1]);
                if (!n.Unitize())
                    n = Vector3d.ZAxis;
                double shade = Math.Abs(n * Light);
                double depth = pts.Average(p => p.X + p.Y + p.Z) + depthBias;
                _faces.Add((depth, pts.Select(Iso).ToArray(), Lerp(dark, light, shade)));
            }

            public void Flush(Graphics g)
            {
                foreach (var face in _faces.OrderBy(f => f.depth))
                {
                    g.FillPolygon(face.color, face.pts);
                    g.DrawPolygon(new Pen(face.color, 0.8f), face.pts); // hides hairline seams
                }
                // Crisp outline around big flat faces (boxes): drawn by the caller's edges otherwise.
                foreach (var face in _faces.Where(f => f.pts.Length != 4 || _faces.Count <= 3))
                    g.DrawPolygon(RoundPen(Edge, 1.6f), face.pts);
                _faces.Clear();
            }
        }
    }
}
