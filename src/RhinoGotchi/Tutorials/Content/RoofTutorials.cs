using System.Linq;
using Rhino.Geometry;
using RhinoGotchi.Levels;

namespace RhinoGotchi.Tutorials.Content
{
    /// <summary>
    /// Teaching text and set-up for Part A (roof surface) and Part B (working curves from the roof).
    /// Numbers come from the Klosterneuburg model: the roof patch is the 6 × 6 m piece on the
    /// north-east edge (X 27.5–33.2, Y 17.5–23.4) whose height drops from about 5.25 m to 3.8 m;
    /// the building centre is at (20.00, 17.49); one long main beam points due east from it.
    /// </summary>
    internal static class RoofTutorials
    {
        private const string LiftWhy = "tutorial copies float above the real roof so you never pick the original by mistake";

        // ---------------------------------------------------------------- 1 Loft

        public static void Loft(Tutorial t)
        {
            t.Goal = "Rebuild a 6 × 6 m piece of the wavy roof from four section curves with Loft, and see how History keeps the surface tied to its curves so you can reshape it later.";
            t.InputGeometry = "Sixteen orange points on the Input layer: four rows of four, taken from the roof's north-east edge (X 27.5–33.2 m, Y 17.5–23.4 m). Each row will become one section curve. The blue see-through shape is the real roof patch, your target. Everything is lifted 4 m above the roof. (Without the Klosterneuburg model you get a made-up patch at the origin instead.)";

            t.Steps.Add(new TutorialStep("Turn on the Point object snap in the Osnap bar and turn the other snaps off, so clicks land exactly on the orange points."));
            t.Steps.Add(new TutorialStep("Run InterpCrv and click the four points of row 1 (the row at X ≈ 27.5), starting at the Y 23.4 end. Press Enter.", "InterpCrv", "Perspective"));
            t.Steps.Add(new TutorialStep("Draw rows 2, 3 and 4 (X ≈ 29.4, 31.3, 33.2) the same way, always starting at the Y 23.4 end, so all four curves point in the same direction.", "InterpCrv"));
            t.Steps.Add(new TutorialStep("Turn on History: click Record History in the status bar, or run History and set Record=Yes. Now the loft will remember its curves.", "History"));
            t.Steps.Add(new TutorialStep("Run Loft and pick the four curves in order, row 1 to row 4, clicking each one near its Y 23.4 end. Press Enter.", "Loft"));
            t.Steps.Add(new TutorialStep("Look at the arrows near the curve ends. They should all point the same way. If one doesn't, click that curve near its other end to flip it, then press Enter."));
            t.Steps.Add(new TutorialStep("In the Loft Options dialog choose Style: Normal and click OK. Compare your surface with the blue ghost from the Front and Right views."));
            t.Steps.Add(new TutorialStep("Undo, then loft again with Style: Loose. The surface follows the curves' control points instead of the curves themselves, so it's smoother but no longer touches them exactly. Keep the Normal version.", "Loft"));
            t.Steps.Add(new TutorialStep("Select the row 2 curve, turn on its control points, and drag one point up about 0.5 m with the Gumball. Because History was on, the roof surface reshapes itself.", "PointsOn"));

            t.KeyOptions.Add("Style: Normal passes smoothly through every curve. Loose follows the curves' control points (smoother, but off the curves). Tight hugs the curves closely. Straight sections makes flat strips between curves.");
            t.KeyOptions.Add("Curve direction (the arrows): open curves must all point the same way, or the surface twists. Click near a curve's other end to flip it.");
            t.KeyOptions.Add("Cross-section curve options: Do not simplify keeps your curves exactly; Rebuild with N control points makes a lighter surface that only approximates them.");
            t.KeyOptions.Add("Record History (status bar): keeps the loft linked to its curves, so editing a curve updates the surface. It has to be on before you run Loft.");

            t.CommonMistakes.Clear();
            t.CommonMistakes.Add("Picking the curves out of order (for example row 1, 3, 2, 4). The surface folds back on itself. Pick them in the order they sit along the roof.");
            t.CommonMistakes.Add("Drawing the curves in opposite directions. The surface twists like a bow tie. Start every curve from the same end, or flip the arrow in the Loft preview.");

            t.Check = "From the Front and Right views, your loft should lie on the blue ghost, within about 10 cm everywhere. Then drag a control point of one section curve: if the surface follows, History is working.";

            t.AssumedDimensions.Add(new AssumedDimension("Lift above the real roof", TutorialGeometry.Lift, "m", LiftWhy));
            t.AssumedDimensions.Add(new AssumedDimension("Patch size", TutorialGeometry.PatchSize, "m", "a piece small enough for four section curves"));
            t.AssumedDimensions.Add(new AssumedDimension("Fit tolerance", 0.10, "m", "how close counts as matching the real roof"));

            t.SetUp = context =>
            {
                Surface patch = TutorialGeometry.Patch(context);
                var rows = TutorialGeometry.Grid(patch, 4, 4);
                for (int i = 0; i < rows.Count; i++)
                    for (int j = 0; j < rows[i].Length; j++)
                        context.AddInput(new Point(rows[i][j]), $"Row {i + 1} · point {j + 1}");
            };
            t.CreateLevel = context => WithPatchGhost(t, context);
        }

