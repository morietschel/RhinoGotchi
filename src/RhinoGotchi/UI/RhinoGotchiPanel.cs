using System;
using System.Runtime.InteropServices;
using Eto.Forms;
using Rhino.UI;
using RhinoGotchi.Display;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// A view that drives something outside the panel (like the viewport preview).
    /// The panel calls Leave on the old view before Enter on the new one.
    /// </summary>
    internal interface IGameView
    {
        void Enter();
        void Leave();
    }

    /// <summary>
    /// The RhinoGotchi side panel. It hosts one view at a time: the home screen, a game, or stats.
    /// </summary>
    [Guid("6d1c3b51-7a0e-4f7b-9c55-2b8e1e9f4a31")]
    public class RhinoGotchiPanel : Panel, IPanel
    {
        public static Guid PanelId => typeof(RhinoGotchiPanel).GUID;

        private Control? _view;

        public RhinoGotchiPanel(uint documentSerialNumber)
        {
            ShowHome();
        }

        internal void ShowHome() => Navigate(new HomeView(this));

        internal void Navigate(Control view)
        {
            (_view as IGameView)?.Leave();
            // Views can be revisited (e.g. back to the Library), so detach from the old scroller first.
            if (view.Parent is Scrollable oldScroller)
                oldScroller.Content = null;
            _view = view;
            Content = new Scrollable
            {
                Content = view,
                Border = BorderType.None,
                ExpandContentWidth = true,
                ExpandContentHeight = false,
            };
            (view as IGameView)?.Enter();
        }

        void IPanel.PanelShown(uint documentSerialNumber, ShowPanelReason reason) => (_view as IGameView)?.Enter();

        void IPanel.PanelHidden(uint documentSerialNumber, ShowPanelReason reason) => ShapePreview.Stop();

        void IPanel.PanelClosing(uint documentSerialNumber, bool onCloseDocument) => ShapePreview.Stop();
    }
}
