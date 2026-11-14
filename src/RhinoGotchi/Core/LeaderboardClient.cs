using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace RhinoGotchi.Core
{
    /// <summary>One row of a leaderboard.</summary>
    internal sealed class BoardEntry
    {
        public string PlayerId = "";
        public string Name = "";
        public int Score;
        /// <summary>Changes whenever the player's icon changes; empty when they have none.</summary>
        public string IconVersion = "";
    }

    /// <summary>
    /// Talks to the shared leaderboard: a Firebase Realtime Database used over plain REST.
    /// No Rhino types here, so any game (or a test harness) can reuse this file as is.
    ///
    /// Data layout:
    ///   boards/{board}/{playerId} = { "s": score, "n": name, "i": iconVersion, "t": serverTime }
    ///   icons/{playerId}          = "base64 image"
    /// A board is a game key ("shape", "order", ...) or "global" for the total across games.
    /// </summary>
    internal sealed class LeaderboardClient
    {
        public const string GlobalBoard = "global";
        public const int MaxNameLength = 20;
        public const int MaxIconChars = 20000;

        private static readonly HttpClient SharedHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        private static readonly HttpMethod Patch = new HttpMethod("PATCH");

        private readonly string _baseUrl;
        private readonly HttpClient _http;

        public LeaderboardClient(string databaseUrl, HttpClient? http = null)
        {
            _baseUrl = databaseUrl.TrimEnd('/');
            _http = http ?? SharedHttp;
        }

        /// <summary>The best <paramref name="count"/> players on a board, highest score first.</summary>
        public async Task<List<BoardEntry>> GetTopAsync(string board, int count)
        {
            string url = $"{_baseUrl}/boards/{Uri.EscapeDataString(board)}.json?orderBy=%22s%22&limitToLast={count}";
            string json = await GetAsync(url).ConfigureAwait(false);

            var entries = new List<BoardEntry>();
            if (MiniJson.Parse(json) is Dictionary<string, object?> rows)
            {
                foreach (var row in rows)
                {
                    if (!(row.Value is Dictionary<string, object?> fields))
                        continue;
                    entries.Add(new BoardEntry
                    {
                        PlayerId = row.Key,
                        Name = fields.TryGetValue("n", out var n) && n is string name ? name : "?",
                        Score = fields.TryGetValue("s", out var s) && s is double score ? (int)score : 0,
                        IconVersion = fields.TryGetValue("i", out var i) && i is string icon ? icon : "",
                    });
                }
            }
            // The REST API filters by score but returns an unordered object, so sort here.
            return entries
                .OrderByDescending(e => e.Score)
                .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>A player's icon as base64 image data, or null if they have none.</summary>
        public async Task<string?> GetIconAsync(string playerId)
        {
            string json = await GetAsync($"{_baseUrl}/icons/{Uri.EscapeDataString(playerId)}.json").ConfigureAwait(false);
            return MiniJson.Parse(json) as string;
        }

        /// <summary>
        /// Writes a player's row on every board in <paramref name="scores"/> in one request.
        /// Pass <paramref name="iconBase64"/> to upload the icon too ("" removes it, null leaves it alone).
        /// </summary>
        public async Task PushAsync(string playerId, string name, string iconVersion,
            IEnumerable<KeyValuePair<string, int>> scores, string? iconBase64)
        {
            var body = new StringBuilder("{");
            foreach (var board in scores)
            {
                body.Append(MiniJson.Quote($"boards/{board.Key}/{playerId}"))
                    .Append(":{\"s\":").Append(board.Value.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"n\":").Append(MiniJson.Quote(name))
                    .Append(",\"i\":").Append(MiniJson.Quote(iconVersion))
                    .Append(",\"t\":{\".sv\":\"timestamp\"}},");
            }
            if (iconBase64 != null)
            {
                body.Append(MiniJson.Quote($"icons/{playerId}")).Append(':')
                    .Append(iconBase64.Length == 0 ? "null" : MiniJson.Quote(iconBase64)).Append(',');
            }
            if (body.Length == 1)
                return;
            body.Length--; // trailing comma
            body.Append('}');

            using var request = new HttpRequestMessage(Patch, $"{_baseUrl}/.json?print=silent")
            {
                Content = new StringContent(body.ToString(), Encoding.UTF8, "application/json"),
            };
            using var response = await _http.SendAsync(request).ConfigureAwait(false);
            await EnsureOk(response).ConfigureAwait(false);
        }

        private async Task<string> GetAsync(string url)
        {
            using var response = await _http.GetAsync(url).ConfigureAwait(false);
            await EnsureOk(response).ConfigureAwait(false);
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        private static async Task EnsureOk(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
                return;
            string detail = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new HttpRequestException($"Leaderboard server said {(int)response.StatusCode}: {detail}");
        }
    }
}
