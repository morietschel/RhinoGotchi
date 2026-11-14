using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;
using RhinoGotchi.Levels;

namespace RhinoGotchi.Tutorials.Content
{
    /// <summary>
    /// Part E: details. From the model: the steel connection plates are 0.95 × 0.29 m and 0.020 m
    /// thick, fixed with Ø 0.020 m dowels; the stair handrail is a Ø 0.042 m tube following the
    /// curved stair; the balustrade is drawn as 790 sloping rods, simplified here to vertical bars.
    /// Detail tutorials are built 25 m east of the building, clear of the model.
    /// </summary>
    internal static class DetailTutorials
    {
        /// <summary>Where the detail tutorials are built: east of the terrace, outside the model.</summary>
        public static readonly Vector3d DetailShift = new Vector3d(25, 0, 0);

        // ---------------------------------------------------------------- 10 BooleanDifference

        public const double PlateWidth = 0.29;
        public const double PlateLength = 0.95;
        public const double PlateThickness = 0.020;
        public const double HoleRadius = 0.010;
        public const double CutterHeight = 0.040;
        public static readonly Point3d PlateCorner = new Point3d(45, 5, 0);

        /// <summary>Hole centres relative to the plate corner (assumed layout).</summary>
        public static readonly Point2d[] Holes =
        {
            new Point2d(0.075, 0.10), new Point2d(0.215, 0.10),
            new Point2d(0.075, 0.85), new Point2d(0.215, 0.85),
        };

        public static void BooleanDifference(Tutorial t)
        {
            t.Goal = "Model a steel connection plate with four bolt holes: a solid plate, cylinders that pass right through it, and BooleanDifference to cut them out. Then see what happens when a cutter doesn't go all the way through.";
            t.InputGeometry = $"On the Input layer, east of the building at (45, 5, 0): a point for the plate's corner, four points marking hole centres 1 cm below the plate, and one short orange cylinder (the \"short cutter\") for the failed-cut step. The magenta highlight shows the model's 24 plates, {PlateLength:0.00} × {PlateWidth:0.00} m and {PlateThickness:0.000} m thick, and their Ø {2 * HoleRadius:0.000} m dowels.";

            t.Steps.Add(new TutorialStep("Turn on the Point object snap.", null, "Perspective"));
            t.Steps.Add(new TutorialStep($"Run Box. Click the Plate corner point, type @{PlateWidth},{PlateLength} for the other corner and press Enter, then type {PlateThickness} for the height. That's the plate.", "Box"));
            t.Steps.Add(new TutorialStep($"Run Cylinder. Click Hole 1's point for the base centre, type {HoleRadius} for the radius (Ø {2 * HoleRadius:0.000} m), then {CutterHeight} for the height. The cylinder starts 1 cm below the plate and ends 1 cm above it.", "Cylinder"));
            t.Steps.Add(new TutorialStep("Make the other three cutters the same way at Hole 2, 3 and 4, or Copy the first one onto them.", "Copy"));
            t.Steps.Add(new TutorialStep("Run BooleanDifference. Select the plate first and press Enter, then select the four cylinders and press Enter. The cylinders disappear and leave four clean holes.", "BooleanDifference"));
            t.Steps.Add(new TutorialStep("Now the failed cut: run BooleanDifference again, select the plate, press Enter, then select the orange short cutter and press Enter.", "BooleanDifference"));
            t.Steps.Add(new TutorialStep("Look underneath: the short cutter starts inside the plate, so it only leaves a shallow pocket in the top, not a hole. Undo it. A cutter has to pass right through both faces to make a hole."));
            t.Steps.Add(new TutorialStep("Select the plate and check the status bar: it should still be a closed solid polysurface."));

            t.KeyOptions.Add("Selection order: first the object to cut from (the plate), Enter, then the cutters. Swap them and you're left with bits of the cylinders instead.");
            t.KeyOptions.Add("DeleteInput: Yes (the default) removes the cutters after the cut. Set it to No to keep them for another try.");
            t.KeyOptions.Add("Cylinder height: make cutters a bit longer than the plate is thick, so they stick out on both sides.");
            t.KeyOptions.Add("Cylinder radius versus diameter: Cylinder asks for the radius by default. Its Diameter option switches to diameter.");

            t.CommonMistakes.Add("Selecting the cylinders first and the plate second. Rhino subtracts the plate from the cylinders and you get small discs. Undo and pick the plate first.");
            t.CommonMistakes.Add("Cutters that end exactly at the plate's faces or stop inside it. You get a pocket, a thin skin, or the cut fails. Make every cutter longer than the plate is thick.");

            t.Check = $"The plate is still one closed solid with four holes right through it. Its volume (run Volume) is {PlateWidth:0.00} × {PlateLength:0.00} × {PlateThickness:0.000} minus the four holes: about {PlateVolumeWithHoles():0.000000} m³.";

            t.AssumedDimensions.Add(new AssumedDimension("Hole layout", 4, "holes", "two at each end, 0.10 m from the short edges; the model's dowel positions vary from plate to plate"));
            t.AssumedDimensions.Add(new AssumedDimension("Cutter height", CutterHeight, "m", "1 cm longer than the plate on each side"));
            t.AssumedDimensions.Add(new AssumedDimension("Work area", 45, "m east (X)", "a clear spot next to the building"));

            t.SetUp = context =>
            {
                context.AddInput(new Point(PlateCorner), "Plate corner");
                for (int i = 0; i < Holes.Length; i++)
                    context.AddInput(new Point(PlateCorner + new Vector3d(Holes[i].X, Holes[i].Y, -0.01)), $"Hole {i + 1}");
                // Starts halfway into the plate and exits the top: leaves a pocket, not a hole.
                var shortBase = new Plane(PlateCorner + new Vector3d(PlateWidth / 2, PlateLength / 2, PlateThickness / 2), Vector3d.ZAxis);
                var shortCutter = new Cylinder(new Circle(shortBase, HoleRadius), PlateThickness);
                context.AddInput(shortCutter.ToBrep(true, true), "Short cutter");
            };
        }

