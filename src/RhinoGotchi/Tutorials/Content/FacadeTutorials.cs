using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;
using RhinoGotchi.Levels;

namespace RhinoGotchi.Tutorials.Content
{
    /// <summary>
    /// Part D: facade and terrace. From the model: 34 curved glass panels on a ring about 10.2 m in
    /// radius (the panel facing east is at −5.6°, 3.0 m tall); the terrace ramp curves from X 20.0 to
    /// X 31.3 and rises only 0.127 m, from −0.232 m at its west end to −0.105 m at its east end.
    /// </summary>
    internal static class FacadeTutorials
    {
        // ---------------------------------------------------------------- 8 OffsetSrf

        public const double GlassThickness = 0.024;
        public const double GlassShift = 2.0;

        public static void OffsetSrf(Tutorial t)
        {
            t.Goal = "Turn a curved glass surface into a solid glass panel with OffsetSrf, choosing the thickness, the direction it grows in, and the Solid option.";
            t.InputGeometry = "On the Input layer: the outer face of the glass panel that faces east (3.0 m tall, curved around the 10.2 m facade ring), copied as a single surface and moved 2 m outward onto the terrace so it sits clear of the real facade. The magenta highlight shows all 34 glass panels.";

            t.Steps.Add(new TutorialStep("Check the units: open Document Properties > Units. This model is in metres, so 24 mm is 0.024.", "DocumentProperties"));
            t.Steps.Add(new TutorialStep("Select the glass surface and run Dir. The arrows show which way the surface faces. They should point away from the building. If not, click Flip and press Enter.", "Dir", "Perspective"));
            t.Steps.Add(new TutorialStep("Run OffsetSrf and select the glass surface. Press Enter.", "OffsetSrf"));
            t.Steps.Add(new TutorialStep($"Click Distance on the command line and type {GlassThickness}. Set Solid=Yes."));
            t.Steps.Add(new TutorialStep("Look at the arrows: they show which way the offset grows. Click FlipAll so they point toward the building. The outer face then stays where it is and the glass grows inward."));
            t.Steps.Add(new TutorialStep("Press Enter. You get a thin curved slab of glass."));
            t.Steps.Add(new TutorialStep("Select it: the status bar says closed solid polysurface. Run Distance and snap from one face to the other at an edge (End snap) to measure the thickness.", "Distance"));

            t.KeyOptions.Add("Distance: the thickness, in model units (metres here).");
            t.KeyOptions.Add("Solid: Yes closes the gap between the two faces and makes a solid. No gives just a second, separate surface.");
            t.KeyOptions.Add("FlipAll / clicking the surface: changes the direction the offset grows in.");
            t.KeyOptions.Add("DeleteInput: Yes removes the original surface. Keep it off while you're learning so you can compare.");

            t.CommonMistakes.Add("Typing 24 because you're thinking in millimetres. In this metre model that makes a 24 m thick slab. Check the units and type 0.024.");
            t.CommonMistakes.Add("Leaving Solid=No. You get two loose surfaces instead of a closed panel, and the check below fails. Run it again with Solid=Yes.");

            t.Check = $"The panel is a closed solid polysurface, and its thickness measures {GlassThickness} m (24 mm) everywhere you check: at the top, the middle and the bottom.";

            t.AssumedDimensions.Add(new AssumedDimension("Glass thickness", GlassThickness, "m", "an example value for a laminated pane, not the project's specification"));
            t.AssumedDimensions.Add(new AssumedDimension("Copy moved outward", GlassShift, "m", "keeps the tutorial surface clear of the real facade"));

            t.SetUp = context =>
            {
                Brep face = EastGlassFace(context);
                context.AddInput(face, "Glass surface (outer face)");
            };
        }

        /// <summary>The outer face of the east-facing glass panel, moved outward. Standalone: a made-up curved panel.</summary>
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
                    // The outer face: of the two big faces, the one farther from the centre.
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
                // Standalone: a 3 m tall piece of a 10.25 m radius cylinder, 16° wide, facing east.
                var profile = new LineCurve(new Point3d(10.25, 0, 0), new Point3d(10.25, 0, 3));
                var rev = RevSurface.Create(profile, new Line(Point3d.Origin, Vector3d.ZAxis), -8 * Math.PI / 180, 8 * Math.PI / 180);
                face = rev.ToBrep();
            }

