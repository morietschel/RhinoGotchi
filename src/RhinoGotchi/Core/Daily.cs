using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RhinoGotchi.Core
{
    /// <summary>Picks the same daily puzzle for everyone on a given date.</summary>
    internal static class Daily
    {
        public static string TodayKey => Key(DateTime.Today);
        public static string YesterdayKey => Key(DateTime.Today.AddDays(-1));

        private static string Key(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        /// <summary>
        /// A random generator seeded by today's date and the game name, or a fresh one for practice.
        /// The seed is computed by hand because string.GetHashCode is randomized per process on .NET Core.
        /// </summary>
        public static Random Rng(string game, bool practice)
        {
            if (practice)
                return new Random();

            int seed = DateTime.Today.Year * 1000 + DateTime.Today.DayOfYear;
            foreach (char c in game)
                seed = unchecked(seed * 31 + c);
            return new Random(seed);
        }

        public static List<T> Shuffle<T>(IEnumerable<T> items, Random rng)
        {
            var list = items.ToList();
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            return list;
        }
    }
}
