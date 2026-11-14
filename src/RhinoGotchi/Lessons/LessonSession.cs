using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using RhinoGotchi.Levels;

namespace RhinoGotchi.Lessons
{
    public enum SessionScreen
    {
        Intro,
        Quiz,
        Build,
        Complete,
        OutOfHearts,
    }

    /// <summary>
    /// Plays one lesson: hearts, XP, progress, and the current card. Knows nothing
    /// about the UI; the panel draws whatever this says and calls its methods.
    /// Like Duolingo, a quiz you get wrong comes back at the end of the lesson.
    /// </summary>
    public sealed class LessonSession
    {
        public const int MaxHearts = 5;

        private readonly Random _random = new Random();
        private readonly List<LessonStep> _queue = new List<LessonStep>();
        private int _queueIndex;
        private LevelRunner? _runner;

        public LessonSession(Lesson lesson, bool isPlacement = false)
        {
            Lesson = lesson;
            IsPlacement = isPlacement;
            Restart();
            LevelRunner.StateChanged += OnLevelChanged;
        }

        /// <summary>Raised whenever anything the panel draws has changed.</summary>
        public event Action? Changed;

        public Lesson Lesson { get; }

        /// <summary>
        /// Placement test: no hearts, no repeats, and it ends at the first wrong answer.
        /// The level of that question is where the player is placed.
        /// </summary>
        public bool IsPlacement { get; }

        /// <summary>Placement only: the level the player lands on (all levels count when nothing was missed).</summary>
        public int PlacedLevel => _firstMissTier ?? LessonCatalog.All.Count;

        private int? _firstMissTier;

        public SessionScreen Screen { get; private set; }

        public int Hearts { get; private set; }

        public int Xp { get; private set; }

        /// <summary>Quizzes answered right on the first try / quizzes asked.</summary>
        public int FirstTryCorrect { get; private set; }

        public int QuizzesAsked { get; private set; }

        /// <summary>0 to 1 for the progress bar.</summary>
        public double Progress => _queue.Count == 0 ? 0 : (double)_queueIndex / _queue.Count;

        public LessonStep? Step => _queueIndex < _queue.Count ? _queue[_queueIndex] : null;

        // ---- quiz card ----

        /// <summary>The four commands on the current quiz card, in display order.</summary>
        public IReadOnlyList<string> Options { get; private set; } = Array.Empty<string>();

        public int SelectedIndex { get; private set; } = -1;

        /// <summary>True once Check was pressed on this card.</summary>
        public bool Checked { get; private set; }

        public bool AnsweredCorrectly { get; private set; }

        public int CorrectIndex => Step is IconQuizStep q ? IndexOf(Options, q.Command) : -1;

        /// <summary>True when the current card is a quiz the player already missed once.</summary>
        public bool IsRetry { get; private set; }

        // ---- build card ----

        public bool BuildRunning => _runner != null && LevelRunner.Current == _runner;

        public bool BuildSolved { get; private set; }

        public LevelResult? BuildResult { get; private set; }

        public Level? BuildLevel { get; private set; }

        // ---- actions ----

        public void Restart()
        {
            StopBuild();
            _queue.Clear();
            _queue.AddRange(Lesson.Steps);
            _queueIndex = 0;
            Hearts = MaxHearts;
            Xp = 0;
            FirstTryCorrect = 0;
            QuizzesAsked = 0;
            _firstMissTier = null;
            Screen = SessionScreen.Intro;
            ResetCard();
            Raise();
        }

        public void Begin()
        {
            _queueIndex = 0;
            EnterStep();
            Raise();
        }

        public void Select(int index)
        {
            if (Screen != SessionScreen.Quiz || Checked || index < 0 || index >= Options.Count)
                return;
            SelectedIndex = index;
            Raise();
        }

        public void Check()
        {
            if (Screen != SessionScreen.Quiz || Checked || SelectedIndex < 0 || !(Step is IconQuizStep quiz))
                return;

            Checked = true;
            AnsweredCorrectly = SelectedIndex == CorrectIndex;
            if (!IsRetry)
                QuizzesAsked++;

            if (AnsweredCorrectly)
            {
                if (!IsRetry)
                {
                    FirstTryCorrect++;
                    Xp += quiz.Xp;
                }
            }
            else if (IsPlacement)
            {
                _firstMissTier ??= quiz.Tier;
            }
            else
            {
                Hearts = Math.Max(0, Hearts - 1);
                _queue.Add(new RetryQuizStep(quiz)); // it comes back at the end
            }
            Raise();
        }

