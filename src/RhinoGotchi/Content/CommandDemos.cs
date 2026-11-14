using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Morphs;

namespace RhinoGotchi.Content
{
    internal sealed class DemoStep
    {
        public DemoStep(string text, ShapeGeometry frame, string? command = null)
        {
            Text = text;
            Frame = frame;
            Command = command;
        }

        public string Text { get; }
        public ShapeGeometry Frame { get; }

        /// <summary>The command this step uses (recipe stages only).</summary>
        public string? Command { get; }
    }

    /// <summary>A step-by-step walkthrough of one command, drawn in the viewport.</summary>
    internal sealed class CommandDemo
    {
        public CommandDemo(string command, List<DemoStep> steps)
        {
            Command = command;
            Steps = steps;
            Focus = BoundingBox.Empty;
            foreach (var s in steps)
                Focus.Union(s.Frame.GetBoundingBox());
            TryInputs = steps.SelectMany(s => s.Frame.Inputs).Distinct().ToList();
        }

        public string Command { get; }
        public List<DemoStep> Steps { get; }

        /// <summary>Box around every step, so the camera stays still while stepping.</summary>
        public BoundingBox Focus { get; }

        /// <summary>Starting geometry the player can add to the model to try the command on.</summary>
        public List<GeometryBase> TryInputs { get; }

        /// <summary>The final result alone, shown as a target while the player tries the command.</summary>
        public ShapeGeometry Target()
        {
            var target = new ShapeGeometry();
            target.Results.AddRange(Steps[Steps.Count - 1].Frame.Results);
            return target;
        }
    }

    /// <summary>
    /// Builds demos one step at a time. Each step starts as a copy of the step before it,
    /// so a demo reads like a story: add a pick point, then another, then show the result.
    /// </summary>
    internal sealed class DemoBuilder
    {
        private readonly List<DemoStep> _steps = new List<DemoStep>();
        private ShapeGeometry _frame = new ShapeGeometry();

        public DemoBuilder Step(string text) => Stage(null, text);

        /// <summary>A step that uses a specific command (for recipes that chain several commands).</summary>
        public DemoBuilder Stage(string? command, string text)
        {
            _frame = _steps.Count == 0 ? _frame : _frame.Clone();
            _steps.Add(new DemoStep(text, _frame, command));
            return this;
        }

        /// <summary>The last result becomes the next command's input: results turn orange, marks and guides clear.</summary>
        public DemoBuilder Promote()
        {
            _frame.Inputs.Clear();
            _frame.Inputs.AddRange(_frame.Results);
            _frame.Results.Clear();
            _frame.Marks.Clear();
            _frame.Guides.Clear();
            return this;
        }

        public DemoBuilder ClearResults() { _frame.Results.Clear(); return this; }

        public DemoBuilder Input(params GeometryBase[] geometry) { _frame.Inputs.AddRange(geometry); return this; }
        public DemoBuilder Inputs(IEnumerable<GeometryBase> geometry) { _frame.Inputs.AddRange(geometry); return this; }
        public DemoBuilder Result(params GeometryBase[] geometry) { _frame.Results.AddRange(geometry.Where(g => g != null)); return this; }
        public DemoBuilder Results(IEnumerable<GeometryBase>? geometry) { if (geometry != null) _frame.Results.AddRange(geometry.Where(g => g != null)); return this; }
        public DemoBuilder Mark(Point3d point, string label) { _frame.Marks.Add((point, label)); return this; }
        public DemoBuilder Guide(Point3d from, Point3d to) { _frame.Guides.Add(new Line(from, to)); return this; }
        public DemoBuilder ClearMarks() { _frame.Marks.Clear(); return this; }
        public DemoBuilder ClearGuides() { _frame.Guides.Clear(); return this; }
        public DemoBuilder ClearInputs() { _frame.Inputs.Clear(); return this; }

        public DemoBuilder Polygon(params Point3d[] points)
        {
            for (int i = 0; i < points.Length - 1; i++)
                Guide(points[i], points[i + 1]);
            return this;
        }

        public CommandDemo Build(string command) => new CommandDemo(command, _steps);
    }

