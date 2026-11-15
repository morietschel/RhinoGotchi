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
    /// The panel's start page: Gotchi's card (streak, solved), then two tabs. Learning has the Learning
    /// Campaign, the Command Library and the tip of the day; Games has the games. Words are kept to a
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

            var p = Palette.Current;
            UpdatePet();
            Avatars.KeepOwnIconCurrent(); // the leaderboard icon is this Gotchi
            Items.Add(_pet);

            var tabs = new TabBar(_lastTab, ("Learning", Glyph.Book, p.Ink), ("Games", Glyph.Gamepad, p.Ink));
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
            var progress = GotchiState.Game.Progress;
            int total = LessonCatalog.All.Count;
            int current = progress.CurrentLevel;
            string detail = progress.IsNew ? ""
                : current < 0 ? "All levels done"
                : $"Level {current + 1} · {LessonCatalog.All[current].Title}";

            _tabContent.Items.Add(new SectionLabel("LEARN RHINO"));
            _tabContent.Items.Add(new ListRow("Learning Campaign", detail, Glyph.Star, 0, _host.ShowLessons)
            {
                Emphasis = current >= 0,
                Right = $"{progress.DoneCount}/{total}",
                RightAccent = true,
            });

            var cats = CommandCatalog.Categories;
            int commands = cats.Sum(c => c.Commands.Count);
            int demos = cats.Sum(c => c.Commands.Count(e => e.HasDemo));
            _tabContent.Items.Add(new ListRow("Command Library", $"{commands} commands · {demos} demos", Glyph.Book, 1,
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

        private void GamesTab()
        {
            _tabContent.Items.Add(GameRow(Games.Order, Glyph.Blocks, 0,
                () => _host.Navigate(new BuildOrderView(_host, practice: false))));
            _tabContent.Items.Add(GameRow(Games.Shape, Glyph.Eye, 3,
                () => _host.Navigate(new ShapeQuizView(_host, practice: false))));
            _tabContent.Items.Add(GameRow(Games.Deep, Glyph.Cube, 4,
                () => _host.Navigate(new DeepCutView(_host, practice: false))));
            _tabContent.Items.Add(GameRow(Games.Blocked, Glyph.Ban, 2,
                () => _host.Navigate(new BlockedView(_host, practice: false))));

            // Water Run is played level by level, not once a day, so its tag counts levels.
            int levels = Water.WaterLevelCatalog.All.Count;
            int cleared = Water.WaterLevelCatalog.All.Count(l => Water.WaterGame.IsDone(l.Number));
            _tabContent.Items.Add(new ListRow("Water Run", "", Glyph.Arrows, 1,
                () => _host.Navigate(new WaterLabView(_host)))
            {
                TagText = cleared == levels ? "DONE" : cleared == 0 ? "PLAY" : $"{cleared}/{levels}",
                TagDone = cleared == levels,
            });

            // Drone School (mouse lessons) is counted the same way.
            int lessons = Drone.SchoolLevels.All.Length, learned = Drone.DroneProgress.DoneCount;
            _tabContent.Items.Add(new ListRow("Drone School", "", Glyph.Mouse, 5, () => _host.Navigate(new DroneSchoolView(_host)))
            {
                TagText = learned >= lessons ? "DONE" : learned == 0 ? "PLAY" : $"{learned}/{lessons}",
                TagDone = learned >= lessons,
            });

            // Trophies, the leaderboard and stats have their own tab at the bottom.
            _tabContent.Items.Add(Theme.Grid(2,
                Theme.Button("Settings", () => _host.Navigate(new SettingsView(_host))),
                Theme.Button("Reset", () => ConfirmReset(_host))));
        }

        private static ListRow GameRow(string game, Glyph glyph, int tint, Action play)
        {
            var stats = Core.Progress.Get(game);
            return new ListRow(Games.Title(game), "", glyph, tint, play)
            {
                TagText = stats.PlayedToday ? "DONE" : "PLAY",
                TagDone = stats.PlayedToday,
            };
        }

        private static void ConfirmReset(RhinoGotchiPanel host)
        {
            var answer = MessageBox.Show(
                "Clear the daily-game stats and streaks? Lesson progress and hats are kept.",
                "RhinoGotchi",
                MessageBoxButtons.YesNo,
                MessageBoxType.Question);
            if (answer != DialogResult.Yes)
                return;
            Core.Progress.Reset();
            host.ShowHome();
        }
    }
}
