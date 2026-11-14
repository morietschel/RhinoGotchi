using System.Linq;
using System.Runtime.InteropServices;
using Rhino;
using Rhino.Commands;
using Rhino.Input.Custom;
using RhinoGotchi.Levels;

namespace RhinoGotchi
{
    /// <summary>
    /// Starts a RhinoGotchi level. While one is running, the same command offers
    /// Check (evaluate now), Restart and Quit.
    /// </summary>
    [Guid("6a0f3c1e-8d4b-4e2a-b7f5-1c9d2e3f4a5b")]
    public class GotchiLevelCommand : Command
    {
        public const string Name = "GotchiLevel";

        public override string EnglishName => Name;

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            LevelRunner? running = LevelRunner.Current;
            if (running == null)
            {
                LevelRunner.Start(doc, LevelCatalog.All[0]());
                return Result.Success;
            }

            var go = new GetOption();
            go.SetCommandPrompt($"Level \"{running.Level.Title}\" is running");
            int check = go.AddOption("Check");
            int restart = go.AddOption("Restart");
            int quit = go.AddOption("Quit");
            go.Get();
            if (go.CommandResult() != Result.Success)
                return go.CommandResult();

            int choice = go.Option().Index;
            if (choice == check)
            {
                if (running.Evaluate() == null)
                    RhinoApp.WriteLine("[RhinoGotchi] This level has no evaluator.");
            }
            else if (choice == restart)
            {
                // Clear the previous attempt so the start geometry isn't duplicated.
                var previous = running.Attempt.StartObjectIds.Concat(running.Attempt.AddedObjectIds).ToList();
                string levelId = running.Level.Id;
                running.Stop();
                doc.Objects.Delete(previous, true);
                Level level = LevelCatalog.All.Select(create => create()).FirstOrDefault(l => l.Id == levelId)
                              ?? LevelCatalog.All[0]();
                LevelRunner.Start(doc, level);
            }
            else if (choice == quit)
            {
                running.Stop();
                RhinoApp.WriteLine("[RhinoGotchi] Level quit.");
            }
            return Result.Success;
        }
    }
}
