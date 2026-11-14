using System.Runtime.InteropServices;
using Rhino;
using Rhino.Commands;
using Rhino.UI;

namespace HouseTutor
{
    /// <summary>Opens the House Tutor panel.</summary>
    [Guid("8f03ec02-18bc-483e-ad9c-7bdf24b34474")]
    public class HouseTutorCommand : Command
    {
        public override string EnglishName => "HouseTutor";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            Panels.OpenPanel(typeof(HouseTutorPanel).GUID);
            return Result.Success;
        }
    }
}
