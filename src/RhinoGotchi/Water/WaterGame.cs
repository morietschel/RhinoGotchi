using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using Rhino;
using Rhino.ApplicationSettings;
using Rhino.Commands;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;

namespace RhinoGotchi.Water
{
    /// <summary>
    /// Water Run: levels played with nothing but the Gumball. Starting a level puts its scene in the
    /// model, switches the Gumball on, locks the fixed pieces and undoes any other command that edits
    /// the model. Flat levels are played in the Front view and undo any move out of the plane. Touching
    /// a hazard loses the level. Edit mode lifts all of that so a level can be rebuilt and saved; a
    /// saved level is a .3dm (the scene) and a .txt (title, text, numbers) in levels/water.
    /// </summary>
    internal static class WaterGame
    {
        public const string LevelKey = "RhinoGotchi.Level";   // on every object that belongs to the level
        public const string FixedKey = "RhinoGotchi.Fixed";   // on pieces the player may not move
        public const string LookKey = "RhinoGotchi.Look";     // "stone": pale scenery (fountain, basin)

        private static readonly Color SourceColor = Color.FromArgb(40, 140, 255);
        private static readonly Color GoalColor = Color.FromArgb(40, 190, 110);
        private static readonly Color HazardColor = Color.FromArgb(230, 55, 45);
        private static readonly Color MovableColor = Color.FromArgb(240, 140, 40);
        private static readonly Color FixedColor = Color.FromArgb(96, 102, 110);
        private static readonly Color StoneColor = Color.FromArgb(196, 190, 176);

        private static readonly HashSet<Guid> AddedInPlay = new HashSet<Guid>(); // e.g. what a Gumball extrude made
        private static RhinoDoc? _doc;
        private static bool _guarding;
        private static int _edits;          // model edits seen so far
        private static int _editsAtBegin;   // ...when the running command began
        private static bool _loading;
        private static bool _undoPending;
        private static DateTime _started;
        private static TimeSpan _finished;

        /// <summary>Raised when a level starts, is won, lost or quit, or play/edit mode changes.</summary>
        public static event Action? Changed;

        /// <summary>The level in the model, or null. In the sandbox this is null while <see cref="Active"/> is true.</summary>
        public static WaterLevel? Current { get; private set; }

        /// <summary>A level or the sandbox is open.</summary>
        public static bool Active { get; private set; }

        /// <summary>Edit mode: everything is unlocked and every command works.</summary>
        public static bool Editing { get; private set; }

        public static bool Won { get; private set; }

        /// <summary>A hazard was touched. The water is paused until the level is restarted.</summary>
        public static bool Failed { get; private set; }

        public static string FailReason { get; private set; } = "";

        /// <summary>Gumball edits (or any other edit of a piece) since the level started.</summary>
        public static int Moves { get; private set; }

        public static TimeSpan Elapsed => Won || Failed ? _finished : Active ? DateTime.Now - _started : TimeSpan.Zero;

        public static bool IsDone(int number) => (DoneMask & (1 << number)) != 0;

        public static bool HasSavedVersion(int number) => File.Exists(ScenePath(number, false));

        // ---------------- starting and stopping ----------------