        public static double PlateVolumeWithHoles() =>
            PlateWidth * PlateLength * PlateThickness - Holes.Length * Math.PI * HoleRadius * HoleRadius * PlateThickness;

        // ---------------------------------------------------------------- 11 Pipe

        public const double RailRadius = 0.021;
        public const double ReturnGap = 0.15;
        public const double ReturnLength = 0.30;
        public const double TooBigRadius = 0.6;

        public static void Pipe(Tutorial t)
        {
            t.Goal = "Model a short tubular handrail with Pipe: a clean centreline that slopes up the stair and turns smoothly into a level return, then a round tube along it.";
            t.InputGeometry = $"On the Input layer, 25 m east of the stair: five orange points along the top part of the model's handrail centreline (rising about 1.2 m), then two points for a level return: Return start, {ReturnGap:0.00} m on and about 3 cm higher than the last rail point (so the bend can rise smoothly into the level part), and Return end, {ReturnLength:0.00} m after that. The blue see-through tube is the real handrail, moved the same 25 m. It's Ø {2 * RailRadius:0.000} m.";

            t.Steps.Add(new TutorialStep("Turn on the Point and End object snaps.", null, "Perspective"));
            t.Steps.Add(new TutorialStep("Run InterpCrv through Rail points 1 to 5, low to high. Press Enter. That's the sloping run.", "InterpCrv"));
            t.Steps.Add(new TutorialStep("Run Line from the Return start point to the Return end point. That's the level return. There's a small gap between it and the run.", "Line"));
            t.Steps.Add(new TutorialStep("Run BlendCrv. Click the run near its top end, then the return near its start. Set Continuity to Curvature on both ends and click OK. A smooth bend fills the gap.", "BlendCrv"));
            t.Steps.Add(new TutorialStep("Select the run, the blend and the return and run Join. It's now one smooth centreline.", "Join"));
            t.Steps.Add(new TutorialStep($"Run Pipe and select the centreline. For the start radius type {RailRadius} and press Enter. Press Enter again to use the same radius at the end. Set Cap=Round, then press Enter.", "Pipe"));
            t.Steps.Add(new TutorialStep($"See what a too-tight bend does: Undo, and pipe again with a radius of {TooBigRadius}. The bend is now tighter than the tube is thick, so the tube folds into itself at the corner. Undo and keep the {RailRadius} tube.", "Pipe"));
            t.Steps.Add(new TutorialStep("Compare your tube with the blue handrail. Along the sloping run they should overlap."));

            t.KeyOptions.Add($"Radius versus diameter: Pipe asks for a radius. Click Diameter on the command line to type the diameter instead ({2 * RailRadius:0.000} here).");
            t.KeyOptions.Add("Cap: None leaves the ends open, Flat closes them with flat discs, Round with half-spheres (nice for handrails).");
            t.KeyOptions.Add("Thick: makes a hollow tube with a wall thickness, like a real steel rail.");
            t.KeyOptions.Add("BlendCrv continuity: Curvature gives the smoothest bend; Position would just join the ends with a kink.");

            t.CommonMistakes.Add($"Typing the diameter when Pipe asks for the radius. The tube comes out twice as thick ({4 * RailRadius:0.000} m). Type {RailRadius}, or switch to Diameter first.");
            t.CommonMistakes.Add("A broken or kinked centreline: pieces that aren't joined, or meet at a sharp corner. Pipe then makes separate tubes or a pinched corner. Blend the gap and join the pieces first.");

            t.Check = $"The tube is Ø {2 * RailRadius:0.000} m everywhere (measure across it with Distance, Quad snap), the bend is smooth, and it doesn't run into itself: in a shaded view the inside of the bend is a clean curve with no creases or tube poking through. (Rhino's Check command calls a self-overlapping tube valid, so look, don't rely on Check.)";

            t.AssumedDimensions.Add(new AssumedDimension("Return length", ReturnLength, "m", "a typical level extension at the top of a stair handrail"));
            t.AssumedDimensions.Add(new AssumedDimension("Gap for the bend", ReturnGap, "m", "room for a smooth bend between the slope and the level return"));
            t.AssumedDimensions.Add(new AssumedDimension("Moved east", DetailShift.X, "m", "keeps the tutorial clear of the real stair"));

            t.SetUp = context =>
            {
                var run = HandrailRun(context);
                for (int i = 0; i < run.Points.Count; i++)
                    context.AddInput(new Point(run.Points[i]), $"Rail point {i + 1}");
                context.AddInput(new Point(run.ReturnStart), "Return start");
                context.AddInput(new Point(run.ReturnEnd), "Return end");
            };
            t.CreateLevel = context =>
            {
                Level level = TutorialLevels.DefaultLevel(t);
                Brep? rail = HandrailRun(context).Ghost;
                if (rail != null)
                    level.GhostGeometry.Add(rail);
                return level;
            };
        }

