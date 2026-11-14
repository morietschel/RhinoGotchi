using System;
using System.Collections.Generic;
using System.Linq;
using RhinoGotchi.Tutorials.Content;

namespace RhinoGotchi.Tutorials
{
    /// <summary>
    /// The Klosterneuburg tutorials: twelve, easiest first, each with a core card (input prepared,
    /// one command, Run picks it for you) and a challenge card (prepare the input yourself).
    /// </summary>
    public static class TutorialCatalog
    {
        /// <summary>All tutorial geometry goes under this layer, never on the model's own layers.</summary>
        public const string RootLayer = "RhinoGotchi Tutorials";

        /// <summary>How many tutorials (each has two cards).</summary>
        public const int Count = 12;

        /// <summary>Every card: tutorial 1 core, tutorial 1 challenge, tutorial 2 core, … Built with a decimal point in every number.</summary>
        public static readonly IReadOnlyList<Tutorial> All = InvariantText.Run(Cards.Build);

        /// <summary>The core cards, easiest first.</summary>
        public static IEnumerable<Tutorial> Cores => All.Where(t => !t.IsChallenge);

        public static Tutorial? Find(string id) =>
            All.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.Ordinal));

        public static Tutorial? ChallengeOf(Tutorial core) =>
            All.FirstOrDefault(t => t.IsChallenge && t.Number == core.Number);
    }
}
