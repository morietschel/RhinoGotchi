using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.Geometry;

namespace RhinoGotchi.Content
{
    /// <summary>A chain of commands that builds something, shown stage by stage in the viewport.</summary>
    internal sealed class Recipe
    {
        private readonly Action<DemoBuilder, double> _build;
        private CommandDemo? _demo;

        public Recipe(string title, string summary, string[] chain, Action<DemoBuilder, double> build)
        {
            Title = title;
            Summary = summary;
            Chain = chain;
            _build = build;
        }

        public string Title { get; }
        public string Summary { get; }

        /// <summary>The commands in order, e.g. Line → Join → ExtrudeCrv → FilletEdge.</summary>
        public string[] Chain { get; }

        public string ChainText => string.Join(" → ", Chain);

        public bool Uses(string command) => Chain.Contains(command, StringComparer.OrdinalIgnoreCase);

        public CommandDemo? Demo(double tolerance)
        {
            if (_demo != null)
                return _demo;
            try
            {
                var builder = new DemoBuilder();
                _build(builder, tolerance);
                _demo = builder.Build(Title);
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine("RhinoGotchi: could not build the \"{0}\" recipe ({1}).", Title, ex.Message);
            }
            return _demo;
        }
    }

    /// <summary>How commands string together: small projects that chain Curves, Solids and Transform commands.</summary>
    internal static class Recipes
    {
        public static IEnumerable<Recipe> Using(string command) => All.Where(r => r.Uses(command));

