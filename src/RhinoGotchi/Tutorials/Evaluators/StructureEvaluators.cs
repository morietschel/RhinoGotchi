using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using RhinoGotchi.Levels;

namespace RhinoGotchi.Tutorials.Evaluators
{
    /// <summary>5 ArrayPolar: N upright planes through the centre's vertical axis, 360/N° apart, none doubled.</summary>
    public sealed class ArrayPolarEvaluator : TutorialEvaluator
    {
        public const double AngleTolerance = 0.5;
        private readonly Point3d _centre;
        private readonly int _count;
        private readonly string _originalName;

        public ArrayPolarEvaluator(Tutorial tutorial, Point3d centre, int count, string originalName) : base(tutorial)
        {
            _centre = centre;
            _count = count;
            _originalName = originalName;
        }

        protected override Grade Check(LevelAttempt attempt, List<RhinoObject> work, double tolerance)
        {
            var candidates = Measure.Breps(work).Select(x => x.Brep)
                .Concat(StartObjects(attempt, _originalName).Select(Measure.AsBrep).Where(b => b != null).Cast<Brep>())
                .ToList();

            var bearings = new List<double>();
            int offAxis = 0;
            foreach (Brep b in candidates)
            {
                if (b.Faces.Count != 1 || !b.Faces[0].TryGetPlane(out Plane plane, 1e-4))
                    continue;
                if (Math.Abs(plane.Normal.Z) > 0.01)
                    continue; // not upright
                if (Math.Abs(plane.DistanceTo(_centre)) > 0.01)
                {
                    offAxis++;
                    continue;
                }
                Point3d c = AreaMassProperties.Compute(b)?.Centroid ?? b.GetBoundingBox(true).Center;
                bearings.Add((Math.Atan2(c.Y - _centre.Y, c.X - _centre.X) * 180 / Math.PI + 360) % 360);
            }

            bearings.Sort();
            var unique = new List<double>();
            int doubled = 0;
            foreach (double b in bearings)
            {
                if (unique.Count > 0 && Math.Abs(b - unique[unique.Count - 1]) < AngleTolerance)
                    doubled++;
                else
                    unique.Add(b);
            }
            if (unique.Count > 1 && 360 - unique[unique.Count - 1] + unique[0] < AngleTolerance)
            {
                unique.RemoveAt(unique.Count - 1);
                doubled++;
            }

            if (unique.Count <= 1)
                return Grade.Nothing(offAxis > 0
                    ? "Your copies don't turn around the building centre. Use the Building centre point as the array centre."
                    : "No arrayed planes yet. Select the reference plane and run ArrayPolar.");

            double step = 360.0 / _count;
            var gaps = unique.Zip(unique.Skip(1), (a, b) => b - a).ToList();
            gaps.Add(360 - unique[unique.Count - 1] + unique[0]);
            bool even = gaps.All(g => Math.Abs(g - step) <= AngleTolerance);

            double score = Math.Min(1, unique.Count / (double)_count) * (even ? 1 : 0.6) * (doubled > 0 ? 0.8 : 1);
            bool solved = unique.Count == _count && even && doubled == 0 && offAxis == 0;
            var notes = new List<string> { $"{unique.Count} of {_count} planes around the centre{(even ? $", {step:0.#}° apart" : ", but the gaps aren't even")}." };
            if (doubled > 0)
                notes.Add($"{doubled} plane(s) sit on top of another: the count includes the original.");
            if (unique.Count > _count)
                notes.Add($"That's too many: type {_count} for the number of items (the count includes the original).");
            else if (unique.Count < _count && unique.Count > 1)
                notes.Add($"That's too few: type {_count} for the number of items.");
            if (offAxis > 0)
                notes.Add($"{offAxis} plane(s) don't pass through the building centre.");
            return new Grade(score, solved, string.Join(" ", notes));
        }
    }

    /// <summary>6 Sweep1: a closed solid along the roof-edge rail, section about W × D, staying upright (no roll).</summary>
    public sealed class Sweep1Evaluator : TutorialEvaluator
    {
        public const double RollTolerance = 5.0;
        private readonly Curve _rail;
        private readonly double _width, _depth, _sectionArea;

        public Sweep1Evaluator(Tutorial tutorial, Curve rail, double width, double depth) : base(tutorial)
        {
            _rail = rail;
            _width = width;
            _depth = depth;
            _sectionArea = width * depth;
        }

        protected override Grade Check(LevelAttempt attempt, List<RhinoObject> work, double tolerance)
        {
            var railPoints = Measure.Samples(_rail, 15);
            var candidates = Measure.Breps(work)
                .Select(x => x.Brep)
                .Where(b => railPoints.Count(p => Measure.DistanceTo(b, p) <= 0.01) >= railPoints.Count * 0.8)
                .ToList();
            if (candidates.Count == 0)
                return Grade.Nothing("No beam along the east quarter of the roof edge yet. Split the border at the two points, then Sweep1 the section along it.");

            Grade? best = null;
            foreach (Brep beam in candidates)
            {
                bool solid = beam.IsSolid;
                var areas = new List<double>();
                var rolls = new List<double>();
                foreach (double f in new[] { 0.1, 0.3, 0.5, 0.7, 0.9 })
                {
                    _rail.NormalizedLengthParameter(f, out double t);
                    if (!_rail.PerpendicularFrameAt(t, out Plane frame))
                        continue;
                    if (!Intersection.BrepPlane(beam, frame, tolerance, out Curve[] cuts, out _) || cuts.Length == 0)
                        continue;
                    Curve? section = Curve.JoinCurves(cuts, tolerance * 10).OrderByDescending(c => c.GetLength()).FirstOrDefault();
                    if (section == null || !section.IsClosed)
                        continue;
                    var props = AreaMassProperties.Compute(section);
                    if (props == null)
                        continue;
                    areas.Add(props.Area);
                    // Roll: the angle from "up" (world Z within the section plane) to the section's centroid.
                    Vector3d up = Vector3d.ZAxis - (Vector3d.ZAxis * frame.Normal) * frame.Normal;
                    Vector3d toCentroid = props.Centroid - frame.Origin;
                    toCentroid -= (toCentroid * frame.Normal) * frame.Normal;
                    if (up.Unitize() && toCentroid.Unitize())
                        rolls.Add(Vector3d.VectorAngle(up, toCentroid, new Plane(frame.Origin, frame.Normal)) * 180 / Math.PI);
                }

                bool areaOk = areas.Count > 0 && areas.All(a => Math.Abs(a - _sectionArea) <= 0.1 * _sectionArea);
                double roll = rolls.Count > 1 ? Spread(rolls) : 0;
                bool upright = rolls.Count > 1 && roll <= RollTolerance;
                double score = 0.3 * (solid ? 1 : 0) + 0.2 + 0.3 * (areaOk ? 1 : 0) + 0.2 * (upright ? 1 : 0);

                var notes = new List<string>();
                notes.Add(solid ? "Your beam is a closed solid along the roof edge." : "Your beam follows the roof edge but isn't closed: run Cap on it.");
                if (!areaOk && areas.Count > 0)
                    notes.Add($"Its section is about {areas.Average():0.0000} m², not {_sectionArea:0.0000} m² ({_width:0.00} × {_depth:0.00} m). Check the rectangle's size.");
                notes.Add(upright ? "The section stays upright all the way." : $"The section rolls by about {roll:0}° along the way: sweep again with Frame style Roadlike Top.");
                var grade = new Grade(score, solid && areaOk && upright, string.Join(" ", notes));
                if (best == null || grade.Score > best.Score)
                    best = grade;
            }
            return best ?? Grade.Nothing("Nothing to check yet.");
        }

        // The spread of angles in degrees, allowing for wrap-around at ±180°.
        private static double Spread(List<double> angles)
        {
            double reference = angles[0];
            var rel = angles.Select(a => Math.IEEERemainder(a - reference, 360)).ToList();
            return rel.Max() - rel.Min();
        }
    }

    /// <summary>7 FlowAlongSrf: at least 5 joists sitting on the roof patch (undersides within 3 cm), none overlapping.</summary>
    public sealed class FlowEvaluator : TutorialEvaluator
    {
        public const double SitTolerance = 0.03;
        public const int MinJoists = 5;
        private readonly Surface _target;
        private readonly int _expected;

        public FlowEvaluator(Tutorial tutorial, Surface target, int expected) : base(tutorial)
        {
            _target = target;
            _expected = expected;
        }

        protected override Grade Check(LevelAttempt attempt, List<RhinoObject> work, double tolerance)
        {
            var onRoof = new List<Brep>();
            foreach (var (_, b) in Measure.Breps(work))
            {
                if (!b.IsSolid)
                    continue;
                var distances = b.Vertices.Select(v => Measure.DistanceTo(_target, v.Location)).OrderBy(d => d).ToList();
                if (distances.Count == 0)
                    continue;
                // The underside: the nearer half of the corners.
                if (distances.Take(Math.Max(1, distances.Count / 2)).All(d => d <= SitTolerance))
                    onRoof.Add(b);
            }

            if (onRoof.Count == 0)
                return Grade.Nothing("No joists on the roof yet. Model the joists on the flat base, then FlowAlongSrf them onto the roof patch.");

            int overlaps = 0;
            for (int i = 0; i < onRoof.Count; i++)
            {
                BoundingBox a = onRoof[i].GetBoundingBox(true);
                for (int j = i + 1; j < onRoof.Count; j++)
                {
                    BoundingBox b = onRoof[j].GetBoundingBox(true);
                    if (BoundingBox.Intersection(a, b).IsValid &&
                        Intersection.BrepBrep(onRoof[i], onRoof[j], tolerance, out Curve[] curves, out _) && curves.Length > 0)
                        overlaps++;
                }
            }

            double score = Math.Min(1, onRoof.Count / (double)_expected) * (overlaps > 0 ? 0.7 : 1);
            bool solved = onRoof.Count >= MinJoists && overlaps == 0;
            var notes = new List<string> { $"{onRoof.Count} of {_expected} joists sit on the roof." };
            if (overlaps > 0)
                notes.Add($"{overlaps} pair(s) of joists overlap. Space them {Content.StructureTutorials.JoistSpacing} m apart before flowing.");
            if (onRoof.Count < MinJoists)
                notes.Add($"Flow at least {MinJoists}.");
            return new Grade(score, solved, string.Join(" ", notes));
        }
    }
}
