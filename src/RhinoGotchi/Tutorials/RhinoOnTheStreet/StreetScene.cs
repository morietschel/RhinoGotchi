using System;
using System.Collections.Generic;
using Rhino.Geometry;

namespace RhinoGotchi.Tutorials.RhinoOnTheStreet
{
    /// <summary>
    /// The fixed geometry of the tutorial: a street that curves away from the
    /// viewer and ends under an archway. World units, Z up, street on the XY plane.
    /// </summary>
    internal static class StreetScene
    {
        /// <summary>Arch plane (Y), radius of the arch centreline and height where the arc springs.</summary>
        public const double ArchY = 24, ArchRadius = 2.2, ArchSpring = 3.2;
        public const double StreetHalfWidth = 0.85;
        public const double PillarHalfWidth = 0.3, ArchHalfDepth = 0.4;

        /// <summary>Control points of the street. A degree-3 Curve through these clicks matches the ghost exactly.</summary>
        public static readonly Point3d[] StreetControlPoints =
        {
            new Point3d(0, 0.5, 0), new Point3d(3.6, 6, 0), new Point3d(-3.6, 12, 0),
            new Point3d(0, 18, 0), new Point3d(0, ArchY, 0),
        };

        public static readonly Point3d LeftBase = new Point3d(-ArchRadius, ArchY, 0);
        public static readonly Point3d LeftTop = new Point3d(-ArchRadius, ArchY, ArchSpring);
        public static readonly Point3d RightBase = new Point3d(ArchRadius, ArchY, 0);
        public static readonly Point3d RightTop = new Point3d(ArchRadius, ArchY, ArchSpring);
        public static readonly Point3d Crown = new Point3d(0, ArchY, ArchSpring + ArchRadius);

        /// <summary>The curve Rhino's Curve command makes from the five control points (clamped, uniform, degree 3).</summary>
        public static readonly NurbsCurve StreetCurve = NurbsCurve.Create(false, 3, StreetControlPoints);

        public static readonly Point3d[] StreetSamples = Sample(StreetCurve, 90);

        public static readonly Point3d[] StreetLeft = Offset(StreetSamples, StreetHalfWidth);
        public static readonly Point3d[] StreetRight = Offset(StreetSamples, -StreetHalfWidth);

        /// <summary>Ghost street surface, drawn see-through until the player builds it.</summary>
        public static readonly Mesh StreetMesh = RibbonMesh(StreetSamples.Length);

        /// <summary>Ghost arch solid.</summary>
        public static readonly Brep? ArchGhost = ArchBrep(1);

        /// <summary>Walk path for the finale: lead-in, the street, then straight on past the arch.</summary>
        public static readonly Point3d[] WalkPath = BuildWalk();
        private static readonly double[] WalkLengths = Cumulative(WalkPath);
        public static double WalkLength => WalkLengths[WalkLengths.Length - 1];
        public static double WalkStreetStart => WalkLengths[10];

        public static Point3d WalkAt(double s)
        {
            s = Math.Max(0, Math.Min(WalkLength, s));
            int i = 1;
            while (i < WalkLengths.Length - 1 && WalkLengths[i] < s) i++;
            double span = WalkLengths[i] - WalkLengths[i - 1];
            double t = span > 0 ? (s - WalkLengths[i - 1]) / span : 0;
            return WalkPath[i - 1] + (WalkPath[i] - WalkPath[i - 1]) * t;
        }

        /// <summary>Street surface as a mesh, covering the first <paramref name="count"/> samples (for the paving animation).</summary>
        public static Mesh RibbonMesh(int count)
        {
            count = Math.Max(2, Math.Min(StreetSamples.Length, count));
            var mesh = new Mesh();
            for (int i = 0; i < count; i++)
            {
                mesh.Vertices.Add(StreetLeft[i]);
                mesh.Vertices.Add(StreetRight[i]);
            }
            for (int i = 0; i < count - 1; i++)
                mesh.Faces.AddFace(2 * i, 2 * i + 1, 2 * i + 3, 2 * i + 2);
            mesh.Normals.ComputeNormals();
            return mesh;
        }

