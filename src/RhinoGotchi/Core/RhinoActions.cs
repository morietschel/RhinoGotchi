using System;
using Eto.Forms;
using Rhino;

namespace RhinoGotchi.Core
{
    /// <summary>Lets a game hand the player straight to the real command in Rhino.</summary>
    internal static class RhinoActions
    {
        public static void Run(string command)
        {
            if (Rhino.Commands.Command.InCommand())
            {
                RhinoApp.WriteLine("RhinoGotchi: finish or cancel the current command first, then try {0} again.", command);
                return;
            }
            RhinoApp.RunScript("_" + command, false);
        }

        public static string HelpUrl(string command) =>
            $"https://docs.mcneel.com/rhino/8/help/en-us/commands/{command.ToLowerInvariant()}.htm";

        public static void OpenHelp(string command)
        {
            try
            {
                Application.Instance.Open(HelpUrl(command));
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine("RhinoGotchi: could not open {0} ({1}).", HelpUrl(command), ex.Message);
            }
        }
    }
}
