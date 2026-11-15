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

    /// <summary>Body clothes. Tracksuit and bomber both cover the torso, so only one at a time.</summary>
    [Flags]
    public enum Clothes
    {
        None = 0,
        Chain = 1,
        Tracksuit = 2,
        Bomber = 4,
        Jeans = 8,
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

        // Daily-game wins, per game (practice rounds don't count).
        public int OrderWins;
        public int ShapeWins;
        public int DeepWins;
        public int BlockedWins;
        /// <summary>1 once all four dailies were won on the same day.</summary>
        public int AllDailiesOneDay;
    }

    public sealed class Achievement
    {
        private readonly Func<AchievementStats, int> _value;

        public Achievement(string id, string name, string condition, int goal, Func<AchievementStats, int> value,
            Hat hat = Hat.None, Clothes clothes = Clothes.None, string rewardName = "", string comingWith = "")
        {
            Id = id;
            Name = name;
            Condition = condition;
            Goal = goal;
            _value = value;
            Hat = hat;
            Clothes = clothes;
            RewardName = rewardName;
            ComingWith = comingWith;
        }

        public string Id { get; }
        public string Name { get; }
        public string Condition { get; }
        public int Goal { get; }

        /// <summary>The hat it unlocks (None for a clothes reward).</summary>
        public Hat Hat { get; }

        /// <summary>The garment it unlocks (None for a hat reward).</summary>
        public Clothes Clothes { get; }

        public string RewardName { get; }

        /// <summary>Kept for compatibility with the hat-only screens.</summary>
        public string HatName => RewardName;

        /// <summary>Set when the game that tracks it isn't built yet: the achievement can't be earned until it arrives.</summary>
        public string ComingWith { get; }

        public bool IsComingSoon => ComingWith.Length > 0;

        /// <summary>How far along the player is, capped at <see cref="Goal"/>.</summary>
        public int ProgressOf(AchievementStats stats) => IsComingSoon ? 0 : Math.Max(0, Math.Min(Goal, _value(stats)));

        public bool IsUnlocked(AchievementStats stats) => !IsComingSoon && _value(stats) >= Goal;
    }

    public static class Achievements
    {
        /// <summary>Achievements that unlock the nine hats, from lessons and the four daily games.</summary>
        public static readonly IReadOnlyList<Achievement> Hats = new[]
        {
            new Achievement("first", "First Steps", "Finish your first lesson", 1, s => s.LevelsDone, Hat.HardHat, rewardName: "Hard hat"),
            new Achievement("par", "Par Excellence", "Win a daily puzzle", 1, s => s.DailyWins, Hat.Beret, rewardName: "Beret"),
            new Achievement("streak", "Hot Streak", "3-day daily streak", 3, s => s.BestStreak, Hat.Flame, rewardName: "Flame"),
            new Achievement("order", "Order Up", "Win Build Order 5 times", 5, s => s.OrderWins, Hat.Cowboy, rewardName: "Cowboy"),
            new Achievement("blocked", "Workaround", "Win Blocked!", 1, s => s.BlockedWins, Hat.Wizard, rewardName: "Wizard"),
            new Achievement("scholar", "Scholar", "Finish all 10 levels", 10, s => s.LevelsDone, Hat.Grad, rewardName: "Grad cap"),
            new Achievement("deep", "Deep Roots", "Win Deep Cut", 1, s => s.DeepWins, Hat.Tree, rewardName: "Tree"),
            new Achievement("sweep", "Clean Sweep", "Win all four dailies on one day", 1, s => s.AllDailiesOneDay, Hat.Crown, rewardName: "Crown"),
            new Achievement("shape", "Know-It-All", "Win What Made This? 5 times", 5, s => s.ShapeWins, Hat.Propeller, rewardName: "Propeller"),
        };

        /// <summary>Learning Campaign achievements; each unlocks a piece of clothing.</summary>
        public static readonly IReadOnlyList<Achievement> Wardrobe = new[]
        {
            new Achievement("placement", "Know Thyself", "Take the placement test", 1, s => s.PlacementDone ? 1 : 0, clothes: Clothes.Chain, rewardName: "Gold chain"),
            new Achievement("perfect", "Flawless", "Finish a lesson with no mistakes", 1, s => s.PerfectLessons, clothes: Clothes.Tracksuit, rewardName: "Tracksuit"),
            new Achievement("halfway", "Halfway There", "Finish 5 levels", 5, s => s.LevelsDone, clothes: Clothes.Bomber, rewardName: "Bomber jacket"),
            new Achievement("xp", "XP Collector", "Earn 1,000 XP", 1000, s => s.TotalXp, clothes: Clothes.Jeans, rewardName: "Jeans"),
        };

        public static readonly IReadOnlyList<Achievement> All = Hats.Concat(Wardrobe).ToList();

        public static Achievement? ForHat(Hat hat) => Hats.FirstOrDefault(a => a.Hat == hat);

        public static Achievement? ForClothes(Clothes piece) => Wardrobe.FirstOrDefault(a => a.Clothes == piece);

        public static int UnlockedCount(AchievementStats stats) => All.Count(a => a.IsUnlocked(stats));

        public static bool IsHatUnlocked(Hat hat, AchievementStats stats) =>
            hat == Hat.None || (ForHat(hat)?.IsUnlocked(stats) ?? false);

        /// <summary>True if every piece in <paramref name="clothes"/> is unlocked.</summary>
        public static bool AreClothesUnlocked(Clothes clothes, AchievementStats stats) =>
            new[] { Clothes.Chain, Clothes.Tracksuit, Clothes.Bomber, Clothes.Jeans }
                .Where(c => clothes.HasFlag(c))
                .All(c => ForClothes(c)?.IsUnlocked(stats) ?? false);

        /// <summary>Achievements unlocked in <paramref name="after"/> but not in <paramref name="before"/>.</summary>
        public static List<Achievement> NewlyUnlocked(AchievementStats before, AchievementStats after) =>
            All.Where(a => !a.IsUnlocked(before) && a.IsUnlocked(after)).ToList();
    }
}
