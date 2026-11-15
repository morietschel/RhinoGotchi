using System.Collections.Generic;
using System.Linq;

namespace RhinoGotchi.Drone
{
    /// <summary>Drone School progress: which levels are cleared and the best stars on each, in the plugin settings.</summary>
    internal static class DroneProgress
    {
        private const string Key = "drone.done";
        private static string StarsKey(string id) => "drone.stars." + id;

        public static HashSet<string> Done()
        {
            var s = RhinoGotchiPlugin.Instance?.Settings;
            return new HashSet<string>(s?.GetStringList(Key, new string[0]) ?? new string[0]);
        }

        public static int DoneCount => SchoolLevels.All.Count(l => Done().Contains(l.Id));

        public static int Stars(string id) => RhinoGotchiPlugin.Instance?.Settings.GetInteger(StarsKey(id), 0) ?? 0;

        /// <summary>Marks a level cleared and keeps the best star rating.</summary>
        public static void Cleared(string levelId, int stars)
        {
            var s = RhinoGotchiPlugin.Instance?.Settings;
            if (s == null)
                return;
            var done = Done();
            if (done.Add(levelId))
                s.SetStringList(Key, done.ToArray());
            if (stars > Stars(levelId))
                s.SetInteger(StarsKey(levelId), stars);
            RhinoGotchiPlugin.Instance?.SaveSettings();
        }

        public static void Reset()
        {
            var s = RhinoGotchiPlugin.Instance?.Settings;
            if (s == null)
                return;
            s.DeleteItem(Key);
            foreach (var level in SchoolLevels.All)
                s.DeleteItem(StarsKey(level.Id));
        }

        /// <summary>The first level not cleared yet (or the first, when all are done).</summary>
        public static int NextIndex()
        {
            var done = Done();
            for (int i = 0; i < SchoolLevels.All.Length; i++)
                if (!done.Contains(SchoolLevels.All[i].Id))
                    return i;
            return 0;
        }

        /// <summary>Three stars for a clean run, fewer for wrong clicks or bonks. Every clear gets at least one.</summary>
        public static int StarsFor(int slips) => slips <= 0 ? 3 : slips <= 2 ? 2 : 1;
    }
}
