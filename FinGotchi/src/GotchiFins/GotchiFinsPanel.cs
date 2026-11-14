using System.Runtime.InteropServices;
using Eto.Forms;

namespace GotchiFins
{
    /// <summary>The dockable GotchiFins panel. All drawing happens in <see cref="PuzzleBoard"/>.</summary>
    [Guid("1aa2aad2-34d5-4905-9395-248a42d74f1c")]
    public class GotchiFinsPanel : Panel
    {
        public GotchiFinsPanel()
        {
            BackgroundColor = Palette.Paper;
            Content = new PuzzleBoard();
        }
    }
}
