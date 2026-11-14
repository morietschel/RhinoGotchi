using System;
using Eto.Forms;
using RhinoGotchi.Content;
using RhinoGotchi.Core;

namespace RhinoGotchi.UI
{
    /// <summary>The panel's start page: today's games and quick actions.</summary>
    internal sealed class HomeView : StackLayout
    {
        private int _tip = Tips.TodayIndex;

        public HomeView(RhinoGotchiPanel host)
        {
            Padding = 12;
            Spacing = 10;
            HorizontalContentAlignment = HorizontalAlignment.Stretch;

            Items.Add(Theme.Title("RhinoGotchi"));
            Items.Add(Theme.Muted($"Daily games for learning Rhino · {DateTime.Today:dddd, MMMM d}"));

            Items.Add(Theme.Heading("Today's game"));
            Items.Add(GameCard(Games.Shape, "Look at the viewport and name the command that made the shape.",
                () => host.Navigate(new ShapeQuizView(host, practice: false))));

            Items.Add(Theme.Heading("Tip of the day"));
            var tip = Theme.Body(Tips.All[_tip]);
            Items.Add(tip);
            Items.Add(new StackLayoutItem(Theme.Button("Another tip", () =>
            {
                _tip = (_tip + 1) % Tips.All.Length;
                tip.Text = Tips.All[_tip];
            }), HorizontalAlignment.Left));

            Items.Add(Theme.Heading("More"));
            Items.Add(Theme.Row(
                Theme.Button("My stats", () => host.Navigate(new StatsView(host))),
                Theme.Button("Reset progress", () => ConfirmReset(host))));
        }

        private static Control GameCard(string game, string blurb, Action play)
        {
            var stats = Progress.Get(game);
            string status = stats.PlayedToday
                ? $"Done today: {stats.LastResult}"
                : "Not played today";
            if (stats.CurrentStreak > 0)
                status += $" · {stats.CurrentStreak}-day streak";

            var title = new Label { Text = Games.Title(game), Font = Theme.HeadingFont };
            var playButton = Theme.Button(stats.PlayedToday ? "View" : "Play", play);

            var body = new StackLayout
            {
                Spacing = 3,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Items = { title, Theme.Body(blurb), Theme.Muted(status) },
            };
            var layout = new TableLayout
            {
                Spacing = new Eto.Drawing.Size(8, 0),
                Rows = { new TableRow(new TableCell(body, true), new TableCell(playButton)) },
            };
            return new GroupBox { Content = layout, Padding = 8 };
        }

        private static void ConfirmReset(RhinoGotchiPanel host)
        {
            var answer = MessageBox.Show(
                "Clear all RhinoGotchi stats and streaks?",
                "RhinoGotchi",
                MessageBoxButtons.YesNo,
                MessageBoxType.Question);
            if (answer != DialogResult.Yes)
                return;
            Progress.Reset();
            host.ShowHome();
        }
    }
}
