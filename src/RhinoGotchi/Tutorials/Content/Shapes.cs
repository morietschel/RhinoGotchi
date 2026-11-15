using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;

namespace RhinoGotchi.Tutorials.Content
{
    /// <summary>
    /// The geometry the cards set up and check against, taken from the Klosterneuburg model (with a
    /// made-up stand-in for each when the model isn't open). Measured from the model: plates
    /// 0.95 × 0.29 × 0.020 m with Ø 0.020 m dowels; handrail Ø 0.042 m; 34 glass panels on a ring of
    /// about 10.2 m radius; radial beams every 15° (8 long + 16 short); the roof edge strip about
    /// 0.17 m deep; joists about 0.49 m apart; the terrace ramp rising about 0.12 m.
    /// Roof copies are lifted 4 m and the detail work is built east of the building, so the learner
    /// never picks an original by mistake.
    /// </summary>
    internal static class Shapes
    {
        // ---- 1 plate ----
        public const double PlateWidth = 0.29, PlateLength = 0.95, PlateThickness = 0.020;
        public const double HoleRadius = 0.010, CutterHeight = 0.040;
        public static readonly Point3d PlateCorner = new Point3d(45, 5, 0);
        public static readonly Point2d[] Holes =
        {
            new Point2d(0.075, 0.10), new Point2d(0.215, 0.10),
            new Point2d(0.075, 0.85), new Point2d(0.215, 0.85),
        };

        public static BoundingBox PlateBox => new BoundingBox(PlateCorner, PlateCorner + new Vector3d(PlateWidth, PlateLength, PlateThickness));

        public static Point3d HoleBase(int i) => PlateCorner + new Vector3d(Holes[i].X, Holes[i].Y, -0.01);

        public static Brep Cutter(int i) =>
            new Cylinder(new Circle(new Plane(HoleBase(i), Vector3d.ZAxis), HoleRadius), CutterHeight).ToBrep(true, true);

        public static double HoleVolume => Math.PI * HoleRadius * HoleRadius * PlateThickness;

        // ---- 2 and 5 handrail ----
        public static readonly Vector3d DetailShift = new Vector3d(25, 0, 0);
        public const double RailRadius = 0.021, ReturnGap = 0.15, ReturnLength = 0.30;
        public const double BarHeight = 0.90, BarSpacing = 0.12;

        // ---- 3 glass ----
        public const double GlassThickness = 0.024, GlassShift = 2.0;

        // ---- 4 setting-out ----
        public const int RadialCount = 24;
        public const double PlaneLength = 16, PlaneHeight = 12;

        // ---- 6 contour ----
        public const double ContourThickness = 0.30, ContourShift = 8.0, ContourSpacing = 0.5;

        // ---- 8 edge beam ----
        public const double EdgeBeamWidth = 0.20, EdgeBeamDepth = 0.17;

        // ---- 9 ramp ----
        public const double RampLift = 1.0;

        // ---- 10 joists ----
        public const double JoistWidth = 0.06, JoistDepth = 0.20, JoistSpacing = 0.5, FlowBaseShift = 8.0;
        public const int JoistCount = 12;

        // ================================================================ helpers

        public static Brep? AsBrep(GeometryBase? g) => g switch
        {
            Brep b => b,
            Extrusion e => e.ToBrep(),
            Surface s => s.ToBrep(),
            _ => null,
        };

        public static double Bearing(Point3d p, Point3d centre) => Math.Atan2(p.Y - centre.Y, p.X - centre.X) * 180 / Math.PI;

        public static double PlanDistance(Point3d p, Point3d centre) =>
            Math.Sqrt((p.X - centre.X) * (p.X - centre.X) + (p.Y - centre.Y) * (p.Y - centre.Y));

        public static Point3d Mid(Curve c)
        {
            c.NormalizedLengthParameter(0.5, out double t);
            return c.PointAt(t);
        }

        /// <summary>An upright plane surface from <paramref name="start"/> along a bearing (degrees from east).</summary>
        public static Brep UprightPlane(Point3d start, double bearing, double length, double height)
        {
            var dir = new Vector3d(Math.Cos(bearing * Math.PI / 180), Math.Sin(bearing * Math.PI / 180), 0);
            return new PlaneSurface(new Plane(start, dir, Vector3d.ZAxis), new Interval(0, length), new Interval(0, height)).ToBrep();
        }

