using System;
using System.Linq;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;

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
        private bool _addingStartGeometry;

        private LevelRunner(RhinoDoc doc, Level level)
        {
            Attempt = new LevelAttempt(doc, level);
            _ghosts = new GhostConduit(level.GhostGeometry);
        }

        /// <summary>Raised when a level starts, is evaluated, or stops. Always on the UI thread.</summary>
        public static event Action<LevelRunner>? StateChanged;

        /// <summary>The level being played, or null.</summary>
        public static LevelRunner? Current { get; private set; }

        public LevelAttempt Attempt { get; }

        public Level Level => Attempt.Level;

        /// <summary>The latest verdict, updated after each command.</summary>
        public LevelResult? LastResult { get; private set; }

        /// <summary>Stops any running level and starts this one in the given document.</summary>
        public static LevelRunner Start(RhinoDoc doc, Level level)
        {
            Current?.Stop();

            var runner = new LevelRunner(doc, level);
            runner.AddStartGeometry();
            runner._ghosts.Enabled = true;
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
            Unsubscribe();
            _ghosts.Enabled = false;
            if (Current == this)
                Current = null;
            Attempt.Doc.Views.Redraw();
            StateChanged?.Invoke(this);
        }

        /// <summary>Asks the evaluator for a verdict now and reports it on the command line.</summary>
        public LevelResult? Evaluate()
        {
            if (Level.Evaluator == null)
                return null;

            LevelResult result = Level.Evaluator.Evaluate(Attempt);
            LastResult = result;
            Report(result);
            StateChanged?.Invoke(this);

            if (result.Solved)
            {
                RhinoApp.WriteLine($"[RhinoGotchi] Level solved! Score {result.Score:P0}.");
                Stop();
            }
            return result;
        }

        private void AddStartGeometry()
        {
            RhinoDoc doc = Attempt.Doc;
            _addingStartGeometry = true;
            try
            {
                foreach (GeometryBase g in Level.StartGeometry)
                {
                    Guid id = doc.Objects.Add(g);
                    if (id != Guid.Empty)
                        Attempt.StartObjectIds.Add(id);
                }
            }
            finally
            {
                _addingStartGeometry = false;
            }
        }

        private void ZoomToLevel()
        {
            BoundingBox box = _ghosts.Bounds;
            foreach (Guid id in Attempt.StartObjectIds)
            {
                RhinoObject? obj = Attempt.Doc.Objects.FindId(id);
                if (obj != null)
                    box.Union(obj.Geometry.GetBoundingBox(false));
            }
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
            RhinoDoc.DeleteRhinoObject += OnDeleteObject;
            RhinoDoc.CloseDocument += OnCloseDocument;
            Command.EndCommand += OnEndCommand;
        }

        private void Unsubscribe()
        {
            RhinoDoc.AddRhinoObject -= OnAddObject;
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
            Guid id = e.ObjectId;
            if (!Attempt.StartObjectIds.Contains(id) && !Attempt.AddedObjectIds.Contains(id))
                Attempt.AddedObjectIds.Add(id);
        }

        private void OnDeleteObject(object? sender, RhinoObjectEventArgs e)
        {
            if (e.TheObject.Document == Attempt.Doc)
                Attempt.AddedObjectIds.Remove(e.ObjectId);
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
            if (e.CommandResult == Result.Success)
                Evaluate();
        }

        private void Report(LevelResult result)
        {
            string state = result.Solved ? "solved" : "not yet";
            string feedback = string.IsNullOrEmpty(result.Feedback) ? "" : $" {result.Feedback}";
            RhinoApp.WriteLine($"[RhinoGotchi] {state} - score {result.Score:P0}.{feedback}");
        }
    }
}