        /// <summary>
        /// Five points along the top part of the model's first handrail centreline (low to high), the
        /// level return's two points, and the real handrail as a ghost, all moved by <see cref="DetailShift"/>.
        /// Standalone: a made-up curved run rising 1.2 m and no ghost.
        /// </summary>
        public static (List<Point3d> Points, Point3d ReturnStart, Point3d ReturnEnd, Brep? Ghost) HandrailRun(TutorialContext context)
        {
            List<Point3d>? centre = null;
            Brep? ghost = null;
            if (context.Reference != null)
            {
                foreach (GeometryBase g in context.Reference.StairRails)
                {
                    Brep? b = FacadeTutorials.AsBrep(g);
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

            // The return heads on, level, in the direction the learner's InterpCrv leaves its last point.
            // It starts a little higher than that point: a curve leaving at slope θ that ends level after
            // a horizontal distance d rises d·tan(θ/2), so the blend is one clean arc instead of an S.
            Vector3d tangent = RunCurve(run).TangentAtEnd;
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

        /// <summary>The run as the learner draws it: InterpCrv through the points (degree 3, chord-length knots, its default).</summary>
        public static Curve RunCurve(IEnumerable<Point3d> points) =>
            Curve.CreateInterpolatedCurve(points, 3, CurveKnotStyle.Chord);

        // The centre line of a round tube: the centres of its circular cross-sections along the main
        // face, only where the face actually exists (the surface may run on past its trim).
        private static List<Point3d>? TubeCentreline(Brep tube, int count)
        {
            BrepFace face = tube.Faces.OrderByDescending(f => AreaMassProperties.Compute(f.DuplicateFace(false))?.Area ?? 0).First();
            Surface s = face.UnderlyingSurface();
            NurbsSurface ns = s.ToNurbsSurface();
            int around = ns.Degree(0) == 2 ? 0 : 1;
            int along = 1 - around;
            Interval run = s.Domain(along), ring = s.Domain(around);

            // Find the part of the run the trim keeps.
            double lo = double.NaN, hi = double.NaN;
            const int Probe = 400;
            for (int i = 0; i <= Probe; i++)
            {
                double t = run.ParameterAt(i / (double)Probe);
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

        // ---------------------------------------------------------------- 12 ArrayCrv

        public const double BarHeight = 0.90;
        public const double BarSpacing = 0.12;

        public static void ArrayCrv(Tutorial t)
        {
            t.Goal = "Place balustrade bars along the curved, sloping handrail with ArrayCrv, and see how the orientation setting decides whether the bars stay vertical.";
            t.InputGeometry = $"On the Input layer, 25 m east of the stair: the handrail centreline from tutorial 11 (a copy, so this works even if you skipped it) and one vertical bar, {BarHeight:0.00} m tall, hanging from the centreline's low end. The model draws its balustrade as 790 sloping rods; here it's simplified to vertical bars.";

            t.Steps.Add(new TutorialStep("Turn on the End object snap.", null, "Perspective"));
            t.Steps.Add(new TutorialStep("Select the bar and run ArrayCrv.", "ArrayCrv"));
            t.Steps.Add(new TutorialStep("For the base point, click the bar's top end. It sits on the start of the centreline, which keeps every copy hanging from the rail."));
            t.Steps.Add(new TutorialStep("Click the centreline near its low end as the path curve."));
            t.Steps.Add(new TutorialStep($"In the dialog choose Distance between items and type {BarSpacing}. Set Orientation to No rotation. Click OK."));
            t.Steps.Add(new TutorialStep("Look from the Front view: every bar hangs straight down from the rail, evenly spaced.", null, "Front"));
            t.Steps.Add(new TutorialStep("Undo and run ArrayCrv again with Orientation Freeform. The bars tilt with the slope of the rail, because each copy turns to follow the curve's direction. Undo and keep the No rotation version.", "ArrayCrv"));

            t.KeyOptions.Add("Number of items or Distance between items: give a count to fit exactly that many, or a spacing to place one every so often.");
            t.KeyOptions.Add("Orientation, Freeform: each copy turns to follow the curve in 3D, so on a slope the bars tilt.");
            t.KeyOptions.Add("Orientation, No rotation: copies are only moved, never turned, so vertical bars stay vertical. Right for balustrades.");
            t.KeyOptions.Add("Orientation, Roadlike: copies turn to follow the curve in plan but stay upright relative to the construction plane.");

            t.CommonMistakes.Add("Picking a base point that isn't on the path's start, for example the bar's bottom. Every copy is offset by that gap and the bars float below or above the rail. Use the bar's top, on the curve.");
            t.CommonMistakes.Add("Leaving Orientation on Freeform. On a sloping rail the bars lean. Use No rotation.");

            t.Check = $"Every bar stays vertical (within 1°), they're all {BarHeight:0.00} m tall, and the gaps between neighbouring bars are equal, {BarSpacing} m along the rail.";

            t.AssumedDimensions.Add(new AssumedDimension("Bar spacing", BarSpacing, "m", "a common balustrade spacing; the model's balustrade is a pattern of sloping rods instead"));
            t.AssumedDimensions.Add(new AssumedDimension("Bar height", BarHeight, "m", "a common handrail height"));
            t.AssumedDimensions.Add(new AssumedDimension("Moved east", DetailShift.X, "m", "keeps the tutorial clear of the real stair"));

            t.SetUp = context =>
            {
                Curve centreline = RunCurve(HandrailRun(context).Points);
                context.AddInput(centreline, "Handrail centreline");
                Point3d top = centreline.PointAtStart;
                context.AddInput(new LineCurve(top, top - new Vector3d(0, 0, BarHeight)), "Bar");
            };
        }
    }
}
