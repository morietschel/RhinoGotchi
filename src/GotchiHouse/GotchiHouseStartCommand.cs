using System.Runtime.InteropServices;
using Rhino;
using Rhino.Commands;

namespace GotchiHouse
{
    /// <summary>
    /// Starts the tutorial without the panel: same as pressing START.
    /// Handy for keyboard users and for scripted runs.
    /// </summary>
    [Guid("e43166d9-1f7b-40a9-a7bb-fb58b6bb1955")]
    public class GotchiHouseStartCommand : Command
    {
        public override string EnglishName => "GotchiHouseStart";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            HouseSession.Start(doc);
            return Result.Success;
        }
    }
}
