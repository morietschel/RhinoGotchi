using System.Collections.Generic;
using RhinoGotchi.Levels.Samples;

namespace RhinoGotchi.Lessons
{
    /// <summary>All lessons. The panel plays the first one.</summary>
    public static class LessonCatalog
    {
        public static Lesson SurfacesOne()
        {
            var lesson = new Lesson { Id = "surfaces-01", Title = "Solids & Surfaces 1" };
            lesson.Steps.Add(Quiz("Box", "Cylinder", "Sphere", "Cone", "ExtrudeCrv"));
            lesson.Steps.Add(new BuildStep(TwoPointBoxLevel.Create));
            lesson.Steps.Add(Quiz("Sweep2", "Sweep1", "Loft", "NetworkSrf", "Pipe"));
            lesson.Steps.Add(Quiz("Loft", "Sweep2", "ExtrudeCrv", "NetworkSrf"));
            lesson.Steps.Add(Quiz("Revolve", "Sweep1", "Pipe", "Cylinder", "Loft"));
            lesson.Steps.Add(Quiz("Sweep1", "Sweep2", "Pipe", "ExtrudeCrv"));
            lesson.Steps.Add(Quiz("BooleanUnion", "BooleanDifference", "FilletEdge", "Sphere"));
            lesson.Steps.Add(Quiz("FilletEdge", "Box", "BooleanDifference", "Cone"));
            return lesson;
        }

        public static IReadOnlyList<Lesson> All => new[] { SurfacesOne() };

        private static IconQuizStep Quiz(string command, params string[] distractors)
        {
            var step = new IconQuizStep { Command = command };
            step.Distractors.AddRange(distractors);
            return step;
        }
    }
}
