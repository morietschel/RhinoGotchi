using System.Drawing;
using System.Linq;
using Rhino;
using Rhino.Display;
using Rhino.Geometry;
using RhinoGotchi.Content;

namespace RhinoGotchi.Display
{
    /// <summary>
    /// Draws game rounds and command demos in a perspective viewport without adding anything to the
    /// document. Inputs are orange, results blue, guides dotted and pick points numbered dots.
    /// A preview session starts on the first Show and ends with Stop, which restores the view.
    /// </summary>
    internal static class ShapePreview
    {
        private static readonly PreviewConduit Conduit = new PreviewConduit();
        private static RhinoView? _view;
        private static Transform _move = Transform.Identity;

        public static void Show(RhinoDoc doc, ShapeGeometry frame) => Show(doc, frame, frame.GetBoundingBox());

        /// <summary>Draws a frame and zooms to <paramref name="focus"/> (pass the same box for every step to keep the camera still).</summary>
        public static void Show(RhinoDoc doc, ShapeGeometry frame, BoundingBox focus)
        {
            if (!focus.IsValid)
                focus = frame.GetBoundingBox();
            if (!focus.IsValid)
                return;

            if (_view == null)
            {
                _view = FindPerspective(doc);
                if (_view == null)
                    return;
                _move = PlaceBesideModel(doc, focus);
                doc.Views.ActiveView = _view;
                _view.ActiveViewport.PushViewProjection();
            }

            var shown = frame.Transformed(_move);
            var box = focus;
            box.Transform(_move);
            box.Union(shown.GetBoundingBox());

            Conduit.Frame = shown;
            Conduit.Box = box;
            Conduit.Enabled = true;

            var zoom = box;
            zoom.Inflate(box.Diagonal.Length * 0.15);
            _view.ActiveViewport.ZoomBoundingBox(zoom);
            _view.Redraw();
        }

        /// <summary>A copy of demo geometry placed where the preview draws it, for adding to the model.</summary>
        public static GeometryBase ToPreviewSpace(GeometryBase g) => ShapeGeometry.Moved(g, _move);

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
            _move = Transform.Identity;
            RhinoDoc.ActiveDoc?.Views.Redraw();
        }

        /// <summary>Puts the preview to the right of the existing model so it doesn't overlap anything.</summary>
        private static Transform PlaceBesideModel(RhinoDoc doc, BoundingBox box)
        {
            var docBox = doc.Objects.BoundingBox;
            if (!docBox.IsValid)
                return Transform.Identity;
            double gap = 0.25 * System.Math.Max(docBox.Diagonal.Length, box.Diagonal.Length);
            return Transform.Translation(new Vector3d(
                docBox.Max.X + gap - box.Min.X,
                docBox.Center.Y - box.Center.Y,
                docBox.Min.Z - box.Min.Z));
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
            private static readonly Color GuideColor = Color.FromArgb(90, 90, 90);
            private static readonly Color MarkColor = Color.FromArgb(40, 45, 50);
            private static readonly DisplayMaterial InputMaterial = new DisplayMaterial(InputColor, 0.75);
            private static readonly DisplayMaterial ResultMaterial = new DisplayMaterial(Color.FromArgb(120, 170, 230), 0.1);

            public ShapeGeometry Frame = new ShapeGeometry();
            public BoundingBox Box = BoundingBox.Empty;

            protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e)
            {
                if (Box.IsValid)
                    e.IncludeBoundingBox(Box);
            }

            protected override void PostDrawObjects(DrawEventArgs e)
            {
                foreach (var g in Frame.Results)
                    Draw(e.Display, g, ResultColor, ResultMaterial, 3);
                foreach (var g in Frame.Inputs)
                    Draw(e.Display, g, InputColor, InputMaterial, 4);
                foreach (var line in Frame.Guides)
                    e.Display.DrawDottedLine(line, GuideColor);
            }

            protected override void DrawForeground(DrawEventArgs e)
            {
                foreach (var mark in Frame.Marks)
                    e.Display.DrawDot(mark.Point, mark.Label, MarkColor, Color.White);
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
                    case Mesh m:
                        display.DrawMeshShaded(m, material);
                        break;
                    case Rhino.Geometry.Point p:
                        display.DrawPoint(p.Location, color);
                        break;
                }
            }
        }
    }
}
