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

        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {
            RhinoApp.WriteLine($"RhinoGotchi loaded ({RuntimeLabel()}).");
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
