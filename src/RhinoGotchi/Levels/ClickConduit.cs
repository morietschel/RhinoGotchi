using System.Collections.Generic;
using System.Drawing;
using Rhino.Display;
using Rhino.Geometry;

namespace RhinoGotchi.Levels
{
    /// <summary>
    /// Draws a level's click marks: yellow numbered dots with a black label beside them, on top of
    /// everything so they're never hidden. Display only, nothing is added to the document.
    /// </summary>
    internal sealed class ClickConduit : DisplayConduit
    {
        private static readonly Color Dot = Color.FromArgb(255, 205, 0);
        private static readonly Color Ink = Color.FromArgb(25, 25, 25);
        private readonly IReadOnlyList<ClickMark> _marks;
        private readonly BoundingBox _bounds;

        public ClickConduit(IReadOnlyList<ClickMark> marks)
        {
            _marks = marks;
            _bounds = BoundingBox.Empty;
            foreach (ClickMark m in marks)
                _bounds.Union(m.At);
        }

        public BoundingBox Bounds => _bounds;

        protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e)
        {
            if (_bounds.IsValid)
                e.IncludeBoundingBox(_bounds);
        }

        protected override void DrawForeground(DrawEventArgs e)
        {
            DisplayPipeline d = e.Display;
            for (int i = 0; i < _marks.Count; i++)
            {
                ClickMark m = _marks[i];
                Point2d screen = e.Viewport.WorldToClient(m.At);
                var centre = new Point2d(screen.X, screen.Y);
                d.DrawPoint(m.At, PointStyle.RoundSimple, 11, Ink); // dark ring
                d.DrawPoint(m.At, PointStyle.RoundSimple, 8, Dot);
                string text = string.IsNullOrEmpty(m.Label) ? $"{i + 1}" : $"{i + 1} {m.Label}";
                var at = new Point2d(centre.X + 12, centre.Y - 20);
                d.Draw2dText(text, Color.White, new Point2d(at.X + 1, at.Y + 1), false, 14, "Arial"); // halo
                d.Draw2dText(text, Ink, at, false, 14, "Arial");
            }
        }
    }
}
