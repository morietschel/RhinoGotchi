using System.Runtime.InteropServices;
using Rhino;
using Rhino.Commands;
using Rhino.UI;
using RhinoGotchi.UI;

namespace RhinoGotchi
{
    /// <summary>Opens the RhinoGotchi lesson panel.</summary>
    [Guid("e4b1c7a0-9f2d-4c3b-8a6e-5d0f1b2c3a49")]
    public class GotchiCommand : Command
    {
        public override string EnglishName => "Gotchi";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            Panels.OpenPanel(GotchiPanel.PanelId);
            return Result.Success;
        }
    }
}
