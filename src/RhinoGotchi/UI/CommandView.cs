using System;
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
    /// How one command works: icon and description, a step-by-step viewport demo, Try it!,
    /// where its button is in Rhino, what to do next, and recipes that chain it with other commands.
    /// </summary>
    internal sealed class CommandView : StackLayout, IGameView
    {
        private readonly RhinoGotchiPanel _host;
        private readonly CommandEntry _entry;
        private readonly CommandDemo? _demo;
        private readonly RhinoDoc? _doc;
        private int _step;

        private readonly Words _stepLabel = Theme.Heading("");
        private readonly Words _stepText = Theme.Body("");
        private readonly ChunkyButton _prev = new ChunkyButton { Text = "◀ Back" };
        private readonly ChunkyButton _next = new ChunkyButton { Text = "Next ▶", Kind = ButtonKind.Secondary };
        private readonly Words _status = Theme.Muted("");
        private readonly Words _findStatus = Theme.Muted("");

        public CommandView(RhinoGotchiPanel host, CommandEntry entry, Control back)
        {
            _host = host;
            _entry = entry;
            _doc = RhinoDoc.ActiveDoc;
            _demo = _doc == null ? null : CommandDemos.Get(entry.Command, _doc.ModelAbsoluteTolerance);

            Theme.SetUpPage(this, host, entry.Command, entry.Category, "← Back", () => host.Navigate(back));

            var icon = new ImageView { Image = CommandIcons.Get(entry, 48), Size = new Size(48, 48) };
            Items.Add(new TableLayout
            {
                Spacing = new Size(10, 0),
                Rows = { new TableRow(new TableCell(icon), new TableCell(Theme.Body(entry.Description), true)) },
            });

            AddHowItWorks();
            AddTryIt();
            AddWhereToFindIt();
            AddWhatsNext();
            AddRecipes();
        }

        public void Enter()
        {
            if (_demo != null)
                GoTo(_step);
        }

        public void Leave() => ShapePreview.Stop();

        // ---- How it works ----

        private void AddHowItWorks()
        {
            if (_demo == null)
            {
                Items.Add(Theme.Muted("No step-by-step demo for this command yet. Try it, or read the help page."));
                return;
            }
            Items.Add(Theme.Heading("How it works"));
            Items.Add(Theme.Muted("Watch the viewport: orange is what you start with, numbered dots are your picks, blue is the result."));
            Items.Add(_stepLabel);
            Items.Add(_stepText);
            _prev.Click += (s, e) => GoTo(_step - 1);
            _next.Click += (s, e) => GoTo(_step + 1);
            Items.Add(Theme.Row(_prev, _next));
        }

        private void GoTo(int step)
        {
            if (_demo == null || _doc == null)
                return;
            _step = Math.Max(0, Math.Min(step, _demo.Steps.Count - 1));
            _stepLabel.Text = $"Step {_step + 1} of {_demo.Steps.Count}";
            _stepText.Text = _demo.Steps[_step].Text;
            _prev.Enabled = _step > 0;
            _next.Enabled = _step < _demo.Steps.Count - 1;
            ShapePreview.Show(_doc, _demo.Steps[_step].Frame, _demo.Focus);
        }

        // ---- Try it! ----

        private void AddTryIt()
        {
            Items.Add(Theme.Heading("Try it!"));
            var tryIt = new ChunkyButton { Text = "Try it!", Kind = ButtonKind.Primary, Font = Theme.HeadingFont };
            tryIt.Click += (s, e) => TryIt(withDemoSetup: false);
            Items.Add(Theme.Row(tryIt, Theme.Secondary("Help page", () => RhinoActions.OpenHelp(_entry.Command))));
            if (_demo != null && _demo.TryInputs.Count > 0)
            {
                Items.Add(new StackLayoutItem(
                    Theme.Button("Try it on the demo setup", () => TryIt(withDemoSetup: true)),
                    HorizontalAlignment.Left));
                Items.Add(Theme.Muted("Adds the orange starting geometry to your model (Undo removes it) and shows the blue result as a target."));
            }
            Items.Add(_status);
        }

        private void TryIt(bool withDemoSetup)
        {
            if (withDemoSetup && _demo != null && _doc != null)
            {
                // Make sure the preview session exists so the geometry lands where the demo is drawn.
                ShapePreview.Show(_doc, _demo.Target(), _demo.Focus);
                uint undo = _doc.BeginUndoRecord($"RhinoGotchi {_entry.Command} demo setup");
                foreach (var g in _demo.TryInputs)
                    _doc.Objects.Add(ShapePreview.ToPreviewSpace(g));
                _doc.EndUndoRecord(undo);
                _doc.Views.Redraw();
                _status.Text = $"Added the starting geometry. Now follow the steps in {_entry.Command}. The blue shape is your target.";
            }
            else
            {
                _status.Text = $"Running {_entry.Command}. Follow the prompts on the command line.";
            }
            RhinoActions.Run(_entry.Command);
        }

        // ---- Where to find it ----

        private void AddWhereToFindIt()
        {
            Items.Add(Theme.Heading("Where to find it"));
            var spots = ToolbarMap.Find(_entry.Command);
            if (spots.Length == 0)
            {
                Items.Add(Theme.Body($"{_entry.Command} isn't on the default toolbars. Type it on the command line, or use Try it! above."));
                return;
            }

            // A tab first (the most visible place), then the Main sidebar.
            foreach (var spot in spots.OrderBy(s => ToolbarMap.IsTab(s.Root) ? 0 : 1).Take(2))
            {
                Items.Add(Theme.Body(ToolbarMap.Describe(spot)));
                Items.Add(Strip(spot.Root, spot.RootIndex));
                if (spot.Flyout != null)
                {
                    Items.Add(Theme.Muted($"{spot.Flyout} flyout:"));
                    Items.Add(Strip(spot.Flyout, spot.FlyoutIndex));
                }
                var root = spot.Root;
                Items.Add(new StackLayoutItem(Theme.Secondary("Show me in Rhino", () => ShowToolbar(root)), HorizontalAlignment.Left));
            }
            Items.Add(Theme.Muted("The highlighted icon is the button to click. Hover any icon to see its name."));
            Items.Add(_findStatus);
        }

        /// <summary>A copy of a Rhino toolbar: its button icons in order, with one highlighted.</summary>
        private static Control Strip(string toolbar, int highlight) => new ToolbarStrip(toolbar, highlight);
        /// <summary>Tries to open the toolbar through RhinoCommon; otherwise says where to click.</summary>
        private void ShowToolbar(string toolbar)
        {
            bool opened = false;
            try
            {
                foreach (var file in RhinoApp.ToolbarFiles)
                {
                    var group = file.GetGroup(toolbar);
                    if (group == null)
                        continue;
                    group.Visible = true;
                    opened = true;
                    break;
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine("RhinoGotchi: could not open the {0} toolbar ({1}).", toolbar, ex.Message);
            }

            _findStatus.Text = opened
                ? $"Opened the {toolbar} toolbar. Look for the highlighted button."
                : toolbar == "Main"
                    ? "Click the Standard tab at the top of Rhino. The Main sidebar is the column of buttons down the left edge."
                    : $"Click the {toolbar} tab along the top of the Rhino window. The button is in the position highlighted above.";
        }

        // ---- What's next / recipes ----

        private void AddWhatsNext()
        {
            var (sentence, next) = NextSteps.For(_entry);
            if (next.Count == 0)
                return;
            Items.Add(Theme.Heading("What's next"));
            Items.Add(Theme.Body(sentence));
            Items.Add(Theme.Grid(3, next.Select(e => (Control)Theme.Button(e.Command, () => _host.Navigate(new CommandView(_host, e, this)))).ToArray()));
        }

        private void AddRecipes()
        {
            var recipes = Recipes.Using(_entry.Command).ToList();
            if (recipes.Count == 0)
                return;
            Items.Add(Theme.Heading("String it together"));
            Items.Add(Theme.Muted($"Recipes that use {_entry.Command} with other commands to build something:"));
            foreach (var recipe in recipes)
            {
                var r = recipe;
                Items.Add(new StackLayoutItem(Theme.Button($"{r.Title}:  {r.ChainText}", () => _host.Navigate(new RecipeView(_host, r, this))),
                    HorizontalAlignment.Stretch));
            }
        }
    }
}