    /// <summary>Hand-built "how it works" demos for Curves, Solids and Transform commands.</summary>
    internal static class CommandDemos
    {
        private static readonly Dictionary<string, Action<DemoBuilder, double>> Builders =
            new Dictionary<string, Action<DemoBuilder, double>>(StringComparer.OrdinalIgnoreCase)
            {
                // ---------------- Curves ----------------
                ["Line"] = (d, tol) =>
                {
                    Point3d a = P(0, 0), b = P(10, 4);
                    d.Step("Run Line and pick the start point.").Mark(a, "1");
                    d.Step("Pick the end point, or type a length and press Enter. Hold Shift for Ortho.").Mark(b, "2").Guide(a, b);
                    d.Step("Rhino adds one straight segment. Options like Bisector, Perpendicular and Tangent start the line from other geometry.")
                        .ClearGuides().Result(new LineCurve(a, b));
                },
                ["Polyline"] = (d, tol) =>
                {
                    var pts = new[] { P(0, 0), P(4, 5), P(9, 2), P(13, 6) };
                    d.Step("Run Polyline and pick the first point.").Mark(pts[0], "1");
                    d.Step("Keep picking points. Each one adds a straight segment.")
                        .Mark(pts[1], "2").Mark(pts[2], "3").Mark(pts[3], "4").Polygon(pts);
                    d.Step("Press Enter to finish, or pick the start point to close it. The result is one joined polyline.")
                        .ClearGuides().Result(new PolylineCurve(pts));
                },
                ["Rectangle"] = (d, tol) =>
                {
                    Point3d a = P(0, 0), b = P(10, 6);
                    d.Step("Run Rectangle and pick the first corner.").Mark(a, "1");
                    d.Step("Pick the opposite corner, or type the length, then the width. Rounded adds corner radii.")
                        .Mark(b, "2").Guide(a, b);
                    d.Step("Rhino adds a closed rectangle as one polyline.")
                        .ClearGuides().Result(new Rectangle3d(Plane.WorldXY, a, b).ToNurbsCurve());
                },
                ["Polygon"] = (d, tol) =>
                {
                    var pts = Enumerable.Range(0, 7).Select(i => P(5 * Math.Cos(i * Math.PI / 3), 5 * Math.Sin(i * Math.PI / 3))).ToArray();
                    d.Step("Run Polygon, set NumSides (6 here) and pick the center.").Mark(Point3d.Origin, "1");
                    d.Step("Pick a corner. Its distance sets the size and its direction sets the rotation.")
                        .Mark(pts[0], "2").Guide(Point3d.Origin, pts[0]);
                    d.Step("Rhino adds a regular polygon. Use Polygon Edge or Star options for other shapes.")
                        .ClearGuides().Result(new PolylineCurve(pts));
                },
                ["Circle"] = (d, tol) =>
                {
                    var c = new Circle(Point3d.Origin, 4);
                    d.Step("Run Circle and pick the center.").Mark(c.Center, "1");
                    d.Step("Pick a point or type the radius. Type D to enter a diameter instead.")
                        .Mark(c.PointAt(0), "2").Guide(c.Center, c.PointAt(0));
                    d.Step("Rhino adds the circle. Options let you draw it from 3 points, tangent to curves and more.")
                        .ClearGuides().Result(c.ToNurbsCurve());
                },
                ["Arc"] = (d, tol) =>
                {
                    var arc = new Arc(Plane.WorldXY, 5, RhinoMath.ToRadians(130));
                    d.Step("Run Arc and pick the center.").Mark(arc.Center, "1");
                    d.Step("Pick the start point. Its distance from the center is the radius.")
                        .Mark(arc.StartPoint, "2").Guide(arc.Center, arc.StartPoint);
                    d.Step("Pick the end point or type an angle (130 degrees here).")
                        .Mark(arc.EndPoint, "3").Guide(arc.Center, arc.EndPoint);
                    d.Step("Rhino adds the arc.").ClearGuides().Result(arc.ToNurbsCurve());
                },
                ["Ellipse"] = (d, tol) =>
                {
                    var e = new Ellipse(Plane.WorldXY, 6, 3);
                    d.Step("Run Ellipse and pick the center.").Mark(Point3d.Origin, "1");
                    d.Step("Pick the end of the first axis.").Mark(P(6, 0), "2").Guide(Point3d.Origin, P(6, 0));
                    d.Step("Pick the end of the second axis.").Mark(P(0, 3), "3").Guide(Point3d.Origin, P(0, 3));
                    d.Step("Rhino adds the ellipse.").ClearGuides().Result(e.ToNurbsCurve());
                },
                ["Helix"] = (d, tol) =>
                {
                    d.Step("Run Helix and pick the start of the axis.").Mark(Point3d.Origin, "1");
                    d.Step("Pick the end of the axis.").Mark(P(0, 0, 10), "2").Guide(Point3d.Origin, P(0, 0, 10));
                    d.Step("Set Turns or Pitch, then pick the radius.").Mark(P(3, 0, 0), "3").Guide(Point3d.Origin, P(3, 0, 0));
                    d.Step("Rhino adds a spring-like curve around the axis. Pipe it to make a real spring.")
                        .Result(Spiral(5, 3, 3, 10));
                },
                ["Spiral"] = (d, tol) =>
                {
                    d.Step("Run Spiral, choose Flat, and pick the center.").Mark(Point3d.Origin, "1");
                    d.Step("Pick the first radius.").Mark(P(1, 0), "2").Guide(Point3d.Origin, P(1, 0));
                    d.Step("Set Turns and pick the second radius.").Mark(P(6, 0), "3").Guide(P(1, 0), P(6, 0));
                    d.Step("Rhino adds a spiral that grows from the first radius to the second. Without Flat, it climbs an axis like a helix.")
                        .ClearGuides().Result(Spiral(4, 1, 6, 0));
                },
                ["InterpCrv"] = (d, tol) =>
                {
                    var pts = new[] { P(0, 0), P(3, 4), P(7, 1), P(10, 5), P(14, 2) };
                    d.Step("Run InterpCrv and pick points. The curve will pass through every one.");
                    for (int i = 0; i < pts.Length; i++)
                        d.Mark(pts[i], (i + 1).ToString());
                    d.Step("Press Enter. Compare it with Curve, whose points only pull the curve.")
                        .Result(Curve.CreateInterpolatedCurve(pts, 3));
                },
                ["Curve"] = (d, tol) =>
                {
                    var pts = new[] { P(0, 0), P(3, 6), P(7, -1), P(10, 6), P(14, 1) };
                    d.Step("Run Curve and pick control points. The dotted control polygon connects them.").Polygon(pts);
                    for (int i = 0; i < pts.Length; i++)
                        d.Mark(pts[i], (i + 1).ToString());
                    d.Step("Press Enter. The curve is pulled toward the points but only touches the ends. Smooth shapes come from few points.")
                        .Result(NurbsCurve.Create(false, 3, pts));
                },
                ["Offset"] = (d, tol) =>
                {
                    var outline = Curve.CreateInterpolatedCurve(new[] { P(0, 0), P(8, -1), P(10, 5), P(3, 6) }, 3, CurveKnotStyle.ChordPeriodic);
                    d.Step("Start with a planar curve.").Input(outline);
                    d.Step("Run Offset, pick the curve and set Distance (1.5 here). Corner sets how sharp corners are handled.");
                    d.Step("Click the side to offset toward. Rhino adds a curve that stays the same distance from the original everywhere.")
                        .Results(outline.Offset(Plane.WorldXY, 1.5, tol, CurveOffsetCornerStyle.Sharp));
                },
                ["Fillet"] = (d, tol) =>
                {
                    var a = new LineCurve(P(-10, 0), P(0, 0));
                    var b = new LineCurve(P(0, 0), P(0, 7));
                    d.Step("Start with two curves that meet at a corner.").Input(a, b);
                    d.Step("Run Fillet, type Radius (2.5 here), then pick both curves near the parts you want to keep.")
                        .Mark(P(-6, 0), "1").Mark(P(0, 4), "2");
                    d.Step("Rhino trims both curves and joins them with a tangent arc. Radius 0 makes a sharp corner instead.")
                        .ClearMarks().Results(Curve.CreateFilletCurves(a, P(-6, 0), b, P(0, 4), 2.5, true, true, true, tol, RhinoMath.ToRadians(1)));
                },
                ["BlendCrv"] = (d, tol) =>
                {
                    var a = new LineCurve(P(0, 0), P(6, 0));
                    var b = new LineCurve(P(11, 5), P(17, 5));
                    d.Step("Start with two curves with a gap between them.").Input(a, b);
                    d.Step("Run BlendCrv and pick near the ends you want to connect.").Mark(a.PointAtEnd, "1").Mark(b.PointAtStart, "2");
                    d.Step("Choose the continuity (Position, Tangency, Curvature...) and drag the handles. The blend flows smoothly into both curves.")
                        .ClearMarks().Result(Curve.CreateBlendCurve(a, b, BlendContinuity.Curvature));
                },
                ["Extend"] = (d, tol) =>
                {
                    var crv = new LineCurve(P(0, 0), P(6, 2));
                    var boundary = new LineCurve(P(12, -2), P(12, 8));
                    d.Step("Start with a curve and a boundary for it to reach.").Input(crv, boundary);
                    d.Step("Run Extend, pick the boundary and press Enter.").Mark(P(12, 6), "1");
                    d.Step("Pick near the end of the curve to extend. Type sets whether the extension is a Line, Arc or Smooth.")
                        .Mark(P(5.5, 1.8), "2");
                    d.Step("The curve grows until it hits the boundary.").ClearMarks().Result(new LineCurve(P(0, 0), P(12, 4)));
                },
                ["Trim"] = (d, tol) =>
                {
                    var h = new LineCurve(P(0, 0), P(12, 0));
                    var v = new LineCurve(P(7, -4), P(7, 4));
                    d.Step("Start with two crossing curves.").Input(h, v);
                    d.Step("Run Trim, pick the cutting object (the vertical line) and press Enter.").Mark(P(7, 3), "1");
                    d.Step("Click the part you want to remove.").Mark(P(10, 0), "2");
                    d.Step("That part is deleted. The rest of the curve stops exactly at the cutter.")
                        .ClearMarks().ClearInputs().Result(new LineCurve(P(0, 0), P(7, 0)), v);
                },
                ["Split"] = (d, tol) =>
                {
                    var h = new LineCurve(P(0, 0), P(12, 0));
                    var v = new LineCurve(P(7, -4), P(7, 4));
                    d.Step("Start with a curve and a cutting object.").Input(h, v);
                    d.Step("Run Split, pick the curve to split and press Enter.").Mark(P(3, 0), "1");
                    d.Step("Pick the cutting object and press Enter.").Mark(P(7, 3), "2");
                    d.Step("Unlike Trim, nothing is deleted: the curve is now two separate pieces.")
                        .ClearMarks().ClearInputs().Input(v)
                        .Result(new LineCurve(P(0, 0), P(7, 0)), new LineCurve(P(7, 0), P(12, 0)))
                        .Mark(P(3.5, 0), "piece 1").Mark(P(9.5, 0), "piece 2");
                },
                ["Join"] = (d, tol) =>
                {
                    var pts = new[] { P(0, 0), P(5, 0), P(8, 4), P(13, 4) };
                    var parts = Enumerable.Range(0, 3).Select(i => (GeometryBase)new LineCurve(pts[i], pts[i + 1])).ToArray();
                    d.Step("Start with separate curves whose ends touch.").Input(parts);
                    d.Step("Run Join and pick the curves (or select them first).")
                        .Mark(P(2.5, 0), "1").Mark(P(6.5, 2), "2").Mark(P(10.5, 4), "3");
                    d.Step("They become one polycurve that selects, offsets and extrudes as a single object. Explode undoes it.")
                        .ClearMarks().ClearInputs().Result(new PolylineCurve(pts));
                },
                ["Explode"] = (d, tol) =>
                {
                    var rect = new Rectangle3d(Plane.WorldXY, P(0, 0), P(10, 6));
                    var corners = new[] { P(0, 0), P(10, 0), P(10, 6), P(0, 6), P(0, 0) };
                    d.Step("Start with a joined object, like this rectangle.").Input(rect.ToNurbsCurve());
                    d.Step("Run Explode and pick it.").Mark(P(5, 0), "1");
                    var pieces = Enumerable.Range(0, 4).Select(i => (GeometryBase)new LineCurve(corners[i], corners[i + 1])).ToArray();
                    d.Step("It breaks into its parts: four separate lines. Join puts them back together.")
                        .ClearMarks().ClearInputs().Result(pieces)
                        .Mark(P(5, 0), "1").Mark(P(10, 3), "2").Mark(P(5, 6), "3").Mark(P(0, 3), "4");
                },
                ["Rebuild"] = (d, tol) =>
                {
                    var pts = Enumerable.Range(0, 12).Select(i => P(i * 1.3, 2.5 * Math.Sin(i * 0.9) + (i % 3) * 0.6)).ToArray();
                    var wobbly = Curve.CreateInterpolatedCurve(pts, 3).ToNurbsCurve();
                    d.Step("Start with a curve that has many uneven control points (dotted).")
                        .Input(wobbly).Polygon(wobbly.Points.Select(p => p.Location).ToArray());
                    var rebuilt = wobbly.Rebuild(6, 3, true);
                    d.Step("Run Rebuild and set Point count (6 here) and Degree (3).");
                    d.Step("Rhino replaces the curve with a smoother one with evenly spaced points. It may move slightly from the original.")
                        .ClearGuides().Result(rebuilt).Polygon(rebuilt.Points.Select(p => p.Location).ToArray());
                },
                ["Divide"] = (d, tol) =>
                {
                    var crv = Curve.CreateInterpolatedCurve(new[] { P(0, 0), P(4, 4), P(9, 1), P(14, 5) }, 3);
                    d.Step("Start with a curve.").Input(crv);
                    d.Step("Run Divide, pick the curve, and type the number of segments (8 here) or choose Length.");
                    var ts = crv.DivideByCount(8, true) ?? new double[0];
                    d.Step("Rhino places points at equal spacing along the curve. Turn on the Point osnap to snap to them.")
                        .Results(ts.Select(t => (GeometryBase)new Point(crv.PointAt(t))));
                },
                ["Contour"] = (d, tol) =>
                {
                    var blob = new Sphere(Point3d.Origin, 5).ToBrep();
                    d.Step("Start with the object you want to slice.").Input(blob);
                    d.Step("Run Contour, pick the object, then pick a base point and a direction. Slices are perpendicular to it.")
                        .Mark(P(0, 0, -5), "1").Mark(P(0, 0, 5), "2").Guide(P(0, 0, -5), P(0, 0, 5));
                    d.Step("Type the distance between contours. Rhino adds a curve at every slice, like elevation lines on a map.")
                        .ClearMarks().ClearGuides().Results(Brep.CreateContourCurves(blob, P(0, 0, -5), P(0, 0, 5), 1.0));
                },

                // ---------------- Solids ----------------
                ["Box"] = (d, tol) =>
                {
                    d.Step("Run Box and pick the first corner of the base.").Mark(P(0, 0), "1");
                    d.Step("Pick the opposite corner, or type the length and width.").Mark(P(8, 5), "2")
                        .Polygon(P(0, 0), P(8, 0), P(8, 5), P(0, 5), P(0, 0));
                    d.Step("Pick or type the height.").Mark(P(8, 5, 4), "3").Guide(P(8, 5), P(8, 5, 4));
                    d.Step("Rhino adds a closed box.").ClearGuides()
                        .Result(new Box(Plane.WorldXY, new Interval(0, 8), new Interval(0, 5), new Interval(0, 4)).ToBrep());
                },
                ["Sphere"] = (d, tol) =>
                {
                    d.Step("Run Sphere and pick the center.").Mark(Point3d.Origin, "1");
                    d.Step("Pick a point or type the radius.").Mark(P(4, 0), "2").Guide(Point3d.Origin, P(4, 0));
                    d.Step("Rhino adds a closed sphere.").ClearGuides().Result(new Sphere(Point3d.Origin, 4).ToBrep());
                },
                ["Cylinder"] = (d, tol) =>
                {
                    d.Step("Run Cylinder and pick the center of the base.").Mark(Point3d.Origin, "1");
                    d.Step("Pick the radius.").Mark(P(3, 0), "2").Guide(Point3d.Origin, P(3, 0));
                    d.Step("Pick or type the height.").Mark(P(0, 0, 8), "3").Guide(Point3d.Origin, P(0, 0, 8));
                    d.Step("Rhino adds a closed cylinder.").ClearGuides()
                        .Result(new Cylinder(new Circle(Point3d.Origin, 3), 8).ToBrep(true, true));
                },
                ["Cone"] = (d, tol) =>
                {
                    d.Step("Run Cone and pick the center of the base.").Mark(Point3d.Origin, "1");
                    d.Step("Pick the radius.").Mark(P(4, 0), "2").Guide(Point3d.Origin, P(4, 0));
                    d.Step("Pick the tip (the apex).").Mark(P(0, 0, 8), "3").Guide(Point3d.Origin, P(0, 0, 8));
                    // Side surface revolved from a slanted line, plus a flat base disk.
                    var rev = RevSurface.Create(new LineCurve(P(4, 0), P(0, 0, 8)), new Line(Point3d.Origin, P(0, 0, 8)));
                    d.Step("Rhino adds a closed cone.").ClearGuides();
                    if (rev != null)
                        d.Result(Brep.CreateFromRevSurface(rev, false, false));
                    d.Results(Brep.CreatePlanarBreps(new Circle(Point3d.Origin, 4).ToNurbsCurve(), tol));
                },
                ["Torus"] = (d, tol) =>
                {
                    d.Step("Run Torus and pick the center.").Mark(Point3d.Origin, "1");
                    d.Step("Pick the first radius, to the middle of the tube.").Mark(P(5, 0), "2").Guide(Point3d.Origin, P(5, 0));
                    d.Step("Pick the second radius, the size of the tube.").Mark(P(6.5, 0), "3").Guide(P(5, 0), P(6.5, 0));
                    d.Step("Rhino adds a donut-shaped solid.").ClearGuides()
                        .Result(new Torus(Plane.WorldXY, 5, 1.5).ToNurbsSurface().ToBrep());
                },
                ["Pipe"] = (d, tol) =>
                {
                    var rail = Curve.CreateInterpolatedCurve(new[] { P(0, 0), P(4, 4, 2), P(8, 0, 5), P(12, 4, 3), P(15, 1, 6) }, 3);
                    d.Step("Start with the curve the pipe will follow.").Input(rail);
                    d.Step("Run Pipe, pick the curve, and type the radius at the start and at the end.")
                        .Mark(rail.PointAtStart, "start radius").Mark(rail.PointAtEnd, "end radius");
                    d.Step("Set Cap to Round, Flat or None and press Enter. A round tube follows the whole curve.")
                        .ClearMarks().Results(Brep.CreatePipe(rail, 0.7, false, PipeCapMode.Round, true, tol, RhinoMath.ToRadians(1)));
                },
                ["ExtrudeCrv"] = (d, tol) =>
                {
                    var pts = Enumerable.Range(0, 11).Select(i => P((i % 2 == 0 ? 5 : 2.2) * Math.Cos(i * Math.PI / 5), (i % 2 == 0 ? 5 : 2.2) * Math.Sin(i * Math.PI / 5))).ToArray();
                    var star = new PolylineCurve(pts);
                    d.Step("Start with a closed, planar curve.").Input(star);
                    d.Step("Run ExtrudeCrv, pick the curve, and set Solid=Yes to cap both ends.").Mark(pts[0], "1");
                    var ext = Extrusion.Create(star, Plane.WorldXY, 3, true);
                    d.Step("Drag or type a distance (3 here). The curve is pushed straight out into a solid.")
                        .ClearMarks().Result(ext?.ToBrep()!);
                },
                ["Cap"] = (d, tol) =>
                {
                    var open = Extrusion.Create(new Circle(Point3d.Origin, 3).ToNurbsCurve(), Plane.WorldXY, 6, false);
                    var closed = Extrusion.Create(new Circle(Point3d.Origin, 3).ToNurbsCurve(), Plane.WorldXY, 6, true);
                    d.Step("Start with a polysurface that has flat, open ends, like this tube.").Input(open?.ToBrep()!);
                    d.Step("Run Cap and pick it.").Mark(P(3, 0, 3), "1");
                    d.Step("Rhino fills each planar hole with a flat surface and joins it, making a closed solid.")
                        .ClearMarks().ClearInputs().Result(closed?.ToBrep()!);
                },
                ["BooleanUnion"] = (d, tol) =>
                {
                    var box = new Box(Plane.WorldXY, new Interval(0, 8), new Interval(0, 5), new Interval(0, 3)).ToBrep();
                    var cyl = new Cylinder(new Circle(P(4, 2.5), 2), 7).ToBrep(true, true);
                    d.Step("Start with overlapping closed solids.").Input(box, cyl);
                    d.Step("Run BooleanUnion, pick all of them and press Enter.").Mark(P(0, 0, 3), "1").Mark(P(4, 2.5, 7), "2");
                    d.Step("They merge into one closed polysurface. The parts inside each other are removed.")
                        .ClearMarks().ClearInputs().Results(Brep.CreateBooleanUnion(new[] { box, cyl }, tol));
                },
                ["BooleanDifference"] = (d, tol) =>
                {
                    var box = new Box(Plane.WorldXY, new Interval(0, 6), new Interval(0, 6), new Interval(0, 6)).ToBrep();
                    var sphere = new Sphere(P(6, 6, 6), 3.5).ToBrep();
                    d.Step("Start with two overlapping closed solids: one to keep, one to cut with.").Input(box, sphere);
                    d.Step("Run BooleanDifference, pick the box to keep and press Enter.").Mark(P(0, 0, 6), "1");
                    d.Step("Pick the sphere to subtract and press Enter. DeleteInput=No keeps the cutter.").Mark(P(6, 6, 9.5), "2");
                    d.Step("The sphere's volume is cut out of the box.")
                        .ClearMarks().ClearInputs().Input(sphere).Results(Brep.CreateBooleanDifference(box, sphere, tol));
                },
                ["BooleanIntersection"] = (d, tol) =>
                {
                    var box = new Box(Plane.WorldXY, new Interval(0, 6), new Interval(0, 6), new Interval(0, 6)).ToBrep();
                    var sphere = new Sphere(P(6, 6, 6), 4.5).ToBrep();
                    d.Step("Start with two overlapping closed solids.").Input(box, sphere);
                    d.Step("Run BooleanIntersection, pick the first set and press Enter.").Mark(P(0, 0, 6), "1");
                    d.Step("Pick the second set and press Enter.").Mark(P(6, 6, 10.5), "2");
                    d.Step("Only the volume they share is kept.")
                        .ClearMarks().ClearInputs().Results(Brep.CreateBooleanIntersection(box, sphere, tol));
                },
                ["FilletEdge"] = (d, tol) =>
                {
                    var box = new Box(Plane.WorldXY, new Interval(0, 8), new Interval(0, 5), new Interval(0, 4)).ToBrep();
                    var edges = Enumerable.Range(0, box.Edges.Count).ToList();
                    var radii = edges.Select(_ => 0.8).ToList();
                    d.Step("Start with a closed polysurface with sharp edges.").Input(box);
                    d.Step("Run FilletEdge, set NextRadius (0.8 here), and pick the edges to round. SelChain picks a whole tangent chain.")
                        .Mark(P(4, 0, 4), "1").Mark(P(8, 2.5, 4), "2");
                    d.Step("Press Enter twice. A ball of that radius rolls along each edge and rounds it.")
                        .ClearMarks().Results(Brep.CreateFilletEdges(box, edges, radii, radii, BlendType.Fillet, RailType.RollingBall, tol));
                },

                // ---------------- Transform ----------------
                ["Move"] = (d, tol) =>
                {
                    var box = SmallBox();
                    var to = new Vector3d(9, 4, 0);
                    d.Step("Run Move and select the objects.").Input(box);
                    d.Step("Pick the point to move from. Snap to something on the object for precision.").Mark(P(0, 0), "1");
                    d.Step("Pick the point to move to. The objects travel along the vector between the two points.")
                        .Mark(P(9, 4), "2").Guide(P(0, 0), P(9, 4));
                    d.Step("Done. Tip: the Gumball arrows do quick moves without a command.")
                        .ClearInputs().Result(Moved(box, Transform.Translation(to)));
                },
                ["Copy"] = (d, tol) =>
                {
                    var box = SmallBox();
                    d.Step("Run Copy and select the objects.").Input(box);
                    d.Step("Pick the point to copy from.").Mark(P(0, 0), "1");
                    d.Step("Pick where each copy goes. Copy keeps going until you press Enter.")
                        .Mark(P(6, 0), "2").Mark(P(12, 0), "3").Guide(P(0, 0), P(12, 0));
                    d.Step("The original stays where it was.")
                        .Result(Moved(box, Transform.Translation(6, 0, 0)), Moved(box, Transform.Translation(12, 0, 0)));
                },
                ["Rotate"] = (d, tol) =>
                {
                    var box = new Box(Plane.WorldXY, new Interval(2, 8), new Interval(-1, 1), new Interval(0, 2)).ToBrep();
                    var rot = Transform.Rotation(RhinoMath.ToRadians(60), Vector3d.ZAxis, Point3d.Origin);
                    var tip = P(8, 0); tip.Transform(rot);
                    d.Step("Run Rotate and select the objects.").Input(box);
                    d.Step("Pick the center of rotation.").Mark(Point3d.Origin, "1");
                    d.Step("Pick a first reference point, or just type the angle.").Mark(P(8, 0), "2").Guide(Point3d.Origin, P(8, 0));
                    d.Step("Pick the second reference point (60 degrees here). Copy=Yes keeps the original.")
                        .Mark(tip, "3").Guide(Point3d.Origin, tip);
                    d.Step("The objects turn around the center in the construction plane. Rotate3D turns around any axis.")
                        .ClearGuides().ClearMarks().Result(Moved(box, rot));
                },
                ["Scale"] = (d, tol) =>
                {
                    var box = SmallBox();
                    d.Step("Run Scale and select the objects.").Input(box);
                    d.Step("Pick the base point. It's the one point that doesn't move.").Mark(P(0, 0), "1");
                    d.Step("Type a scale factor (1.8 here), or pick two reference points to scale by their ratio.")
                        .Mark(P(4, 0), "2").Mark(P(7.2, 0), "3").Guide(P(0, 0), P(7.2, 0));
                    d.Step("Everything grows by the same factor in X, Y and Z.")
                        .ClearMarks().ClearGuides().Result(Moved(box, Transform.Scale(Point3d.Origin, 1.8)));
                },
                ["Scale1D"] = (d, tol) =>
                {
                    var box = SmallBox();
                    d.Step("Run Scale1D and select the objects.").Input(box);
                    d.Step("Pick the base point.").Mark(P(0, 0), "1");
                    d.Step("Pick the direction and a reference point, then the new length (2.5x here).")
                        .Mark(P(4, 0), "2").Mark(P(10, 0), "3").Guide(P(0, 0), P(10, 0));
                    d.Step("Objects stretch in that one direction only.")
                        .ClearMarks().ClearGuides().Result(Moved(box, Transform.Scale(Plane.WorldXY, 2.5, 1, 1)));
                },
                ["Mirror"] = (d, tol) =>
                {
                    var l = Extrusion.Create(new PolylineCurve(new[] { P(2, 0), P(7, 0), P(7, 2), P(4, 2), P(4, 6), P(2, 6), P(2, 0) }), Plane.WorldXY, 2, true)?.ToBrep();
                    if (l == null) return;
                    d.Step("Run Mirror and select the objects.").Input(l);
                    d.Step("Pick the start of the mirror line.").Mark(P(0, -1), "1");
                    d.Step("Pick the end of the mirror line. Hold Shift to keep it straight.").Mark(P(0, 7), "2").Guide(P(0, -1), P(0, 7));
                    d.Step("A flipped copy appears on the other side. Set Copy=No to flip the original instead.")
                        .ClearMarks().Result(Moved(l, Transform.Mirror(new Plane(Point3d.Origin, Vector3d.XAxis))));
                },
                ["Array"] = (d, tol) =>
                {
                    var box = new Box(Plane.WorldXY, new Interval(0, 2), new Interval(0, 2), new Interval(0, 2)).ToBrep();
                    d.Step("Run Array and select the objects.").Input(box);
                    d.Step("Type the number in X, Y and Z (4 x 3 x 1 here).");
                    d.Step("Set the spacing, or pick it, and press Enter. Copies fill a grid.")
                        .Results(from x in Enumerable.Range(0, 4) from y in Enumerable.Range(0, 3)
                                 where x + y > 0
                                 select Moved(box, Transform.Translation(x * 3.5, y * 3.5, 0)));
                },
                ["ArrayPolar"] = (d, tol) =>
                {
                    var source = new Box(Plane.WorldXY, new Interval(5, 7), new Interval(-0.75, 0.75), new Interval(0, 3)).ToBrep();
                    d.Step("Run ArrayPolar and select the object to copy.").Input(source);
                    d.Step("Pick the center of the array.").Mark(Point3d.Origin, "1");
                    d.Step("Type the number of items (8) and the fill angle (360 for a full circle), then press Enter.")
                        .Results(Enumerable.Range(1, 7).Select(i => Moved(source, Transform.Rotation(i * Math.PI / 4, Vector3d.ZAxis, Point3d.Origin))));
                },
                ["ArrayCrv"] = (d, tol) =>
                {
                    var path = Curve.CreateInterpolatedCurve(new[] { P(0, 0), P(5, 4), P(10, 1), P(15, 5) }, 3);
                    var item = new Box(new Plane(Point3d.Origin, Vector3d.ZAxis), new Interval(-0.6, 0.6), new Interval(-0.6, 0.6), new Interval(0, 2)).ToBrep();
                    d.Step("Start with an object and a path curve.").Input(item, path);
                    d.Step("Run ArrayCrv, select the object, then pick the path curve.").Mark(path.PointAtEnd, "path");
                    var ts = path.DivideByCount(7, true) ?? new double[0];
                    d.Step("Set the number of items or the distance between them. Copies follow the curve and turn with it.")
                        .ClearMarks().Results(ts.Select(t =>
                        {
                            // X along the curve, Y to its left, Z stays up.
                            var tangent = path.TangentAt(t);
                            var frame = new Plane(path.PointAt(t), tangent, Vector3d.CrossProduct(Vector3d.ZAxis, tangent));
                            return Moved(item, Transform.PlaneToPlane(Plane.WorldXY, frame));
                        }));
                },
                ["Twist"] = (d, tol) => Deform(d,
                    "Run Twist and select the objects.",
                    "Pick the start and end of the twist axis.",
                    "Type the angle (90 degrees here). Infinite=Yes twists the whole object, not only the part along the axis.",
                    new TwistSpaceMorph { TwistAxis = new Line(Point3d.Origin, P(0, 0, 10)), TwistAngleRadians = Math.PI / 2 }),
                ["Bend"] = (d, tol) => Deform(d,
                    "Run Bend and select the objects.",
                    "Pick the start and end of the spine, the part that will bend.",
                    "Pick a point to bend through. The spine curves into an arc and the object follows it.",
                    new BendSpaceMorph(Point3d.Origin, P(0, 0, 10), P(5, 0, 9), false, false),
                    bendPoint: P(5, 0, 9)),
                ["Taper"] = (d, tol) => Deform(d,
                    "Run Taper and select the objects.",
                    "Pick the start and end of the taper axis.",
                    "Pick the start distance, then the end distance. The object narrows (or widens) along the axis.",
                    new TaperSpaceMorph(Point3d.Origin, P(0, 0, 10), 1.5, 0.4, false, false)),
                ["Stretch"] = (d, tol) => Deform(d,
                    "Run Stretch and select the objects.",
                    "Pick the start and end of the stretch axis.",
                    "Pick the new end point. Everything along the axis stretches to the new length.",
                    new StretchSpaceMorph(Point3d.Origin, P(0, 0, 10), 15)),
                ["Maelstrom"] = (d, tol) =>
                {
                    var flat = Mesh.CreateFromBox(new Box(Plane.WorldXY, new Interval(-8, 8), new Interval(-1, 1), new Interval(0, 0.5)), 60, 6, 2);
                    var swirled = flat.DuplicateMesh();
                    new MaelstromSpaceMorph(Plane.WorldXY, 1, 8, Math.PI).Morph(swirled);
                    d.Step("Run Maelstrom and select the objects.").Input(flat);
                    d.Step("Pick the center, then the first and second radius. Objects between them are affected.")
                        .Mark(Point3d.Origin, "1").Mark(P(1, 0), "2").Mark(P(8, 0), "3");
                    d.Step("Type the angle (180 degrees here). Objects spiral around the center, like water down a drain.")
                        .ClearMarks().ClearInputs().Result(swirled);
                },
                ["Shear"] = (d, tol) =>
                {
                    var box = new Box(Plane.WorldXY, new Interval(0, 4), new Interval(0, 3), new Interval(0, 6)).ToBrep();
                    var shear = Transform.Shear(Plane.WorldXY, Vector3d.XAxis, Vector3d.YAxis, new Vector3d(0.6, 0, 1));
                    d.Step("Run Shear and select the objects.").Input(box);
                    d.Step("Pick the origin point, then a reference point. These stay put.").Mark(P(0, 0), "1").Mark(P(4, 0), "2");
                    d.Step("Pick or type the shear angle. The object slants like a pushed deck of cards.")
                        .ClearMarks().ClearInputs().Result(Moved(box, shear));
                },
            };