        /// <summary>Moves past the current card (after Check, after a solved build, or to skip a build).</summary>
        public void Continue()
        {
            if (Screen == SessionScreen.Quiz && !Checked)
                return;

            if (IsPlacement && _firstMissTier != null)
            {
                Screen = SessionScreen.Complete; // placement ends at the first miss
                Raise();
                return;
            }

            if (Hearts == 0)
            {
                StopBuild();
                Screen = SessionScreen.OutOfHearts;
                Raise();
                return;
            }

            StopBuild();
            _queueIndex++;
            EnterStep();
            Raise();
        }

        /// <summary>Starts the current build card's level in the active document.</summary>
        public void StartBuild()
        {
            if (!(Step is BuildStep build) || BuildSolved)
                return;
            RhinoDoc? doc = RhinoDoc.ActiveDoc;
            if (doc == null)
                return;

            StopBuild();
            BuildLevel = build.CreateLevel();
            BuildResult = null;
            _runner = LevelRunner.Start(doc, BuildLevel);
            Raise();
        }

        public void Detach()
        {
            LevelRunner.StateChanged -= OnLevelChanged;
            StopBuild();
        }

        // ---- internals ----

        private void EnterStep()
        {
            ResetCard();
            switch (Step)
            {
                case null:
                    Screen = SessionScreen.Complete;
                    break;
                case RetryQuizStep retry:
                    IsRetry = true;
                    Screen = SessionScreen.Quiz;
                    Options = MakeOptions(retry.Quiz);
                    break;
                case IconQuizStep quiz:
                    Screen = SessionScreen.Quiz;
                    Options = MakeOptions(quiz);
                    break;
                case BuildStep build:
                    Screen = SessionScreen.Build;
                    BuildLevel = build.CreateLevel();
                    break;
            }
        }

        private void ResetCard()
        {
            Options = Array.Empty<string>();
            SelectedIndex = -1;
            Checked = false;
            AnsweredCorrectly = false;
            IsRetry = false;
            BuildSolved = false;
            BuildResult = null;
            BuildLevel = null;
        }

        private IReadOnlyList<string> MakeOptions(IconQuizStep quiz)
        {
            // Prefer the look-alikes the lesson listed; top up from every known icon.
            var pool = quiz.Distractors.Where(d => !Same(d, quiz.Command)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var wrong = pool.OrderBy(_ => _random.Next()).Take(3).ToList();
            if (wrong.Count < 3)
            {
                wrong.AddRange(CommandInfo.All
                    .Where(c => !Same(c, quiz.Command) && !wrong.Contains(c, StringComparer.OrdinalIgnoreCase))
                    .OrderBy(_ => _random.Next())
                    .Take(3 - wrong.Count));
            }
            wrong.Add(quiz.Command);
            return wrong.OrderBy(_ => _random.Next()).ToList();
        }

        private void OnLevelChanged(LevelRunner runner)
        {
            if (runner != _runner || Screen != SessionScreen.Build || !(Step is BuildStep build))
                return;

            if (runner.LastResult != null)
                BuildResult = runner.LastResult;

            if (runner.LastResult?.Solved == true && !BuildSolved)
            {
                BuildSolved = true;
                Xp += (int)Math.Round(build.Xp * runner.LastResult.Score);
            }
            Raise();
        }

        private void StopBuild()
        {
            if (_runner != null && LevelRunner.Current == _runner)
                _runner.Stop();
            _runner = null;
        }

        private void Raise() => Changed?.Invoke();

        private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        private static int IndexOf(IReadOnlyList<string> list, string value)
        {
            for (int i = 0; i < list.Count; i++)
                if (Same(list[i], value))
                    return i;
            return -1;
        }

        /// <summary>A missed quiz, queued again at the end. Worth no XP the second time.</summary>
        private sealed class RetryQuizStep : IconQuizStep
        {
            public RetryQuizStep(IconQuizStep quiz)
            {
                Quiz = quiz;
                Command = quiz.Command;
                Kind = quiz.Kind;
                Prompt = quiz.Prompt;
                Tier = quiz.Tier;
                Distractors.AddRange(quiz.Distractors);
                Xp = 0;
            }

            public IconQuizStep Quiz { get; }
        }
    }
}
