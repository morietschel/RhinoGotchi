using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using RhinoGotchi.Levels;

namespace RhinoGotchi.Tutorials
{
    /// <summary>Runs a tutorial card as a level: workspace and set-up first, then the level runner.</summary>
    public static class TutorialLevels
    {
        /// <summary>
        /// Opens the card's workspace, clears any earlier attempt, adds its start geometry and starts its
        /// level: reference objects highlighted, ghosts and yellow click marks shown, the live check
        /// running after every command. Works without the Klosterneuburg model (standalone set-up).
        /// The level keeps running once solved, so the bonus can still be earned; it ends when the
        /// player moves on (or quits), which closes the workspace: the current layer and snaps are put
        /// back and the original model is checked for changes.
        /// </summary>
        public static LevelRunner? Start(RhinoDoc doc, Tutorial tutorial)
        {
            LevelRunner.Current?.Stop();
            TutorialHud.Enable();

            ReferenceModel? reference = ReferenceModel.TryOpen(doc, out string reason);
            if (reference == null)
                RhinoApp.WriteLine("[RhinoGotchi] {0} Starting \"{1}\" in standalone mode.", reason, tutorial.Title);

            // Every start is a fresh attempt, like "Clear and try another way" in Blocked!.
            TutorialWorkspace.ClearTutorial(doc, tutorial, askFirst: false);
            TutorialWorkspace workspace = TutorialWorkspace.Open(doc, tutorial);
            List<Guid> inputs = workspace.SetUp(reference).ToList();

            var context = new TutorialContext(workspace, reference);
            Level level = InvariantText.Run(() => CreateLevel(tutorial, context));

            LevelRunner runner = LevelRunner.Start(doc, level, inputs);
            workspace.MakeResultCurrent();

            bool recorded = false;
            void OnState(LevelRunner r)
            {
                if (r != runner || recorded || r.LastResult?.Solved != true)
                    return;
                recorded = true;
                TutorialProgress.Record(tutorial.Id, true, r.LastResult.Score, r.Misses == 0);
            }
            LevelRunner.StateChanged += OnState;
            runner.Stopped += (s, e) =>
            {
                LevelRunner.StateChanged -= OnState;
                if (TutorialWorkspace.Current == workspace)
                    workspace.Close();
            };
            return runner;
        }

        /// <summary>The card's level: ghosts, click marks, references and the live check.</summary>
        private static Level CreateLevel(Tutorial tutorial, TutorialContext context)
        {
            Level level = PreviewLevel(tutorial);
            level.StopWhenSolved = false;
            level.Layer = tutorial.LayerPath + "::" + TutorialWorkspace.InputName;
            if (tutorial.Ghosts != null)
                level.GhostGeometry.AddRange(tutorial.Ghosts(context));
            if (tutorial.Clicks != null)
                level.ClickMarks.AddRange(tutorial.Clicks(context));
            if (context.Reference != null)
                level.ReferenceIds.AddRange(context.Reference.Ids(tutorial.ReferenceLayers.ToArray()));
            level.Evaluator = tutorial.Check?.Invoke(context);
            return level;
        }

        /// <summary>The level as shown before it starts (title, command, steps as hints). Needs no document.</summary>
        public static Level PreviewLevel(Tutorial tutorial)
        {
            var level = new Level
            {
                Id = tutorial.Id,
                Title = tutorial.Title,
                Command = tutorial.MainCommand,
                TutorialId = tutorial.Id,
            };
            level.Hints.AddRange(tutorial.Steps.Select(step => step.Text));
            return level;
        }

        /// <summary>
        /// Runs the card's Run macro once Rhino is idle: cancels whatever is running, fills in the ids
        /// of the named Input objects and runs it (FinGotchi's "Go"). Without a macro it just starts
        /// the main command.
        /// </summary>
        public static void Run(Tutorial tutorial)
        {
            LevelRunner? runner = LevelRunner.Current;
            string macro = tutorial.RunMacro ?? "_" + tutorial.MainCommand;
            if (runner != null && runner.Level.TutorialId == tutorial.Id)
            {
                foreach (Guid id in runner.Attempt.StartObjectIds)
                {
                    var obj = runner.Attempt.Doc.Objects.FindId(id);
                    if (obj != null && !string.IsNullOrEmpty(obj.Name))
                        macro = macro.Replace("{" + obj.Name + "}", id.ToString());
                }
            }
            if (macro.Contains("{"))
            {
                // A named input is missing (e.g. the card isn't started): just run the command.
                macro = "_" + tutorial.MainCommand;
            }

            void Once(object? s, EventArgs e)
            {
                RhinoApp.Idle -= Once;
                RhinoApp.RunScript("!", false);
                RhinoDoc.ActiveDoc?.Objects.UnselectAll();
                RhinoApp.RunScript(macro, true);
            }
            RhinoApp.Idle += Once;
        }
    }
}
