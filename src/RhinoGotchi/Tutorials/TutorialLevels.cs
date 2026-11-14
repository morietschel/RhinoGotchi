using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using RhinoGotchi.Levels;

namespace RhinoGotchi.Tutorials
{
    /// <summary>Runs a tutorial as a level: workspace and set-up first, then the level runner.</summary>
    public static class TutorialLevels
    {
        /// <summary>
        /// Opens the tutorial's workspace, adds its start geometry, and starts its level with the
        /// reference objects highlighted. Works without the Klosterneuburg model (standalone set-up).
        /// When the level stops, the workspace closes: the current layer is put back and the
        /// original model is checked for changes.
        /// <para>
        /// If the tutorial's layers already hold work from an earlier session, it resumes instead of
        /// setting up again: the Input objects are reused and the Result objects count as the learner's.
        /// </para>
        /// </summary>
        public static LevelRunner? Start(RhinoDoc doc, Tutorial tutorial)
        {
            LevelRunner.Current?.Stop();

            ReferenceModel? reference = ReferenceModel.TryOpen(doc, out string reason);
            if (reference == null)
                RhinoApp.WriteLine("[RhinoGotchi] {0} Starting \"{1}\" in standalone mode.", reason, tutorial.Title);

            TutorialWorkspace workspace = TutorialWorkspace.Open(doc, tutorial);
            List<Guid> inputs = workspace.ObjectsOn(TutorialWorkspace.InputName);
            List<Guid> earlierWork = workspace.ObjectsOn(TutorialWorkspace.ResultName);
            bool resume = inputs.Count > 0 || earlierWork.Count > 0;
            if (resume)
            {
                workspace.MakeResultCurrent();
                TutorialProgress.MarkStarted(tutorial.Id);
                RhinoApp.WriteLine("[RhinoGotchi] Continuing \"{0}\": your earlier work is still on its layers.", tutorial.Title);
            }
            else
            {
                inputs = workspace.SetUp(reference).ToList();
            }

            var context = new TutorialContext(workspace, reference);
            Level level = InvariantText.Run(() => tutorial.CreateLevel?.Invoke(context) ?? DefaultLevel(tutorial));
            level.TutorialId = tutorial.Id;
            level.StopWhenSolved = false;
            if (reference != null && level.ReferenceIds.Count == 0)
                level.ReferenceIds.AddRange(reference.Ids(tutorial.ReferenceLayers.ToArray()));
            if (level.Layer == null)
                level.Layer = tutorial.LayerPath + "::" + TutorialWorkspace.InputName;

            LevelRunner runner = LevelRunner.Start(doc, level, inputs);
            runner.Attempt.AddedObjectIds.AddRange(earlierWork);
            workspace.MakeResultCurrent();
            // Only checks the learner asked for count towards progress (the quiet after-command checks don't).
            runner.Checked += (s, result) => TutorialProgress.Record(tutorial.Id, result.Solved, result.Score);
            runner.Stopped += (s, e) =>
            {
                if (TutorialWorkspace.Current == workspace)
                    workspace.Close();
            };
            return runner;
        }

        /// <summary>The level used until a tutorial has its own: no ghosts, no evaluator, step texts as hints.</summary>
        public static Level DefaultLevel(Tutorial tutorial)
        {
            var level = new Level
            {
                Id = tutorial.Id,
                Title = $"{tutorial.Number:00} {tutorial.Title}",
                Command = tutorial.MainCommand,
            };
            level.Hints.AddRange(tutorial.Steps.Select(step => step.Text));
            return level;
        }
    }
}
