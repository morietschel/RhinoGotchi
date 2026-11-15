using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using RhinoGotchi.Content;
using RhinoGotchi.Core;
using RhinoGotchi.Levels;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// Blocked!: build the ghost shape in the real model, but the obvious command is banned.
    /// There are many ways to do it, so each solve is matched to a known way (or saved as a new one)
    /// and the player can keep going to find more. The list of ways sits at the bottom of the page.
    /// Uses the team's LevelRunner (start points, ghost, object tracking) plus a CommandBlocker.
    /// </summary>
    internal sealed class BlockedView : StackLayout, IGameView
    {
        private readonly RhinoGotchiPanel _host;
        private readonly bool _practice;
        private readonly RhinoDoc? _doc;
        private readonly BlockedChallenge _challenge;
        private readonly HashSet<string> _revealed = new HashSet<string>();
        private LevelRunner? _runner;
        private ShapeMatchEvaluator? _evaluator;
        private CommandBlocker? _blocker;
        private bool _solved;

        private readonly Panel _actions = new Panel();
        private readonly StackLayout _status = new StackLayout { Spacing = 8, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        private readonly StackLayout _waysArea = new StackLayout { Spacing = 8, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        private readonly Words _blockedNote = Theme.Body("");
        private readonly Words _countLabel = Theme.Heading("");
        private readonly ProgressBar _countBar = new ProgressBar(0, Palette.Current.Green);

        public BlockedView(RhinoGotchiPanel host, bool practice, string? challengeId = null)
        {
            _host = host;
            _practice = practice;
            _doc = RhinoDoc.ActiveDoc;
            var rng = Daily.Rng(Games.Blocked, practice);
            _challenge = (challengeId != null ? BlockedChallenges.Find(challengeId) : null)
                         ?? BlockedChallenges.All[rng.Next(BlockedChallenges.All.Length)];

            Theme.SetUpPage(this, host,
                practice ? "Blocked! · Practice" : "Blocked!",
                "Build the ghost shape in your model, but the obvious command is off-limits. How many ways can you find?");

            var p = Palette.Current;

            // The challenge card: goal and the banned commands as red pills.
            var bannedPills = _challenge.Blocked.Take(3).Select(c => (Control)new Pill(c, p.Red, Glyph.Ban)).ToList();
            if (_challenge.Blocked.Length > 3)
                bannedPills.Add(new Pill($"+{_challenge.Blocked.Length - 3} more", p.Red, Glyph.Ban));
            Items.Add(Theme.Box(new StackLayout
            {
                Spacing = 8,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Items =
                {
                    new TableLayout
                    {
                        Spacing = new Size(10, 0),
                        Rows = { new TableRow(new Badge(Glyph.Ban, p.Red, 46), new TableCell(new StackLayout
                        {
                            Spacing = 3,
                            HorizontalContentAlignment = HorizontalAlignment.Stretch,
                            Items = { Theme.Heading(_challenge.Title), Theme.Muted("Blocked:") },
                        }, true)) },
                    },
                    Theme.Wrap(bannedPills.ToArray()),
                    Theme.Body(_challenge.Goal),
                    Theme.Muted("The blue ghost shows the target and the points help you snap. Use any other commands you like."),
                },
            }, padding: 12));

            Items.Add(_actions);
            Items.Add(_blockedNote);
            Items.Add(_status);
            Items.Add(_waysArea);
            Items.Add(Theme.Wide(Theme.Button("All challenges & ways", () => _host.Navigate(new BlockedListView(_host)))));

            var stats = Progress.Get(Games.Blocked);
            if (!practice && stats.PlayedToday)
                SetStatus(p.Green, "Done for today!", "You solved today's challenge. You can still hunt for more ways. They count toward your collection below.");
            ShowStartButton(first: true);
            RefreshWays();
        }

        public void Enter() => LevelRunner.StateChanged += OnStateChanged;

        public void Leave()
        {
            LevelRunner.StateChanged -= OnStateChanged;
            ClearAttempt(); // leaving ends the challenge: its points and geometry are deleted
            StopChallenge();
        }

        // ---------------- Playing ----------------

        private void ShowStartButton(bool first)
        {
            if (_doc == null)
            {
                _actions.Content = Theme.Body("Open a Rhino document to play.");
                return;
            }
            var start = Theme.Primary(first ? "Start the challenge" : "Clear and try another way", () =>
            {
                ClearAttempt();
                StartChallenge();
            });
            start.Height = 42;
            start.Fill = true;
            _actions.Content = start;
        }

        private void StartChallenge()
        {
            if (_doc == null)
                return;
            _solved = false;
            var (level, evaluator) = _challenge.CreateLevel();
            _evaluator = evaluator;
            _blocker = new CommandBlocker(_doc, _challenge.Blocked);
            evaluator.Exclude = _blocker.IsBlockedObject;
            _blocker.Blocked += command =>
            {
                _blockedNote.TextColor = Palette.Current.RedText;
                _blockedNote.Text = $"🚫 {command} is blocked! Rhino removed what it made. Try another way.";
            };
            _runner = LevelRunner.Start(_doc, level);
            _blockedNote.Text = "";

            _actions.Content = Theme.Grid(2,
                Theme.Secondary("Check my model", () => _runner?.Evaluate()),
                Theme.Button("Stop", () => { StopChallenge(); ShowStartButton(first: false); }));
            SetStatus(Palette.Current.Blue, "Challenge started!", "Build the shape in the viewport. I'll check your model after every command.");
        }

        private void OnStateChanged(LevelRunner runner)
        {
            if (runner != _runner || _solved)
                return;
            var result = runner.LastResult;
            if (result == null)
                return;

            if (result.Solved)
            {
                _solved = true;
                Solved(result.Feedback ?? "Solved!");
            }
            else
            {
                SetStatus(Palette.Current.Gold, "Not yet", result.Feedback ?? "Keep going!");
            }
        }

        private void Solved(string verification)
        {
            var route = Route(_runner?.Attempt.CommandsRun ?? new List<string>());
            string routeText = route.Count > 0 ? string.Join(" → ", route) : "your own way";

            // Stop listening, but remember the attempt so "Clear and try another way" can remove it.
            var attempt = _runner;
            StopChallenge();
            _runner = attempt;
            if (_doc != null && _evaluator != null && _evaluator.MatchedId != Guid.Empty)
            {
                _doc.Objects.UnselectAll();
                _doc.Objects.Select(_evaluator.MatchedId);
                _doc.Views.Redraw();
            }

            var way = _challenge.Match(route);
            string wayId = way?.Id ?? "custom:" + routeText;
            bool isNew = Progress.AddWay(_challenge.Id, wayId);
            if (!_practice)
                Progress.Record(Games.Blocked, true, $"{_challenge.Id} via {routeText}");

            // The challenge is over: clear its geometry after a short look at the result.
            var solved = _runner;
            var clearLater = new UITimer { Interval = 3 };
            clearLater.Elapsed += (s, e) =>
            {
                clearLater.Stop();
                if (_runner == solved) // not if the player already started another try
                    ClearAttempt();
            };
            clearLater.Start();

            string headline = way == null
                ? (isNew ? "A brand-new way! 🌟" : "Your own way again!")
                : (isNew ? "New way found! 🎉" : "You already found this way");
            string detail = way == null
                ? $"Your route: {routeText}. It isn't on the list, so it's saved under Your own ways."
                : $"{way.Name}. " + (isNew ? "Added to your collection." : "Try a different route to find another one!");

            _blockedNote.Text = "";
            _status.Items.Clear();
            _status.Items.Add(Theme.Card(isNew ? Palette.Current.Green : Palette.Current.Gold,
                Theme.Heading(headline),
                Theme.Body(detail),
                Theme.Muted(verification)));
            ShowStartButton(first: false);
            RefreshWays(highlight: wayId);
        }

        /// <summary>Removes the previous attempt's points and geometry so the next try starts clean.</summary>
        private void ClearAttempt()
        {
            if (_doc == null || _runner == null)
                return;
            var ids = _runner.Attempt.StartObjectIds.Concat(_runner.Attempt.AddedObjectIds).ToList();
            StopChallenge();
            if (ids.Count > 0)
            {
                _doc.Objects.Delete(ids, true);
                _doc.Views.Redraw();
            }
        }

        private void StopChallenge()
        {
            if (_runner != null && LevelRunner.Current == _runner)
                _runner.Stop();
            _runner = null;
            _blocker?.Dispose();
            _blocker = null;
        }

        /// <summary>The modeling commands the player used, without view, selection and blocked commands.</summary>
        private List<string> Route(IEnumerable<string> commands)
        {
            var skip = new[]
            {
                "Zoom", "Pan", "Rotate", "RotateView", "Undo", "Redo", "Delete", "Hide", "Show", "Lock", "Unlock",
                "Osnap", "Ortho", "Snap", "Planar", "SetView", "Gumball", "Properties", "What", "Check", "RhinoGotchi",
                "GotchiLevel", "Cancel",
            };
            var route = new List<string>();
            foreach (var c in commands)
            {
                if (skip.Contains(c, StringComparer.OrdinalIgnoreCase) || c.StartsWith("Sel", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (_challenge.Blocked.Contains(c, StringComparer.OrdinalIgnoreCase))
                    continue;
                if (route.Count == 0 || !string.Equals(route[route.Count - 1], c, StringComparison.OrdinalIgnoreCase))
                    route.Add(c);
            }
            return route;
        }

        private void SetStatus(Color accent, string heading, string text)
        {
            _status.Items.Clear();
            _status.Items.Add(Theme.Card(accent, Theme.Heading(heading), Theme.Body(text)));
        }

        // ---------------- The list of ways ----------------

        private void RefreshWays(string? highlight = null)
        {
            var p = Palette.Current;
            var found = Progress.WaysFound(_challenge.Id);
            var custom = found.Where(f => f.StartsWith("custom:")).ToList();
            int known = _challenge.Ways.Length;
            int foundKnown = _challenge.Ways.Count(w => found.Contains(w.Id));

            _waysArea.Items.Clear();
            _waysArea.Items.Add(Theme.Heading("Ways to build it"));
            _countLabel.Text = $"{foundKnown} of {known} found" + (custom.Count > 0 ? $" · +{custom.Count} of your own" : "");
            _countBar.Value = known == 0 ? 0 : (double)foundKnown / known;
            _waysArea.Items.Add(_countLabel);
            _waysArea.Items.Add(_countBar);

            // Found ways first, then the locked ones. Numbers follow the list order.
            var ordered = _challenge.Ways.Select((w, i) => (Way: w, Number: i + 1))
                .OrderBy(x => found.Contains(x.Way.Id) ? 0 : 1).ThenBy(x => x.Number);
            foreach (var (way, number) in ordered)
                _waysArea.Items.Add(found.Contains(way.Id)
                    ? WayCard(Glyph.Check, p.Green, way.Name, way.How, highlight == way.Id)
                    : LockedCard(number, way));

            if (custom.Count > 0)
            {
                _waysArea.Items.Add(Theme.Heading("Your own ways"));
                foreach (var c in custom)
                    _waysArea.Items.Add(WayCard(Glyph.Star, p.Purple, c.Substring("custom:".Length), "A route that isn't on the list. Nice thinking!", highlight == c));
            }
        }

        private Control WayCard(Glyph glyph, Color color, string name, string how, bool justFound)
        {
            var p = Palette.Current;
            var content = new TableLayout
            {
                Spacing = new Size(10, 0),
                Rows =
                {
                    new TableRow(new Badge(glyph, color, 34), new TableCell(new StackLayout
                    {
                        Spacing = 2,
                        HorizontalContentAlignment = HorizontalAlignment.Stretch,
                        Items = { Theme.Heading(name), Theme.Muted(how) },
                    }, true)),
                },
            };
            return justFound
                ? new StyledCard(content, Theme.Mix(color, p.Background, 0.85f), color, 8)
                : new StyledCard(content, padding: 8);
        }

        private Control LockedCard(int number, BlockedWay way)
        {
            var p = Palette.Current;
            bool revealed = _revealed.Contains(way.Id);
            var text = new StackLayout
            {
                Spacing = 2,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Items =
                {
                    Theme.Heading(revealed ? $"Way {number}: {way.Name}" : $"Way {number}: ???"),
                    Theme.Muted(revealed
                        ? way.How + " Build it to collect it."
                        : $"{way.Steps} step{(way.Steps == 1 ? "" : "s")}. Can you work it out?"),
                },
            };
            var rows = new StackLayout { Spacing = 6, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            rows.Items.Add(new TableLayout
            {
                Spacing = new Size(10, 0),
                Rows = { new TableRow(new Badge(Glyph.Lock, p.DisabledText, 34), new TableCell(text, true)) },
            });
            if (!revealed)
                rows.Items.Add(Theme.Wide(Theme.Button("Reveal", () => { _revealed.Add(way.Id); RefreshWays(); })));
            return new StyledCard(rows, padding: 8);
        }
    }
}
