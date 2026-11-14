using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;
using Rhino.Geometry;

namespace RhinoGotchi.UI
{
    // Icons for the higher levels: curve tools, transforms, surface tools, advanced solids.
    public static partial class LessonIcons
    {
        private static readonly Color Ghost = Color.FromArgb(0xAF, 0xAF, 0xAF);
        private static readonly Color FilletFace = Color.FromArgb(0xFF, 0xE7, 0xC2);
        private static readonly Color FilletDark = Color.FromArgb(0xFF, 0xB8, 0x4D);

        private static void RegisterMore(Dictionary<string, Action<Graphics>> p)
        {
            p["Offset"] = DrawOffset;
            p["Trim"] = DrawTrim;
            p["Join"] = DrawJoin;
            p["Fillet"] = DrawFilletCurve;
            p["Move"] = DrawMove;
            p["Rotate"] = DrawRotate;
            p["Scale"] = DrawScale;
            p["Mirror"] = DrawMirror;
            p["ArrayPolar"] = DrawArrayPolar;
            p["OffsetSrf"] = DrawOffsetSrf;
            p["BlendSrf"] = DrawBlendSrf;
            p["Contour"] = DrawContour;
            p["Patch"] = DrawPatch;
            p["Cap"] = DrawCap;
            p["Shell"] = DrawShell;
            p["BooleanIntersection"] = DrawBooleanIntersection;
            p["ExtrudeSrf"] = DrawExtrudeSrf;
        }

        // ------------------------------------------------------------------ curve tools

        private static void DrawOffset(Graphics g)
        {
            CPlane(g);
            Func<double, Point3d> curve = t => new Point3d(-1.2 + t * 2.4, -0.45 + Math.Sin(t * Math.PI * 1.6) * 0.35, 0);
            Func<double, Point3d> offset = t => OffsetInPlane(curve, t, 0.55);
            Polyline3(g, curve, Profile, 3.4f);
            Polyline3(g, offset, Rail, 3.4f);
            // distance marker
            PointF a = Iso(curve(0.5)), b = Iso(offset(0.5));
            DashedLine(g, a, b, Ink);
            Arrowhead(g, a, b, Ink);
            Arrowhead(g, b, a, Ink);
        }

        private static void DrawTrim(Graphics g)
        {
            CPlane(g);
            // Cutting line (purple), and an orange curve whose end past the cutter is trimmed away.
            Stroke(g, Profile, 3.2f, Iso(0.35, -1.3, 0), Iso(0.35, 1.3, 0));
            Func<double, Point3d> curve = t => new Point3d(-1.3 + t * 2.6, Math.Sin(t * Math.PI) * 0.6 - 0.3, 0);
            Polyline3(g, t => curve(t * 0.635), Rail, 3.4f);
            var gone = Enumerable.Range(0, 21).Select(i => Iso(curve(0.635 + i / 20.0 * 0.365))).ToArray();
            g.DrawLines(new Pen(Ghost, 2.4f) { DashStyle = DashStyles.Dash }, gone);
            // Little X where the piece is removed
            PointF x = Iso(curve(0.85));
            x = new PointF(x.X, x.Y - 9);
            Stroke(g, Color.FromArgb(0xFF, 0x4B, 0x4B), 2.8f, new PointF(x.X - 5, x.Y - 5), new PointF(x.X + 5, x.Y + 5));
            Stroke(g, Color.FromArgb(0xFF, 0x4B, 0x4B), 2.8f, new PointF(x.X + 5, x.Y - 5), new PointF(x.X - 5, x.Y + 5));
        }

        private static void DrawJoin(Graphics g)
        {
            CPlane(g);
            Point3d mid = new Point3d(0.1, 0.1, 0);
            Polyline3(g, t => new Point3d(-1.3 + t * 1.4, -0.6 + Math.Sin(t * Math.PI) * 0.4 + t * 0.7, 0), Profile, 3.4f);
            Polyline3(g, t => new Point3d(0.1 + t * 1.2, 0.1 - Math.Sin(t * Math.PI) * 0.5 - t * 0.6, 0), Rail, 3.4f);
            PointF m = Iso(mid);
            g.DrawEllipse(new Pen(Color.FromArgb(0x58, 0xCC, 0x02), 2.4f), m.X - 8, m.Y - 8, 16, 16);
            Dot(g, m, Ink, 3f);
            Plus(g, new PointF(84, 18), Color.FromArgb(0x58, 0xCC, 0x02));
        }

