using System;
using System.Linq;
using Rhino;
using Rhino.Display;
using RhinoGotchi.Fin;
using RhinoGotchi.Levels;
using EtoSize = Eto.Drawing.SizeF;
using EtoRect = Eto.Drawing.RectangleF;

namespace RhinoGotchi.Tutorials
{
    /// <summary>
    /// FinGotchi's viewport HUD for a running tutorial level, in the active viewport: the lesson card top
    /// left, the dark check card top right, the yellow "type it up here" callout top centre, the pet and
    /// its speech bubble bottom left, and the commands run so far bottom centre.
    /// </summary>
    internal sealed class TutorialHud : DisplayConduit
    {
        private const int MissesToLose = 3;
        private const double PetFps = 8;

        private static readonly System.Collections.Generic.HashSet<string> Ignored = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Undo", "Redo", "SelID", "SelNone", "ScriptEditor", "RunPythonScript", "RunCSharpScript", "RunScript", "-RunPythonScript",
        };

        private static TutorialHud? _instance;
        private static Eto.Forms.UITimer? _timer;
        private static PetMood _lastMood;

        /// <summary>Turns the HUD on (once). It draws only while a tutorial level is running.</summary>
        public static void Enable()
        {
            if (_instance != null)
                return;
            _instance = new TutorialHud { Enabled = true };
            try
            {
                // The pet breathes, dozes and dances: redraw the active view a few times a second while it moves.
                _timer = new Eto.Forms.UITimer { Interval = 1 / PetFps };
                _timer.Elapsed += (s, e) => Tick();
                _timer.Start();
            }
            catch
            {
                // No UI (scripted runs): the HUD still draws, it just doesn't animate.
            }
        }

        private static bool TryActive(out LevelRunner runner, out Tutorial tutorial)
        {
            runner = null!;
            tutorial = null!;
            LevelRunner? current = LevelRunner.Current;
            string? id = current?.Level.TutorialId;
            Tutorial? found = id == null ? null : TutorialCatalog.Find(id);
            if (current == null || found == null)
                return false;
            runner = current;
            tutorial = found;
            return true;
        }

        private static PetMood MoodOf(LevelRunner runner) =>
            runner.LastResult?.Solved == true ? PetMood.Win
            : runner.Misses >= MissesToLose ? PetMood.Lose
            : PetActivity.Dozing ? PetMood.Standby
            : PetMood.Awake;

        private static void Tick()
        {
            if (!TryActive(out LevelRunner runner, out _))
                return;
            PetMood mood = MoodOf(runner);
            if (mood != PetMood.Awake || mood != _lastMood)
                runner.Attempt.Doc.Views.ActiveView?.Redraw();
            _lastMood = mood;
        }

