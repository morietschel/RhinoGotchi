using System.Collections.Generic;
using System.Linq;
using RhinoGotchi.Levels.Samples;

namespace RhinoGotchi.Lessons
{
    /// <summary>
    /// The level ladder, easiest first. Each level is one lesson. The placement test
    /// asks the first two questions from each level in this order,
    /// so it gets harder as it goes: pick-the-icon, then name-it, then scenarios.
    /// </summary>
    public static class LessonCatalog
    {
        // Look-alike commands for the quiz card before each tutorial (commands with a quiz icon only).
        private static readonly Dictionary<string, string[]> LookAlikes = new Dictionary<string, string[]>
        {
            ["BooleanDifference"] = new[] { "BooleanUnion", "BooleanIntersection", "Trim" },
            ["Pipe"] = new[] { "Sweep1", "Cylinder", "Revolve" },
            ["OffsetSrf"] = new[] { "Offset", "ExtrudeSrf", "Shell" },
            ["ArrayPolar"] = new[] { "Rotate", "Mirror", "Move" },
            ["Contour"] = new[] { "Trim", "OffsetSrf", "Patch" },
            ["Sweep1"] = new[] { "Sweep2", "Pipe", "ExtrudeCrv" },
            ["Sweep2"] = new[] { "Sweep1", "Loft", "NetworkSrf" },
            ["Loft"] = new[] { "Sweep2", "NetworkSrf", "ExtrudeCrv" },
            ["NetworkSrf"] = new[] { "Loft", "Patch", "Sweep2" },
        };

        public static readonly IReadOnlyList<Lesson> All = new[]
        {
            Curves(),
            Solids(),
            Surfaces(),
            Sweeps(),
            EditingSolids(),
            CurveTools(),
            Transforms(),
            SurfaceTools(),
            AdvancedSolids(),
            Mastery(),
        };

