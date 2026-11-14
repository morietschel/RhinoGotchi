using System.Runtime.InteropServices;
using Rhino;
using Rhino.Commands;
using Rhino.UI;
using RhinoGotchi.UI;

namespace RhinoGotchi
{
    /// <summary>Opens the RhinoGotchi games panel.</summary>
    [Guid("bfcfbeee-5952-4fa0-9479-c1ea59b220c7")]
    public class RhinoGotchiCommand : Command
    {
        public RhinoGotchiCommand()
        {
            Instance = this;
        }

        /// <summary>The only instance of this command.</summary>
        public static RhinoGotchiCommand? Instance { get; private set; }

        /// <summary>The command name as it appears on the Rhino command line.</summary>
        public override string EnglishName => "RhinoGotchi";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            Panels.OpenPanel(RhinoGotchiPanel.PanelId);
            return Result.Success;
        }
    }
}
