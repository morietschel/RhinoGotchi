using Rhino.Geometry;

namespace GotchiHouse
{
    /// <summary>One value the player types, and where it lands in the model.</summary>
    public sealed class TutorialInput
    {
        public TutorialInput(string value, string hint, Point3d at)
        {
            Value = value;
            Hint = hint;
            At = at;
        }

        /// <summary>What to type, e.g. "0,0,0".</summary>
        public string Value { get; }

        /// <summary>What Rhino is asking for, e.g. "first corner".</summary>
        public string Hint { get; }

        /// <summary>Where the value lands; the viewport shows a dot there.</summary>
        public Point3d At { get; }
    }

    public enum PageKind
    {
        Welcome,
        Build,
        Done,
    }

    /// <summary>One screen of the tutorial.</summary>
    public sealed class TutorialPage
    {
        private TutorialPage(PageKind kind, string title, string intro, string goLabel,
                             HousePart? part = null, string? macro = null, TutorialInput[]? inputs = null)
        {
            Kind = kind;
            Title = title;
            Intro = intro;
            GoLabel = goLabel;
            Part = part;
            Macro = macro;
            Inputs = inputs ?? new TutorialInput[0];
        }

        public PageKind Kind { get; }
        public string Title { get; }
        public string Intro { get; }

        /// <summary>Label under the middle button.</summary>
        public string GoLabel { get; }

        /// <summary>The part this page builds, for Build pages.</summary>
        public HousePart? Part { get; }

        /// <summary>Rhino macro the GO button runs, for Build pages.</summary>
        public string? Macro { get; }

        public TutorialInput[] Inputs { get; }

        public const int WelcomeIndex = 0;
        public const int WallsIndex = 1;
        public const int RoofIndex = 2;
        public const int DoneIndex = 3;

        public static readonly TutorialPage[] All =
        {
            new TutorialPage(
                PageKind.Welcome,
                "BUILD A HOUSE",
                "One box for the walls. One pyramid for the roof. Press START to set up the axon view.",
                "START"),

            new TutorialPage(
                PageKind.Build,
                "1 · WALLS",
                "Press GO to start Box. Type each value, then Enter:",
                "GO",
                HousePart.Walls,
                "_Box",
                new[]
                {
                    new TutorialInput("0,0,0", "first corner", new Point3d(0, 0, 0)),
                    new TutorialInput("10,10", "other corner", new Point3d(10, 10, 0)),
                    new TutorialInput("8", "height", new Point3d(10, 10, 8)),
                }),

            new TutorialPage(
                PageKind.Build,
                "2 · ROOF",
                "Press GO to start a 4-sided Pyramid. Type each value, then Enter:",
                "GO",
                HousePart.Roof,
                "_Pyramid _NumSides=4",
                new[]
                {
                    new TutorialInput("5,5,8", "middle of base", new Point3d(5, 5, 8)),
                    new TutorialInput("-1,-1,8", "corner", new Point3d(-1, -1, 8)),
                    new TutorialInput("8", "height", new Point3d(5, 5, 16)),
                }),

            new TutorialPage(
                PageKind.Done,
                "HOME SWEET HOME!",
                "You built the whole house. Great job! Bonus: BooleanUnion joins the walls and roof into one solid.",
                "ZOOM"),
        };
    }
}
