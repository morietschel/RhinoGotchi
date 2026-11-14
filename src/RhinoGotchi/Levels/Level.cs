using System.Collections.Generic;
using Rhino.Geometry;

namespace RhinoGotchi.Levels
{
    /// <summary>
    /// One level of the game: teaches one Rhino command.
    /// Deliberately minimal; extend it as the game takes shape.
    /// </summary>
    public sealed class Level
    {
        /// <summary>Stable id, e.g. "point-01".</summary>
        public string Id { get; set; } = "";

        /// <summary>What the player sees, e.g. "Place a point on the ghost".</summary>
        public string Title { get; set; } = "";

        /// <summary>The Rhino command this level teaches, by English name, e.g. "Point".</summary>
        public string Command { get; set; } = "";

        /// <summary>Added to the document when the level starts. The player can select and edit it.</summary>
        public List<GeometryBase> StartGeometry { get; } = new List<GeometryBase>();

        /// <summary>
        /// Shown during the level as a display-only overlay. Never added to the
        /// document, so it can't be selected, snapped to, edited or saved.
        /// </summary>
        public List<GeometryBase> GhostGeometry { get; } = new List<GeometryBase>();

        /// <summary>Decides whether the level was solved and how well.</summary>
        public ILevelEvaluator? Evaluator { get; set; }
    }
}
