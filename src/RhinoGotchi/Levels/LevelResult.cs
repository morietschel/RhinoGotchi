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
    }
}
