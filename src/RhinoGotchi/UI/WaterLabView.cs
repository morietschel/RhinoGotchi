using System;
using System.Linq;
using Eto.Forms;
using RhinoGotchi.Core;
using RhinoGotchi.Water;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// The Water Run page. With no level open it lists the levels; with one open it shows what to
    /// do, the goal's progress, and (in edit mode) the tools to rebuild and save the level. Buttons that
    /// change the model run the GotchiWater command, so they can be undone like any other command.
    /// </summary>
    internal sealed class WaterLabView : StackLayout, IGameView
    {
        private readonly RhinoGotchiPanel _host;
        private readonly UITimer _timer = new UITimer { Interval = 1 / 24.0 };
        private double _time;
        private GumballHint? _hint;
        private WaterGauge? _gauge;
        private GameHud? _hud;
        private ResultBanner? _banner;
        private Words _rate = Theme.Body("");
        private Words _target = Theme.Body("");
        private Words _size = Theme.Body("");
        private string _shown = "";
        private bool _listening;

        public WaterLabView(RhinoGotchiPanel host)
        {
            _host = host;
            _timer.Elapsed += (s, e) =>
            {
                _time += _timer.Interval;
                _hint?.Tick(_time);
                _gauge?.Tick(_time);
                UpdateHud();
                _banner?.Tick(_time);
            };
            UnLoad += (s, e) => _timer.Stop();
            Rebuild();
        }

        public void Enter()
        {
            if (!_listening)
            {
                _listening = true;
                WaterLab.Changed += Refresh;
                WaterGame.Changed += Refresh;
            }
            _timer.Start();
            Refresh();
        }

        // Leaving the page ends the level being played (see Leave).
        public void Leave()
        {
            _timer.Stop();
            // Leaving ends the level: its objects and the water go (not while editing a level).
            if (WaterGame.Active && !WaterGame.Editing && Rhino.RhinoDoc.ActiveDoc is Rhino.RhinoDoc doc)
                WaterGame.Quit(doc);
            if (_listening)
            {
                _listening = false;
                WaterLab.Changed -= Refresh;
                WaterGame.Changed -= Refresh;
            }
        }

        private static void Run(string option) => RhinoActions.Run(GotchiWaterCommand.Name + " " + option);

        /// <summary>What the page is showing; when this changes the page is laid out again.</summary>
        private static string Signature() =>
            $"{WaterGame.Active}|{WaterGame.Current?.Number}|{WaterGame.Editing}|{WaterGame.Won}|{WaterGame.Failed}|{WaterGame.Current?.Title}";

        private static string Moves() => $"{WaterGame.Moves} {(WaterGame.Moves == 1 ? "move" : "moves")}";

        private void Refresh()
        {
            if (Signature() != _shown)
            {
                // Lay the page out again once the click that caused this has finished.
                Application.Instance.AsyncInvoke(() =>
                {
                    if (Signature() != _shown)
                        Rebuild();
                });
                return;
            }
            if (!WaterGame.Active)
                return;

            string side = WaterGame.Current != null && !WaterGame.Editing
                ? $"{WaterGame.FormatTime(WaterGame.Elapsed)} · {Moves()}"
                : $"{WaterLab.Flowing} flowing";
            _gauge?.Set(WaterLab.InGoal, WaterLab.Target, WaterLab.Reached, side);
            _rate.Text = $"Flow · {WaterLab.Rate}/s";
            _target.Text = $"Goal · {WaterLab.Target}";
            _size.Text = WaterLab.DropSize > 0 ? $"Drop · {WaterLab.DropSize:0.###}" : $"Drop · auto ({WaterLab.Radius:0.###})";
            UpdateHud();
        }

        /// <summary>The level's clock: the time bonus runs down, then the clear is still worth the base points.</summary>
        private void UpdateHud()
        {
            if (_hud == null || !(WaterGame.Current is WaterLevel level))
                return;
            double left = WaterGame.TimeLeft;
            string caption = WaterGame.Won ? "CLEARED" : WaterGame.Failed ? "FAILED" : left > 0 ? "TIME BONUS" : "NO TIME BONUS LEFT";
            int points = WaterGame.Won ? WaterGame.LastPoints : WaterGame.Failed ? 0 : WaterGame.PointsNow;
            var pips = WaterLevelCatalog.All.Select(l =>
                l.Number == level.Number ? (WaterGame.Won ? Pip.Good : WaterGame.Failed ? Pip.Bad : Pip.Now)
                : WaterGame.IsDone(l.Number) ? Pip.Good : Pip.Todo);
            _hud.Set(caption, $"{points} pts", pips);
            _hud.SetTime(left, WaterGame.TimeLimit(level));
        }

        private void Rebuild()
        {
            _shown = Signature();
            _hint = null;
            _gauge = null;
            _hud = null;
            _banner = null;
            Items.Clear();
            if (WaterGame.Active)
                LevelPage();
            else
                ListPage();
            Refresh();
        }

        // ---------------- the list of levels ----------------

        private void ListPage()
        {
            Theme.SetUpPage(this, _host, "Water Run", "");

            var all = WaterLevelCatalog.All;
            var done = new bool[all.Count];
            for (int i = 0; i < all.Count; i++)
                done[i] = WaterGame.IsDone(all[i].Number);
            Items.Add(new LevelDots(done));
            Items.Add(Theme.Row(
                new Pill($"{WaterGame.TotalPoints:N0} points", Theme.P.Gold, Glyph.Trophy),
                Theme.Button(Leaderboard.HasProfile ? "Leaderboard" : "Join the leaderboard", () => _host.Navigate(new LeaderboardView(_host)))));

            foreach (WaterLevel level in all)
            {
                int number = level.Number;
                bool finished = WaterGame.IsDone(number);
                Items.Add(new ListRow($"{number} · {level.Title}", level.Lesson, Glyph.Arrows, number % 6, () => Run("_Level " + number))
                {
                    TagText = finished ? (WaterGame.LevelBest(number) > 0 ? $"{WaterGame.LevelBest(number)} PTS" : "DONE") : WaterGame.HasSavedVersion(number) ? "EDITED" : level.Flat ? "2D" : "3D",
                    TagDone = finished,
                });
            }

            Items.Add(new ListRow("Sandbox", "Build your own", Glyph.Cube, 1, () => Run("_Level 0")));
        }

        // ---------------- a level (or the sandbox) ----------------

        private void LevelPage()
        {
            WaterLevel? level = WaterGame.Current;
            bool editing = WaterGame.Editing;
            Theme.SetUpPage(this, _host, level != null ? $"{level.Number} · {level.Title}" : "Sandbox", "", "← Levels",
                () => Run("_Quit"));

            // What to do: a moving picture of the handle to grab, and one line under it.
            if (level != null && !editing)
            {
                _hint = new GumballHint(GumballHint.PartFor(level.Hint), level.Lesson, !level.Flat);
                var say = new Words(level.Instruction, Theme.HeadingFont, Theme.P.Text) { Centered = true };
                Items.Add(Theme.Box(new StackLayout
                {
                    Spacing = 6,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Items = { _hint, say },
                }));
            }

            if (level != null && !editing)
            {
                _hud = new GameHud(clock: true);
                Items.Add(_hud);
            }

            _gauge = new WaterGauge();
            Items.Add(_gauge);

            bool failed = WaterGame.Failed && level != null;
            if (failed)
            {
                _banner = new ResultBanner(false, "Failed", WaterGame.FailReason);
                Items.Add(_banner);
                Items.Add(Theme.Primary("Try again", () => Run("_Restart")));
            }
            else if (WaterGame.Won && level != null)
            {
                string points = WaterGame.EditedThisRun ? "no points (edited)" : $"+{WaterGame.LastPoints} pts";
                _banner = new ResultBanner(true, "Level complete!", $"{points} · {WaterGame.FormatTime(WaterGame.Elapsed)} · {Moves()}");
                Items.Add(_banner);
                if (WaterGame.LastWasBest)
                    Items.Add(LeaderboardView.NewBestRow(_host, WaterGame.TotalPoints));
                Items.Add(Theme.Primary(level.Number < WaterLevelCatalog.All.Count ? "Next level" : "Back to level 1", () => Run("_Next")));
            }

            if (level != null)
            {
                var mode = Theme.Button(editing ? "Play" : "Edit", () => Run(editing ? "_PlayMode" : "_EditMode"));
                Items.Add(failed ? (Control)mode : Theme.Grid(2, Theme.Secondary("Restart", () => Run("_Restart")), mode));
            }

            if (!editing)
            {
                var p = Theme.P;
                Items.Add(level == null || level.Red
                    ? new Legend(("move", p.Orange), ("locked", p.DisabledText), ("deadly", p.Red))
                    : new Legend(("move", p.Orange), ("locked", p.DisabledText)));
                return;
            }

            Items.Add(new SectionLabel("BUILD"));
            Items.Add(Theme.Grid(3,
                Theme.Button("+ Source", () => Run("_AddSource")),
                Theme.Button("+ Goal", () => Run("_AddGoal")),
                Theme.Button("+ Hazard", () => Run("_AddHazard")),
                Theme.Button("Fixed", () => Run("_Fixed")),
                Theme.Button("Movable", () => Run("_Movable")),
                Theme.Button("Untag", () => Run("_Untag"))));
            Items.Add(Theme.Button("Reset water", () => Run("_ResetWater")));

            Items.Add(new SectionLabel("SETTINGS"));
            _rate = Theme.Body("");
            _target = Theme.Body("");
            _size = Theme.Body("");
            Items.Add(Stepper(_rate, () => WaterLab.Rate = Math.Max(10, WaterLab.Rate - 10), () => WaterLab.Rate = Math.Min(600, WaterLab.Rate + 10)));
            Items.Add(Stepper(_target, () => WaterLab.Target = Math.Max(50, WaterLab.Target - 50), () => WaterLab.Target = Math.Min(5000, WaterLab.Target + 50)));
            Items.Add(Stepper(_size, () => Resize(1 / 1.25), () => Resize(1.25)));

            if (level != null)
                Items.Add(Theme.Grid(2,
                    Theme.Primary("Save", () => Run("_SaveLevel")),
                    Theme.Button("Revert", () => Run("_RevertLevel"))));
        }

        /// <summary>Changes the drop size (and starts the water again, since drops of two sizes can't mix).</summary>
        private static void Resize(double factor)
        {
            double current = WaterLab.DropSize > 0 ? WaterLab.DropSize : WaterLab.Radius;
            WaterLab.DropSize = Math.Round(current * factor, 4);
            WaterLab.Reset();
        }

        private TableLayout Stepper(Words label, Action less, Action more)
        {
            var minus = Theme.Button("−", () => { less(); Refresh(); });
            var plus = Theme.Button("+", () => { more(); Refresh(); });
            minus.Width = plus.Width = 44;
            return new TableLayout
            {
                Spacing = new Eto.Drawing.Size(8, 0),
                Rows = { new TableRow(new TableCell(label, true), new TableCell(minus), new TableCell(plus)) },
            };
        }
    }
}
