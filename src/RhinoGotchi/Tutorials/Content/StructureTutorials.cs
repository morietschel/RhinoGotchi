using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;
using RhinoGotchi.Levels;

namespace RhinoGotchi.Tutorials.Content
{
    /// <summary>
    /// Part C: setting out and structure. From the model: radial roof beams sit every 15° around the
    /// building centre (8 long main beams every 45° plus 16 short ones); the roof border is a closed
    /// curve about 93 m long with an edge strip about 0.17 m deep; the joists near the north-east
    /// roof patch run north–south about 0.49 m apart.
    /// </summary>
    internal static class StructureTutorials
    {
        private const string LiftWhy = "tutorial copies float above the real roof so you never pick the original by mistake";

        // ---------------------------------------------------------------- 5 ArrayPolar

        public const int RadialCount = 24;

        public static void ArrayPolar(Tutorial t)
        {
            t.Goal = "Lay out the roof's radial setting-out: one upright reference plane through the building centre, copied around it with ArrayPolar, one plane for every radial beam.";
            t.InputGeometry = "On the Input layer: a point marking the building centre on the ground at (20.00, 17.49, 0), one orange upright plane running 16 m due east from it (along a long main beam), and an orange copy of the roof lifted 4 m. The magenta highlight shows the model's main beams: 8 long ones every 45° and 16 short ones in between, a radial beam every 15°.";

            t.Steps.Add(new TutorialStep("Switch to the Top view and turn on the Point object snap.", null, "Top"));
            t.Steps.Add(new TutorialStep("Select the orange reference plane and run ArrayPolar.", "ArrayPolar"));
            t.Steps.Add(new TutorialStep("For the centre of the array, click the Building centre point. The planes will turn around a vertical axis through it."));
            t.Steps.Add(new TutorialStep($"For the number of items type {RadialCount} and press Enter. The count includes the plane you started with."));
            t.Steps.Add(new TutorialStep("For the angle to fill, press Enter to accept 360. Rhino spreads the 24 planes evenly: 360 ÷ 24 = 15° apart. Press Enter again to finish."));
            t.Steps.Add(new TutorialStep("Compare with the magenta beams: every third plane lines up with a long main beam (every 45°), and the planes between line up with the short beams.", null, "Top"));
            t.Steps.Add(new TutorialStep("Run Intersect with the roof copy and two neighbouring planes, for example the ones at 0° and 15°. You get two roof profiles.", "Intersect", "Perspective"));
            t.Steps.Add(new TutorialStep("Look at the two profiles from the side: they rise and fall differently. That is why you can't model one beam and copy it around. Each radial beam follows its own profile."));

            t.KeyOptions.Add("Centre of polar array: the point the copies turn around. In the Top view, the axis is vertical through that point.");
            t.KeyOptions.Add("Number of items: the total, including the original. 24 items over 360° gives one every 15°.");
            t.KeyOptions.Add("Angle to fill: 360 spreads the items all the way round. A smaller angle (for example 90) spreads them over a quarter, with the first and last items at the ends of that angle.");
            t.KeyOptions.Add("Step angle (in the options): set the angle between items instead of the total. 15 with 24 items gives the same result.");

            t.CommonMistakes.Add("Clicking the wrong centre, for example a point on the roof or the world origin. The planes swing around the wrong point and miss the beams. Snap to the Building centre point.");
            t.CommonMistakes.Add("Typing 23 or 25 items. The count includes the original, so you get one gap that's too big, or two planes on top of each other. Use 24.");

            t.Check = "You should have 24 planes, each turned 15° from its neighbours around the vertical line through the building centre, with none on top of another. From the Top view every third plane lies along a long main beam.";

            t.AssumedDimensions.Add(new AssumedDimension("Reference plane length", 16, "m", "the roof reaches about 14.6 m from the centre"));
            t.AssumedDimensions.Add(new AssumedDimension("Reference plane height", 12, "m", "from the ground to above the lifted roof copy"));
            t.AssumedDimensions.Add(new AssumedDimension("Lift above the real roof", TutorialGeometry.Lift, "m", LiftWhy));

            t.SetUp = context =>
            {
                Point3d centre = TutorialGeometry.Centre(context);
                context.AddInput(new Point(centre), "Building centre");
                var plane = new Plane(centre, Vector3d.XAxis, Vector3d.ZAxis);
                context.AddInput(new PlaneSurface(plane, new Interval(0, 16), new Interval(0, 12)).ToBrep(), "Reference plane (due east)");
                context.AddInput(TutorialGeometry.Roof(context), "Roof copy");
            };
        }

        // ---------------------------------------------------------------- 6 Sweep1

        public const double EdgeBeamWidth = 0.20;
        public const double EdgeBeamDepth = 0.17;

