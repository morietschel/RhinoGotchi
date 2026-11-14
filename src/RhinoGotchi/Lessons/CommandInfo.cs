using System;
using System.Collections.Generic;

namespace RhinoGotchi.Lessons
{
    /// <summary>What the game knows about a Rhino command: a one-line explanation shown after each answer.</summary>
    public static class CommandInfo
    {
        private static readonly Dictionary<string, string> Tips = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Box"] = "Box makes a solid box. Its Diagonal option takes two opposite corners.",
            ["Sphere"] = "Sphere makes a ball from a center and a radius.",
            ["Cylinder"] = "Cylinder makes a solid tube: a base circle and a height.",
            ["Cone"] = "Cone makes a solid that tapers from a circle to a point.",
            ["Loft"] = "Loft stretches a smooth skin through a row of profile curves.",
            ["Sweep1"] = "Sweep1 moves profile curves along ONE rail.",
            ["Sweep2"] = "Sweep2 moves profile curves along TWO rails, so you control both edges.",
            ["Revolve"] = "Revolve spins a profile curve around an axis.",
            ["ExtrudeCrv"] = "ExtrudeCrv pushes a curve straight out into a surface.",
            ["Pipe"] = "Pipe wraps a round tube around a curve.",
            ["NetworkSrf"] = "NetworkSrf fits a surface to a grid of crossing curves.",
            ["FilletEdge"] = "FilletEdge rounds over the sharp edges of a solid.",
            ["BooleanUnion"] = "BooleanUnion merges overlapping solids into one.",
            ["BooleanDifference"] = "BooleanDifference cuts one solid out of another.",
            ["Polyline"] = "Polyline draws connected straight segments.",
            ["Circle"] = "Circle draws a circle from a center and a radius.",
            ["Line"] = "Line draws one straight segment between two points.",
            ["Rectangle"] = "Rectangle draws a closed four-sided curve from two corners.",
        };

        /// <summary>Every command that has an icon, so it can be a quiz answer.</summary>
        public static IEnumerable<string> All => Tips.Keys;

        public static string Tip(string command) =>
            Tips.TryGetValue(command, out string? tip) ? tip : command;
    }
}
