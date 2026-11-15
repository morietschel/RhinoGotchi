using System;
using System.Collections.Generic;
using Rhino.Geometry;

namespace RhinoGotchi.Tutorials
{
    /// <summary>
    /// Geometry the tutorials share: the roof patch, the whole roof and their standalone stand-ins,
    /// all lifted above the real roof so learners never pick the original by accident.
    /// </summary>
    public static class TutorialGeometry
    {
        /// <summary>Tutorial copies of roof geometry sit this far above the real roof (assumed, metres).</summary>
        public const double Lift = 4.0;

        /// <summary>Side length of the roof patch used by tutorials 1–3 and 7 (assumed, metres).</summary>
        public const double PatchSize = 6.0;

        public static Transform LiftTransform => Transform.Translation(0, 0, Lift);

        /// <summary>The wavy roof patch, lifted. Standalone: a made-up 6 × 6 m wavy patch at the origin.</summary>
        public static Surface Patch(TutorialContext context)
        {
            if (context.Reference == null)
                return StandalonePatch();
            Surface patch = context.Reference.RoofPatch(PatchSize);
            patch.Transform(LiftTransform);
            return patch;
        }

        /// <summary>The whole trimmed roof, lifted. Standalone: a made-up 24 m wavy disc around the origin.</summary>
        public static Brep Roof(TutorialContext context)
        {
            if (context.Reference == null)
                return StandaloneRoof();
            Brep roof = context.Reference.RoofBrep;
            roof.Transform(LiftTransform);
            return roof;
        }

        /// <summary>The building centre on the ground. Standalone: the origin.</summary>
        public static Point3d Centre(TutorialContext context) =>
            context.Reference?.BuildingCentre ?? Point3d.Origin;

        /// <summary>
        /// A grid of points on a surface: <paramref name="rows"/> rows along the U direction, each with
        /// <paramref name="perRow"/> points along V, evenly spaced in parameter.
        /// </summary>
        public static List<Point3d[]> Grid(Surface surface, int rows, int perRow)
        {
            Interval u = surface.Domain(0), v = surface.Domain(1);
            var grid = new List<Point3d[]>();
            for (int i = 0; i < rows; i++)
            {
                var row = new Point3d[perRow];
                for (int j = 0; j < perRow; j++)
                    row[j] = surface.PointAt(u.ParameterAt(i / (double)(rows - 1)), v.ParameterAt(j / (double)(perRow - 1)));
                grid.Add(row);
            }
            return grid;
        }

        /// <summary>The four boundary curves of an untrimmed surface.</summary>
        public static IEnumerable<Curve> Boundary(Surface surface)
        {
            Interval u = surface.Domain(0), v = surface.Domain(1);
            yield return surface.IsoCurve(0, v.Min); // runs along U at the V start
            yield return surface.IsoCurve(0, v.Max);
            yield return surface.IsoCurve(1, u.Min); // runs along V at the U start
            yield return surface.IsoCurve(1, u.Max);
        }

        /// <summary>
        /// The patch thickened downward into a closed solid. Returns null if Rhino can't offset it.
        /// </summary>
        public static Brep? Thicken(Surface surface, double thickness, double tolerance)
        {
            Brep brep = surface.ToBrep();
            Interval u = surface.Domain(0), v = surface.Domain(1);
            Vector3d normal = surface.NormalAt(u.Mid, v.Mid);
            double distance = normal.Z > 0 ? -thickness : thickness;
            Brep[] solids = Brep.CreateOffsetBrep(brep, distance, true, false, tolerance, out _, out _);
            return solids.Length > 0 ? solids[0] : null;
        }

        private static Surface StandalonePatch()
        {
            const int n = 7;
            var points = new List<Point3d>();
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    double x = PatchSize * i / (n - 1), y = PatchSize * j / (n - 1);
                    double z = 0.6 * Math.Sin(x * 0.9) * Math.Cos(y * 0.7) + 0.08 * x;
                    points.Add(new Point3d(x, y, z));
                }
            }
            return NurbsSurface.CreateFromPoints(points, n, n, 3, 3);
        }

        private static Brep StandaloneRoof()
        {
            const int n = 13;
            const double size = 24.0;
            var points = new List<Point3d>();
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    double x = size * i / (n - 1) - size / 2, y = size * j / (n - 1) - size / 2;
                    double r = Math.Sqrt(x * x + y * y);
                    double z = 5 - 0.012 * r * r + 0.5 * Math.Sin(Math.Atan2(y, x) * 5) * r / 12;
                    points.Add(new Point3d(x, y, z));
                }
            }
            Surface s = NurbsSurface.CreateFromPoints(points, n, n, 3, 3);
            return s.ToBrep();
        }
    }
}
