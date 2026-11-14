using System.Runtime.InteropServices;
using Eto.Forms;
using RhinoGotchi.Lessons;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// The RhinoGotchi side panel. Every panel instance (Rhino on Mac makes one per
    /// document window) shows the same shared lesson session.
    /// </summary>
    [Guid("c3a9e0d2-5b7f-4f61-9a3e-2d8b6f1c4e70")]
    public sealed class GotchiPanel : Panel
    {
        private static LessonSession? _session;
        private readonly LessonCanvas _canvas;

        public GotchiPanel()
        {
            _canvas = new LessonCanvas(Session);
            Content = new Scrollable
            {
                Content = _canvas,
                ExpandContentWidth = true,
                ExpandContentHeight = true,
                Border = BorderType.None,
            };
        }

        /// <summary>The lesson being played in the panel.</summary>
        public static LessonSession Session => _session ??= new LessonSession(LessonCatalog.SurfacesOne());

        public static System.Guid PanelId => typeof(GotchiPanel).GUID;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _canvas.Unsubscribe();
            base.Dispose(disposing);
        }
    }
}
