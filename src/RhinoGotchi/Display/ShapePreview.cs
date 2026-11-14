using System.Drawing;
using System.Linq;
using Rhino;
using Rhino.Display;
using Rhino.Geometry;
using RhinoGotchi.Content;

namespace RhinoGotchi.Display
{
    /// <summary>
    /// Draws a "What Made This?" round in a perspective viewport without adding anything to the document.
    /// Inputs are orange, results are blue. The view is restored when the preview stops.
    /// </summary>
    internal static class ShapePreview
    {
        private static readonly PreviewConduit Conduit = new PreviewConduit();
        private static RhinoView? _view;

        public static void Show(RhinoDoc doc, ShapeGeometry shape)
        {
            var box = BoundingBox.Empty;
            foreach (var geo in shape.Inputs.Concat(shape.Results))
                box.Union(geo.GetBoundingBox(true));

            // Put the preview beside the existing model so it doesn't overlap anything.
            var docBox = doc.Objects.BoundingBox;
            var move = Transform.Identity;
            if (docBox.IsValid)
            {
                double gap = 0.25 * System.Math.Max(docBox.Diagonal.Length, box.Diagonal.Length);
                move = Transform.Translation(new Vector3d(
                    docBox.Max.X + gap - box.Min.X,
                    docBox.Center.Y - box.Center.Y,
                    docBox.Min.Z - box.Min.Z));
            }

            Conduit.Inputs = shape.Inputs.Select(g => Moved(g, move)).ToArray();
            Conduit.Results = shape.Results.Select(g => Moved(g, move)).ToArray();
            box.Transform(move);
            Conduit.Box = box;
            Conduit.Enabled = true;

            if (_view == null)
            {
                _view = FindPerspective(doc);
                if (_view == null)
                    return;
                doc.Views.ActiveView = _view;
                _view.ActiveViewport.PushViewProjection();
            }

            var zoom = box;
            zoom.Inflate(box.Diagonal.Length * 0.15);
            _view.ActiveViewport.ZoomBoundingBox(zoom);
            _view.Redraw();
        }

        public static void Stop()
        {
            if (!Conduit.Enabled && _view == null)
                return;

            Conduit.Enabled = false;
            if (_view != null)
            {
                _view.ActiveViewport.PopViewProjection();
                _view.Redraw();
                _view = null;
            }
            RhinoDoc.ActiveDoc?.Views.Redraw();
        }

        private static GeometryBase Moved(GeometryBase g, Transform xf)
        {
            var copy = g.Duplicate();
            copy.Transform(xf);
            return copy;
        }

        private static RhinoView? FindPerspective(RhinoDoc doc)
        {
            var active = doc.Views.ActiveView;
            if (active != null && active.ActiveViewport.IsPerspectiveProjection)
                return active;
            return doc.Views.GetStandardRhinoViews().FirstOrDefault(v => v.ActiveViewport.IsPerspectiveProjection)
                   ?? active;
        }

        private sealed class PreviewConduit : DisplayConduit
        {
            private static readonly Color InputColor = Color.FromArgb(235, 130, 40);
            private static readonly Color ResultColor = Color.FromArgb(60, 120, 200);
            private static readonly DisplayMaterial InputMaterial = new DisplayMaterial(InputColor, 0.75);
            private static readonly DisplayMaterial ResultMaterial = new DisplayMaterial(Color.FromArgb(120, 170, 230), 0.1);

            public GeometryBase[] Inputs = new GeometryBase[0];
            public GeometryBase[] Results = new GeometryBase[0];
            public BoundingBox Box = BoundingBox.Empty;

            protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e)
            {
                if (Box.IsValid)
                    e.IncludeBoundingBox(Box);
            }

            protected override void PostDrawObjects(DrawEventArgs e)
            {
                foreach (var g in Results)
                    Draw(e.Display, g, ResultColor, ResultMaterial, 3);
                foreach (var g in Inputs)
                    Draw(e.Display, g, InputColor, InputMaterial, 4);
            }

            private static void Draw(DisplayPipeline display, GeometryBase g, Color color, DisplayMaterial material, int curveThickness)
            {
                switch (g)
                {
                    case Curve c:
                        display.DrawCurve(c, color, curveThickness);
                        break;
                    case Brep b:
                        display.DrawBrepShaded(b, material);
                        display.DrawBrepWires(b, color, -1);
                        break;
                }
            }
        }
    }
}
