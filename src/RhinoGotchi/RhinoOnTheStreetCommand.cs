using System.Runtime.InteropServices;
using Rhino;
using Rhino.Commands;
using Rhino.Input.Custom;
using RhinoGotchi.Tutorials.RhinoOnTheStreet;

namespace RhinoGotchi
{
    /// <summary>
    /// Starts the RhinoOnTheStreet tutorial. While it runs, the same command
    /// offers SkipIntro, RestartLesson, ReplayFinale and Quit.
    /// </summary>
    [Guid("3f7b2c94-5e1a-4d8b-9c06-7a2e41b8d5f3")]
    // A game command: Enter on an empty command line must not start it again.
    [CommandStyle(Style.DoNotRepeat)]
    public class RhinoOnTheStreetCommand : Command
    {
        public const string Name = "RhinoOnTheStreet";

        public override string EnglishName => Name;

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            StreetTutorial? running = StreetTutorial.Current;
            if (running == null)
            {
                StreetTutorial.Start(doc);
                return Result.Success;
            }

            var go = new GetOption();
            go.SetCommandPrompt("RhinoOnTheStreet is running");
            int skip = running.Phase == Phase.Intro ? go.AddOption("SkipIntro") : -1;
            int restartLesson = running.Phase == Phase.Lesson || running.Phase == Phase.Lose ? go.AddOption("RestartLesson") : -1;
            int replay = running.Phase == Phase.End ? go.AddOption("ReplayFinale") : -1;
            int restart = go.AddOption("RestartTutorial");
            int quit = go.AddOption("Quit");
            go.Get();
            if (go.CommandResult() != Result.Success)
                return go.CommandResult();

            int choice = go.Option().Index;
            if (choice == skip) running.SkipIntro();
            else if (choice == restartLesson) running.RestartLesson();
            else if (choice == replay) running.ReplayFinale();
            else if (choice == restart) StreetTutorial.Start(doc);
            else if (choice == quit) running.Stop();
            return Result.Success;
        }
    }
}
