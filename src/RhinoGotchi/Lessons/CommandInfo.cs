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
            ["Offset"] = "Offset makes a parallel copy of a curve at a set distance.",
            ["Trim"] = "Trim cuts away the parts of an object that cross a cutting object.",
            ["Join"] = "Join connects curves or surfaces that touch end to end into one object.",
            ["Fillet"] = "Fillet rounds the corner between two curves with an arc. (FilletEdge does it on solids.)",
            ["Move"] = "Move slides objects from one point to another.",
            ["Rotate"] = "Rotate turns objects around a center point by an angle.",
            ["Scale"] = "Scale makes objects bigger or smaller from a base point.",
            ["Mirror"] = "Mirror flips a copy of objects across a line: great for symmetric parts.",
            ["ArrayPolar"] = "ArrayPolar copies objects evenly around a center point.",
            ["OffsetSrf"] = "OffsetSrf makes a copy of a surface at a constant distance (and can make it solid).",
            ["BlendSrf"] = "BlendSrf builds a smooth bridge surface between the edges of two surfaces.",
            ["Contour"] = "Contour slices objects into evenly spaced section curves.",
            ["Patch"] = "Patch fits a surface into a closed boundary of curves.",
            ["Cap"] = "Cap fills planar open holes, turning an open surface into a closed solid.",
            ["Shell"] = "Shell hollows out a solid, leaving walls of a set thickness.",
            ["BooleanIntersection"] = "BooleanIntersection keeps only the volume where solids overlap.",
            ["ExtrudeSrf"] = "ExtrudeSrf pushes a surface straight out into a solid.",
        };

        // Tips for commands the tutorials teach that have no quiz icon (so they never appear as quiz answers).
        private static readonly Dictionary<string, string> MoreTips = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ArrayCrv"] = "ArrayCrv copies objects evenly along a curve.",
            ["Intersect"] = "Intersect makes curves (or points) where objects cross each other.",
            ["FlowAlongSrf"] = "FlowAlongSrf bends objects from a flat base surface onto a curved target surface.",
        };

        /// <summary>Every command that has an icon, so it can be a quiz answer.</summary>
        public static IEnumerable<string> All => Tips.Keys;

        public static string Tip(string command) =>
            Tips.TryGetValue(command, out string? tip) ? tip
            : MoreTips.TryGetValue(command, out string? more) ? more
            : command;
    }
}
