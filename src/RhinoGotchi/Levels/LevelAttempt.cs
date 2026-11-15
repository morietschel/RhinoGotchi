using System;
using System.Collections.Generic;
using Rhino;
using Rhino.DocObjects;

namespace RhinoGotchi.Levels
{
    /// <summary>Everything an evaluator can look at when grading a level.</summary>
    public sealed class LevelAttempt
    {
        public LevelAttempt(RhinoDoc doc, Level level)
        {
            Doc = doc;
            Level = level;
        }

        public RhinoDoc Doc { get; }

        public Level Level { get; }

        /// <summary>Document objects created from <see cref="Level.StartGeometry"/>, or added by a tutorial's set-up.</summary>
        public List<Guid> StartObjectIds { get; } = new List<Guid>();

        /// <summary>Objects the player added during the level. Objects on the original model's layers never count.</summary>
        public List<Guid> AddedObjectIds { get; } = new List<Guid>();

        /// <summary>English names of the commands the player ran, in order.</summary>
        public List<string> CommandsRun { get; } = new List<string>();

        /// <summary>
        /// Whether each added object carried a History record (History was on when it was made,
        /// e.g. a Loft that updates when its curves are edited). Refreshed after every command,
        /// so an object that History replaced keeps its entry.
        /// </summary>
        public Dictionary<Guid, bool> HasHistory { get; } = new Dictionary<Guid, bool>();

        /// <summary>The added objects that still exist, skipping deleted ones.</summary>
        public IEnumerable<RhinoObject> AddedObjects()
        {
            foreach (Guid id in AddedObjectIds)
            {
                RhinoObject? obj = Doc.Objects.FindId(id);
                if (obj != null && !obj.IsDeleted)
                    yield return obj;
            }
        }

        /// <summary>Re-reads <see cref="HasHistory"/> for every added object.</summary>
        public void RefreshHistory()
        {
            foreach (RhinoObject obj in AddedObjects())
                HasHistory[obj.Id] = obj.HasHistoryRecord();
        }
    }
}
