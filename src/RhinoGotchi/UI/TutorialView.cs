using System;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.Display;
using Rhino.Geometry;
using RhinoGotchi.Content;
using RhinoGotchi.Core;
using RhinoGotchi.Levels;
using RhinoGotchi.Tutorials;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// One tutorial: what it builds, the steps, options, mistakes and check, plus buttons to show the
    /// reference in the model, set up the start geometry, check the result and clear the work.
    /// Leaving the page turns the highlight off, puts the view back and ends a running tutorial
    /// (its work stays on the tutorial layers; Set up resumes it).
    /// </summary>
    internal sealed class TutorialView : StackLayout, IGameView
    {
        private readonly RhinoGotchiPanel _host;
        private readonly Tutorial _tutorial;
        private readonly ReferenceConduit _highlight = new ReferenceConduit();
        private RhinoView? _zoomedView;
        private Rhino.DocObjects.ViewportInfo? _savedView;

        private readonly Button _showReference = new Button { Text = "Show reference" };
        private readonly Panel _statusChip = new Panel();
        private readonly Panel _result = new Panel();
        private readonly Label _status = Theme.Muted("");

        public TutorialView(RhinoGotchiPanel host, Tutorial tutorial, Control back)
        {
            _host = host;
            _tutorial = tutorial;

            Theme.SetUpPage(this, host, $"{tutorial.Number:00} · {tutorial.Title}",
                $"Part {tutorial.Part} · {tutorial.PartTitle}", "← Tutorials", () => host.Navigate(back));

            AddHeader();
            var banner = TutorialUi.ModelBanner(RhinoDoc.ActiveDoc);
            if (banner != null)
                Items.Add(banner);
            AddActions();
            AddContent();
            AddWhereToFindIt();
        }

        public void Enter() => RefreshStatus();

        public void Leave()
        {
            HideReference();
            LevelRunner? running = LevelRunner.Current;
            if (running != null && running.Level.TutorialId == _tutorial.Id)
                running.Stop(); // closes the workspace: current layer back, model checked
        }

        // ---- Header and actions ----

        private void AddHeader()
        {
            var icon = new ImageView { Image = TutorialUi.Icon(_tutorial.MainCommand, 48), Size = new Size(48, 48) };
            string commands = _tutorial.SupportingCommands.Count == 0
                ? _tutorial.MainCommand
                : $"{_tutorial.MainCommand}, with {string.Join(", ", _tutorial.SupportingCommands)}";
            var text = new StackLayout
            {
                Spacing = 2,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Items =
                {
                    new Label { Text = commands, Font = Theme.HeadingFont },
                    Theme.Body(_tutorial.Element),
                    _statusChip,
                },
            };
            Items.Add(new TableLayout
            {
                Spacing = new Size(10, 0),
                Rows = { new TableRow(new TableCell(icon), new TableCell(text, true)) },
            });
            RefreshStatus();
        }

        private void AddActions()
        {
            _showReference.Click += (s, e) => ToggleReference();
            var setUp = new Button { Text = "Set up", Font = Theme.HeadingFont };
            setUp.Click += (s, e) => SetUp();

            Items.Add(Theme.Grid(3,
                setUp,
                Theme.Button("Check my result", Check),
                _showReference,
                Theme.Button($"Try {_tutorial.MainCommand}", () => RhinoActions.Run(_tutorial.MainCommand)),
                Theme.Button("Help page", () => RhinoActions.OpenHelp(_tutorial.MainCommand)),
                Theme.Button("Clear my work", Clear)));
            Items.Add(Theme.Muted($"Set up adds the start geometry to \"{_tutorial.LayerPath}::Input\" and makes \"…::Result\" the current layer. Undo removes it."));
            Items.Add(_result);
            Items.Add(_status);
        }

        private void RefreshStatus() => _statusChip.Content = TutorialUi.StatusChip(_tutorial);

        private void SetUp()
        {
            RhinoDoc? doc = RhinoDoc.ActiveDoc;
            if (doc == null)
            {
                _status.Text = "Open a Rhino document first.";
                return;
            }
            if (Rhino.Commands.Command.InCommand())
            {
                _status.Text = "Finish or cancel the current command first.";
                return;
            }
            if (LevelRunner.Current?.Level.TutorialId == _tutorial.Id)
            {
                _status.Text = "This tutorial is already set up. Work on the Result layer, or use Clear my work to start over.";
                return;
            }

            HideReference();
            LevelRunner? runner = TutorialLevels.Start(doc, _tutorial);
            _result.Content = null;
            _status.Text = runner == null
                ? "Couldn't start this tutorial. See the command line."
                : $"Set up. Follow the steps below and model on the Result layer. Use Check my result when you're done.";
            RefreshStatus();
        }

        private void Check()
        {
            LevelRunner? runner = LevelRunner.Current;
            if (runner == null || runner.Level.TutorialId != _tutorial.Id)
            {
                _status.Text = "Set up the tutorial first, then model your result.";
                return;
            }

            LevelResult? result = runner.Evaluate();
            if (result == null)
            {
                _result.Content = null;
                _status.Text = "This tutorial doesn't have an automatic check yet. Use the Check section below to judge your result.";
                return;
            }

            string verdict = result.Solved ? $"Done! Score {result.Score:P0}." : $"Not yet. Score so far {result.Score:P0}.";
            _result.Content = Theme.Card(result.Solved ? Theme.Hit : Theme.Near,
                new Label { Text = verdict, Font = Theme.HeadingFont },
                Theme.Body(result.Feedback ?? ""));
            _status.Text = "";
            RefreshStatus();
        }

        private void Clear()
        {
            RhinoDoc? doc = RhinoDoc.ActiveDoc;
            if (doc == null)
                return;

            int deleted = TutorialWorkspace.ClearTutorial(doc, _tutorial, askFirst: true);
            if (deleted < 0)
                return;
            if (deleted == 0)
            {
                _status.Text = "Nothing to clear: the tutorial layers are empty.";
                return;
            }

            LevelRunner? running = LevelRunner.Current;
            if (running != null && running.Level.TutorialId == _tutorial.Id)
                running.Stop();
            _result.Content = null;
            _status.Text = $"Cleared {deleted} objects. Undo brings them back. Set up starts again from the beginning.";
        }

        // ---- Reference highlight ----

        private void ToggleReference()
        {
            if (_highlight.Enabled)
            {
                HideReference();
                return;
            }

            RhinoDoc? doc = RhinoDoc.ActiveDoc;
            ReferenceModel? reference = ReferenceModel.TryOpen(doc, out string reason);
            if (doc == null || reference == null)
            {
                _status.Text = reason;
                return;
            }

            _highlight.SetGeometry(reference.Get(_tutorial.ReferenceLayers.ToArray()));
            if (!_highlight.Bounds.IsValid)
            {
                _status.Text = "Couldn't find this tutorial's reference objects in the model.";
                return;
            }
            _highlight.Enabled = true;

            RhinoView? view = doc.Views.ActiveView;
            if (view != null)
            {
                if (_zoomedView == null)
                {
                    _savedView = new Rhino.DocObjects.ViewportInfo(view.ActiveViewport);
                    _zoomedView = view;
                }
                BoundingBox box = _highlight.Bounds;
                box.Inflate(Math.Max(box.Diagonal.Length * 0.2, 0.5));
                view.ActiveViewport.ZoomBoundingBox(box);
            }
            doc.Views.Redraw();
            _showReference.Text = "Hide reference";
            _status.Text = "The magenta objects are what this tutorial rebuilds. They're only highlighted, never changed.";
        }

        private void HideReference()
        {
            if (_highlight.Enabled)
            {
                _highlight.Enabled = false;
                _showReference.Text = "Show reference";
            }
            if (_zoomedView != null)
            {
                try
                {
                    if (_savedView != null)
                        _zoomedView.ActiveViewport.SetViewProjection(_savedView, true);
                    _zoomedView.Redraw();
                }
                catch (Exception ex)
                {
                    RhinoApp.WriteLine("RhinoGotchi: could not restore the view ({0}).", ex.Message);
                }
                _zoomedView = null;
                _savedView = null;
            }
            RhinoDoc.ActiveDoc?.Views.Redraw();
        }

        // ---- Teaching content ----

        private void AddContent()
        {
            bool written = !TutorialUi.IsTodo(_tutorial.Goal);
            if (!written)
            {
                Items.Add(Theme.Card(Theme.Tile,
                    new Label { Text = "Step-by-step text coming soon", Font = Theme.HeadingFont },
                    Theme.Body($"You can already set up the tutorial, look at the reference and try {_tutorial.MainCommand}.")));
                return;
            }

            Section("Goal", _tutorial.Goal);
            Section("Input geometry", _tutorial.InputGeometry);

            if (_tutorial.Steps.Count > 0)
            {
                Items.Add(Theme.Heading("Steps"));
                for (int i = 0; i < _tutorial.Steps.Count; i++)
                {
                    TutorialStep step = _tutorial.Steps[i];
                    var text = Theme.Body($"{i + 1}. {step.Text}" + (step.ViewHint != null ? $" ({step.ViewHint} view)" : ""));
                    if (step.Command == null)
                    {
                        Items.Add(text);
                        continue;
                    }
                    string command = step.Command;
                    Items.Add(new TableLayout
                    {
                        Spacing = new Size(6, 0),
                        Rows = { new TableRow(new TableCell(text, true), new TableCell(Theme.Button($"Try {command}", () => RhinoActions.Run(command)))) },
                    });
                }
            }

            List("Key options", _tutorial.KeyOptions);
            List("Common mistakes", _tutorial.CommonMistakes);
            Section("Check your result", _tutorial.Check);

            if (_tutorial.AssumedDimensions.Count > 0)
            {
                Items.Add(Theme.Heading("Assumed dimensions"));
                Items.Add(Theme.Card(Theme.Tile, _tutorial.AssumedDimensions
                    .Select(d => (Control)Theme.Body(FormattableString.Invariant($"{d.Name}: {d.Value} {d.Unit}. Assumed: {d.Why}")))
                    .ToArray()));
            }
        }

        private void Section(string heading, string text)
        {
            if (TutorialUi.IsTodo(text))
                return;
            Items.Add(Theme.Heading(heading));
            Items.Add(Theme.Body(text));
        }

        private void List(string heading, System.Collections.Generic.IEnumerable<string> lines)
        {
            var shown = lines.Where(l => !TutorialUi.IsTodo(l)).ToList();
            if (shown.Count == 0)
                return;
            Items.Add(Theme.Heading(heading));
            foreach (string line in shown)
                Items.Add(Theme.Body("• " + line));
        }

        // ---- Where to find it ----

        private void AddWhereToFindIt()
        {
            Items.Add(Theme.Heading("Where to find it"));
            var spots = ToolbarMap.Find(_tutorial.MainCommand);
            if (spots.Length == 0)
            {
                Items.Add(Theme.Body($"{_tutorial.MainCommand} isn't on the default toolbars. Type it on the command line, or use Try {_tutorial.MainCommand} above."));
                return;
            }
            var spot = spots.OrderBy(s => ToolbarMap.IsTab(s.Root) ? 0 : 1).First();
            Items.Add(Theme.Body(ToolbarMap.Describe(spot)));
            Items.Add(new ToolbarStrip(spot.Root, spot.RootIndex));
            if (spot.Flyout != null)
            {
                Items.Add(Theme.Muted($"{spot.Flyout} flyout:"));
                Items.Add(new ToolbarStrip(spot.Flyout, spot.FlyoutIndex));
            }
        }
    }
}
