using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoGotchi.Levels;

namespace RhinoGotchi.Tutorials.Evaluators
{
    /// <summary>1 Loft: a surface from at least 3 curves, within 10 cm of the roof patch, made with History on.</summary>
    public sealed class LoftEvaluator : TutorialEvaluator
    {
        public const double FitTolerance = 0.10;
        private readonly Surface _target;
        private readonly double _targetArea;

        public LoftEvaluator(Tutorial tutorial, Surface target) : base(tutorial)
        {
            _target = target;
            _targetArea = Measure.Area(target);
        }

        protected override Grade Check(LevelAttempt attempt, List<RhinoObject> work, double tolerance)
        {
            int curves = Measure.Curves(work).Count();
            var best = Measure.Breps(work)
                .Where(x => Measure.Area(x.Brep) >= 0.8 * _targetArea)
                .Select(x => (x.Obj, Deviation: Measure.MaxDeviation(x.Brep, _target)))
                .OrderBy(x => x.Deviation)
                .FirstOrDefault();

            if (best.Obj == null)
                return Grade.Nothing(curves < 3
                    ? $"No roof surface yet. Draw the section curves first ({curves} of 4 so far), then Loft them."
                    : "No roof surface yet that covers the patch. Loft your section curves.");

            double fit = best.Deviation <= FitTolerance ? 1 : Math.Max(0, 1 - (best.Deviation - FitTolerance) / 0.2);
            bool history = (attempt.HasHistory.TryGetValue(best.Obj.Id, out bool h) && h) || best.Obj.HasHistoryRecord();
            bool enoughCurves = curves >= 3;

            double score = 0.75 * fit + 0.15 * (history ? 1 : 0) + 0.10 * (enoughCurves ? 1 : 0);
            bool solved = fit >= 1 && history && enoughCurves;

            var notes = new List<string>
            {
                best.Deviation <= FitTolerance
                    ? $"Your loft lies within {Measure.Metres(best.Deviation)} of the roof."
                    : $"Your loft strays up to {Measure.Metres(best.Deviation)} from the roof (aim for {FitTolerance:0.00} m). Check the curve order and directions.",
            };
            if (!history)
                notes.Add("History wasn't on when you lofted: turn on Record History and loft again, so the surface follows its curves.");
            if (!enoughCurves)
                notes.Add("Loft from at least three section curves.");
            return new Grade(score, solved, string.Join(" ", notes));
        }
    }

    /// <summary>2 NetworkSrf: a network surface through its curves, then a rebuilt surface (≤ 10 × 10 points) within 8 cm of the roof.</summary>
    public sealed class NetworkSrfEvaluator : TutorialEvaluator
    {
        public const double CurveTolerance = 0.01;
        public const double FitTolerance = 0.08;
        public const int MaxPoints = 10;
        private readonly Surface _target;
        private readonly double _targetArea;
        private readonly Curve[] _boundary;

        public NetworkSrfEvaluator(Tutorial tutorial, Surface target, IEnumerable<Curve> boundary) : base(tutorial)
        {
            _target = target;
            _targetArea = Measure.Area(target);
            _boundary = boundary.ToArray();
        }

