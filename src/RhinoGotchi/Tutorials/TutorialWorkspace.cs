using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.UI;

namespace RhinoGotchi.Tutorials
{
    /// <summary>
    /// The layers a tutorial works on, and the guard around the original model.
    /// <para>
    /// Layout: <c>RhinoGotchi Tutorials::NN Command</c> with sublayers <c>Input</c> (start geometry,
    /// orange), <c>Result</c> (what the learner models, blue; current while the tutorial runs) and
    /// <c>Checks</c> (helper geometry from the check step, green). Nothing is ever written to the
    /// model's own layers.
    /// </para>
    /// Open one with <see cref="Open"/>; only one is open at a time. <see cref="Close"/> puts the
    /// current layer back and warns if the original model changed while the tutorial was open.
    /// </summary>
    public sealed class TutorialWorkspace
    {
        public const string InputName = "Input";
        public const string ResultName = "Result";
        public const string ChecksName = "Checks";

        private static readonly Color RootColor = Color.FromArgb(120, 120, 120);
        private static readonly Color TutorialColor = Color.FromArgb(150, 150, 150);
        private static readonly Color InputColor = Color.FromArgb(240, 140, 30);
        private static readonly Color ResultColor = Color.FromArgb(40, 120, 230);
        private static readonly Color ChecksColor = Color.FromArgb(40, 170, 90);

        private readonly ReferenceFingerprint _fingerprint;
        private int _previousLayer = -1;
        private bool _settingUp;
        private (bool On, Rhino.ApplicationSettings.OsnapModes Modes)? _savedSnaps;

        private TutorialWorkspace(RhinoDoc doc, Tutorial tutorial)
        {
            Doc = doc;
            Tutorial = tutorial;
            _fingerprint = ReferenceFingerprint.Take(doc);
        }

        /// <summary>The open workspace, or null.</summary>
        public static TutorialWorkspace? Current { get; private set; }

        public RhinoDoc Doc { get; }

        public Tutorial Tutorial { get; }

        /// <summary>Layer indices, valid after <see cref="EnsureLayers"/> (called by Open).</summary>
        public int TutorialLayer { get; private set; } = -1;
        public int InputLayer { get; private set; } = -1;
        public int ResultLayer { get; private set; } = -1;
        public int ChecksLayer { get; private set; } = -1;

        /// <summary>The objects the last <see cref="SetUp"/> added.</summary>
        public List<Guid> InputIds { get; } = new List<Guid>();

        /// <summary>
        /// Opens the workspace for a tutorial: closes any open one, records the model fingerprint
        /// and makes sure the layers exist. Does not add geometry; call <see cref="SetUp"/> for that.
        /// </summary>
        public static TutorialWorkspace Open(RhinoDoc doc, Tutorial tutorial)
        {
            Current?.Close();
            var workspace = new TutorialWorkspace(doc, tutorial);
            workspace.EnsureLayers();
            workspace.TurnOnSnaps();
            Current = workspace;
            return workspace;
        }

        /// <summary>Creates the tutorial's layers if they are missing, and stores their indices.</summary>
        public void EnsureLayers()
        {
            int root = EnsureLayer(TutorialCatalog.RootLayer, null, RootColor);
            TutorialLayer = EnsureLayer(Tutorial.LayerPath, root, TutorialColor);
            InputLayer = EnsureLayer(Tutorial.LayerPath + "::" + InputName, TutorialLayer, InputColor);
            ResultLayer = EnsureLayer(Tutorial.LayerPath + "::" + ResultName, TutorialLayer, ResultColor);
            ChecksLayer = EnsureLayer(Tutorial.LayerPath + "::" + ChecksName, TutorialLayer, ChecksColor);
        }

        /// <summary>
        /// Adds the tutorial's start geometry to the Input layer in one undoable step, then makes
        /// Result the current layer so the learner's work lands there. Runs
        /// <see cref="Tutorial.SetUp"/> with <paramref name="reference"/> (null = standalone).
        /// Returns the ids of the added objects.
        /// </summary>
        public IReadOnlyList<Guid> SetUp(ReferenceModel? reference)
        {
            EnsureLayers();
            InputIds.Clear();

            uint undo = Doc.BeginUndoRecord("RhinoGotchi tutorial setup");
            _settingUp = true;
            try
            {
                InvariantText.Run(() =>
                {
                    Tutorial.SetUp?.Invoke(new TutorialContext(this, reference));
                    return true;
                });
            }
            finally
            {
                _settingUp = false;
                if (undo != 0)
                    Doc.EndUndoRecord(undo);
            }

            MakeResultCurrent();
            TutorialProgress.MarkStarted(Tutorial.Id);
            Doc.Views.Redraw();
            return InputIds;
        }