        // ---------------------------------------------------------------- 2 NetworkSrf

        public static void NetworkSrf(Tutorial t)
        {
            t.Goal = "Rebuild the same roof patch from a net of curves running in two directions with NetworkSrf, then use Rebuild to make the surface lighter and easier to edit.";
            t.InputGeometry = "On the Input layer: the four orange boundary curves of the roof patch, and 16 guide points where your network curves will cross (a 4 × 4 grid). The blue see-through shape is the real roof patch. Everything is lifted 4 m above the roof.";

            t.Steps.Add(new TutorialStep("Turn on the Point and End object snaps."));
            t.Steps.Add(new TutorialStep("Draw two north–south curves: run InterpCrv through the four guide points at X ≈ 29.4, from the Y 23.4 end to the Y 17.5 end. Repeat at X ≈ 31.3.", "InterpCrv", "Perspective"));
            t.Steps.Add(new TutorialStep("Draw two east–west curves the same way, through the guide points at Y ≈ 21.4 and Y ≈ 19.5, from the X 27.5 end to the X 33.2 end. Each one should start and end on a boundary curve.", "InterpCrv"));
            t.Steps.Add(new TutorialStep("Check the net from the Top view: every crossing should sit on a guide point, and every curve should touch the curves it crosses.", null, "Top"));
            t.Steps.Add(new TutorialStep("Run NetworkSrf and select all eight curves: the four boundary curves and your four new ones. Press Enter.", "NetworkSrf"));
            t.Steps.Add(new TutorialStep("In the dialog leave the tolerances as they are, set all four edges to Position, and click OK."));
            t.Steps.Add(new TutorialStep("Turn on the surface's control points and look at how many there are. NetworkSrf often makes a dense grid.", "PointsOn"));
            t.Steps.Add(new TutorialStep("Turn the points off, select the surface and run Rebuild. Set Point count to 6 × 6 and Degree to 3 × 3, untick Delete input, click Calculate to see how far the new surface moves, then OK.", "Rebuild"));
            t.Steps.Add(new TutorialStep("Turn on the new surface's control points: fewer, evenly spaced points. Drag one with the Gumball and watch how smoothly the roof bends.", "PointsOn"));

            t.KeyOptions.Add("Edge and interior curve tolerance: how far the surface may stray from your curves. The defaults (the model's tolerance) are right for this patch.");
            t.KeyOptions.Add("Edge matching: Position keeps each edge on its boundary curve. Tangency and Curvature also blend into a neighbouring surface, which this patch doesn't have.");
            t.KeyOptions.Add("Rebuild point count and degree: fewer points give a smoother surface that is easier to edit but deviates more. Calculate shows the deviation before you commit.");
            t.KeyOptions.Add("Rebuild Delete input: untick it to keep the original surface for comparison.");

            t.CommonMistakes.Clear();
            t.CommonMistakes.Add("A curve that stops short of the curve it should cross. NetworkSrf then says the curves don't intersect. Snap every crossing and end to a guide point or a boundary curve.");
            t.CommonMistakes.Add("Using too many curves, or curves that wiggle between crossings. The surface gets lumpy and heavy. Use just enough curves to describe the rise and fall.");

            t.Check = "From the Front and Right views the surface should pass through every curve in the net. After Rebuild it should have at most 10 × 10 control points and still lie within about 8 cm of the blue ghost. (Four curves each way only approximate the real roof, so a few centimetres off is expected.)";

            t.AssumedDimensions.Add(new AssumedDimension("Lift above the real roof", TutorialGeometry.Lift, "m", LiftWhy));
            t.AssumedDimensions.Add(new AssumedDimension("Rebuild point count", 6, "points per direction", "the roof itself uses 15 × 15 for the whole 29 m roof, so 6 × 6 is plenty for a 6 m patch"));
            t.AssumedDimensions.Add(new AssumedDimension("Fit tolerance after Rebuild", 0.08, "m", "a 4 × 4 net lands about 5 cm from the real roof, so 8 cm leaves room for hand-drawn curves"));

            t.SetUp = context =>
            {
                Surface patch = TutorialGeometry.Patch(context);
                int edge = 1;
                foreach (Curve c in TutorialGeometry.Boundary(patch))
                    context.AddInput(c, $"Boundary curve {edge++}");
                var rows = TutorialGeometry.Grid(patch, 4, 4);
                for (int i = 0; i < rows.Count; i++)
                    for (int j = 0; j < rows[i].Length; j++)
                        context.AddInput(new Point(rows[i][j]), $"Guide point {i + 1}-{j + 1}");
            };
            t.CreateLevel = context => WithPatchGhost(t, context);
        }