        private static readonly Dictionary<string, CommandDemo?> Cache =
            new Dictionary<string, CommandDemo?>(StringComparer.OrdinalIgnoreCase);

        public static bool Has(string command) => Builders.ContainsKey(command);

        /// <summary>Builds (once) the demo for a command, or null if it has none or the geometry failed.</summary>
        public static CommandDemo? Get(string command, double tolerance)
        {
            if (Cache.TryGetValue(command, out var cached))
                return cached;
            CommandDemo? demo = null;
            if (Builders.TryGetValue(command, out var build))
            {
                try
                {
                    var builder = new DemoBuilder();
                    build(builder, tolerance);
                    demo = builder.Build(command);
                    if (demo.Steps.Count == 0)
                        demo = null;
                }
                catch (Exception ex)
                {
                    RhinoApp.WriteLine("RhinoGotchi: could not build the {0} demo ({1}).", command, ex.Message);
                }
            }
            Cache[command] = demo;
            return demo;
        }

        private static Point3d P(double x, double y, double z = 0) => new Point3d(x, y, z);

        private static Brep SmallBox() =>
            new Box(Plane.WorldXY, new Interval(0, 4), new Interval(0, 3), new Interval(0, 2)).ToBrep();

        private static GeometryBase Moved(GeometryBase g, Transform xf) => ShapeGeometry.Moved(g, xf);

