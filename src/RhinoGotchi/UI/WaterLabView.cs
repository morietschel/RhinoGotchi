using System;
using Eto.Forms;
using RhinoGotchi.Core;
using RhinoGotchi.Water;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// The Water Run page. With no level open it lists the ten levels; with one open it shows what to
    /// do, the goal's progress, and (in edit mode) the tools to rebuild and save the level. Buttons that
    /// change the model run the GotchiWater command, so they can be undone like any other command.
    /// </summary>
    internal sealed class WaterLabView : StackLayout, IGameView
    {
        private readonly RhinoGotchiPanel _host;
        private Words _score = Theme.Heading("");
        private Words _detail = Theme.Muted("");
        private Words _rate = Theme.Body("");
        private Words _target = Theme.Body("");
        private Words _size = Theme.Body("");
        private FillBar _bar = new FillBar();
        private string _shown = "";
        private bool _listening;

        public WaterLabView(RhinoGotchiPanel host)
        {
            _host = host;
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
            Refresh();
        }

        // The water keeps running when you leave the page; only the page stops updating.
        public void Leave()
        {
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

            _score.Text = WaterLab.Reached ? "Goal reached!" : $"{WaterLab.InGoal} / {WaterLab.Target} drops in the goal";
            _bar.Set(WaterLab.Progress, WaterLab.Reached);
            _detail.Text = WaterGame.Current != null && !WaterGame.Editing
                ? $"{WaterGame.FormatTime(WaterGame.Elapsed)} · {WaterGame.Moves} {(WaterGame.Moves == 1 ? "move" : "moves")} · {WaterLab.Spilled} spilled"
                : $"{WaterLab.SourceCount} {(WaterLab.SourceCount == 1 ? "source" : "sources")} · {WaterLab.GoalCount} {(WaterLab.GoalCount == 1 ? "goal" : "goals")} · {WaterLab.Flowing} flowing";
            _rate.Text = $"Flow: {WaterLab.Rate} drops a second";
            _target.Text = $"Goal amount: {WaterLab.Target} drops";
            _size.Text = WaterLab.DropSize > 0 ? $"Drop size: {WaterLab.DropSize:0.###}" : $"Drop size: automatic ({WaterLab.Radius:0.###})";
        }

        private void Rebuild()
        {
            _shown = Signature();
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
            Theme.SetUpPage(this, _host, "Water Run",
                "Get the water into the goal using nothing but the Gumball. Each level is about one part of it.");

            foreach (WaterLevel level in WaterLevelCatalog.All)
            {
                int number = level.Number;
                bool done = WaterGame.IsDone(number);
                Items.Add(new ListRow($"{number} · {level.Title}", (level.Flat ? "2D · " : "3D · ") + level.Lesson, Glyph.Arrows, number % 6, () => Run("_Level " + number))
                {
                    TagText = done ? "DONE" : WaterGame.HasSavedVersion(number) ? "EDITED" : "PLAY",
                    TagDone = done,
                });
            }

            Items.Add(new SectionLabel("BUILD", "start from nothing"));
            Items.Add(new ListRow("Empty sandbox", "Sources, goals and your own geometry", Glyph.Cube, 1, () => Run("_Level 0")));
            Items.Add(Theme.Small("Playing adds the level's pieces to the open model and removes them again when you leave. Use an empty file."));
        }

        // ---------------- a level (or the sandbox) ----------------

        private void LevelPage()
        {
            WaterLevel? level = WaterGame.Current;
            bool editing = WaterGame.Editing;
            string title = level != null ? $"{level.Number} · {level.Title}" : "Sandbox";
            Theme.SetUpPage(this, _host, title, level != null ? level.Lesson.ToUpperInvariant() : "BUILD ANYTHING", "← Levels",
                () => Run("_Quit"));

            if (level != null)
                Items.Add(Theme.Body(level.Instruction));

            _score = Theme.Heading("");
            _detail = Theme.Muted("");
            _bar = new FillBar();
            Items.Add(Theme.Card(Theme.Tile, _score, _bar, _detail));

            if (WaterGame.Failed && level != null)
            {
                Items.Add(Theme.Card(Theme.P.Red,
                    Theme.Heading("Failed!"),
                    Theme.Body(WaterGame.FailReason + ". Red is deadly: neither the water nor your pieces may touch it.")));
                Items.Add(Theme.Primary("Try again", () => Run("_Restart")));
            }
            else if (WaterGame.Won && level != null)
            {
                Items.Add(Theme.Card(Theme.Hit,
                    Theme.Heading("Level complete!"),
                    Theme.Body($"{WaterGame.FormatTime(WaterGame.Elapsed)} · {WaterGame.Moves} {(WaterGame.Moves == 1 ? "move" : "moves")}")));
                Items.Add(Theme.Primary(level.Number < WaterLevelCatalog.All.Count ? "Next level" : "Back to level 1", () => Run("_Next")));
            }

            if (level != null)
                Items.Add(Theme.Grid(2,
                    Theme.Secondary("Restart", () => Run("_Restart")),
                    Theme.Button(editing ? "Play it" : "Edit level", () => Run(editing ? "_PlayMode" : "_EditMode"))));

            if (!editing)
            {
                Items.Add(Theme.Small("Orange pieces move, gray and stone ones are locked, red ones are deadly. Only the Gumball works here: any other command that changes the model is undone."
                    + (level != null && level.Flat ? " This level is flat: pieces only go left, right, up and down." : "")));
                return;
            }

            Items.Add(Theme.Heading("Build"));
            Items.Add(Theme.Small("Edit mode: everything is unlocked and every Rhino command works. Anything visible in the model becomes part of the level when you save."));
            Items.Add(Theme.Grid(2,
                Theme.Button("Add source", () => Run("_AddSource")),
                Theme.Button("Add goal", () => Run("_AddGoal")),
                Theme.Button("Add hazard", () => Run("_AddHazard")),
                Theme.Button("Untag", () => Run("_Untag")),
                Theme.Button("Make fixed", () => Run("_Fixed")),
                Theme.Button("Make movable", () => Run("_Movable"))));
            Items.Add(Theme.Button("Reset water", () => Run("_ResetWater")));

            Items.Add(Theme.Heading("Settings"));
            _rate = Theme.Body("");
            _target = Theme.Body("");
            _size = Theme.Body("");
            Items.Add(Stepper(_rate, () => WaterLab.Rate = Math.Max(10, WaterLab.Rate - 10), () => WaterLab.Rate = Math.Min(300, WaterLab.Rate + 10)));
            Items.Add(Stepper(_target, () => WaterLab.Target = Math.Max(50, WaterLab.Target - 50), () => WaterLab.Target = Math.Min(5000, WaterLab.Target + 50)));
            Items.Add(Stepper(_size, () => Resize(1 / 1.25), () => Resize(1.25)));

            if (level != null)
            {
                Items.Add(Theme.Grid(2,
                    Theme.Primary("Save level", () => Run("_SaveLevel")),
                    Theme.Button("Revert to built-in", () => Run("_RevertLevel"))));
                Items.Add(Theme.Small($"Saved to {WaterGame.LevelFolder(true)} as level-{level.Number:00}.3dm and .txt. Edit the .txt to change the title, the instruction and whether the level is flat."));
            }
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

    /// <summary>The goal's progress bar: green while filling, gold when the goal amount is reached.</summary>
    internal sealed class FillBar : Drawable
    {
        private double _value;
        private bool _done;

        public FillBar()
        {
            Height = 16;
            Paint += (s, e) =>
            {
                var p = Palette.Current;
                var g = e.Graphics;
                var all = new Eto.Drawing.RectangleF(0, 2, Width, Height - 4);
                g.FillRectangle(p.Border, all);
                g.FillRectangle(p.TileFace, new Eto.Drawing.RectangleF(all.X + 2, all.Y + 2, all.Width - 4, all.Height - 4));
                float filled = (float)((all.Width - 4) * _value);
                if (filled > 0)
                    g.FillRectangle(_done ? p.Gold : p.Green, new Eto.Drawing.RectangleF(all.X + 2, all.Y + 2, filled, all.Height - 4));
            };
        }

        public void Set(double value, bool done)
        {
            _value = Math.Max(0, Math.Min(1, value));
            _done = done;
            Invalidate();
        }
    }
}
