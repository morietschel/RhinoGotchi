using Rhino.Geometry;
using RhinoGotchi.Fin;

namespace GotchiFins
{
    /// <summary>
    /// A point the player clicks. It glows yellow in the Axon view with a numbered label,
    /// and a locked guide point sits under it so clicks snap to it.
    /// </summary>
    public sealed class Click
    {
        public Click(string label, Point3d at, double labelX, double labelY)
        {
            Label = label;
            At = at;
            LabelX = labelX;
            LabelY = labelY;
        }

        public string Label { get; }
        public Point3d At { get; }

        /// <summary>Screen direction from the point to its label (x right, y down), about -1..1.</summary>
        public double LabelX { get; }
        public double LabelY { get; }
    }

    public enum PageKind
    {
        Welcome,
        Level,
        Done,
    }

    /// <summary>
    /// One screen of the game: the tutorial, a level, or the end. A level teaches one Rhino
    /// command, in a headline, one short line and at most three steps.
    /// </summary>
    public sealed class GamePage
    {
        private GamePage(PageKind kind, string kicker, string headline, string intro, string goLabel,
                         Stage? stage = null, string? command = null, string? macro = null,
                         string[]? steps = null, Click[]? clicks = null, bool arrows = false)
        {
            Kind = kind;
            Kicker = kicker;
            Headline = headline;
            Intro = intro;
            GoLabel = goLabel;
            Stage = stage;
            Command = command;
            Macro = macro;
            Steps = steps ?? new string[0];
            Clicks = clicks ?? new Click[0];
            ArrowsBetweenClicks = arrows;
        }

        public PageKind Kind { get; }

        /// <summary>Small capitals above the headline, e.g. "LEVEL 2 OF 6".</summary>
        public string Kicker { get; }

        public string Headline { get; }

        /// <summary>One short line.</summary>
        public string Intro { get; }

        /// <summary>Label of the main button.</summary>
        public string GoLabel { get; }

        /// <summary>The stage this page builds, for Level pages.</summary>
        public Stage? Stage { get; }

        /// <summary>The Rhino command this level teaches, as the player would type it.</summary>
        public string? Command { get; }

        /// <summary>
        /// Rhino macro the main button runs. "{profile}" and "{fin}" are replaced with the ids
        /// of the player's profile curve and fin, so the command starts with them selected.
        /// </summary>
        public string? Macro { get; }

        /// <summary>How to use the command, at most three short steps.</summary>
        public string[] Steps { get; }

        /// <summary>Points to click, shown in the Axon view.</summary>
        public Click[] Clicks { get; }

        /// <summary>Draw arrows from each click point to the next in the Axon view.</summary>
        public bool ArrowsBetweenClicks { get; }

        public const int LevelCount = 6;
        public const int WelcomeIndex = 0;
        public const int DoneIndex = LevelCount + 1;

        public static int IndexOf(Stage stage) => 1 + (int)stage;

        public static GamePage For(Stage stage) => All[IndexOf(stage)];

        private static Click[] ProfileClicks()
        {
            Point3d[] p = FinModel.Profile;
            return new[]
            {
                new Click("start", p[0], 1, 0.6),
                new Click("out", p[1], -1, 0.5),
                new Click("zig", p[2], -1, 0),
                new Click("zag", p[3], 1, 0),
                new Click("zig", p[4], -1, 0),
                new Click("out", p[5], -1, -0.5),
                new Click("back", p[6], 1, -0.6),
            };
        }

        public static readonly GamePage[] All =
        {
            new GamePage(
                PageKind.Welcome,
                "TUTORIAL",
                "The Fin Facade",
                "Six commands turn a box into a rippling facade. Watch, then press Start.",
                "Start"),

            new GamePage(
                PageKind.Level,
                "LEVEL 1 OF 6",
                "Build the mass.",
                "A box for the building behind the fins.",
                "Go",
                GotchiFins.Stage.Mass,
                "Box",
                "_Box",
                new[] { "Click corner 1", "Click corner 2", "Click the height" },
                new[]
                {
                    new Click("corner", FinModel.MassCornerA, -1, 0.5),
                    new Click("corner", FinModel.MassCornerB, 1, 0.3),
                    new Click("height", FinModel.MassTop, 1, -0.6),
                },
                arrows: true),

            new GamePage(
                PageKind.Level,
                "LEVEL 2 OF 6",
                "Draw the fin profile.",
                "One fin, seen from the side.",
                "Go",
                GotchiFins.Stage.Profile,
                "Polyline",
                "_Polyline",
                new[] { "Click points 1 to 7", "Click 1 again to close" },
                ProfileClicks(),
                arrows: true),

            new GamePage(
                PageKind.Level,
                "LEVEL 3 OF 6",
                "Give it thickness.",
                "Push the profile into a solid fin.",
                "Go",
                GotchiFins.Stage.Fin,
                "ExtrudeCrv",
                "_SelID {profile} _ExtrudeCrv _Solid=_Yes _BothSides=_No _Direction w0,0,0 w1,0,0",
                new[] { "Pick the profile", "Type " + FinModel.ThicknessText + ", press Enter" }),

            new GamePage(
                PageKind.Level,
                "LEVEL 4 OF 6",
                "Wrap the mass in fins.",
                "Copy the fin along the yellow path.",
                "Go",
                GotchiFins.Stage.Array,
                "ArrayCrv",
                "_SelID {fin} _ArrayCrv",
                new[] { "Pick the fin, then the yellow path", "Items " + FinModel.FinCount + ", Roadlike, Top", "Press OK" }),

            new GamePage(
                PageKind.Level,
                "LEVEL 5 OF 6",
                "Let the sun in.",
                "Fins come alive in shadow.",
                "Go",
                GotchiFins.Stage.Sun,
                "Sun",
                "_Sun",
                new[] { "Tick On in the Sun panel", "Right-drag the Axon view to orbit" }),

            new GamePage(
                PageKind.Level,
                "LEVEL 6 OF 6",
                "Dress the fins.",
                "Pick a finish, then scale its texture.",
                "Go",
                GotchiFins.Stage.Material,
                "ApplyBoxMapping",
                "{fins} _ApplyBoxMapping",
                new[] { "Pick a finish below", "ApplyBoxMapping on all the fins", "Box size 2 × 2 × 2, then Enter" }),

            new GamePage(
                PageKind.Done,
                "SOLVED",
                "Splendid.",
                "Box, Polyline, ExtrudeCrv, ArrayCrv, Sun, ApplyBoxMapping: six commands, one facade.",
                "Zoom"),
        };
    }
}