        // ---- glass ----

        /// <summary>The outer face of the east-facing glass panel, moved 2 m outward. Standalone: a made-up curved panel.</summary>
        public static Brep EastGlassFace(TutorialContext context)
        {
            Point3d centre = TutorialGeometry.Centre(context);
            Brep? face = null;
            if (context.Reference != null)
            {
                var panels = context.Reference.GlassPanels.Select(AsBrep).Where(b => b != null).Cast<Brep>().ToList();
                Brep? east = panels.OrderBy(b => Math.Abs(Bearing(b.GetBoundingBox(true).Center, centre))).FirstOrDefault();
                if (east != null)
                {
                    // Of the two big faces, the one farther from the centre.
                    face = east.Faces
                        .Select(f => f.DuplicateFace(false))
                        .OrderByDescending(f => AreaMassProperties.Compute(f)?.Area ?? 0)
                        .Take(2)
                        .OrderByDescending(f => PlanDistance(AreaMassProperties.Compute(f)?.Centroid ?? Point3d.Origin, centre))
                        .FirstOrDefault();
                }
            }
            if (face == null)
            {
                var profile = new LineCurve(new Point3d(10.25, 0, 0), new Point3d(10.25, 0, 3));
                face = RevSurface.Create(profile, new Line(Point3d.Origin, Vector3d.ZAxis), -8 * Math.PI / 180, 8 * Math.PI / 180).ToBrep();
            }
            Point3d mid = AreaMassProperties.Compute(face)?.Centroid ?? face.GetBoundingBox(true).Center;
            var outward = new Vector3d(mid.X - centre.X, mid.Y - centre.Y, 0);
            if (outward.Unitize())
                face.Transform(Transform.Translation(outward * GlassShift));
            return face;
        }

        // ---- roof edge ----

        /// <summary>The lifted roof's border, closed.</summary>
        public static Curve? RoofBorder(Brep roof) =>
            Curve.JoinCurves(roof.DuplicateNakedEdgeCurves(true, false)).OrderByDescending(c => c.GetLength()).FirstOrDefault();

        /// <summary>The point on a closed curve whose plan direction from the centre is closest to a bearing.</summary>
        public static Point3d PointAtBearing(Curve curve, Point3d centre, double degrees)
        {
            double target = degrees * Math.PI / 180;
            double[] ts = curve.DivideByCount(720, true) ?? new[] { curve.Domain.Min };
            Point3d best = curve.PointAt(ts[0]);
            double bestDiff = double.MaxValue;
            foreach (double t in ts)
            {
                Point3d p = curve.PointAt(t);
                double diff = Math.Abs(Math.IEEERemainder(Math.Atan2(p.Y - centre.Y, p.X - centre.X) - target, 2 * Math.PI));
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    best = p;
                }
            }
            return best;
        }

        /// <summary>The east quarter of the lifted roof edge (between the −45° and 45° bearings), running south to north.</summary>
        public static Curve? EdgeRail(TutorialContext context)
        {
            Curve? border = RoofBorder(TutorialGeometry.Roof(context));
            if (border == null)
                return null;
            Point3d centre = TutorialGeometry.Centre(context);
            Point3d se = PointAtBearing(border, centre, -45), ne = PointAtBearing(border, centre, 45);
            border.ClosestPoint(se, out double t1);
            border.ClosestPoint(ne, out double t2);
            Curve? rail = border.Split(new[] { t1, t2 }).OrderBy(c => c.GetLength()).FirstOrDefault();
            if (rail != null && rail.PointAtStart.DistanceTo(se) > rail.PointAtEnd.DistanceTo(se))
                rail.Reverse();
            return rail;
        }

        /// <summary>A W × D rectangle square to the rail at its start, with the rail at one corner.</summary>
        public static Curve? EdgeSection(Curve rail)
        {
            if (!rail.PerpendicularFrameAt(rail.Domain.Min, out Plane frame))
                return null;
            return new Rectangle3d(frame, EdgeBeamWidth, EdgeBeamDepth).ToNurbsCurve();
        }

        // ---- ramp ----

