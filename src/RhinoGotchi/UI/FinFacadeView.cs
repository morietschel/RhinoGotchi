using Eto.Drawing;
using Eto.Forms;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// Fin Facade as a page of the panel: six levels that build a box wrapped in rippling fins. The
    /// game draws itself (GotchiFins.PuzzleBoard) and fits whatever height the panel gives it.
    /// </summary>
    internal sealed class FinFacadeView : StackLayout
    {
        public FinFacadeView(RhinoGotchiPanel host)
        {
            Theme.SetUpPage(this, host, "Fin Facade", "");
            GotchiFins.FinSession.Initialize();
            var board = new GotchiFins.PuzzleBoard { MinimumSize = new Size(200, 560) };
            Items.Add(new StackLayoutItem(board, HorizontalAlignment.Stretch, true));
        }
    }
}