        protected override void DrawForeground(DrawEventArgs e)
        {
            if (!TryActive(out LevelRunner runner, out Tutorial tutorial))
                return;
            RhinoView? active = runner.Attempt.Doc.Views.ActiveView;
            if (active == null || e.Viewport.Id != active.ActiveViewportID)
                return;

            float s = Math.Max(1f, (float)e.Display.DpiScale);
            System.Drawing.Rectangle view = e.Viewport.Bounds;
            int margin = (int)(14 * s);
            LevelResult? result = runner.LastResult;
            bool solved = result?.Solved == true;
            int misses = runner.Misses;
            PetMood mood = MoodOf(runner);

            // Lesson card, top left
            int w = (int)(250 * s);
            var steps = tutorial.Steps.Select(st => st.Text).ToList();
            string kicker = $"BUILD A BUILDING · LEVEL {tutorial.Number} OF {TutorialCatalog.Count}";
            string key = $"tlesson:{tutorial.Id}:{misses}:{solved}:{s}";
            var lesson = Hud.Get(key, w + (int)(8 * s), (int)(500 * s), g =>
                new EtoSize(w + 8 * s, Cards.Lesson(g, 1, 1, w, s, kicker, tutorial.Title, tutorial.MainCommand, steps, -1, misses, MissesToLose, solved) + 8 * s));
            if (lesson.HasValue)
                e.Display.DrawBitmap(lesson.Value.Bitmap, margin, margin);

            // Commands the player ran (not Undo, selections or script tools)
            var recorded = runner.Attempt.CommandsRun.Where(c => !Ignored.Contains(c)).ToList();

            // Check card, top right (wide views only, like FinGotchi)
            bool checkShown = result != null && result.Checks.Count > 0 && view.Width > 700 * s;
            if (checkShown)
            {
                int cw = (int)(240 * s);
                var rows = result!.Checks.Select(c => new CheckRow(c.Label, c.Pass, "")).ToList();
                double score = result.Score;
                string ckey = "tcheck:" + string.Join("|", rows.Select(r => r.Label + r.Pass)) + $":{score:0.000}:{solved}:{s}";
                var card = Hud.Get(ckey, cw + (int)(8 * s), (int)(400 * s), g =>
                    new EtoSize(cw + 8 * s, Cards.Check(g, 1, 1, cw, s, rows, score, solved) + 8 * s));
                if (card.HasValue)
                    e.Display.DrawBitmap(card.Value.Bitmap, view.Width - margin - card.Value.Width, margin);
            }

            // Callout, top centre: where to type the command
            if (!solved)
            {
                string text = $"Type *{tutorial.MainCommand}* in the command line up here, or press Run in the panel.";
                int maxW = (int)Math.Min(420 * s, view.Width * 0.42);
                var callout = Hud.Get($"tcallout:{text}:{maxW}:{s}", maxW, (int)(120 * s), g => Cards.Callout(g, 1, 1, s, text, maxW - 6 * s));
                // Only where it fits between the lesson card and the check card
                int beside = w + margin + (checkShown ? (int)(240 * s) + margin : 0);
                if (callout.HasValue && view.Width - 2 * beside > callout.Value.Width + 2 * margin)
                    e.Display.DrawBitmap(callout.Value.Bitmap, (view.Width - callout.Value.Width) / 2, (int)(8 * s));
            }

            // Pet and bubble, bottom left
            // The same Gotchi as every other game (it wears the player's outfit).
            int petW = (int)(110 * s), bw = (int)(240 * s);
            string speech = Speech(tutorial, recorded.Count > 0 ? result : null, solved, misses, mood);
            RhinoGotchi.Display.ViewportPet.DrawPet(e, mood switch
            {
                PetMood.Win => UI.Mood.Celebrate,
                PetMood.Lose => UI.Mood.Sad,
                PetMood.Standby => UI.Mood.Sleepy,
                _ => UI.Mood.Happy,
            }, speech);

            // Commands run so far, bottom centre
            if (recorded.Count > 6)
                recorded = recorded.Skip(recorded.Count - 6).ToList();
            if (recorded.Count > 0)
            {
                string rkey = "trecorded:" + string.Join(">", recorded) + ":" + s;
                var pill = Hud.Get(rkey, (int)(720 * s), (int)(50 * s), g => Cards.Recorded(g, 1, 1, s, recorded) + new EtoSize(4 * s, 6 * s));
                if (pill.HasValue && view.Width > pill.Value.Width + petW * 2 + bw)
                    e.Display.DrawBitmap(pill.Value.Bitmap, (view.Width - pill.Value.Width) / 2, view.Height - margin - pill.Value.Height);
            }
        }

        private static string Speech(Tutorial t, LevelResult? result, bool solved, int misses, PetMood mood)
        {
            if (solved)
                return misses == 0 ? "First try! Splendid." : "Solved! Press Next in the panel.";
            if (misses >= MissesToLose)
                return "Three misses. Undo, then press Run: it picks everything for you.";
            if (mood == PetMood.Standby)
                return "Zzz... click a yellow point to wake me.";
            LevelCheck? failing = result?.Checks.FirstOrDefault(c => !c.Pass);
            if (failing != null)
                return "Not yet: " + failing.Label + ".";
            return $"{t.MainCommand} time! Snap to the yellow points.";
        }

        /// <summary>The pet at this moment, from a cache of 8-per-second frames. Awake is a still picture.</summary>
        private static (DisplayBitmap Bitmap, int Width, int Height)? PetFrame(PetMood mood, double time, int width)
        {
            int frames = Math.Max(1, (int)Math.Round(Pet.LoopSeconds(mood) * PetFps));
            int frame = mood == PetMood.Awake ? 0 : (int)(time * PetFps) % frames;
            int height = (int)(width / Pet.Aspect(mood));
            int headroom = (int)(height * 0.3);
            return Hud.Get($"tpet:{mood}:{frame}:{width}", width, height + headroom, g =>
            {
                Pet.Draw(g, new EtoRect(0, headroom, width, height), mood, frame / PetFps);
                return new EtoSize(width, height + headroom);
            });
        }
    }
}
