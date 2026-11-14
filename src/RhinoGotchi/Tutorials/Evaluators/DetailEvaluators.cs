using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoGotchi.Levels;

namespace RhinoGotchi.Tutorials.Evaluators
{
    /// <summary>10 BooleanDifference: one closed plate with the right number of through-holes (checked by volume).</summary>
    public sealed class BooleanEvaluator : TutorialEvaluator
    {
        private readonly BoundingBox _plateBox;
        private readonly double _plateVolume;
        private readonly double _holeVolume;
        private readonly int _holes;

        public BooleanEvaluator(Tutorial tutorial, BoundingBox plateBox, double holeVolume, int holes) : base(tutorial)
        {
            _plateBox = plateBox;
            _plateVolume = plateBox.Diagonal.X * plateBox.Diagonal.Y * plateBox.Diagonal.Z;
            _holeVolume = holeVolume;
            _holes = holes;
        }

        protected override Grade Check(LevelAttempt attempt, List<RhinoObject> work, double tolerance)
        {
            BoundingBox near = _plateBox;
            near.Inflate(0.01);
            var plates = Measure.Breps(work).Select(x => x.Brep)
                .Where(b => b.IsSolid && near.Contains(b.GetBoundingBox(true)) && b.GetBoundingBox(true).Diagonal.Y > 0.5 * _plateBox.Diagonal.Y)
                .ToList();
            if (plates.Count == 0)
                return Grade.Nothing("No plate yet. Make the box from the Plate corner point.");

            Grade? best = null;
            foreach (Brep plate in plates)
            {
                double removed = _plateVolume - Measure.Volume(plate);
                double holes = removed / _holeVolume;
                int whole = (int)Math.Round(holes);
                bool clean = Math.Abs(holes - whole) < 0.1;
                Grade grade;
                if (whole == _holes && clean)
                    grade = new Grade(1, true, $"A closed plate with {_holes} holes right through it.");
                else if (holes < 0.1)
                    grade = new Grade(0.3, false, "The plate has no holes yet. Make the cutters longer than the plate is thick, then BooleanDifference: plate first, then the cutters.");
                else if (!clean)
                    grade = new Grade(0.6, false, $"About {holes:0.#} holes' worth is cut away: one cut didn't go all the way through (a pocket). Undo the short cutter's cut.");
                else
                    grade = new Grade(0.4 + 0.5 * Math.Min(whole, _holes) / _holes, false, $"{whole} of {_holes} holes cut.");
                if (best == null || grade.Score > best.Score)
                    best = grade;
            }
            return best ?? Grade.Nothing("Nothing to check yet.");
        }
    }

    /// <summary>11 Pipe: a tube of the right radius along the expected centreline, not overlapping itself (checked by volume).</summary>
    public sealed class PipeEvaluator : TutorialEvaluator
    {
        public const double RadiusTolerance = 0.002;
        private readonly Curve _centreline;
        private readonly double _radius;

        public PipeEvaluator(Tutorial tutorial, Curve centreline, double radius) : base(tutorial)
        {
            _centreline = centreline;
            _radius = radius;
        }

        protected override Grade Check(LevelAttempt attempt, List<RhinoObject> work, double tolerance)
        {
            // Skip the very ends, where caps and the learner's exact end points differ.
            var probes = Measure.Samples(_centreline, 20).Skip(1).Take(19).ToList();
            var tubes = Measure.Breps(work).Select(x => x.Brep)
                .Where(b => probes.Count(p => Measure.DistanceTo(b, p) <= 0.2 || (b.IsSolid && b.IsPointInside(p, tolerance, false))) >= probes.Count * 0.8)
                .ToList();
            if (tubes.Count == 0)
                return Grade.Nothing("No handrail tube yet. Build the centreline, then run Pipe on it.");

            double length = _centreline.GetLength();
            double straight = Math.PI * _radius * _radius * length;
            double rounded = straight + 4.0 / 3.0 * Math.PI * Math.Pow(_radius, 3);

            Grade? best = null;
            foreach (Brep tube in tubes)
            {
                var distances = probes.Select(p => Measure.DistanceTo(tube, p)).ToList();
                double fraction = distances.Count(d => Math.Abs(d - _radius) <= RadiusTolerance) / (double)distances.Count;
                double volume = Measure.Volume(tube);
                bool volumeOk = tube.IsSolid && (Math.Abs(volume - straight) <= 0.05 * straight || Math.Abs(volume - rounded) <= 0.05 * rounded);

                var notes = new List<string>();
                if (fraction >= 0.9)
                    notes.Add($"The tube is Ø {2 * _radius:0.000} m along the centreline.");
                else
                {
                    double typical = distances.OrderBy(d => d).ElementAt(distances.Count / 2);
                    notes.Add(Math.Abs(typical - 2 * _radius) <= RadiusTolerance
                        ? $"The tube is twice too thick: you typed the diameter as the radius. Use {_radius}."
                        : $"The tube's radius is about {typical:0.000} m, or it doesn't follow the centreline. It should be {_radius} m.");
                }
                if (!tube.IsSolid)
                    notes.Add("The tube isn't closed: set Cap to Round or Flat.");
                else if (!volumeOk && fraction >= 0.9)
                    notes.Add("The tube overlaps itself or has extra pieces: check the centreline is one joined, smooth curve.");
                double score = 0.6 * fraction + 0.4 * (volumeOk ? 1 : 0);
                var grade = new Grade(score, fraction >= 0.9 && volumeOk, string.Join(" ", notes));
                if (best == null || grade.Score > best.Score)
                    best = grade;
            }
            return best ?? Grade.Nothing("Nothing to check yet.");
        }
    }

