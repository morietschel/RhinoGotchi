using System;

namespace RhinoGotchi.Lessons
{
    public enum GameMode
    {
        /// <summary>First run: "start from scratch" or "find my level".</summary>
        Welcome,

        /// <summary>Home: your level, and the path of levels (done / current / locked).</summary>
        Map,

        /// <summary>A lesson or the placement test is being played.</summary>
        Playing,

        /// <summary>"You placed into Level 3: Surfaces."</summary>
        PlacementResult,
    }

    /// <summary>
    /// Top of the game: which screen the panel is on, the saved progress, and the
    /// lesson or placement test being played. UI-free; the panel draws it.
    /// </summary>
    public sealed class GotchiGame
    {
        public GotchiGame(Progress progress)
        {
            Progress = progress;
            Mode = progress.IsNew ? GameMode.Welcome : GameMode.Map;
        }

        /// <summary>Raised whenever anything the panel draws has changed.</summary>
        public event Action? Changed;

        public Progress Progress { get; }

        public GameMode Mode { get; private set; }

        /// <summary>The lesson or placement test being played, or null.</summary>
        public LessonSession? Session { get; private set; }

        /// <summary>Level index of <see cref="Session"/> (-1 for the placement test).</summary>
        public int PlayingLevel { get; private set; } = -1;

        /// <summary>Set after a placement test: the level the player landed on.</summary>
        public int PlacedLevel { get; private set; }

        public void StartFromScratch()
        {
            Progress.StartFromScratch();
            StartLesson(0);
        }

        public void StartPlacement()
        {
            Play(new LessonSession(LessonCatalog.PlacementTest(), isPlacement: true), -1);
        }

        public void StartLesson(int levelIndex)
        {
            if (levelIndex < 0 || levelIndex >= LessonCatalog.All.Count || Progress.StateOf(levelIndex) == LevelState.Locked)
                return;
            Play(new LessonSession(LessonCatalog.All[levelIndex]), levelIndex);
        }

        /// <summary>
        /// Shows the welcome screen ("New to Rhino?" / "Find my level") again.
        /// Progress is kept; the player can go back to the map from there.
        /// </summary>
        public void ShowWelcome()
        {
            EndSession();
            Mode = GameMode.Welcome;
            Raise();
        }

        /// <summary>Leaves the lesson or test without saving anything and shows the map.</summary>
        public void BackToMap()
        {
            EndSession();
            Mode = Progress.IsNew ? GameMode.Welcome : GameMode.Map;
            Raise();
        }

        /// <summary>Called from the "lesson complete" screen: saves the result and returns to the map.</summary>
        public void FinishLesson()
        {
            if (Session != null && !Session.IsPlacement && Session.Screen == SessionScreen.Complete)
                Progress.CompleteLesson(Session.Lesson, Session.Xp);
            BackToMap();
        }

        private void Play(LessonSession session, int levelIndex)
        {
            EndSession();
            Session = session;
            PlayingLevel = levelIndex;
            Session.Changed += OnSessionChanged;
            Mode = GameMode.Playing;
            Session.Begin();
            Raise();
        }

        private void OnSessionChanged()
        {
            if (Session != null && Session.IsPlacement && Session.Screen == SessionScreen.Complete)
            {
                PlacedLevel = Math.Min(Session.PlacedLevel, LessonCatalog.All.Count);
                Progress.ApplyPlacement(PlacedLevel);
                EndSession();
                Mode = GameMode.PlacementResult;
            }
            Raise();
        }

        private void EndSession()
        {
            if (Session == null)
                return;
            Session.Changed -= OnSessionChanged;
            Session.Detach();
            Session = null;
            PlayingLevel = -1;
        }

        private void Raise() => Changed?.Invoke();
    }
}
