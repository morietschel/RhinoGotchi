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
        /// <summary>Leaderboard points for each right answer in a round.</summary>
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

        private readonly GameHud _hud = new GameHud();
        private readonly List<bool> _answers = new List<bool>();
        private readonly StackLayout _prompt = new StackLayout { Spacing = 4, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        private readonly Panel _optionsHost = new Panel();
        private readonly StackLayout _feedback = new StackLayout { Spacing = 8, HorizontalContentAlignment = HorizontalAlignment.Stretch };

        public ShapeQuizView(RhinoGotchiPanel host, bool practice)
        {
            _host = host;
            _practice = practice;
            _doc = RhinoDoc.ActiveDoc;

            Theme.SetUpPage(this, host, practice ? "What Made This? · Practice" : "What Made This?", "");

            var stats = Progress.Get(Games.Shape);
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

            BuildRounds(Daily.Rng(Games.Shape, practice), _doc.ModelAbsoluteTolerance);
            var p = Palette.Current;
            _prompt.Items.Add(Theme.Heading("Which command made the blue shape?"));
            _prompt.Items.Add(new Legend(("what you start with", p.Orange), ("the result", p.Blue)));

            Items.Add(_hud);
            Items.Add(_prompt);
            Items.Add(_optionsHost);
            Items.Add(_feedback);
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

        private void UpdateHud()
        {
            var pips = Enumerable.Range(0, _rounds.Count).Select(i =>
                i < _answers.Count ? (_answers[i] ? Pip.Good : Pip.Bad) : i == _index && _playing ? Pip.Now : Pip.Todo);
            _hud.Set(_playing ? $"ROUND {_index + 1} OF {_rounds.Count}" : "FINISHED", $"{_score} right", pips);
        }

        private void ShowRound()
        {
            var round = _rounds[_index];
            UpdateHud();
            _feedback.Items.Clear();

            var tiles = new List<OptionTile>();
            foreach (string option in round.Options)
            {
                var entry = CommandCatalog.Find(option);
                var tile = new OptionTile(option, entry != null ? CommandIcons.Get(entry, 68) : null);
                tile.Click += (s, e) => Answer(round, option, tiles);
                tiles.Add(tile);
            }
            _optionsHost.Content = Theme.Grid(2, tiles.Cast<Control>().ToArray());

            if (_doc != null)
                ShapePreview.Show(_doc, round.Geometry);
        }

        private void Answer(Round round, string option, List<OptionTile> tiles)
        {
            bool right = option == round.Shape.Command;
            if (right)
                _score++;
            _answers.Add(right);

            foreach (var tile in tiles)
            {
                tile.Locked = true;
                tile.State = tile.CommandName == round.Shape.Command ? TileState.Right
                    : tile.CommandName == option ? TileState.Wrong : TileState.Faded;
            }
            UpdateHud();

            bool last = _index == _rounds.Count - 1;
            _feedback.Items.Clear();
            _feedback.Items.Add(Theme.Card(right ? Theme.Hit : Palette.Current.Red,
                Theme.Heading(right ? $"Yes, {round.Shape.Command}" : $"It was {round.Shape.Command}"),
                Theme.Body(round.Shape.Explanation)));
            _feedback.Items.Add(Theme.Primary(last ? "See my score" : "Next shape →", () =>
            {
                if (last)
                    Finish();
                else
                {
                    _index++;
                    ShowRound();
                }
            }));
            _feedback.Items.Add(Theme.Row(
                Theme.Secondary($"Try {round.Shape.Command}", () => RhinoActions.Run(round.Shape.Command)),
                Theme.Button("Help page", () => RhinoActions.OpenHelp(round.Shape.Command))));
        }

        private void Finish()
        {
            _playing = false;
            ShapePreview.Stop();
            string result = $"{_score}/{_rounds.Count}";
            if (!_practice)
                Progress.Record(Games.Shape, _score >= PassScore, result);
            // Practice rounds count for the leaderboard too: it shows your best round.
            int points = _score * PointsPerAnswer;
            bool newBest = Leaderboard.Report(Games.Shape, points);

            UpdateHud();
            _prompt.Visible = false;
            _optionsHost.Content = null;
            _feedback.Items.Clear();
            _feedback.Items.Add(Theme.Card(_score >= PassScore ? Theme.Hit : Theme.Tile,
                Theme.Heading($"{_score} of {_rounds.Count} right"),
                Theme.Body(_score == _rounds.Count
                    ? "Perfect. You know your surfacing tools."
                    : "Tap Try on a command you missed to build the shape yourself.")));
            if (newBest)
                _feedback.Items.Add(LeaderboardView.NewBestRow(_host, points));
            _feedback.Items.Add(PracticeButton());
        }

        private StackLayoutItem PracticeButton() => new StackLayoutItem(
            Theme.Button("Play a practice round", () => _host.Navigate(new ShapeQuizView(_host, practice: true))),
            HorizontalAlignment.Left);
    }
}