    /// <summary>12 ArrayCrv: at least 5 vertical bars of the right height hanging from the centreline, evenly spaced along it.</summary>
    public sealed class ArrayCrvEvaluator : TutorialEvaluator
    {
        public const double MaxTiltDegrees = 1.0;
        public const int MinBars = 5;
        private readonly Curve _centreline;
        private readonly double _height;
        private readonly double _spacing;
        private readonly string _originalName;

        public ArrayCrvEvaluator(Tutorial tutorial, Curve centreline, double height, double spacing, string originalName) : base(tutorial)
        {
            _centreline = centreline;
            _height = height;
            _spacing = spacing;
            _originalName = originalName;
        }

        protected override Grade Check(LevelAttempt attempt, List<RhinoObject> work, double tolerance)
        {
            var lines = Measure.Curves(work).Select(x => x.Curve)
                .Concat(StartObjects(attempt, _originalName).OfType<Curve>())
                .Where(c => c.IsLinear(1e-4) && Math.Abs(c.GetLength() - _height) <= 0.01)
                .ToList();

            var bars = new List<(double Along, double Tilt)>();
            foreach (Curve c in lines)
            {
                Point3d top = c.PointAtStart.Z > c.PointAtEnd.Z ? c.PointAtStart : c.PointAtEnd;
                Point3d bottom = c.PointAtStart.Z > c.PointAtEnd.Z ? c.PointAtEnd : c.PointAtStart;
                if (!_centreline.ClosestPoint(top, out double t) || _centreline.PointAt(t).DistanceTo(top) > 0.01)
                    continue;
                double along = _centreline.GetLength(new Interval(_centreline.Domain.Min, t));
                if (bars.Any(b => Math.Abs(b.Along - along) < 1e-3))
                    continue; // a copy on top of another (e.g. the original)
                double tilt = Vector3d.VectorAngle(top - bottom, Vector3d.ZAxis) * 180 / Math.PI;
                bars.Add((along, tilt));
            }

            int tiltedOff = 0;
            if (bars.Count < 2)
            {
                // Freeform copies lean, so their tops may still sit on the curve but they fail the length or tilt test above.
                tiltedOff = Measure.Curves(work).Count(x => x.Curve.IsLinear(1e-4) && Math.Abs(x.Curve.GetLength() - _height) <= 0.01);
                return Grade.Nothing(tiltedOff > 1
                    ? "Your bars don't hang from the centreline. Use the bar's top as the base point."
                    : "No arrayed bars yet. Select the bar and run ArrayCrv along the centreline.");
            }

            bars.Sort((a, b) => a.Along.CompareTo(b.Along));
            int vertical = bars.Count(b => b.Tilt <= MaxTiltDegrees);
            var gaps = bars.Zip(bars.Skip(1), (a, b) => b.Along - a.Along).ToList();
            bool even = gaps.All(g => Math.Abs(g - _spacing) <= 0.02 * _spacing);

            double score = 0.5 * vertical / bars.Count + 0.5 * (even ? 1 : 0) * Math.Min(1, bars.Count / (double)MinBars);
            bool solved = bars.Count >= MinBars && vertical == bars.Count && even;
            var notes = new List<string> { $"{bars.Count} bars along the rail." };
            notes.Add(vertical == bars.Count ? "All of them are vertical." : $"{bars.Count - vertical} lean off vertical: set Orientation to No rotation.");
            notes.Add(even ? $"They're evenly spaced, {_spacing} m apart." : $"The gaps aren't all {_spacing} m: use Distance between items = {_spacing}.");
            return new Grade(score, solved, string.Join(" ", notes));
        }
    }
}
