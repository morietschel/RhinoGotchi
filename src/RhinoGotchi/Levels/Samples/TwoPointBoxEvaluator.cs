using System;
using System.Linq;
using Rhino.Geometry;

namespace RhinoGotchi.Levels.Samples
{
    /// <summary>
    /// Grades the two-point box level: the player must add a solid box whose
    /// opposite corners are the two given points.
    /// Score 1 when the corners match within the document tolerance, falling
    /// linearly to 0 when they are 10% of the box diagonal away. Not using the
    /// Box command halves the score.
    /// </summary>
    public sealed class TwoPointBoxEvaluator : ILevelEvaluator
    {
        private readonly BoundingBox _target;

        public TwoPointBoxEvaluator(Point3d cornerA, Point3d cornerB)
        {
            _target = new BoundingBox(cornerA, cornerB);
        }

        public LevelResult Evaluate(LevelAttempt attempt)
        {
            double tolerance = Math.Max(attempt.Doc.ModelAbsoluteTolerance, 1e-9);
            double diagonal = _target.Diagonal.Length;

            double bestError = double.MaxValue;
            foreach (Guid id in attempt.AddedObjectIds)
            {
                Brep? brep = AsBrep(attempt.Doc.Objects.FindId(id)?.Geometry);
                if (brep == null || !IsSolidBox(brep, tolerance))
                    continue;

                BoundingBox box = brep.GetBoundingBox(true);
                double error = Math.Max(box.Min.DistanceTo(_target.Min), box.Max.DistanceTo(_target.Max));
                bestError = Math.Min(bestError, error);
            }

            if (bestError == double.MaxValue)
                return new LevelResult { Solved = false, Score = 0, Feedback = "No box found yet. Try Box > Diagonal." };

            double score = bestError <= tolerance ? 1.0 : Math.Max(0.0, 1.0 - bestError / (0.1 * diagonal));
            bool usedCommand = attempt.CommandsRun.Any(c => string.Equals(c, attempt.Level.Command, StringComparison.OrdinalIgnoreCase));
            if (!usedCommand)
                score *= 0.5;

            bool solved = score > 0 && bestError <= 0.01 * diagonal;
            string feedback = bestError <= tolerance
                ? "Perfect fit!"
                : $"Corners are {bestError:0.###} off. Snap to the points.";
            if (!usedCommand)
                feedback += $" Use the {attempt.Level.Command} command for full points.";

            return new LevelResult { Solved = solved, Score = score, Feedback = feedback };
        }

        private static Brep? AsBrep(GeometryBase? geometry) => geometry switch
        {
            Brep b => b,
            Extrusion e => e.ToBrep(),
            _ => null,
        };

        // A closed brep that fills its world-aligned bounding box is a box (Box > Diagonal
        // on the World Top CPlane makes exactly that).
        private static bool IsSolidBox(Brep brep, double tolerance)
        {
            if (!brep.IsSolid || brep.Faces.Count != 6)
                return false;

            BoundingBox bbox = brep.GetBoundingBox(true);
            double bboxVolume = bbox.Diagonal.X * bbox.Diagonal.Y * bbox.Diagonal.Z;
            double volume = brep.GetVolume(tolerance, tolerance);
            return bboxVolume > 0 && Math.Abs(volume - bboxVolume) <= 0.001 * bboxVolume;
        }
    }
}
