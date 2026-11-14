using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RhinoGotchi.Content
{
    internal sealed class CommandEntry
    {
        public CommandEntry(string command, string category, string description, string iconFile)
        {
            Command = command;
            Category = category;
            Description = description;
            IconFile = iconFile;
        }

        public string Command { get; }
        public string Category { get; }
        public string Description { get; }

        /// <summary>File name (without .svg) in Rhino/RhinoIcons/svg, copied to the build's Icons folder.</summary>
        public string IconFile { get; }

        public bool HasDemo => CommandDemos.Has(Command);
    }

    internal sealed class CommandCategory
    {
        public CommandCategory(string name, string blurb, List<CommandEntry> commands)
        {
            Name = name;
            Blurb = blurb;
            Commands = commands;
        }

        public string Name { get; }
        public string Blurb { get; }
        public List<CommandEntry> Commands { get; }
    }

    /// <summary>
    /// The Command Library: Curves, Solids and Transform commands. Descriptions come from the team's
    /// command dataset (Rhino/rhino_commands_all.csv, embedded in the plugin).
    /// </summary>
    internal static class CommandCatalog
    {
        private const string CsvResource = "RhinoGotchi.Data.rhino_commands_all.csv";

        private static readonly Lazy<List<CommandCategory>> Lazy = new Lazy<List<CommandCategory>>(Load);

        public static List<CommandCategory> Categories => Lazy.Value;

        public static CommandEntry? Find(string command) =>
            Categories.SelectMany(c => c.Commands)
                .FirstOrDefault(e => string.Equals(e.Command, command, StringComparison.OrdinalIgnoreCase));

        // "Command|Icon file". Icon names are the tooltip-style file names in Rhino/RhinoIcons/svg.
        private static readonly string[] Curves =
        {
            "Line|Line", "Polyline|Polyline", "Rectangle|Rectangle Corner to Corner", "Polygon|Polygon_ Center, Radius",
            "Circle|Circle_ center, radius", "Arc|Arc_ Center, Start, Angle", "Ellipse|Ellipse_ from foci",
            "Parabola|Parabola by focus", "Hyperbola|Hyperbola", "Conic|Conic", "Helix|Helix", "Spiral|Spiral",
            "Curve|Control point curve", "InterpCrv|Curve_ interpolate points", "Sketch|Sketch", "Catenary|Catenary",
            "Point|Single point", "Points|Multiple points", "Offset|Offset curve", "OffsetMultiple|Offset curve multiple",
            "Fillet|Fillet curves", "Chamfer|Chamfer curves", "BlendCrv|Adjustable curve blend", "ArcBlend|ArcBlend",
            "Extend|Extend curve", "Trim|Trim", "Split|Split", "Join|Join", "Explode|Explode", "Rebuild|Rebuild Curve",
            "FitCrv|Refit curve", "Fair|Fair curve", "Smooth|Smooth", "Match|Match curve", "CurveBoolean|Curve Boolean",
            "Project|Project", "Pull|Pull", "DupEdge|Duplicate edge", "DupBorder|Duplicate border", "Contour|Contour",
            "Section|Section", "Silhouette|Silhouette", "Intersect|Intersection",
            "Divide|Divide curve by number of segments", "Length|Length", "CurvatureGraph|Curvature graph on",
            "Crv2View|Curve from 2 views",
        };

        private static readonly string[] Solids =
        {
            "Box|Box_ Corner to Corner, Height", "Sphere|Sphere_ Center, Radius", "Ellipsoid|Ellipsoid_ from foci",
            "Cylinder|Cylinder", "Cone|Cone", "Pyramid|Pyramid", "Torus|Torus", "Tube|Tube", "Pipe|Pipe",
            "ExtrudeCrv|Extrude straight", "ExtrudeSrf|Extrude surface", "Cap|Cap planar holes", "Slab|Slab from polyline",
            "BooleanUnion|Boolean union", "BooleanDifference|Boolean difference", "BooleanIntersection|Boolean intersection",
            "BooleanSplit|Boolean split", "FilletEdge|Fillet edges", "ChamferEdge|Chamfer Edges", "BlendEdge|Blend edges",
            "Shell|Shell", "OffsetSrf|Offset Surface", "MoveFace|Move face", "MoveEdge|Move Edge",
            "SplitFace|Split planar face", "ExtractSrf|Extract surface", "CreateSolid|Create solid", "Wirecut|Wire cut",
            "Rib|Rib", "MakeHole|Make hole", "RoundHole|Round hole", "TextObject|Text object",
        };

        private static readonly string[] Transforms =
        {
            "Move|Move", "Copy|Copy", "Rotate|Rotate 2-D", "Rotate3D|Rotate 3-D", "Scale|Scale 3-D", "Scale1D|Scale 1-D",
            "Scale2D|Scale 2-D", "ScaleNU|Scale non-uniform", "Mirror|Mirror", "Orient|Orient_ 2 points",
            "Orient3Pt|Orient_ 3 points", "OrientOnSrf|Orient on surface", "OrientOnCrv|Orient perpendicular to curve",
            "Array|Array", "ArrayPolar|Array polar", "ArrayCrv|Array along curve", "ArraySrf|Array on surface",
            "ArrayLinear|Array linear", "Align|Align objects", "Distribute|Distribute", "Twist|Twist", "Bend|Bend",
            "Taper|Taper", "Flow|Flow along curve", "FlowAlongSrf|Flow along surface", "Splop|Splop",
            "Maelstrom|Maelstrom", "Stretch|Stretch", "CageEdit|Cage Edit", "BoxEdit|Box edit", "Shear|Shear",
            "SetPt|Set points", "RemapCPlane|Remap to CPlane", "Gumball|Gumball",
        };

        /// <summary>Entries in the dataset whose description belongs to a different command.</summary>
        private static readonly Dictionary<string, string> DescriptionFixes = new Dictionary<string, string>
        {
            ["Curve"] = "Draws a curve from control points.",
            ["Cylinder"] = "Draws a solid cylinder.",
            ["CurveBoolean"] = "Trims and joins overlapping curves into regions by picking areas.",
            ["CurvatureGraph"] = "Shows a curvature graph on curves and surfaces.",
        };

        private static List<CommandCategory> Load()
        {
            var descriptions = ReadDescriptions();
            return new List<CommandCategory>
            {
                Build("Curves", "Draw, edit and analyze curves.", Curves, descriptions),
                Build("Solids", "Make closed solids, combine them and shape their edges.", Solids, descriptions),
                Build("Transform", "Move, copy, rotate, scale and deform objects.", Transforms, descriptions),
            };
        }

        private static CommandCategory Build(string name, string blurb, string[] rows, Dictionary<string, string> descriptions)
        {
            var entries = rows.Select(row =>
            {
                var parts = row.Split('|');
                string command = parts[0];
                if (!DescriptionFixes.TryGetValue(command, out var description) &&
                    !descriptions.TryGetValue(command, out description))
                    description = "";
                return new CommandEntry(command, name, description, parts[1]);
            }).ToList();
            return new CommandCategory(name, blurb, entries);
        }

        private static Dictionary<string, string> ReadDescriptions()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using var stream = typeof(CommandCatalog).Assembly.GetManifestResourceStream(CsvResource);
            if (stream == null)
                return result;

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            reader.ReadLine(); // header: command,description
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var fields = ParseCsvLine(line);
                if (fields.Count >= 2 && fields[0].Length > 0)
                    result[fields[0]] = fields[1];
            }
            return result;
        }

        /// <summary>Splits one CSV line, honoring double-quoted fields with commas and "" escapes.</summary>
        private static List<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var field = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else if (c == '"')
                    {
                        quoted = false;
                    }
                    else
                    {
                        field.Append(c);
                    }
                }
                else if (c == '"')
                {
                    quoted = true;
                }
                else if (c == ',')
                {
                    fields.Add(field.ToString());
                    field.Clear();
                }
                else
                {
                    field.Append(c);
                }
            }
            fields.Add(field.ToString());
            return fields;
        }
    }
}
