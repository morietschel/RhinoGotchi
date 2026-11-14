using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.Geometry;

namespace RhinoGotchi.Content
{
    /// <summary>Geometry for one round: what you start with, and what the command produced.</summary>
    internal sealed class ShapeGeometry
    {
        public List<GeometryBase> Inputs { get; } = new List<GeometryBase>();
        public List<GeometryBase> Results { get; } = new List<GeometryBase>();
    }

    internal sealed class ShapeRound
    {
        private readonly Func<double, ShapeGeometry> _build;

        public ShapeRound(string command, string explanation, Func<double, ShapeGeometry> build)
        {
            Command = command;
            Explanation = explanation;
            _build = build;
        }

        public string Command { get; }
        public string Explanation { get; }

        /// <summary>Builds the round's geometry, or returns null if RhinoCommon could not make it.</summary>
        public ShapeGeometry? Build(double tolerance)
        {
            try
            {
                var g = _build(tolerance);
                return g.Results.Count > 0 ? g : null;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine("RhinoGotchi: skipped the {0} round ({1}).", Command, ex.Message);
                return null;
            }
        }
    }

    /// <summary>"What Made This?" rounds. Each one is built live with RhinoCommon.</summary>
    internal static class ShapeRounds
    {
        /// <summary>Wrong answers that don't make any of the shapes below.</summary>
        public static readonly string[] ExtraDistractors =
        {
            "Sweep2", "NetworkSrf", "BooleanUnion", "ChamferEdge", "ArrayCrv", "OffsetSrf", "ExtrudeSrf", "Twist",
        };

