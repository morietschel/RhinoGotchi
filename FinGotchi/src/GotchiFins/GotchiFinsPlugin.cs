using Rhino;
using Rhino.PlugIns;
using Rhino.UI;

namespace GotchiFins
{
    /// <summary>
    /// The plugin. Rhino creates one instance when it loads the .rhp.
    /// It registers the GotchiFins panel and starts tracking the facade.
    /// </summary>
    public class GotchiFinsPlugin : PlugIn
    {
        public GotchiFinsPlugin()
        {
            Instance = this;
        }

        public static GotchiFinsPlugin? Instance { get; private set; }

        // Load with Rhino so the panel and the GotchiFins command are always available.
        public override PlugInLoadTime LoadTime => PlugInLoadTime.AtStartup;

        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {
            Panels.RegisterPanel(
                this,
                typeof(GotchiFinsPanel),
                "GotchiFins",
                typeof(GotchiFinsPlugin).Assembly,
                "GotchiFins.Resources.GotchiFins.ico",
                PanelType.System);

            FinSession.Initialize();

            RhinoApp.WriteLine("GotchiFins loaded. Type GotchiFins to open the panel.");
            return LoadReturnCode.Success;
        }
    }
}