        private static void DrawFilletCurve(Graphics g)
        {
            CPlane(g);
            // Two lines meeting at a sharp corner; the corner is replaced by an arc.
            const double r = 0.7;
            Point3d corner = new Point3d(0.9, -0.9, 0);
            Point3d endA = new Point3d(-1.2, -0.9, 0), endB = new Point3d(0.9, 1.2, 0);
            Stroke(g, Ink, 3.2f, Iso(endA), Iso(corner.X - r, corner.Y, 0));
            Stroke(g, Ink, 3.2f, Iso(endB), Iso(corner.X, corner.Y + r, 0));
            g.DrawLines(new Pen(Ghost, 2f) { DashStyle = DashStyles.Dash }, Iso(corner.X - r, corner.Y, 0), Iso(corner), Iso(corner.X, corner.Y + r, 0));
            Point3d c = new Point3d(corner.X - r, corner.Y + r, 0);
            Polyline3(g, t => c + new Vector3d(Math.Sin(t * Math.PI / 2) * r, -Math.Cos(t * Math.PI / 2) * r, 0), Rail, 3.8f);
        }

        // ------------------------------------------------------------------ transforms

        private static void DrawMove(Graphics g)
        {
            CPlane(g);
            Point3d from = new Point3d(-0.9, -0.2, 0), to = new Point3d(0.55, 0.15, 0);
            GhostBox(g, from, 0.5, 0.5, 0.6);
            var scene = new Scene();
            scene.Box(to + new Vector3d(-0.25, -0.25, 0), to + new Vector3d(0.25, 0.25, 0.6));
            scene.Flush(g);
            PointF a = Iso(from + new Vector3d(0.3, 0, 0.3)), b = Iso(to + new Vector3d(-0.3, 0, 0.3));
            Stroke(g, Rail, 3f, a, b);
            Arrowhead(g, a, b, Rail);
        }

        private static void DrawRotate(Graphics g)
        {
            CPlane(g);
            Point3d pivot = new Point3d(-0.3, 0.2, 0);
            Func<double, Point3d[]> rect = ang =>
            {
                var pts = new[] { new Point3d(0, -0.2, 0), new Point3d(1.2, -0.2, 0), new Point3d(1.2, 0.2, 0), new Point3d(0, 0.2, 0), new Point3d(0, -0.2, 0) };
                return pts.Select(p => pivot + new Vector3d(p.X * Math.Cos(ang) - p.Y * Math.Sin(ang), p.X * Math.Sin(ang) + p.Y * Math.Cos(ang), 0)).ToArray();
            };
            g.DrawLines(new Pen(Ghost, 2f) { DashStyle = DashStyles.Dash }, rect(-0.9).Select(Iso).ToArray());
            Stroke(g, Rail, 3.2f, rect(0.6).Select(Iso).ToArray());
            Polyline3(g, t => pivot + new Vector3d(Math.Cos(-0.75 + t * 1.25) * 0.85, Math.Sin(-0.75 + t * 1.25) * 0.85, 0), Profile, 2.6f);
            Arrowhead(g, Iso(pivot + new Vector3d(Math.Cos(0.4) * 0.85, Math.Sin(0.4) * 0.85, 0)), Iso(pivot + new Vector3d(Math.Cos(0.5) * 0.85, Math.Sin(0.5) * 0.85, 0)), Profile);
            Dot(g, Iso(pivot), Ink, 3.6f);
        }

        private static void DrawScale(Graphics g)
        {
            CPlane(g);
            Point3d b = new Point3d(-1.1, -1.0, 0);
            Func<double, PointF[]> sq = k => new[] { b, b + new Vector3d(k, 0, 0), b + new Vector3d(k, k, 0), b + new Vector3d(0, k, 0), b }.Select(Iso).ToArray();
            Stroke(g, Rail, 3.2f, sq(2.0));
            g.DrawLines(new Pen(Ghost, 2f) { DashStyle = DashStyles.Dash }, sq(0.9));
            PointF from = Iso(b + new Vector3d(0.9, 0.9, 0)), to = Iso(b + new Vector3d(1.85, 1.85, 0));
            Stroke(g, Profile, 2.6f, from, to);
            Arrowhead(g, from, to, Profile);
            Dot(g, Iso(b), Ink, 3.6f);
        }

