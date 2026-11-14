using System;
using System.Collections.Generic;
using System.Linq;

namespace RhinoGotchi.Content
{
    /// <summary>
    /// How commands chain: what kind of object a command leaves you with, and which commands
    /// commonly take that object next.
    /// </summary>
    internal static class NextSteps
    {
        private enum Kind { OpenCurve, ClosedCurve, Points, Solid, Transformed, Measurement }

        private static readonly Dictionary<Kind, string[]> Makers = new Dictionary<Kind, string[]>
        {
            [Kind.OpenCurve] = new[]
            {
                "Line", "Polyline", "Arc", "Parabola", "Hyperbola", "Conic", "Helix", "Spiral", "Curve", "InterpCrv",
                "Sketch", "Catenary", "BlendCrv", "ArcBlend", "Extend", "Trim", "Split", "Rebuild", "FitCrv", "Fair",
                "Smooth", "Match", "Project", "Pull", "DupEdge", "Crv2View", "Explode", "Fillet", "Chamfer", "Offset",
                "OffsetMultiple", "Silhouette",
            },
            [Kind.ClosedCurve] = new[] { "Rectangle", "Polygon", "Circle", "Ellipse", "CurveBoolean", "Join", "DupBorder", "Section", "Contour" },
            [Kind.Points] = new[] { "Point", "Points", "Divide", "Intersect" },
            [Kind.Measurement] = new[] { "Length", "CurvatureGraph" },
        };

        private static readonly Dictionary<Kind, (string Sentence, string[] Next)> Rules = new Dictionary<Kind, (string, string[])>
        {
            [Kind.OpenCurve] = ("leaves you with an open curve. Next you can string curves together, round their corners, or turn them into solids:",
                new[] { "Join", "Fillet", "Extend", "Offset", "Rebuild", "Divide", "Pipe", "ArrayCrv" }),
            [Kind.ClosedCurve] = ("leaves you with a closed curve, ready to become a solid:",
                new[] { "ExtrudeCrv", "Offset", "CurveBoolean", "Fillet", "ArrayPolar", "Divide" }),
            [Kind.Points] = ("leaves you with points. Connect them into curves:",
                new[] { "Polyline", "InterpCrv", "Curve" }),
            [Kind.Solid] = ("leaves you with a solid. Combine it with other solids, shape its edges, or slice it:",
                new[] { "BooleanUnion", "BooleanDifference", "BooleanIntersection", "FilletEdge", "ChamferEdge", "Shell", "Contour", "ArrayPolar" }),
            [Kind.Transformed] = ("changes objects you already have (curves or solids). Other transforms to chain with it:",
                new[] { "Move", "Copy", "Rotate", "Scale", "Mirror", "Array", "ArrayPolar" }),
            [Kind.Measurement] = ("only reports information. Use what you learn to fix the curve:",
                new[] { "Rebuild", "Fair", "Smooth", "Extend" }),
        };

        /// <summary>A sentence about what the command leaves you with, and commands that take it next.</summary>
        public static (string Sentence, List<CommandEntry> Next) For(CommandEntry entry)
        {
            Kind kind = KindOf(entry);
            var rule = Rules[kind];
            var next = rule.Next
                .Where(c => !string.Equals(c, entry.Command, StringComparison.OrdinalIgnoreCase))
                .Select(CommandCatalog.Find)
                .Where(e => e != null)
                .Cast<CommandEntry>()
                .ToList();
            return ($"{entry.Command} {rule.Sentence}", next);
        }

        private static Kind KindOf(CommandEntry entry)
        {
            foreach (var pair in Makers)
                if (pair.Value.Contains(entry.Command, StringComparer.OrdinalIgnoreCase))
                    return pair.Key;
            return entry.Category switch
            {
                "Solids" => Kind.Solid,
                "Transform" => Kind.Transformed,
                _ => Kind.OpenCurve,
            };
        }
    }
}
