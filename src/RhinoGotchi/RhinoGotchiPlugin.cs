using Rhino;
using Rhino.PlugIns;

namespace RhinoGotchi
{
    /// <summary>
    /// Every RhinoCommon .rhp assembly must have exactly one class derived from PlugIn.
    /// Rhino creates a single instance of it when the plugin loads.
    /// </summary>
    public class RhinoGotchiPlugin : PlugIn
    {
        public RhinoGotchiPlugin()
        {
            Instance = this;
        }

        /// <summary>The only instance of this plugin.</summary>
        public static RhinoGotchiPlugin? Instance { get; private set; }

        // Load with Rhino instead of on first use. A load-on-demand plugin is only known
        // through the command list Rhino cached last session, so newly added commands
        // (like GotchiLevel) are "unknown" until the plugin happens to load.
        public override PlugInLoadTime LoadTime => PlugInLoadTime.AtStartup;

        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {
            Rhino.UI.Panels.RegisterPanel(this, typeof(UI.GotchiPanel), "RhinoGotchi",
                typeof(RhinoGotchiPlugin).Assembly, "RhinoGotchi.Resources.GotchiPanel.svg", Rhino.UI.PanelType.System);
            RhinoApp.WriteLine($"RhinoGotchi loaded ({RuntimeLabel()}). Type Gotchi to open the lesson panel.");
            return LoadReturnCode.Success;
        }

        private static string RuntimeLabel()
        {
#if NETFRAMEWORK
            return ".NET Framework 4.8";
#else
            return System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
#endif
        }
    }
}
