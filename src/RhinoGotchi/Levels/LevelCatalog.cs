using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Rhino;
using RhinoGotchi.Levels.Samples;
using RhinoGotchi.Tutorials;

namespace RhinoGotchi.Levels
{
    /// <summary>One playable entry: a plain level or a tutorial.</summary>
    public sealed class LevelEntry
    {
        public LevelEntry(string id, string title, string command, Func<RhinoDoc, LevelRunner?> start)
        {
            Id = id;
            Title = title;
            Command = command;
            Start = start;
            OptionName = ToOptionName(id);
        }

        /// <summary>Stable id, e.g. "box-2pt-01" or "tut-01-loft".</summary>
        public string Id { get; }

        public string Title { get; }

        public string Command { get; }

        /// <summary>The id as a command-line option value (letters and digits only), e.g. "Tut01Loft".</summary>
        public string OptionName { get; }

        /// <summary>Starts the entry in a document (stopping any running level). Null if it couldn't start.</summary>
        public Func<RhinoDoc, LevelRunner?> Start { get; }

        // Rhino option values may only hold letters, digits and underscores: "tut-01-loft" -> "Tut01Loft".
        private static string ToOptionName(string id)
        {
            var sb = new StringBuilder();
            foreach (string part in id.Split(new[] { '-', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string clean = new string(part.Where(char.IsLetterOrDigit).ToArray());
                if (clean.Length > 0)
                    sb.Append(char.ToUpperInvariant(clean[0])).Append(clean.Substring(1));
            }
            return sb.ToString();
        }
    }

    /// <summary>All playable levels, in order: the sample level, then the Klosterneuburg tutorials.</summary>
    public static class LevelCatalog
    {
        public static readonly IReadOnlyList<LevelEntry> All = Build();

        public static LevelEntry? Find(string id) =>
            All.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.Ordinal));

        private static IReadOnlyList<LevelEntry> Build()
        {
            var entries = new List<LevelEntry>
            {
                FromLevel(TwoPointBoxLevel.Create),
            };
            foreach (Tutorial t in TutorialCatalog.All)
                entries.Add(new LevelEntry(t.Id, $"{t.Number:00} {t.Title}", t.MainCommand, doc => TutorialLevels.Start(doc, t)));
            return entries;
        }

        private static LevelEntry FromLevel(Func<Level> create)
        {
            Level sample = create();
            return new LevelEntry(sample.Id, sample.Title, sample.Command, doc => LevelRunner.Start(doc, create()));
        }
    }
}
