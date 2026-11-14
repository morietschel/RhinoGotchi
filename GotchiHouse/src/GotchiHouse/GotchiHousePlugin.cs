using Rhino;
using Rhino.PlugIns;
using Rhino.UI;

namespace GotchiHouse
{
    /// <summary>
    /// The plugin. Rhino creates one instance when it loads the .rhp.
    /// It registers the GotchiHouse panel and starts tracking the house.
    /// </summary>
    public class GotchiHousePlugin : PlugIn
    {
        public GotchiHousePlugin()
        {
            Instance = this;
        }

        public static GotchiHousePlugin? Instance { get; private set; }

        // Load with Rhino so the panel and the GotchiHouse command are always available.
        public override PlugInLoadTime LoadTime => PlugInLoadTime.AtStartup;

        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {
            Panels.RegisterPanel(
                this,
                typeof(GotchiHousePanel),
                "GotchiHouse",
                typeof(GotchiHousePlugin).Assembly,
                "GotchiHouse.Resources.GotchiHouse.ico",
                PanelType.System);

            HouseSession.Initialize();

            RhinoApp.WriteLine("GotchiHouse loaded. Type GotchiHouse to open the panel.");
            return LoadReturnCode.Success;
        }
    }
}
