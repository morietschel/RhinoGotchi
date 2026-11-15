using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Rhino;
using Rhino.PlugIns;

namespace RhinoGotchi.Lessons
{
    public enum LevelState
    {
        Done,
        Current,
        Locked,
    }

    /// <summary>
    /// What the player has finished, saved in the plugin's settings so it survives
    /// a Rhino restart. Per user, on this machine; no accounts.
    /// </summary>
    public sealed class Progress
    {
        private const string CompletedKey = "progress.completed";
        private const string PlacementKey = "progress.placementDone";
        private const string PlacementTakenKey = "progress.placementTaken";
        private const string XpKey = "progress.totalXp";
        private const string PerfectKey = "progress.perfectLessons";
        private const string CorrectKey = "progress.correctAnswers";
        private const string StreakKey = "progress.streak";
        private const string BestStreakKey = "progress.bestStreak";
        private const string LastDayKey = "progress.lastActiveDay";
        private const string HatKey = "pet.hat";
        private const string ClothesKey = "pet.clothes";

        private readonly HashSet<string> _completed = new HashSet<string>();
        private int _streak;
        private string _lastDay = "";

        /// <summary>Today's date. Replaceable so tests can move through days.</summary>
        public static Func<DateTime> Clock { get; set; } = () => DateTime.Today;

        /// <summary>The player made the first-run choice (scratch or test), so the welcome screen is skipped.</summary>
        public bool PlacementDone { get; private set; }

        /// <summary>The placement test was actually finished at least once.</summary>
        public bool PlacementTaken { get; private set; }

        public int TotalXp { get; private set; }

        /// <summary>Lessons finished without a single mistake.</summary>
        public int PerfectLessons { get; private set; }

        /// <summary>Questions answered right on the first try, in lessons and the placement test.</summary>
        public int CorrectAnswers { get; private set; }

        public int BestStreak { get; private set; }

        /// <summary>Days in a row with a finished lesson or test. Zero once a day is skipped.</summary>
        public int Streak => _lastDay == DayKey(Clock()) || _lastDay == DayKey(Clock().AddDays(-1)) ? _streak : 0;

        /// <summary>True if a lesson or test was finished today.</summary>
        public bool ActiveToday => _lastDay == DayKey(Clock());

        /// <summary>The hat Gotchi is wearing.</summary>
        public Hat EquippedHat { get; private set; }

        /// <summary>The clothes Gotchi is wearing.</summary>
        public Clothes EquippedClothes { get; private set; }

        /// <summary>True for a brand-new player: show the welcome screen instead of the map.</summary>
        public bool IsNew => !PlacementDone && _completed.Count == 0;

        /// <summary>Index of the first level not done yet, or -1 when every level is done.</summary>
        public int CurrentLevel
        {
            get
            {
                // The main path only: chapter levels don't block it.
                for (int i = 0; i < LessonCatalog.All.Count; i++)
                    if (!LessonCatalog.All[i].IsProject && !_completed.Contains(LessonCatalog.All[i].Id))
                        return i;
                return -1;
            }
        }

        /// <summary>Main levels done (the "n/10" on the map and the home page).</summary>
        public int DoneCount => LessonCatalog.All.Count(l => !l.IsProject && _completed.Contains(l.Id));

        /// <summary>Levels done in a chapter, e.g. "KLOSTERNEUBURG".</summary>
        public int ChapterDone(string chapter) => LessonCatalog.All.Count(l => l.Chapter == chapter && _completed.Contains(l.Id));

        public bool IsDone(string lessonId) => _completed.Contains(lessonId);

        public LevelState StateOf(int levelIndex)
        {
            if (_completed.Contains(LessonCatalog.All[levelIndex].Id))
                return LevelState.Done;
            Lesson lesson = LessonCatalog.All[levelIndex];
            if (lesson.IsProject)
            {
                // A chapter is open from the start and played in order, like FinGotchi's levels.
                bool earlierOpen = LessonCatalog.All.Take(levelIndex)
                    .Where(l => l.Chapter == lesson.Chapter)
                    .All(l => _completed.Contains(l.Id));
                return earlierOpen ? LevelState.Current : LevelState.Locked;
            }
            return levelIndex == CurrentLevel ? LevelState.Current : LevelState.Locked;
        }

        /// <summary>The lesson side of the achievement stats. Daily-game numbers are added by the caller.</summary>
        public AchievementStats Stats(int dailyWins = 0, int gameBestStreak = 0) => new AchievementStats
        {
            LevelsDone = DoneCount,
            PlacementDone = PlacementTaken,
            PerfectLessons = PerfectLessons,
            CorrectAnswers = CorrectAnswers,
            BestStreak = Math.Max(BestStreak, gameBestStreak),
            DailyWins = dailyWins,
            TotalXp = TotalXp,
        };

