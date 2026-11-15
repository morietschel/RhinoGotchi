using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using RhinoGotchi.Content;
using RhinoGotchi.Core;
using RhinoGotchi.Display;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// Deep Cut: a real Rhino model appears in the viewport and the player has 30 seconds to name a command
    /// that was used to build it. An AI judge looks at the viewport; the rarer the command, the more points.
    /// It opens on a ready screen: nothing is shown and the clock waits until the player presses Start.
    /// </summary>
    internal sealed class DeepCutView : StackLayout, IGameView
    {
        private const int RoundCount = 5;
        private const int PassHits = 3;
        private const double Seconds = 30;

        private readonly RhinoGotchiPanel _host;
        private readonly bool _practice;
        private readonly RhinoDoc? _doc;
        private readonly List<DeepCutModel> _models = new List<DeepCutModel>();
        private readonly List<JudgeResult> _results = new List<JudgeResult>();
        private int _index;
        private int _total;
        private bool _playing;
        private bool _started;

        /// <summary>Typing = clock running; Judging = waiting for the judge; Reveal = verdict shown.</summary>
        private enum Phase { Typing, Judging, Reveal }
        private Phase _phase = Phase.Reveal;
        private double _left = Seconds;
        private DateTime _deadline;
        private readonly UITimer _clock = new UITimer { Interval = 0.1 };

        private readonly GameHud _hud = new GameHud(clock: true);
        private readonly TableLayout _inputRow;
        private readonly Words _prompt = Theme.Heading("Name a command that built this model. Rarer scores more.");
        private readonly TextBox _input = new TextBox { PlaceholderText = "Type a command, e.g. Loft" };
        private readonly ChunkyButton _guess;
        private readonly Panel _suggestions = new Panel();
        private readonly StackLayout _feedback = new StackLayout { Spacing = 8, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        private readonly StackLayout _ready = new StackLayout { Spacing = 10, HorizontalContentAlignment = HorizontalAlignment.Stretch };

        public DeepCutView(RhinoGotchiPanel host, bool practice)
        {
            _host = host;
            _practice = practice;
            _doc = RhinoDoc.ActiveDoc;
            _guess = Theme.Primary("Guess", () => Submit(DeepCutModels.Resolve(_input.Text)));

            Theme.SetUpPage(this, host,
                practice ? "Deep Cut · Practice" : "Deep Cut", "");
            _inputRow = new TableLayout
            {
                Spacing = new Size(6, 0),
                Rows = { new TableRow(new TableCell(_input, true), new TableCell(_guess)) },
            };

            var stats = Progress.Get(Games.Deep);
            if (!practice && stats.PlayedToday)
            {
                Items.Add(Theme.Card(Theme.Tile,
                    Theme.Heading($"Today's round is done: {stats.LastResult}"),
                    Theme.Muted("A new one arrives tomorrow.")));
                Items.Add(PracticeButton());
                return;
            }
            if (_doc == null)
            {
                Items.Add(Theme.Body("Open a Rhino document to play. This game draws in the viewport."));
                return;
            }

            // A fresh draw every time the game is opened, daily or practice, so reopening never repeats the same five.
            _models.AddRange(Daily.Shuffle(DeepCutModels.All, new Random()).Take(RoundCount));
            if (_models.Count == 0)
            {
                Items.Add(Theme.Body("The Deep Cut models are missing. They should be in a DeepCut/Models folder next to the plugin file."));
                return;
            }

            _input.TextChanged += (s, e) => ShowSuggestions();
            _input.KeyDown += (s, e) =>
            {
                if (e.Key == Keys.Enter)
                {
                    e.Handled = true;
                    Submit(DeepCutModels.Resolve(_input.Text));
                }
            };
            _clock.Elapsed += (s, e) => Tick();

            // The ready screen: the clock waits at full time and the model stays hidden until Start.
            var start = Theme.Primary("Start", Begin);
            start.Fill = true;
            start.Height = 42;
            _ready.Items.Add(Theme.Heading("Name a command that built each model. Rarer scores more."));
            _ready.Items.Add(Theme.Body($"{_models.Count} models, {Seconds:0} seconds each. The model appears and the clock starts when you press Start."));
            _ready.Items.Add(new StackLayoutItem(start, HorizontalAlignment.Stretch));
            if (!practice)
                _ready.Items.Add(PracticeButton());
            _prompt.Visible = _inputRow.Visible = false;
            _hud.Set($"{_models.Count} MODELS", "0 pts", Enumerable.Repeat(Pip.Todo, _models.Count));
            _hud.SetTime(Seconds, Seconds);

            Items.Add(_hud);
            Items.Add(_ready);
            Items.Add(_prompt);
            Items.Add(_inputRow);
            Items.Add(_suggestions);
            Items.Add(_feedback);
            Items.Add(Theme.Small("Each guess is judged by OpenAI from a picture of the viewport."));
            _playing = true;
        }

        /// <summary>Start was pressed: the ready screen goes and the first model and its clock begin.</summary>
        private void Begin()
        {
            if (_started || !_playing || _doc == null)
                return;
            _started = true;
            _ready.Visible = false;
            ShowRound();
        }

        /// <summary>Brings the model and the clock back when the panel is shown again (nothing before Start).</summary>
        public void Enter()
        {
            if (!_playing || _doc == null || !_started)
                return;
            if (!ModelPreview.Active)
                ModelPreview.Show(_doc, _models[_index], frame: true);
            RhinoGotchi.Display.ViewportPet.Show(PetId, "Name a command that was used to model this.");
            if (_phase == Phase.Typing && !_clock.Started)
                StartClock();
        }

        /// <summary>Pauses the clock and gives the viewport back.</summary>
        public void Leave()
        {
            if (_clock.Started)
            {
                _left = Math.Max(0, (_deadline - DateTime.UtcNow).TotalSeconds);
                _clock.Stop();
            }
            ModelPreview.Stop();
            RhinoGotchi.Display.ViewportPet.Hide(PetId);
        }

        private const string PetId = "deep";

        // ---------------- rounds ----------------

        private void ShowRound()
        {
            // A model that fails to load is swapped for one that isn't in today's set.
            while (!_models[_index].Load())
            {
                var spare = DeepCutModels.All.FirstOrDefault(m => !_models.Contains(m) && m.Load());
                if (spare == null)
                {
                    _feedback.Items.Clear();
                    _feedback.Items.Add(Theme.Body("This model could not be loaded."));
                    return;
                }
                _models[_index] = spare;
            }

            UpdateHeader();
            if (!RhinoGotchi.Display.ViewportPet.IsShownBy(PetId))
                RhinoGotchi.Display.ViewportPet.Show(PetId, "Name a command that was used to model this.");
            else
                RhinoGotchi.Display.ViewportPet.Say(PetId, $"Model {_index + 1}: name a command used to make it.");
            _feedback.Items.Clear();
            _input.Text = "";
            _input.Enabled = _guess.Enabled = true;
            _inputRow.Visible = _prompt.Visible = true;
            _suggestions.Content = null;
            ModelPreview.Show(_doc!, _models[_index], frame: true);

            _phase = Phase.Typing;
            _left = Seconds;
            StartClock();
            _input.Focus();
        }

        private void UpdateHeader()
        {
            var pips = Enumerable.Range(0, _models.Count).Select(i =>
                i < _results.Count ? (_results[i].Points > 0 ? Pip.Good : Pip.Bad) : i == _index && _playing ? Pip.Now : Pip.Todo);
            _hud.Set(_playing ? $"MODEL {_index + 1} OF {_models.Count}" : "FINISHED", $"{_total:N0} pts", pips);
        }

        private void StartClock()
        {
            _deadline = DateTime.UtcNow.AddSeconds(_left);
            _hud.SetTime(_left, Seconds);
            _clock.Start();
        }

        private void Tick()
        {
            _left = Math.Max(0, (_deadline - DateTime.UtcNow).TotalSeconds);
            _hud.SetTime(_left, Seconds);
            if (_left > 0)
                return;
            _clock.Stop();
            if (_phase == Phase.Typing)
                Reveal(new JudgeResult { Command = _input.Text.Trim().Length > 0 ? _input.Text.Trim() : "-", Verdict = Verdict.TimedOut });
        }

        private void ShowSuggestions()
        {
            if (_phase != Phase.Typing)
                return;
            var matches = DeepCutModels.Suggest(_input.Text, 6);
            if (matches.Count == 0 || (matches.Count == 1 && string.Equals(matches[0], _input.Text.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                _suggestions.Content = null;
                return;
            }
            var buttons = matches.Select(m =>
            {
                var b = new ChunkyButton(m, ButtonKind.Outline, 30) { Font = new Font(SystemFont.Bold, 9) };
                b.Click += (s, e) => Submit(m);
                return (Control)b;
            }).ToArray();
            _suggestions.Content = Theme.Grid(2, buttons);
        }

        // ---------------- judging ----------------

        private void Submit(string? command)
        {
            if (_phase != Phase.Typing || command == null)
                return;
            _clock.Stop();
            _left = Math.Max(0, (_deadline - DateTime.UtcNow).TotalSeconds);
            _input.Text = command;
            _suggestions.Content = null;

            var picture = ModelPreview.Capture(1280);
            if (picture == null)
            {
                Problem(command, null, "Could not take a picture of the viewport for the judge.", keyProblem: false);
                return;
            }
            Ask(command, picture);
        }

        private void Ask(string command, byte[] picture)
        {
            _phase = Phase.Judging;
            _input.Enabled = _guess.Enabled = false;
            _feedback.Items.Clear();
            _feedback.Items.Add(Theme.Card(Theme.Tile,
                Theme.Heading($"Asking the judge about {command}…"),
                Theme.Muted("The clock is stopped.")));

            int round = _index;
            Task.Run(async () =>
            {
                JudgeResult? result = null;
                JudgeException? error = null;
                try { result = await DeepCutJudge.Judge(command, picture).ConfigureAwait(false); }
                catch (JudgeException ex) { error = ex; }
                catch (Exception ex) { error = new JudgeException(ex.Message, keyProblem: false); }

                Application.Instance.AsyncInvoke(() =>
                {
                    // The player may have left the game or the round while the judge was thinking.
                    if (!_playing || _phase != Phase.Judging || round != _index)
                        return;
                    if (result != null)
                        Reveal(result);
                    else
                        Problem(command, picture, error!.Message, error.KeyProblem);
                });
            });
        }

        /// <summary>The judge could not answer. The clock stays stopped and the same guess can be sent again.</summary>
        private void Problem(string command, byte[]? picture, string message, bool keyProblem)
        {
            _phase = Phase.Judging;
            _feedback.Items.Clear();
            Action retry = () =>
            {
                var again = picture ?? ModelPreview.Capture(1280);
                if (again != null)
                    Ask(command, again);
            };
            Action skip = () => Reveal(new JudgeResult { Command = command, Verdict = Verdict.NoVerdict });

            if (!keyProblem)
            {
                _feedback.Items.Add(Theme.Card(Palette.Current.Red,
                    Theme.Heading("The judge didn't answer"),
                    Theme.Body(message)));
                _feedback.Items.Add(Theme.Row(Theme.Primary("Try again", retry), Theme.Button("Skip this model", skip)));
                return;
            }

            var keyBox = new PasswordBox();
            _feedback.Items.Add(Theme.Card(Palette.Current.Gold,
                Theme.Heading(ApiKey.UsingOwn ? "Your OpenAI key was rejected" : "The built-in OpenAI key no longer works"),
                Theme.Body(message),
                Theme.Body("Paste your own OpenAI API key to keep playing. It is saved in the plugin settings on this computer; you can change it later under Settings on the home page.")));
            _feedback.Items.Add(keyBox);
            _feedback.Items.Add(Theme.Row(
                Theme.Primary("Save key and retry", () =>
                {
                    if (keyBox.Text.Trim().Length == 0)
                        return;
                    ApiKey.SetOwn(keyBox.Text);
                    retry();
                }),
                Theme.Button("Skip this model", skip)));
            keyBox.Focus();
        }

        private void Reveal(JudgeResult result)
        {
            _phase = Phase.Reveal;
            if (result.Verdict == Verdict.Scored)
                RhinoGotchi.Display.ViewportPet.Cheer(PetId, $"+{result.Points}! {result.Command} was used here.");
            else if (result.Verdict == Verdict.NotUsed)
                RhinoGotchi.Display.ViewportPet.Groan(PetId, $"{result.Command} wasn't used here.");
            else if (result.Verdict == Verdict.TimedOut)
                RhinoGotchi.Display.ViewportPet.Groan(PetId, "Out of time!");
            _clock.Stop();
            _input.Enabled = _guess.Enabled = false;
            _inputRow.Visible = _prompt.Visible = false;
            _suggestions.Content = null;
            _results.Add(result);
            _total += result.Points;
            UpdateHeader();

            var p = Palette.Current;
            string headline = result.Verdict switch
            {
                Verdict.Scored => $"+{result.Points} · {result.Command}",
                Verdict.NotUsed => $"0 · {result.Command} wasn't used here",
                Verdict.TimedOut => "Out of time",
                _ => "No verdict",
            };
            string detail = result.Verdict switch
            {
                Verdict.Scored => result.Tier,
                Verdict.NotUsed => $"It would have scored {Math.Max(25, (int)Math.Round(1000 * result.Rarity))}.",
                Verdict.TimedOut => "No guess in 30 seconds.",
                _ => "The judge couldn't score this one.",
            };

            bool judged = result.Verdict == Verdict.Scored || result.Verdict == Verdict.NotUsed;
            var rows = new List<Control> { Theme.Heading(headline), Theme.Muted(detail) };
            if (judged)
            {
                rows.Add(new Meter("USED HERE", result.Used, result.Verdict == Verdict.Scored ? p.Green : p.Red));
                rows.Add(new Meter("RARITY", result.Rarity, p.Ink));
            }

            _feedback.Items.Clear();
            _feedback.Items.Add(Theme.Card(result.Points > 0 ? Theme.Hit : p.Red, rows.ToArray()));

            bool last = _index == _models.Count - 1;
            _feedback.Items.Add(Theme.Primary(last ? "See my score" : "Next model →", () =>
            {
                if (last)
                    Finish();
                else
                {
                    _index++;
                    ShowRound();
                }
            }));
            if (judged)
                _feedback.Items.Add(Theme.Row(
                    Theme.Secondary($"Try {result.Command}", () => RhinoActions.Run(result.Command)),
                    Theme.Button("Help page", () => RhinoActions.OpenHelp(result.Command))));
            _feedback.Items.Add(SourceLink(_models[_index]));
        }

        private void Finish()
        {
            _playing = false;
            ModelPreview.Stop();
            RhinoGotchi.Display.ViewportPet.Hide(PetId);
            int hits = _results.Count(r => r.Points > 0);
            string result = $"{_total} pts";
            if (!_practice)
                Progress.Record(Games.Deep, hits >= PassHits, result);
            // Practice rounds count for the leaderboard too: it shows your best round.
            bool newBest = Leaderboard.Report(Games.Deep, _total);

            UpdateHeader();
            _hud.Clock = false;
            _inputRow.Visible = _prompt.Visible = false;
            _suggestions.Content = null;

            var p = Palette.Current;
            _feedback.Items.Clear();
            _feedback.Items.Add(Theme.Card(hits >= PassHits ? Theme.Hit : Theme.Tile,
                Theme.Heading($"{_total:N0} points"),
                Theme.Body(Rank(_total / (_models.Count * 1000.0)))));

            var table = new TableLayout { Spacing = new Size(8, 6) };
            for (int i = 0; i < _results.Count; i++)
            {
                var r = _results[i];
                table.Rows.Add(new TableRow(
                    new TableCell(new Label { Text = (i + 1).ToString(), Font = Theme.HeadingFont, TextColor = p.SubtleText }),
                    new TableCell(Theme.Body(r.Command), true),
                    new TableCell(new Label
                    {
                        Text = r.Points > 0 ? "+" + r.Points.ToString("N0") : "0",
                        Font = Theme.HeadingFont,
                        TextColor = r.Points > 0 ? p.Green : p.SubtleText,
                        TextAlignment = TextAlignment.Right,
                    })));
            }
            _feedback.Items.Add(Theme.Box(table, padding: 12));
            if (newBest)
                _feedback.Items.Add(LeaderboardView.NewBestRow(_host, _total));
            _feedback.Items.Add(PracticeButton());
        }

        private static string Rank(double share) =>
            share >= 0.8 ? "Command-line archaeologist." :
            share >= 0.6 ? "Deep menu diver." :
            share >= 0.4 ? "Alias collector." :
            share >= 0.2 ? "Toolbar clicker." : "Just installed Rhino. Go for commands that fit the model first, rare second.";

        /// <summary>A link that wraps. Eto's LinkButton is one long line, which made the whole page wider.</summary>
        private static Control SourceLink(DeepCutModel model)
        {
            var link = Theme.Small("From the McNeel forum: " + model.Title + " ↗");
            link.TextColor = Palette.Current.Blue;
            link.Cursor = Cursors.Pointer;
            link.MouseDown += (s, e) =>
            {
                try { Application.Instance.Open(model.Source); }
                catch (Exception ex) { RhinoApp.WriteLine("RhinoGotchi: could not open {0} ({1}).", model.Source, ex.Message); }
            };
            return link;
        }

        private StackLayoutItem PracticeButton() => new StackLayoutItem(
            Theme.Button("Play a practice round", () => _host.Navigate(new DeepCutView(_host, practice: true))),
            HorizontalAlignment.Left);
    }

    /// <summary>A labelled percentage bar, e.g. how sure the judge is that a command was used.</summary>
    internal sealed class Meter : Drawable
    {
        private readonly string _label;
        private readonly double _value;
        private readonly Color _color;

        public Meter(string label, double value, Color color)
        {
            _label = label;
            _value = Math.Max(0, Math.Min(1, value));
            _color = color;
            Height = 30;
            SizeChanged += (s, e) => Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var p = Palette.Current;
            g.AntiAlias = true;
            float w = Width - 1;
            if (w < 40)
                return;
            var font = Ui.Bold(8.5f);
            string percent = Math.Round(_value * 100) + "%";
            var percentSize = font.MeasureString(percent);
            g.DrawText(font, p.SubtleText, 0, 2, Ui.Ellipsize(font, _label, w - percentSize.Width - 6));
            g.DrawText(font, p.Text, w - percentSize.Width, 2, percent);

            const float h = 6, y = 20;
            g.FillPath(p.Track, GraphicsPath.GetRoundRect(new RectangleF(0, y, w, h), h / 2));
            float fill = (float)(w * _value);
            if (fill >= h)
                g.FillPath(_color, GraphicsPath.GetRoundRect(new RectangleF(0, y, fill, h), h / 2));
        }
    }
}
