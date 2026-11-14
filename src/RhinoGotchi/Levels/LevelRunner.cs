using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoGotchi.Tutorials;

namespace RhinoGotchi.Levels
{
    /// <summary>
    /// Plays one level at a time: adds its start geometry, shows its ghosts,
    /// records what the player adds and which commands they run, and asks the
    /// level's evaluator for a verdict after every command.
    /// </summary>
    public sealed class LevelRunner
    {
        private readonly GhostConduit _ghosts;
        private readonly ReferenceConduit _references = new ReferenceConduit();
        private readonly ClickConduit _clicks;
        private int _changesThisCommand;
        private bool _addingStartGeometry;
        private int _nextHint;
        private bool _announcedSolved;

        private LevelRunner(RhinoDoc doc, Level level)
        {
            Attempt = new LevelAttempt(doc, level);
            _ghosts = new GhostConduit(level.GhostGeometry);
            _clicks = new ClickConduit(level.ClickMarks);
        }

        /// <summary>
        /// Commands that changed the model but didn't solve the level. Undo, Redo and Delete don't count,
        /// and nothing counts once the level is solved.
        /// </summary>
        public int Misses { get; private set; }

        /// <summary>Raised once when the level stops (solved, quit, restarted or document closed).</summary>
        public event EventHandler? Stopped;

        /// <summary>Raised when a level starts, is evaluated (after every command too), or stops. Always on the UI thread.</summary>
        public static event Action<LevelRunner>? StateChanged;

        /// <summary>The level being played, or null.</summary>
        public static LevelRunner? Current { get; private set; }

        public LevelAttempt Attempt { get; }

        public Level Level => Attempt.Level;

        /// <summary>The latest verdict, updated after each command.</summary>
        public LevelResult? LastResult { get; private set; }

        /// <summary>Stops any running level and starts this one in the given document.</summary>
        public static LevelRunner Start(RhinoDoc doc, Level level) => Start(doc, level, null);

        /// <summary>
        /// Stops any running level and starts this one. <paramref name="existingStartIds"/> are objects
        /// already in the document that count as start geometry (e.g. a tutorial's set-up): Restart
        /// deletes them with the player's work, and they are never counted as the player's own.
        /// </summary>
        public static LevelRunner Start(RhinoDoc doc, Level level, IEnumerable<Guid>? existingStartIds)
        {
            Current?.Stop();

            var runner = new LevelRunner(doc, level);
            if (existingStartIds != null)
                runner.Attempt.StartObjectIds.AddRange(existingStartIds);
            runner.AddStartGeometry();
            runner.ShowReferences();
            runner._ghosts.Enabled = true;
            runner._clicks.Enabled = level.ClickMarks.Count > 0;
            runner.Subscribe();
            Current = runner;

            runner.ZoomToLevel();
            RhinoApp.WriteLine($"[RhinoGotchi] Level \"{level.Title}\" started. Command to use: {level.Command}.");
            StateChanged?.Invoke(runner);
            return runner;
        }

        /// <summary>Ends the level: removes the ghosts and stops listening. Geometry stays in the document.</summary>
        public void Stop()
        {
            bool wasRunning = Current == this;
            Unsubscribe();
            _ghosts.Enabled = false;
            _references.Enabled = false;
            _clicks.Enabled = false;
            if (Current == this)
                Current = null;
            Attempt.Doc.Views.Redraw();
            if (wasRunning)
            {
                Stopped?.Invoke(this, EventArgs.Empty);
                StateChanged?.Invoke(this);
            }
        }

        /// <summary>The next hint in order, or null when the level has none left.</summary>
        public string? NextHint()
        {
            if (_nextHint >= Level.Hints.Count)
                return null;
            return Level.Hints[_nextHint++];
        }

        /// <summary>Raised after every check the player asked for (Check button or GotchiLevel > Check), before a solved level stops.</summary>
        public event EventHandler<LevelResult>? Checked;