        private static void DrawMirror(Graphics g)
        {
            CPlane(g);
            DashedLine(g, Iso(0, -1.5, 0), Iso(0, 1.5, 0), Ink);
            var half = new[] { new Point3d(-0.15, -0.9, 0), new Point3d(-1.1, -0.9, 0), new Point3d(-1.1, -0.2, 0), new Point3d(-0.55, -0.2, 0), new Point3d(-0.55, 0.9, 0), new Point3d(-0.15, 0.9, 0) };
            Stroke(g, Profile, 3.2f, half.Select(Iso).ToArray());
            Stroke(g, Rail, 3.2f, half.Select(p => Iso(-p.X, p.Y, p.Z)).ToArray());
        }

        private static void DrawArrayPolar(Graphics g)
        {
            CPlane(g);
            const int n = 6;
            Polyline3(g, t => new Point3d(Math.Cos(t * Math.PI * 2) * 1.0, Math.Sin(t * Math.PI * 2) * 1.0, 0), Ghost, 1.4f);
            var scene = new Scene();
            for (int i = 0; i < n; i++)
            {
                double a = i * Math.PI * 2 / n + 0.3;
                var c = new Point3d(Math.Cos(a) * 1.0, Math.Sin(a) * 1.0, 0);
                scene.Box(c + new Vector3d(-0.17, -0.17, 0), c + new Vector3d(0.17, 0.17, 0.4));
            }
            scene.Flush(g);
            var first = new Point3d(Math.Cos(0.3), Math.Sin(0.3), 0.2);
            g.DrawEllipse(new Pen(Rail, 2.6f), Iso(first).X - 11, Iso(first).Y - 11, 22, 22);
            Dot(g, Iso(0, 0, 0), Ink, 3.6f);
        }

        // ------------------------------------------------------------------ surface tools

        private static void DrawOffsetSrf(Graphics g)
        {
            Func<double, double, Point3d> s = (u, v) =>
                new Point3d(-1.1 + u * 2.2, -1.1 + v * 2.2, -0.75 + Math.Sin(u * Math.PI) * 0.35);
            var scene = new Scene();
            scene.Surface(s, 14, 6);
            scene.Surface((u, v) => s(u, v) + Vector3d.ZAxis * 0.85, 14, 6, FilletFace, FilletDark);
            scene.Flush(g);
            PointF a = Iso(s(0.5, 1.0) + new Vector3d(0.1, 0.1, 0)), b = Iso(s(0.5, 1.0) + new Vector3d(0.1, 0.1, 0.85));
            Stroke(g, Ink, 2.2f, a, b);
            Arrowhead(g, a, b, Ink);
        }

        private static void DrawBlendSrf(Graphics g)
        {
            // Two separate panels at different heights; an orange blend bridges them smoothly.
            var scene = new Scene();
            scene.Surface((u, v) => new Point3d(-1.3 + u * 0.8, -1.0 + v * 2.0, -0.7), 3, 4);
            scene.Surface((u, v) => new Point3d(0.5 + u * 0.8, -1.0 + v * 2.0, 0.5), 3, 4);
            scene.Surface((u, v) =>
            {
                double s = u * u * (3 - 2 * u);
                return new Point3d(-0.5 + u * 1.0, -1.0 + v * 2.0, -0.7 + s * 1.2);
            }, 10, 4, FilletFace, FilletDark);
            scene.Flush(g);
            Polyline3(g, v => new Point3d(-0.5, -1.0 + v * 2.0, -0.7), Rail, 3f);
            Polyline3(g, v => new Point3d(0.5, -1.0 + v * 2.0, 0.5), Rail, 3f);
        }

        private static void DrawContour(Graphics g)
        {
            Func<double, double, Point3d> blob = (u, v) =>
            {
                double a = u * Math.PI * 2, z = -1.0 + v * 2.0;
                double r = 0.55 + 0.35 * Math.Sin(v * Math.PI * 1.2 + 0.3);
                return new Point3d(Math.Cos(a) * r, Math.Sin(a) * r, z);
            };
            var scene = new Scene();
            scene.Surface(blob, 20, 12);
            scene.Flush(g);
            for (int k = 1; k <= 5; k++)
            {
                double v = k / 6.0;
                Polyline3(g, u => blob(u, v), Rail, 2.4f);
            }
        }