        public static void Sweep1(Tutorial t)
        {
            t.Goal = "Model a curved beam along the roof edge with Sweep1: take the rail from the roof's border, place a rectangular section square to it, and keep the section from twisting as it follows the curve.";
            t.InputGeometry = "On the Input layer: an orange copy of the roof, lifted 4 m, and two points on its edge marking the east quarter (one south-east, one north-east of the centre). The magenta highlight is the model's roof-edge strip, about 0.17 m deep, which this beam sits behind.";

            t.Steps.Add(new TutorialStep("Run DupBorder and select the roof copy. You get one closed curve all the way round the roof edge, about 93 m long.", "DupBorder", "Perspective"));
            t.Steps.Add(new TutorialStep("Run Split, select the border curve and press Enter, choose the Point option, click the two marked points (Point snap), and press Enter. The border is now in two pieces.", "Split"));
            t.Steps.Add(new TutorialStep("Delete the long piece and keep the east quarter. This is your rail."));
            t.Steps.Add(new TutorialStep($"Run Rectangle with the AroundCurve option. Click the rail, then snap to its south-east End. For the size type {EdgeBeamWidth:0.00} and press Enter, then {EdgeBeamDepth:0.00} and press Enter. The rectangle stands square to the rail at its start.", "Rectangle"));
            t.Steps.Add(new TutorialStep("Run Sweep1. Select the rail, then the rectangle, and press Enter.", "Sweep1"));
            t.Steps.Add(new TutorialStep("In the dialog set Frame style to Roadlike Top. The section stays upright as it follows the curve. Click OK."));
            t.Steps.Add(new TutorialStep("Undo and sweep again with Freeform to compare. Along the wavy edge the section slowly rolls, so the beam's top no longer stays level across its width. Keep the Roadlike Top version.", "Sweep1"));
            t.Steps.Add(new TutorialStep("Run Cap on the beam to close both ends. It's now a closed solid.", "Cap"));

            t.KeyOptions.Add("Frame style: Freeform lets the section turn with the curve's own twist. Roadlike Top keeps it upright relative to the Top construction plane, which is what a beam needs.");
            t.KeyOptions.Add("Closed sweep: only for closed rails. Leave it off for the east quarter.");
            t.KeyOptions.Add("Cross-section options: Do not simplify keeps the rectangle exact. Rebuild makes a lighter, approximate surface.");
            t.KeyOptions.Add("Cap closes the planar end openings so the sweep becomes a solid.");

            t.CommonMistakes.Add("Drawing the section flat on the ground instead of square to the rail. The sweep comes out squashed or self-intersecting. Use Rectangle's AroundCurve option at the rail's end.");
            t.CommonMistakes.Add("Leaving Frame style on Freeform on a 3D curve. The section rolls along the way. Look along the beam: if its top edge tilts from side to side, sweep again with Roadlike Top.");

            t.Check = $"Select the beam: the status bar (or What) should call it a closed solid polysurface. Measure the end face with Distance: {EdgeBeamWidth:0.00} m by {EdgeBeamDepth:0.00} m. Look along the beam: its top stays level across its width all the way.";

            t.AssumedDimensions.Add(new AssumedDimension("Beam width", EdgeBeamWidth, "m", "the model shows the edge strip but not the beam behind it"));
            t.AssumedDimensions.Add(new AssumedDimension("Beam depth", EdgeBeamDepth, "m", "taken from the model's roof-edge strip, which averages 0.17 m deep"));
            t.AssumedDimensions.Add(new AssumedDimension("Lift above the real roof", TutorialGeometry.Lift, "m", LiftWhy));

            t.SetUp = context =>
            {
                Brep roof = TutorialGeometry.Roof(context);
                context.AddInput(roof, "Roof copy");
                Curve? border = Curve.JoinCurves(roof.DuplicateNakedEdgeCurves(true, false)).OrderByDescending(c => c.GetLength()).FirstOrDefault();
                if (border == null)
                    return;
                Point3d centre = TutorialGeometry.Centre(context);
                context.AddInput(new Point(PointAtBearing(border, centre, -45)), "Split point (south-east)");
                context.AddInput(new Point(PointAtBearing(border, centre, 45)), "Split point (north-east)");
            };
        }

        /// <summary>The point on a closed curve whose plan direction from the centre is closest to a bearing (degrees from east).</summary>
        public static Point3d PointAtBearing(Curve curve, Point3d centre, double degrees)
        {
            double target = degrees * Math.PI / 180;
            double[] ts = curve.DivideByCount(720, true) ?? new double[] { curve.Domain.Min };
            Point3d best = curve.PointAt(ts[0]);
            double bestDiff = double.MaxValue;
            foreach (double t in ts)
            {
                Point3d p = curve.PointAt(t);
                double angle = Math.Atan2(p.Y - centre.Y, p.X - centre.X);
                double diff = Math.Abs(Math.IEEERemainder(angle - target, 2 * Math.PI));
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    best = p;
                }
            }
            return best;
        }

        // ---------------------------------------------------------------- 7 FlowAlongSrf

