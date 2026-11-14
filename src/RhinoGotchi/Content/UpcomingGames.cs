namespace RhinoGotchi.Content
{
    /// <summary>
    /// Modes from the RhinoGotchi canvas that aren't built yet. The panel lists them with a
    /// "SOON" tag and a page that explains how they'll play.
    /// </summary>
    internal sealed class UpcomingGame
    {
        public string Id = "";
        public string Title = "";
        /// <summary>One line for the list row.</summary>
        public string Blurb = "";
        /// <summary>Where it lives on the home page: "learning", "daily", "campaign" or "create".</summary>
        public string Section = "";
        public string[] HowToPlay = new string[0];
        /// <summary>The hat its achievement unlocks, if any.</summary>
        public string Unlocks = "";
    }

    internal static class UpcomingGames
    {
        public static readonly UpcomingGame Codex = new UpcomingGame
        {
            Id = "codex", Title = "Every Single Command", Section = "learning",
            Blurb = "48 command cards, each a viewport microgame",
            HowToPlay = new[]
            {
                "Each card teaches one Rhino command, in four tabs: Transform, Curve, Surface, Solid.",
                "The puzzle is built so that command is the 1-step route. Any method still counts.",
                "Earn stars to fill the grid and work through all 48 commands.",
            },
            Unlocks = "Hard hat (First Steps) · Grad cap (Scholar)",
        };

        public static readonly UpcomingGame Quiz = new UpcomingGame
        {
            Id = "quiz", Title = "Quiz", Section = "learning",
            Blurb = "Name that command · 4 questions",
            HowToPlay = new[]
            {
                "Four questions about an object in the viewport, by topic: Mixed, Transform, Curve, Surface, Solid.",
                "Answer in the panel, type the command in Rhino, or click the object.",
                "Score 4/4 for the Propeller hat.",
            },
            Unlocks = "Propeller (Know-It-All)",
        };

        public static readonly UpcomingGame Missing = new UpcomingGame
        {
            Id = "missing", Title = "Missing Command", Section = "daily",
            Blurb = "Fill the gap in the recipe",
            HowToPlay = new[]
            {
                "A recipe of commands built the ghost, but some steps are blank.",
                "The known steps already ran. Run the missing ones, any way you like.",
                "Fill every blank with no extra steps for 3★. Practice goes from 1 blank to 4.",
            },
            Unlocks = "Wizard (No Take-Backs: solve without Undo)",
        };

        public static readonly UpcomingGame OneMoreLine = new UpcomingGame
        {
            Id = "oml", Title = "One More Line", Section = "daily",
            Blurb = "One step · 10 seconds",
            HowToPlay = new[]
            {
                "Every puzzle is exactly one step from done.",
                "You get 10 seconds per puzzle. A wrong step or running out of time breaks your streak.",
                "The daily is 8 puzzles for everyone. The 10 practice runs get longer and trickier.",
            },
            Unlocks = "Cowboy (One and Done: 5 in a row)",
        };

        public static readonly UpcomingGame Skyline = new UpcomingGame
        {
            Id = "skyline", Title = "Skyline", Section = "campaign",
            Blurb = "10 towers, 3 floors each",
            HowToPlay = new[]
            {
                "Build 10 towers, 3 floors each, difficulty 1 to 10.",
                "Each floor has a step budget (fewest + 3). Go over and lose a hard hat. Lose all 3 and the run ends.",
                "Clear a floor to pick 1 of 3 powerups. Top out to grow your skyline.",
            },
            Unlocks = "Tree (Topped Out) · Crown (all 10 towers)",
        };

        public static readonly UpcomingGame Office = new UpcomingGame
        {
            Id = "office", Title = "Office Tower", Section = "campaign",
            Blurb = "10 floors in one climb",
            HowToPlay = new[]
            {
                "Climb 10 floors in one run, each with its own task.",
                "3 hard hats for the whole climb. Go over a floor's budget and lose one.",
                "Clear a floor to pick a powerup: Free Step, Undo Pass, Star Shield and more.",
            },
            Unlocks = "Tree (Topped Out)",
        };

        public static readonly UpcomingGame Builder = new UpcomingGame
        {
            Id = "builder", Title = "Level Builder", Section = "create",
            Blurb = "Make puzzles for any mode, inside Rhino",
            HowToPlay = new[]
            {
                "Model the start with any commands, then run RG_SetStart.",
                "Model the goal, then run RG_SetTarget. It becomes the ghost players match.",
                "Solve it once to set the 3★ minimum, fill in the panel, then run RG_ExportPuzzle.",
            },
        };

        public static readonly UpcomingGame[] All = { Codex, Quiz, Missing, OneMoreLine, Skyline, Office, Builder };
    }
}
