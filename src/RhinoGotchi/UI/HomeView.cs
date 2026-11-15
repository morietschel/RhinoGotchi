using System;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using RhinoGotchi.Content;
using RhinoGotchi.Core;
using RhinoGotchi.Lessons;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// The panel's start page: Gotchi's card (streak, solved), then two tabs. Learning has Learn Projects,
    /// Learn Commands and the tip of the day; Games has the games. Words are kept to a
    /// minimum: titles and tags, no blurbs. Everything stretches with the panel.
    /// </summary>
    internal sealed class HomeView : StackLayout, INavPage
    {
        /// <summary>Remembered so coming back from a game or the Library returns to the same tab.</summary>
        private static int _lastTab;

        private readonly RhinoGotchiPanel _host;
        private readonly PetCard _pet = new PetCard();
        private readonly StackLayout _tabContent = new StackLayout { Spacing = 8, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        private int _tip = Tips.TodayIndex;

        public HomeView(RhinoGotchiPanel host)
        {
            _host = host;
            Padding = new Padding(14, 10, 14, 10);
            Spacing = 10;
            HorizontalContentAlignment = HorizontalAlignment.Stretch;

            UpdatePet();
            Avatars.KeepOwnIconCurrent(); // the leaderboard icon is this Gotchi
            Items.Add(_pet);

            var tabs = new ArtTabSwitch(_lastTab);
            tabs.SelectedChanged += (s, e) => ShowTab(tabs.Selected);
            Items.Add(tabs);
            // The tab content takes whatever height is left, so the Games grid can fit the panel without scrolling.
            Items.Add(new StackLayoutItem(_tabContent, true));
            ShowTab(_lastTab);
        }

        public NavTab Tab => NavTab.Home;

        private void UpdatePet()
        {
            var game = GotchiState.Game;
            var stats = game.Stats;
            int left = Games.All.Count(g => !Core.Progress.Get(g).PlayedToday);
            int streak = GotchiState.Streak;

            // Gotchi's face says how the day is going; no sentence needed.
            Mood mood = !GotchiState.ActiveToday ? Mood.Sleepy : left == 0 ? Mood.Celebrate : Mood.Happy;
            _pet.Set(mood, "", "", 0,
                $"{streak}-day streak",
                $"{stats.CorrectAnswers} solved");
        }

        private void ShowTab(int tab)
        {
            _lastTab = tab;
            _tabContent.Items.Clear();
            if (tab == 0)
                LearningTab();
            else
                GamesTab();
        }

        // ---------------- Learning ----------------

        /// <summary>Two painted cards (Learn Projects, Learn Commands) and the tip of the day.</summary>
        private void LearningTab()
        {
            var tiles = new[]
            {
                new GameTile("card-learn-projects", "Learn Projects", _host.ShowProjects),
                new GameTile("card-learn-commands", "Learn Commands", () => _host.Navigate(new LibraryView(_host, 0))),
            };
            _tabContent.Items.Add(new GameGrid(tiles));

            var tip = new TipCard("TIP OF THE DAY", Tips.All[_tip]);
            tip.MouseUp += (s, e) =>
            {
                _tip = (_tip + 1) % Tips.All.Length;
                tip.Text = Tips.All[_tip];
            };
            _tabContent.Items.Add(new StackLayoutItem(tip) { });
        }

        // ---------------- Games ----------------

        /// <summary>
        /// A two-column grid of game cards that fills the rest of the panel, so the page never scrolls.
        /// A click on a card opens the game; a green check marks a game done today (or fully done).
        /// Settings and Reset live on the My Gotchi tab.
        /// </summary>
        private void GamesTab()
        {
            int levels = Water.WaterLevelCatalog.All.Count;
            int cleared = Water.WaterLevelCatalog.All.Count(l => Water.WaterGame.IsDone(l.Number));
            int lessons = Drone.SchoolLevels.All.Length, learned = Drone.DroneProgress.DoneCount;

            var tiles = new[]
            {
                DailyTile(Games.Order, "card-build-order", () => _host.Navigate(new BuildOrderView(_host, practice: false))),
                DailyTile(Games.Shape, "card-what-made-this", () => _host.Navigate(new ShapeQuizView(_host, practice: false))),
                DailyTile(Games.Deep, "card-deep-cut", () => _host.Navigate(new DeepCutView(_host, practice: false))),
                DailyTile(Games.Blocked, "card-blocked", () => _host.Navigate(new BlockedView(_host, practice: false))),
                new GameTile("card-water-run", "Water Run", () => _host.Navigate(new WaterLabView(_host))) { Done = cleared >= levels },
                new GameTile("card-drone-school", "Drone School", () => _host.Navigate(new DroneSchoolView(_host))) { Done = learned >= lessons },
            };
            _tabContent.Items.Add(new StackLayoutItem(new GameGrid(tiles, fillHeight: true), true));
        }

        private static GameTile DailyTile(string game, string card, Action open) =>
            new GameTile(card, Games.Title(game), open) { Done = Core.Progress.Get(game).PlayedToday };

        /// <summary>Asks, then clears the daily-game stats and streaks; <paramref name="after"/> refreshes the page.</summary>
        internal static void ConfirmReset(Action after)
        {
            var answer = MessageBox.Show(
                "Clear the daily-game stats and streaks? Lesson progress and hats are kept.",
                "RhinoGotchi",
                MessageBoxButtons.YesNo,
                MessageBoxType.Question);
            if (answer != DialogResult.Yes)
                return;
            Core.Progress.Reset();
            after();
        }
    }
}
