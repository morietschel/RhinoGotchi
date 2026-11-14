using System;
using System.Collections.Generic;
using System.Linq;
using RhinoGotchi.Tutorials.Content;

namespace RhinoGotchi.Tutorials
{
    /// <summary>
    /// The Klosterneuburg tutorial series, in teaching order: roof surface, working
    /// curves from the roof, setting out and structure, facade and terrace, details.
    /// Titles, commands and reference layers are final; the teaching text is still
    /// placeholder until the content pass.
    /// </summary>
    public static class TutorialCatalog
    {
        /// <summary>All tutorial geometry goes under this layer, never on the model's own layers.</summary>
        public const string RootLayer = "RhinoGotchi Tutorials";

        /// <summary>Marks text that the content pass still has to write.</summary>
        public const string Todo = "TODO: written in the content pass.";

        private static readonly Dictionary<string, string> PartTitles = new Dictionary<string, string>
        {
            ["A"] = "Roof surface",
            ["B"] = "Working curves from the roof",
            ["C"] = "Setting out and structure",
            ["D"] = "Facade and terrace",
            ["E"] = "Details",
        };

        // Teaching text and set-up, by tutorial number. Tutorials not listed keep the placeholders.
        // Declared before All: static fields initialise in order, and All's Make() reads this.
        private static readonly Dictionary<int, Action<Tutorial>> Fillers = new Dictionary<int, Action<Tutorial>>
        {
            [1] = RoofTutorials.Loft,
            [2] = RoofTutorials.NetworkSrf,
            [3] = RoofTutorials.Contour,
            [4] = RoofTutorials.Intersect,
            [5] = StructureTutorials.ArrayPolar,
            [6] = StructureTutorials.Sweep1,
            [7] = StructureTutorials.FlowAlongSrf,
            [8] = FacadeTutorials.OffsetSrf,
            [9] = FacadeTutorials.Sweep2,
            [10] = DetailTutorials.BooleanDifference,
            [11] = DetailTutorials.Pipe,
            [12] = DetailTutorials.ArrayCrv,
        };

        // Built with the invariant culture so every number in the text reads 0.024, never 0,024.
        public static readonly IReadOnlyList<Tutorial> All = InvariantText.Run(() => new[]
        {
            Make(1, "A", "Undulating roof", "Loft", "Wavy roof patch", new string[0], ReferenceLayers.Roof),
            Make(2, "A", "Controlled roof patch", "NetworkSrf", "Wavy roof patch", new[] { "Rebuild" }, ReferenceLayers.Roof),
            Make(3, "B", "Roof section profiles", "Contour", "Roof patch sections", new string[0], ReferenceLayers.Roof),
            Make(4, "B", "Radial roof-rib profile", "Intersect", "Radial rib through the roof", new[] { "Project" }, ReferenceLayers.Roof),
            Make(5, "C", "Radial setting-out", "ArrayPolar", "Radial roof reference planes", new string[0], ReferenceLayers.MainBeamsLong, ReferenceLayers.MainBeamsShort),
            Make(6, "C", "Curved perimeter beam", "Sweep1", "Beam along the roof edge", new[] { "DupBorder", "Split", "Cap" }, ReferenceLayers.RoofEdge),
            Make(7, "C", "Secondary joists on the roof", "FlowAlongSrf", "Joists following the roof", new string[0], ReferenceLayers.Joists, ReferenceLayers.Roof),
            Make(8, "D", "Glazing thickness", "OffsetSrf", "One facade glass panel", new string[0], ReferenceLayers.Glass),
            Make(9, "D", "Curved terrace ramp", "Sweep2", "Terrace ramp segment", new string[0], ReferenceLayers.Ramp, ReferenceLayers.TerraceOuter),
            Make(10, "E", "Steel connection plate", "BooleanDifference", "Bolted steel plate", new string[0], ReferenceLayers.Plates, ReferenceLayers.Dowels),
            Make(11, "E", "Stair handrail", "Pipe", "Tubular stair handrail", new[] { "BlendCrv", "Join" }, ReferenceLayers.StairRail),
            Make(12, "E", "Balustrade bars", "ArrayCrv", "Stair balustrade bars", new string[0], ReferenceLayers.StairFence, ReferenceLayers.StairRail),
        });

        public static Tutorial? Find(string id) =>
            All.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.Ordinal));

        public static IEnumerable<IGrouping<string, Tutorial>> ByPart() => All.GroupBy(t => t.Part);

        public static string PartTitle(string part) => PartTitles.TryGetValue(part, out var title) ? title : part;

        private static Tutorial Make(int number, string part, string title, string command, string element,
            string[] supporting, params string[] referenceLayers)
        {
            var t = new Tutorial
            {
                Id = $"tut-{number:00}-{command.ToLowerInvariant()}",
                Number = number,
                Part = part,
                PartTitle = PartTitle(part),
                Title = title,
                MainCommand = command,
                Element = element,
                Goal = Todo,
                InputGeometry = Todo,
                Check = Todo,
            };
            t.SupportingCommands.AddRange(supporting);
            t.ReferenceLayers.AddRange(referenceLayers);
            t.KeyOptions.Add(Todo);
            t.CommonMistakes.Add(Todo);
            t.CommonMistakes.Add(Todo);

            if (Fillers.TryGetValue(number, out var fill))
            {
                t.KeyOptions.Clear();
                t.CommonMistakes.Clear();
                fill(t);
                TutorialChecks.Attach(t);
            }
            return t;
        }
    }
}