        private static void DrawPatch(Graphics g)
        {
            Func<double, Point3d> loop = t =>
            {
                double a = t * Math.PI * 2;
                double r = 1.05 + 0.15 * Math.Sin(a * 3);
                return new Point3d(Math.Cos(a) * r, Math.Sin(a) * r, Math.Sin(a * 2) * 0.35);
            };
            var scene = new Scene();
            scene.Surface((t, s) =>
            {
                Point3d p = loop(t);
                double k = 1 - s;
                return new Point3d(p.X * k, p.Y * k, p.Z * k + Math.Sin(s * Math.PI / 2) * 0.25);
            }, 28, 6);
            scene.Flush(g);
            Polyline3(g, loop, Rail, 3.4f, 60);
        }

        // ------------------------------------------------------------------ advanced solids

        private static void DrawCap(Graphics g)
        {
            var scene = new Scene();
            scene.Surface((u, v) => new Point3d(Math.Cos(u * Math.PI * 2) * 0.9, Math.Sin(u * Math.PI * 2) * 0.9, -0.9 + v * 1.4), 24, 2);
            scene.Flush(g);
            // The cap: a disc lifted above the open end, about to drop in.
            var cap = new Scene();
            var pts = Enumerable.Range(0, 32).Select(i => new Point3d(Math.Cos(i * Math.PI / 16) * 0.9, Math.Sin(i * Math.PI / 16) * 0.9, 0.95)).ToArray();
            cap.Polygon(pts, FilletFace, FilletDark);
            cap.Flush(g);
            Polyline3(g, t => new Point3d(Math.Cos(t * Math.PI * 2) * 0.9, Math.Sin(t * Math.PI * 2) * 0.9, 0.5), Edge, 1.6f);
            PointF a = Iso(0, 0, 1.45), b = Iso(0, 0, 1.05);
            Stroke(g, Rail, 2.6f, a, b);
            Arrowhead(g, a, b, Rail);
        }

        private static void DrawShell(Graphics g)
        {
            // A box with its top removed and walls of visible thickness.
            const double t = 0.22;
            var scene = new Scene();
            scene.Box(new Point3d(-1, -1, -0.8), new Point3d(1, 1, 0.6));
            scene.Flush(g);
            // Hollow: inner top opening, then the two inner walls you can see.
            Point3d a = new Point3d(-1 + t, -1 + t, 0.6), b = new Point3d(1 - t, -1 + t, 0.6), c = new Point3d(1 - t, 1 - t, 0.6), d = new Point3d(-1 + t, 1 - t, 0.6);
            double floor = -0.8 + t;
            g.FillPolygon(Color.FromArgb(0x2B, 0x9C, 0xD8), Iso(a), Iso(b), Iso(c), Iso(d));
            g.FillPolygon(Color.FromArgb(0x49, 0xB4, 0xEC), Iso(a), Iso(b), Iso(b.X, b.Y, floor), Iso(a.X, a.Y, floor));
            g.FillPolygon(Color.FromArgb(0x6F, 0xC6, 0xF2), Iso(a), Iso(d), Iso(d.X, d.Y, floor), Iso(a.X, a.Y, floor));
            g.DrawPolygon(RoundPen(Edge, 1.6f), new[] { Iso(a), Iso(b), Iso(c), Iso(d) });
            Stroke(g, Rail, 3f, Iso(c), Iso(1, 1, 0.6));
        }