        protected override Grade Check(LevelAttempt attempt, List<RhinoObject> work, double tolerance)
        {
            var learnerCurves = Measure.Curves(work).Select(x => x.Curve).ToList();
            var surfaces = Measure.Breps(work).Where(x => x.Brep.Faces.Count == 1 && Measure.Area(x.Brep) >= 0.8 * _targetArea).ToList();
            if (surfaces.Count == 0)
                return Grade.Nothing(learnerCurves.Count < 4
                    ? $"No surface yet. Draw the network curves first ({learnerCurves.Count} of 4 so far), then run NetworkSrf."
                    : "No surface yet. Run NetworkSrf on the boundary curves and your four curves.");

            var allCurves = _boundary.Concat(learnerCurves).ToList();
            bool throughCurves = surfaces.Any(s => allCurves.All(c => Measure.Samples(c, 8).All(p => Measure.DistanceTo(s.Brep, p) <= CurveTolerance)));

            var rebuilt = surfaces
                .Select(s => (s.Brep, Points: s.Brep.Faces[0].UnderlyingSurface().ToNurbsSurface()))
                .Where(s => s.Points != null && s.Points.Points.CountU <= MaxPoints && s.Points.Points.CountV <= MaxPoints)
                .Select(s => (s.Brep, s.Points, Deviation: Measure.MaxDeviation(s.Brep, _target)))
                .OrderBy(s => s.Deviation)
                .FirstOrDefault();
            bool rebuiltOk = rebuilt.Brep != null && rebuilt.Deviation <= FitTolerance;

            double score = 0.5 * (throughCurves ? 1 : 0) + 0.5 * (rebuiltOk ? 1 : rebuilt.Brep != null ? 0.5 : 0);
            var notes = new List<string>();
            notes.Add(throughCurves
                ? "Your network surface passes through every curve."
                : "No surface passes through all the curves yet. Check that every curve touches the curves it crosses, and include the four boundary curves.");
            if (rebuilt.Brep == null)
                notes.Add($"Now Rebuild it to at most {MaxPoints} × {MaxPoints} control points (keep the original).");
            else if (!rebuiltOk)
                notes.Add($"The rebuilt surface ({rebuilt.Points!.Points.CountU} × {rebuilt.Points.Points.CountV} points) strays {Measure.Metres(rebuilt.Deviation)} from the roof; use a few more points.");
            else
                notes.Add($"The rebuilt surface has {rebuilt.Points!.Points.CountU} × {rebuilt.Points.Points.CountV} control points and stays within {Measure.Metres(rebuilt.Deviation)} of the roof.");
            return new Grade(score, throughCurves && rebuiltOk, string.Join(" ", notes));
        }
    }

    /// <summary>3 Contour: at least 5 parallel profiles, evenly spaced to within 1%, from the surface or the solid copy.</summary>
    public sealed class ContourEvaluator : TutorialEvaluator
    {
        public const int MinProfiles = 5;

        public ContourEvaluator(Tutorial tutorial) : base(tutorial) { }

        protected override Grade Check(LevelAttempt attempt, List<RhinoObject> work, double tolerance)
        {
            var curves = Measure.Curves(work).Select(x => x.Curve).ToList();
            var open = Assess(curves.Where(c => !c.IsClosed).ToList(), tolerance);
            var closed = Assess(curves.Where(c => c.IsClosed).ToList(), tolerance);

            if (open.Count == 0 && closed.Count == 0)
                return Grade.Nothing("No profiles yet. Run Contour on the surface copy.");

            bool openOk = open.Count >= MinProfiles && open.Even;
            bool closedOk = closed.Count >= MinProfiles && closed.Even;
            var notes = new List<string>();
            if (openOk)
                notes.Add($"{open.Count} open profiles from the surface copy, parallel and {Measure.Metres(open.Spacing)} apart.");
            else if (open.Count > 0)
                notes.Add($"{open.Count} open profiles from the surface copy, {(open.Even ? "evenly spaced" : "but not evenly spaced or not parallel")}; you need at least {MinProfiles}.");
            if (closedOk)
                notes.Add($"{closed.Count} closed outlines from the solid copy, {Measure.Metres(closed.Spacing)} apart.");
            else if (closed.Count > 0)
                notes.Add($"{closed.Count} closed outlines from the solid copy, {(closed.Even ? "evenly spaced" : "but not evenly spaced or not parallel")}; you need at least {MinProfiles}.");
            if (openOk && !closedOk)
                notes.Add("Contour the solid copy too, to compare.");
            if (closedOk && !openOk)
                notes.Add("Contour the surface copy too, to compare.");

            bool solved = openOk || closedOk;
            double score = solved ? (openOk && closedOk ? 1 : 0.85) : 0.4 * Math.Min(1, Math.Max(open.Count, closed.Count) / (double)MinProfiles);
            return new Grade(score, solved, string.Join(" ", notes));
        }

