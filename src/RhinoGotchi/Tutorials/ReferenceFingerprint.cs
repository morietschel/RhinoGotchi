using System;
using System.Collections.Generic;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace RhinoGotchi.Tutorials
{
    /// <summary>
    /// A cheap summary of the original model, taken when a tutorial starts and compared when it
    /// ends: how many objects sit on model layers, where they are (sum of bounding boxes), and the
    /// model layers' count, visibility and locking. If any of that changes, something touched the
    /// original model and the learner is warned.
    /// </summary>
    public sealed class ReferenceFingerprint
    {
        private ReferenceFingerprint(string root, int objects, Vector3d boxSum, int layers, int hiddenLayers, int lockedLayers)
        {
            Root = root;
            Objects = objects;
            BoxSum = boxSum;
            Layers = layers;
            HiddenLayers = hiddenLayers;
            LockedLayers = lockedLayers;
        }

        /// <summary>The layer subtree that was summarised, normally <see cref="ReferenceLayers.ModelRoot"/>.</summary>
        public string Root { get; }

        public int Objects { get; }

        /// <summary>Sum of every object's bounding-box min and max corners.</summary>
        public Vector3d BoxSum { get; }

        public int Layers { get; }

        public int HiddenLayers { get; }

        public int LockedLayers { get; }

        public static ReferenceFingerprint Take(RhinoDoc doc, string root = ReferenceLayers.ModelRoot)
        {
            var inRoot = new HashSet<int>();
            int hidden = 0, locked = 0;
            foreach (Layer layer in doc.Layers)
            {
                if (layer.IsDeleted || !InRoot(layer.FullPath, root))
                    continue;
                inRoot.Add(layer.Index);
                if (!layer.IsVisible)
                    hidden++;
                if (layer.IsLocked)
                    locked++;
            }

            var settings = new ObjectEnumeratorSettings
            {
                HiddenObjects = true,
                LockedObjects = true,
                ReferenceObjects = true,
                NormalObjects = true,
                IncludeLights = false,
                IncludeGrips = false,
            };
            int count = 0;
            var sum = Vector3d.Zero;
            foreach (RhinoObject obj in doc.Objects.GetObjectList(settings))
            {
                if (!inRoot.Contains(obj.Attributes.LayerIndex) || obj.Geometry == null)
                    continue;
                count++;
                // The fast (non-accurate) box is enough: any move or edit changes it.
                BoundingBox box = obj.Geometry.GetBoundingBox(false);
                if (box.IsValid)
                    sum += new Vector3d(box.Min) + new Vector3d(box.Max);
            }
            return new ReferenceFingerprint(root, count, sum, inRoot.Count, hidden, locked);
        }

        /// <summary>
        /// What differs from <paramref name="later"/>, in words, or an empty list when nothing did.
        /// Positions are compared within <paramref name="tolerance"/> per coordinate sum.
        /// </summary>
        public List<string> Differences(ReferenceFingerprint later, double tolerance)
        {
            var changes = new List<string>();
            if (later.Objects != Objects)
                changes.Add($"object count changed from {Objects} to {later.Objects}");
            if ((later.BoxSum - BoxSum).Length > tolerance)
                changes.Add("objects were moved or edited");
            if (later.Layers != Layers)
                changes.Add($"layer count changed from {Layers} to {later.Layers}");
            if (later.HiddenLayers != HiddenLayers)
                changes.Add($"hidden layers changed from {HiddenLayers} to {later.HiddenLayers}");
            if (later.LockedLayers != LockedLayers)
                changes.Add($"locked layers changed from {LockedLayers} to {later.LockedLayers}");
            return changes;
        }

        public override string ToString() =>
            $"{Root}: {Objects} objects on {Layers} layers ({HiddenLayers} hidden, {LockedLayers} locked)";

        private static bool InRoot(string fullPath, string root) =>
            string.Equals(fullPath, root, StringComparison.Ordinal) ||
            fullPath.StartsWith(root + "::", StringComparison.Ordinal);
    }
}
