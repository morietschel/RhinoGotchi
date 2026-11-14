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
    /// Shows real geometry in the viewport (inputs in orange, result in blue) and asks which command made it.
    /// </summary>
    internal sealed class ShapeQuizView : StackLayout, IGameView
    {
        private const int RoundCount = 5;
        private const int PassScore = 3;
        /// <summary>Leaderboard points for each right answer in the daily round.</summary>
        private const int PointsPerAnswer = 20;

        private sealed class Round
        {
            public Round(ShapeRound shape, ShapeGeometry geometry, List<string> options)
            {
                Shape = shape;
                Geometry = geometry;
                Options = options;
            }

            public ShapeRound Shape { get; }
            public ShapeGeometry Geometry { get; }
            public List<string> Options { get; }
        }

        private readonly RhinoGotchiPanel _host;
        private readonly bool _practice;
        private readonly RhinoDoc? _doc;
        private readonly List<Round> _rounds = new List<Round>();
        private int _index;
        private int _score;
        private bool _playing;
        private bool _started;

        private readonly Label _roundLabel = Theme.Heading("");
        private readonly Panel _optionsHost = new Panel();
        private readonly StackLayout _feedback = new StackLayout { Spacing = 8, HorizontalContentAlignment = HorizontalAlignment.Stretch };

        public ShapeQuizView(RhinoGotchiPanel host, bool practice)
        {
            _host = host;
            _practice = practice;
            _doc = RhinoDoc.ActiveDoc;

            Theme.SetUpPage(this, host,
                practice ? "What Made This? · Practice" : "What Made This?",
                "Look at the viewport. Orange is what you start with, blue is the result. Which command made it?");

            var stats = Progress.Get(Games.Shape);
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

            BuildRounds(Daily.Rng(Games.Shape, practice), _doc.ModelAbsoluteTolerance);
            Items.Add(_roundLabel);
            Items.Add(_optionsHost);
            Items.Add(_feedback);
            Items.Add(Theme.Muted("Nothing is added to your model. The preview disappears and your view comes back when you leave."));
            _playing = _rounds.Count > 0;
        }

        /// <summary>Starts the first round, or redraws the current one when the panel is shown again.</summary>
        public void Enter()
        {
            if (!_playing)
                return;
            if (!_started)
            {
                _started = true;
                ShowRound();
            }
            else if (_doc != null)
            {
                ShapePreview.Show(_doc, _rounds[_index].Geometry);
            }
        }

        public void Leave() => ShapePreview.Stop();

        private void BuildRounds(Random rng, double tolerance)
        {
            var pool = ShapeRounds.All.Select(r => r.Command).Concat(ShapeRounds.ExtraDistractors).Distinct().ToList();
            foreach (var shape in Daily.Shuffle(ShapeRounds.All, rng))
            {
                if (_rounds.Count == RoundCount)
                    break;
                var geometry = shape.Build(tolerance);
                if (geometry == null)
                    continue;
                var wrong = Daily.Shuffle(pool.Where(c => c != shape.Command), rng).Take(3);
                var options = Daily.Shuffle(wrong.Append(shape.Command), rng);
                _rounds.Add(new Round(shape, geometry, options));
            }
        }

        private void ShowRound()
        {
            var round = _rounds[_index];
            _roundLabel.Text = $"Round {_index + 1} of {_rounds.Count} · Score {_score}";
            _feedback.Items.Clear();

            var buttons = new List<ChunkyButton>();
            var table = new TableLayout { Spacing = new Size(6, 6) };
            for (int i = 0; i < round.Options.Count; i += 2)
            {
                var row = new TableRow();
                for (int j = i; j < Math.Min(i + 2, round.Options.Count); j++)
                {
                    string option = round.Options[j];
                    var button = new ChunkyButton { Text = option, Height = 34 };
                    button.Click += (s, e) => Answer(round, option, buttons);
                    buttons.Add(button);
                    row.Cells.Add(new TableCell(button, true));
                }
                table.Rows.Add(row);
            }
            _optionsHost.Content = table;

            if (_doc != null)
                ShapePreview.Show(_doc, round.Geometry);
        }

        private void Answer(Round round, string option, List<ChunkyButton> buttons)
        {
            bool right = option == round.Shape.Command;
            if (right)
                _score++;

            foreach (var b in buttons)
            {
                b.Enabled = false;
                var p = Palette.Current;
                if (b.Text == round.Shape.Command)
                {
                    b.SetColors(p.Green, p.GreenShadow, Colors.White);
                    b.Text = "✓ " + b.Text;
                }
                else if (b.Text == option)
                {
                    b.SetColors(p.Red, p.RedShadow, Colors.White);
                    b.Text = "✗ " + b.Text;
                }
            }
            _roundLabel.Text = $"Round {_index + 1} of {_rounds.Count} · Score {_score}";

            _feedback.Items.Clear();
            _feedback.Items.Add(Theme.Card(right ? Theme.Hit : Palette.Current.Red,
                new Label { Text = right ? $"Right: {round.Shape.Command}" : $"It was {round.Shape.Command}", Font = Theme.HeadingFont },
                Theme.Body(round.Shape.Explanation)));
            _feedback.Items.Add(Theme.CommandButtons(round.Shape.Command));

            bool last = _index == _rounds.Count - 1;
            _feedback.Items.Add(new StackLayoutItem(
                Theme.Secondary(last ? "See score" : "Next shape →", () =>
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
            ShapePreview.Stop();
            string result = $"{_score}/{_rounds.Count}";
            int earned = 0;
            if (!_practice)
            {
                Progress.Record(Games.Shape, _score >= PassScore, result);
                earned = Leaderboard.Award(Games.Shape, _score * PointsPerAnswer);
            }

            _roundLabel.Text = "Finished";
            _optionsHost.Content = null;
            _feedback.Items.Clear();
            _feedback.Items.Add(Theme.Card(_score >= PassScore ? Theme.Hit : Theme.Tile,
                new Label { Text = $"You scored {result}", Font = Theme.HeadingFont },
                Theme.Body(_score == _rounds.Count
                    ? "Perfect. You know your surfacing tools."
                    : "Use Try on any command to build the shape yourself next time.")));
            if (earned > 0)
                _feedback.Items.Add(LeaderboardView.EarnedRow(_host, earned));
            _feedback.Items.Add(PracticeButton());
        }

        private StackLayoutItem PracticeButton() => new StackLayoutItem(
            Theme.Button("Play a practice round", () => _host.Navigate(new ShapeQuizView(_host, practice: true))),
            HorizontalAlignment.Left);
    }
}
