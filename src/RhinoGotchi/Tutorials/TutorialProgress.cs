using System;
using Rhino;

namespace RhinoGotchi.Tutorials
{
    /// <summary>Where a learner is with one tutorial.</summary>
    internal sealed class TutorialStatus
    {
        public bool Started;
        public bool Completed;

        /// <summary>Best score so far, 0 to 1.</summary>
        public double BestScore;

        /// <summary>Day key (see Daily) of the first completion, or empty.</summary>
        public string CompletedDay = "";

        /// <summary>Solved at least once without a miss (for the chapter's "first try" count).</summary>
        public bool FirstTry;
    }

    /// <summary>Tutorial progress, stored in the plugin's persistent settings next to the daily-game stats.</summary>
    internal static class TutorialProgress
    {
        private static PersistentSettings? Settings => RhinoGotchiPlugin.Instance?.Settings;

        private static string Key(string tutorialId, string field) => $"tutorial.{tutorialId}.{field}";

        public static TutorialStatus Get(string tutorialId)
        {
            var status = new TutorialStatus();
            var s = Settings;
            if (s == null)
                return status;

            status.Started = s.GetBool(Key(tutorialId, "started"), false);
            status.Completed = s.GetBool(Key(tutorialId, "completed"), false);
            status.BestScore = s.GetDouble(Key(tutorialId, "best"), 0.0);
            status.CompletedDay = s.GetString(Key(tutorialId, "completedDay"), "");
            status.FirstTry = s.GetBool(Key(tutorialId, "firstTry"), false);
            return status;
        }

        public static void MarkStarted(string tutorialId)
        {
            var s = Settings;
            if (s == null || s.GetBool(Key(tutorialId, "started"), false))
                return;
            s.SetBool(Key(tutorialId, "started"), true);
            RhinoGotchiPlugin.Instance?.SaveSettings();
        }

        /// <summary>Records a checked result. Keeps the best score; completion is never undone.</summary>
        public static void Record(string tutorialId, bool solved, double score, bool firstTry = false)
        {
            var s = Settings;
            if (s == null)
                return;

            score = Math.Max(0.0, Math.Min(1.0, score));
            s.SetBool(Key(tutorialId, "started"), true);
            if (score > s.GetDouble(Key(tutorialId, "best"), 0.0))
                s.SetDouble(Key(tutorialId, "best"), score);
            if (solved && !s.GetBool(Key(tutorialId, "completed"), false))
            {
                s.SetBool(Key(tutorialId, "completed"), true);
                s.SetString(Key(tutorialId, "completedDay"), Core.Daily.TodayKey);
            }
            if (solved && firstTry)
                s.SetBool(Key(tutorialId, "firstTry"), true);
            RhinoGotchiPlugin.Instance?.SaveSettings();
        }

        public static int CompletedCount()
        {
            int count = 0;
            foreach (Tutorial t in TutorialCatalog.All)
                if (Get(t.Id).Completed)
                    count++;
            return count;
        }

        public static void Reset()
        {
            var s = Settings;
            if (s == null)
                return;
            foreach (Tutorial t in TutorialCatalog.All)
                foreach (var field in new[] { "started", "completed", "best", "completedDay", "firstTry" })
                    s.DeleteItem(Key(t.Id, field));
            RhinoGotchiPlugin.Instance?.SaveSettings();
        }
    }
}
