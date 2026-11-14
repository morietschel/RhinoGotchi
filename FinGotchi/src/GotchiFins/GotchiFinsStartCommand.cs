using System.Runtime.InteropServices;
using Rhino;
using Rhino.Commands;

namespace GotchiFins
{
    /// <summary>
    /// Starts the game without the panel: same as pressing Start.
    /// Handy for keyboard users and for scripted runs.
    /// </summary>
    [Guid("22c3b6c5-5ae8-4392-8f3e-6c446df713ad")]
    public class GotchiFinsStartCommand : Command
    {
        public override string EnglishName => "GotchiFinsStart";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            FinSession.Start(doc);
            return Result.Success;
        }
    }
}
