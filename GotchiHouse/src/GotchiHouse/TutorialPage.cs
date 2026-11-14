using Rhino.Geometry;

namespace GotchiHouse
{
    /// <summary>One point the player clicks, and how it is labelled in the Axon view.</summary>
    public sealed class TutorialInput
    {
        public TutorialInput(string label, Point3d at, double labelX, double labelY)
        {
            Label = label;
            At = at;
            LabelX = labelX;
            LabelY = labelY;
        }

        /// <summary>What the click means, e.g. "first corner".</summary>
        public string Label { get; }

        /// <summary>Where to click. A snappable guide point is placed here.</summary>
        public Point3d At { get; }

        /// <summary>Screen direction from the point to its label (x right, y down), about -1..1.</summary>
        public double LabelX { get; }
        public double LabelY { get; }
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
                             HousePart? part = null, string? macro = null,
                             TutorialInput[]? inputs = null, (int From, int To)[]? arrows = null)
        {
            Kind = kind;
            Title = title;
            Intro = intro;
            GoLabel = goLabel;
            Part = part;
            Macro = macro;
            Inputs = inputs ?? new TutorialInput[0];
            Arrows = arrows ?? new (int, int)[0];
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

        /// <summary>The points to click, in order.</summary>
        public TutorialInput[] Inputs { get; }

        /// <summary>Guiding arrows in the Axon view, as pairs of input indices.</summary>
        public (int From, int To)[] Arrows { get; }

        public const int WelcomeIndex = 0;
        public const int WallsIndex = 1;
        public const int RoofIndex = 2;
        public const int DoneIndex = 3;

        public static TutorialPage ForPart(HousePart part) => All[part == HousePart.Walls ? WallsIndex : RoofIndex];

        public static readonly TutorialPage[] All =
        {
            new TutorialPage(
                PageKind.Welcome,
                "BUILD A HOUSE",
                "Watch the demo: a box for the walls, then a pyramid for the roof. Press START to try it.",
                "START"),

            new TutorialPage(
                PageKind.Build,
                "1 · WALLS",
                "Press GO, then click the glowing points in the Axon view, in order:",
                "GO",
                HousePart.Walls,
                "_Box",
                new[]
                {
                    new TutorialInput("first corner", new Point3d(0, 0, 0), -1, 0.4),
                    new TutorialInput("opposite corner", new Point3d(10, 10, 0), 1, 0.4),
                    new TutorialInput("height", new Point3d(10, 10, 8), 1, -0.6),
                },
                new[] { (0, 1), (1, 2) }),

            new TutorialPage(
                PageKind.Build,
                "2 · ROOF",
                "Press GO, then click the glowing points in the Axon view, in order:",
                "GO",
                HousePart.Roof,
                "_Pyramid _NumSides=4",
                new[]
                {
                    new TutorialInput("middle of roof base", new Point3d(5, 5, 8), 1, 0.9),
                    new TutorialInput("corner, past the wall", new Point3d(-1, -1, 8), -1, 0.2),
                    new TutorialInput("top of the roof", new Point3d(5, 5, 16), 1, -0.5),
                },
                new[] { (0, 1), (0, 2) }),

            new TutorialPage(
                PageKind.Done,
                "HOME SWEET HOME!",
                "You built the whole house. Great job! Bonus: BooleanUnion joins the walls and roof into one solid.",
                "ZOOM"),
        };
    }
}
