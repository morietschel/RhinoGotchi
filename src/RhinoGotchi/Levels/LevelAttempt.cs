using System;
using System.Collections.Generic;
using Rhino;

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

        /// <summary>Document objects created from <see cref="Level.StartGeometry"/>.</summary>
        public List<Guid> StartObjectIds { get; } = new List<Guid>();

        /// <summary>Objects the player added during the level.</summary>
        public List<Guid> AddedObjectIds { get; } = new List<Guid>();

        /// <summary>English names of the commands the player ran, in order.</summary>
        public List<string> CommandsRun { get; } = new List<string>();
    }
}