        private static void DrawBooleanIntersection(Graphics g)
        {
            var ca = new PointF(38, 48);
            var cb = new PointF(62, 56);
            const float r = 26;
            var dashed = new Pen(Ghost, 2.2f) { DashStyle = DashStyles.Dash };
            g.DrawEllipse(dashed, ca.X - r, ca.Y - r, r * 2, r * 2);
            g.DrawEllipse(dashed, cb.X - r, cb.Y - r, r * 2, r * 2);
            List<PointF> lens = ArcInside(ca, cb, r).Concat(ArcInside(cb, ca, r)).ToList();
            g.FillPolygon(SurfDark, lens.ToArray());
            g.DrawPolygon(RoundPen(Edge, 2.4f), lens.ToArray());
            // highlight
            g.FillEllipse(SurfLight, (ca.X + cb.X) / 2 - 6, (ca.Y + cb.Y) / 2 - 12, 9, 12);
            // "∩" badge
            g.FillEllipse(Rail, 75, 77, 18, 18);
            g.DrawArc(new Pen(Colors.White, 2.6f) { LineCap = PenLineCap.Round }, 79.5f, 81, 9, 10, 180, 180);
            g.DrawLine(new Pen(Colors.White, 2.6f) { LineCap = PenLineCap.Round }, 79.5f, 86, 79.5f, 90);
            g.DrawLine(new Pen(Colors.White, 2.6f) { LineCap = PenLineCap.Round }, 88.5f, 86, 88.5f, 90);
        }

        private static void DrawExtrudeSrf(Graphics g)
        {
            // A closed, flat region pushed up into a solid.
            Func<double, Point3d> edge = t =>
            {
                double a = t * Math.PI * 2;
                double r = 0.95 + 0.2 * Math.Sin(a * 4);
                return new Point3d(Math.Cos(a) * r, Math.Sin(a) * r, -0.8);
            };
            var scene = new Scene();
            scene.Surface((t, v) => edge(t) + Vector3d.ZAxis * (v * 1.2), 32, 2);
            scene.Polygon(Enumerable.Range(0, 48).Select(i => edge(i / 48.0) + Vector3d.ZAxis * 1.2).ToArray());
            scene.Flush(g);
            Polyline3(g, edge, Profile, 3.2f, 64);
            PointF a = Iso(1.5, 0.3, -0.5), b = Iso(1.5, 0.3, 0.8);
            Stroke(g, Rail, 2.8f, a, b);
            Arrowhead(g, a, b, Rail);
        }

        // ------------------------------------------------------------------ helpers

        private static Point3d OffsetInPlane(Func<double, Point3d> curve, double t, double d)
        {
            Vector3d tan = curve(Math.Min(1, t + 0.01)) - curve(Math.Max(0, t - 0.01));
            tan.Unitize();
            var n = new Vector3d(-tan.Y, tan.X, 0);
            return curve(t) + n * d;
        }

        private static void GhostBox(Graphics g, Point3d baseCenter, double dx, double dy, double dz)
        {
            var pen = new Pen(Ghost, 1.8f) { DashStyle = DashStyles.Dash };
            double x0 = baseCenter.X - dx / 2, x1 = baseCenter.X + dx / 2, y0 = baseCenter.Y - dy / 2, y1 = baseCenter.Y + dy / 2, z0 = baseCenter.Z, z1 = baseCenter.Z + dz;
            var top = new[] { Iso(x0, y0, z1), Iso(x1, y0, z1), Iso(x1, y1, z1), Iso(x0, y1, z1), Iso(x0, y0, z1) };
            g.DrawLines(pen, top);
            g.DrawLines(pen, Iso(x1, y0, z0), Iso(x1, y1, z0), Iso(x0, y1, z0));
            g.DrawLine(pen, Iso(x1, y0, z0), Iso(x1, y0, z1));
            g.DrawLine(pen, Iso(x1, y1, z0), Iso(x1, y1, z1));
            g.DrawLine(pen, Iso(x0, y1, z0), Iso(x0, y1, z1));
        }

        /// <summary>The part of circle <paramref name="c"/> that lies inside the circle at <paramref name="other"/>, counter-clockwise.</summary>
        private static IEnumerable<PointF> ArcInside(PointF c, PointF other, float r)
        {
            // Start sampling on the far side so the inside part is one contiguous run.
            double start = Math.Atan2(c.Y - other.Y, c.X - other.X);
            for (int i = 0; i <= 120; i++)
            {
                double a = start + i / 120.0 * Math.PI * 2;
                var p = new PointF(c.X + (float)Math.Cos(a) * r, c.Y + (float)Math.Sin(a) * r);
                float dx = p.X - other.X, dy = p.Y - other.Y;
                if (dx * dx + dy * dy <= r * r + 0.01f)
                    yield return p;
            }
        }
    }
}
