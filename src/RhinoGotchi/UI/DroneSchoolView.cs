using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using RhinoGotchi.Drone;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// Drone School: twelve short lessons on the mouse and keyboard moves for getting around Rhino.
    /// Each lesson: learn the move, do it in the viewport (a live mouse picture shows what you're
    /// pressing), and the drone locks on so you can pop it. The flying lessons use the Gumball and nudge keys.
    /// </summary>
    internal sealed class DroneSchoolView : StackLayout, IGameView
    {
        private readonly RhinoGotchiPanel _host;
        private readonly RhinoDoc? _doc;
        private SchoolGame? _game;
        private int _index;
        private bool _won;

        private readonly Panel _picker = new Panel();
        private readonly Panel _card = new Panel();
        private readonly GameHud _hud = new GameHud { Visible = false };
        private readonly Words _message = Theme.Body("");
        private readonly Panel _actions = new Panel();
        private readonly StackLayout _result = new StackLayout { Spacing = 8, HorizontalContentAlignment = HorizontalAlignment.Stretch };

        public DroneSchoolView(RhinoGotchiPanel host, int? level = null)
        {
            _host = host;
            _doc = RhinoDoc.ActiveDoc;
            _index = level ?? DroneProgress.NextIndex();

            Theme.SetUpPage(this, host, "Drone School",
                "Learn the mouse moves for getting around Rhino. Do the move, the drone locks on, then pop it.");
            Items.Add(_picker);
            Items.Add(_card);
            Items.Add(_hud);
            Items.Add(_message);
            Items.Add(_actions);
            Items.Add(_result);
            ShowLevel(_index);
        }

        public void Enter()
        {
            if (_game != null)
                _game.Paused = false;
        }

        public void Leave() => StopGame();

        private SchoolLevel Level => SchoolLevels.All[_index];

        private void ShowLevel(int index)
        {
            StopGame();
            _index = index;
            _won = false;
            BuildPicker();
            var level = Level;
            var p = Palette.Current;
            int stars = DroneProgress.Stars(level.Id);
            var label = Theme.Heading(level.Label);
            label.TextColor = p.AccentText;
            _card.Content = Theme.Box(new StackLayout
            {
                Spacing = 8,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Items =
                {
                    new SectionLabel($"LESSON {_index + 1} OF {SchoolLevels.All.Length}", stars > 0 ? new string('★', stars) + new string('☆', 3 - stars) : ""),
                    Theme.Heading(level.Title),
                    new MouseDiagram(level.Mouse, level.Keys),
                    label,
                    Theme.Body(level.How),
                    Theme.Small("Why it matters: " + level.Why),
                },
            }, padding: 12);
            _hud.Visible = false;
            _message.Text = "";
            _result.Items.Clear();
            ShowStart("Start lesson");
        }

        /// <summary>One chip per lesson ("1 ★★★", "2"...), the current one highlighted.</summary>
        private void BuildPicker()
        {
            var chips = SchoolLevels.All.Select((l, i) =>
            {
                int stars = DroneProgress.Stars(l.Id);
                var chip = new ChunkyButton($"{i + 1}" + (stars > 0 ? " " + new string('★', stars) : ""), i == _index ? ButtonKind.Selected : ButtonKind.Outline, 32);
                int n = i;
                chip.Click += (s, e) => ShowLevel(n);
                chip.ToolTip = $"{l.Title}: {l.Label}";
                return (Control)chip;
            }).ToArray();
            _picker.Content = Theme.Grid(4, chips);
        }

        private void ShowStart(string text)
        {
            if (_doc == null)
            {
                _actions.Content = Theme.Body("Open a Rhino document to start.");
                return;
            }
            var start = Theme.Primary(text, StartGame);
            start.Fill = true;
            start.Height = 42;
            _actions.Content = start;
        }

        private void StartGame()
        {
            if (_doc == null)
                return;
            StopGame();
            _won = false;
            _result.Items.Clear();
            _game = new SchoolGame(_doc, Level);
            _game.Changed += Refresh;
            _hud.Clock = Level.TimeLimit != null;
            _hud.Visible = true;
            if (Level.TimeLimit is double limit)
            {
                // Keep the clock ticking between other updates.
                var clock = new UITimer { Interval = 0.25 };
                var game = _game;
                clock.Elapsed += (s, e) =>
                {
                    if (_game != game || game.Finished || game.TimeUp)
                    {
                        clock.Stop();
                        return;
                    }
                    _hud.SetTime(game.TimeLeft, limit);
                };
                clock.Start();
            }
            _actions.Content = Theme.Grid(2,
                Theme.Button("Restart", StartGame),
                Theme.Button("Stop", () => { StopGame(); ShowStart("Start lesson"); _hud.Visible = false; _message.Text = ""; }));
            Say(Palette.Current.Text, Level.Course != null
                ? (Level.Move == Move.Nudge ? "Your drone is selected. Press Alt + an arrow key to nudge it." : "Your drone is selected. Turn on Gumball and drag an arrow.")
                : "Look at the Perspective view. The corner card shows the move: give it a try!");
            Refresh();
        }

        /// <summary>Updates the step pips and the coaching line from the game.</summary>
        private void Refresh()
        {
            var g = _game;
            if (g == null)
                return;
            var p = Palette.Current;
            var level = Level;

            if (level.Course != null)
            {
                _hud.Set($"LESSON {_index + 1} · {SchoolGame.Name(level.Move)}", $"{g.GoodMoves} moves · {g.Crashes} bonks",
                    new[] { g.MoveDone ? Pip.Good : Pip.Now, g.Finished ? Pip.Good : g.MoveDone ? Pip.Now : Pip.Todo });
                if (g.LastCrash != null && g.Seconds - g.LastMoveTime > 0.05)
                {
                    Say(p.RedText, g.LastCrash);
                }
                else if (g.LastMove != null)
                {
                    Say(p.GreenText, "✓ " + g.LastMoveText + ". " + (level.Move == Move.Nudge ? "Keep nudging to the green ring." : "Keep flying to the green ring."));
                }
                if (!g.FlyerSelected && !g.Finished)
                    Say(p.AccentText, "Click your drone to select it, so you can move it.");
            }
            else
            {
                int popped = g.Drones.Count(d => !d.Alive);
                bool anyLocked = g.Drones.Any(d => d.Alive && d.Locked) || popped > 0;
                _hud.Set($"LESSON {_index + 1} · {(level.Move == Move.Any ? "EVERYTHING" : SchoolGame.Name(level.Move))}",
                    $"{popped} / {g.Drones.Count} popped",
                    new[]
                    {
                        g.MoveDone || level.Move == Move.Click ? Pip.Good : Pip.Now,
                        anyLocked ? Pip.Good : g.MoveDone ? Pip.Now : Pip.Todo,
                        g.Finished ? Pip.Good : anyLocked ? Pip.Now : Pip.Todo,
                    });

                if (g.ShotResultLast == ShotResult.Popped && g.ShotText.Length > 0)
                    Say(p.GreenText, "💥 " + g.ShotText);
                else if (g.ShotResultLast == ShotResult.NotLocked)
                    Say(p.AccentText, g.ShotText);
                else if (g.LastMove != null && !g.LastMoveRight && g.WrongMoveHint.Length > 0)
                    Say(p.AccentText, g.WrongMoveHint);
                else if (g.Drones.Any(d => d.Alive && d.Locked))
                    Say(p.GreenText, (g.LastMoveRight ? "✓ " + g.LastMoveText + "! " : "") + "Locked on. Left-click the drone with the green ring to pop it.");
                else if (g.LastMoveRight)
                    Say(p.GreenText, "✓ " + g.LastMoveText + ". " + (g.Drones.FirstOrDefault(d => d.Alive)?.Hint ?? ""));
                g.ShotResultLast = null;
            }

            if (level.TimeLimit is double limit)
                _hud.SetTime(g.TimeLeft, limit);

            if (g.Finished && !_won)
            {
                _won = true;
                Application.Instance.AsyncInvoke(Win);
            }
            else if (g.TimeUp && !_won)
            {
                _won = true;
                Application.Instance.AsyncInvoke(TimeUp);
            }
        }

        private void Win()
        {
            var g = _game;
            if (g == null)
                return;
            int slips = Level.Course != null ? g.Crashes : g.Shots - g.Drones.Count;
            int stars = DroneProgress.StarsFor(slips);
            DroneProgress.Cleared(Level.Id, stars);

            // Let the pop play before giving the view back.
            _game = null;
            g.Changed -= Refresh;
            var timer = new UITimer { Interval = 1.3 };
            timer.Elapsed += (s, e) => { timer.Stop(); g.Dispose(); };
            timer.Start();

            var p = Palette.Current;
            bool last = _index == SchoolLevels.All.Length - 1;
            _message.Text = "";
            _hud.Visible = false;
            _result.Items.Clear();
            _result.Items.Add(Theme.Card(p.Green,
                Theme.Heading(last ? "Drone School graduate! 🎓" : "Lesson done! 🎉"),
                Theme.Body($"{new string('★', stars)}{new string('☆', 3 - stars)}   You learned: {Level.Label}"),
                Theme.Muted(stars < 3
                    ? (Level.Course != null ? "Fewer bonks earn more stars. Line up before you move." : "Lock on before you click to earn more stars.")
                    : "Perfect run!")));
            var next = last
                ? Theme.Primary("Back to games", () => _host.ShowHome())
                : Theme.Primary($"Next: {SchoolLevels.All[_index + 1].Title} ▶", () => ShowLevel(_index + 1));
            next.Fill = true;
            next.Height = 42;
            _actions.Content = new StackLayout
            {
                Spacing = 8,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Items = { next, Theme.Wide(Theme.Button("Do it again", StartGame)) },
            };
            BuildPicker();
        }

        /// <summary>The clock ran out: show how far you got and offer another go.</summary>
        private void TimeUp()
        {
            var g = _game;
            if (g == null)
                return;
            int popped = g.Drones.Count(d => !d.Alive);
            int total = g.Drones.Count;
            StopGame();
            var p = Palette.Current;
            _message.Text = "";
            _hud.Visible = false;
            _result.Items.Clear();
            _result.Items.Add(Theme.Card(p.Red,
                Theme.Heading("Time's up! ⏱"),
                Theme.Body($"You popped {popped} of {total} drones."),
                Theme.Muted("Tip: zoom out first to spot them all, then pan and orbit in close for each one.")));
            var again = Theme.Primary("Try again", StartGame);
            again.Fill = true;
            again.Height = 42;
            _actions.Content = again;
        }

        private void Say(Color color, string text)
        {
            _message.TextColor = color;
            _message.Text = text;
        }

        private void StopGame()
        {
            if (_game == null)
                return;
            _game.Changed -= Refresh;
            _game.Dispose();
            _game = null;
        }
    }
}
