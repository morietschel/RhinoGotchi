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

        private readonly DeepCutHud _hud = new DeepCutHud();
        private readonly TableLayout _inputRow;
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
                "Name a command that was used to build the model in the viewport. Rarer commands score more.");
            _inputRow = new TableLayout
            {
                Spacing = new Size(6, 0),
                Rows = { new TableRow(new TableCell(_input, true), new TableCell(_guess)) },
            };

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

            Items.Add(_hud);
            Items.Add(_inputRow);
            Items.Add(_suggestions);
            Items.Add(_feedback);
            Items.Add(Small("30 seconds and one guess per model. Your own model is hidden while you play and nothing is added to it. A picture of the viewport is sent to OpenAI to judge each guess."));
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
            _hud.SetRound(_index, _models.Count, _total, _results.Select(r => r.Points).ToList());

        private static Label Small(string text) => new Label
        {
            Text = text,
            Wrap = WrapMode.Word,
            Font = new Font(SystemFont.Default, 8.5f),
            TextColor = Palette.Current.SubtleText,
        };

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
                Theme.Muted("This takes a few seconds. The clock is stopped.")));

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
            _clock.Stop();
            _input.Enabled = _guess.Enabled = false;
            _suggestions.Content = null;
            _results.Add(result);
            _total += result.Points;
            UpdateHeader();

            var p = Palette.Current;
            string headline = result.Verdict switch
            {
                Verdict.Scored => $"+{result.Points} for {result.Command}",
                Verdict.NotUsed => $"{result.Command} wasn't used here",
                Verdict.TimedOut => "Out of time",
                _ => "No verdict",
            };
            string detail = result.Verdict switch
            {
                Verdict.Scored => $"{result.Tier}: the judge agrees it was used here.",
                Verdict.NotUsed => $"The judge doesn't think this model needed it. It would have been worth {Math.Max(25, (int)Math.Round(1000 * result.Rarity))}.",
                Verdict.TimedOut => "No guess was sent in 30 seconds.",
                _ => "The judge couldn't score this one, so it counts as zero.",
            };

            bool judged = result.Verdict == Verdict.Scored || result.Verdict == Verdict.NotUsed;
            var rows = new List<Control> { Theme.Heading(headline), Theme.Body(detail) };
            if (judged)
            {
                rows.Add(new Meter("Used on this model", result.Used, result.Verdict == Verdict.Scored ? p.Green : p.Red));
                rows.Add(new Meter("How rare it is", result.Rarity, p.Ink));
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
            int hits = _results.Count(r => r.Points > 0);
            string result = $"{_total} pts";
            if (!_practice)
                Progress.Record(Games.Deep, hits >= PassHits, result);

            UpdateHeader();
            _hud.Finished = true;
            _inputRow.Visible = false;
            _suggestions.Content = null;

            var p = Palette.Current;
            _feedback.Items.Clear();
            _feedback.Items.Add(Theme.Card(hits >= PassHits ? Theme.Hit : Theme.Tile,
                Theme.Heading($"You scored {_total:N0} of {_models.Count * 1000:N0}"),
                Theme.Body(Rank(_total / (_models.Count * 1000.0)))));

            var table = new TableLayout { Spacing = new Size(8, 6) };
            for (int i = 0; i < _results.Count; i++)
            {
                var r = _results[i];
                table.Rows.Add(new TableRow(
                    new TableCell(new Label { Text = (i + 1).ToString(), Font = Theme.HeadingFont, TextColor = p.SubtleText }),
                    new TableCell(new Label { Text = r.Command, Font = Theme.BodyFont, TextColor = p.Text, Wrap = WrapMode.Word }, true),
                    new TableCell(new Label
                    {
                        Text = r.Points > 0 ? "+" + r.Points.ToString("N0") : "0",
                        Font = Theme.HeadingFont,
                        TextColor = r.Points > 0 ? p.Green : p.SubtleText,
                        TextAlignment = TextAlignment.Right,
                    })));
            }
            _feedback.Items.Add(Theme.Box(table, padding: 12));
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
            var link = Small("Model from the McNeel forum: " + model.Title + " ↗");
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

    /// <summary>
    /// The strip at the top of a Deep Cut round: which model this is, the points so far, one pip per
    /// model (green = scored, red = nothing, dark = now) and the clock, which turns red for the last ten seconds.
    /// </summary>
    internal sealed class DeepCutHud : Drawable
    {
        private int _index, _count = 1, _total;
        private IReadOnlyList<int> _points = new int[0];
        private double _left, _seconds = 1;
        private bool _finished;

        public DeepCutHud()
        {
            Height = 84;
            SizeChanged += (s, e) => Invalidate();
        }

        /// <summary>After the last model the clock is hidden and every pip shows its result.</summary>
        public bool Finished
        {
            get => _finished;
            set { _finished = value; Height = value ? 58 : 84; Invalidate(); }
        }

        public void SetRound(int index, int count, int total, IReadOnlyList<int> points)
        {
            _index = index;
            _count = Math.Max(1, count);
            _total = total;
            _points = points;
            Invalidate();
        }

        public void SetTime(double left, double seconds)
        {
            _left = left;
            _seconds = Math.Max(1, seconds);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var p = Palette.Current;
            g.AntiAlias = true;
            float w = Width - 1;
            if (w < 80)
                return;
            Ui.Chunky(g, new RectangleF(0, 0, w, Height - 1), 12, p.TileFace, p.Border, lip: 3);

            const float pad = 12;
            float inner = w - 2 * pad;

            // Which model, and the points so far.
            string points = _total.ToString("N0") + " pts";
            var pointsFont = Ui.Bold(12);
            var pointsSize = pointsFont.MeasureString(points);
            g.DrawText(pointsFont, p.Text, w - pad - pointsSize.Width, 9, points);
            string round = _finished ? "FINISHED" : $"MODEL {_index + 1} OF {_count}";
            Ui.Eyebrow(g, round, pad, 12, p.SubtleText);

            // One pip per model.
            const float gap = 4, pipHeight = 6;
            float pip = (inner - gap * (_count - 1)) / _count;
            for (int i = 0; i < _count; i++)
            {
                Color color = i < _points.Count ? (_points[i] > 0 ? p.Green : p.Red)
                    : i == _index && !_finished ? p.Ink : p.Track;
                g.FillPath(color, GraphicsPath.GetRoundRect(new RectangleF(pad + i * (pip + gap), 34, pip, pipHeight), pipHeight / 2));
            }
            if (_finished)
                return;

            // The clock.
            bool hurry = _left <= 10;
            string seconds = Math.Ceiling(_left).ToString("0") + "s";
            var clockFont = Ui.Bold(11);
            var clockSize = clockFont.MeasureString("30s");
            float barWidth = inner - clockSize.Width - 8, barHeight = 10, y = 54;
            g.FillPath(p.Track, GraphicsPath.GetRoundRect(new RectangleF(pad, y, barWidth, barHeight), barHeight / 2));
            float fill = (float)(barWidth * Math.Max(0, Math.Min(1, _left / _seconds)));
            if (fill >= barHeight)
                g.FillPath(hurry ? p.Red : p.Ink, GraphicsPath.GetRoundRect(new RectangleF(pad, y, fill, barHeight), barHeight / 2));
            var size = clockFont.MeasureString(seconds);
            g.DrawText(clockFont, hurry ? p.Red : p.Text, w - pad - size.Width, y + (barHeight - size.Height) / 2, seconds);
        }
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
