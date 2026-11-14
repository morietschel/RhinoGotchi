using System;
using System.Collections.Generic;
using System.Linq;

namespace RhinoGotchi.Lessons
{
    /// <summary>The hats Gotchi can wear. Each one is unlocked by an achievement.</summary>
    public enum Hat
    {
        None,
        HardHat,
        Beret,
        Grad,
        Flame,
        Cowboy,
        Propeller,
        Tree,
        Wizard,
        Crown,
    }

    /// <summary>Everything the achievements look at, gathered from lessons and the daily games.</summary>
    public sealed class AchievementStats
    {
        public int LevelsDone;
        public bool PlacementDone;
        public int PerfectLessons;
        public int CorrectAnswers;
        public int BestStreak;
        public int DailyWins;
        public int TotalXp;
    }

    public sealed class Achievement
    {
        private readonly Func<AchievementStats, int> _value;

        public Achievement(string id, string name, string condition, Hat hat, string hatName, int goal, Func<AchievementStats, int> value)
        {
            Id = id;
            Name = name;
            Condition = condition;
            Hat = hat;
            HatName = hatName;
            Goal = goal;
            _value = value;
        }

        public string Id { get; }
        public string Name { get; }
        public string Condition { get; }
        public Hat Hat { get; }
        public string HatName { get; }
        public int Goal { get; }

        /// <summary>How far along the player is, capped at <see cref="Goal"/>.</summary>
        public int ProgressOf(AchievementStats stats) => Math.Max(0, Math.Min(Goal, _value(stats)));

        public bool IsUnlocked(AchievementStats stats) => _value(stats) >= Goal;
    }

    public static class Achievements
    {
        public static readonly IReadOnlyList<Achievement> All = new[]
        {
            new Achievement("first", "First Steps", "Finish your first lesson", Hat.HardHat, "Hard hat", 1, s => s.LevelsDone),
            new Achievement("placement", "Know Thyself", "Take the placement test", Hat.Beret, "Beret", 1, s => s.PlacementDone ? 1 : 0),
            new Achievement("perfect", "Flawless", "Finish a lesson with no mistakes", Hat.Grad, "Grad cap", 1, s => s.PerfectLessons),
            new Achievement("streak", "Hot Streak", "Learn or play 3 days in a row", Hat.Flame, "Flame", 3, s => s.BestStreak),
            new Achievement("halfway", "Halfway There", "Finish 5 levels", Hat.Cowboy, "Cowboy", 5, s => s.LevelsDone),
            new Achievement("sharp", "Sharp Eye", "Answer 50 questions right on the first try", Hat.Propeller, "Propeller", 50, s => s.CorrectAnswers),
            new Achievement("daily", "Daily Habit", "Win 3 daily games", Hat.Tree, "Tree", 3, s => s.DailyWins),
            new Achievement("xp", "XP Collector", "Earn 1,000 XP", Hat.Wizard, "Wizard", 1000, s => s.TotalXp),
            new Achievement("master", "Rhino Master", "Finish all 10 levels", Hat.Crown, "Crown", 10, s => s.LevelsDone),
        };

        public static Achievement? ForHat(Hat hat) => All.FirstOrDefault(a => a.Hat == hat);

        public static int UnlockedCount(AchievementStats stats) => All.Count(a => a.IsUnlocked(stats));

        public static bool IsHatUnlocked(Hat hat, AchievementStats stats) =>
            hat == Hat.None || (ForHat(hat)?.IsUnlocked(stats) ?? false);

        /// <summary>Achievements unlocked in <paramref name="after"/> but not in <paramref name="before"/>.</summary>
        public static List<Achievement> NewlyUnlocked(AchievementStats before, AchievementStats after) =>
            All.Where(a => !a.IsUnlocked(before) && a.IsUnlocked(after)).ToList();
    }
}
