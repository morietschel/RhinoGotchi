using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>A top-level page that shows the bottom navigation bar, with its tab highlighted.</summary>
    internal interface INavPage
    {
        NavTab Tab { get; }
    }

    /// <summary>
    /// The RhinoGotchi side panel. It hosts one view at a time (home, a game, the Learning Campaign,
    /// trophies or stats) and a bottom navigation bar on the top-level pages.
    /// </summary>
    [Guid("6d1c3b51-7a0e-4f7b-9c55-2b8e1e9f4a31")]
    public class RhinoGotchiPanel : Panel, IPanel
    {
        private static readonly List<WeakReference<RhinoGotchiPanel>> Instances = new List<WeakReference<RhinoGotchiPanel>>();
        private static bool _openLessonsNext;

        public static Guid PanelId => typeof(RhinoGotchiPanel).GUID;

        private readonly Scrollable _scroller;
        private readonly NavBar _nav;
        private Control? _view;

        public RhinoGotchiPanel(uint documentSerialNumber)
        {
            _ = GotchiState.Game; // loads progress and the hat Gotchi wears

            _scroller = new Scrollable { Border = BorderType.None, ExpandContentWidth = true };
            _nav = new NavBar();
            _nav.Selected += tab =>
            {
                switch (tab)
                {
                    case NavTab.Trophies: ShowTrophies(); break;
                    case NavTab.Stats: Navigate(new StatsView(this)); break;
                    default: ShowHome(); break;
                }
            };
            Content = new TableLayout
            {
                Spacing = new Eto.Drawing.Size(0, 0),
                Rows = { new TableRow(_scroller) { ScaleHeight = true }, new TableRow(_nav) },
            };

            lock (Instances)
            {
                Instances.RemoveAll(w => !w.TryGetTarget(out _));
                Instances.Add(new WeakReference<RhinoGotchiPanel>(this));
            }

            if (_openLessonsNext)
            {
                _openLessonsNext = false;
                ShowLessons();
            }
            else
                ShowHome();
        }

        /// <summary>Shows the Learning Campaign in every open panel, and in the next one Rhino creates.</summary>
        internal static void OpenLessonsEverywhere()
        {
            bool any = false;
            lock (Instances)
            {
                foreach (var w in Instances.ToList())
                    if (w.TryGetTarget(out var panel))
                    {
                        panel.ShowLessons();
                        any = true;
                    }
            }
            _openLessonsNext = !any;
        }

        internal void ShowHome() => Navigate(new HomeView(this));

        internal void ShowLessons() => Navigate(new LessonView(this));

        internal void ShowTrophies() => Navigate(new AchievementsView(this));

        internal void Navigate(Control view)
        {
            (_view as IGameView)?.Leave();
            // Views can be revisited (e.g. back to the Library), so detach from the old scroller first.
            if (view.Parent is Scrollable oldScroller)
                oldScroller.Content = null;
            _view = view;
            var background = Palette.Current.Background;
            BackgroundColor = background;
            view.BackgroundColor = background;
            _scroller.BackgroundColor = background;
            _scroller.ExpandContentHeight = view is IFillView;
            _scroller.Content = view;
            _scroller.ScrollPosition = new Eto.Drawing.Point(0, 0);

            _nav.Visible = view is INavPage;
            if (view is INavPage page)
                _nav.Current = page.Tab;
            (view as IGameView)?.Enter();
        }

        void IPanel.PanelShown(uint documentSerialNumber, ShowPanelReason reason) => (_view as IGameView)?.Enter();

        void IPanel.PanelHidden(uint documentSerialNumber, ShowPanelReason reason)
        {
            ShapePreview.Stop();
            if (_view is LessonView)
                (_view as IGameView)?.Leave();
        }

        void IPanel.PanelClosing(uint documentSerialNumber, bool onCloseDocument)
        {
            ShapePreview.Stop();
            (_view as IGameView)?.Leave();
        }
    }
}
