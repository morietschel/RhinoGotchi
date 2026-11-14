using System;
using System.Drawing;
using System.Linq;
using Rhino;
using Rhino.Display;
using Rhino.Geometry;
using Rhino.UI;
using RhinoGotchi.Content;

namespace RhinoGotchi.Display
{
    /// <summary>
    /// Shows a Deep Cut model alone in a perspective viewport: the document's own objects are hidden
    /// there while the model is up, nothing is added to the document, and Stop restores the view.
    /// </summary>
    internal static class ModelPreview
    {
        private static readonly ModelConduit Conduit = new ModelConduit();
        private static RhinoView? _view;

        public static bool Active => _view != null;

        /// <summary>Draws the model. <paramref name="frame"/> also points the camera at it (false keeps the player's view).</summary>
        public static void Show(RhinoDoc doc, DeepCutModel model, bool frame)
        {
            if (_view == null)
            {
                var active = doc.Views.ActiveView;
                _view = active != null && active.ActiveViewport.IsPerspectiveProjection
                    ? active
                    : doc.Views.GetStandardRhinoViews().FirstOrDefault(v => v.ActiveViewport.IsPerspectiveProjection) ?? active;
                if (_view == null)
                    return;
                doc.Views.ActiveView = _view;
                _view.ActiveViewport.PushViewProjection();
            }

            Conduit.Model = model;
            Conduit.ViewportId = _view.ActiveViewportID;
            Conduit.Enabled = true;

            if (frame)
            {
                var box = model.Focus;
                var viewport = _view.ActiveViewport;
                var target = box.Center;
                viewport.SetCameraLocations(target, target + new Vector3d(-0.62, -0.72, 0.42) * Math.Max(box.Diagonal.Length, 1e-6) * 2);
                box.Inflate(box.Diagonal.Length * 0.05);
                viewport.ZoomBoundingBox(box);
            }
            _view.Redraw();
        }

        /// <summary>A JPEG of what the player is looking at right now, for the judge. Null if it can't be captured.</summary>
        public static byte[]? Capture(int width)
        {
            if (_view == null)
                return null;
            try
            {
                var size = _view.ActiveViewport.Size;
                int height = size.Width > 0 ? Math.Max(1, width * size.Height / size.Width) : width * 3 / 4;
                using (var bitmap = _view.CaptureToBitmap(new Size(width, height)))
                {
                    if (bitmap == null)
                        return null;
                    using (var eto = bitmap.ToEto())
                        return eto.ToByteArray(Eto.Drawing.ImageFormat.Jpeg);
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine("RhinoGotchi: could not capture the viewport ({0}).", ex.Message);
                return null;
            }
        }

        public static void Stop()
        {
            if (!Conduit.Enabled && _view == null)
                return;

            Conduit.Enabled = false;
            Conduit.Model = null;
            if (_view != null)
            {
                _view.ActiveViewport.PopViewProjection();
                _view.Redraw();
                _view = null;
            }
            RhinoDoc.ActiveDoc?.Views.Redraw();
        }

        private sealed class ModelConduit : DisplayConduit
        {
            private static readonly Color CurveColor = Color.FromArgb(40, 45, 50);
            private static readonly DisplayMaterial Material = new DisplayMaterial(Color.FromArgb(225, 228, 232));

            public DeepCutModel? Model;
            public Guid ViewportId;

            protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e)
            {
                if (Model?.Mesh != null)
                    e.IncludeBoundingBox(Model.Mesh.GetBoundingBox(false));
                if (Model != null && Model.Focus.IsValid)
                    e.IncludeBoundingBox(Model.Focus);
            }

            /// <summary>Hides the player's own model in the game viewport so only the puzzle shows.</summary>
            protected override void ObjectCulling(CullObjectEventArgs e)
            {
                if (e.Viewport.Id == ViewportId)
                    e.CullObject = true;
            }

            protected override void PostDrawObjects(DrawEventArgs e)
            {
                if (Model == null || e.Viewport.Id != ViewportId)
                    return;
                if (Model.Mesh != null)
                    e.Display.DrawMeshShaded(Model.Mesh, Material);
                if (Model.Lines.Length > 0)
                    e.Display.DrawLines(Model.Lines, CurveColor);
            }
        }
    }
}
