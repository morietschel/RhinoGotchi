using System.Collections.Generic;
using System.Drawing;
using Rhino.Display;
using Rhino.Geometry;

namespace RhinoGotchi.Tutorials
{
    /// <summary>
    /// Highlights reference geometry from the Klosterneuburg model: a translucent magenta tint and
    /// thick magenta wires drawn over copies of the objects. Magenta because the model already uses
    /// amber (timber) and cyan (glass). Wires are drawn on top of everything, so a highlighted part
    /// stays visible under the roof. The originals aren't selected, changed or redrawn, so the
    /// highlight disappears cleanly when the conduit is turned off.
    /// </summary>
    internal sealed class ReferenceConduit : DisplayConduit
    {
        private static readonly Color HighlightColor = Color.FromArgb(230, 40, 160);
        private readonly DisplayMaterial _material = new DisplayMaterial(HighlightColor, 0.65);

        private readonly List<Brep> _breps = new List<Brep>();
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly List<Curve> _curves = new List<Curve>();
        private readonly List<Point3d> _points = new List<Point3d>();
        private BoundingBox _bounds = BoundingBox.Empty;

        /// <summary>Wire thickness in pixels.</summary>
        public int WireWidth { get; set; } = 3;

        public BoundingBox Bounds => _bounds;

        /// <summary>Replaces what is highlighted. Pass copies (e.g. from <see cref="ReferenceModel"/>).</summary>
        public void SetGeometry(IEnumerable<GeometryBase> geometry)
        {
            _breps.Clear();
            _meshes.Clear();
            _curves.Clear();
            _points.Clear();
            _bounds = BoundingBox.Empty;

            foreach (GeometryBase g in geometry)
            {
                switch (g)
                {
                    case Brep brep:
                        _breps.Add(brep);
                        break;
                    case Extrusion extrusion when extrusion.ToBrep() is Brep eb:
                        _breps.Add(eb);
                        break;
                    case Surface surface when surface.ToBrep() is Brep sb:
                        _breps.Add(sb);
                        break;
                    case Mesh mesh:
                        _meshes.Add(mesh);
                        break;
                    case Curve curve:
                        _curves.Add(curve);
                        break;
                    case Rhino.Geometry.Point point:
                        _points.Add(point.Location);
                        break;
                    default:
                        continue;
                }
                _bounds.Union(g.GetBoundingBox(false));
            }
        }

        protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e)
        {
            if (_bounds.IsValid)
                e.IncludeBoundingBox(_bounds);
        }

        protected override void PostDrawObjects(DrawEventArgs e)
        {
            DisplayPipeline d = e.Display;
            foreach (Brep brep in _breps)
                d.DrawBrepShaded(brep, _material);
            foreach (Mesh mesh in _meshes)
                d.DrawMeshShaded(mesh, _material);
        }

        // Wires in the foreground ignore depth, so parts hidden behind other objects still show.
        protected override void DrawForeground(DrawEventArgs e)
        {
            DisplayPipeline d = e.Display;
            foreach (Brep brep in _breps)
                d.DrawBrepWires(brep, HighlightColor, WireWidth);
            foreach (Mesh mesh in _meshes)
                d.DrawMeshWires(mesh, HighlightColor);
            foreach (Curve curve in _curves)
                d.DrawCurve(curve, HighlightColor, WireWidth);
            foreach (Point3d point in _points)
                d.DrawPoint(point, PointStyle.RoundSimple, 7, HighlightColor);
        }
    }
}
