using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input.Custom;
using RhinoGotchi.Content;
using RhinoGotchi.Levels;

namespace RhinoGotchi
{
    /// <summary>
    /// Developer self-test for the Blocked! game, run from a script. Writes to %TEMP%\RhinoGotchiSelfTest.log.
    ///   Unit   - checks the shape evaluator against good and bad solutions for every challenge.
    ///   Start  - starts the Box challenge (ghost, points, Box blocked) for a scripted run.
    ///   Report - after the script ran Box and then Rectangle + ExtrudeCrv, reports what the game saw.
    /// </summary>
    [Guid("9d2f4b6a-3c1e-4f8d-a5b7-2e6c8d0f1a34")]
    [CommandStyle(Style.Hidden)]
    public class SelfTestCommand : Command
    {
        public override string EnglishName => "RhinoGotchiSelfTest";

        private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "RhinoGotchiSelfTest.log");
        private static LevelRunner? _runner;
        private static ShapeMatchEvaluator? _evaluator;
        private static CommandBlocker? _blocker;
        private static readonly List<string> BlockedEvents = new List<string>();

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var go = new GetOption();
            go.SetCommandPrompt("Self test");
            int unit = go.AddOption("Unit");
            int start = go.AddOption("Start");
            int report = go.AddOption("Report");
            int layout = go.AddOption("Layout");
            go.Get();
            if (go.CommandResult() != Result.Success)
                return go.CommandResult();
            int choice = go.Option().Index;

