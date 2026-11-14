using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using RhinoGotchi.Content;

namespace RhinoGotchi.Core
{
    internal enum Verdict
    {
        /// <summary>The command was used: points for how rare it is.</summary>
        Scored,
        NotUsed,
        /// <summary>The model declined to answer.</summary>
        NoVerdict,
        TimedOut,
    }

    internal sealed class JudgeResult
    {
        public string Command = "";
        public Verdict Verdict;
        public int Points;
        /// <summary>How likely the command is "niche and rare", 0 to 1.</summary>
        public double Rarity;
        /// <summary>How likely the command was used on this model, 0 to 1.</summary>
        public double Used;

        public string Tier =>
            Rarity >= 0.85 ? "Arcane" :
            Rarity >= 0.65 ? "Deep menu" :
            Rarity >= 0.45 ? "Off the toolbar" :
            Rarity >= 0.25 ? "Daily driver" : "Toolbar staple";
    }

    internal sealed class JudgeException : Exception
    {
        public JudgeException(string message, bool keyProblem) : base(message)
        {
            KeyProblem = keyProblem;
        }

        /// <summary>No key, or OpenAI rejected it (invalid, expired, revoked or out of credit).</summary>
        public bool KeyProblem { get; }
    }

    /// <summary>
    /// Scores a Deep Cut guess with the OpenAI Decisions API: one request with the guess and a picture of
    /// the viewport, three questions back. Points = 1000 × "is this command rare", but only if "was it used".
    /// </summary>
    internal static class DeepCutJudge
    {
        private const string Endpoint = "https://api.openai.com/v1/decisions";
        private const string Model = "gpt-6-luna";
        private const string UsedYes = "This command was used";
        private const string UsedNo = "This command was not used";
        private const string AskInList = "is the command in this list:\n";
        private const string AskRare = "This is a niche and rare rhino command. Few have used it.";
        private const string AskUsed = "Was this RhinoCommand used directly for modeling the displayed geometry? It must be actively involved in the specific model.";

        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };

        public static async Task<JudgeResult> Judge(string command, byte[] jpeg)
        {
            string key = ApiKey.Current;
            if (key.Length == 0)
                throw new JudgeException("There is no OpenAI key yet.", keyProblem: true);

            string status;
            string text;
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, Endpoint))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                    request.Content = new StringContent(RequestBody(command, jpeg), Encoding.UTF8, "application/json");
                    using (var response = await Http.SendAsync(request).ConfigureAwait(false))
                    {
                        text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        status = response.IsSuccessStatusCode ? "" : ((int)response.StatusCode).ToString();
                    }
                }
            }
            catch (TaskCanceledException)
            {
                throw new JudgeException("The judge took too long to answer.", keyProblem: false);
            }
            catch (HttpRequestException ex)
            {
                throw new JudgeException("Could not reach OpenAI: " + ex.Message, keyProblem: false);
            }

            object? body;
            try { body = MiniJson.Parse(text); }
            catch (FormatException) { body = null; }

            if (status.Length > 0)
            {
                string message = MiniJson.Get(body, "error", "message") as string ?? "HTTP " + status;
                string code = MiniJson.Get(body, "error", "code") as string ?? "";
                bool keyProblem = status == "401" || status == "403" || code == "insufficient_quota" || code == "invalid_api_key";
                throw new JudgeException(message, keyProblem);
            }

            var answers = (MiniJson.Get(body, "answers") as List<object?> ?? new List<object?>())
                .OfType<Dictionary<string, object?>>().ToList();
            Dictionary<string, object?> Answer(string name, int index) =>
                answers.FirstOrDefault(a => a.TryGetValue("name", out var n) && n as string == name)
                ?? (index < answers.Count ? answers[index] : new Dictionary<string, object?>());

            var rare = Answer("Decision 2", 1);
            var used = Answer("Decision 3", 2);
            var result = new JudgeResult { Command = command };
            if (new[] { rare, used }.Any(a => a.TryGetValue("type", out var t) && t as string == "refusal"))
            {
                result.Verdict = Verdict.NoVerdict;
                return result;
            }

            result.Rarity = rare.TryGetValue("probability", out var p) && p is double rarity ? rarity : 0;
            string choice = used.TryGetValue("choice", out var c) ? c as string ?? "" : "";
            bool wasUsed = choice == UsedYes;
            double confidence = used.TryGetValue("confidence", out var cf) && cf is double conf ? conf : 1;
            result.Used = wasUsed ? confidence : 1 - confidence;
            if (used.TryGetValue("probabilities", out var probabilities) && probabilities is List<object?> list)
            {
                foreach (var item in list.OfType<Dictionary<string, object?>>())
                    if (item.TryGetValue("value", out var v) && v as string == UsedYes && item.TryGetValue("probability", out var up) && up is double usedP)
                        result.Used = usedP;
            }

            result.Verdict = wasUsed ? Verdict.Scored : Verdict.NotUsed;
            result.Points = wasUsed ? Math.Max(25, (int)Math.Round(1000 * result.Rarity)) : 0;
            return result;
        }

        private static string RequestBody(string command, byte[] jpeg)
        {
            string image = "data:image/jpeg;base64," + Convert.ToBase64String(jpeg);
            string inList = AskInList + string.Join("\n", DeepCutModels.Commands) + "\n";
            var q = MiniJson.Quote;
            return "{\"model\":" + q(Model) +
                ",\"input\":[{\"role\":\"user\",\"content\":[" +
                "{\"type\":\"input_text\",\"text\":" + q("command: \"" + command + "\"") + "}," +
                "{\"type\":\"input_image\",\"image_url\":" + q(image) + "}]}]" +
                ",\"questions\":[" +
                "{\"type\":\"choice\",\"instructions\":" + q(inList) + ",\"choices\":[{\"value\":\"Yes\"},{\"value\":\"No\"}],\"name\":\"Decision 1\"}," +
                "{\"type\":\"predicate\",\"instructions\":" + q(AskRare) + ",\"name\":\"Decision 2\"}," +
                "{\"type\":\"choice\",\"instructions\":" + q(AskUsed) + ",\"choices\":[{\"value\":" + q(UsedYes) + "},{\"value\":" + q(UsedNo) + "}],\"name\":\"Decision 3\"}" +
                "]}";
        }
    }
}
