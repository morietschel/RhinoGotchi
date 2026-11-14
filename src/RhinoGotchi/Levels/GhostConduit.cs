using System.Collections.Generic;
using System.Drawing;
using Rhino.Display;
using Rhino.Geometry;

namespace RhinoGotchi.Levels
{
    /// <summary>
    /// Draws a level's ghost geometry as a see-through overlay. Nothing is added
    /// to the document, so ghosts can't be selected, snapped to, edited or saved.
    /// </summary>
    internal sealed class GhostConduit : DisplayConduit
    {
        private static readonly Color GhostColor = Color.FromArgb(120, 170, 255);
        private readonly DisplayMaterial _material = new DisplayMaterial(GhostColor, 0.8); // 80% transparent

        private readonly IReadOnlyList<GeometryBase> _geometry;
        private readonly BoundingBox _bounds;

        public GhostConduit(IReadOnlyList<GeometryBase> geometry)
        {
            _geometry = geometry;
            _bounds = BoundingBox.Empty;
            foreach (GeometryBase g in geometry)
                _bounds.Union(g.GetBoundingBox(false));
        }

        public BoundingBox Bounds => _bounds;

        protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e)
        {
            if (_bounds.IsValid)
                e.IncludeBoundingBox(_bounds);
        }

        protected override void CalculateBoundingBoxZoomExtents(CalculateBoundingBoxEventArgs e)
        {
            if (_bounds.IsValid)
                e.IncludeBoundingBox(_bounds);
        }

        protected override void PostDrawObjects(DrawEventArgs e)
        {
            DisplayPipeline d = e.Display;
            foreach (GeometryBase g in _geometry)
            {
                switch (g)
                {
                    case Brep brep:
                        d.DrawBrepShaded(brep, _material);
                        d.DrawBrepWires(brep, GhostColor, 1);
                        break;
                    case Extrusion extrusion when extrusion.ToBrep() is Brep eb:
                        d.DrawBrepShaded(eb, _material);
                        d.DrawBrepWires(eb, GhostColor, 1);
                        break;
                    case Mesh mesh:
                        d.DrawMeshShaded(mesh, _material);
                        d.DrawMeshWires(mesh, GhostColor);
                        break;
                    case Curve curve:
                        d.DrawCurve(curve, GhostColor, 2);
                        break;
                    case Rhino.Geometry.Point point:
                        d.DrawPoint(point.Location, PointStyle.Circle, 6, GhostColor);
                        break;
                }
            }
        }
    }
}
