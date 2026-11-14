using System.Runtime.InteropServices;
using Eto.Forms;

namespace GotchiHouse
{
    /// <summary>The dockable GotchiHouse panel. All drawing happens in <see cref="GotchiDevice"/>.</summary>
    [Guid("af11547b-111d-4655-8168-7936aca77d04")]
    public class GotchiHousePanel : Panel
    {
        public GotchiHousePanel()
        {
            BackgroundColor = Palette.Paper;
            Content = new GotchiDevice();
        }
    }
}
