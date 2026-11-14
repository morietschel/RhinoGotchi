using System;
using System.Collections.Generic;

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
            StatsProvider = () => Progress.Stats();
        }

        /// <summary>
        /// Builds the achievement stats. The panel replaces it to add the daily games' numbers;
        /// the default only knows about lessons.
        /// </summary>
        public Func<AchievementStats> StatsProvider { get; set; }

        /// <summary>Achievements unlocked by the lesson or test that just ended (shown on its result screen).</summary>
        public List<Achievement> NewUnlocks { get; private set; } = new List<Achievement>();

        public AchievementStats Stats => StatsProvider();

        /// <summary>Puts on or takes off a garment if it is unlocked. Returns false for a locked one.</summary>
        public bool ToggleClothes(Clothes piece)
        {
            bool ok = Progress.ToggleClothes(piece, Stats);
            if (ok)
                Raise();
            return ok;
        }

        /// <summary>Puts on a hat if it is unlocked. Returns false for a locked hat.</summary>
        public bool Wear(Hat hat)
        {
            bool ok = Progress.Wear(hat, Stats);
            if (ok)
                Raise();
            return ok;
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

        private bool _saved;

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

        /// <summary>Called from the "lesson complete" screen: makes sure the result is saved and returns to the map.</summary>
        public void FinishLesson()
        {
            SaveIfComplete();
            BackToMap();
        }

        /// <summary>Saves a finished lesson once, as soon as it reaches the Complete screen.</summary>
        private void SaveIfComplete()
        {
            if (Session == null || Session.IsPlacement || Session.Screen != SessionScreen.Complete || _saved)
                return;
            _saved = true;
            var before = Stats;
            bool perfect = Session.QuizzesAsked > 0 && Session.FirstTryCorrect == Session.QuizzesAsked;
            Progress.CompleteLesson(Session.Lesson, Session.Xp, Session.FirstTryCorrect, perfect);
            NewUnlocks = Achievements.NewlyUnlocked(before, Stats);
        }

        private void Play(LessonSession session, int levelIndex)
        {
            EndSession();
            Session = session;
            PlayingLevel = levelIndex;
            NewUnlocks = new List<Achievement>();
            _saved = false;
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
                var before = Stats;
                Progress.ApplyPlacement(PlacedLevel, Session.FirstTryCorrect);
                NewUnlocks = Achievements.NewlyUnlocked(before, Stats);
                EndSession();
                Mode = GameMode.PlacementResult;
            }
            SaveIfComplete();
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