        /// <summary>The ramp top's two long edges, lifted 1 m, each running from the low end. Standalone: two arcs.</summary>
        public static (Curve A, Curve B) RampRails(TutorialContext context)
        {
            Brep? ramp = context.Reference?.Ramp.Select(AsBrep).FirstOrDefault();
            BrepFace? top = ramp?.Faces
                .Where(f => f.NormalAt(f.Domain(0).Mid, f.Domain(1).Mid).Z > 0.5)
                .OrderByDescending(f => AreaMassProperties.Compute(f.DuplicateFace(false))?.Area ?? 0)
                .FirstOrDefault();

            List<Curve> rails;
            if (top != null)
            {
                rails = top.DuplicateFace(false).DuplicateEdgeCurves(true).OrderByDescending(c => c.GetLength()).Take(2).ToList();
                foreach (Curve c in rails)
                    c.Transform(Transform.Translation(0, 0, RampLift));
            }
            else
            {
                rails = new List<Curve>();
                foreach (double r in new[] { 8.0, 10.5 })
                {
                    rails.Add(Curve.CreateInterpolatedCurve(Enumerable.Range(0, 7).Select(i =>
                    {
                        double a = i / 6.0 * Math.PI / 3;
                        return new Point3d(r * Math.Cos(a), r * Math.Sin(a), RampLift - 0.232 + 0.127 * i / 6.0);
                    }), 3));
                }
            }
            foreach (Curve c in rails)
                if (c.PointAtStart.Z > c.PointAtEnd.Z + 1e-6 || (Math.Abs(c.PointAtStart.Z - c.PointAtEnd.Z) <= 1e-6 && c.PointAtStart.X > c.PointAtEnd.X))
                    c.Reverse();
            return (rails[0], rails[1]);
        }

        /// <summary>Start, middle and end of each ramp edge, each pair levelled across the width (no sideways tilt).</summary>
        public static (Point3d[] A, Point3d[] B) RampPoints(TutorialContext context)
        {
            var (a, b) = RampRails(context);
            Point3d[] pa = ThreePoints(a), pb = ThreePoints(b);
            for (int i = 0; i < 3; i++)
            {
                double z = (pa[i].Z + pb[i].Z) / 2;
                pa[i].Z = z;
                pb[i].Z = z;
            }
            return (pa, pb);
        }

        private static Point3d[] ThreePoints(Curve rail)
        {
            rail.NormalizedLengthParameter(0.5, out double mid);
            return new[] { rail.PointAtStart, rail.PointAt(mid), rail.PointAtEnd };
        }

        public static Curve Interp(IEnumerable<Point3d> points) => Curve.CreateInterpolatedCurve(points, 3, CurveKnotStyle.Chord);

        // ---- handrail ----

        /// <summary>
        /// Five points along the top of the model's handrail centreline (low to high), the level return's
        /// start (a little higher, so the bend is one clean arc) and end, and the real rail as a ghost,
        /// all moved 25 m east. Standalone: a made-up curved run and no ghost.
        /// </summary>
        public static (List<Point3d> Points, Point3d ReturnStart, Point3d ReturnEnd, Brep? Ghost) HandrailRun(TutorialContext context)
        {
            List<Point3d>? centre = null;
            Brep? ghost = null;
            if (context.Reference != null)
            {
                foreach (GeometryBase g in context.Reference.StairRails)
                {
                    Brep? b = AsBrep(g);
                    if (b == null || b.Faces.Count != 5)
                        continue;
                    centre = TubeCentreline(b, 11);
                    if (centre == null)
                        continue;
                    ghost = b;
                    break;
                }
            }

            List<Point3d> run;
            if (centre != null)
            {
                if (centre[0].Z > centre[centre.Count - 1].Z)
                    centre.Reverse();
                run = centre.Skip(centre.Count - 5).ToList();
            }
            else
            {
                run = Enumerable.Range(0, 5).Select(i =>
                {
                    double a = i / 4.0 * 0.9;
                    return new Point3d(3 * Math.Cos(a), 3 * Math.Sin(a), 0.3 * i);
                }).ToList();
            }

            Vector3d tangent = Interp(run).TangentAtEnd;
            var along = new Vector3d(tangent.X, tangent.Y, 0);
            double slope = Math.Atan2(tangent.Z, along.Length);
            along.Unitize();
            Point3d top = run[run.Count - 1];
            Point3d returnStart = top + along * ReturnGap + new Vector3d(0, 0, ReturnGap * Math.Tan(Math.Max(0, slope) / 2));
            Point3d returnEnd = returnStart + along * ReturnLength;

            var move = Transform.Translation(DetailShift);
            run = run.Select(p => { p.Transform(move); return p; }).ToList();
            returnStart.Transform(move);
            returnEnd.Transform(move);
            ghost?.Transform(move);
            return (run, returnStart, returnEnd, ghost);
        }

