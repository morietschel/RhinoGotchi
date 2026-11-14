using Rhino;
using Rhino.PlugIns;
using Rhino.UI;

namespace HouseTutor
{
    /// <summary>
    /// The plugin. Rhino creates one instance when it loads the .rhp.
    /// It registers the House Tutor panel so it can be opened and docked.
    /// </summary>
    public class HouseTutorPlugin : PlugIn
    {
        public HouseTutorPlugin()
        {
            Instance = this;
        }

        public static HouseTutorPlugin? Instance { get; private set; }

        // Load with Rhino so the panel and the HouseTutor command are always available.
        public override PlugInLoadTime LoadTime => PlugInLoadTime.AtStartup;

        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {
            Panels.RegisterPanel(
                this,
                typeof(HouseTutorPanel),
                "House Tutor",
                typeof(HouseTutorPlugin).Assembly,
                "HouseTutor.Resources.HouseTutor.ico",
                PanelType.System);

            RhinoApp.WriteLine("HouseTutor loaded. Type HouseTutor to open the panel.");
            return LoadReturnCode.Success;
        }
    }
}