        public static int IndexOf(string lessonId)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i].Id == lessonId)
                    return i;
            return -1;
        }

        private static Lesson Curves()
        {
            var lesson = new Lesson { Id = "curves", Title = "Curves", Subtitle = "Line, Polyline, Circle, Rectangle" };
            lesson.Steps.Add(Quiz("Line", "Polyline", "Rectangle", "Circle"));
            lesson.Steps.Add(Quiz("Circle", "Rectangle", "Sphere", "Polyline"));
            lesson.Steps.Add(Quiz("Polyline", "Line", "Rectangle", "NetworkSrf"));
            lesson.Steps.Add(Quiz("Rectangle", "Polyline", "Box", "Line"));
            return lesson;
        }

        private static Lesson Solids()
        {
            var lesson = new Lesson { Id = "solids", Title = "Solids", Subtitle = "Box, Sphere, Cylinder, Cone" };
            lesson.Steps.Add(Quiz("Box", "Cylinder", "Sphere", "Cone", "ExtrudeCrv"));
            lesson.Steps.Add(Quiz("Cylinder", "Cone", "Pipe", "Box"));
            lesson.Steps.Add(new BuildStep(TwoPointBoxLevel.Create));
            lesson.Steps.Add(Quiz("Sphere", "Circle", "Cylinder", "BooleanUnion"));
            lesson.Steps.Add(Quiz("Cone", "Cylinder", "Revolve", "Box"));
            return lesson;
        }

        // From here on, the first two questions of each level (the ones the placement
        // test asks) get harder: Name-it from level 3, Scenario from level 5.

        private static Lesson Surfaces()
        {
            var lesson = new Lesson { Id = "surfaces", Title = "Surfaces", Subtitle = "ExtrudeCrv, Revolve, Loft, NetworkSrf" };
            lesson.Steps.Add(Quiz("Loft", "Sweep2", "ExtrudeCrv", "NetworkSrf"));
            lesson.Steps.Add(Name("Revolve", "Sweep1", "Pipe", "Loft"));
            lesson.Steps.Add(Quiz("ExtrudeCrv", "Loft", "Sweep1", "Box"));
            lesson.Steps.Add(Quiz("NetworkSrf", "Loft", "Sweep2", "ExtrudeCrv"));
            return lesson;
        }

        private static Lesson Sweeps()
        {
            var lesson = new Lesson { Id = "sweeps", Title = "Sweeps & Pipes", Subtitle = "Sweep1, Sweep2, Pipe" };
            lesson.Steps.Add(Name("Sweep2", "Sweep1", "Loft", "NetworkSrf"));
            lesson.Steps.Add(Name("Sweep1", "Sweep2", "Pipe", "ExtrudeCrv"));
            lesson.Steps.Add(Quiz("Pipe", "Sweep1", "Cylinder", "Revolve"));
            lesson.Steps.Add(Task("Sweep2", "You have two edge curves and one cross-section. Which command makes a surface that follows BOTH edges?", "Sweep1", "Loft", "NetworkSrf"));
            return lesson;
        }

        private static Lesson EditingSolids()
        {
            var lesson = new Lesson { Id = "editing-solids", Title = "Editing Solids", Subtitle = "BooleanUnion, BooleanDifference, FilletEdge" };
            lesson.Steps.Add(Name("BooleanDifference", "BooleanUnion", "BooleanIntersection", "Trim"));
            lesson.Steps.Add(Task("FilletEdge", "A box has sharp edges that would hurt to hold. Which command rounds them over?", "Fillet", "Shell", "BooleanDifference"));
            lesson.Steps.Add(Quiz("BooleanUnion", "BooleanDifference", "BooleanIntersection", "Sphere"));
            lesson.Steps.Add(Name("FilletEdge", "Fillet", "BlendSrf", "Cap"));
            return lesson;
        }

        private static Lesson CurveTools()
        {
            var lesson = new Lesson { Id = "curve-tools", Title = "Curve Tools", Subtitle = "Offset, Trim, Join, Fillet" };
            lesson.Steps.Add(Name("Offset", "OffsetSrf", "Move", "Scale"));
            lesson.Steps.Add(Task("Trim", "A curve sticks out past a cutting line. Which command removes the extra piece?", "Join", "Offset", "BooleanDifference"));
            lesson.Steps.Add(Quiz("Join", "Trim", "Polyline", "Fillet"));
            lesson.Steps.Add(Name("Fillet", "FilletEdge", "BlendSrf", "Trim"));
            lesson.Steps.Add(Task("Offset", "Draw a wall outline 20 mm inside a room outline, following every bend.", "Scale", "Move", "OffsetSrf"));
            return lesson;
        }

        private static Lesson Transforms()
        {
            var lesson = new Lesson { Id = "transforms", Title = "Transforms", Subtitle = "Move, Rotate, Scale, Mirror, ArrayPolar" };
            lesson.Steps.Add(Task("ArrayPolar", "Place 8 chairs evenly around a round table.", "Rotate", "Mirror", "Move"));
            lesson.Steps.Add(Name("Rotate", "ArrayPolar", "Revolve", "Scale"));
            lesson.Steps.Add(Quiz("Mirror", "Rotate", "Move", "Offset"));
            lesson.Steps.Add(Quiz("Scale", "Offset", "Move", "Rectangle"));
            lesson.Steps.Add(Task("Mirror", "You modeled the left half of a symmetric phone case. How do you get the right half?", "Rotate", "ArrayPolar", "Offset"));
            return lesson;
        }

        private static Lesson SurfaceTools()
        {
            var lesson = new Lesson { Id = "surface-tools", Title = "Surface Tools", Subtitle = "OffsetSrf, BlendSrf, Patch, Contour" };
            lesson.Steps.Add(Task("BlendSrf", "Two surfaces don't touch. You need a smooth, curvature-continuous bridge between their edges.", "Loft", "Patch", "NetworkSrf"));
            lesson.Steps.Add(Name("OffsetSrf", "Offset", "ExtrudeSrf", "Shell"));
            lesson.Steps.Add(Name("Patch", "NetworkSrf", "Cap", "Loft"));
            lesson.Steps.Add(Task("Contour", "You want slice lines every 5 mm through a model, for laser-cut layers.", "Trim", "OffsetSrf", "Patch"));
            return lesson;
        }

        private static Lesson AdvancedSolids()
        {
            var lesson = new Lesson { Id = "advanced-solids", Title = "Advanced Solids", Subtitle = "Cap, Shell, ExtrudeSrf, BooleanIntersection" };
            lesson.Steps.Add(Task("Shell", "Turn a solid box into a hollow container with 2 mm walls and an open top.", "OffsetSrf", "BooleanDifference", "Cap"));
            lesson.Steps.Add(Name("BooleanIntersection", "BooleanUnion", "BooleanDifference", "Trim"));
            lesson.Steps.Add(Task("Cap", "Your extruded tube is open at both ends. Which command closes it into a solid?", "Patch", "Shell", "Join"));
            lesson.Steps.Add(Name("ExtrudeSrf", "ExtrudeCrv", "OffsetSrf", "Cap"));
            return lesson;
        }

        private static Lesson Mastery()
        {
            var lesson = new Lesson { Id = "mastery", Title = "Mastery", Subtitle = "Pick the right tool for real modeling tasks" };
            lesson.Steps.Add(Task("Patch", "Fill a closed, wavy 3D boundary curve with a single surface.", "NetworkSrf", "Cap", "Loft"));
            lesson.Steps.Add(Task("BooleanIntersection", "Keep ONLY the volume where a sphere and a box overlap.", "BooleanDifference", "BooleanUnion", "Trim"));
            lesson.Steps.Add(Task("Revolve", "Make a wine glass from a single profile curve.", "Loft", "Sweep1", "Pipe"));
            lesson.Steps.Add(Task("Pipe", "Turn a curvy wire path into a round cable 3 mm thick.", "Sweep1", "ExtrudeCrv", "OffsetSrf"));
            lesson.Steps.Add(Task("Loft", "Make a boat hull through five cross-section curves.", "Sweep2", "NetworkSrf", "Patch"));
            lesson.Steps.Add(Task("ExtrudeSrf", "Turn a flat, closed floor-plan surface into a 3 m tall solid block.", "ExtrudeCrv", "OffsetSrf", "Cap"));
            return lesson;
        }

        /// <summary>
        /// One Klosterneuburg tutorial as a short lesson, played from the Klosterneuburg Tutorials page
        /// (not on the campaign map): a quiz card on its command (when the command has a quiz icon),
        /// the core card, then the challenge card unless <paramref name="coreOnly"/>.
        /// </summary>
        public static Lesson TutorialLesson(Tutorials.Tutorial core, bool coreOnly = false)
        {
            var lesson = new Lesson
            {
                Id = $"klosterneuburg-{core.Number:00}",
                Title = $"Klosterneuburg {core.Number}: {core.Title}",
                Subtitle = core.MainCommand,
                IsProject = true,
            };
            if (LookAlikes.TryGetValue(core.MainCommand, out string[]? alike))
                lesson.Steps.Add(Quiz(core.MainCommand, alike));
            lesson.Steps.Add(new TutorialBuildStep(core));
            if (!coreOnly && Tutorials.TutorialCatalog.ChallengeOf(core) is Tutorials.Tutorial challenge)
                lesson.Steps.Add(new TutorialBuildStep(challenge));
            return lesson;
        }

        /// <summary>Just one card (e.g. a challenge replayed on its own) as a lesson.</summary>
        public static Lesson CardLesson(Tutorials.Tutorial card)
        {
            var lesson = new Lesson
            {
                Id = card.Id,
                Title = $"Klosterneuburg {card.Number}: {card.Title}",
                Subtitle = card.MainCommand,
                IsProject = true,
            };
            lesson.Steps.Add(new TutorialBuildStep(card));
            return lesson;
        }

        /// <summary>
        /// The placement test: the first two quiz questions of every level, easiest
        /// level first. Each question's <see cref="LessonStep.Tier"/> is its level.
        /// </summary>
        public static Lesson PlacementTest()
        {
            var test = new Lesson { Id = "placement", Title = "Placement test" };
            for (int tier = 0; tier < All.Count; tier++)
            {
                if (All[tier].IsProject)
                    continue;
                foreach (IconQuizStep q in All[tier].Steps.OfType<IconQuizStep>().Take(2))
                {
                    var copy = new IconQuizStep { Command = q.Command, Kind = q.Kind, Prompt = q.Prompt, Tier = tier, Xp = 0 };
                    copy.Distractors.AddRange(q.Distractors);
                    test.Steps.Add(copy);
                }
            }
            return test;
        }

        /// <summary>"Which one of these is X?" Pick the icon.</summary>
        private static IconQuizStep Quiz(string command, params string[] distractors) =>
            Make(QuizKind.PickIcon, command, "", 10, distractors);

        /// <summary>One icon: pick its name from look-alike names.</summary>
        private static IconQuizStep Name(string command, params string[] distractors) =>
            Make(QuizKind.NameIt, command, "", 15, distractors);

        /// <summary>A modelling task: pick the icon of the command that does it.</summary>
        private static IconQuizStep Task(string command, string prompt, params string[] distractors) =>
            Make(QuizKind.Scenario, command, prompt, 20, distractors);

        private static IconQuizStep Make(QuizKind kind, string command, string prompt, int xp, string[] distractors)
        {
            var step = new IconQuizStep { Command = command, Kind = kind, Prompt = prompt, Xp = xp };
            step.Distractors.AddRange(distractors);
            return step;
        }
    }
}