        /// <summary>
        /// Adds one object to the Input layer. Only allowed while <see cref="SetUp"/> runs. A locked
        /// object can be snapped to but not picked: used for the guide points under click marks.
        /// </summary>
        internal Guid AddInput(GeometryBase geometry, string? name, bool locked)
        {
            if (!_settingUp)
                throw new InvalidOperationException("Input geometry can only be added during set-up.");

            var attributes = new ObjectAttributes { LayerIndex = InputLayer };
            if (!string.IsNullOrEmpty(name))
                attributes.Name = name;
            if (locked)
                attributes.Mode = ObjectMode.Locked;
            Guid id = Doc.Objects.Add(geometry, attributes);
            if (id != Guid.Empty)
                InputIds.Add(id);
            return id;
        }

        /// <summary>Makes Result the current layer, remembering the layer that was current before.</summary>
        public void MakeResultCurrent()
        {
            if (ResultLayer < 0)
                return;
            int current = Doc.Layers.CurrentLayerIndex;
            if (current != ResultLayer && !IsOwnLayer(current))
                _previousLayer = current;
            Doc.Layers.SetCurrentLayerIndex(ResultLayer, true);
        }

        /// <summary>
        /// Puts back the layer that was current before the tutorial, if the learner is still on one
        /// of this tutorial's layers (if they moved on to another layer themselves, that choice stays).
        /// </summary>
        public void Restore()
        {
            if (_previousLayer < 0)
                return;
            Layer? previous = _previousLayer < Doc.Layers.Count ? Doc.Layers[_previousLayer] : null;
            if (previous != null && !previous.IsDeleted && IsOwnLayer(Doc.Layers.CurrentLayerIndex))
                Doc.Layers.SetCurrentLayerIndex(_previousLayer, true);
            _previousLayer = -1;
        }

        /// <summary>
        /// Deletes every object on this tutorial's layers (Input, Result, Checks), in one undoable step.
        /// Asks first when <paramref name="askFirst"/> is true. Returns how many objects were deleted,
        /// or -1 if the learner said no.
        /// </summary>
        public int Clear(bool askFirst = true)
        {
            List<Guid> ids = ObjectsOnOwnLayers().ToList();
            if (ids.Count == 0)
                return 0;

            if (askFirst)
            {
                var answer = Dialogs.ShowMessage(
                    $"Delete the {ids.Count} objects on the \"{Tutorial.Number:00} {Tutorial.MainCommand}\" tutorial layers? You can undo this.",
                    "RhinoGotchi",
                    ShowMessageButton.YesNo,
                    ShowMessageIcon.Question);
                if (answer != ShowMessageResult.Yes)
                    return -1;
            }

            uint undo = Doc.BeginUndoRecord("RhinoGotchi clear tutorial");
            int deleted;
            try
            {
                // ignoreModes: the locked snap guides go too.
                deleted = 0;
                foreach (Guid id in ids)
                {
                    RhinoObject? obj = Doc.Objects.FindId(id);
                    if (obj != null && Doc.Objects.Delete(obj, true, true))
                        deleted++;
                }
            }
            finally
            {
                if (undo != 0)
                    Doc.EndUndoRecord(undo);
            }
            InputIds.Clear();
            Doc.Views.Redraw();
            return deleted;
        }

        /// <summary>
        /// Ends the tutorial session: restores the current layer and compares the model fingerprint.
        /// Returns what changed in the original model (empty when nothing did) and warns on the command line.
        /// </summary>
        public List<string> Close()
        {
            Restore();
            RestoreSnaps();
            if (Current == this)
                Current = null;

            List<string> changes = CheckReference();
            if (changes.Count > 0)
                RhinoApp.WriteLine("[RhinoGotchi] Warning: the original model changed during the tutorial ({0}). Use Undo if that wasn't intended.",
                    string.Join("; ", changes));
            return changes;
        }

