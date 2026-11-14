namespace HouseTutor
{
    /// <summary>One page of the tutorial.</summary>
    public sealed class TutorialStep
    {
        public TutorialStep(string title, string body, string? buttonText = null, string? macro = null)
        {
            Title = title;
            Body = body;
            ButtonText = buttonText;
            Macro = macro;
        }

        public string Title { get; }

        /// <summary>The instructions. Line breaks are kept.</summary>
        public string Body { get; }

        /// <summary>Label of the action button, or null for no button.</summary>
        public string? ButtonText { get; }

        /// <summary>Rhino command macro the button runs, e.g. "_Box".</summary>
        public string? Macro { get; }

        /// <summary>The house tutorial: walls from a box, roof from a pyramid.</summary>
        public static TutorialStep[] House { get; } =
        {
            new TutorialStep(
                "Build a simple house",
                "You'll make a little house from two shapes:\n\n" +
                "  • a box for the walls\n" +
                "  • a pyramid for the roof\n\n" +
                "Each step tells you exactly what to type. Type the numbers in Rhino's " +
                "command line and press Enter after each one.\n\n" +
                "Tip: start in an empty file so the house is easy to see.\n\n" +
                "Press Next to begin."),

            new TutorialStep(
                "Step 1: Walls (Box)",
                "Click Start Box below, then answer Rhino's questions:\n\n" +
                "1. \"First corner of base\"\n     type  0,0,0  and press Enter\n\n" +
                "2. \"Other corner of base\"\n     type  10,10  and press Enter\n\n" +
                "3. \"Height\"\n     type  8  and press Enter\n\n" +
                "You now have a 10 × 10 × 8 box. Its top is at height 8, which is " +
                "where the roof will sit.",
                "Start Box",
                "_Box"),

            new TutorialStep(
                "Step 2: Roof (Pyramid)",
                "Click Start Pyramid below. It starts the Pyramid command with 4 sides, " +
                "so the base is a square.\n\n" +
                "1. \"Middle of pyramid base\"\n     type  5,5,8  and press Enter\n" +
                "     (the centre of the top of the walls)\n\n" +
                "2. \"Corner of pyramid\"\n     type  -1,-1,8  and press Enter\n" +
                "     (1 unit past the wall corner, so the roof overhangs)\n\n" +
                "3. \"End of pyramid\" (the height)\n     type  6  and press Enter\n\n" +
                "Doing it by hand? Run Pyramid and set NumSides to 4 first.",
                "Start Pyramid",
                "_Pyramid _NumSides=4"),

            new TutorialStep(
                "Step 3: Join them (optional)",
                "The box and pyramid are still two separate objects. To make one solid:\n\n" +
                "1. Click Start BooleanUnion below\n" +
                "2. Click the box, then the pyramid\n" +
                "3. Press Enter\n\n" +
                "Skip this step if you'd rather keep the walls and roof separate, " +
                "for example to give them different colours.",
                "Start BooleanUnion",
                "_BooleanUnion"),

            new TutorialStep(
                "Done!",
                "Your house is finished.\n\n" +
                "Click Show my house to zoom in and see it shaded.\n\n" +
                "Try it again with your own numbers:\n" +
                "  • a taller box makes a two-storey house\n" +
                "  • a bigger corner offset makes a wider roof\n" +
                "  • a larger height makes a steeper roof",
                "Show my house",
                "_Zoom _All _Extents _SetDisplayMode _Mode=Shaded"),
        };
    }
}