        // The largest group of parallel planar curves, and whether their planes are evenly spaced.
        private static (int Count, bool Even, double Spacing) Assess(List<Curve> curves, double tolerance)
        {
            var planes = new List<Plane>();
            foreach (Curve c in curves)
                if (c.TryGetPlane(out Plane p, Math.Max(tolerance * 10, 1e-3)))
                    planes.Add(p);
            if (planes.Count == 0)
                return (0, false, 0);

            // Group by direction: the most common normal (within 1°), sign ignored.
            Vector3d best = Vector3d.Unset;
            List<Plane> group = new List<Plane>();
            foreach (Plane seed in planes)
            {
                var members = planes.Where(p => Vector3d.VectorAngle(p.Normal, seed.Normal) < Rhino.RhinoMath.ToRadians(1) ||
                                                Vector3d.VectorAngle(p.Normal, -seed.Normal) < Rhino.RhinoMath.ToRadians(1)).ToList();
                if (members.Count > group.Count)
                {
                    group = members;
                    best = seed.Normal;
                }
            }

            var offsets = group.Select(p => (Vector3d)p.Origin * best).OrderBy(x => x).ToList();
            // Curves from one plane (pieces of one cut) share an offset: keep one per plane.
            var distinct = new List<double>();
            foreach (double o in offsets)
                if (distinct.Count == 0 || o - distinct[distinct.Count - 1] > 1e-3)
                    distinct.Add(o);
            if (distinct.Count < 2)
                return (distinct.Count, false, 0);

            var gaps = distinct.Zip(distinct.Skip(1), (a, b) => b - a).ToList();
            double median = gaps.OrderBy(g => g).ElementAt(gaps.Count / 2);
            bool even = median > 0 && gaps.All(g => Math.Abs(g - median) <= 0.01 * median ||
                                                    // a second run further along: a whole number of steps away
                                                    Math.Abs(g / median - Math.Round(g / median)) <= 0.01);
            return (distinct.Count, even, median);
        }
    }

    /// <summary>4 Intersect: a curve lying on the roof copy, in a vertical plane through the building centre.</summary>
    public sealed class IntersectEvaluator : TutorialEvaluator
    {
        public const double OnRoofTolerance = 0.01;
        public const double CentreTolerance = 0.05;
        private readonly Brep _roof;
        private readonly Point3d _centre;

        public IntersectEvaluator(Tutorial tutorial, Brep roof, Point3d centre) : base(tutorial)
        {
            _roof = roof;
            _centre = centre;
        }

        protected override Grade Check(LevelAttempt attempt, List<RhinoObject> work, double tolerance)
        {
            var curves = Measure.Curves(work).Select(x => x.Curve).Where(c => c.GetLength() > 1.0).ToList();
            if (curves.Count == 0)
                return Grade.Nothing("No profile curve yet. Make the vertical plane, then Intersect it with the roof copy.");

            int onRoof = 0, good = 0;
            foreach (Curve c in curves)
            {
                var samples = Measure.Samples(c, 20);
                if (!samples.All(p => Measure.DistanceTo(_roof, p) <= OnRoofTolerance))
                    continue;
                onRoof++;
                if (Plane.FitPlaneToPoints(samples, out Plane plane) != PlaneFitResult.Success)
                    continue;
                bool vertical = Math.Abs(plane.Normal.Z) < 0.02;
                bool throughCentre = Math.Abs(plane.DistanceTo(_centre)) <= CentreTolerance;
                if (vertical && throughCentre)
                    good++;
            }

            if (good > 0)
                return new Grade(1, true, good > 1
                    ? "Both curves lie on the roof in a vertical plane through the building centre. Remember: this is the roof's top, not the beam's centreline."
                    : "Your curve lies on the roof in a vertical plane through the building centre. Try the Project method too, and compare.");
            if (onRoof > 0)
                return new Grade(0.5, false, "Your curve lies on the roof, but not in a vertical plane through the building centre. Start the plane at the Building centre point.");
            return new Grade(0.2, false, "Your curve doesn't lie on the roof copy. Intersect the plane with the orange roof copy (not the original).");
        }
    }
}
