using System;
using Eto.Forms;
using RhinoGotchi.Core;

namespace RhinoGotchi.UI
{
    internal sealed class StatsView : StackLayout, INavPage
    {
        public NavTab Tab => NavTab.Stats;

        public StatsView(RhinoGotchiPanel host)
        {
            Theme.SetUpPage(this, host, "My stats", "Only daily games count. Practice rounds are just for fun.");

            foreach (var game in Games.All)
            {
                var s = Progress.Get(game);
                int winRate = s.Played == 0 ? 0 : (int)Math.Round(100.0 * s.Wins / s.Played);
                Items.Add(Theme.Heading(Games.Title(game)));
                Items.Add(Theme.Body(
                    $"Played {s.Played} · Won {winRate}% · Streak {s.CurrentStreak} · Best {s.BestStreak}"));
                if (s.LastResult.Length > 0)
                    Items.Add(Theme.Muted($"Last result ({s.LastPlayedDay}): {s.LastResult}"));
            }
        }
    }
}
