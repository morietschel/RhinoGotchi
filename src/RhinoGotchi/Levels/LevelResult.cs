using System.Collections.Generic;

namespace RhinoGotchi.Levels
{
    /// <summary>The evaluator's verdict on an attempt.</summary>
    public sealed class LevelResult
    {
        public bool Solved { get; set; }

        /// <summary>0 to 1. How this maps to stars or points is decided later.</summary>
        public double Score { get; set; }

        /// <summary>Optional message for the player, e.g. "Close! 0.4 mm off the target".</summary>
        public string? Feedback { get; set; }

        /// <summary>
        /// Optional live check lines, e.g. "Closed solid ✓" / "4 holes ✗ (2 so far)". When present the
        /// UI shows them as a check list that updates after every command.
        /// </summary>
        public List<LevelCheck> Checks { get; } = new List<LevelCheck>();

        /// <summary>An optional extra goal worth a bonus star, e.g. "Cap it into a closed solid". Null if none.</summary>
        public string? BonusText { get; set; }

        /// <summary>Whether the bonus goal is met right now.</summary>
        public bool BonusEarned { get; set; }
    }

    /// <summary>One line of a live check list.</summary>
    public sealed class LevelCheck
    {
        public LevelCheck(string label, bool pass, string? hint = null)
        {
            Label = label;
            Pass = pass;
            Hint = hint;
        }

        /// <summary>What is checked, e.g. "24 mm thick".</summary>
        public string Label { get; }

        public bool Pass { get; }

        /// <summary>What to do about it when it fails, e.g. "It's 240 mm: type 0.024".</summary>
        public string? Hint { get; }
    }
}