        public static readonly Recipe[] All =
        {
            new Recipe("Lines to a solid bracket",
                "String separate lines together into one closed curve, then turn it into a solid with rounded edges.",
                new[] { "Line", "Join", "ExtrudeCrv", "FilletEdge" },
                (d, tol) =>
                {
                    var pts = new[] { P(0, 0), P(10, 0), P(10, 2), P(2, 2), P(2, 8), P(0, 8), P(0, 0) };
                    var lines = Enumerable.Range(0, pts.Length - 1).Select(i => (GeometryBase)new LineCurve(pts[i], pts[i + 1])).ToArray();
                    d.Stage("Line", "Draw the outline one segment at a time with Line. Turn on the End osnap so each line starts exactly where the last one ended.")
                        .Result(lines);
                    for (int i = 0; i < pts.Length - 1; i++)
                        d.Mark(pts[i], (i + 1).ToString());

                    var outline = new PolylineCurve(pts);
                    d.Stage("Join", "Join the six lines. Because their ends touch, they become one closed polyline that selects as a single object.")
                        .ClearMarks().ClearResults().Result(outline);

                    var solid = Extrusion.Create(outline, Plane.WorldXY, 3, true)?.ToBrep();
                    d.Stage("ExtrudeCrv", "ExtrudeCrv the closed curve with Solid=Yes. The flat outline becomes a closed solid.")
                        .Promote().Result(solid!);

                    if (solid != null)
                    {
                        var edges = Enumerable.Range(0, solid.Edges.Count).ToList();
                        var radii = edges.Select(_ => 0.4).ToList();
                        var rounded = Brep.CreateFilletEdges(solid, edges, radii, radii, BlendType.Fillet, RailType.RollingBall, tol);
                        d.Stage("FilletEdge", "FilletEdge rounds every edge. Your six lines are now a finished part.")
                            .Promote().Results(rounded != null && rounded.Length > 0 ? rounded : new[] { solid });
                    }
                }),

            new Recipe("Circles to a washer",
                "Two circles become a ring-shaped solid: the inner circle turns into a hole.",
                new[] { "Circle", "Offset", "ExtrudeCrv" },
                (d, tol) =>
                {
                    var outer = new Circle(Point3d.Origin, 5).ToNurbsCurve();
                    var inner = new Circle(Point3d.Origin, 2).ToNurbsCurve();
                    d.Stage("Circle", "Draw a circle: pick the center, then the radius.")
                        .Result(outer).Mark(Point3d.Origin, "center");
                    d.Stage("Offset", "Offset the circle inward by 3 to get a second circle the same distance away everywhere.")
                        .ClearMarks().Promote().Result(inner);
                    var ring = Extrusion.Create(outer, Plane.WorldXY, 1.5, true)?.ToBrep();
                    // The cutter pokes out above and below so no faces are flush (flush faces make Booleans fail).
                    var hole = new Cylinder(new Circle(new Plane(P(0, 0, -0.5), Vector3d.ZAxis), 2), 2.5).ToBrep(true, true);
                    var washer = ring != null ? Brep.CreateBooleanDifference(ring, hole, tol) : null;
                    d.Stage("ExtrudeCrv", "Select both circles and ExtrudeCrv with Solid=Yes. Rhino caps between them, so the inner circle becomes a hole.")
                        .ClearResults().ClearInputs().Input(outer, inner)
                        .Results(washer != null && washer.Length > 0 ? washer : (ring != null ? new[] { ring } : null));
                }),

            new Recipe("Polyline to a handrail",
                "A sharp polyline becomes a smooth tube, like a railing.",
                new[] { "Polyline", "Fillet", "Pipe" },
                (d, tol) =>
                {
                    var pts = new[] { P(0, 0, 0), P(8, 0, 0), P(8, 6, 3), P(16, 6, 3), P(16, 12, 6) };
                    var path = new PolylineCurve(pts);
                    d.Stage("Polyline", "Draw the path with Polyline: one point at each corner.").Result(path);
                    for (int i = 0; i < pts.Length; i++)
                        d.Mark(pts[i], (i + 1).ToString());
                    var smooth = Curve.CreateFilletCornersCurve(path, 1.5, tol, RhinoMath.ToRadians(1));
                    d.Stage("Fillet", "Round each corner with Fillet (or every corner at once with FilletCorners). The sharp turns become arcs.")
                        .ClearMarks().Promote().Result(smooth ?? path);
                    d.Stage("Pipe", "Pipe the smooth curve. A tube follows it, round at every turn.")
                        .Promote().Results(Brep.CreatePipe(smooth ?? path, 0.35, false, PipeCapMode.Round, true, tol, RhinoMath.ToRadians(1)));
                }),

            new Recipe("Box to a perforated plate",
                "Cut a grid of holes through a plate with one Boolean.",
                new[] { "Box", "Cylinder", "Array", "BooleanDifference" },
                (d, tol) =>
                {
                    var plate = new Box(Plane.WorldXY, new Interval(0, 12), new Interval(0, 8), new Interval(0, 1)).ToBrep();
                    d.Stage("Box", "Draw a thin box for the plate.").Result(plate);
                    var peg = new Cylinder(new Circle(new Plane(P(2, 2, -1), Vector3d.ZAxis), 0.6), 3).ToBrep(true, true);
                    d.Stage("Cylinder", "Draw one cylinder that goes all the way through the plate.").Promote().Result(peg);
                    var pegs = (from x in Enumerable.Range(0, 5) from y in Enumerable.Range(0, 3)
                                select (Brep)Moved(peg, Transform.Translation(x * 2, y * 2, 0))).ToList();
                    d.Stage("Array", "Array the cylinder: 5 in X, 3 in Y, 2 apart.")
                        .ClearResults().Results(pegs);
                    d.Stage("BooleanDifference", "BooleanDifference: pick the plate, press Enter, then pick all the cylinders. They're subtracted in one go.")
                        .ClearInputs().ClearResults().Input(pegs.ToArray<GeometryBase>())
                        .Results(Brep.CreateBooleanDifference(new[] { plate }, pegs, tol));
                }),

            new Recipe("Circle to a gear",
                "A disk plus one tooth, copied around the center and merged into one solid.",
                new[] { "Circle", "ExtrudeCrv", "Box", "ArrayPolar", "BooleanUnion" },
                (d, tol) =>
                {
                    var rim = new Circle(Point3d.Origin, 4).ToNurbsCurve();
                    d.Stage("Circle", "Draw a circle for the body of the gear.").Result(rim).Mark(Point3d.Origin, "center");
                    var disk = Extrusion.Create(rim, Plane.WorldXY, 1.5, true)?.ToBrep();
                    d.Stage("ExtrudeCrv", "ExtrudeCrv the circle with Solid=Yes to make a disk.").ClearMarks().Promote().Result(disk!);
                    // The tooth overlaps the rim and sits slightly inside the disk's top and bottom, so no faces are flush.
                    var tooth = new Box(Plane.WorldXY, new Interval(3.5, 5), new Interval(-0.45, 0.45), new Interval(0.1, 1.4)).ToBrep();
                    d.Stage("Box", "Draw one tooth as a box that overlaps the edge of the disk.").Promote().Result(tooth);
                    var teeth = Enumerable.Range(0, 12)
                        .Select(i => (Brep)Moved(tooth, Transform.Rotation(i * Math.PI / 6, Vector3d.ZAxis, Point3d.Origin))).ToList();
                    d.Stage("ArrayPolar", "ArrayPolar the tooth around the center: 12 items, 360 degrees.").ClearResults().Results(teeth);
                    var parts = new List<Brep>(teeth);
                    if (disk != null)
                        parts.Add(disk);
                    d.Stage("BooleanUnion", "BooleanUnion the disk and all the teeth into one closed solid.")
                        .Promote().Results(Brep.CreateBooleanUnion(parts, tol));
                }),

            new Recipe("Polygon to a twisted tower",
                "Extrude a polygon into a prism, then twist it.",
                new[] { "Polygon", "ExtrudeCrv", "Twist" },
                (d, tol) =>
                {
                    PolylineCurve Hexagon(double z, double turn) => new PolylineCurve(Enumerable.Range(0, 7)
                        .Select(i => P(4 * Math.Cos(i * Math.PI / 3 + turn), 4 * Math.Sin(i * Math.PI / 3 + turn), z)));
                    var hex = Hexagon(0, 0);
                    d.Stage("Polygon", "Draw a 6-sided polygon from its center.").Result(hex).Mark(Point3d.Origin, "center");
                    var prism = Extrusion.Create(hex, Plane.WorldXY, 20, true)?.ToBrep();
                    d.Stage("ExtrudeCrv", "ExtrudeCrv it 20 units with Solid=Yes.").ClearMarks().Promote().Result(prism!);
                    // A twisted prism is the same as lofting hexagons that turn a little at each level.
                    var levels = Enumerable.Range(0, 21).Select(i => (Curve)Hexagon(i, i * Math.PI / 2 / 20)).ToList();
                    var twisted = Brep.CreateFromLoft(levels, Point3d.Unset, Point3d.Unset, LoftType.Normal, false)?
                        .Select(b => b.CapPlanarHoles(tol) ?? b).ToArray();
                    d.Stage("Twist", "Twist it 90 degrees around its vertical axis. Each level turns a bit more than the one below.")
                        .Promote().Results(twisted);
                }),
        };

        private static Point3d P(double x, double y, double z = 0) => new Point3d(x, y, z);

        private static GeometryBase Moved(GeometryBase g, Transform xf) => ShapeGeometry.Moved(g, xf);
    }
}