        public const double JoistWidth = 0.06;
        public const double JoistDepth = 0.20;
        public const double JoistSpacing = 0.5;
        public const int JoistCount = 12;
        public const double FlowBaseShift = 8.0;

        public static void FlowAlongSrf(Tutorial t)
        {
            t.Goal = "Model a set of joists flat, then bend them all onto the wavy roof in one go with FlowAlongSrf, instead of sweeping each joist on its own.";
            t.InputGeometry = "On the Input layer: a flat orange base surface the same size as the roof patch in plan, 8 m east of it, and an orange copy of the roof patch (X 27.5–33.2 m, Y 17.5–23.4 m) lifted 4 m. That's the target. The magenta highlight shows the model's joists, which run north–south about 0.49 m apart.";

            t.Steps.Add(new TutorialStep("In the Top view, turn on the End object snap.", null, "Top"));
            t.Steps.Add(new TutorialStep($"Run Box. Click the base surface's south-west corner (End snap), type @{JoistWidth:0.00},5.88 for the other corner and press Enter, then type {JoistDepth:0.00} for the height. That's one joist running the full length of the base.", "Box"));
            t.Steps.Add(new TutorialStep($"Select the joist and run Array. Type {JoistCount} for X, 1 for Y and 1 for Z, then {JoistSpacing} for the X spacing (press Enter through the Y and Z spacings). You now have {JoistCount} joists {JoistSpacing} m apart.", "Array"));
            t.Steps.Add(new TutorialStep("Run FlowAlongSrf and select all 12 joists. Press Enter.", "FlowAlongSrf", "Perspective"));
            t.Steps.Add(new TutorialStep("Check the options on the command line: Copy=Yes and Rigid=No. Then click the base surface near its south-west corner."));
            t.Steps.Add(new TutorialStep("Click the roof patch copy near its matching south-west corner. The joists appear on the roof, bent to follow its rise and fall."));
            t.Steps.Add(new TutorialStep("Undo and try again with Rigid=Yes. Each joist keeps its straight shape and is only moved, so it no longer follows the roof. Keep the Rigid=No result.", "FlowAlongSrf"));
            t.Steps.Add(new TutorialStep("Think about the alternative: with Sweep1 you would need a rail curve on the roof and a section for every one of the 12 joists. Flow did them all at once, and adding more joists is just a bigger array."));

            t.KeyOptions.Add("Base and target surfaces: pick each one near the same corner (here south-west on both). The corners you pick decide how the objects are turned.");
            t.KeyOptions.Add("Rigid: No bends the objects to follow the target. Yes keeps their shape and only moves each one into place.");
            t.KeyOptions.Add("Copy: Yes keeps the flat originals, which is handy for trying again.");
            t.KeyOptions.Add("Surface direction: if the result comes out mirrored or upside down, use Dir on the base or target surface, flip it, and flow again.");

            t.CommonMistakes.Add("Picking the two surfaces near different corners. The joists come out turned 90° or mirrored. Pick both near the south-west corner.");
            t.CommonMistakes.Add("Modelling the joists away from the base surface, for example floating above it. They land the same distance above the roof. Start the joists on the base surface.");

            t.Check = "All 12 joists sit on the roof patch copy: their undersides touch the surface (within about 3 cm everywhere: flowing straight boxes onto a curved roof leaves small gaps at the corners) and no two joists overlap. Seen from the Top view they're still 0.5 m apart.";

            t.AssumedDimensions.Add(new AssumedDimension("Joist section", JoistWidth, "m wide", $"by {JoistDepth:0.00} m deep, roughly the model's joist volume divided by its length"));
            t.AssumedDimensions.Add(new AssumedDimension("Joist spacing", JoistSpacing, "m", "the model's joists near this patch are about 0.49 m apart"));
            t.AssumedDimensions.Add(new AssumedDimension("Base surface moved east", FlowBaseShift, "m", "keeps the flat work area clear of the roof"));
            t.AssumedDimensions.Add(new AssumedDimension("Lift above the real roof", TutorialGeometry.Lift, "m", LiftWhy));

            t.SetUp = context =>
            {
                Surface patch = TutorialGeometry.Patch(context);
                context.AddInput(patch.ToBrep(), "Roof patch (target)");
                context.AddInput(FlatBase(patch), "Base surface (flat)");
            };
        }

        /// <summary>A flat copy of the patch's corners, moved east, with the same U and V directions as the patch.</summary>
        public static Brep FlatBase(Surface patch)
        {
            Interval u = patch.Domain(0), v = patch.Domain(1);
            double z = patch.GetBoundingBox(true).Min.Z;
            Point3d Corner(double a, double b)
            {
                Point3d p = patch.PointAt(u.ParameterAt(a), v.ParameterAt(b));
                return new Point3d(p.X + FlowBaseShift, p.Y, z);
            }
            NurbsSurface flat = NurbsSurface.CreateFromCorners(Corner(0, 0), Corner(1, 0), Corner(1, 1), Corner(0, 1));
            return flat.ToBrep();
        }
    }
}
