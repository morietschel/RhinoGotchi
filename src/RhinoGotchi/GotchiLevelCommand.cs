using System;
using System.Linq;
using System.Runtime.InteropServices;
using Rhino;
using Rhino.Commands;
using Rhino.Input;
using Rhino.Input.Custom;
using RhinoGotchi.Levels;

namespace RhinoGotchi
{
    /// <summary>
    /// Starts a RhinoGotchi level or tutorial. With nothing running it asks which one
    /// (Level=..., Enter to start). While one is running it offers Check, Hint, Restart and Quit.
    /// Scriptable: <c>-GotchiLevel Level=Tut01Loft _Enter</c>.
    /// </summary>
    [Guid("6a0f3c1e-8d4b-4e2a-b7f5-1c9d2e3f4a5b")]
    // A game command: Enter on an empty command line must not start it again.
    [CommandStyle(Style.DoNotRepeat)]
    public class GotchiLevelCommand : Command
    {
        public const string Name = "GotchiLevel";

        // Remembered between runs so Enter replays the last choice.
        private static int _lastChoice;

        public override string EnglishName => Name;

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            LevelRunner? running = LevelRunner.Current;
            return running == null ? ChooseAndStart(doc) : WhileRunning(doc, running);
        }

        private static Result ChooseAndStart(RhinoDoc doc)
        {
            var entries = LevelCatalog.All;
            int choice = Math.Max(0, Math.Min(_lastChoice, entries.Count - 1));

            var go = new GetOption();
            go.AcceptNothing(true);
            while (true)
            {
                go.ClearCommandOptions();
                go.SetCommandPrompt($"Level to start: {entries[choice].Title} ({entries[choice].Command}). Press Enter to start");
                int levelOption = go.AddOptionList("Level", entries.Select(e => e.OptionName), choice);

                GetResult result = go.Get();
                if (result == GetResult.Option && go.OptionIndex() == levelOption)
                {
                    choice = go.Option().CurrentListOptionIndex;
                    continue;
                }
                if (result == GetResult.Nothing)
                    break;
                return go.CommandResult() == Result.Success ? Result.Cancel : go.CommandResult();
            }

            _lastChoice = choice;
            return entries[choice].Start(doc) != null ? Result.Success : Result.Failure;
        }

        private static Result WhileRunning(RhinoDoc doc, LevelRunner running)
        {
            var go = new GetOption();
            go.SetCommandPrompt($"Level \"{running.Level.Title}\" is running");
            int check = go.AddOption("Check");
            int hint = go.AddOption("Hint");
            int restart = go.AddOption("Restart");
            int quit = go.AddOption("Quit");
            go.Get();
            if (go.CommandResult() != Result.Success)
                return go.CommandResult();

            int option = go.Option().Index;
            if (option == check)
            {
                if (running.Evaluate() == null)
                    RhinoApp.WriteLine("[RhinoGotchi] This level has no check yet.");
            }
            else if (option == hint)
            {
                string? text = running.NextHint();
                RhinoApp.WriteLine(text == null ? "[RhinoGotchi] No more hints for this level." : "[RhinoGotchi] Hint: " + text);
            }
            else if (option == restart)
            {
                // Clear the previous attempt so the start geometry isn't duplicated.
                var previous = running.Attempt.StartObjectIds.Concat(running.Attempt.AddedObjectIds).ToList();
                string levelId = running.Level.Id;
                running.Stop();
                doc.Objects.Delete(previous, true);
                LevelEntry entry = LevelCatalog.Find(levelId) ?? LevelCatalog.All[0];
                entry.Start(doc);
            }
            else if (option == quit)
            {
                running.Stop();
                RhinoApp.WriteLine("[RhinoGotchi] Level quit.");
            }
            return Result.Success;
        }
    }
}
