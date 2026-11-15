using Eto.Drawing;
using Eto.Forms;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// Fin Facade as a page of the panel: six levels that build a box wrapped in rippling fins. The
    /// game draws itself (GotchiFins.PuzzleBoard) and fits whatever height the panel gives it.
    /// </summary>
    internal sealed class FinFacadeView : StackLayout, IGameView
    {
        public FinFacadeView(RhinoGotchiPanel host)
        {
            Theme.SetUpPage(this, host, "Build a facade", "", "← Learn Projects", host.ShowProjects);
            GotchiFins.FinSession.Initialize();
            var board = new GotchiFins.PuzzleBoard { MinimumSize = new Size(200, 560) };
            Items.Add(new StackLayoutItem(board, HorizontalAlignment.Stretch, true));
        }

        public void Enter()
        {
        }

        /// <summary>Leaving Build a facade ends the game: everything it made is deleted.</summary>
        public void Leave()
        {
            if (GotchiFins.FinSession.Started)
                GotchiFins.FinSession.EndGame(Rhino.RhinoDoc.ActiveDoc);
        }
    }
}
