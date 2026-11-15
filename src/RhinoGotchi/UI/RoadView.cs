using Eto.Forms;
using RhinoGotchi.Tutorials.RhinoOnTheStreet;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// Build a road as a page of the panel. Opening it starts the game in the viewport; the page has Skip intro,
    /// Restart and Quit. Going back (the page's back button, or any other page) ends the game, which clears
    /// the street and arch and puts the view back.
    /// </summary>
    internal sealed class RoadView : StackLayout, IGameView
    {
        private readonly UITimer _timer = new UITimer { Interval = 0.5 };
        private readonly Words _status = Theme.Body("");
        private readonly ChunkyButton _skip;

        public RoadView(RhinoGotchiPanel host)
        {
            Theme.SetUpPage(this, host, "Build a road", "", "← Learn Projects", host.ShowProjects);
            Items.Add(Theme.Muted("Draw the street with Curve, then build the archway with Line and Arc. Everything is cleared when you leave."));
            Items.Add(_status);
            _skip = Theme.Button("Skip intro", () => StreetTutorial.Current?.SkipIntro());
            Items.Add(Theme.Grid(2,
                _skip,
                Theme.Button("Restart", () => { if (Rhino.RhinoDoc.ActiveDoc is Rhino.RhinoDoc doc) StreetTutorial.Start(doc); })));
            Items.Add(Theme.Wide(Theme.Primary("Quit and clear", host.ShowProjects)));
            _timer.Elapsed += (s, e) => Refresh();
        }

        public void Enter()
        {
            if (StreetTutorial.Current == null && Rhino.RhinoDoc.ActiveDoc is Rhino.RhinoDoc doc)
                StreetTutorial.Start(doc);
            _timer.Start();
            Refresh();
        }

        /// <summary>Leaving the page ends the game: the street and arch are deleted and the view comes back.</summary>
        public void Leave()
        {
            _timer.Stop();
            StreetTutorial.Current?.Stop();
        }

        private void Refresh()
        {
            var t = StreetTutorial.Current;
            _skip.Enabled = t != null && t.Phase == Phase.Intro;
            _status.Text = t == null ? "The game has ended."
                : t.Phase == Phase.Intro ? "Watch the intro, or skip it."
                : t.Phase == Phase.End ? "Finished! The street clears in a few seconds."
                : t.Lesson != null ? $"Level {t.Lesson.Number} of 2: {t.Lesson.Title}"
                : "Playing in the Perspective viewport.";
        }
    }
}
