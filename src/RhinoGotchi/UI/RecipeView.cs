using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Forms;
using Rhino;
using RhinoGotchi.Content;
using RhinoGotchi.Core;
using RhinoGotchi.Display;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// A recipe: several commands strung together, stage by stage in the viewport.
    /// Each stage's result (blue) becomes the next stage's input (orange).
    /// </summary>
    internal sealed class RecipeView : StackLayout, IGameView
    {
        private readonly RhinoGotchiPanel _host;
        private readonly CommandDemo? _demo;
        private readonly RhinoDoc? _doc;
        private readonly List<ChunkyButton> _chainButtons = new List<ChunkyButton>();
        private int _stage;

        private readonly Label _stageLabel = Theme.Heading("");
        private readonly Label _stageText = Theme.Body("");
        private readonly ChunkyButton _prev = new ChunkyButton { Text = "◀ Back" };
        private readonly ChunkyButton _next = new ChunkyButton { Text = "Next ▶", Kind = ButtonKind.Secondary };
        private readonly StackLayout _stageButtons = new StackLayout { Spacing = 6, HorizontalContentAlignment = HorizontalAlignment.Stretch };

        public RecipeView(RhinoGotchiPanel host, Recipe recipe, Control back)
        {
            _host = host;
            _doc = RhinoDoc.ActiveDoc;
            _demo = _doc == null ? null : recipe.Demo(_doc.ModelAbsoluteTolerance);

            Theme.SetUpPage(this, host, recipe.Title, recipe.Summary, "← Back", () => host.Navigate(back));

            if (_demo == null)
            {
                Items.Add(Theme.Body("Open a Rhino document to see this recipe in the viewport."));
                return;
            }

            Items.Add(Theme.Heading("The chain"));
            foreach (var command in recipe.Chain)
            {
                string c = command;
                var button = new ChunkyButton { Text = c };
                button.Click += (s, e) => GoTo(_demo.Steps.FindIndex(st => st.Command == c));
                _chainButtons.Add(button);
            }
            Items.Add(Theme.Grid(4, _chainButtons.ToArray<Control>()));
            Items.Add(Theme.Muted("Each stage's blue result becomes the orange input for the next command."));

            Items.Add(_stageLabel);
            Items.Add(_stageText);
            _prev.Click += (s, e) => GoTo(_stage - 1);
            _next.Click += (s, e) => GoTo(_stage + 1);
            Items.Add(Theme.Row(_prev, _next));
            Items.Add(_stageButtons);
        }

        public void Enter()
        {
            if (_demo != null)
                GoTo(_stage);
        }

        public void Leave() => ShapePreview.Stop();

        private void GoTo(int stage)
        {
            if (_demo == null || _doc == null || stage < 0)
                return;
            _stage = Math.Min(stage, _demo.Steps.Count - 1);
            var step = _demo.Steps[_stage];
            string command = step.Command ?? "";

            _stageLabel.Text = $"Stage {_stage + 1} of {_demo.Steps.Count}: {command}";
            _stageText.Text = step.Text;
            _prev.Enabled = _stage > 0;
            _next.Enabled = _stage < _demo.Steps.Count - 1;
            foreach (var b in _chainButtons)
                b.Kind = b.Text == command ? ButtonKind.Selected : ButtonKind.Outline;

            _stageButtons.Items.Clear();
            var entry = CommandCatalog.Find(command);
            var row = Theme.Row(Theme.Primary($"Try {command}", () => RhinoActions.Run(command)));
            if (entry != null)
                row.Items.Add(Theme.Button($"How {command} works", () => _host.Navigate(new CommandView(_host, entry, this))));
            _stageButtons.Items.Add(row);

            ShapePreview.Show(_doc, step.Frame, _demo.Focus);
        }
    }
}
