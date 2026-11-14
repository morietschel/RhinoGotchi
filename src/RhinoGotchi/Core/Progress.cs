using System.Collections.Generic;
using System.Linq;
using Rhino;

namespace RhinoGotchi.Core
{
    /// <summary>Keys for each game, used for daily seeds and saved stats.</summary>
    internal static class Games
    {
        public const string Shape = "shape";
        public const string Order = "order";
        public const string Deep = "deep";
        public const string Blocked = "blocked";
        public static readonly string[] All = { Shape, Order, Deep, Blocked };

        public static string Title(string game) => game switch
        {
            Shape => "What Made This?",
            Order => "Build Order",
            Deep => "Deep Cut",
            Blocked => "Blocked!",
            _ => game,
        };

        /// <summary>A one-word name that fits in a tab.</summary>
        public static string ShortTitle(string game) => game switch
        {
            Shape => "Shapes",
            Order => "Build",
            Deep => "Deep",
            Blocked => "Blocked",
            _ => game,
        };
    }

    internal sealed class GameStats
    {
        public int Played;
        public int Wins;
        public int Streak;
        public int BestStreak;
        public string LastPlayedDay = "";
        public string LastWinDay = "";
        public string LastResult = "";

        public bool PlayedToday => LastPlayedDay == Daily.TodayKey;

        /// <summary>The streak only counts if the last win was today or yesterday.</summary>
        public int CurrentStreak =>
            LastWinDay == Daily.TodayKey || LastWinDay == Daily.YesterdayKey ? Streak : 0;
    }

    /// <summary>Daily-game stats, stored in the plugin's persistent settings.</summary>
    internal static class Progress
    {
        private static PersistentSettings? Settings => RhinoGotchiPlugin.Instance?.Settings;

        public static GameStats Get(string game)
        {
            var stats = new GameStats();
            var s = Settings;
            if (s == null)
                return stats;

            stats.Played = s.GetInteger(game + ".played", 0);
            stats.Wins = s.GetInteger(game + ".wins", 0);
            stats.Streak = s.GetInteger(game + ".streak", 0);
            stats.BestStreak = s.GetInteger(game + ".best", 0);
            stats.LastPlayedDay = s.GetString(game + ".lastPlayed", "");
            stats.LastWinDay = s.GetString(game + ".lastWin", "");
            stats.LastResult = s.GetString(game + ".lastResult", "");
            return stats;
        }

        /// <summary>Records today's daily game. Only the first result of the day counts.</summary>
        public static void Record(string game, bool won, string result)
        {
            var s = Settings;
            if (s == null)
                return;

            var stats = Get(game);
            if (stats.PlayedToday)
                return;

            stats.Played++;
            if (won)
            {
                stats.Wins++;
                stats.Streak = stats.LastWinDay == Daily.YesterdayKey ? stats.Streak + 1 : 1;
                if (stats.Streak > stats.BestStreak)
                    stats.BestStreak = stats.Streak;
                s.SetString(game + ".lastWin", Daily.TodayKey);
            }
            else
            {
                stats.Streak = 0;
            }

            s.SetInteger(game + ".played", stats.Played);
            s.SetInteger(game + ".wins", stats.Wins);
            s.SetInteger(game + ".streak", stats.Streak);
            s.SetInteger(game + ".best", stats.BestStreak);
            s.SetString(game + ".lastPlayed", Daily.TodayKey);
            s.SetString(game + ".lastResult", result);
            RhinoGotchiPlugin.Instance?.SaveSettings();
        }

        // ---- Blocked! ways found (kept per challenge, across days) ----

        private static string WaysKey(string challenge) => "blocked.ways." + challenge;

        /// <summary>Ids of the ways found for a challenge. Known ways use their id; new ones are "custom:Route → Here".</summary>
        public static List<string> WaysFound(string challenge)
        {
            var s = Settings;
            return s == null
                ? new List<string>()
                : (s.GetStringList(WaysKey(challenge), new string[0]) ?? new string[0]).ToList();
        }

        /// <summary>Saves a found way. Returns false if it was already found.</summary>
        public static bool AddWay(string challenge, string wayId)
        {
            var s = Settings;
            if (s == null)
                return false;
            var found = WaysFound(challenge);
            if (found.Contains(wayId))
                return false;
            found.Add(wayId);
            s.SetStringList(WaysKey(challenge), found.ToArray());
            RhinoGotchiPlugin.Instance?.SaveSettings();
            return true;
        }

        public static void Reset()
        {
            var s = Settings;
            if (s == null)
                return;

            foreach (var game in Games.All)
                foreach (var key in new[] { "played", "wins", "streak", "best", "lastPlayed", "lastWin", "lastResult" })
                    s.DeleteItem(game + "." + key);
            foreach (var challenge in Content.BlockedChallenges.All)
                s.DeleteItem(WaysKey(challenge.Id));
            RhinoGotchiPlugin.Instance?.SaveSettings();
            Tutorials.TutorialProgress.Reset();
        }
    }
}
