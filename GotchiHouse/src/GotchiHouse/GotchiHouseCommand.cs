using System.Runtime.InteropServices;
using Rhino;
using Rhino.Commands;
using Rhino.UI;

namespace GotchiHouse
{
    /// <summary>Opens the GotchiHouse panel.</summary>
    [Guid("8374765e-8541-4934-b4cb-9f46a5b74f39")]
    public class GotchiHouseCommand : Command
    {
        public override string EnglishName => "GotchiHouse";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            Panels.OpenPanel(typeof(GotchiHousePanel).GUID);
            if (!GotchiFonts.IsKorolev)
                RhinoApp.WriteLine($"[GotchiHouse] Korolev font not found; using {GotchiFonts.Family.Name}.");
            return Result.Success;
        }
    }
}
