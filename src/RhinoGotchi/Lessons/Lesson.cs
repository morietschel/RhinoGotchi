using System;
using System.Collections.Generic;
using RhinoGotchi.Levels;

namespace RhinoGotchi.Lessons
{
    /// <summary>A short run of steps, played in order in the RhinoGotchi panel.</summary>
    public sealed class Lesson
    {
        public string Id { get; set; } = "";

        public string Title { get; set; } = "";

        public List<LessonStep> Steps { get; } = new List<LessonStep>();
    }

    /// <summary>One card in a lesson.</summary>
    public abstract class LessonStep
    {
        /// <summary>XP for getting it right first time.</summary>
        public int Xp { get; set; } = 10;
    }

    /// <summary>
    /// "Which one of these is Sweep2?" The player picks one of four icons.
    /// The three wrong options come from <see cref="Distractors"/>, picked at random.
    /// </summary>
    public class IconQuizStep : LessonStep
    {
        /// <summary>The command being asked about. Must have an icon in <see cref="CommandInfo"/>.</summary>
        public string Command { get; set; } = "";

        /// <summary>Commands that look or work alike: the best wrong answers. Needs at least three.</summary>
        public List<string> Distractors { get; } = new List<string>();
    }

    /// <summary>Plays a <see cref="Level"/> in the viewport. Done when its evaluator says solved.</summary>
    public sealed class BuildStep : LessonStep
    {
        public BuildStep(Func<Level> createLevel)
        {
            CreateLevel = createLevel;
            Xp = 20;
        }

        public Func<Level> CreateLevel { get; }
    }
}
