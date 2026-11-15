using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rhino;
using Rhino.DocObjects;

namespace RhinoGotchi.Tutorials
{
    /// <summary>
    /// The Klosterneuburg model that ships inside the plugin (a 2 MB light copy, see
    /// Rhino/Models/Klosterneuburg_light.3dm). Build a building adds it to the open document when it
    /// starts and removes exactly what it added when it ends, so the document is left as it was.
    /// If the document already holds the model (e.g. the full original is open), nothing is added
    /// or removed.
    /// </summary>
    internal static class KlosterneuburgModel
    {
        private const string Resource = "RhinoGotchi.Klosterneuburg.3dm";

        private static readonly List<Guid> AddedObjects = new List<Guid>();
        private static readonly List<Guid> AddedLayers = new List<Guid>();
        private static uint _doc;

        /// <summary>True while the model this class added is in a document.</summary>
        public static bool IsAdded => AddedObjects.Count > 0;

        /// <summary>
        /// Makes sure the model is in <paramref name="doc"/>. Returns false (after telling the player why)
        /// when it can't be: the document is in other units and already has work in it.
        /// </summary>
        public static bool Add(RhinoDoc doc)
        {
            if (IsAdded && _doc == doc.RuntimeSerialNumber)
                return true;
            if (ReferenceModel.TryOpen(doc, out _) != null)
                return true; // the model is already open; it isn't ours to remove later

            if (doc.ModelUnitSystem != UnitSystem.Meters)
            {
                if (AllObjects(doc).Any())
                {
                    Rhino.UI.Dialogs.ShowMessage(
                        "Build a building uses a model in metres, and this document is in " +
                        doc.ModelUnitSystem.ToString().ToLowerInvariant() + " with work in it.\n\n" +
                        "Start a new file in metres (File > New, \"Large Objects - Meters\"), then open Build a building again.",
                        "RhinoGotchi");
                    return false;
                }
                doc.AdjustModelUnitSystem(UnitSystem.Meters, false);
            }

            string? path = WriteToTemp();
            if (path == null)
            {
                RhinoApp.WriteLine("[RhinoGotchi] The built-in building model is missing; levels use simple stand-ins.");
                return true;
            }

            var objectsBefore = new HashSet<Guid>(AllObjects(doc).Select(o => o.Id));
            var layersBefore = new HashSet<Guid>(doc.Layers.Where(l => !l.IsDeleted).Select(l => l.Id));
            bool ok;
            try
            {
                ok = doc.Import(path);
            }
            finally
            {
                try { File.Delete(path); } catch { /* temp file, not important */ }
            }
            if (!ok)
            {
                RhinoApp.WriteLine("[RhinoGotchi] Couldn't add the building model; levels use simple stand-ins.");
                return true;
            }

            AddedObjects.Clear();
            AddedLayers.Clear();
            AddedObjects.AddRange(AllObjects(doc).Select(o => o.Id).Where(id => !objectsBefore.Contains(id)));
            AddedLayers.AddRange(doc.Layers.Where(l => !l.IsDeleted && !layersBefore.Contains(l.Id)).Select(l => l.Id));
            _doc = doc.RuntimeSerialNumber;
            doc.Objects.UnselectAll();
            doc.Views.ActiveView?.ActiveViewport.ZoomExtents();
            doc.Views.Redraw();
            RhinoApp.WriteLine("[RhinoGotchi] Added the building model ({0} objects). It is removed when you leave Build a building.", AddedObjects.Count);
            return true;
        }

        /// <summary>Removes the objects and layers <see cref="Add"/> put in the document.</summary>
        public static void Remove(RhinoDoc doc)
        {
            if (!IsAdded)
                return;
            if (_doc == doc.RuntimeSerialNumber)
            {
                foreach (Guid id in AddedObjects)
                    if (doc.Objects.FindId(id) is RhinoObject obj)
                        doc.Objects.Delete(obj, true, true);
                RemoveLayers(doc, AddedLayers);
                doc.Views.Redraw();
                RhinoApp.WriteLine("[RhinoGotchi] Removed the building model.");
            }
            AddedObjects.Clear();
            AddedLayers.Clear();
        }

        /// <summary>Deletes layers (and anything left on them), deepest first, never the current layer.</summary>
        internal static void RemoveLayers(RhinoDoc doc, IEnumerable<Guid> layerIds)
        {
            var layers = layerIds.Select(id => doc.Layers.FindId(id)).Where(l => l != null && !l.IsDeleted).Cast<Layer>()
                .OrderByDescending(l => l.FullPath.Split(new[] { "::" }, StringSplitOptions.None).Length).ToList();
            if (layers.Count == 0)
                return;
            var doomed = new HashSet<Guid>(layers.Select(l => l.Id));
            if (doomed.Contains(doc.Layers.CurrentLayer.Id))
            {
                Layer? keep = doc.Layers.FirstOrDefault(l => !l.IsDeleted && !doomed.Contains(l.Id));
                if (keep != null)
                    doc.Layers.SetCurrentLayerIndex(keep.Index, true);
            }
            foreach (Layer layer in layers)
                doc.Layers.Purge(layer.Index, true);
        }

        private static IEnumerable<RhinoObject> AllObjects(RhinoDoc doc) =>
            doc.Objects.GetObjectList(new ObjectEnumeratorSettings
            {
                NormalObjects = true,
                LockedObjects = true,
                HiddenObjects = true,
                IncludeLights = false,
                IncludeGrips = false,
            });

        private static string? WriteToTemp()
        {
            using Stream? stream = typeof(KlosterneuburgModel).Assembly.GetManifestResourceStream(Resource);
            if (stream == null)
                return null;
            string path = Path.Combine(Path.GetTempPath(), "RhinoGotchi_Klosterneuburg_" + Guid.NewGuid().ToString("N") + ".3dm");
            using (var file = File.Create(path))
                stream.CopyTo(file);
            return path;
        }
    }
}
