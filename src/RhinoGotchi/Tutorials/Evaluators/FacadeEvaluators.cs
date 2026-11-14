using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using RhinoGotchi.Levels;

namespace RhinoGotchi.Tutorials.Evaluators
{
    /// <summary>8 OffsetSrf: a closed solid on the glass surface whose thickness is right at 5 places.</summary>
    public sealed class OffsetSrfEvaluator : TutorialEvaluator
    {
        public const double ThicknessTolerance = 0.002;
        private readonly Brep _face;
        private readonly double _thickness;

        public OffsetSrfEvaluator(Tutorial tutorial, Brep face, double thickness) : base(tutorial)
        {
            _face = face;
            _thickness = thickness;
        }

        protected override Grade Check(LevelAttempt attempt, List<RhinoObject> work, double tolerance)
        {
            BrepFace face = _face.Faces[0];
            var spots = new List<(Point3d P, Vector3d N)>();
            foreach (var (a, b) in new[] { (0.2, 0.2), (0.8, 0.2), (0.5, 0.5), (0.2, 0.8), (0.8, 0.8) })
            {
                double u = face.Domain(0).ParameterAt(a), v = face.Domain(1).ParameterAt(b);
                spots.Add((face.PointAt(u, v), face.NormalAt(u, v)));
            }

            var near = Measure.Breps(work).Select(x => x.Brep)
                .Where(b => spots.All(s => Measure.DistanceTo(b, s.P) <= _thickness + 0.01))
                .ToList();
            if (near.Count == 0)
                return Grade.Nothing("No glass panel on the glass surface yet. Run OffsetSrf with Solid=Yes.");

            Grade? best = null;
            foreach (Brep panel in near)
            {
                var measured = new List<double>();
                foreach (var (p, n) in spots)
                {
                    var probe = new LineCurve(p + n * 0.5, p - n * 0.5); // long enough to find a far too thick panel
                    if (!Intersection.CurveBrep(probe, panel, tolerance, out _, out Point3d[] hits) || hits.Length < 2)
                        continue;
                    // The two faces nearest the glass surface.
                    var nearest = hits.OrderBy(h => h.DistanceTo(p)).Take(2).ToList();
                    measured.Add(nearest[0].DistanceTo(nearest[1]));
                }
                int right = measured.Count(t => Math.Abs(t - _thickness) <= ThicknessTolerance);
                bool solid = panel.IsSolid;
                double score = 0.4 * (solid ? 1 : 0) + 0.6 * right / spots.Count;
                var notes = new List<string>();
                notes.Add(solid ? "Your panel is a closed solid." : "Your panel isn't closed: run OffsetSrf again with Solid=Yes.");
                if (measured.Count > 0)
                {
                    double avg = measured.Average();
                    notes.Add(right == spots.Count
                        ? $"It's {avg * 1000:0.#} mm thick everywhere."
                        : $"It measures about {avg * 1000:0.#} mm thick; it should be {_thickness * 1000:0.#} mm (type {_thickness} in this metre model).");
                }
                var grade = new Grade(score, solid && right == spots.Count, string.Join(" ", notes));
                if (best == null || grade.Score > best.Score)
                    best = grade;
            }
            return best ?? Grade.Nothing("Nothing to check yet.");
        }
    }

    /// <summary>9 Sweep2: a surface meeting the four landing points, with under 1% slope across its width.</summary>
    public sealed class Sweep2Evaluator : TutorialEvaluator
    {
        public const double CornerTolerance = 0.01;
        public const double MaxCrossSlope = 0.01;
        private readonly Point3d[] _corners;

        public Sweep2Evaluator(Tutorial tutorial, IEnumerable<Point3d> corners) : base(tutorial)
        {
            _corners = corners.ToArray();
        }

        protected override Grade Check(LevelAttempt attempt, List<RhinoObject> work, double tolerance)
        {
            var surfaces = Measure.Breps(work).Where(x => x.Brep.Faces.Count == 1).Select(x => x.Brep).ToList();
            if (surfaces.Count == 0)
                return Grade.Nothing("No ramp surface yet. Draw the two rails and the two landing lines, then run Sweep2.");

            Grade? best = null;
            foreach (Brep s in surfaces)
            {
                int meets = _corners.Count(c => Measure.DistanceTo(s, c) <= CornerTolerance);
                Surface srf = s.Faces[0].UnderlyingSurface();

                // "Across" is the direction whose isocurves are shorter (the ramp's width).
                Interval d0 = srf.Domain(0), d1 = srf.Domain(1);
                double len0 = srf.IsoCurve(0, d1.Mid)?.GetLength() ?? 0;   // runs along U
                double len1 = srf.IsoCurve(1, d0.Mid)?.GetLength() ?? 0;   // runs along V
                int across = len0 < len1 ? 0 : 1;
                Interval along = across == 0 ? d1 : d0;

                var slopes = new List<double>();
                foreach (double f in new[] { 0.1, 0.3, 0.5, 0.7, 0.9 })
                {
                    Curve? c = srf.IsoCurve(across, along.ParameterAt(f));
                    if (c == null)
                        continue;
                    Point3d a = c.PointAtStart, b = c.PointAtEnd;
                    double plan = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
                    if (plan > 1e-6)
                        slopes.Add(Math.Abs(b.Z - a.Z) / plan);
                }
                double worst = slopes.Count > 0 ? slopes.Max() : 1;
                bool level = slopes.Count > 0 && worst < MaxCrossSlope;

                double score = 0.5 * meets / _corners.Length + 0.5 * (level ? 1 : Math.Max(0, 1 - worst / 0.05));
                var notes = new List<string>
                {
                    meets == _corners.Length ? "The ramp meets all four landing points." : $"The ramp meets {meets} of the 4 landing points. Snap the rails and lines to the points.",
                    level ? "It doesn't tilt sideways anywhere." : $"It tilts sideways by up to {worst * 100:0.#}%. Check both rails run the same way and the lines join matching points.",
                };
                var grade = new Grade(score, meets == _corners.Length && level, string.Join(" ", notes));
                if (best == null || grade.Score > best.Score)
                    best = grade;
            }
            return best ?? Grade.Nothing("Nothing to check yet.");
        }
    }
}