        /// <summary>
        /// The player asked for a check: asks the evaluator for a verdict, reports it on the command
        /// line and, if solved, ends the level.
        /// </summary>
        public LevelResult? Evaluate()
        {
            if (Level.Evaluator == null)
                return null;

            LevelResult result = Level.Evaluator.Evaluate(Attempt);
            LastResult = result;
            Report(result);
            Checked?.Invoke(this, result);
            StateChanged?.Invoke(this);

            if (result.Solved)
            {
                RhinoApp.WriteLine($"[RhinoGotchi] Level solved! Score {result.Score:P0}.");
                Stop();
            }
            return result;
        }

        // After each command. Levels that stop when solved get a full check; the others (tutorials,
        // whose later steps explore options after the result already passes) are checked quietly and
        // only say once that the result passes, so the player finishes with Check when ready.
        private void EvaluateAfterCommand()
        {
            if (Level.Evaluator == null)
                return;
            if (Level.StopWhenSolved)
            {
                Evaluate();
                return;
            }

            LevelResult result;
            try
            {
                result = Level.Evaluator.Evaluate(Attempt);
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine("[RhinoGotchi] Couldn't check the result ({0}).", ex.Message);
                return;
            }
            LastResult = result;
            StateChanged?.Invoke(this);
            if (result.Solved && !_announcedSolved)
            {
                _announcedSolved = true;
                RhinoApp.WriteLine("[RhinoGotchi] Every check passes! Press CONTINUE in the RhinoGotchi panel, or keep going for the bonus star.");
            }
            else if (!result.Solved)
            {
                _announcedSolved = false;
            }
        }

        private void AddStartGeometry()
        {
            RhinoDoc doc = Attempt.Doc;
            if (Level.StartGeometry.Count == 0)
                return;

            // Default attributes put objects on the current layer, as before levels had a Layer.
            ObjectAttributes attributes = doc.CreateDefaultAttributes();
            if (!string.IsNullOrEmpty(Level.Layer))
            {
                int layer = FindOrAddLayer(doc, Level.Layer!);
                if (layer >= 0)
                    attributes.LayerIndex = layer;
            }

            _addingStartGeometry = true;
            try
            {
                foreach (GeometryBase g in Level.StartGeometry)
                {
                    Guid id = doc.Objects.Add(g, attributes);
                    if (id != Guid.Empty)
                        Attempt.StartObjectIds.Add(id);
                }
            }
            finally
            {
                _addingStartGeometry = false;
            }
        }

        // Creates each missing layer along a "::" path. Returns the layer index, or -1.
        private static int FindOrAddLayer(RhinoDoc doc, string fullPath)
        {
            int index = doc.Layers.FindByFullPath(fullPath, -1);
            if (index >= 0)
                return index;

            Guid parent = Guid.Empty;
            string path = "";
            foreach (string name in fullPath.Split(new[] { "::" }, StringSplitOptions.None))
            {
                path = path.Length == 0 ? name : path + "::" + name;
                index = doc.Layers.FindByFullPath(path, -1);
                if (index < 0)
                    index = doc.Layers.Add(new Layer { Name = name, ParentLayerId = parent });
                if (index < 0)
                    return -1;
                parent = doc.Layers[index].Id;
            }
            return index;
        }

        private void ShowReferences()
        {
            if (Level.ReferenceIds.Count == 0)
                return;
            var copies = new List<GeometryBase>();
            foreach (Guid id in Level.ReferenceIds)
            {
                GeometryBase? g = Attempt.Doc.Objects.FindId(id)?.Geometry;
                if (g != null)
                    copies.Add(g.Duplicate());
            }
            _references.SetGeometry(copies);
            _references.Enabled = copies.Count > 0;
        }

