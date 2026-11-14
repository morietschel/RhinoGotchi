using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using RhinoGotchi.Content;
using RhinoGotchi.Core;
using RhinoGotchi.Display;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// Build Order: the viewport shows a finished object. Pick the commands that built it, in order.
    /// Feedback per slot: teal = right command in the right place, gold = used but elsewhere, gray = not used.
    /// </summary>
    internal sealed class BuildOrderView : StackLayout, IGameView
    {
        private const int MaxTries = 4;
        private const int Decoys = 3;

        private readonly RhinoGotchiPanel _host;
        private readonly bool _practice;
        private readonly RhinoDoc? _doc;
        private readonly Recipe? _recipe;
        private readonly CommandDemo? _demo;
        private readonly List<string> _bank = new List<string>();
        private readonly List<string> _guess = new List<string>();
        private readonly List<string[]> _history = new List<string[]>();
        private bool _hinted;
        private bool _over;

        private readonly StackLayout _historyArea = new StackLayout { Spacing = 4, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        private readonly Panel _slotsRow = new Panel();
        private readonly Panel _bankHost = new Panel();
        private readonly Words _message = Theme.Body("");
        private readonly GameHud _hud = new GameHud();
        private readonly Control _controls;
        private readonly ChunkyButton _hintButton;
        private readonly StackLayout _end = new StackLayout { Spacing = 8, HorizontalContentAlignment = HorizontalAlignment.Stretch };

        public BuildOrderView(RhinoGotchiPanel host, bool practice)
        {
            _host = host;
            _practice = practice;
            _doc = RhinoDoc.ActiveDoc;

            Theme.SetUpPage(this, host, practice ? "Build Order · Practice" : "Build Order", "");

            var stats = Progress.Get(Games.Order);
            if (!practice && stats.PlayedToday)
            {
                Items.Add(Theme.Card(Theme.Tile,
                    Theme.Heading($"Today's build is done: {stats.LastResult}"),
                    Theme.Muted("A new one arrives tomorrow.")));
                Items.Add(PracticeButton());
                _controls = new StackLayout();
                _hintButton = new ChunkyButton();
                return;
            }

            var rng = Daily.Rng(Games.Order, practice);
            if (_doc != null)
            {
                foreach (var candidate in Daily.Shuffle(global::RhinoGotchi.Content.Recipes.All, rng))
                {
                    var demo = candidate.Demo(_doc.ModelAbsoluteTolerance);
                    if (demo != null && demo.Steps.Count == candidate.Chain.Length && demo.Steps[demo.Steps.Count - 1].Frame.Results.Count > 0)
                    {
                        _recipe = candidate;
                        _demo = demo;
                        break;
                    }
                }
            }

            _hintButton = Theme.Button("Hint", Hint);
            _controls = new StackLayout
            {
                Spacing = 8,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Items =
                {
                    Theme.Primary("Check", Check),
                    Theme.Row(
                        Theme.Button("Undo", () => { if (_guess.Count > 0) _guess.RemoveAt(_guess.Count - 1); Redraw(); }),
                        Theme.Button("Clear", () => { _guess.Clear(); Redraw(); }),
                        _hintButton),
                },
            };

            if (_recipe == null || _demo == null)
            {
                Items.Add(Theme.Body("Open a Rhino document to play. This game draws the target object in the viewport."));
                return;
            }

            // The real chain plus a few decoys from the library, shuffled.
            var decoys = Daily.Shuffle(CommandCatalog.Categories.SelectMany(c => c.Commands)
                    .Where(e => e.HasDemo && !_recipe.Uses(e.Command))
                    .Select(e => e.Command), rng)
                .Take(Decoys);
            _bank.AddRange(Daily.Shuffle(_recipe.Chain.Concat(decoys), rng));

            Items.Add(_hud);
            Items.Add(Theme.Heading("Which commands built the blue object, in order?"));
            Items.Add(_historyArea);
            Items.Add(new SectionLabel("YOUR ORDER"));
            Items.Add(_slotsRow);
            Items.Add(new SectionLabel("COMMANDS", "tap to add"));
            Items.Add(_bankHost);
            Items.Add(_message);
            Items.Add(_controls);
            Items.Add(_end);
            Redraw();
        }

        public void Enter()
        {
            if (_demo != null && _doc != null && !_over)
                ShapePreview.Show(_doc, _demo.Target(), _demo.Focus);
        }

        public void Leave() => ShapePreview.Stop();

        private void Redraw()
        {
            if (_recipe == null)
                return;

            var slots = new List<Control>();
            for (int i = 0; i < _recipe.Chain.Length; i++)
            {
                bool filled = i < _guess.Count;
                slots.Add(Theme.Chip(filled ? $"{i + 1}. {_guess[i]}" : $"{i + 1}",
                    filled ? Theme.Selected : Theme.Tile, filled ? Theme.P.SelectedText : Theme.P.DisabledText, 0, 38));
            }
            _slotsRow.Content = Theme.Grid(2, slots.ToArray());

            var buttons = _bank.Select(command =>
            {
                var b = new ChunkyButton { Text = command, Enabled = !_over && !_guess.Contains(command) && _guess.Count < _recipe.Chain.Length };
                b.Click += (s, e) => { _guess.Add(command); _message.Text = ""; Redraw(); };
                return (Control)b;
            }).ToArray();
            _bankHost.Content = Theme.Grid(2, buttons);

            // One pip per try: red for a miss, green for the solve.
            int left = MaxTries - _history.Count;
            bool solved = _over && _history.Count > 0 && _history[_history.Count - 1].SequenceEqual(_recipe.Chain);
            var pips = Enumerable.Range(0, MaxTries).Select(i =>
                i < _history.Count ? (solved && i == _history.Count - 1 ? Pip.Good : Pip.Bad)
                : i == _history.Count && !_over ? Pip.Now : Pip.Todo);
            _hud.Set($"BUILD IN {_recipe.Chain.Length} COMMANDS", _over ? (solved ? "Solved" : "Out of tries") : left == 1 ? "Last try" : $"{left} tries", pips);
        }

        private void Hint()
        {
            if (_recipe == null || _over)
                return;
            _hinted = true;
            _hintButton.Enabled = false;
            _message.Text = $"It starts with {_recipe.Chain[0]}.";
        }

        private void Check()
        {
            if (_recipe == null || _over)
                return;
            if (_guess.Count != _recipe.Chain.Length)
            {
                _message.Text = $"Fill all {_recipe.Chain.Length} slots first.";
                return;
            }

            var attempt = _guess.ToArray();
            _history.Add(attempt);
            _historyArea.Items.Add(FeedbackRow(attempt));
            _guess.Clear();

            int right = attempt.Where((c, i) => c == _recipe.Chain[i]).Count();
            if (right == _recipe.Chain.Length)
            {
                _message.Text = _history.Count == 1 ? "First try!" : "";
                Finish(true);
            }
            else if (_history.Count >= MaxTries)
            {
                _message.Text = "";
                Finish(false);
            }
            else
            {
                int used = attempt.Count(c => _recipe.Uses(c));
                _message.Text = $"{right} in the right place · {used - right} in the wrong place";
                Redraw();
            }
        }

        /// <summary>One past attempt: each slot colored by how close it was.</summary>
        private Control FeedbackRow(string[] attempt)
        {
            var chips = new List<Control>();
            for (int i = 0; i < attempt.Length; i++)
            {
                var color = attempt[i] == _recipe!.Chain[i] ? Theme.Hit : _recipe.Uses(attempt[i]) ? Theme.Near : Theme.Miss;
                chips.Add(Theme.Chip($"{i + 1}. {attempt[i]}", color, Theme.Light, 0, 30));
            }
            return Theme.Grid(2, chips.ToArray());
        }

        private void Finish(bool won)
        {
            _over = true;
            _controls.Visible = false;
            _hintButton.Enabled = false;
            Redraw();

            string result = (won ? $"{_history.Count}/{MaxTries}" : $"X/{MaxTries}") + (_hinted ? " with hint" : "");
            if (!_practice)
                Progress.Record(Games.Order, won, result);
            // 100 points for a first-try solve, 20 fewer per extra try, 10 fewer with the hint.
            // Practice rounds count for the leaderboard too: it shows your best round.
            int points = won ? 100 - 20 * (_history.Count - 1) - (_hinted ? 10 : 0) : 0;
            bool newBest = Leaderboard.Report(Games.Order, points);

            var recipe = _recipe!;
            _end.Items.Clear();
            if (newBest)
                _end.Items.Add(LeaderboardView.NewBestRow(_host, points));
            _end.Items.Add(Theme.Card(won ? Theme.Hit : Theme.Tile,
                Theme.Heading(recipe.Title),
                Theme.Body(recipe.ChainText),
                Theme.Body(recipe.Summary)));
            _end.Items.Add(Theme.Primary("Watch it get built", () => _host.Navigate(new RecipeView(_host, recipe, this))));
            _end.Items.Add(PracticeButton());
        }

        private StackLayoutItem PracticeButton() => new StackLayoutItem(
            Theme.Button("Play a practice round", () => _host.Navigate(new BuildOrderView(_host, practice: true))),
            HorizontalAlignment.Left);
    }
}
