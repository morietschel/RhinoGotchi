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

        /// <summary>
        /// Full path of the layer that receives <see cref="StartGeometry"/>, e.g.
        /// "RhinoGotchi Tutorials::01 Loft::Input". Created if missing. Null = the current layer.
        /// </summary>
        public string? Layer { get; set; }

        /// <summary>
        /// Existing document objects to highlight while the level runs (e.g. the Klosterneuburg roof).
        /// Only copies are drawn; the objects themselves are never selected or changed.
        /// </summary>
        public List<System.Guid> ReferenceIds { get; } = new List<System.Guid>();

        /// <summary>The tutorial this level belongs to, or null for a plain level.</summary>
        public string? TutorialId { get; set; }

        /// <summary>
        /// True (the default): the level ends as soon as a command leaves it solved. False: it's
        /// checked quietly after each command and only ends when the player asks for a check
        /// (tutorials, whose last steps explore options after the result already passes).
        /// </summary>
        public bool StopWhenSolved { get; set; } = true;

        /// <summary>One hint per step, given out in order by GotchiLevel > Hint.</summary>
        public List<string> Hints { get; } = new List<string>();

        /// <summary>
        /// Points the player should click, drawn as yellow numbered dots with a label while the level
        /// runs (e.g. "1 centre"). Display only; put a locked point in the document under each one
        /// if the player needs to snap to it.
        /// </summary>
        public List<ClickMark> ClickMarks { get; } = new List<ClickMark>();
    }

    /// <summary>A point to click, with a short label.</summary>
    public sealed class ClickMark
    {
        public ClickMark(string label, Point3d at)
        {
            Label = label;
            At = at;
        }

        public string Label { get; }

        public Point3d At { get; }
    }
}