        public void CompleteLesson(Lesson lesson, int xp, int firstTryCorrect = 0, bool perfect = false)
        {
            if (LessonCatalog.IndexOf(lesson.Id) >= 0)
                _completed.Add(lesson.Id);
            TotalXp += Math.Max(0, xp);
            CorrectAnswers += Math.Max(0, firstTryCorrect);
            if (perfect)
                PerfectLessons++;
            MarkActive();
            Save();
        }

        /// <summary>
        /// Records a placement result: every level below <paramref name="placedLevel"/>
        /// counts as done. Levels already finished stay finished.
        /// </summary>
        public void ApplyPlacement(int placedLevel, int correctAnswers = 0)
        {
            for (int i = 0; i < Math.Min(placedLevel, LessonCatalog.MainCount); i++)
                _completed.Add(LessonCatalog.All[i].Id);
            PlacementDone = true;
            PlacementTaken = true;
            CorrectAnswers += Math.Max(0, correctAnswers);
            MarkActive();
            Save();
        }

        public void StartFromScratch()
        {
            PlacementDone = true; // they chose; don't show the welcome screen again
            Save();
        }

        /// <summary>Puts a hat on Gotchi. Returns false (and changes nothing) if the hat is still locked.</summary>
        public bool Wear(Hat hat, AchievementStats stats)
        {
            if (!Achievements.IsHatUnlocked(hat, stats))
                return false;
            EquippedHat = hat;
            Save();
            return true;
        }

        /// <summary>
        /// Puts a garment on, or takes it off if it's already on. Tracksuit and bomber swap with each
        /// other. Returns false (and changes nothing) if the garment is still locked.
        /// </summary>
        public bool ToggleClothes(Clothes piece, AchievementStats stats)
        {
            if (!Achievements.AreClothesUnlocked(piece, stats))
                return false;
            if (EquippedClothes.HasFlag(piece))
                EquippedClothes &= ~piece;
            else
            {
                if (piece == Clothes.Tracksuit) EquippedClothes &= ~Clothes.Bomber;
                if (piece == Clothes.Bomber) EquippedClothes &= ~Clothes.Tracksuit;
                EquippedClothes |= piece;
            }
            Save();
            return true;
        }

        private void MarkActive()
        {
            string today = DayKey(Clock());
            if (_lastDay == today)
                return;
            _streak = _lastDay == DayKey(Clock().AddDays(-1)) ? _streak + 1 : 1;
            _lastDay = today;
            BestStreak = Math.Max(BestStreak, _streak);
        }

        private static string DayKey(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public static Progress Load()
        {
            var p = new Progress();
            PersistentSettings? s = Settings();
            if (s == null)
                return p;
            p.PlacementDone = s.GetBool(PlacementKey, false);
            p.PlacementTaken = s.GetBool(PlacementTakenKey, false);
            p.TotalXp = s.GetInteger(XpKey, 0);
            p.PerfectLessons = s.GetInteger(PerfectKey, 0);
            p.CorrectAnswers = s.GetInteger(CorrectKey, 0);
            p._streak = s.GetInteger(StreakKey, 0);
            p.BestStreak = s.GetInteger(BestStreakKey, 0);
            p._lastDay = s.GetString(LastDayKey, "");
            p.EquippedHat = Enum.TryParse(s.GetString(HatKey, nameof(Hat.None)), out Hat hat) ? hat : Hat.None;
            p.EquippedClothes = (Clothes)(s.GetInteger(ClothesKey, 0) & 15);
            foreach (string id in s.GetStringList(CompletedKey, Array.Empty<string>()) ?? Array.Empty<string>())
                p._completed.Add(id);
            return p;
        }

        private void Save()
        {
            PersistentSettings? s = Settings();
            if (s == null)
                return;
            s.SetBool(PlacementKey, PlacementDone);
            s.SetBool(PlacementTakenKey, PlacementTaken);
            s.SetInteger(XpKey, TotalXp);
            s.SetInteger(PerfectKey, PerfectLessons);
            s.SetInteger(CorrectKey, CorrectAnswers);
            s.SetInteger(StreakKey, _streak);
            s.SetInteger(BestStreakKey, BestStreak);
            s.SetString(LastDayKey, _lastDay);
            s.SetString(HatKey, EquippedHat.ToString());
            s.SetInteger(ClothesKey, (int)EquippedClothes);
            s.SetStringList(CompletedKey, _completed.ToArray());
            try
            {
                RhinoGotchiPlugin.Instance?.SaveSettings();
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"[RhinoGotchi] Could not save progress: {ex.Message}");
            }
        }

        private static PersistentSettings? Settings() => RhinoGotchiPlugin.Instance?.Settings;
    }
}