        /// <summary>The finished handrail centreline: run, curvature blend and level return, joined.</summary>
        public static Curve HandrailCentreline(TutorialContext context)
        {
            var hr = HandrailRun(context);
            Curve slope = Interp(hr.Points);
            var ret = new LineCurve(hr.ReturnStart, hr.ReturnEnd);
            Curve? blend = Curve.CreateBlendCurve(slope, slope.Domain.Max, false, BlendContinuity.Curvature,
                                                  ret, ret.Domain.Min, true, BlendContinuity.Curvature);
            if (blend == null)
                return slope;
            Curve[] joined = Curve.JoinCurves(new Curve[] { slope, blend, ret }, 0.001);
            return joined.Length == 1 ? joined[0] : slope;
        }

        // The centre line of a round tube: the centres of its circular sections, where the face exists.
        private static List<Point3d>? TubeCentreline(Brep tube, int count)
        {
            BrepFace face = tube.Faces.OrderByDescending(f => AreaMassProperties.Compute(f.DuplicateFace(false))?.Area ?? 0).First();
            Surface s = face.UnderlyingSurface();
            NurbsSurface ns = s.ToNurbsSurface();
            int around = ns.Degree(0) == 2 ? 0 : 1;
            int along = 1 - around;
            Interval run = s.Domain(along), ring = s.Domain(around);

            double lo = double.NaN, hi = double.NaN;
            for (int i = 0; i <= 400; i++)
            {
                double t = run.ParameterAt(i / 400.0);
                double u = along == 0 ? t : ring.Mid, v = along == 0 ? ring.Mid : t;
                if (face.IsPointOnFace(u, v) == PointFaceRelation.Exterior)
                    continue;
                if (double.IsNaN(lo))
                    lo = t;
                hi = t;
            }
            if (double.IsNaN(lo) || hi <= lo)
                return null;

            var points = new List<Point3d>();
            for (int i = 0; i < count; i++)
            {
                double t = lo + (hi - lo) * i / (count - 1);
                Curve? section = s.IsoCurve(around, t);
                if (section == null || !section.TryGetCircle(out Circle circle, 1e-4))
                    return null;
                points.Add(circle.Center);
            }
            return points;
        }

        // ---- joists ----

        /// <summary>A flat copy of the patch's corners, moved east, with the patch's U and V directions.</summary>
        public static Brep FlatBase(Surface patch)
        {
            Interval u = patch.Domain(0), v = patch.Domain(1);
            double z = patch.GetBoundingBox(true).Min.Z;
            Point3d Corner(double a, double b)
            {
                Point3d p = patch.PointAt(u.ParameterAt(a), v.ParameterAt(b));
                return new Point3d(p.X + FlowBaseShift, p.Y, z);
            }
            return NurbsSurface.CreateFromCorners(Corner(0, 0), Corner(1, 0), Corner(1, 1), Corner(0, 1)).ToBrep();
        }

        /// <summary>The 12 flat joists on the base, running north–south, 0.5 m apart from its south-west corner.</summary>
        public static List<Brep> FlatJoists(Brep flatBase)
        {
            BoundingBox bb = flatBase.GetBoundingBox(true);
            var sw = new Point3d(bb.Min.X, bb.Min.Y, bb.Min.Z);
            return Enumerable.Range(0, JoistCount)
                .Select(i => new Box(new BoundingBox(sw + new Vector3d(i * JoistSpacing, 0, 0),
                                                     sw + new Vector3d(i * JoistSpacing + JoistWidth, bb.Diagonal.Y, JoistDepth))).ToBrep())
                .ToList();
        }

        /// <summary>The south-west corner of a surface (smallest X + Y).</summary>
        public static Point3d SouthWest(Brep b)
        {
            BrepFace f = b.Faces[0];
            var corners = new[] { (0.0, 0.0), (1.0, 0.0), (0.0, 1.0), (1.0, 1.0) }
                .Select(c => f.PointAt(f.Domain(0).ParameterAt(c.Item1), f.Domain(1).ParameterAt(c.Item2)));
            return corners.OrderBy(p => p.X + p.Y).First();
        }
    }
}