            if (choice == unit) Unit(doc);
            else if (choice == start) Start(doc);
            else if (choice == report) Report(doc);
            else if (choice == layout)
            {
                try { Layout(); }
                catch (Exception ex) { Log("Layout failed: " + ex); }
            }
            return Result.Success;
        }

        private static void Log(string line)
        {
            File.AppendAllText(LogPath, line + Environment.NewLine);
            RhinoApp.WriteLine("[SelfTest] " + line);
        }

        private static void Check(string name, bool ok, string detail) =>
            Log($"{(ok ? "PASS" : "FAIL")} {name}: {detail}");

        // ---------------- Unit ----------------

        private static void Unit(RhinoDoc doc)
        {
            Log($"=== Unit {DateTime.Now:u} (tolerance {doc.ModelAbsoluteTolerance})");
            double tol = doc.ModelAbsoluteTolerance;
            var line = new LineCurve(Point3d.Origin, new Point3d(0, 0, 8));
            var arc = new Arc(new Point3d(0, 0, -4), new Point3d(4, 0, 0), new Point3d(0, 0, 4)).ToNurbsCurve();
            var sphereRev = RevSurface.Create(arc, new Line(new Point3d(0, 0, -4), new Point3d(0, 0, 4)));

            var cases = new (string Challenge, string Name, Func<GeometryBase?> Make, bool Expect, bool Exclude)[]
            {
                ("box", "Rectangle + ExtrudeCrv", () => Extrusion.Create(new Rectangle3d(Plane.WorldXY, Point3d.Origin, new Point3d(10, 6, 0)).ToNurbsCurve(), Plane.WorldXY, 4, true), true, false),
                ("box", "open extrusion (no Solid)", () => Extrusion.Create(new Rectangle3d(Plane.WorldXY, Point3d.Origin, new Point3d(10, 6, 0)).ToNurbsCurve(), Plane.WorldXY, 4, false), false, false),
                ("box", "too short (3.5)", () => Extrusion.Create(new Rectangle3d(Plane.WorldXY, Point3d.Origin, new Point3d(10, 6, 0)).ToNurbsCurve(), Plane.WorldXY, 3.5, true), false, false),
                ("box", "right size, wrong place", () => new Box(new BoundingBox(new Point3d(20, 0, 0), new Point3d(30, 6, 4))).ToBrep(), false, false),
                ("box", "made by blocked Box", () => new Box(new BoundingBox(Point3d.Origin, new Point3d(10, 6, 4))).ToBrep(), false, true),
                ("cylinder", "Circle + ExtrudeCrv", () => Extrusion.Create(new Circle(Point3d.Origin, 3).ToNurbsCurve(), Plane.WorldXY, 8, true), true, false),
                ("cylinder", "Line + Pipe (flat caps)", () => Brep.CreatePipe(line, 3, false, PipeCapMode.Flat, true, tol, RhinoMath.ToRadians(1))?.FirstOrDefault(), true, false),
                ("sphere", "Arc + Revolve", () => sphereRev == null ? null : Brep.CreateFromRevSurface(sphereRev, false, false), true, false),
                ("sphere", "half sphere (Revolve 180)", () => RevSurface.Create(arc, new Line(new Point3d(0, 0, -4), new Point3d(0, 0, 4)), 0, Math.PI) is RevSurface half ? Brep.CreateFromRevSurface(half, false, false) : null, false, false),
                ("plate", "Rectangle + Circle, PlanarSrf + ExtrudeSrf", () =>
                {
                    var outer = new Rectangle3d(Plane.WorldXY, Point3d.Origin, new Point3d(10, 10, 0)).ToNurbsCurve();
                    var hole = new Circle(new Point3d(5, 5, 0), 2).ToNurbsCurve();
                    var face = Brep.CreatePlanarBreps(new Curve[] { outer, hole }, tol)?[0];
                    return face?.Faces[0].CreateExtrusion(new LineCurve(Point3d.Origin, new Point3d(0, 0, 2)), true);
                }, true, false),
                ("plate", "plate without the hole", () => new Box(new BoundingBox(Point3d.Origin, new Point3d(10, 10, 2))).ToBrep(), false, false),
            };

            int pass = 0;
            foreach (var c in cases)
            {
                var challenge = BlockedChallenges.All.First(x => x.Id == c.Challenge);
                var (level, evaluator) = challenge.CreateLevel();
                var attempt = new LevelAttempt(doc, level);
                var geometry = c.Make();
                if (geometry == null)
                {
                    Check($"{c.Challenge} / {c.Name}", false, "could not build the test geometry");
                    continue;
                }
                Guid id = doc.Objects.Add(geometry);
                attempt.AddedObjectIds.Add(id);
                if (c.Exclude)
                    evaluator.Exclude = g => g == id;
                var result = evaluator.Evaluate(attempt);
                bool ok = result.Solved == c.Expect;
                if (ok) pass++;
                Check($"{c.Challenge} / {c.Name}", ok, $"solved={result.Solved} (expected {c.Expect}). {result.Feedback}");
                doc.Objects.Delete(id, true);
            }
            Log($"Unit: {pass}/{cases.Length} passed");
            doc.Modified = false;
        }

        // ---------------- Layout screenshots ----------------

        /// <summary>Shows each screen in a window at several widths and saves a screenshot of each.</summary>
        private static void Layout()
        {
#if NETFRAMEWORK
            Log("Layout: run on the .NET 7+ runtime");
#else
            if (!OperatingSystem.IsWindows())
            {
                Log("Layout: screenshots are Windows-only");
                return;
            }
            string folder = Path.Combine(Path.GetTempPath(), "RhinoGotchiLayout");
            Directory.CreateDirectory(folder);
            var screens = new (string Name, Func<UI.RhinoGotchiPanel, Eto.Forms.Control?> Make)[]
            {
                ("home", host => null),
                ("library", host => new UI.LibraryView(host, 0)),
                ("command-line", host => new UI.CommandView(host, CommandCatalog.Find("Line")!, new Eto.Forms.Panel())),
                ("build-order", host => new UI.BuildOrderView(host, practice: true)),
                ("blocked", host => new UI.BlockedView(host, practice: true)),
                ("blocked-list", host => new UI.BlockedListView(host)),
            };
            float scale = Eto.Forms.Screen.PrimaryScreen.LogicalPixelSize;
            // One live window per screen, resized like dragging the panel edge: normal, squished, wide, back.
            int[] widths = { 360, 230, 720, 360 };
            foreach (var (name, make) in screens)
            {
                var host = new UI.RhinoGotchiPanel(0);
                var view = make(host);
                if (view == null) host.ShowHome(); else host.Navigate(view);
                var form = new Eto.Forms.Form
                {
                    Title = name,
                    ClientSize = new Eto.Drawing.Size(widths[0], 760),
                    Content = host,
                    Topmost = true,
                    Location = new Eto.Drawing.Point(40, 40),
                };
                form.Show();
                for (int step = 0; step < widths.Length; step++)
                {
                    form.ClientSize = new Eto.Drawing.Size(widths[step], 760);
                    for (int i = 0; i < 30; i++)
                    {
                        Eto.Forms.Application.Instance.RunIteration();
                        System.Threading.Thread.Sleep(15);
                    }
                    var r = form.Bounds;
                    if (r.Width <= 0 || r.Height <= 0)
                    {
                        Log($"Layout: {name} at {widths[step]} had no size on screen, skipped");
                        continue;
                    }
                    using (var bmp = new System.Drawing.Bitmap((int)(r.Width * scale), (int)(r.Height * scale)))
                    {
                        using (var g = System.Drawing.Graphics.FromImage(bmp))
                            g.CopyFromScreen((int)(r.X * scale), (int)(r.Y * scale), 0, 0, bmp.Size);
                        bmp.Save(Path.Combine(folder, $"{name}_{step + 1}_{widths[step]}.png"), System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
                (view as UI.IGameView)?.Leave();
                form.Close();
                Log($"Layout: saved {name}");
            }
            Log($"Layout screenshots in {folder}");
#endif
        }

        // ---------------- Scripted session ----------------

        private static void Start(RhinoDoc doc)
        {
            Log($"=== Session {DateTime.Now:u}");
            var challenge = BlockedChallenges.All.First(x => x.Id == "box");
            var (level, evaluator) = challenge.CreateLevel();
            _evaluator = evaluator;
            _blocker = new CommandBlocker(doc, challenge.Blocked);
            evaluator.Exclude = _blocker.IsBlockedObject;
            _blocker.Blocked += c => BlockedEvents.Add(c);
            _runner = LevelRunner.Start(doc, level);
            Log($"Started '{challenge.Title}', blocked: {string.Join(", ", challenge.Blocked)}; start objects: {_runner.Attempt.StartObjectIds.Count}; ghost shown: {level.GhostGeometry.Count}");
        }

        private static void Report(RhinoDoc doc)
        {
            if (_runner == null || _evaluator == null || _blocker == null)
            {
                Check("session", false, "Start was not run");
                return;
            }
            var attempt = _runner.Attempt;
            Log("Commands the game saw: " + string.Join(" → ", attempt.CommandsRun));
            Log($"Active view: {doc.Views.ActiveView?.ActiveViewport.Name}");
            foreach (var id in attempt.AddedObjectIds)
            {
                var o = doc.Objects.FindId(id);
                var b = o?.Geometry.GetBoundingBox(true) ?? BoundingBox.Empty;
                Log($"  added {o?.ObjectType}: {b.Min} to {b.Max}");
            }
            Check("Box was blocked", BlockedEvents.Contains("Box"), $"blocked events: [{string.Join(", ", BlockedEvents)}]");

            int boxesLeft = doc.Objects.Where(o => _blocker.IsBlockedObject(o.Id)).Count();
            Check("blocked Box's geometry was removed", boxesLeft == 0, $"{boxesLeft} blocked objects still in the model");

            var result = _runner.LastResult ?? _evaluator.Evaluate(attempt);
            Check("Rectangle + ExtrudeCrv verified in the model", result.Solved, result.Feedback ?? "");
            Check("runner stopped after solving", LevelRunner.Current == null, LevelRunner.Current == null ? "stopped" : "still running");
            var matched = doc.Objects.FindId(_evaluator.MatchedId);
            Check("matched object is the extrusion", matched != null && !_blocker.IsBlockedObject(matched.Id),
                matched == null ? "none" : $"{matched.ObjectType} {matched.Id}");

            var way = BlockedChallenges.All.First(x => x.Id == "box").Match(attempt.CommandsRun);
            Check("route named as a known way", way?.Id == "rect", way == null ? "no match" : $"{way.Id}: {way.Name}");

            _runner.Stop();
            _blocker.Dispose();
            doc.Modified = false;
            Log("Session done");
        }
    }
}
