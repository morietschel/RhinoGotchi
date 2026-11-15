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
    /// The panel's start page: title and date, Gotchi's card (streak, hats, solved), then two tabs.
    /// Learning has the Learning Campaign, the Command Library, recipes and the tip of the day;
    /// Games has today's daily puzzles. Everything stretches with the panel.
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
            var title = new Label { Text = "RhinoGotchi", Font = new Font(SystemFont.Bold, 20), TextColor = p.Text };
            var date = new Label { Text = DateTime.Today.ToString("ddd, MMM d"), Font = new Font(SystemFont.Default, 9.5f), TextColor = p.SubtleText };
            Items.Add(new TableLayout
            {
                Rows = { new TableRow(new TableCell(title, true), new TableCell(date)) { } },
            });
            UpdatePet();
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
            int hats = Achievements.UnlockedCount(stats);

            Mood mood;
            string message;
            if (left == 0 && GotchiState.ActiveToday)
            {
                mood = Mood.Celebrate;
                message = "All of today's dailies done! You're on fire.";
            }
            else if (GotchiState.ActiveToday)
            {
                mood = Mood.Happy;
                message = left == 1 ? "Nice work! One daily puzzle left today." : "Great to see you! Keep the streak going.";
            }
            else
            {
                mood = Mood.Sleepy;
                message = streak > 0
                    ? "Zzz... finish a lesson or a daily to keep your streak!"
                    : "Zzz... finish a lesson or a daily to wake me up!";
            }

            string status = left == 0 ? "all dailies done" : $"{left} {(left == 1 ? "daily" : "dailies")} left today";
            _pet.Set(mood, status, message, 0,
                $"{streak}-day streak",
                $"{hats}/{Achievements.All.Count} unlocked",
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
            string detail = progress.IsNew
                ? "New? Start at Level 1 or take a placement test"
                : current < 0 ? "All levels done · replay any time"
                : $"Level {current + 1} · {LessonCatalog.All[current].Title}";

            _tabContent.Items.Add(new SectionLabel("LEARN RHINO", "from your first Line to a final exam"));
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

            var tip = new TipCard("TIP OF THE DAY · TAP FOR ANOTHER", Tips.All[_tip]);
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
            int done = Games.All.Count(g => Core.Progress.Get(g).PlayedToday);
            _tabContent.Items.Add(new SectionLabel($"DAILIES · {DateTime.Today:MMM d}".ToUpperInvariant(), $"{done} of {Games.All.Length} done"));

            _tabContent.Items.Add(GameRow(Games.Order, "Put the commands in order", Glyph.Blocks, 0,
                () => _host.Navigate(new BuildOrderView(_host, practice: false))));
            _tabContent.Items.Add(GameRow(Games.Shape, "Name the command that made it", Glyph.Eye, 3,
                () => _host.Navigate(new ShapeQuizView(_host, practice: false))));
            _tabContent.Items.Add(GameRow(Games.Deep, "Name the rarest command that built it", Glyph.Cube, 4,
                () => _host.Navigate(new DeepCutView(_host, practice: false))));
            _tabContent.Items.Add(GameRow(Games.Blocked, "Build it without the obvious command", Glyph.Ban, 2,
                () => _host.Navigate(new BlockedView(_host, practice: false))));

            _tabContent.Items.Add(new SectionLabel("GUMBALL", "learn it by playing"));
            _tabContent.Items.Add(new ListRow("Water Run", "12 levels · deliver the water with the Gumball", Glyph.Arrows, 1,
                () => _host.Navigate(new WaterLabView(_host))));

            _tabContent.Items.Add(new SectionLabel("SKILLS", "learn the mouse"));
            _tabContent.Items.Add(new ListRow("Drone School",
                $"Learn to zoom, pan, orbit and fly · {Drone.DroneProgress.DoneCount} of {Drone.SchoolLevels.All.Length} lessons",
                Glyph.Mouse, 5, () => _host.Navigate(new DroneSchoolView(_host)))
            {
                Right = "›",
            });
            _tabContent.Items.Add(new SectionLabel("YOUR PROGRESS"));
            var achievements = new ListRow("Trophies, leaderboard & stats",
                $"{Achievements.UnlockedCount(GotchiState.Game.Stats)} of {Achievements.All.Count} unlocked", Glyph.Star, 2, _host.ShowTrophies)
            {
                Right = "›",
            };
            _tabContent.Items.Add(achievements);
            _tabContent.Items.Add(Theme.Grid(2,
                Theme.Button("Settings", () => _host.Navigate(new SettingsView(_host))),
                Theme.Button("Reset", () => ConfirmReset(_host))));
        }

        private static ListRow GameRow(string game, string blurb, Glyph glyph, int tint, Action play)
        {
            var stats = Core.Progress.Get(game);
            string detail = stats.PlayedToday ? $"Today: {stats.LastResult}" : blurb;
            if (stats.CurrentStreak > 0)
                detail += $" · {stats.CurrentStreak}-day streak";
            return new ListRow(Games.Title(game), detail, glyph, tint, play)
            {
                TagText = stats.PlayedToday ? "DONE TODAY" : "PLAY",
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