        /// <summary>Starts a level (1 to 10), or the empty sandbox (0). Call from inside a command.</summary>
        public static bool Start(RhinoDoc doc, int number)
        {
            WaterLevel? level = null;
            if (number > 0)
            {
                WaterLevel? builtIn = WaterLevelCatalog.All.FirstOrDefault(l => l.Number == number);
                if (builtIn == null)
                {
                    RhinoApp.WriteLine("RhinoGotchi: there is no water level {0}.", number);
                    return false;
                }
                level = builtIn.Copy();
                ReadNotes(level);
            }

            Watch(doc);
            _loading = true;
            try
            {
                RemoveLevelObjects(doc);
                if (level != null && !LoadScene(doc, level.Number))
                {
                    var builder = new LevelBuilder(level.Depth);
                    level.Build(builder);
                    foreach (LevelPiece piece in builder.Pieces)
                        doc.Objects.Add(piece.Geometry, Attributes(doc, piece.Role, !piece.Movable, piece.Name, piece.Look));
                }
            }
            finally
            {
                _loading = false;
            }

            Current = level;
            Active = true;
            Won = Failed = false;
            FailReason = "";
            Moves = 0;
            _started = DateTime.Now;
            if (level != null)
            {
                WaterLab.Rate = level.Rate;
                WaterLab.Target = level.Target;
                WaterLab.DropSize = level.DropSize;
                WaterLab.Depth = level.Depth;
                WaterLab.Flat = level.Flat;
            }
            else
            {
                WaterLab.Depth = 0;
                WaterLab.Flat = false;
            }

            SetMode(doc, editing: level == null);
            ModelAidSettings.AutoGumballEnabled = true; // the Gumball is the whole game
            doc.Objects.UnselectAll();
            FrameScene(doc, level != null && level.Flat);

            WaterLab.Reset();
            WaterLab.Start(doc);
            WaterLab.MarkDirty();
            doc.Views.Redraw();
            if (level != null)
                RhinoApp.WriteLine("RhinoGotchi: level {0}, {1}. {2}", level.Number, level.Title, level.Instruction);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Leaves the game: the level's objects and the water go, the model is yours again.</summary>
        public static void Quit(RhinoDoc doc)
        {
            _loading = true;
            try
            {
                RemoveLevelObjects(doc);
            }
            finally
            {
                _loading = false;
            }
            Unwatch();
            WaterLab.Stop();
            Current = null;
            Active = Editing = Won = Failed = false;
            if (doc.Views.ActiveView is RhinoView view)
                view.Maximized = false;
            doc.Views.Redraw();
            Changed?.Invoke();
        }

        /// <summary>Play mode locks the fixed pieces and allows only the Gumball; edit mode unlocks everything.</summary>
        public static void SetMode(RhinoDoc doc, bool editing)
        {
            Editing = editing;
            if (editing && Failed)
            {
                // Editing a lost level: clear the failure and let the water run again.
                Failed = false;
                WaterLab.Reset();
                WaterLab.Start(doc);
            }
            _loading = true;
            try
            {
                foreach (RhinoObject obj in LevelObjects(doc))
                {
                    bool locked = !editing && obj.Attributes.GetUserString(FixedKey) == "1";
                    if (locked && !obj.IsLocked)
                        doc.Objects.Lock(obj, true);
                    else if (!locked && obj.IsLocked)
                        doc.Objects.Unlock(obj, true);
                }
            }
            finally
            {
                _loading = false;
            }
            WaterLab.MarkDirty();
            doc.Views.Redraw();
            Changed?.Invoke();
        }

        /// <summary>Gives the selected objects a role (solid, source, goal, hazard) and marks them fixed or movable.</summary>
        public static void SetRole(RhinoDoc doc, IEnumerable<RhinoObject> objects, PieceRole? role, bool isFixed)
        {
            foreach (RhinoObject obj in objects)
                doc.Objects.ModifyAttributes(obj, Attributes(doc, role ?? RoleOf(obj.Attributes), isFixed, obj.Attributes.Name, obj.Attributes.GetUserString(LookKey)), true);
            WaterLab.MarkDirty();
            doc.Views.Redraw();
        }

        public static PieceRole RoleOf(ObjectAttributes attributes)
        {
            string? tag = attributes.GetUserString(WaterLab.TagKey);
            return tag == WaterLab.SourceTag ? PieceRole.Source
                : tag == WaterLab.GoalTag ? PieceRole.Goal
                : tag == WaterLab.HazardTag ? PieceRole.Hazard
                : PieceRole.Solid;
        }

        // ---------------- looks ----------------

        /// <summary>The attributes of a level piece: its tags, its colour (orange moves, gray is fixed, red is a hazard) and its name.</summary>
        public static ObjectAttributes Attributes(RhinoDoc doc, PieceRole role, bool isFixed, string? name, string? look = null)
        {
            ObjectAttributes attributes = doc.CreateDefaultAttributes();
            attributes.SetUserString(LevelKey, "1");
            if (isFixed)
                attributes.SetUserString(FixedKey, "1");
            attributes.Name = name ?? "";
            attributes.ColorSource = ObjectColorSource.ColorFromObject;
            switch (role)
            {
                case PieceRole.Source:
                    attributes.SetUserString(WaterLab.TagKey, WaterLab.SourceTag);
                    attributes.ObjectColor = SourceColor;
                    break;
                case PieceRole.Goal:
                    attributes.SetUserString(WaterLab.TagKey, WaterLab.GoalTag);
                    attributes.ObjectColor = GoalColor;
                    // Goals are see-through, so you can watch them fill up.
                    if (DisplayModeDescription.FindByName("Ghosted") is DisplayModeDescription ghosted)
                        attributes.SetDisplayModeOverride(ghosted);
                    break;
                case PieceRole.Hazard:
                    attributes.SetUserString(WaterLab.TagKey, WaterLab.HazardTag);
                    attributes.ObjectColor = HazardColor;
                    break;
                default:
                    bool stone = look == "stone" && isFixed;
                    if (stone)
                        attributes.SetUserString(LookKey, "stone");
                    attributes.ObjectColor = stone ? StoneColor : isFixed ? FixedColor : MovableColor;
                    break;
            }
            return attributes;
        }

        /// <summary>
        /// Flat levels fill the Front view, where the Gumball only offers left/right, up/down and one turn.
        /// 3D levels use the perspective view, from the front and a little above and to the side.
        /// </summary>
        private static void FrameScene(RhinoDoc doc, bool flat)
        {
            BoundingBox box = BoundingBox.Empty;
            foreach (RhinoObject obj in LevelObjects(doc))
                box.Union(obj.Geometry.GetBoundingBox(true));
            if (!box.IsValid)
                return;
            box.Inflate(box.Diagonal.Length * 0.08);

            RhinoView[] views = doc.Views.GetStandardRhinoViews();
            RhinoView? view = flat
                ? views.FirstOrDefault(v => v.ActiveViewport.IsParallelProjection && v.ActiveViewport.CameraDirection.IsParallelTo(Vector3d.YAxis, 0.01) == 1)
                : views.FirstOrDefault(v => v.ActiveViewport.IsPerspectiveProjection);
            view ??= doc.Views.ActiveView;
            if (view == null)
                return;

            if (doc.Views.ActiveView is RhinoView previous && previous != view)
                previous.Maximized = false;
            doc.Views.ActiveView = view;
            view.Maximized = true;
            if (!flat && view.ActiveViewport.IsPerspectiveProjection)
            {
                Point3d target = box.Center;
                view.ActiveViewport.SetCameraLocations(target, target + new Vector3d(-0.22, -1, 0.22) * box.Diagonal.Length * 2);
            }
            view.ActiveViewport.ZoomBoundingBox(box);
        }

        // ---------------- the model ----------------

        private static IEnumerable<RhinoObject> LevelObjects(RhinoDoc doc)
        {
            var settings = new ObjectEnumeratorSettings
            {
                NormalObjects = true,
                LockedObjects = true,
                HiddenObjects = true,
                DeletedObjects = false,
                IncludeLights = false,
                IncludeGrips = false,
            };
            return doc.Objects.GetObjectList(settings)
                .Where(o => !string.IsNullOrEmpty(o.Attributes.GetUserString(LevelKey)))
                .ToList();
        }

        private static void RemoveLevelObjects(RhinoDoc doc)
        {
            foreach (RhinoObject obj in LevelObjects(doc))
                doc.Objects.Delete(obj, true, true);
            foreach (Guid id in AddedInPlay)
                if (doc.Objects.FindId(id) is RhinoObject extra)
                    doc.Objects.Delete(extra, true, true);
            AddedInPlay.Clear();
        }

        // ---------------- the rules ----------------

        private static void Watch(RhinoDoc doc)
        {
            _doc = doc;
            if (_guarding)
                return;
            _guarding = true;
            RhinoDoc.AddRhinoObject += OnAdd;
            RhinoDoc.DeleteRhinoObject += OnDelete;
            RhinoDoc.ReplaceRhinoObject += OnReplace;
            RhinoDoc.CloseDocument += OnCloseDocument;
            Command.BeginCommand += OnBeginCommand;
            Command.EndCommand += OnEndCommand;
            WaterLab.GoalReached += OnGoalReached;
            WaterLab.HazardTouched += OnHazardTouched;
        }

        private static void Unwatch()
        {
            if (!_guarding)
                return;
            _guarding = false;
            RhinoDoc.AddRhinoObject -= OnAdd;
            RhinoDoc.DeleteRhinoObject -= OnDelete;
            RhinoDoc.ReplaceRhinoObject -= OnReplace;
            RhinoDoc.CloseDocument -= OnCloseDocument;
            Command.BeginCommand -= OnBeginCommand;
            Command.EndCommand -= OnEndCommand;
            WaterLab.GoalReached -= OnGoalReached;
            WaterLab.HazardTouched -= OnHazardTouched;
            _doc = null;
        }

        private static bool Playing => Active && !Editing;

        private static void OnAdd(object? sender, RhinoObjectEventArgs e)
        {
            if (_loading)
                return;
            _edits++;
            if (Current != null)
                AddedInPlay.Add(e.ObjectId);
        }

        private static void OnDelete(object? sender, RhinoObjectEventArgs e)
        {
            if (!_loading)
                _edits++;
        }

        private static void OnReplace(object? sender, RhinoReplaceObjectEventArgs e)
        {
            if (_loading)
                return;
            _edits++;
            if (!Playing || Won || Failed)
                return;
            Moves++;

            // A flat level stays flat: a piece that leaves the plane (moved, turned or scaled in depth) goes back.
            if (Current != null && Current.Flat && e.OldRhinoObject?.Geometry != null && e.NewRhinoObject?.Geometry != null)
            {
                BoundingBox before = e.OldRhinoObject.Geometry.GetBoundingBox(true);
                BoundingBox after = e.NewRhinoObject.Geometry.GetBoundingBox(true);
                double tolerance = Math.Max(1e-6, before.Diagonal.Length * 1e-4);
                if (Math.Abs(before.Min.Y - after.Min.Y) > tolerance || Math.Abs(before.Max.Y - after.Max.Y) > tolerance)
                    UndoSoon("this level is flat, so pieces only go left, right, up and down. That move was undone.");
            }
        }

        private static void OnCloseDocument(object? sender, DocumentEventArgs e)
        {
            if (e.Document != _doc)
                return;
            Unwatch();
            Current = null;
            Active = Editing = Won = Failed = false;
            Changed?.Invoke();
        }

        private static void OnBeginCommand(object? sender, CommandEventArgs e) => _editsAtBegin = _edits;

        /// <summary>
        /// While playing, a command that changed the model is taken back, unless it is the Gumball (or a
        /// plain drag, which Rhino reports the same way), Undo/Redo or the game's own command.
        /// </summary>
        private static void OnEndCommand(object? sender, CommandEventArgs e)
        {
            if (!Playing || _edits == _editsAtBegin)
                return;
            string name = e.CommandEnglishName ?? "";
            if (!Allowed(name))
                UndoSoon($"only the Gumball works in a water level, so {name} was undone. Choose Edit level to build freely.");
        }

        private static bool Allowed(string command) =>
            command.Length == 0
            || command.IndexOf("Gumball", StringComparison.OrdinalIgnoreCase) >= 0
            || command.IndexOf("Drag", StringComparison.OrdinalIgnoreCase) >= 0
            || command.Equals("Undo", StringComparison.OrdinalIgnoreCase)
            || command.Equals("Redo", StringComparison.OrdinalIgnoreCase)
            || command.Equals(GotchiWaterCommand.Name, StringComparison.OrdinalIgnoreCase);

        /// <summary>Takes the last edit back once Rhino is idle again (it can't be undone from inside its own event).</summary>
        private static void UndoSoon(string why)
        {
            if (_undoPending)
                return;
            _undoPending = true;
            RhinoDoc? doc = _doc;
            EventHandler? undo = null;
            undo = (s, a) =>
            {
                RhinoApp.Idle -= undo;
                _undoPending = false;
                if (doc == null || !Playing)
                    return;
                _loading = true;
                try
                {
                    doc.Undo();
                }
                finally
                {
                    _loading = false;
                }
                WaterLab.MarkDirty();
                RhinoApp.WriteLine("RhinoGotchi: " + why);
            };
            RhinoApp.Idle += undo;
        }

        private static void OnGoalReached()
        {
            if (Current == null || Editing || Won || Failed)
                return;
            Won = true;
            _finished = DateTime.Now - _started;
            DoneMask |= 1 << Current.Number;
            RhinoApp.WriteLine($"RhinoGotchi: level {Current.Number} complete in {FormatTime(_finished)} with {Moves} {(Moves == 1 ? "move" : "moves")}!");
            Changed?.Invoke();
        }

        /// <summary>Water or a piece touched a hazard: the level is lost on the spot.</summary>
        private static void OnHazardTouched(string what, Point3d where)
        {
            if (!Playing || Current == null || Won || Failed)
                return;
            Failed = true;
            FailReason = what;
            _finished = DateTime.Now - _started;
            WaterLab.Pause();
            WaterLab.ShowAlert("Failed! " + what + ".", where);
            RhinoApp.WriteLine("RhinoGotchi: level failed. {0}. Restart to try again.", what);
            Changed?.Invoke();
        }

        public static string FormatTime(TimeSpan time) => $"{(int)time.TotalMinutes}:{time.Seconds:00}";

        private static int DoneMask
        {
            get => RhinoGotchiPlugin.Instance?.Settings.GetInteger("WaterLevelsDone", 0) ?? 0;
            set => RhinoGotchiPlugin.Instance?.Settings.SetInteger("WaterLevelsDone", value);
        }

        // ---------------- saved levels ----------------

        /// <summary>
        /// Where saved levels live: levels/water in the repository when the plugin runs from a build inside
        /// it (so edits can be committed), otherwise the WaterLevels folder next to the plugin.
        /// </summary>
        public static string LevelFolder(bool forWriting)
        {
            string here = Path.GetDirectoryName(typeof(WaterGame).Assembly.Location) ?? "";
            string? folder = here;
            for (int i = 0; i < 8 && !string.IsNullOrEmpty(folder); i++)
            {
                if (File.Exists(Path.Combine(folder, "RhinoGotchi.sln")))
                {
                    string repo = Path.Combine(folder, "levels", "water");
                    if (forWriting || Directory.Exists(repo))
                        return repo;
                }
                folder = Path.GetDirectoryName(folder);
            }
            return Path.Combine(here, "WaterLevels");
        }

        private static string ScenePath(int number, bool forWriting) =>
            Path.Combine(LevelFolder(forWriting), $"level-{number:00}.3dm");

        private static string NotesPath(int number, bool forWriting) =>
            Path.Combine(LevelFolder(forWriting), $"level-{number:00}.txt");

        /// <summary>Saves the scene as it is now, and the level's text and numbers, over the built-in version.</summary>
        public static string? Save(RhinoDoc doc)
        {
            WaterLevel? level = Current;
            if (level == null)
            {
                RhinoApp.WriteLine("RhinoGotchi: start a level first; saving replaces that level's scene.");
                return null;
            }

            level.Rate = WaterLab.Rate;
            level.Target = WaterLab.Target;
            if (WaterLab.DropSize > 0)
                level.DropSize = WaterLab.DropSize;

            Directory.CreateDirectory(LevelFolder(true));
            string path = ScenePath(level.Number, true);
            using (var file = new File3dm())
            {
                var settings = new ObjectEnumeratorSettings
                {
                    NormalObjects = true,
                    LockedObjects = true,
                    HiddenObjects = false,
                    DeletedObjects = false,
                    IncludeLights = false,
                    IncludeGrips = false,
                };
                foreach (RhinoObject obj in doc.Objects.GetObjectList(settings).ToList())
                {
                    if (!obj.Visible)
                        continue;
                    // Whatever is in the scene is part of the level from now on.
                    if (string.IsNullOrEmpty(obj.Attributes.GetUserString(LevelKey)))
                    {
                        ObjectAttributes tagged = obj.Attributes.Duplicate();
                        tagged.SetUserString(LevelKey, "1");
                        doc.Objects.ModifyAttributes(obj, tagged, true);
                    }
                    ObjectAttributes attributes = obj.Attributes.Duplicate();
                    attributes.SetUserString(LevelKey, "1");
                    attributes.LayerIndex = 0;
                    attributes.RemoveDisplayModeOverride();
                    file.Objects.Add(obj.Geometry, attributes);
                }
                if (!file.Write(path, 7))
                {
                    RhinoApp.WriteLine("RhinoGotchi: could not write {0}.", path);
                    return null;
                }
            }

            File.WriteAllLines(NotesPath(level.Number, true), new[]
            {
                "title=" + level.Title,
                "lesson=" + level.Lesson,
                "instruction=" + level.Instruction,
                "target=" + level.Target.ToString(CultureInfo.InvariantCulture),
                "rate=" + level.Rate.ToString(CultureInfo.InvariantCulture),
                "dropsize=" + level.DropSize.ToString(CultureInfo.InvariantCulture),
                "depth=" + level.Depth.ToString(CultureInfo.InvariantCulture),
                "flat=" + (level.Flat ? "true" : "false"),
            });
            RhinoApp.WriteLine("RhinoGotchi: level {0} saved to {1} (edit the .txt next to it to change the title and text).", level.Number, path);
            Changed?.Invoke();
            return path;
        }

        /// <summary>Throws the saved version away, so the level is the built-in one again.</summary>
        public static void Revert(int number)
        {
            foreach (string path in new[] { ScenePath(number, false), NotesPath(number, false) })
                if (File.Exists(path))
                    File.Delete(path);
        }

        private static bool LoadScene(RhinoDoc doc, int number)
        {
            string path = ScenePath(number, false);
            if (!File.Exists(path))
                return false;
            try
            {
                using (File3dm? file = File3dm.Read(path))
                {
                    if (file == null)
                        return false;
                    int added = 0;
                    foreach (File3dmObject item in file.Objects)
                    {
                        ObjectAttributes from = item.Attributes;
                        bool isFixed = from.GetUserString(FixedKey) == "1";
                        if (doc.Objects.Add(item.Geometry, Attributes(doc, RoleOf(from), isFixed, from.Name, from.GetUserString(LookKey))) != Guid.Empty)
                            added++;
                    }
                    return added > 0;
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine("RhinoGotchi: could not read {0} ({1}); using the built-in level.", path, ex.Message);
                return false;
            }
        }

        /// <summary>Reads level-NN.txt (key=value lines) over the built-in title, text and numbers.</summary>
        private static void ReadNotes(WaterLevel level)
        {
            string path = NotesPath(level.Number, false);
            if (!File.Exists(path))
                return;
            try
            {
                foreach (string line in File.ReadAllLines(path))
                {
                    int split = line.IndexOf('=');
                    if (split <= 0)
                        continue;
                    string key = line.Substring(0, split).Trim().ToLowerInvariant();
                    string value = line.Substring(split + 1).Trim();
                    switch (key)
                    {
                        case "title": level.Title = value; break;
                        case "lesson": level.Lesson = value; break;
                        case "instruction": level.Instruction = value; break;
                        case "target": if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int target)) level.Target = target; break;
                        case "rate": if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int rate)) level.Rate = rate; break;
                        case "dropsize": if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double size)) level.DropSize = size; break;
                        case "depth": if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double depth)) level.Depth = depth; break;
                        case "flat": level.Flat = value.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                    }
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine("RhinoGotchi: could not read {0} ({1}).", path, ex.Message);
            }
        }
    }
}
