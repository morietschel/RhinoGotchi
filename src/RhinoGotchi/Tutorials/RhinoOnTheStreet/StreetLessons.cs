using System.Collections.Generic;
using System.Drawing;
using Rhino.Geometry;
using static RhinoGotchi.Tutorials.RhinoOnTheStreet.StreetScene;

namespace RhinoGotchi.Tutorials.RhinoOnTheStreet
{
    internal enum StepKind
    {
        /// <summary>Wait for the player to start a Rhino command.</summary>
        Command,
        /// <summary>Wait for a click on the target point (inside the running command).</summary>
        Point,
        /// <summary>Wait for Enter / right-click that ends the running command.</summary>
        Finish,
        /// <summary>Wait for Enter on an empty command line, which repeats the last command.</summary>
        Repeat,
    }

    /// <summary>Step colour group. Red first, green next, blue last (same as Rhino's X, Y, Z axes).</summary>
    internal enum StepGroup { Red, Green, Blue }

    internal sealed class Step
    {
        public StepKind Kind;
        public StepGroup Group;
        public string Command = "";
        public Point3d Target = Point3d.Unset;
        public string Label = "";
        public string Say = "";
        public string Hint = "";
    }

    internal sealed class Lesson
    {
        public int Number;
        public string Name = "";
        public string Title = "";
        public string Description = "";
        public string[] GroupTitles = new string[3];
        public string[] GroupHints = new string[3];
        public List<Step> Steps = new List<Step>();
        public Point3d[] Targets = new Point3d[0];
    }

    internal static class StreetLessons
    {
        public static readonly Color Red = Color.FromArgb(224, 49, 49);
        public static readonly Color Green = Color.FromArgb(47, 158, 68);
        public static readonly Color Blue = Color.FromArgb(28, 126, 214);
        public static readonly Color Orange = Color.FromArgb(232, 89, 12);
        public static readonly Color Ink = Color.FromArgb(18, 18, 18);

        public static Color ColorOf(StepGroup g) => g == StepGroup.Red ? Red : g == StepGroup.Green ? Green : Blue;
        public static string NameOf(StepGroup g) => g == StepGroup.Red ? "RED" : g == StepGroup.Green ? "GREEN" : "BLUE";

        public static Lesson Curve()
        {
            Point3d[] c = StreetControlPoints;
            var l = new Lesson
            {
                Number = 1, Name = "Curve", Title = "Draw the street",
                Description = "A Curve is shaped by control points. They pull on it like magnets.",
                GroupTitles = new[] { "Start", "Shape", "Finish" },
                GroupHints = new[] { "Type Curve, Enter, click the start", "Click control points 2, 3 and 4", "Click the end at the arch, then Enter" },
                Targets = c,
            };
            l.Steps.Add(new Step { Kind = StepKind.Command, Group = StepGroup.Red, Command = "Curve", Say = "Type  Curve  in the command line and press Enter.", Hint = "Watch the coloured rings: red, then green, then blue." });
            l.Steps.Add(Pt(StepGroup.Red, c[0], "Start point", "Click the RED ring - the street starts at your feet.", "Osnap Point is on, so the cursor locks onto the ring."));
            l.Steps.Add(Pt(StepGroup.Green, c[1], "Control point 2", "Click GREEN point 2. Watch the curve bend toward it.", "Control points sit off the curve and pull it."));
            l.Steps.Add(Pt(StepGroup.Green, c[2], "Control point 3", "Click GREEN point 3 to swing the street back.", ""));
            l.Steps.Add(Pt(StepGroup.Green, c[3], "Control point 4", "Click GREEN point 4 to straighten up toward the arch.", ""));
            l.Steps.Add(Pt(StepGroup.Blue, c[4], "End point", "Click the BLUE end point under the archway.", "End points are the only control points the curve touches."));
            l.Steps.Add(new Step { Kind = StepKind.Finish, Group = StepGroup.Blue, Command = "Curve", Say = "Press Enter (or right-click) to finish the curve.", Hint = "In Rhino, Enter and right-click both mean \"done\"." });
            return l;
        }

        public static Lesson Arch()
        {
            var l = new Lesson
            {
                Number = 2, Name = "Arch", Title = "Build the archway",
                Description = "Two Line pillars and an Arc on top make an archway.",
                GroupTitles = new[] { "Left pillar", "Right pillar", "Arch top" },
                GroupHints = new[] { "Line, Enter: base, then top", "Enter repeats Line: base, then top", "Arc, Enter, S, Enter: start, end, crown" },
                Targets = new[] { LeftBase, LeftTop, RightBase, RightTop, Crown },
            };
            l.Steps.Add(new Step { Kind = StepKind.Command, Group = StepGroup.Red, Command = "Line", Say = "Type  Line  and press Enter.", Hint = "A Line needs two clicks: start and end." });
            l.Steps.Add(Pt(StepGroup.Red, LeftBase, "Pillar base", "Click the RED base of the left pillar.", ""));
            l.Steps.Add(Pt(StepGroup.Red, LeftTop, "Pillar top", "Go straight up and click the RED top point.", "Line ends by itself after the second click."));
            l.Steps.Add(new Step { Kind = StepKind.Repeat, Group = StepGroup.Green, Command = "Line", Say = "Press Enter on the empty command line to repeat Line.", Hint = "Rhino pro tip: Enter repeats the last command." });
            l.Steps.Add(Pt(StepGroup.Green, RightBase, "Pillar base", "Click the GREEN base of the right pillar.", ""));
            l.Steps.Add(Pt(StepGroup.Green, RightTop, "Pillar top", "Straight up again - click the GREEN top.", ""));
            l.Steps.Add(new Step { Kind = StepKind.Command, Group = StepGroup.Blue, Command = "Arc", Say = "Type  Arc  and press Enter.", Hint = "Arc first asks for a centre. We'll switch to the Start option next." });
            l.Steps.Add(Pt(StepGroup.Blue, LeftTop, "Start of arc", "Type  S  and Enter (Start option), then click the LEFT pillar top.", "Start option: start point, end point, then a point on the arc."));
            l.Steps.Add(Pt(StepGroup.Blue, RightTop, "End of arc", "Click the RIGHT pillar top - the end of the arc.", ""));
            l.Steps.Add(Pt(StepGroup.Blue, Crown, "Point on arc", "Pull up and click the BLUE crown to set the arc's height.", "Three points define one arc."));
            return l;
        }

        private static Step Pt(StepGroup g, Point3d target, string label, string say, string hint) =>
            new Step { Kind = StepKind.Point, Group = g, Target = target, Label = label, Say = say, Hint = hint };
    }
}
