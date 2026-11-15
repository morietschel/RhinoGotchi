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

    /// <summary>The clothing slot: one item at a time (from the Wardrobe Lab).</summary>
    public enum Clothes
    {
        None,
        Jacket,
        JeansA,
        JeansB,
        BigChain,
        Bow,
    }

    /// <summary>The face slot: one item at a time. Face items are free to wear.</summary>
    public enum Face
    {
        None,
        Snorkel,
        Visor,
        Ninja,
        Shades,
    }

    /// <summary>Names shown in the wardrobe.</summary>
    public static class Outfit
    {
        public static string Name(Hat h) => h switch
        {
            Hat.None => "No hat",
            Hat.HardHat => "Hard hat",
            Hat.Beret => "Beret",
            Hat.Grad => "Grad cap",
            Hat.Flame => "Flame crown",
            Hat.Cowboy => "Cowboy hat",
            Hat.Propeller => "Propeller",
            Hat.Tree => "Topping-out tree",
            Hat.Wizard => "Wizard hat",
            _ => "Gold crown",
        };

        public static string Name(Clothes c) => c switch
        {
            Clothes.None => "Nothing",
            Clothes.Jacket => "Leather jacket",
            Clothes.JeansA => "Jeans",
            Clothes.JeansB => "Back jeans",
            Clothes.BigChain => "Big gold chain",
            _ => "Bow + pearls",
        };

        public static string Name(Face f) => f switch
        {
            Face.None => "Nothing",
            Face.Snorkel => "Snorkel mask",
            Face.Visor => "Visor",
            Face.Ninja => "Ninja mask",
            _ => "Shutter shades",
        };
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
            new Achievement("placement", "Know Thyself", "Take the placement test", 1, s => s.PlacementDone ? 1 : 0, clothes: Clothes.BigChain, rewardName: "Big gold chain"),
            new Achievement("perfect", "Flawless", "Finish a lesson with no mistakes", 1, s => s.PerfectLessons, clothes: Clothes.Jacket, rewardName: "Leather jacket"),
            new Achievement("halfway", "Halfway There", "Finish 5 levels", 5, s => s.LevelsDone, clothes: Clothes.JeansA, rewardName: "Jeans (both styles)"),
            new Achievement("xp", "XP Collector", "Earn 1,000 XP", 1000, s => s.TotalXp, clothes: Clothes.Bow, rewardName: "Bow + pearls"),
        };

        public static readonly IReadOnlyList<Achievement> All = Hats.Concat(Wardrobe).ToList();

        public static Achievement? ForHat(Hat hat) => Hats.FirstOrDefault(a => a.Hat == hat);

        /// <summary>The achievement that unlocks a clothing item (both jeans styles share one).</summary>
        public static Achievement? ForClothes(Clothes piece) =>
            Wardrobe.FirstOrDefault(a => a.Clothes == (piece == Clothes.JeansB ? Clothes.JeansA : piece));

        public static int UnlockedCount(AchievementStats stats) => All.Count(a => a.IsUnlocked(stats));

        public static bool IsHatUnlocked(Hat hat, AchievementStats stats) =>
            hat == Hat.None || (ForHat(hat)?.IsUnlocked(stats) ?? false);

        public static bool AreClothesUnlocked(Clothes clothes, AchievementStats stats) =>
            clothes == Clothes.None || (ForClothes(clothes)?.IsUnlocked(stats) ?? false);

        /// <summary>Achievements unlocked in <paramref name="after"/> but not in <paramref name="before"/>.</summary>
        public static List<Achievement> NewlyUnlocked(AchievementStats before, AchievementStats after) =>
            All.Where(a => !a.IsUnlocked(before) && a.IsUnlocked(after)).ToList();
    }
}