        /// <summary>The finished street as a lofted surface between its two edges.</summary>
        public static Brep? StreetBrep()
        {
            Curve? left = Curve.CreateInterpolatedCurve(StreetLeft, 3);
            Curve? right = Curve.CreateInterpolatedCurve(StreetRight, 3);
            if (left == null || right == null) return null;
            Brep[] lofts = Brep.CreateFromLoft(new[] { left, right }, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
            return lofts.Length > 0 ? lofts[0] : null;
        }

        /// <summary>The arch as a solid. <paramref name="growth"/> 0..1 thickens it from the centreline (ExtrudeCrv animation).</summary>
        public static Brep? ArchBrep(double growth)
        {
            growth = Math.Max(0.02, Math.Min(1, growth));
            double hw = PillarHalfWidth * growth, dh = ArchHalfDepth * growth;
            List<Point3d> profile = ArchProfile(hw, ArchY - dh);
            var curve = new PolylineCurve(profile);
            Surface? side = Surface.CreateExtrusion(curve, new Vector3d(0, 2 * dh, 0));
            Brep? brep = side?.ToBrep();
            return brep?.CapPlanarHoles(0.001) ?? brep;
        }

        /// <summary>Closed outline of the arch (two pillars and a round top) in the plane Y = <paramref name="y"/>.</summary>
        public static List<Point3d> ArchProfile(double hw, double y)
        {
            double ro = ArchRadius + hw, ri = ArchRadius - hw;
            var pts = new List<Point3d> { new Point3d(-ro, y, 0) };
            for (int i = 0; i <= 24; i++)
            {
                double a = Math.PI - i / 24.0 * Math.PI;
                pts.Add(new Point3d(ro * Math.Cos(a), y, ArchSpring + ro * Math.Sin(a)));
            }
            pts.Add(new Point3d(ro, y, 0));
            pts.Add(new Point3d(ri, y, 0));
            for (int i = 0; i <= 24; i++)
            {
                double a = i / 24.0 * Math.PI;
                pts.Add(new Point3d(ri * Math.Cos(a), y, ArchSpring + ri * Math.Sin(a)));
            }
            pts.Add(new Point3d(-ri, y, 0));
            pts.Add(pts[0]);
            return pts;
        }

        private static Point3d[] Sample(Curve c, int n)
        {
            var pts = new Point3d[n + 1];
            for (int i = 0; i <= n; i++)
                pts[i] = c.PointAt(c.Domain.ParameterAt(i / (double)n));
            return pts;
        }

        private static Point3d[] Offset(Point3d[] c, double d)
        {
            var o = new Point3d[c.Length];
            for (int i = 0; i < c.Length; i++)
            {
                Vector3d t = c[Math.Min(c.Length - 1, i + 1)] - c[Math.Max(0, i - 1)];
                t.Z = 0;
                t.Unitize();
                o[i] = c[i] + new Vector3d(-t.Y, t.X, 0) * d;
            }
            return o;
        }

        private static Point3d[] BuildWalk()
        {
            var a = new List<Point3d>();
            for (int i = 0; i <= 10; i++) a.Add(new Point3d(0, -14 + 14.5 * i / 10, 0));
            for (int i = 1; i < StreetSamples.Length; i++) a.Add(StreetSamples[i]);
            for (int i = 1; i <= 24; i++) a.Add(new Point3d(0, ArchY + 2 * i, 0));
            return a.ToArray();
        }

        private static double[] Cumulative(Point3d[] p)
        {
            var c = new double[p.Length];
            for (int i = 1; i < p.Length; i++) c[i] = c[i - 1] + p[i].DistanceTo(p[i - 1]);
            return c;
        }
    }
}
