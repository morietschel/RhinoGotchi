using System;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using RhinoGotchi.Content;
using RhinoGotchi.Core;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// The panel's start page: the mascot, then two tabs. Games has today's puzzles and stats;
    /// Learning has the tip of the day and the Command Library topics.
    /// </summary>
    internal sealed class HomeView : StackLayout
    {
        /// <summary>Remembered so coming back from a game or the Library returns to the same tab.</summary>
        private static int _lastTab;

        private readonly RhinoGotchiPanel _host;
        private readonly MascotHeader _mascot = new MascotHeader("");
        private readonly Panel _tabContent = new Panel();
        private int _tip = Tips.TodayIndex;

        public HomeView(RhinoGotchiPanel host)
        {
            _host = host;
            Padding = 14;
            Spacing = 12;
            HorizontalContentAlignment = HorizontalAlignment.Stretch;

            var p = Palette.Current;
            var tabs = new TabBar(_lastTab, ("Games", Glyph.Gamepad, p.Green), ("Learning", Glyph.Book, p.Blue));
            tabs.SelectedChanged += (s, e) => ShowTab(tabs.Selected);

            var title = new Label { Text = "RhinoGotchi", Font = new Font(SystemFont.Bold, 18), TextColor = p.Text };
            var date = Theme.Muted(DateTime.Today.ToString("dddd, MMMM d"));
            Items.Add(new StackLayout { Spacing = 0, Items = { title, date } });
            Items.Add(_mascot);
            Items.Add(tabs);
            Items.Add(_tabContent);
            ShowTab(_lastTab);
        }

        private void ShowTab(int tab)
        {
            _lastTab = tab;
            _tabContent.Content = tab == 0 ? GamesTab() : LearningTab();
        }

        // ---------------- Games ----------------

        private Control GamesTab()
        {
            var p = Palette.Current;
            int left = Games.All.Count(g => !Progress.Get(g).PlayedToday);
            if (left == 0)
                _mascot.Say("You finished all of today's games! Come back tomorrow, or keep practicing.", MascotMood.Excited);
            else
                _mascot.Say(left == Games.All.Length
                    ? "Hi, I'm Rhino! Two fresh puzzles are waiting for you today."
                    : "Nice work! One more puzzle to go today.", MascotMood.Happy);

            var stack = new StackLayout { Spacing = 12, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            stack.Items.Add(GameCard(Games.Order, Glyph.Blocks, p.Orange,
                "See a finished object and put the commands that built it in the right order.",
                () => _host.Navigate(new BuildOrderView(_host, practice: false))));
            stack.Items.Add(GameCard(Games.Shape, Glyph.Eye, p.Blue,
                "Look at the viewport and name the command that made the shape.",
                () => _host.Navigate(new ShapeQuizView(_host, practice: false))));

            int bestStreak = Games.All.Max(g => Progress.Get(g).CurrentStreak);
            int played = Games.All.Sum(g => Progress.Get(g).Played);
            var statsRow = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Items =
                {
                    new Pill($"{bestStreak}-day streak", p.Orange, Glyph.Flame),
                    new Pill($"{played} played", p.Purple, Glyph.Star),
                },
            };
            stack.Items.Add(Theme.Box(new StackLayout
            {
                Spacing = 8,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Items =
                {
                    new TableLayout
                    {
                        Spacing = new Size(10, 0),
                        Rows = { new TableRow(new Badge(Glyph.Chart, p.Purple, 40), new TableCell(new StackLayout
                        {
                            Spacing = 4,
                            Items = { Theme.Heading("Your progress"), statsRow },
                        }, true)) },
                    },
                    Theme.Grid(2,
                        Theme.Secondary("My stats", () => _host.Navigate(new StatsView(_host))),
                        Theme.Button("Reset", () => ConfirmReset(_host))),
                },
            }));
            return stack;
        }

        private static Control GameCard(string game, Glyph glyph, Color color, string blurb, Action play)
        {
            var p = Palette.Current;
            var stats = Progress.Get(game);

            var pills = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 6 };
            pills.Items.Add(stats.PlayedToday
                ? new Pill($"Done · {stats.LastResult}", p.Green, Glyph.Star)
                : new Pill("New today!", p.Blue));
            if (stats.CurrentStreak > 0)
                pills.Items.Add(new Pill($"{stats.CurrentStreak}-day streak", p.Orange, Glyph.Flame));

            var header = new TableLayout
            {
                Spacing = new Size(12, 0),
                Rows =
                {
                    new TableRow(
                        new TableCell(new Badge(glyph, color, 52)),
                        new TableCell(new StackLayout
                        {
                            Spacing = 3,
                            HorizontalContentAlignment = HorizontalAlignment.Stretch,
                            Items = { Theme.Heading(Games.Title(game)), Theme.Muted(blurb) },
                        }, true)),
                },
            };
            var playButton = stats.PlayedToday
                ? Theme.Button("See today's result", play)
                : Theme.Primary("Play", play);
            playButton.Height = 42;

            return Theme.Box(new StackLayout
            {
                Spacing = 10,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Items = { header, pills, playButton },
            }, padding: 12);
        }

        // ---------------- Learning ----------------

        private Control LearningTab()
        {
            var p = Palette.Current;
            _mascot.Say("Let's learn some commands! Pick a topic, watch how it works, then try it.", MascotMood.Thinking);

            var stack = new StackLayout { Spacing = 12, HorizontalContentAlignment = HorizontalAlignment.Stretch };

            // Tip of the day.
            var tipText = Theme.Body(Tips.All[_tip]);
            stack.Items.Add(new StyledCard(new StackLayout
            {
                Spacing = 8,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Items =
                {
                    new TableLayout
                    {
                        Spacing = new Size(10, 0),
                        Rows = { new TableRow(new Badge(Glyph.Bulb, p.Gold, 40), new TableCell(Theme.Heading("Tip of the day"), true)) },
                    },
                    tipText,
                    new StackLayoutItem(Theme.Button("Another tip", () =>
                    {
                        _tip = (_tip + 1) % Tips.All.Length;
                        tipText.Text = Tips.All[_tip];
                    }), HorizontalAlignment.Left),
                },
            }, Theme.Mix(p.Gold, p.Background, 0.85f), p.Gold, 12));

            // Library topics.
            stack.Items.Add(Theme.Heading("Command Library"));
            var cats = CommandCatalog.Categories;
            int recipeCount = global::RhinoGotchi.Content.Recipes.All.Length;
            var topics = new (string Name, string Detail, Glyph Glyph, Color Color)[]
            {
                (cats[0].Name, Detail(cats[0]), Glyph.Curve, p.Blue),
                (cats[1].Name, Detail(cats[1]), Glyph.Cube, p.Purple),
                (cats[2].Name, Detail(cats[2]), Glyph.Arrows, p.Orange),
                ("Recipes", $"{recipeCount} chains of commands", Glyph.Chain, p.Green),
            };
            var tiles = topics.Select((t, i) => (Control)TopicTile(t.Name, t.Detail, t.Glyph, t.Color, i)).ToArray();
            stack.Items.Add(Theme.Grid(2, tiles));
            stack.Items.Add(Theme.Secondary("Search all commands", () => _host.Navigate(new LibraryView(_host, 0))));
            return stack;
        }

        private static string Detail(CommandCategory c) =>
            $"{c.Commands.Count} commands · {c.Commands.Count(e => e.HasDemo)} demos";

        private Control TopicTile(string name, string detail, Glyph glyph, Color color, int tab)
        {
            var badge = new Badge(glyph, color, 46);
            var title = new Label { Text = name, Font = Theme.HeadingFont, TextColor = Palette.Current.Text, TextAlignment = TextAlignment.Center };
            var info = new Label { Text = detail, Font = new Font(SystemFont.Default, 8.5f), TextColor = Palette.Current.SubtleText, TextAlignment = TextAlignment.Center, Wrap = WrapMode.Word };
            var content = new StackLayout
            {
                Spacing = 6,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Items = { badge, title, info },
            };
            var tile = new StyledCard(content, padding: 10) { Hoverable = true };
            EventHandler<MouseEventArgs> open = (s, e) => _host.Navigate(new LibraryView(_host, tab));
            foreach (var c in new Control[] { tile, content, badge, title, info })
                c.MouseDown += open;
            return tile;
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