        // ---------------------------------------------------------------- 3 Contour

        public const double ContourThickness = 0.30;
        public const double ContourShift = 8.0;
        public const double ContourSpacing = 0.5;

        public static void Contour(Tutorial t)
        {
            t.Goal = "Cut evenly spaced profiles through a roof patch with Contour, and see how cutting a surface differs from cutting a solid. These profiles are the starting point for roof ribs.";
            t.InputGeometry = "Two orange copies of the roof patch on the Input layer, lifted 4 m: the patch as a surface (over the real roof, X 27.5–33.2 m), and the same patch thickened 0.30 m downward into a solid, moved 8 m east (X 35.5–41.2 m).";

            t.Steps.Add(new TutorialStep("Switch to the Top view and turn on Ortho, so directions snap to the axes.", null, "Top"));
            t.Steps.Add(new TutorialStep("Run Contour, select the surface copy and press Enter.", "Contour"));
            t.Steps.Add(new TutorialStep("For the base point, type 27.5,20.4 and press Enter. That's the middle of the patch's west edge."));
            t.Steps.Add(new TutorialStep("For the direction, click anywhere to the east (Ortho keeps it on the X axis). The cutting planes stand at right angles to this direction, so they run north–south."));
            t.Steps.Add(new TutorialStep("Type 0.5 for the distance between contours and press Enter. You get about a dozen curves, one every 0.5 m."));
            t.Steps.Add(new TutorialStep("Look at them in Perspective: each one is a single open curve that rises and falls with the roof.", null, "Perspective"));
            t.Steps.Add(new TutorialStep("Run Contour again on the solid copy, with base point 35.5,20.4, direction east and spacing 0.5.", "Contour"));
            t.Steps.Add(new TutorialStep("Compare: cutting the solid gives closed outlines, the top and bottom of a 0.30 m slice joined at the ends. Cutting the surface gives single open lines."));
            t.Steps.Add(new TutorialStep("Organise the surface profiles: select them (SelLast right after Contour selects the latest ones) and run Group, so they move and hide together.", "Group"));

            t.KeyOptions.Add("Base point: where the first cutting plane goes. Every other plane is measured from here.");
            t.KeyOptions.Add("Direction: the planes stand at right angles to it. Point east to get north–south profiles.");
            t.KeyOptions.Add("Distance between contours: the spacing, in model units (metres in this model).");
            t.KeyOptions.Add("Group output: keeps each run's curves in one group, which saves a step when organising them.");

            t.CommonMistakes.Clear();
            t.CommonMistakes.Add("Pointing the direction along the profiles you want instead of across them. You get profiles at right angles to what you meant. The direction is the way the planes step, not the way they face.");
            t.CommonMistakes.Add("Typing the spacing in the wrong units, for example 500 meaning millimetres. This model is in metres, so 500 gives no curves at all. Use 0.5.");

            t.Check = "You should have at least 5 profiles. From the Top view they're straight, parallel lines, and the distance between any two neighbours (Distance command, End snap) is 0.5 m. To turn them into real ribs you would still need a second edge below each profile (Offset or ExtrudeCrv down by the rib depth), a planar surface between the two edges, and a thickness (ExtrudeSrf).";

            t.AssumedDimensions.Add(new AssumedDimension("Lift above the real roof", TutorialGeometry.Lift, "m", LiftWhy));
            t.AssumedDimensions.Add(new AssumedDimension("Solid thickness", ContourThickness, "m", "a plausible build-up depth to show what a solid section looks like"));
            t.AssumedDimensions.Add(new AssumedDimension("Solid copy moved east", ContourShift, "m", "keeps the two copies apart"));
            t.AssumedDimensions.Add(new AssumedDimension("Profile spacing", ContourSpacing, "m", "a readable rib spacing for a 6 m patch"));

            t.SetUp = context =>
            {
                Surface patch = TutorialGeometry.Patch(context);
                context.AddInput(patch.ToBrep(), "Roof patch (surface)");
                Brep? solid = TutorialGeometry.Thicken(patch, ContourThickness, context.Doc.ModelAbsoluteTolerance);
                if (solid != null)
                {
                    solid.Transform(Transform.Translation(ContourShift, 0, 0));
                    context.AddInput(solid, $"Roof patch (solid, {ContourThickness:0.00} m thick)");
                }
            };
        }

