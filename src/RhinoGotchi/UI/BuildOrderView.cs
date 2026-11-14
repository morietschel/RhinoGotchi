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

        private readonly StackLayout _historyArea = new StackLayout { Spacing = 4 };
        private readonly Panel _slotsRow = new Panel();
        private readonly Panel _bankHost = new Panel();
        private readonly Label _message = Theme.Body("");
        private readonly Label _triesLabel = Theme.Muted("");
        private readonly StackLayout _controls;
        private readonly ChunkyButton _hintButton;
        private readonly StackLayout _end = new StackLayout { Spacing = 8, HorizontalContentAlignment = HorizontalAlignment.Stretch };

        public BuildOrderView(RhinoGotchiPanel host, bool practice)
        {
            _host = host;
            _practice = practice;
            _doc = RhinoDoc.ActiveDoc;

            Theme.SetUpPage(this, host,
                practice ? "Build Order · Practice" : "Build Order",
                "The blue object in the viewport was built from scratch with a chain of commands. Pick them in the order they were used.");

            var stats = Progress.Get(Games.Order);
            if (!practice && stats.PlayedToday)
            {
                Items.Add(Theme.Body($"You've played today's Build Order ({stats.LastResult}). A new one arrives tomorrow."));
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

            _hintButton = Theme.Secondary("Hint: show the first command", Hint);
            _controls = Theme.Row(
                Theme.Button("Undo", () => { if (_guess.Count > 0) _guess.RemoveAt(_guess.Count - 1); Redraw(); }),
                Theme.Button("Clear", () => { _guess.Clear(); Redraw(); }),
                Theme.Primary("Check", Check));

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

            Items.Add(Theme.Heading($"Build this in {_recipe.Chain.Length} commands"));
            Items.Add(Theme.Muted("Tip: work backwards. What was the last thing done to the object?"));
            Items.Add(_historyArea);
            Items.Add(Theme.Muted("Your order:"));
            Items.Add(_slotsRow);
            Items.Add(Theme.Muted("Commands (tap to add):"));
            Items.Add(_bankHost);
            Items.Add(_controls);
            Items.Add(_triesLabel);
            Items.Add(new StackLayoutItem(_hintButton, HorizontalAlignment.Left));
            Items.Add(_message);
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
                slots.Add(Theme.Chip(filled ? $"{i + 1}. {_guess[i]}" : $"{i + 1}. ?",
                    filled ? Theme.Selected : Theme.Tile, filled ? Theme.Light : Theme.TileText, 0, 34));
            }
            _slotsRow.Content = Theme.Grid(3, slots.ToArray());

            var buttons = _bank.Select(command =>
            {
                var b = new ChunkyButton { Text = command, Enabled = !_over && !_guess.Contains(command) && _guess.Count < _recipe.Chain.Length };
                b.Click += (s, e) => { _guess.Add(command); _message.Text = ""; Redraw(); };
                return (Control)b;
            }).ToArray();
            _bankHost.Content = Theme.Grid(3, buttons);

            _triesLabel.Text = $"Tries left: {MaxTries - _history.Count}";
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
                _message.Text = _history.Count == 1 ? "Perfect build order on the first try!" : $"Solved in {_history.Count} tries.";
                Finish(true);
            }
            else if (_history.Count >= MaxTries)
            {
                _message.Text = "Out of tries. Here's how it was built:";
                Finish(false);
            }
            else
            {
                int used = attempt.Count(c => _recipe.Uses(c));
                _message.Text = $"{right} in the right place, {used - right} used but in the wrong place.";
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
                chips.Add(Theme.Chip($"{i + 1}. {attempt[i]}", color, Theme.Light, 0, 34));
            }
            return Theme.Grid(3, chips.ToArray());
        }

        private void Finish(bool won)
        {
            _over = true;
            _controls.Enabled = false;
            _hintButton.Enabled = false;
            Redraw();

            string result = (won ? $"{_history.Count}/{MaxTries}" : $"X/{MaxTries}") + (_hinted ? " with hint" : "");
            if (!_practice)
                Progress.Record(Games.Order, won, result);

            var recipe = _recipe!;
            _end.Items.Clear();
            _end.Items.Add(Theme.Card(won ? Theme.Hit : Theme.Tile,
                Theme.Heading(recipe.Title),
                Theme.Body(recipe.ChainText),
                Theme.Body(recipe.Summary)));
            _end.Items.Add(new StackLayoutItem(
                Theme.Primary("Watch it get built, step by step", () => _host.Navigate(new RecipeView(_host, recipe, this))),
                HorizontalAlignment.Left));
            _end.Items.Add(PracticeButton());
        }

        private StackLayoutItem PracticeButton() => new StackLayoutItem(
            Theme.Button("Play a practice round", () => _host.Navigate(new BuildOrderView(_host, practice: true))),
            HorizontalAlignment.Left);
    }
}
