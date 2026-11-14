using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoGotchi.Levels;

namespace RhinoGotchi.Tutorials.Evaluators
{
    /// <summary>What a tutorial's grading step returns before the shared rules are applied.</summary>
    public sealed class Grade
    {
        public Grade(double score, bool solved, string feedback)
        {
            Score = score;
            Solved = solved;
            Feedback = feedback;
        }

        public double Score { get; }
        public bool Solved { get; }
        public string Feedback { get; }

        public static Grade Nothing(string feedback) => new Grade(0, false, feedback);
    }

    /// <summary>
    /// Base for the tutorial checks. Looks only at objects the learner added on the tutorial's own
    /// layers (never the set-up objects, never the original model), turns the tutorial's Check into a score from
    /// 0 to 1 with plain feedback, and halves the score when the main command wasn't used.
    /// </summary>
    public abstract class TutorialEvaluator : ILevelEvaluator
    {
        protected TutorialEvaluator(Tutorial tutorial)
        {
            Tutorial = tutorial;
        }

        public Tutorial Tutorial { get; }

        public LevelResult Evaluate(LevelAttempt attempt) => InvariantText.Run(() => EvaluateNow(attempt));

        private LevelResult EvaluateNow(LevelAttempt attempt)
        {
            RhinoDoc doc = attempt.Doc;
            double tolerance = Math.Max(doc.ModelAbsoluteTolerance, 1e-6);
            List<RhinoObject> work = attempt.AddedObjects().Where(o => IsWorkLayer(doc, o.Attributes.LayerIndex)).ToList();

            Grade grade;
            try
            {
                grade = Check(attempt, work, tolerance);
            }
            catch (Exception ex)
            {
                grade = Grade.Nothing($"Couldn't check this result ({ex.Message}).");
            }

            double score = Math.Max(0, Math.Min(1, grade.Score));
            string feedback = grade.Feedback;
            bool used = attempt.CommandsRun.Any(c => string.Equals(c, Tutorial.MainCommand, StringComparison.OrdinalIgnoreCase));
            if (!used && score > 0)
            {
                score *= 0.5;
                feedback += $" Use the {Tutorial.MainCommand} command for full points.";
            }
            return new LevelResult { Solved = grade.Solved, Score = score, Feedback = feedback.Trim() };
        }

        /// <summary>Grades the learner's objects (already filtered to the tutorial's work layers).</summary>
        protected abstract Grade Check(LevelAttempt attempt, List<RhinoObject> work, double tolerance);

        /// <summary>
        /// The tutorial's own layers, Input included: copy commands such as ArrayPolar and ArrayCrv
        /// put their copies on the original's layer. The set-up objects themselves never count,
        /// because they aren't in the attempt's added objects.
        /// </summary>
        protected bool IsWorkLayer(RhinoDoc doc, int layerIndex)
        {
            if (layerIndex < 0 || layerIndex >= doc.Layers.Count)
                return false;
            string path = doc.Layers[layerIndex].FullPath;
            return path == Tutorial.LayerPath || path.StartsWith(Tutorial.LayerPath + "::", StringComparison.Ordinal);
        }

        /// <summary>Start (Input) objects with the given name, e.g. the reference plane.</summary>
        protected static IEnumerable<GeometryBase> StartObjects(LevelAttempt attempt, string name)
        {
            foreach (Guid id in attempt.StartObjectIds)
            {
                RhinoObject? obj = attempt.Doc.Objects.FindId(id);
                if (obj != null && !obj.IsDeleted && obj.Name == name)
                    yield return obj.Geometry;
            }
        }
    }

    /// <summary>Small geometry measurements the checks share.</summary>
    public static class Measure
    {
        public static Brep? AsBrep(GeometryBase? g) => g switch
        {
            Brep b => b,
            Extrusion e => e.ToBrep(),
            Surface s => s.ToBrep(),
            _ => null,
        };

        public static IEnumerable<(RhinoObject Obj, Brep Brep)> Breps(IEnumerable<RhinoObject> objects)
        {
            foreach (RhinoObject o in objects)
                if (AsBrep(o.Geometry) is Brep b)
                    yield return (o, b);
        }

        public static IEnumerable<(RhinoObject Obj, Curve Curve)> Curves(IEnumerable<RhinoObject> objects)
        {
            foreach (RhinoObject o in objects)
                if (o.Geometry is Curve c)
                    yield return (o, c);
        }

        /// <summary>Points on a face in an n × n grid of its parameter domain, skipping any outside its trim.</summary>
        public static List<Point3d> Samples(BrepFace face, int n)
        {
            var points = new List<Point3d>();
            Interval u = face.Domain(0), v = face.Domain(1);
            for (int i = 0; i <= n; i++)
            {
                for (int j = 0; j <= n; j++)
                {
                    double pu = u.ParameterAt(i / (double)n), pv = v.ParameterAt(j / (double)n);
                    if (face.IsPointOnFace(pu, pv) != PointFaceRelation.Exterior)
                        points.Add(face.PointAt(pu, pv));
                }
            }
            return points;
        }

        /// <summary>Points evenly spaced by length along a curve, ends included.</summary>
        public static List<Point3d> Samples(Curve curve, int n)
        {
            var points = new List<Point3d>();
            for (int i = 0; i <= n; i++)
            {
                curve.NormalizedLengthParameter(i / (double)n, out double t);
                points.Add(curve.PointAt(t));
            }
            return points;
        }

        public static double DistanceTo(Surface surface, Point3d p)
        {
            surface.ClosestPoint(p, out double u, out double v);
            return p.DistanceTo(surface.PointAt(u, v));
        }

        public static double DistanceTo(Brep brep, Point3d p) => p.DistanceTo(brep.ClosestPoint(p));

        /// <summary>The largest distance from points on the brep's faces to the surface.</summary>
        public static double MaxDeviation(Brep brep, Surface target, int n = 10)
        {
            double worst = 0;
            foreach (BrepFace face in brep.Faces)
                foreach (Point3d p in Samples(face, n))
                    worst = Math.Max(worst, DistanceTo(target, p));
            return worst;
        }

        public static double Area(Brep brep) => AreaMassProperties.Compute(brep)?.Area ?? 0;

        public static double Area(Surface surface) => AreaMassProperties.Compute(surface)?.Area ?? 0;

        public static double Volume(Brep brep) => Math.Abs(VolumeMassProperties.Compute(brep)?.Volume ?? 0);

        public static string Metres(double value) => $"{value:0.000} m";
    }
}
