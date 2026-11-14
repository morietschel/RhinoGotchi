using System;
using System.Linq;
using Rhino.Geometry;
using RhinoGotchi.Levels;
using RhinoGotchi.Tutorials.Evaluators;

namespace RhinoGotchi.Tutorials.Content
{
    /// <summary>
    /// Gives each tutorial's level its automatic check. The checks rebuild what they compare against
    /// from the same context as the set-up (not from the Input objects), so they still work if the
    /// learner moved or deleted an input.
    /// </summary>
    internal static class TutorialChecks
    {
        public static void Attach(Tutorial t)
        {
            Func<TutorialContext, Level>? inner = t.CreateLevel;
            t.CreateLevel = context =>
            {
                Level level = inner?.Invoke(context) ?? TutorialLevels.DefaultLevel(t);
                level.Evaluator = Create(t, context);
                return level;
            };
        }

        private static ILevelEvaluator? Create(Tutorial t, TutorialContext context)
        {
            double tolerance = Math.Max(context.Doc.ModelAbsoluteTolerance, 1e-6);
            switch (t.Number)
            {
                case 1:
                    return new LoftEvaluator(t, TutorialGeometry.Patch(context));
                case 2:
                {
                    Surface patch = TutorialGeometry.Patch(context);
                    return new NetworkSrfEvaluator(t, patch, TutorialGeometry.Boundary(patch));
                }
                case 3:
                    return new ContourEvaluator(t);
                case 4:
                    return new IntersectEvaluator(t, TutorialGeometry.Roof(context), TutorialGeometry.Centre(context));
                case 5:
                    return new ArrayPolarEvaluator(t, TutorialGeometry.Centre(context), StructureTutorials.RadialCount, "Reference plane (due east)");
                case 6:
                {
                    Curve? rail = EdgeRail(context, tolerance);
                    return rail == null ? null : new Sweep1Evaluator(t, rail, StructureTutorials.EdgeBeamWidth, StructureTutorials.EdgeBeamDepth);
                }
                case 7:
                    return new FlowEvaluator(t, TutorialGeometry.Patch(context), StructureTutorials.JoistCount);
                case 8:
                    return new OffsetSrfEvaluator(t, FacadeTutorials.EastGlassFace(context), FacadeTutorials.GlassThickness);
                case 9:
                {
                    var (a, b) = FacadeTutorials.RampPoints(context);
                    return new Sweep2Evaluator(t, new[] { a[0], b[0], a[2], b[2] });
                }
                case 10:
                {
                    Point3d c = DetailTutorials.PlateCorner;
                    var box = new BoundingBox(c, c + new Vector3d(DetailTutorials.PlateWidth, DetailTutorials.PlateLength, DetailTutorials.PlateThickness));
                    double hole = Math.PI * DetailTutorials.HoleRadius * DetailTutorials.HoleRadius * DetailTutorials.PlateThickness;
                    return new BooleanEvaluator(t, box, hole, DetailTutorials.Holes.Length);
                }
                case 11:
                {
                    Curve? centreline = HandrailCentreline(context, tolerance);
                    return centreline == null ? null : new PipeEvaluator(t, centreline, DetailTutorials.RailRadius);
                }
                case 12:
                    return new ArrayCrvEvaluator(t, DetailTutorials.RunCurve(DetailTutorials.HandrailRun(context).Points),
                        DetailTutorials.BarHeight, DetailTutorials.BarSpacing, "Bar");
                default:
                    return null;
            }
        }

        /// <summary>Tutorial 6's rail: the lifted roof border between the two split points, the shorter (east) piece.</summary>
        private static Curve? EdgeRail(TutorialContext context, double tolerance)
        {
            Brep roof = TutorialGeometry.Roof(context);
            Curve? border = Curve.JoinCurves(roof.DuplicateNakedEdgeCurves(true, false)).OrderByDescending(c => c.GetLength()).FirstOrDefault();
            if (border == null)
                return null;
            Point3d centre = TutorialGeometry.Centre(context);
            border.ClosestPoint(StructureTutorials.PointAtBearing(border, centre, -45), out double t1);
            border.ClosestPoint(StructureTutorials.PointAtBearing(border, centre, 45), out double t2);
            Curve[] pieces = border.Split(new[] { t1, t2 });
            return pieces.OrderBy(c => c.GetLength()).FirstOrDefault();
        }

        /// <summary>Tutorial 11's centreline as the steps build it: run, curvature blend, level return, joined.</summary>
        private static Curve? HandrailCentreline(TutorialContext context, double tolerance)
        {
            var run = DetailTutorials.HandrailRun(context);
            Curve slope = DetailTutorials.RunCurve(run.Points);
            var ret = new LineCurve(run.ReturnStart, run.ReturnEnd);
            Curve? blend = Curve.CreateBlendCurve(slope, slope.Domain.Max, false, BlendContinuity.Curvature,
                                                  ret, ret.Domain.Min, true, BlendContinuity.Curvature);
            if (blend == null)
                return slope;
            Curve[] joined = Curve.JoinCurves(new Curve[] { slope, blend, ret }, tolerance * 10);
            return joined.Length == 1 ? joined[0] : slope;
        }
    }
}
