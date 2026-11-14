using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Rhino;

namespace RhinoGotchi.Core
{
    /// <summary>
    /// This player's side of the shared leaderboard: their profile (name and icon) and the points
    /// they have earned per game, kept in the plugin settings and pushed to the server.
    ///
    /// The local totals are the source of truth. Every sync sends the full totals, so a push that
    /// fails (offline, server down) is simply made up for by the next one.
    /// </summary>
    internal static class Leaderboard
    {
        /// <summary>
        /// The Firebase Realtime Database URL, e.g. "https://my-project-default-rtdb.firebaseio.com".
        /// Leave empty to switch the online leaderboard off. See the README for the one-time setup.
        /// </summary>
        private const string DatabaseUrl = "https://rhinogotchi-default-rtdb.firebaseio.com";

        /// <summary>Lets a developer point a debug session at another database without rebuilding.</summary>
        private const string UrlEnvironmentVariable = "RHINOGOTCHI_LEADERBOARD_URL";

        private static readonly Lazy<LeaderboardClient?> LazyClient = new Lazy<LeaderboardClient?>(() =>
        {
            string url = Environment.GetEnvironmentVariable(UrlEnvironmentVariable) ?? "";
            if (url.Length == 0)
                url = DatabaseUrl;
            return url.Length == 0 ? null : new LeaderboardClient(url);
        });

        private static PersistentSettings? Settings => RhinoGotchiPlugin.Instance?.Settings;

        public static LeaderboardClient? Client => LazyClient.Value;

        public static bool IsConfigured => Client != null;

        // ---------------- Profile ----------------

        /// <summary>A random id made on first use. It is this player's row key on every board.</summary>
        public static string PlayerId
        {
            get
            {
                var s = Settings;
                if (s == null)
                    return "";
                string id = s.GetString("lb.id", "");
                if (id.Length == 0)
                {
                    id = Guid.NewGuid().ToString("N");
                    s.SetString("lb.id", id);
                    RhinoGotchiPlugin.Instance?.SaveSettings();
                }
                return id;
            }
        }

        public static string Name => Settings?.GetString("lb.name", "") ?? "";

        /// <summary>The player's icon as base64 image data, or "" if they have none.</summary>
        public static string Icon => Settings?.GetString("lb.icon", "") ?? "";

        public static string IconVersion => Version(Icon);

        /// <summary>Players only appear on the boards once they have picked a name.</summary>
        public static bool HasProfile => Name.Length > 0;

        /// <summary>Saves the profile locally. Call <see cref="SyncAsync"/> afterwards to upload it.</summary>
        public static void SetProfile(string name, string iconBase64)
        {
            var s = Settings;
            if (s == null)
                return;

            name = CleanName(name);
            if (iconBase64 != Icon)
                s.SetBool("lb.iconDirty", true);
            s.SetString("lb.name", name);
            s.SetString("lb.icon", iconBase64);
            RhinoGotchiPlugin.Instance?.SaveSettings();
        }

        public static string CleanName(string name)
        {
            name = string.Join(" ", name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return name.Length > LeaderboardClient.MaxNameLength ? name.Substring(0, LeaderboardClient.MaxNameLength).TrimEnd() : name;
        }

        // ---------------- Points ----------------

        public static int Points(string game) => Settings?.GetInteger("lb.points." + game, 0) ?? 0;

        public static int TotalPoints => Games.All.Sum(Points);

        /// <summary>
        /// Adds a daily game's points to this player's totals and syncs. Each game scores once per
        /// day (tracked apart from the stats, so resetting stats can't be used to score twice).
        /// Returns the points actually added.
        /// </summary>
        public static int Award(string game, int points)
        {
            var s = Settings;
            if (s == null || points <= 0 || s.GetString("lb.day." + game, "") == Daily.TodayKey)
                return 0;

            s.SetInteger("lb.points." + game, Points(game) + points);
            s.SetString("lb.day." + game, Daily.TodayKey);
            RhinoGotchiPlugin.Instance?.SaveSettings();
            Sync();
            return points;
        }

        // ---------------- Sync ----------------

        /// <summary>Pushes the profile and totals in the background. Failures are retried by the next sync.</summary>
        public static void Sync() => _ = SyncAsync();

        /// <summary>Pushes the profile and totals. Returns false if it could not (not set up, no name, offline).</summary>
        public static async Task<bool> SyncAsync()
        {
            var client = Client;
            var s = Settings;
            if (client == null || s == null || !HasProfile)
                return false;

            // Read everything on the calling (UI) thread, then go to the network.
            string id = PlayerId, name = Name, icon = Icon, version = IconVersion;
            bool sendIcon = s.GetBool("lb.iconDirty", false);
            var scores = new List<KeyValuePair<string, int>>();
            foreach (var game in Games.All)
                scores.Add(new KeyValuePair<string, int>(game, Points(game)));
            scores.Add(new KeyValuePair<string, int>(LeaderboardClient.GlobalBoard, TotalPoints));

            try
            {
                await client.PushAsync(id, name, version, scores, sendIcon ? icon : null).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                RhinoApp.InvokeOnUiThread(new Action(() =>
                    RhinoApp.WriteLine("RhinoGotchi: could not reach the leaderboard (" + ex.Message + ").")));
                return false;
            }

            if (sendIcon)
            {
                RhinoApp.InvokeOnUiThread(new Action(() =>
                {
                    // Only clear the flag if the icon was not changed again while this was in flight.
                    if (Icon == icon)
                    {
                        s.SetBool("lb.iconDirty", false);
                        RhinoGotchiPlugin.Instance?.SaveSettings();
                    }
                }));
            }
            return true;
        }

        /// <summary>A short fingerprint of the icon data (FNV-1a), used as a cache key by other players.</summary>
        private static string Version(string data)
        {
            if (data.Length == 0)
                return "";
            uint hash = 2166136261;
            foreach (char c in data)
                hash = unchecked((hash ^ c) * 16777619);
            return hash.ToString("x8", CultureInfo.InvariantCulture);
        }
    }
}
