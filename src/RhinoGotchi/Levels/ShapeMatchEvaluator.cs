using System;
using Rhino.Geometry;

namespace RhinoGotchi.Levels
{
    /// <summary>
    /// Solved when the player has added a closed solid that matches the target shape: its bounding
    /// box corners within 1% of the target's diagonal and its volume within 1%. It doesn't matter
    /// which commands made it, so any route counts. Feedback explains how close the nearest try is.
    /// </summary>
    public sealed class ShapeMatchEvaluator : ILevelEvaluator
    {
        private const double Fit = 0.01;
        private readonly Brep _target;
        private readonly BoundingBox _targetBox;
        private double _targetVolume = -1;

        public ShapeMatchEvaluator(Brep target)
        {
            _target = target;
            _targetBox = target.GetBoundingBox(true);
        }

        /// <summary>Objects to ignore, e.g. anything a blocked command made.</summary>
        public Func<Guid, bool>? Exclude { get; set; }

        /// <summary>The document object that solved the level, once solved.</summary>
        public Guid MatchedId { get; private set; }

        public LevelResult Evaluate(LevelAttempt attempt)
        {
            double tol = Math.Max(attempt.Doc.ModelAbsoluteTolerance, 1e-6);
            if (_targetVolume < 0)
                _targetVolume = Math.Abs(_target.GetVolume(tol, tol));
            double diagonal = _targetBox.Diagonal.Length;

            string? closest = null;
            double closestError = double.MaxValue;
            bool sawOpen = false;

            foreach (Guid id in attempt.AddedObjectIds)
            {
                if (Exclude?.Invoke(id) == true)
                    continue;
                Brep? brep = attempt.Doc.Objects.FindId(id)?.Geometry switch
                {
                    Brep b => b,
                    Extrusion e => e.ToBrep(),
                    _ => null,
                };
                if (brep == null)
                    continue;

                BoundingBox box = brep.GetBoundingBox(true);
                double boxError = Math.Max(box.Min.DistanceTo(_targetBox.Min), box.Max.DistanceTo(_targetBox.Max));
                if (!brep.IsSolid)
                {
                    if (boxError <= 0.05 * diagonal)
                        sawOpen = true;
                    continue;
                }

                // Absolute value: an inside-out solid (faces pointing in) still has the right shape.
                double volume = Math.Abs(brep.GetVolume(tol, tol));
                double volumeError = _targetVolume > 0 ? Math.Abs(volume - _targetVolume) / _targetVolume : 1;
                if (boxError <= Fit * diagonal && volumeError <= Fit)
                {
                    MatchedId = id;
                    var d = box.Diagonal;
                    return new LevelResult
                    {
                        Solved = true,
                        Score = 1,
                        Feedback = $"Verified in your model: a closed solid, {d.X:0.##} × {d.Y:0.##} × {d.Z:0.##}, volume {volume:0.#} (target {_targetVolume:0.#}).",
                    };
                }

                double error = boxError / diagonal + volumeError;
                if (error < closestError)
                {
                    closestError = error;
                    closest = Describe(box, volume);
                }
            }

            string feedback = closest != null
                ? $"Close! {closest}"
                : sawOpen
                    ? "Found a shape in the right place, but it's open. Use Solid=Yes, or Cap the open ends."
                    : "No matching solid yet. Snap to the points and build the ghost shape.";
            return new LevelResult { Solved = false, Score = 0, Feedback = feedback };
        }

        private string Describe(BoundingBox box, double volume)
        {
            var d = box.Diagonal;
            var t = _targetBox.Diagonal;
            if (Math.Abs(d.Z - t.Z) > Fit * t.Length)
                return $"Height is {d.Z:0.##}, the target is {t.Z:0.##}.";
            if (Math.Abs(d.X - t.X) > Fit * t.Length || Math.Abs(d.Y - t.Y) > Fit * t.Length)
                return $"Footprint is {d.X:0.##} × {d.Y:0.##}, the target is {t.X:0.##} × {t.Y:0.##}.";
            if (box.Min.DistanceTo(_targetBox.Min) > Fit * t.Length)
            {
                // Same footprint, mirrored below the base: the extrusion went the wrong way.
                bool sameFootprint = box.Min.X.Equals(_targetBox.Min.X) || System.Math.Abs(box.Min.X - _targetBox.Min.X) <= Fit * t.Length;
                if (sameFootprint && box.Max.Z <= _targetBox.Min.Z + Fit * t.Length && box.Min.Z < _targetBox.Min.Z)
                    return "It went down instead of up. Extrude the other way (drag upward, or type a negative distance).";
                return "Right size, wrong place. Snap to the points.";
            }
            return $"Volume is {volume:0.#}, the target is {_targetVolume:0.#}. Check the shape matches the ghost.";
        }
    }
}
