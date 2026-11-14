using System.Runtime.InteropServices;
using Rhino;
using Rhino.Commands;
using Rhino.Input;

namespace GotchiFins
{
    /// <summary>
    /// What the RhinoGotchi toolbar buttons run: GotchiFinsRun Box, GotchiFinsRun ExtrudeCrv, ...
    /// When the command is the current level's, it starts the way the level wants (your profile
    /// or fin picked, the extrude direction set); otherwise it just runs the command.
    /// </summary>
    [Guid("b939ac2d-11a4-492f-921a-bed6907a7b92")]
    public class GotchiFinsRunCommand : Command
    {
        public override string EnglishName => "GotchiFinsRun";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            string command = "";
            Result rc = RhinoGet.GetString("Command to run", false, ref command);
            if (rc != Result.Success || string.IsNullOrWhiteSpace(command))
                return rc == Result.Success ? Result.Nothing : rc;

            // Runs once this command has finished, so the two don't collide
            FinSession.RunCommand(command.Trim().TrimStart('_', '!'));
            return Result.Success;
        }
    }
}
