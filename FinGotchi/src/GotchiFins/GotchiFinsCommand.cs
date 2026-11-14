using System.Runtime.InteropServices;
using Rhino;
using Rhino.Commands;
using Rhino.UI;

namespace GotchiFins
{
    /// <summary>Opens the GotchiFins panel.</summary>
    [Guid("2e4d92f4-3194-4878-9d37-e9fc7eaef14f")]
    public class GotchiFinsCommand : Command
    {
        public override string EnglishName => "GotchiFins";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            Panels.OpenPanel(typeof(GotchiFinsPanel).GUID);
            return Result.Success;
        }
    }
}
