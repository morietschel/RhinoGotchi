using Rhino;
using Rhino.PlugIns;
using Rhino.UI;
using RhinoGotchi.UI;

namespace RhinoGotchi
{
    /// <summary>
    /// Every RhinoCommon .rhp assembly must have exactly one class derived from PlugIn.
    /// Rhino creates a single instance of it when the plugin loads.
    /// </summary>
    public class RhinoGotchiPlugin : PlugIn
    {
        /// <summary>Embedded SVG used for the side panel tab icon.</summary>
        internal const string IconResource = "RhinoGotchi.Resources.RhinoGotchi.svg";

        public RhinoGotchiPlugin()
        {
            Instance = this;
        }

        /// <summary>The only instance of this plugin.</summary>
        public static RhinoGotchiPlugin? Instance { get; private set; }

        // Load with Rhino instead of on first use. A load-on-demand plugin is only known
        // through the command list Rhino cached last session, so newly added commands
        // (like GotchiLevel) are "unknown" until the plugin happens to load. It also makes
        // the RhinoGotchi panel tab available without running a command first.
        public override PlugInLoadTime LoadTime => PlugInLoadTime.AtStartup;

        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {
            Panels.RegisterPanel(
                this,
                typeof(RhinoGotchiPanel),
                "RhinoGotchi",
                typeof(RhinoGotchiPlugin).Assembly,
                IconResource,
                PanelType.System);

            Panels.RegisterPanel(
                this,
                typeof(GotchiPanel),
                "Gotchi Lessons",
                typeof(RhinoGotchiPlugin).Assembly,
                "RhinoGotchi.Resources.GotchiPanel.svg",
                PanelType.System);

            RhinoApp.WriteLine($"RhinoGotchi loaded ({RuntimeLabel()}). Type RhinoGotchi for the games panel, or Gotchi for the lesson panel.");
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