        // ---------------------------------------------------------------- 4 Intersect

        public const double RibPlaneLength = 16.0;
        public const double RibPlaneHeight = 12.0;

        public static void Intersect(Tutorial t)
        {
            t.Goal = "Find the curve where an upright plane through the building centre cuts the roof. It's the line a radial roof rib follows. Then get the same curve a second way with Project.";
            t.InputGeometry = "On the Input layer: an orange copy of the whole roof, lifted 4 m, and a point marking the building centre on the ground at (20.00, 17.49, 0). One of the long main beams runs due east from here, so that's the direction you'll cut.";

            t.Steps.Add(new TutorialStep("Turn on the Point object snap and Ortho."));
            t.Steps.Add(new TutorialStep("Run Plane and choose the Vertical option. Click the Building centre point, type @16<0 and press Enter (16 m due east), then type 12 for the height and press Enter. The plane is longer and taller than the roof, so it cuts all the way through.", "Plane", "Top"));
            t.Steps.Add(new TutorialStep("Run Intersect, select the plane and the roof copy, and press Enter. You get one curve from the centre to the roof edge.", "Intersect", "Perspective"));
            t.Steps.Add(new TutorialStep("If you got several short pieces instead, select them and run Join. If part of the roof isn't cut, make the plane bigger and try again.", "Join"));
            t.Steps.Add(new TutorialStep("Now the second way. In the Top view, run Line from the Building centre point to @16<0.", "Line", "Top"));
            t.Steps.Add(new TutorialStep("Run Project, select the line, press Enter, select the roof copy, press Enter. Project works straight down through the Top view, so the line lands on the roof.", "Project", "Top"));
            t.Steps.Add(new TutorialStep("Compare the two curves with CurveDeviation: the maximum distance should be close to zero.", "CurveDeviation"));
            t.Steps.Add(new TutorialStep("Remember: this curve is the top of the roof. A beam centreline sits below it, by half the beam depth plus the roof build-up, so you would move or offset it down before modelling a beam."));

            t.KeyOptions.Add("Plane, Vertical option: draws an upright plane from a base line and a height.");
            t.KeyOptions.Add("Intersect has no settings: it cuts every selected object against every other, and puts the curves on the current layer.");
            t.KeyOptions.Add("Project, Loose option: keeps the curve's control points simple but is less exact. DeleteInput removes the original line.");
            t.KeyOptions.Add("Project direction: curves travel along the active view's construction plane. Use the Top view to project straight down.");

            t.CommonMistakes.Clear();
            t.CommonMistakes.Add("Making the plane too short or too low. It misses part of the roof and the curve stops early or comes out in pieces. Make it longer and taller than the roof.");
            t.CommonMistakes.Add("Running Project in the Front or Right view. The line travels sideways and misses or lands on the wrong part of the roof. Project from the Top view.");

            t.Check = "Your curve lies on the roof copy (zoom in close: it never floats above or dips below it), and from the Top view it's a straight line running due east from the building centre.";

            t.AssumedDimensions.Add(new AssumedDimension("Lift above the real roof", TutorialGeometry.Lift, "m", LiftWhy));
            t.AssumedDimensions.Add(new AssumedDimension("Cutting plane length", RibPlaneLength, "m", "the roof reaches about 14.6 m from the centre, so 16 m is enough"));
            t.AssumedDimensions.Add(new AssumedDimension("Cutting plane height", RibPlaneHeight, "m", "from the ground to above the lifted roof (top about 9.9 m)"));

            t.SetUp = context =>
            {
                context.AddInput(TutorialGeometry.Roof(context), "Roof copy");
                context.AddInput(new Point(TutorialGeometry.Centre(context)), "Building centre");
            };
        }

        // ---------------------------------------------------------------- shared

        /// <summary>The default level for the tutorial, with the lifted roof patch as a see-through target.</summary>
        private static Level WithPatchGhost(Tutorial t, TutorialContext context)
        {
            Level level = TutorialLevels.DefaultLevel(t);
            level.GhostGeometry.Add(TutorialGeometry.Patch(context).ToBrep());
            return level;
        }
    }
}