        /// <summary>A flat or rising spiral as an interpolated curve.</summary>
        private static Curve Spiral(double turns, double r0, double r1, double height)
        {
            int count = (int)(turns * 24);
            var pts = Enumerable.Range(0, count + 1).Select(i =>
            {
                double t = (double)i / count;
                double a = t * turns * 2 * Math.PI;
                double r = r0 + (r1 - r0) * t;
                return P(r * Math.Cos(a), r * Math.Sin(a), height * t);
            });
            return Curve.CreateInterpolatedCurve(pts, 3);
        }

        /// <summary>Shared steps for Twist, Bend, Taper and Stretch on a dense mesh bar.</summary>
        private static void Deform(DemoBuilder d, string select, string axis, string finish, SpaceMorph morph, Point3d? bendPoint = null)
        {
            var bar = Mesh.CreateFromBox(new Box(Plane.WorldXY, new Interval(-1.5, 1.5), new Interval(-1.5, 1.5), new Interval(0, 10)), 8, 8, 40);
            var result = bar.DuplicateMesh();
            morph.Morph(result);
            d.Step(select).Input(bar);
            d.Step(axis).Mark(Point3d.Origin, "1").Mark(P(0, 0, 10), "2").Guide(Point3d.Origin, P(0, 0, 10));
            d.Step(finish);
            if (bendPoint.HasValue)
                d.Mark(bendPoint.Value, "3");
            d.Step("Done. These deformations work best on objects with enough control points, like this dense mesh.")
                .ClearMarks().ClearGuides().ClearInputs().Result(result);
        }
    }
}