        public static readonly ShapeRound[] All =
        {
            new ShapeRound("Loft",
                "Loft stretches a smooth surface through a series of cross-section curves, in the order you pick them. The curves don't have to be the same shape.",
                tol =>
                {
                    var g = new ShapeGeometry();
                    var sections = new[] { (0.0, 4.0), (4.0, 2.0), (8.0, 3.5), (11.0, 1.5) }
                        .Select(s => (Curve)new Circle(new Plane(new Point3d(0, 0, s.Item1), Vector3d.ZAxis), s.Item2).ToNurbsCurve())
                        .ToList();
                    g.Inputs.AddRange(sections);
                    var loft = Brep.CreateFromLoft(sections, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
                    if (loft != null) g.Results.AddRange(loft);
                    return g;
                }),

            new ShapeRound("Revolve",
                "Revolve spins a profile curve around an axis. Draw half of the silhouette, and a vase, bottle or wheel appears.",
                tol =>
                {
                    var g = new ShapeGeometry();
                    var profile = Curve.CreateInterpolatedCurve(new[]
                    {
                        new Point3d(2, 0, 0), new Point3d(3.6, 0, 2), new Point3d(1.6, 0, 5.5),
                        new Point3d(1.2, 0, 7.5), new Point3d(2.4, 0, 10),
                    }, 3);
                    var axis = new Line(Point3d.Origin, new Point3d(0, 0, 10));
                    g.Inputs.Add(profile);
                    g.Inputs.Add(new LineCurve(axis));
                    var rev = RevSurface.Create(profile, axis);
                    if (rev != null) g.Results.Add(Brep.CreateFromRevSurface(rev, false, false));
                    return g;
                }),

            new ShapeRound("Pipe",
                "Pipe puts a round tube around a curve. You give a radius at the start and end (and optionally in between). The ends can be flat, round or open.",
                tol =>
                {
                    var g = new ShapeGeometry();
                    var rail = Curve.CreateInterpolatedCurve(new[]
                    {
                        new Point3d(0, 0, 0), new Point3d(4, 4, 2), new Point3d(8, 0, 5),
                        new Point3d(12, 4, 3), new Point3d(15, 1, 6),
                    }, 3);
                    g.Inputs.Add(rail);
                    var pipe = Brep.CreatePipe(rail, 0.7, false, PipeCapMode.Round, true, tol, RhinoMath.ToRadians(1));
                    if (pipe != null) g.Results.AddRange(pipe);
                    return g;
                }),

            new ShapeRound("ExtrudeCrv",
                "ExtrudeCrv pushes a curve straight out to make a surface. With Solid=Yes, a closed planar curve becomes a capped solid.",
                tol =>
                {
                    var g = new ShapeGeometry();
                    var pts = new List<Point3d>();
                    for (int i = 0; i <= 10; i++)
                    {
                        double a = i * Math.PI / 5;
                        double r = i % 2 == 0 ? 5 : 2.2;
                        pts.Add(new Point3d(r * Math.Cos(a), r * Math.Sin(a), 0));
                    }
                    var star = new PolylineCurve(pts);
                    g.Inputs.Add(star);
                    var ext = Extrusion.Create(star, Plane.WorldXY, 3, true);
                    if (ext != null) g.Results.Add(ext.ToBrep());
                    return g;
                }),

            new ShapeRound("Sweep1",
                "Sweep1 slides a profile along one rail curve. The profile keeps its shape and turns to follow the rail, like a picture-frame molding.",
                tol =>
                {
                    var g = new ShapeGeometry();
                    var rail = Curve.CreateInterpolatedCurve(new[]
                    {
                        new Point3d(0, 0, 0), new Point3d(5, 4, 1), new Point3d(10, 0, 3), new Point3d(15, -3, 2),
                    }, 3);
                    var frame = new Plane(rail.PointAtStart, rail.TangentAtStart);
                    var profile = new Rectangle3d(frame, new Interval(-1.5, 1.5), new Interval(-0.4, 0.4)).ToNurbsCurve();
                    g.Inputs.Add(rail);
                    g.Inputs.Add(profile);
                    var sweep = Brep.CreateFromSweep(rail, profile, false, tol);
                    if (sweep != null) g.Results.AddRange(sweep);
                    return g;
                }),

            new ShapeRound("BooleanDifference",
                "BooleanDifference subtracts one solid from another. Here the orange sphere was cut out of a box. Both must be closed solids.",
                tol =>
                {
                    var g = new ShapeGeometry();
                    var box = new Box(Plane.WorldXY, new Interval(0, 6), new Interval(0, 6), new Interval(0, 6)).ToBrep();
                    var sphere = new Sphere(new Point3d(6, 6, 6), 3.5).ToBrep();
                    g.Inputs.Add(sphere);
                    var diff = Brep.CreateBooleanDifference(box, sphere, tol);
                    if (diff != null) g.Results.AddRange(diff);
                    return g;
                }),

            new ShapeRound("FilletEdge",
                "FilletEdge rounds the edges of a solid with a set radius. The orange box shows the sharp edges before the fillet.",
                tol =>
                {
                    var g = new ShapeGeometry();
                    var box = new Box(Plane.WorldXY, new Interval(0, 8), new Interval(0, 5), new Interval(0, 4)).ToBrep();
                    g.Inputs.Add(box);
                    var edges = Enumerable.Range(0, box.Edges.Count).ToList();
                    var radii = edges.Select(_ => 0.8).ToList();
                    var fillet = Brep.CreateFilletEdges(box, edges, radii, radii, BlendType.Fillet, RailType.RollingBall, tol);
                    if (fillet != null) g.Results.AddRange(fillet);
                    return g;
                }),

            new ShapeRound("ArrayPolar",
                "ArrayPolar copies objects around a center point a set number of times, like spokes or chairs around a table.",
                tol =>
                {
                    var g = new ShapeGeometry();
                    var source = new Box(Plane.WorldXY, new Interval(5, 7), new Interval(-0.75, 0.75), new Interval(0, 3)).ToBrep();
                    g.Inputs.Add(source);
                    for (int i = 0; i < 8; i++)
                    {
                        var copy = source.DuplicateBrep();
                        copy.Transform(Transform.Rotation(i * Math.PI / 4, Vector3d.ZAxis, Point3d.Origin));
                        g.Results.Add(copy);
                    }
                    return g;
                }),

            new ShapeRound("PlanarSrf",
                "PlanarSrf fills closed curves that lie flat in one plane with a trimmed flat surface. Curves inside other curves become holes.",
                tol =>
                {
                    var g = new ShapeGeometry();
                    var outline = Curve.CreateInterpolatedCurve(new[]
                    {
                        new Point3d(0, 0, 0), new Point3d(6, -2, 0), new Point3d(10, 2, 0),
                        new Point3d(7, 6, 0), new Point3d(2, 5, 0),
                    }, 3, CurveKnotStyle.ChordPeriodic);
                    g.Inputs.Add(outline);
                    var srf = Brep.CreatePlanarBreps(outline, tol);
                    if (srf != null) g.Results.AddRange(srf);
                    return g;
                }),

            new ShapeRound("Contour",
                "Contour slices objects with evenly spaced parallel planes and gives you the section curves, like elevation lines on a map.",
                tol =>
                {
                    var g = new ShapeGeometry();
                    var blob = new Sphere(Point3d.Origin, 5).ToBrep();
                    g.Inputs.Add(blob);
                    var curves = Brep.CreateContourCurves(blob, new Point3d(0, 0, -5), new Point3d(0, 0, 5), 1.0);
                    if (curves != null) g.Results.AddRange(curves);
                    return g;
                }),

            new ShapeRound("Offset",
                "Offset makes a curve that is a set distance from the original, everywhere along it. It's great for wall thicknesses and margins.",
                tol =>
                {
                    var g = new ShapeGeometry();
                    var outline = Curve.CreateInterpolatedCurve(new[]
                    {
                        new Point3d(0, 0, 0), new Point3d(8, -1, 0), new Point3d(10, 5, 0), new Point3d(3, 6, 0),
                    }, 3, CurveKnotStyle.ChordPeriodic);
                    g.Inputs.Add(outline);
                    foreach (double d in new[] { 1.0, 2.0, 3.0 })
                    {
                        var offsets = outline.Offset(Plane.WorldXY, d, tol, CurveOffsetCornerStyle.Sharp);
                        if (offsets != null) g.Results.AddRange(offsets);
                    }
                    return g;
                }),
        };
    }
}
