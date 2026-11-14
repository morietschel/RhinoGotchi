using System;
using System.Collections.Generic;
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
        private const string XpKey = "progress.totalXp";

        private readonly HashSet<string> _completed = new HashSet<string>();

        public bool PlacementDone { get; private set; }

        public int TotalXp { get; private set; }

        /// <summary>True for a brand-new player: show the welcome screen instead of the map.</summary>
        public bool IsNew => !PlacementDone && _completed.Count == 0;

        /// <summary>Index of the first level not done yet, or -1 when every level is done.</summary>
        public int CurrentLevel
        {
            get
            {
                for (int i = 0; i < LessonCatalog.All.Count; i++)
                    if (!_completed.Contains(LessonCatalog.All[i].Id))
                        return i;
                return -1;
            }
        }

        public int DoneCount => LessonCatalog.All.Count(l => _completed.Contains(l.Id));

        public LevelState StateOf(int levelIndex)
        {
            if (_completed.Contains(LessonCatalog.All[levelIndex].Id))
                return LevelState.Done;
            return levelIndex == CurrentLevel ? LevelState.Current : LevelState.Locked;
        }

        public void CompleteLesson(Lesson lesson, int xp)
        {
            if (LessonCatalog.IndexOf(lesson.Id) >= 0)
                _completed.Add(lesson.Id);
            TotalXp += Math.Max(0, xp);
            Save();
        }

        /// <summary>
        /// Records a placement result: every level below <paramref name="placedLevel"/>
        /// counts as done. Levels already finished stay finished.
        /// </summary>
        public void ApplyPlacement(int placedLevel)
        {
            for (int i = 0; i < Math.Min(placedLevel, LessonCatalog.All.Count); i++)
                _completed.Add(LessonCatalog.All[i].Id);
            PlacementDone = true;
            Save();
        }

        public void StartFromScratch()
        {
            PlacementDone = true; // they chose; don't show the welcome screen again
            Save();
        }

        public static Progress Load()
        {
            var p = new Progress();
            PersistentSettings? s = Settings();
            if (s == null)
                return p;
            p.PlacementDone = s.GetBool(PlacementKey, false);
            p.TotalXp = s.GetInteger(XpKey, 0);
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
            s.SetInteger(XpKey, TotalXp);
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