        private void ZoomToLevel()
        {
            // Frame what the player works on (ghosts and start geometry). The highlighted reference
            // is often much bigger (the whole roof), so it only decides the view when there's nothing else.
            BoundingBox box = _ghosts.Bounds;
            if (_clicks.Bounds.IsValid)
                box.Union(_clicks.Bounds);
            foreach (Guid id in Attempt.StartObjectIds)
            {
                RhinoObject? obj = Attempt.Doc.Objects.FindId(id);
                if (obj != null)
                    box.Union(obj.Geometry.GetBoundingBox(false));
            }
            if (!box.IsValid && _references.Enabled)
                box = _references.Bounds;
            if (box.IsValid)
            {
                box.Inflate(Math.Max(box.Diagonal.Length * 0.25, 1));
                Attempt.Doc.Views.ActiveView?.ActiveViewport.ZoomBoundingBox(box);
            }
            Attempt.Doc.Views.Redraw();
        }

        private void Subscribe()
        {
            RhinoDoc.AddRhinoObject += OnAddObject;
            RhinoDoc.UndeleteRhinoObject += OnAddObject; // Undo brings deleted objects back this way
            RhinoDoc.DeleteRhinoObject += OnDeleteObject;
            RhinoDoc.CloseDocument += OnCloseDocument;
            Command.EndCommand += OnEndCommand;
        }

        private void Unsubscribe()
        {
            RhinoDoc.AddRhinoObject -= OnAddObject;
            RhinoDoc.UndeleteRhinoObject -= OnAddObject;
            RhinoDoc.DeleteRhinoObject -= OnDeleteObject;
            RhinoDoc.CloseDocument -= OnCloseDocument;
            Command.EndCommand -= OnEndCommand;
        }

        // Moving or editing an object replaces it: Rhino deletes and re-adds it with the
        // same id, so the add/remove pair below leaves the list unchanged.
        private void OnAddObject(object? sender, RhinoObjectEventArgs e)
        {
            if (_addingStartGeometry || e.TheObject.Document != Attempt.Doc)
                return;
            // Work on the original model's layers never counts as the player's own.
            int layer = e.TheObject.Attributes.LayerIndex;
            if (layer >= 0 && layer < Attempt.Doc.Layers.Count &&
                ReferenceLayers.IsModelLayer(Attempt.Doc.Layers[layer].FullPath))
                return;

            Guid id = e.ObjectId;
            if (!Attempt.StartObjectIds.Contains(id) && !Attempt.AddedObjectIds.Contains(id))
                Attempt.AddedObjectIds.Add(id);
            _changesThisCommand++;
        }

        private void OnDeleteObject(object? sender, RhinoObjectEventArgs e)
        {
            if (e.TheObject.Document == Attempt.Doc)
            {
                Attempt.AddedObjectIds.Remove(e.ObjectId);
                _changesThisCommand++;
            }
        }

        private void OnCloseDocument(object? sender, DocumentEventArgs e)
        {
            if (e.Document == Attempt.Doc)
                Stop();
        }

        private void OnEndCommand(object? sender, CommandEventArgs e)
        {
            if (e.Document != Attempt.Doc || e.CommandEnglishName == GotchiLevelCommand.Name)
                return;

            Attempt.CommandsRun.Add(e.CommandEnglishName);
            Attempt.RefreshHistory();
            bool changed = _changesThisCommand > 0;
            _changesThisCommand = 0;
            bool wasSolved = LastResult?.Solved == true;
            if (e.CommandResult == Result.Success)
                EvaluateAfterCommand();

            // A miss: the model changed but the level still isn't solved (FinGotchi's rule).
            string name = e.CommandEnglishName;
            bool neutral = name.Equals("Undo", StringComparison.OrdinalIgnoreCase) ||
                           name.Equals("Redo", StringComparison.OrdinalIgnoreCase) ||
                           name.Equals("Delete", StringComparison.OrdinalIgnoreCase);
            if (changed && !neutral && !wasSolved && Current == this && LastResult?.Solved != true &&
                e.CommandResult == Result.Success)
            {
                Misses++;
                StateChanged?.Invoke(this);
            }
        }

        private void Report(LevelResult result)
        {
            string state = result.Solved ? "solved" : "not yet";
            string feedback = string.IsNullOrEmpty(result.Feedback) ? "" : $" {result.Feedback}";
            RhinoApp.WriteLine($"[RhinoGotchi] {state} - score {result.Score:P0}.{feedback}");
        }
    }
}
