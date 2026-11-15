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
            Padding = new Padding(16, 12, 16, 16);
            Spacing = 12;
            HorizontalContentAlignment = HorizontalAlignment.Stretch;

            UpdatePet();
            Avatars.KeepOwnIconCurrent(); // the leaderboard icon is this Gotchi
            Items.Add(_pet);

            var tabs = new ArtTabSwitch(_lastTab);
            tabs.SelectedChanged += (s, e) => ShowTab(tabs.Selected);
            Items.Add(tabs);
            Items.Add(_tabContent);
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

        private void LearningTab()
        {
            _tabContent.Items.Add(new SectionLabel("LEARN RHINO"));
            _tabContent.Items.Add(new ListRow("Learn Projects", "Road · facade · building", Glyph.Star, 0, _host.ShowProjects));

            var cats = CommandCatalog.Categories;
            int commands = cats.Sum(c => c.Commands.Count);
            int demos = cats.Sum(c => c.Commands.Count(e => e.HasDemo));
            _tabContent.Items.Add(new ListRow("Learn Commands", $"{commands} commands · {demos} demos", Glyph.Book, 1,
                () => _host.Navigate(new LibraryView(_host, 0))));

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
        /// A two-column grid of game cards: a click on a card opens the game (there are no Play buttons).
        /// Settings and Reset live on the My Gotchi tab.
        /// </summary>
        private void GamesTab()
        {
            // Water Run is played level by level and Drone School lesson by lesson, so they count progress.
            int levels = Water.WaterLevelCatalog.All.Count;
            int cleared = Water.WaterLevelCatalog.All.Count(l => Water.WaterGame.IsDone(l.Number));
            int lessons = Drone.SchoolLevels.All.Length, learned = Drone.DroneProgress.DoneCount;

            var tiles = new[]
            {
                DailyTile(Games.Order, "card-build-order", () => _host.Navigate(new BuildOrderView(_host, practice: false))),
                DailyTile(Games.Shape, "card-what-made-this", () => _host.Navigate(new ShapeQuizView(_host, practice: false))),
                DailyTile(Games.Deep, "card-deep-cut", () => _host.Navigate(new DeepCutView(_host, practice: false)), timed: true),
                DailyTile(Games.Blocked, "card-blocked", () => _host.Navigate(new BlockedView(_host, practice: false))),
                new GameTile("card-water-run", "Water Run", cleared >= levels ? "All levels done" : $"{cleared} of {levels} levels",
                    () => _host.Navigate(new WaterLabView(_host))) { StatusDone = cleared >= levels },
                new GameTile("card-drone-school", "Drone School", learned >= lessons ? "All lessons done" : $"{learned} of {lessons} lessons",
                    () => _host.Navigate(new DroneSchoolView(_host))) { StatusDone = learned >= lessons },
            };
            _tabContent.Items.Add(new GameGrid(tiles));
        }

        /// <summary>A daily game's card: "Done today" once today's round is played; Deep Cut also says it is timed.</summary>
        private static GameTile DailyTile(string game, string card, Action open, bool timed = false)
        {
            bool done = Core.Progress.Get(game).PlayedToday;
            string status = done ? "Done today" : timed ? "Daily · 30 s timer" : "Daily";
            return new GameTile(card, Games.Title(game), status, open) { StatusDone = done, StatusTimed = timed && !done };
        }

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