        // Point and End snaps on while a card runs (FinGotchi does the same), put back afterwards.
        private void TurnOnSnaps()
        {
            try
            {
                _savedSnaps = (Rhino.ApplicationSettings.ModelAidSettings.Osnap, Rhino.ApplicationSettings.ModelAidSettings.OsnapModes);
                Rhino.ApplicationSettings.ModelAidSettings.Osnap = true;
                Rhino.ApplicationSettings.ModelAidSettings.OsnapModes |=
                    Rhino.ApplicationSettings.OsnapModes.Point | Rhino.ApplicationSettings.OsnapModes.End;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine("[RhinoGotchi] Couldn't turn on object snaps ({0}).", ex.Message);
            }
        }

        private void RestoreSnaps()
        {
            if (_savedSnaps == null)
                return;
            try
            {
                Rhino.ApplicationSettings.ModelAidSettings.OsnapModes = _savedSnaps.Value.Modes;
                Rhino.ApplicationSettings.ModelAidSettings.Osnap = _savedSnaps.Value.On;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine("[RhinoGotchi] Couldn't restore object snaps ({0}).", ex.Message);
            }
            _savedSnaps = null;
        }

        /// <summary>What changed in the original model since the workspace opened (empty when nothing did).</summary>
        public List<string> CheckReference() =>
            _fingerprint.Differences(ReferenceFingerprint.Take(Doc, _fingerprint.Root), Math.Max(Doc.ModelAbsoluteTolerance, 1e-9));

        /// <summary>True for the tutorial's own layer and its sublayers.</summary>
        public bool IsOwnLayer(int layerIndex)
        {
            if (layerIndex < 0 || layerIndex >= Doc.Layers.Count)
                return false;
            string path = Doc.Layers[layerIndex].FullPath;
            return string.Equals(path, Tutorial.LayerPath, StringComparison.Ordinal) ||
                   path.StartsWith(Tutorial.LayerPath + "::", StringComparison.Ordinal);
        }

        /// <summary>
        /// Clears a tutorial's layers without opening its workspace (the open one, if any, stays open).
        /// Same result as <see cref="Clear"/>: the count deleted, 0 if there was nothing, -1 if the learner said no.
        /// </summary>
        public static int ClearTutorial(RhinoDoc doc, Tutorial tutorial, bool askFirst = true) =>
            new TutorialWorkspace(doc, tutorial).Clear(askFirst);

        /// <summary>Ids of the objects on one of this tutorial's sublayers (Input, Result or Checks).</summary>
        public List<Guid> ObjectsOn(string sublayer)
        {
            string path = Tutorial.LayerPath + "::" + sublayer;
            var ids = new List<Guid>();
            foreach (Guid id in ObjectsOnOwnLayers())
            {
                RhinoObject? obj = Doc.Objects.FindId(id);
                if (obj != null && string.Equals(Doc.Layers[obj.Attributes.LayerIndex].FullPath, path, StringComparison.Ordinal))
                    ids.Add(id);
            }
            return ids;
        }

        /// <summary>Ids of all objects on the tutorial's layers.</summary>
        public IEnumerable<Guid> ObjectsOnOwnLayers()
        {
            var settings = new ObjectEnumeratorSettings
            {
                HiddenObjects = true,
                LockedObjects = true,
                IncludeLights = false,
                IncludeGrips = false,
            };
            foreach (RhinoObject obj in Doc.Objects.GetObjectList(settings))
                if (IsOwnLayer(obj.Attributes.LayerIndex))
                    yield return obj.Id;
        }

        private int EnsureLayer(string fullPath, int? parent, Color color)
        {
            int index = Doc.Layers.FindByFullPath(fullPath, -1);
            if (index >= 0)
                return index;

            int separator = fullPath.LastIndexOf("::", StringComparison.Ordinal);
            string name = separator < 0 ? fullPath : fullPath.Substring(separator + 2);
            var layer = new Layer { Name = name, Color = color };
            if (parent.HasValue)
                layer.ParentLayerId = Doc.Layers[parent.Value].Id;
            return Doc.Layers.Add(layer);
        }
    }
}
