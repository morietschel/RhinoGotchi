using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;
using Rhino.Geometry;
using RhinoGotchi.Tutorials;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// The animated demo at the top of a chapter level, in FinGotchi's key-axon style: grey shaded
    /// input, the picked objects outlined in yellow, the cursor moving to the click points, the typed
    /// value, and the result growing in orange. It loops through the tutorial's steps and returns the
    /// step being shown (0-based) so the panel can light it up.
    /// </summary>
    internal static class TutorialDemos
    {
        private const double SecondsPerStep = 2.8;
        private const double HoldSeconds = 1.6;

        private static readonly Dictionary<int, Action<Scene, int, double>> Demos = new Dictionary<int, Action<Scene, int, double>>
        {
            [1] = Plate,
            [2] = Rail,
            [3] = Glass,
            [4] = Beams,
            [5] = Bars,
            [6] = Slices,
            [7] = Rib,
            [8] = Edge,
            [9] = Ramp,
            [10] = Joists,
            [11] = LoftDemo,
            [12] = Net,
        };

        private static readonly Dictionary<int, BoundingBox> Frames = new Dictionary<int, BoundingBox>();

        public static int Draw(Graphics g, RectangleF area, Tutorial tutorial, double time)
        {
            Palette t = Palette.Fin;
            g.FillPath(t.TileHover, GraphicsPath.GetRoundRect(area, 12));
            int steps = Math.Max(1, tutorial.Steps.Count);
            double loop = steps * SecondsPerStep + HoldSeconds;
            double at = time % loop;
            int step = Math.Min(steps - 1, (int)(at / SecondsPerStep));
            double u = Math.Min(1, (at - step * SecondsPerStep) / SecondsPerStep);

            if (!Demos.TryGetValue(tutorial.Number, out var demo))
            {
                float icon = Math.Min(area.Height * 0.55f, 110);
                LessonIcons.Draw(g, tutorial.MainCommand, new RectangleF(area.X + (area.Width - icon) / 2, area.Y + (area.Height - icon) / 2, icon, icon));
                return step;
            }

            if (!Frames.TryGetValue(tutorial.Number, out BoundingBox frame))
            {
                // Fit everything the loop ever shows, so the picture doesn't jump between steps
                var all = new Scene();
                for (int k = 0; k < steps; k++)
                {
                    demo(all, k, 0);
                    demo(all, k, 1);
                }
                frame = new BoundingBox(all.AllPoints());
                Frames[tutorial.Number] = frame;
            }

            var scene = new Scene();
            demo(scene, step, u);
            scene.Render(g, area, frame);
            return step;
        }

        // ================================================================ the twelve demos

        // 1 BooleanDifference: a plate and four cutters; Enter leaves four holes.
        private static void Plate(Scene s, int step, double u)
        {
            var holes = new[] { P(0.6, 0.6), P(3.4, 0.6), P(0.6, 1.8), P(3.4, 1.8) };
            bool cut = step == 2 && u > 0.35;
            s.Box(P(0, 0, 0), P(4, 2.4, 0.2), picked: step > 0 || u > 0.3);
            if (!cut)
            {
                int clicked = step == 1 ? s.Clicks(u, holes.Select(h => h + V(0, 0, 0.6)).ToArray()) : step > 1 ? 4 : 0;
                for (int i = 0; i < holes.Length; i++)
                    s.Prism(holes[i] + V(0, 0, -0.3), 0.22, 0.9, picked: i < clicked);
                if (step == 2)
                    s.Type(P(4, 2.4, 0.6), "Enter ↵");
            }
            else
            {
                foreach (Point3d h in holes)
                    s.Hole(h + V(0, 0, 0.2), 0.22);
            }
        }

        // 2 Pipe: the stair rail's centreline becomes a round tube.
        private static void Rail(Scene s, int step, double u)
        {
            var line = new[] { P(0, 0, 0.9), P(1, 0, 0.9), P(5, 0, 3.1), P(6, 0, 3.1) };
            // the stair underneath
            var stair = new List<Point3d> { P(0, 0, 0) };
            for (int i = 0; i < 8; i++)
            {
                double x = 1 + i * 0.5, z = i * 0.275;
                stair.Add(P(x, 0, z));
                stair.Add(P(x, 0, z + 0.275));
            }
            stair.Add(P(6, 0, 2.2));
            s.Line(stair, Stroke.Faint);
            s.Line(line, step == 0 && u < 0.3 ? Stroke.Input : Stroke.Picked);
            if (step == 0)
                s.Cursor = Lerp(P(3, 0, 4), P(3, 0, 2), Ease(Math.Min(1, u / 0.3)));
            if (step == 1)
                s.Type(P(6, 0, 3.1), u < 0.6 ? "0.025 ↵" : "0.025 ↵ ↵");
            if (step == 2)
                s.Tube(line, 0.2, Ease(u));
        }

        // 3 OffsetSrf: a curved pane gets its thickness, growing inward.
        private static void Glass(Scene s, int step, double u)
        {
            Point3d F(double a, double b) => P(a * 3, Math.Sin(a * Math.PI) * 0.6, b * 2.4);
            Vector3d N(double a)
            {
                var d = V(3, Math.Cos(a * Math.PI) * 0.6 * Math.PI, 0);
                return V(-d.Y, d.X, 0) / d.Length;
            }
            s.Surface(F, 8, 4, step > 0 || u > 0.3 ? Stroke.Picked : Stroke.Input);
            if (step == 1)
                s.Type(F(1, 1), u < 0.5 ? "Distance 0.024" : "Solid=Yes");
            if (step == 2)
            {
                bool flipped = u > 0.3;
                foreach (double a in new[] { 0.2, 0.5, 0.8 })
                {
                    Point3d o = F(a, 0.6);
                    s.Arrow(o, (flipped ? -1 : 1) * N(a) * 0.6);
                }
                if (u > 0.55)
                {
                    double d = 0.3 * Ease((u - 0.55) / 0.45);
                    s.Surface((a, b) => F(a, b) - N(a) * d, 8, 4, Stroke.Result);
                    foreach (double a in new[] { 0.0, 1.0 })
                        foreach (double b in new[] { 0.0, 1.0 })
                            s.Line(new[] { F(a, b), F(a, b) - N(a) * d }, Stroke.Result);
                }
            }
        }

        // 4 ArrayPolar: one plane, copied around the roof's centre.
        private static void Beams(Scene s, int step, double u)
        {
            s.Line(Circle(P(0, 0, 0), 3, 40), Stroke.Faint);
            Point3d[] Plane(double angle)
            {
                var pts = new[] { P(0.3, 0, 0), P(3, 0, 0), P(3, 0, 0.8), P(0.3, 0, 0.8) };
                return pts.Select(p => Rotate(p, angle)).ToArray();
            }
            s.Face(Plane(0), Stroke.Picked);
            if (step == 0)
                s.Clicks(u, P(0, 0, 0));
            if (step == 1)
                s.Type(P(0, 0, 0.8), "12 ↵");
            if (step == 2)
            {
                int copies = (int)(Ease(u) * 11.99);
                for (int k = 1; k <= copies; k++)
                    s.Face(Plane(k * Math.PI / 6), Stroke.Result);
            }
            s.Dot(P(0, 0, 0), step > 0 || u > 0.6);
        }

        // 5 ArrayCrv: one bar, repeated along the rail.
        private static void Bars(Scene s, int step, double u)
        {
            Point3d R(double a) => P(a * 5, 0, 1 + a * 2.4 + Math.Sin(a * Math.PI) * 0.15);
            var rail = Enumerable.Range(0, 21).Select(i => R(i / 20.0)).ToArray();
            s.Line(rail.Select(p => p - V(0, 0, 0.9)).ToArray(), Stroke.Faint);
            int clicked = step == 0 ? s.Clicks(u, R(0), R(0.5)) : 2;
            s.Line(rail, clicked >= 2 ? Stroke.Picked : Stroke.Input);
            s.Line(new[] { R(0) - V(0, 0, 0.9), R(0) }, clicked >= 1 ? Stroke.Picked : Stroke.Input);
            if (step == 1)
                s.Type(R(0.3), u < 0.5 ? "Distance 0.12" : "No rotation");
            if (step == 2)
            {
                int n = (int)(Ease(u) * 14);
                for (int k = 1; k <= n; k++)
                {
                    Point3d p = R(k / 14.0);
                    s.Line(new[] { p - V(0, 0, 0.9), p }, Stroke.Result);
                }
            }
        }

        private static Point3d Roof(double a, double b) =>
            P(a * 4, b * 3, 0.5 + 0.45 * Math.Sin(a * Math.PI) * Math.Cos((b - 0.3) * Math.PI * 0.7));

        // 6 Contour: evenly spaced slices through the roof.
        private static void Slices(Scene s, int step, double u)
        {
            s.Surface(Roof, 10, 8, step > 0 || u > 0.2 ? Stroke.Picked : Stroke.Input);
            if (step == 0)
                s.Clicks(u, Roof(0, 0), Roof(1, 0));
            if (step == 1)
            {
                s.Type(Roof(1, 0), "0.4 ↵");
                int n = (int)(Ease(u) * 10);
                for (int k = 1; k <= n; k++)
                {
                    double a = k / 11.0;
                    s.Line(Enumerable.Range(0, 21).Select(i => Roof(a, i / 20.0)).ToArray(), Stroke.Result);
                }
            }
        }

        // 7 Intersect: an upright plane cuts the roof in one rib.
        private static void Rib(Scene s, int step, double u)
        {
            const double b = 0.5;
            s.Surface(Roof, 10, 8, step == 0 && u > 0.25 ? Stroke.Picked : Stroke.Input);
            s.Face(new[] { P(0, 1.5, -0.2), P(4, 1.5, -0.2), P(4, 1.5, 1.4), P(0, 1.5, 1.4) },
                step == 0 && u > 0.25 ? Stroke.Picked : Stroke.Faint, translucent: true);
            if (step == 0 && u > 0.55 || step == 1)
            {
                double r = step == 0 ? Ease((u - 0.55) / 0.45) : 1;
                int n = Math.Max(2, (int)(r * 30));
                s.Line(Enumerable.Range(0, n + 1).Select(i => Roof(i * r / n, b)).ToArray(), Stroke.Result);
            }
            if (step == 1)
                s.Type(Roof(1, b), "Front view");
        }

        // 8 Sweep1: a section swept along the roof edge.
        private static void Edge(Scene s, int step, double u)
        {
            Point3d E(double a) => P(Math.Cos(a * Math.PI / 2) * 3, Math.Sin(a * Math.PI / 2) * 3, 2);
            var rail = Enumerable.Range(0, 25).Select(i => E(i / 24.0)).ToArray();
            var section = Section(E(0), V(0, 1, 0), 0.25, 0.4);
            int clicked = step == 0 ? s.Clicks(u, E(0.5), section[1]) : 2;
            s.Line(rail, clicked >= 1 ? Stroke.Picked : Stroke.Input);
            s.Line(section.Append(section[0]).ToArray(), clicked >= 2 ? Stroke.Picked : Stroke.Input);
            if (step == 1)
            {
                s.Type(E(1), u < 0.4 ? "Roadlike Top" : "OK");
                if (u > 0.4)
                {
                    double r = Ease((u - 0.4) / 0.6);
                    int n = Math.Max(1, (int)(r * 24));
                    for (int i = 0; i <= n; i++)
                    {
                        double a = i * r / n;
                        var t = V(-Math.Sin(a * Math.PI / 2), Math.Cos(a * Math.PI / 2), 0);
                        var sec = Section(E(a), t, 0.25, 0.4);
                        if (i % 4 == 0 || i == n)
                            s.Line(sec.Append(sec[0]).ToArray(), Stroke.Result);
                    }
                    for (int c = 0; c < 4; c++)
                    {
                        int corner = c;
                        s.Line(Enumerable.Range(0, n + 1).Select(i =>
                        {
                            double a = i * r / n;
                            var t = V(-Math.Sin(a * Math.PI / 2), Math.Cos(a * Math.PI / 2), 0);
                            return Section(E(a), t, 0.25, 0.4)[corner];
                        }).ToArray(), Stroke.Result);
                    }
                }
            }
        }

        // 9 Sweep2: two rising edges and two landings make the ramp.
        private static void Ramp(Scene s, int step, double u)
        {
            Point3d A(double a) => P(a * 5, 0, a * 1);
            Point3d B(double a) => P(a * 5, 1.5, a * 1);
            var ra = Enumerable.Range(0, 11).Select(i => A(i / 10.0)).ToArray();
            var rb = Enumerable.Range(0, 11).Select(i => B(i / 10.0)).ToArray();
            int rails = step == 0 ? s.Clicks(u, A(0.5), B(0.5)) : 2;
            int lines = step == 1 ? s.Clicks(u, Lerp(A(0), B(0), 0.5), Lerp(A(1), B(1), 0.5)) : step > 1 ? 2 : 0;
            s.Line(ra, rails >= 1 ? Stroke.Picked : Stroke.Input);
            s.Line(rb, rails >= 2 ? Stroke.Picked : Stroke.Input);
            s.Line(new[] { A(0), B(0) }, lines >= 1 ? Stroke.Picked : Stroke.Input);
            s.Line(new[] { A(1), B(1) }, lines >= 2 ? Stroke.Picked : Stroke.Input);
            if (step == 2)
            {
                double r = Ease(u);
                s.Surface((a, b) => Lerp(A(a * r), B(a * r), b), 10, 4, Stroke.Result);
                s.Type(B(1), "OK");
            }
        }

        // 10 FlowAlongSrf: flat joists flow up onto the curved roof.
        private static void Joists(Scene s, int step, double u)
        {
            Point3d Base(double a, double b) => P(a * 4, b * 3, 0);
            Point3d Top(double a, double b) => P(a * 4, b * 3, 2 + 0.5 * Math.Sin(a * Math.PI));
            s.Face(new[] { Base(0, 0), Base(1, 0), Base(1, 1), Base(0, 1) }, Stroke.Faint, translucent: true);
            s.Surface(Top, 8, 6, Stroke.Input);
            int clicked = step == 1 ? s.Clicks(u, Base(0, 0)) : step == 2 ? 1 + s.Clicks(u, Top(0, 0)) : 0;
            double flow = step == 2 ? Ease(Math.Max(0, (u - 0.6) / 0.4)) : 0;
            for (int k = 0; k < 6; k++)
            {
                double a = (k + 0.5) / 6;
                var flat = new[] { Base(a, 0.05), Base(a, 0.95) };
                s.Line(flat, Stroke.Picked);
                if (flow > 0)
                    s.Line(Enumerable.Range(0, 11).Select(i => Lerp(Base(a, 0.05 + 0.09 * i), Top(a, 0.05 + 0.09 * i), flow)).ToArray(), Stroke.Result);
            }
            if (clicked >= 1)
                s.Dot(Base(0, 0), true);
        }

        // 11 Loft: four wavy sections become one surface.
        private static void LoftDemo(Scene s, int step, double u)
        {
            Point3d W(double a, double b) => P(a * 4, b * 3, 0.45 * Math.Sin(a * Math.PI * 1.5 + b * 2));
            int picked = step == 1 ? s.Clicks(u, W(0.5, 0), W(0.5, 1 / 3.0), W(0.5, 2 / 3.0), W(0.5, 1)) : step == 2 ? 4 : 0;
            for (int k = 0; k < 4; k++)
            {
                double b = k / 3.0;
                s.Line(Enumerable.Range(0, 21).Select(i => W(i / 20.0, b)).ToArray(), k < picked ? Stroke.Picked : Stroke.Input);
            }
            if (step == 0)
                s.Type(W(1, 1), u < 0.5 ? "Record History" : "History ● On");
            if (step == 2)
            {
                s.Type(W(1, 1), "Normal · OK");
                if (u > 0.35)
                {
                    double r = Ease((u - 0.35) / 0.65);
                    s.Surface((a, b) => W(a, b * r), 10, 6, Stroke.Result);
                }
            }
        }

        // 12 NetworkSrf: curves in two directions, one surface.
        private static void Net(Scene s, int step, double u)
        {
            Point3d W(double a, double b) => P(a * 4, b * 3, 0.4 * Math.Sin(a * Math.PI) + 0.3 * Math.Sin(b * Math.PI * 1.4));
            int picked = step == 0 ? (int)(Ease(Math.Min(1, u / 0.8)) * 8) : 8;
            for (int k = 0; k < 4; k++)
            {
                double c = k / 3.0;
                s.Line(Enumerable.Range(0, 21).Select(i => W(i / 20.0, c)).ToArray(), 2 * k < picked ? Stroke.Picked : Stroke.Input);
                s.Line(Enumerable.Range(0, 21).Select(i => W(c, i / 20.0)).ToArray(), 2 * k + 1 < picked ? Stroke.Picked : Stroke.Input);
            }
            if (step == 1)
            {
                s.Type(W(1, 1), "OK");
                if (u > 0.3)
                    s.Surface(W, 12, 12, Stroke.Result);
            }
            if (step == 2)
            {
                s.Type(W(1, 1), "Rebuild 6 × 6");
                int n = u < 0.4 ? 12 : 6;
                s.Surface(W, n, n, Stroke.Result);
            }
        }

        // ================================================================ geometry helpers

        private static Point3d P(double x, double y, double z = 0) => new Point3d(x, y, z);

        private static Vector3d V(double x, double y, double z) => new Vector3d(x, y, z);

        private static Point3d Lerp(Point3d a, Point3d b, double t) => a + (b - a) * t;

        private static double Ease(double x)
        {
            x = Math.Max(0, Math.Min(1, x));
            return x < 0.5 ? 2 * x * x : 1 - Math.Pow(-2 * x + 2, 2) / 2;
        }

        private static Point3d Rotate(Point3d p, double angle) =>
            P(p.X * Math.Cos(angle) - p.Y * Math.Sin(angle), p.X * Math.Sin(angle) + p.Y * Math.Cos(angle), p.Z);

        private static Point3d[] Circle(Point3d c, double r, int n) =>
            Enumerable.Range(0, n + 1).Select(i => c + V(Math.Cos(i * 2 * Math.PI / n) * r, Math.Sin(i * 2 * Math.PI / n) * r, 0)).ToArray();

        /// <summary>A w × h rectangle across <paramref name="tangent"/>, hanging below <paramref name="at"/>.</summary>
        private static Point3d[] Section(Point3d at, Vector3d tangent, double w, double h)
        {
            Vector3d side = Vector3d.CrossProduct(tangent, Vector3d.ZAxis);
            side.Unitize();
            return new[] { at - side * w / 2, at + side * w / 2, at + side * w / 2 - V(0, 0, h), at - side * w / 2 - V(0, 0, h) };
        }

        // ================================================================ the scene

        internal enum Stroke { Faint, Input, Picked, Result }

        private sealed class Scene
        {
            private readonly List<(Point3d[] Points, Stroke Stroke, bool Fill, bool Translucent)> _items = new List<(Point3d[], Stroke, bool, bool)>();
            private readonly List<(Point3d At, bool Clicked)> _dots = new List<(Point3d, bool)>();
            private readonly List<(Point3d At, Vector3d Dir)> _arrows = new List<(Point3d, Vector3d)>();
            private readonly List<(Point3d At, double R)> _holes = new List<(Point3d, double)>();
            private (Point3d At, string Text)? _typed;

            public Point3d? Cursor;

            public IEnumerable<Point3d> AllPoints() =>
                _items.SelectMany(i => i.Points).Concat(_dots.Select(d => d.At)).Concat(_holes.Select(h => h.At));

            public void Line(IEnumerable<Point3d> points, Stroke stroke) => _items.Add((points.ToArray(), stroke, false, false));

            public void Face(Point3d[] points, Stroke stroke, bool translucent = false) => _items.Add((points, stroke, true, translucent));

            public void Dot(Point3d at, bool clicked) => _dots.Add((at, clicked));

            public void Arrow(Point3d at, Vector3d dir) => _arrows.Add((at, dir));

            public void Hole(Point3d at, double r) => _holes.Add((at, r));

            public void Type(Point3d at, string text) => _typed = (at, text);

            public void Box(Point3d min, Point3d max, bool picked)
            {
                Point3d[] c = new BoundingBox(min, max).GetCorners();
                int[][] faces = { new[] { 0, 1, 2, 3 }, new[] { 4, 5, 6, 7 }, new[] { 0, 1, 5, 4 }, new[] { 1, 2, 6, 5 }, new[] { 2, 3, 7, 6 }, new[] { 3, 0, 4, 7 } };
                foreach (int[] f in faces)
                    Face(f.Select(i => c[i]).ToArray(), picked ? Stroke.Picked : Stroke.Input);
            }

            /// <summary>An upright n-gon prism (a cutter or a bolt).</summary>
            public void Prism(Point3d bottom, double r, double h, bool picked)
            {
                const int n = 14;
                Point3d[] ring = Circle(bottom, r, n).Take(n).ToArray();
                Stroke st = picked ? Stroke.Picked : Stroke.Input;
                for (int i = 0; i < n; i++)
                {
                    Point3d a = ring[i], b = ring[(i + 1) % n];
                    _items.Add((new[] { a, b, b + V(0, 0, h), a + V(0, 0, h) }, st, true, false));
                }
                _items.Add((ring.Select(p => p + V(0, 0, h)).ToArray(), st, true, false));
            }

            /// <summary>A tube along a polyline, grown to <paramref name="reveal"/> of its length.</summary>
            public void Tube(Point3d[] line, double r, double reveal)
            {
                var pts = Resample(line, 40);
                int n = Math.Max(1, (int)(reveal * (pts.Length - 1)));
                var left = new List<Point3d>();
                var right = new List<Point3d>();
                for (int i = 0; i <= n; i++)
                {
                    Vector3d t = pts[Math.Min(i + 1, pts.Length - 1)] - pts[Math.Max(i - 1, 0)];
                    t.Unitize();
                    Vector3d side = Vector3d.CrossProduct(t, Vector3d.YAxis);
                    side.Unitize();
                    left.Add(pts[i] + side * r);
                    right.Add(pts[i] - side * r);
                    if (i % 5 == 0 || i == n)
                        Line(Ring(pts[i], t, r), Stroke.Result);
                }
                Line(left, Stroke.Result);
                Line(right, Stroke.Result);
            }

            public void Surface(Func<double, double, Point3d> f, int nu, int nv, Stroke stroke)
            {
                if (stroke == Stroke.Picked)
                {
                    // Picked: yellow round the border only, the cells stay plain
                    var border = new List<Point3d>();
                    for (int i = 0; i <= 20; i++) border.Add(f(i / 20.0, 0));
                    for (int i = 0; i <= 20; i++) border.Add(f(1, i / 20.0));
                    for (int i = 20; i >= 0; i--) border.Add(f(i / 20.0, 1));
                    for (int i = 20; i >= 0; i--) border.Add(f(0, i / 20.0));
                    Surface(f, nu, nv, Stroke.Input);
                    _items.Add((border.ToArray(), Stroke.Picked, false, false));
                    return;
                }
                for (int i = 0; i < nu; i++)
                    for (int j = 0; j < nv; j++)
                    {
                        double a0 = (double)i / nu, a1 = (double)(i + 1) / nu, b0 = (double)j / nv, b1 = (double)(j + 1) / nv;
                        _items.Add((new[] { f(a0, b0), f(a1, b0), f(a1, b1), f(a0, b1) }, stroke, true, true));
                    }
            }

            /// <summary>
            /// Moves the cursor to each target in turn (equal slots, the click 60% into each) and puts a dot
            /// on each. Returns how many have been clicked so far.
            /// </summary>
            public int Clicks(double u, params Point3d[] targets)
            {
                int clicked = 0;
                double slot = 1.0 / targets.Length;
                for (int i = 0; i < targets.Length; i++)
                {
                    double local = (u - i * slot) / slot;
                    bool done = local >= 0.6;
                    if (done)
                        clicked++;
                    Dot(targets[i], done);
                    if (local >= 0 && local < 1)
                    {
                        Point3d from = i == 0 ? targets[0] + V(0.8, -0.8, 0.8) : targets[i - 1];
                        Cursor = Lerp(from, targets[i], Ease(Math.Min(1, local / 0.6)));
                    }
                }
                if (u >= 1)
                    Cursor = targets[targets.Length - 1];
                return clicked;
            }

            // ------------------------------------------------ drawing

            private static readonly Vector3d Dir = Unit(V(1, 1.5, -1.1));   // looking from front-left, above
            private static readonly Vector3d Right = Unit(Vector3d.CrossProduct(Dir, Vector3d.ZAxis));
            private static readonly Vector3d Up = Unit(Vector3d.CrossProduct(Right, Dir));
            private static readonly Vector3d Light = Unit(V(-0.4, -0.7, 1));

            public void Render(Graphics g, RectangleF area, BoundingBox frame)
            {
                Palette t = Palette.Fin;
                Point3d[] corners = frame.GetCorners();
                double minX = corners.Min(p => Dot(p, Right)), maxX = corners.Max(p => Dot(p, Right));
                double minY = corners.Min(p => -Dot(p, Up)), maxY = corners.Max(p => -Dot(p, Up));
                float scale = (float)Math.Min((area.Width - 36) / Math.Max(1e-6, maxX - minX), (area.Height - 36) / Math.Max(1e-6, maxY - minY));
                float ox = area.X + area.Width / 2 - (float)((minX + maxX) / 2) * scale;
                float oy = area.Y + area.Height / 2 - (float)((minY + maxY) / 2) * scale;
                PointF Pt(Point3d p) => new PointF(ox + (float)Dot(p, Right) * scale, oy - (float)Dot(p, Up) * scale);

                g.SaveTransform();
                g.SetClip(area);

                // Faces far to near, each filled with a grey (or orange) shade and outlined; lines on top
                var faces = _items.Where(i => i.Fill).OrderByDescending(i => Dot(Centroid(i.Points), Dir)).ToList();
                foreach (var (points, stroke, _, translucent) in faces)
                {
                    PointF[] poly = points.Select(Pt).ToArray();
                    Vector3d n = Newell(points);
                    double lit = Math.Abs(n * Light);
                    Color fill = stroke == Stroke.Result ? Mix(Color.FromArgb(0xF6, 0xC0, 0x90), Color.FromArgb(0xFB, 0xE3, 0xCC), lit)
                        : stroke == Stroke.Faint ? Color.FromArgb(0xEE, 0xEE, 0xEE)
                        : Mix(Color.FromArgb(0xB4, 0xB4, 0xB4), Color.FromArgb(0xEC, 0xEC, 0xEC), lit);
                    if (translucent)
                        fill = Color.FromArgb(fill.Rb, fill.Gb, fill.Bb, stroke == Stroke.Faint ? 110 : 190);
                    g.FillPolygon(fill, poly);
                    if (stroke == Stroke.Picked)
                        g.DrawPolygon(new Pen(t.Gold, 3.2f) { LineJoin = PenLineJoin.Round }, poly);
                    g.DrawPolygon(PenFor(t, stroke, translucent), poly);
                }

                foreach (var (at, r) in _holes)
                {
                    PointF[] ring = Circle(at, r, 24).Select(Pt).ToArray();
                    g.FillPolygon(Color.FromArgb(0x6A, 0x6A, 0x6A), ring);
                    g.DrawPolygon(new Pen(t.Orange, 1.8f), ring);
                }

                foreach (var (points, stroke, _, _) in _items.Where(i => !i.Fill))
                {
                    PointF[] line = points.Select(Pt).ToArray();
                    if (line.Length < 2)
                        continue;
                    if (stroke == Stroke.Picked)
                        g.DrawLines(new Pen(t.Gold, 5f) { LineJoin = PenLineJoin.Round, LineCap = PenLineCap.Round }, line);
                    g.DrawLines(PenFor(t, stroke, false), line);
                }

                foreach (var (at, dir) in _arrows)
                    DrawArrow(g, t, Pt(at), Pt(at + dir));

                // Click points, cursor, typed value
                foreach (var (at, clicked) in _dots)
                {
                    PointF p = Pt(at);
                    float r = clicked ? 5f : 4.2f;
                    g.FillEllipse(clicked ? t.Gold : t.Background, p.X - r, p.Y - r, 2 * r, 2 * r);
                    g.DrawEllipse(new Pen(t.Ink, 1.2f), p.X - r, p.Y - r, 2 * r, 2 * r);
                }
                if (Cursor.HasValue)
                    DrawCursor(g, t, Pt(Cursor.Value));
                if (_typed.HasValue)
                    DrawTyped(g, t, area, Pt(_typed.Value.At), _typed.Value.Text);

                g.ResetClip();
                g.RestoreTransform();
            }

            private static Pen PenFor(Palette t, Stroke stroke, bool translucent) => stroke switch
            {
                Stroke.Result => new Pen(t.Orange, translucent ? 0.9f : 1.9f) { LineJoin = PenLineJoin.Round, LineCap = PenLineCap.Round },
                Stroke.Faint => new Pen(Color.FromArgb(0xC4, 0xC4, 0xC4), 1f),
                _ => new Pen(t.Ink, translucent ? 0.7f : 1.2f) { LineJoin = PenLineJoin.Round, LineCap = PenLineCap.Round },
            };

            private static void DrawArrow(Graphics g, Palette t, PointF from, PointF to)
            {
                var pen = new Pen(t.Ink, 1.6f) { LineCap = PenLineCap.Round };
                g.DrawLine(pen, from, to);
                float dx = to.X - from.X, dy = to.Y - from.Y, len = (float)Math.Sqrt(dx * dx + dy * dy);
                if (len < 1)
                    return;
                dx /= len;
                dy /= len;
                g.FillPolygon(t.Ink, to,
                    new PointF(to.X - 7 * dx + 4 * dy, to.Y - 7 * dy - 4 * dx),
                    new PointF(to.X - 7 * dx - 4 * dy, to.Y - 7 * dy + 4 * dx));
            }

            private static void DrawCursor(Graphics g, Palette t, PointF tip)
            {
                PointF[] shape =
                {
                    new PointF(0, 0), new PointF(0, 15), new PointF(4, 11.5f), new PointF(7, 17.5f),
                    new PointF(9.5f, 16.5f), new PointF(6.5f, 10.5f), new PointF(11, 10.5f),
                };
                PointF[] points = shape.Select(p => new PointF(tip.X + p.X, tip.Y + p.Y)).ToArray();
                g.FillPolygon(t.Background, points);
                g.DrawPolygon(new Pen(t.Ink, 1.1f) { LineJoin = PenLineJoin.Round }, points);
            }

            private static readonly Font TypedFont = Fin.FinType.Sans(11, true);

            private static void DrawTyped(Graphics g, Palette t, RectangleF area, PointF at, string text)
            {
                text += "|";
                SizeF size = TypedFont.MeasureString(text);
                float x = Math.Max(area.X + 6, Math.Min(at.X + 10, area.Right - size.Width - 18));
                float y = Math.Max(area.Y + 6, at.Y - size.Height - 10);
                var box = new RectangleF(x, y, size.Width + 12, size.Height + 6);
                g.FillRectangle(t.Background, box);
                g.DrawRectangle(new Pen(t.Ink, 1), box);
                g.DrawText(TypedFont, t.Ink, box.X + 6, box.Y + 3, text);
            }

            private static Point3d[] Ring(Point3d c, Vector3d tangent, double r)
            {
                var plane = new Plane(c, tangent);
                return Enumerable.Range(0, 17).Select(i => plane.PointAt(Math.Cos(i * Math.PI / 8) * r, Math.Sin(i * Math.PI / 8) * r)).ToArray();
            }

            private static Point3d[] Resample(Point3d[] line, int n)
            {
                double length = new Polyline(line).Length;
                return Enumerable.Range(0, n + 1).Select(i =>
                {
                    double target = length * i / n, run = 0;
                    for (int k = 0; k + 1 < line.Length; k++)
                    {
                        double seg = line[k].DistanceTo(line[k + 1]);
                        if (run + seg >= target || k + 2 == line.Length)
                            return Lerp(line[k], line[k + 1], seg < 1e-9 ? 0 : Math.Min(1, (target - run) / seg));
                        run += seg;
                    }
                    return line[line.Length - 1];
                }).ToArray();
            }

            private static double Dot(Point3d p, Vector3d v) => p.X * v.X + p.Y * v.Y + p.Z * v.Z;

            private static Point3d Centroid(Point3d[] pts) => new Point3d(pts.Average(p => p.X), pts.Average(p => p.Y), pts.Average(p => p.Z));

            private static Vector3d Newell(Point3d[] pts)
            {
                var n = new Vector3d();
                for (int i = 0; i < pts.Length; i++)
                {
                    Point3d a = pts[i], b = pts[(i + 1) % pts.Length];
                    n.X += (a.Y - b.Y) * (a.Z + b.Z);
                    n.Y += (a.Z - b.Z) * (a.X + b.X);
                    n.Z += (a.X - b.X) * (a.Y + b.Y);
                }
                return n.Unitize() ? n : Vector3d.ZAxis;
            }

            private static Vector3d Unit(Vector3d v)
            {
                v.Unitize();
                return v;
            }

            private static Color Mix(Color dark, Color light, double k)
            {
                float f = (float)Math.Max(0, Math.Min(1, k));
                return new Color(dark.R + (light.R - dark.R) * f, dark.G + (light.G - dark.G) * f, dark.B + (light.B - dark.B) * f);
            }
        }
    }
}