            Point3d mid = AreaMassProperties.Compute(face)?.Centroid ?? face.GetBoundingBox(true).Center;
            var outward = new Vector3d(mid.X - centre.X, mid.Y - centre.Y, 0);
            if (outward.Unitize())
                face.Transform(Transform.Translation(outward * GlassShift));
            return face;
        }

        // ---------------------------------------------------------------- 9 Sweep2

        public const double RampLift = 1.0;

        public static void Sweep2(Tutorial t)
        {
            t.Goal = "Rebuild the curved terrace ramp's top surface with Sweep2: two rail curves that rise together along its edges, and two cross-sections at its landings.";
            t.InputGeometry = "On the Input layer: six orange points on the ramp's two long edges (start, middle and end of each), at the ramp's real heights and lifted 1 m above the terrace. The ramp curves from X 20.0 m in the west to X 31.3 m in the east and rises about 0.12 m. The points are levelled across the ramp's width: the real ramp's east end leans about 2 cm from one side to the other, which this simplified version leaves out. The blue see-through shape is the real ramp.";

            t.Steps.Add(new TutorialStep("Turn on the Point object snap."));
            t.Steps.Add(new TutorialStep("Run InterpCrv through Edge A's three points, start → middle → end (west to east). Press Enter. This is rail 1.", "InterpCrv", "Perspective"));
            t.Steps.Add(new TutorialStep("Do the same for Edge B, also from start to end. Both rails now run in the same direction and rise together.", "InterpCrv"));
            t.Steps.Add(new TutorialStep("Run Line from Edge A start to Edge B start. That's the cross-section at the low landing. Draw another Line from Edge A end to Edge B end for the high landing.", "Line"));
            t.Steps.Add(new TutorialStep("Run Sweep2. Select rail 1, then rail 2, then the two cross-section lines. Press Enter.", "Sweep2"));
            t.Steps.Add(new TutorialStep("In the dialog leave Maintain height off and click OK. You get the ramp's top surface."));
            t.Steps.Add(new TutorialStep("From the Front view, check that the surface starts at the low points and ends at the high points, and that it rises smoothly in between.", null, "Front"));

            t.KeyOptions.Add("Rails: pick them first, one after the other. Both should run the same way (here west to east) or the surface twists.");
            t.KeyOptions.Add("Cross-sections: each must touch both rails. Put them where the shape is known, here the two landings.");
            t.KeyOptions.Add("Maintain height: keeps the sections from scaling in height as the rails spread or narrow. Not needed for flat line sections.");
            t.KeyOptions.Add("Add slash: lets you add extra section positions along the rails if the surface needs more control.");

            t.CommonMistakes.Add("Drawing one rail east to west and the other west to east. The sweep twists into a saddle. Draw both from their start points.");
            t.CommonMistakes.Add("A cross-section that stops short of a rail, or is drawn from a point at the wrong height. Sweep2 can't fit it, or the ramp tilts sideways. Snap both ends to the marked points.");

            t.Check = "The surface's ends meet the landing points exactly, and at any cross-section the two edges are at the same height: the ramp only slopes along its length, never sideways. Its total rise is about 0.12 m.";

            t.AssumedDimensions.Add(new AssumedDimension("Lift above the terrace", RampLift, "m", "keeps the tutorial surface clear of the real ramp and below the roof"));

            t.SetUp = context =>
            {
                var (pa, pb) = RampPoints(context);
                string[] where = { "start", "middle", "end" };
                for (int i = 0; i < 3; i++)
                    context.AddInput(new Point(pa[i]), "Edge A " + where[i]);
                for (int i = 0; i < 3; i++)
                    context.AddInput(new Point(pb[i]), "Edge B " + where[i]);
            };
            t.CreateLevel = context =>
            {
                Level level = TutorialLevels.DefaultLevel(t);
                Brep? ramp = context.Reference?.Ramp.Select(AsBrep).FirstOrDefault();
                if (ramp != null)
                {
                    ramp.Transform(Transform.Translation(0, 0, RampLift));
                    level.GhostGeometry.Add(ramp);
                }
                return level;
            };
        }

        /// <summary>
        /// Start, middle and end points of each ramp edge, lifted, with each pair levelled across the
        /// width (the simplified ramp has no sideways tilt).
        /// </summary>
        public static (Point3d[] A, Point3d[] B) RampPoints(TutorialContext context)
        {
            var (a, b) = RampRails(context);
            Point3d[] pa = RailPoints(a), pb = RailPoints(b);
            for (int i = 0; i < 3; i++)
            {
                double z = (pa[i].Z + pb[i].Z) / 2;
                pa[i].Z = z;
                pb[i].Z = z;
            }
            return (pa, pb);
        }

        private static Point3d[] RailPoints(Curve rail)
        {
            rail.NormalizedLengthParameter(0.5, out double mid);
            return new[] { rail.PointAtStart, rail.PointAt(mid), rail.PointAtEnd };
        }

        /// <summary>
        /// The ramp top's two long edges, lifted, each running from the low (west) end to the high end.
        /// Standalone: two concentric arcs rising 0.127 m.
        /// </summary>
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
                // The top face's edges: the two longest are the rails, the two short ones the landings.
                rails = top.DuplicateFace(false).DuplicateEdgeCurves(true)
                    .OrderByDescending(c => c.GetLength()).Take(2).ToList();
                foreach (Curve c in rails)
                    c.Transform(Transform.Translation(0, 0, RampLift));
            }
            else
            {
                rails = new List<Curve>();
                foreach (double r in new[] { 8.0, 10.5 })
                {
                    var pts = Enumerable.Range(0, 7).Select(i =>
                    {
                        double a = i / 6.0 * Math.PI / 3;
                        return new Point3d(r * Math.Cos(a), r * Math.Sin(a), RampLift - 0.232 + 0.127 * i / 6.0);
                    });
                    rails.Add(Curve.CreateInterpolatedCurve(pts, 3));
                }
            }

            foreach (Curve c in rails)
                if (c.PointAtStart.Z > c.PointAtEnd.Z + 1e-6 || (Math.Abs(c.PointAtStart.Z - c.PointAtEnd.Z) <= 1e-6 && c.PointAtStart.X > c.PointAtEnd.X))
                    c.Reverse();
            return (rails[0], rails[1]);
        }

        // ---------------------------------------------------------------- shared

        internal static Brep? AsBrep(GeometryBase? g) => g switch
        {
            Brep b => b,
            Extrusion e => e.ToBrep(),
            Surface s => s.ToBrep(),
            _ => null,
        };

        internal static double Bearing(Point3d p, Point3d centre) => Math.Atan2(p.Y - centre.Y, p.X - centre.X) * 180 / Math.PI;

        internal static double PlanDistance(Point3d p, Point3d centre) =>
            Math.Sqrt((p.X - centre.X) * (p.X - centre.X) + (p.Y - centre.Y) * (p.Y - centre.Y));
    }
}
