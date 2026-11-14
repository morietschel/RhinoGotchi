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

        private readonly Label _roundLabel = Theme.Heading("");
        private readonly TimerBar _timerBar = new TimerBar();
        private readonly TextBox _input = new TextBox { PlaceholderText = "Type a command, e.g. Loft" };
        private readonly ChunkyButton _guess;
        private readonly Panel _suggestions = new Panel();
        private readonly StackLayout _feedback = new StackLayout { Spacing = 8, HorizontalContentAlignment = HorizontalAlignment.Stretch };

        public DeepCutView(RhinoGotchiPanel host, bool practice)
        {
            _host = host;
            _practice = practice;
            _doc = RhinoDoc.ActiveDoc;
            _guess = Theme.Primary("Guess", () => Submit(DeepCutModels.Resolve(_input.Text)));

            Theme.SetUpPage(this, host,
                practice ? "Deep Cut · Practice" : "Deep Cut",
                "A real Rhino model is in the viewport. Orbit it, then name a command that was used to build it. The rarer the command, the more points. 30 seconds, one guess.");

            var stats = Progress.Get(Games.Deep);
            if (!practice && stats.PlayedToday)
            {
                Items.Add(Theme.Body($"You've played today's round ({stats.LastResult}). A new one arrives tomorrow."));
                Items.Add(PracticeButton());
                return;
            }
            if (_doc == null)
            {
                Items.Add(Theme.Body("Open a Rhino document to play. This game draws in the viewport."));
                return;
            }

            _models.AddRange(Daily.Shuffle(DeepCutModels.All, Daily.Rng(Games.Deep, practice)).Take(RoundCount));
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

            Items.Add(_roundLabel);
            Items.Add(_timerBar);
            Items.Add(new TableLayout
            {
                Spacing = new Size(6, 0),
                Rows = { new TableRow(new TableCell(_input, true), new TableCell(_guess)) },
            });
            Items.Add(_suggestions);
            Items.Add(_feedback);
            Items.Add(Theme.Muted("Your own model is hidden in that viewport while you play and nothing is added to it. A picture of the viewport is sent to OpenAI to judge each guess."));
            _playing = true;
        }

        /// <summary>Starts the first round, or brings the model and the clock back when the panel is shown again.</summary>
        public void Enter()
        {
            if (!_playing || _doc == null)
                return;
            if (!_started)
            {
                _started = true;
                ShowRound();
                return;
            }
            if (!ModelPreview.Active)
                ModelPreview.Show(_doc, _models[_index], frame: true);
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
        }

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
            _feedback.Items.Clear();
            _input.Text = "";
            _input.Enabled = _guess.Enabled = true;
            _suggestions.Content = null;
            ModelPreview.Show(_doc!, _models[_index], frame: true);

            _phase = Phase.Typing;
            _left = Seconds;
            StartClock();
            _input.Focus();
        }

        private void UpdateHeader() =>
            _roundLabel.Text = $"Model {_index + 1} of {_models.Count} · {_total} pts";

        private void StartClock()
        {
            _deadline = DateTime.UtcNow.AddSeconds(_left);
            _timerBar.Set(_left, Seconds);
            _clock.Start();
        }

        private void Tick()
        {
            _left = Math.Max(0, (_deadline - DateTime.UtcNow).TotalSeconds);
            _timerBar.Set(_left, Seconds);
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
            _feedback.Items.Add(Theme.Card(Theme.Tile, new Label { Text = $"Asking the judge about {command}…", Font = Theme.HeadingFont }));

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
                    new Label { Text = "The judge didn't answer", Font = Theme.HeadingFont },
                    Theme.Body(message)));
                _feedback.Items.Add(Theme.Row(Theme.Primary("Try again", retry), Theme.Button("Skip this model", skip)));
                return;
            }

            var keyBox = new PasswordBox();
            _feedback.Items.Add(Theme.Card(Palette.Current.Gold,
                new Label { Text = ApiKey.UsingOwn ? "Your OpenAI key was rejected" : "The built-in OpenAI key no longer works", Font = Theme.HeadingFont },
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
            _clock.Stop();
            _input.Enabled = _guess.Enabled = false;
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
                Verdict.Scored => $"{result.Tier}. The judge is {Percent(result.Used)} sure it was used and rates it {Percent(result.Rarity)} rare.",
                Verdict.NotUsed => $"The judge is only {Percent(result.Used)} sure this model needed it. It would have been worth {Math.Max(25, (int)Math.Round(1000 * result.Rarity))}.",
                Verdict.TimedOut => "No guess was sent in 30 seconds.",
                _ => "The judge couldn't score this one, so it counts as zero.",
            };

            _feedback.Items.Clear();
            _feedback.Items.Add(Theme.Card(result.Points > 0 ? Theme.Hit : p.Red,
                new Label { Text = headline, Font = Theme.HeadingFont },
                Theme.Body(detail)));
            if (result.Verdict == Verdict.Scored || result.Verdict == Verdict.NotUsed)
                _feedback.Items.Add(Theme.CommandButtons(result.Command));
            _feedback.Items.Add(SourceLink(_models[_index]));

            bool last = _index == _models.Count - 1;
            _feedback.Items.Add(new StackLayoutItem(
                Theme.Secondary(last ? "See score" : "Next model →", () =>
                {
                    if (last)
                        Finish();
                    else
                    {
                        _index++;
                        ShowRound();
                    }
                }),
                HorizontalAlignment.Left));
        }

        private void Finish()
        {
            _playing = false;
            ModelPreview.Stop();
            int hits = _results.Count(r => r.Points > 0);
            string result = $"{_total} pts";
            if (!_practice)
                Progress.Record(Games.Deep, hits >= PassHits, result);

            _roundLabel.Text = "Finished";
            _timerBar.Visible = false;
            _input.Visible = _guess.Visible = false;
            _feedback.Items.Clear();
            _feedback.Items.Add(Theme.Card(hits >= PassHits ? Theme.Hit : Theme.Tile,
                new Label { Text = $"You scored {_total} of {_models.Count * 1000}", Font = Theme.HeadingFont },
                Theme.Body(Rank(_total / (_models.Count * 1000.0)))));
            for (int i = 0; i < _results.Count; i++)
                _feedback.Items.Add(Theme.Body($"{i + 1}. {_results[i].Command} · {_results[i].Points}"));
            _feedback.Items.Add(PracticeButton());
        }

        private static string Rank(double share) =>
            share >= 0.8 ? "Command-line archaeologist." :
            share >= 0.6 ? "Deep menu diver." :
            share >= 0.4 ? "Alias collector." :
            share >= 0.2 ? "Toolbar clicker." : "Just installed Rhino. Go for commands that fit the model first, rare second.";

        private static string Percent(double x) => $"{Math.Round(x * 100)}%";

        private static Control SourceLink(DeepCutModel model)
        {
            var link = new LinkButton { Text = "From the McNeel forum: " + model.Title };
            link.Click += (s, e) =>
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

    /// <summary>The round clock: a bar that drains and turns red for the last ten seconds.</summary>
    internal sealed class TimerBar : Drawable
    {
        private double _left, _total = 1;
        private readonly Font _font = new Font(SystemFont.Bold, 9);

        public TimerBar()
        {
            Height = 22;
            SizeChanged += (s, e) => Invalidate();
        }

        public void Set(double left, double total)
        {
            _left = left;
            _total = total;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var p = Palette.Current;
            g.AntiAlias = true;
            string text = Math.Ceiling(_left).ToString("0") + "s";
            float label = 34, w = Width - label - 1, h = 12, y = (Height - h) / 2;
            if (w <= h)
                return;
            g.FillPath(p.Track, GraphicsPath.GetRoundRect(new RectangleF(0, y, w, h), h / 2));
            float fill = (float)(w * Math.Max(0, Math.Min(1, _left / _total)));
            if (fill >= h)
                g.FillPath(_left <= 10 ? p.Red : p.Blue, GraphicsPath.GetRoundRect(new RectangleF(0, y, fill, h), h / 2));
            var size = _font.MeasureString(text);
            g.DrawText(_font, _left <= 10 ? p.Red : p.SubtleText, Width - size.Width - 1, (Height - size.Height) / 2, text);
        }
    }
}
