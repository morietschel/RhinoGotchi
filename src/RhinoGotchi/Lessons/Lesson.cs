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

        /// <summary>One line under the title on the level map, e.g. "Line, Polyline, Circle".</summary>
        public string Subtitle { get; set; } = "";

        public List<LessonStep> Steps { get; } = new List<LessonStep>();

        /// <summary>A project lesson (e.g. the Klosterneuburg tutorials): left out of the placement test.</summary>
        public bool IsProject { get; set; }
    }

    /// <summary>One card in a lesson.</summary>
    public abstract class LessonStep
    {
        /// <summary>XP for getting it right first time.</summary>
        public int Xp { get; set; } = 10;

        /// <summary>Placement test only: the level (index in <see cref="LessonCatalog.All"/>) this question checks.</summary>
        public int Tier { get; set; }
    }

    /// <summary>How a quiz card asks its question, easiest first.</summary>
    public enum QuizKind
    {
        /// <summary>"Which one of these is Sweep2?" Four icons.</summary>
        PickIcon,

        /// <summary>One big icon: "What is this command called?" Four look-alike names.</summary>
        NameIt,

        /// <summary>A modelling task in words: "Close the open ends of this tube." Four icons, no names.</summary>
        Scenario,
    }

    /// <summary>
    /// A four-option quiz about one command. The three wrong options come from
    /// <see cref="Distractors"/>, picked at random. <see cref="Kind"/> sets how hard it is.
    /// </summary>
    public class IconQuizStep : LessonStep
    {
        /// <summary>The command being asked about. Must have an icon in <see cref="CommandInfo"/>.</summary>
        public string Command { get; set; } = "";

        public QuizKind Kind { get; set; } = QuizKind.PickIcon;

        /// <summary>Scenario only: the task the player has to pick a command for.</summary>
        public string Prompt { get; set; } = "";

        /// <summary>Commands that look or work alike: the best wrong answers. Needs at least three.</summary>
        public List<string> Distractors { get; } = new List<string>();
    }

    /// <summary>Plays a <see cref="Level"/> in the viewport. Done when its evaluator says solved.</summary>
    public class BuildStep : LessonStep
    {
        public BuildStep(Func<Level> createLevel)
        {
            CreateLevel = createLevel;
            Xp = 20;
        }

        /// <summary>The level, for showing the card before it starts (and for starting it when <see cref="Start"/> is null).</summary>
        public Func<Level> CreateLevel { get; }

        /// <summary>
        /// Starts the level itself, when it needs more than <see cref="LevelRunner.Start(Rhino.RhinoDoc, Level)"/>
        /// (a tutorial card sets up its layers and input first). Null: the session starts <see cref="CreateLevel"/>.
        /// </summary>
        public Func<Rhino.RhinoDoc, LevelRunner?>? Start { get; protected set; }
    }

    /// <summary>
    /// A Klosterneuburg tutorial card in a lesson: played like FinGotchi, with a Run button, at most
    /// three steps, yellow click marks and a live check list. Done when every check passes.
    /// </summary>
    public sealed class TutorialBuildStep : BuildStep
    {
        public TutorialBuildStep(Tutorials.Tutorial tutorial)
            : base(() => Tutorials.TutorialLevels.PreviewLevel(tutorial))
        {
            Tutorial = tutorial;
            Xp = tutorial.IsChallenge ? 30 : 20;
            Start = doc => Tutorials.TutorialLevels.Start(doc, tutorial);
        }

        public Tutorials.Tutorial Tutorial { get; }
    }
}
