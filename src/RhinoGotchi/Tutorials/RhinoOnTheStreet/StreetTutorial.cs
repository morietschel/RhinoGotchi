using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Rhino;
using Rhino.ApplicationSettings;
using Rhino.Commands;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.UI;
using static RhinoGotchi.Tutorials.RhinoOnTheStreet.StreetScene;

namespace RhinoGotchi.Tutorials.RhinoOnTheStreet
{
    internal enum Phase { Intro, Lesson, Building, Celebrate, Lose, Finale, End }

    internal enum Mood { Win, Standby, Lose, Final }

    /// <summary>A full-view message card: celebration, level incomplete, or the ending.</summary>
    internal sealed class Overlay
    {
        public string Title = "";
        public string Sub = "";
        public string Stats = "";
        public string Footer = "";
        public Mood Mood;
        public bool Dance;
        public bool Confetti;
        public bool Orange;
        public double Since;
    }

    /// <summary>
    /// RhinoOnTheStreet: a guided tutorial played with Rhino's real commands.
    /// A floating Perspective view pops out over the viewports, an intro flies the camera
    /// down onto a ghosted street, then two lessons (Curve; Line, Line, Arc) are guided with
    /// RGB-coloured targets, arrows and text, the rhino dances on success, and a finale walks
    /// it through the arch into a blue horizon.
    /// The player's clicks are watched with a MouseCallback; each command's result is checked
    /// when it ends.
    /// </summary>
    internal sealed class StreetTutorial
    {
        public const string ViewTitle = "RhinoOnTheStreet";
        private const double Tolerance = 0.05;
        private const double IdleSeconds = 5;

        private readonly RhinoDoc _doc;
        private readonly StreetConduit _conduit;
        private readonly ClickWatcher _mouse = new ClickWatcher();
        private readonly Eto.Forms.UITimer _timer = new Eto.Forms.UITimer { Interval = 1.0 / 30 };
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly List<(double due, Action action)> _schedule = new List<(double, Action)>();

        private RhinoView? _view;
        private Guid _viewportId;
        private int _layerIndex = -1;
        private readonly List<Guid> _targetIds = new List<Guid>();
        private readonly List<Guid> _commandAdds = new List<Guid>();
        private string? _inCommand;
        private int _commandStepFrom;
        private bool _missedThisCommand;
        private bool _adding;
        private bool _savedOsnap;
        private OsnapModes _savedModes;

        // camera tween
        private Point3d _camEye, _camTgt, _fromEye, _fromTgt, _toEye, _toTgt;
        private double _tweenStart, _tweenDur = -1;
        private Action? _tweenDone;
        private System.Drawing.Size _fullSize;
        private double _popStart = -1;

        private StreetTutorial(RhinoDoc doc)
        {
            _doc = doc;
            _conduit = new StreetConduit(this);
        }

        public static StreetTutorial? Current { get; private set; }

        // ---- state read by the conduit ----
        public Phase Phase { get; private set; } = Phase.Intro;
        public Lesson? Lesson { get; private set; }
        public int StepIndex { get; private set; }
        public Step? Step => Lesson != null && StepIndex < Lesson.Steps.Count ? Lesson.Steps[StepIndex] : null;
        public bool Interactive => Phase == Phase.Lesson;
        public bool StreetBuilt { get; private set; }
        public bool ArchBuilt { get; private set; }
        public double Pave { get; private set; }
        public Brep? ArchPreview { get; private set; }
        public int Misses { get; private set; }
        public int MissesThisLesson { get; private set; }
        public bool Flawless { get; private set; } = true;
        public string? BigCaption { get; private set; }
        public string? BigSub { get; private set; }
        public string? Bubble { get; private set; }
        public Mood Mood { get; private set; } = Mood.Win;
        public bool Sleeping { get; private set; }
        public Overlay? Overlay { get; private set; }
        public double Horizon { get; private set; }
        public double? RhinoWalk { get; private set; }
        public double RhinoFade { get; private set; } = 1;
        public List<(System.Drawing.Point at, double t)> MissMarks { get; } = new List<(System.Drawing.Point, double)>();
        public double Now => _clock.Elapsed.TotalSeconds;
        public Guid ViewportId => _viewportId;
        public bool IsLastLesson => Lesson?.Number == 2;
        public int HitRadius => Math.Max(18, ModelAidSettings.OsnapPickboxRadius + 6);
        private double _lastActivity;
        private double _startedAt;
        private double _endedAt = -1;

        public string ElapsedText
        {
            get
            {
                int s = (int)((_endedAt >= 0 ? _endedAt : Now) - _startedAt);
                return $"{s / 60}:{s % 60:00}";
            }
        }

        // ================= lifecycle =================

        public static StreetTutorial Start(RhinoDoc doc)
        {
            Current?.Stop();
            var t = new StreetTutorial(doc);
            Current = t;
            t.Begin();
            return t;
        }

        private void Begin()
        {
            _startedAt = Now;
            _lastActivity = Now;
            _layerIndex = EnsureLayer();

            _savedOsnap = ModelAidSettings.Osnap;
            _savedModes = ModelAidSettings.OsnapModes;
            ModelAidSettings.Osnap = true;
            ModelAidSettings.OsnapModes = _savedModes | OsnapModes.Point;

            CreateView();
            SetCamera(Cams.Aerial.eye, Cams.Aerial.tgt);

            _conduit.Enabled = true;
            _mouse.Down = OnMouseDown;
            _mouse.Enabled = true;
            Command.BeginCommand += OnBeginCommand;
            Command.EndCommand += OnEndCommand;
            RhinoDoc.AddRhinoObject += OnAddObject;
            RhinoDoc.CloseDocument += OnCloseDocument;
            RhinoApp.KeyboardEvent += OnKey;
            _timer.Elapsed += OnTick;
            _timer.Start();

            Say("Hi! Let's build something.");
            RhinoApp.WriteLine("[RhinoOnTheStreet] Tutorial started. Run RhinoOnTheStreet again to skip, restart or quit.");
            Intro();
        }

        public void Stop()
        {
            _timer.Stop();
            _timer.Elapsed -= OnTick;
            _conduit.Enabled = false;
            _mouse.Enabled = false;
            Command.BeginCommand -= OnBeginCommand;
            Command.EndCommand -= OnEndCommand;
            RhinoDoc.AddRhinoObject -= OnAddObject;
            RhinoDoc.CloseDocument -= OnCloseDocument;
            RhinoApp.KeyboardEvent -= OnKey;

            ClearTargets();
            ModelAidSettings.Osnap = _savedOsnap;
            ModelAidSettings.OsnapModes = _savedModes;

            RhinoView? view = FindView();
            if (view != null)
            {
                try { view.Close(); } catch { /* the user may have closed it already */ }
            }
            if (Current == this)
                Current = null;
            _doc.Views.Redraw();
            RhinoApp.WriteLine("[RhinoOnTheStreet] Tutorial closed. The street and arch you built stay in the model.");
        }

        public void SkipIntro()
        {
            if (Phase != Phase.Intro) return;
            _schedule.Clear();
            BigCaption = BigSub = null;
            StartCurveLesson();
        }

        public void RestartLesson()
        {
            if (Lesson == null || (Phase != Phase.Lesson && Phase != Phase.Lose)) return;
            _schedule.Clear();
            Overlay = null;
            if (Lesson.Number == 1) StartCurveLesson(); else StartArchLesson();
        }

        public void ReplayFinale()
        {
            if (Phase != Phase.End) return;
            Overlay = null;
            StartFinale();
        }

        // ================= flow =================

        private static class Cams
        {
            public static readonly (Point3d eye, Point3d tgt) Aerial = (new Point3d(0, -17, 23), new Point3d(0, 14, 0));
            public static readonly (Point3d eye, Point3d tgt) Stand = (new Point3d(0, -1.2, 1.7), new Point3d(0, ArchY, 1.4));
            public static readonly (Point3d eye, Point3d tgt) Curve = (new Point3d(0, -7.5, 7.8), new Point3d(0, 12.5, 0));
            public static readonly (Point3d eye, Point3d tgt) Arch = (new Point3d(-3.6, 12.2, 3.1), new Point3d(0, ArchY, 2.7));
        }

        private void Intro()
        {
            Phase = Phase.Intro;
            At(0.8, () => { Caption("This is your street - still a ghost.", "See-through blue shapes are plans, not geometry."); CamTo(Cams.Stand, 3.2); });
            At(4.3, () => Caption("You're standing in the middle of it.", "It curves away... and ends at an archway."));
            At(7.0, () => { Caption("Let's turn the ghosts into real geometry.", "Two short lessons. Follow the colours: red, green, blue."); CamTo(Cams.Curve, 1.8); });
            At(9.3, () => { BigCaption = BigSub = null; StartCurveLesson(); });
        }

        private void StartCurveLesson()
        {
            Phase = Phase.Building; // camera moving, not interactive yet
            Overlay = null;
            Lesson = StreetLessons.Curve();
            StepIndex = 0;
            MissesThisLesson = 0;
            StreetBuilt = false;
            Pave = 0;
            ShowTargets();
            Say("Lesson 1! Type Curve up top.");
            Log("Lesson 1 of 2: Curve. Type Curve and press Enter.");
            CamTo(Cams.Curve, 1.0, () => { Phase = Phase.Lesson; Activity(); });
        }

        private void StartArchLesson()
        {
            Phase = Phase.Building;
            Overlay = null;
            Lesson = StreetLessons.Arch();
            StepIndex = 0;
            MissesThisLesson = 0;
            ArchBuilt = false;
            ArchPreview = null;
            ShowTargets();
            Say("Walk with me to the arch...");
            Log("Lesson 2 of 2: Arch. Type Line and press Enter.");
            CamTo(Cams.Arch, 1.8, () => { Phase = Phase.Lesson; Say("Pillars first. Type Line."); Activity(); });
        }

        private void Advance()
        {
            StepIndex++;
            Step? s = Step;
            if (s != null && s.Kind == StepKind.Point && _view != null)
                Log(s.Label + ": " + s.Say);
            _doc.Views.Redraw();
        }

        private void Celebrate()
        {
            ClearTargets();
            Lesson l = Lesson!;
            string stats = (MissesThisLesson == 0 ? "0 misses" : $"{MissesThisLesson} miss{(MissesThisLesson > 1 ? "es" : "")}") + "  |  time " + ElapsedText;
            Overlay = l.Number == 1
                ? new Overlay { Title = "Lesson 1 complete!", Sub = "You drew a degree-3 Curve with 5 control points. The street is real now.", Stats = stats, Footer = "Next: build the archway...", Mood = Mood.Win, Dance = true, Confetti = true, Since = Now }
                : new Overlay { Title = "Lesson 2 complete!", Sub = "Two Line pillars + one 3-point Arc = an archway.", Stats = stats, Footer = "Next: walk through it...", Mood = Mood.Win, Dance = true, Confetti = true, Since = Now };
            Phase = Phase.Celebrate;
            Mood = Mood.Win;
            Bubble = null;
            Log($"Lesson {l.Number} complete!");
            At(4.2, () => { Overlay = null; if (l.Number == 1) StartArchLesson(); else StartFinale(); });
        }

        private void Lose(string reason)
        {
            Flawless = false;
            Phase = Phase.Lose;
            Mood = Mood.Lose;
            Bubble = "Oh no...";
            Overlay = new Overlay { Title = "Level incomplete", Sub = reason, Stats = $"Lesson {Lesson!.Number}: {Lesson.Name}", Footer = "Trying that part again in a moment...", Mood = Mood.Lose, Orange = true, Since = Now };
            Log("Level incomplete: " + reason);
            int retryFrom = _commandStepFrom;
            At(4.5, () =>
            {
                Overlay = null;
                StepIndex = retryFrom;
                Mood = Mood.Win;
                Phase = Phase.Lesson;
                Say("Let's try that again!");
                Activity();
            });
        }

        private void StartFinale()
        {
            Phase = Phase.Finale;
            Bubble = null;
            Horizon = 0;
            double s0 = WalkStreetStart;
            double s1 = WalkLength - 6;
            RhinoWalk = s0;
            RhinoFade = 1;
            Log("The rhino walks through the arch...");
            var start = CamFor(s0);
            CamTo(start, 1.6, () =>
            {
                double t0 = Now, dur = 12.5;
                Action? step = null;
                step = () =>
                {
                    double k = Clamp((Now - t0) / dur, 0, 1), e = Ease(k);
                    double s = s0 + (s1 - s0) * e;
                    RhinoWalk = s;
                    var c = CamFor(Math.Min(s, s0 + (s1 - s0) * 0.86));
                    SetCamera(c.eye, c.tgt);
                    Horizon = Smooth(ArchY - 4, ArchY + 12, c.eye.Y);
                    RhinoFade = 1 - Smooth(0.88, 1, k);
                    if (k < 1) At(0, step!);
                    else At(0.5, Ending);
                };
                step();
            });
        }

        private static (Point3d eye, Point3d tgt) CamFor(double s) =>
            (WalkAt(s - 6.5) + new Vector3d(0, 0, 2.3), WalkAt(s + 3) + new Vector3d(0, 0, 0.9));

        private void Ending()
        {
            Phase = Phase.End;
            _endedAt = Now;
            RhinoWalk = null;
            Overlay = Flawless
                ? new Overlay { Title = "Perfect run!", Sub = "Street, pillars, arch - zero misses from start to finish. RhinoGotchi made it to the blue horizon.", Stats = $"0 misses  |  time {ElapsedText}", Footer = "Run RhinoOnTheStreet again to replay the finale, restart or quit.", Mood = Mood.Final, Dance = true, Confetti = true, Since = Now }
                : new Overlay { Title = "Tutorial complete!", Sub = "RhinoGotchi made it through the arch. Finish with zero misses to unlock the perfect ending.", Stats = $"{Misses} miss{(Misses == 1 ? "" : "es")}  |  time {ElapsedText}", Footer = "Run RhinoOnTheStreet again to replay the finale, restart or quit.", Mood = Mood.Win, Dance = true, Confetti = true, Since = Now };
            Mood = Overlay.Mood;
            Log(Flawless ? "Perfect run!" : "Tutorial complete.");
        }

        // ================= Rhino events =================

        private void OnBeginCommand(object? sender, CommandEventArgs e)
        {
            if (e.Document != _doc || IsOwnCommand(e.CommandEnglishName)) return;
            Activity();
            Step? s = Step;
            if (!Interactive || s == null) return;

            bool wanted = (s.Kind == StepKind.Command || s.Kind == StepKind.Repeat)
                          && string.Equals(e.CommandEnglishName, s.Command, StringComparison.OrdinalIgnoreCase);
            if (wanted)
            {
                _inCommand = e.CommandEnglishName;
                _commandStepFrom = StepIndex;
                _commandAdds.Clear();
                _missedThisCommand = false;
                Say(s.Kind == StepKind.Repeat ? "That's the trick! Enter repeats." :
                    s.Group == StepGroup.Red ? "Yes! Now click the RED ring." : s.Group == StepGroup.Green ? "Nice. Now the GREEN one." : "Now the BLUE points.");
                Advance();
            }
            else if (s.Kind == StepKind.Command || s.Kind == StepKind.Repeat)
            {
                Say($"Not quite - this step needs {s.Command}.");
            }
        }

        private void OnEndCommand(object? sender, CommandEventArgs e)
        {
            if (e.Document != _doc || _inCommand == null) return;
            if (!string.Equals(e.CommandEnglishName, _inCommand, StringComparison.OrdinalIgnoreCase)) return;
            string name = _inCommand;
            _inCommand = null;
            var adds = _commandAdds.ToList();
            Result result = e.CommandResult;
            // Change the document on the next tick, not inside Rhino's command-end notification.
            At(0, () => Judge(name, result, adds));
        }

        private void Judge(string command, Result result, List<Guid> adds)
        {
            Lesson? l = Lesson;
            if (l == null || Phase != Phase.Lesson) return;
            List<Curve> curves = adds.Select(id => _doc.Objects.FindId(id)?.Geometry as Curve).Where(c => c != null).Cast<Curve>().ToList();

            string? reason = command.ToLowerInvariant() switch
            {
                "curve" => CheckCurve(result, curves),
                "line" => CheckLine(result, curves, _commandStepFrom == 0 ? LeftBase : RightBase, _commandStepFrom == 0 ? LeftTop : RightTop),
                "arc" => CheckArc(result, curves),
                _ => null,
            };
            if (reason == null && _missedThisCommand)
                reason = "A click landed off its ring.";

            if (reason != null)
            {
                foreach (Guid id in adds) _doc.Objects.Delete(id, true);
                _doc.Views.Redraw();
                Lose(reason);
                return;
            }

            if (command.Equals("Curve", StringComparison.OrdinalIgnoreCase))
            {
                Phase = Phase.Building;
                Say("Paving the street...");
                Log("Curve created: degree 3, 5 control points. Paving...");
                Animate(1.2, k => Pave = k, () =>
                {
                    AddToModel(StreetBrep(), "Street");
                    StreetBuilt = true;
                    Celebrate();
                });
            }
            else if (command.Equals("Line", StringComparison.OrdinalIgnoreCase))
            {
                StepIndex = _commandStepFrom + 3;
                Say(_commandStepFrom == 0 ? "Left pillar done!" : "Both pillars up!");
                Log(_commandStepFrom == 0 ? "Left pillar done. Press Enter to repeat Line." : "Right pillar done. Type Arc and press Enter.");
            }
            else // Arc
            {
                Phase = Phase.Building;
                Say("Thickening it into a solid...");
                Log("Arc created. Extruding the arch into a solid...");
                Animate(1.5, k => ArchPreview = ArchBrep(k), () =>
                {
                    ArchPreview = null;
                    AddToModel(ArchBrep(1), "Arch");
                    ArchBuilt = true;
                    Celebrate();
                });
            }
            _doc.Views.Redraw();
        }

        private static string? CheckCurve(Result result, List<Curve> curves)
        {
            if (result == Result.Cancel) return "Esc cancelled the Curve command.";
            if (curves.Count == 0) return "The Curve command ended before the street was drawn.";
            NurbsCurve nc = curves[0].ToNurbsCurve();
            int n = nc.Points.Count;
            if (n < 5) return $"You finished after {n} points, so the street stops before the arch.";
            if (n > 5) return $"The curve has {n} control points. The street needs exactly 5.";
            if (nc.Degree != 3) return $"The curve is degree {nc.Degree}. Keep the Curve command's Degree=3.";
            bool fwd = true, back = true;
            for (int i = 0; i < 5; i++)
            {
                fwd &= nc.Points[i].Location.DistanceTo(StreetControlPoints[i]) <= Tolerance;
                back &= nc.Points[i].Location.DistanceTo(StreetControlPoints[4 - i]) <= Tolerance;
            }
            return fwd || back ? null : "Some control points missed their rings.";
        }

        private static string? CheckLine(Result result, List<Curve> curves, Point3d a, Point3d b)
        {
            if (result == Result.Cancel) return "Esc cancelled the Line command.";
            if (curves.Count == 0) return "The Line command ended before the pillar was drawn.";
            Curve c = curves[0];
            Point3d s = c.PointAtStart, e = c.PointAtEnd;
            bool ok = c.IsLinear(Tolerance) &&
                      ((s.DistanceTo(a) <= Tolerance && e.DistanceTo(b) <= Tolerance) || (s.DistanceTo(b) <= Tolerance && e.DistanceTo(a) <= Tolerance));
            return ok ? null : "The pillar line doesn't run from the base ring to the top ring.";
        }

        private static string? CheckArc(Result result, List<Curve> curves)
        {
            if (result == Result.Cancel) return "Esc cancelled the Arc command.";
            if (curves.Count == 0) return "The Arc command ended before the arch top was drawn.";
            Curve c = curves[0];
            if (!c.TryGetArc(out Arc arc, Tolerance)) return "That isn't an arc.";
            Point3d s = c.PointAtStart, e = c.PointAtEnd;
            bool ends = (s.DistanceTo(LeftTop) <= Tolerance && e.DistanceTo(RightTop) <= Tolerance) ||
                        (s.DistanceTo(RightTop) <= Tolerance && e.DistanceTo(LeftTop) <= Tolerance);
            if (!ends) return "The arc must start and end on the pillar tops. Use the Start option (type S, Enter) before the first click.";
            return arc.MidPoint.DistanceTo(Crown) <= Tolerance ? null : "The arc doesn't pass through the blue crown.";
        }

        private void OnAddObject(object? sender, RhinoObjectEventArgs e)
        {
            if (_adding || _inCommand == null || e.TheObject.Document != _doc) return;
            _commandAdds.Add(e.ObjectId);
        }

        private void OnMouseDown(MouseCallbackEventArgs e)
        {
            Activity();
            if (e.MouseButton != MouseButton.Left || !Interactive) return;
            Step? s = Step;
            if (s == null) return;
            if (s.Kind != StepKind.Point || _inCommand == null)
            {
                if (s.Kind == StepKind.Command) Say($"Type {s.Command} first - look at the command line.");
                else if (s.Kind == StepKind.Repeat) Say("Press Enter to repeat Line.");
                else if (s.Kind == StepKind.Finish) Say("Press Enter or right-click to finish.");
                return;
            }

            Point2d tp = e.View.ActiveViewport.WorldToClient(s.Target);
            double d = Math.Sqrt(Math.Pow(tp.X - e.ViewportPoint.X, 2) + Math.Pow(tp.Y - e.ViewportPoint.Y, 2));
            if (d <= HitRadius)
            {
                string[] praise = { "Got it!", "Nice click!", "Bullseye!", "Perfect snap!", "Yes!" };
                Say(praise[StepIndex % praise.Length]);
            }
            else
            {
                Misses++;
                MissesThisLesson++;
                Flawless = false;
                _missedThisCommand = true;
                if (e.View.ActiveViewportID == _viewportId)
                    MissMarks.Add((e.ViewportPoint, Now));
                Say("Off the ring! Finish or press Esc, then we'll redo it.");
                Log("That click missed its target.");
            }
            Advance();
        }

        private void OnKey(int key) => Activity();

        private void OnCloseDocument(object? sender, DocumentEventArgs e)
        {
            if (e.Document == _doc) Stop();
        }

        private static bool IsOwnCommand(string name) => string.Equals(name, RhinoOnTheStreetCommand.Name, StringComparison.OrdinalIgnoreCase);

        // ================= timer =================

        private void OnTick(object? sender, EventArgs e)
        {
            double now = Now;

            // pop-out: the floating view grows from half size
            if (_popStart >= 0 && _view != null)
            {
                double k = Clamp((now - _popStart) / 0.6, 0, 1), eK = 1 - Math.Pow(1 - k, 3);
                try
                {
                    _view.Size = new System.Drawing.Size((int)(_fullSize.Width * (0.5 + 0.5 * eK)), (int)(_fullSize.Height * (0.5 + 0.5 * eK)));
                }
                catch { k = 1; }
                if (k >= 1) _popStart = -1;
            }

            if (_tweenDur >= 0)
            {
                double k = _tweenDur <= 0 ? 1 : Clamp((now - _tweenStart) / _tweenDur, 0, 1), e2 = Ease(k);
                SetCamera(_fromEye + (_toEye - _fromEye) * e2, _fromTgt + (_toTgt - _fromTgt) * e2);
                if (k >= 1)
                {
                    _tweenDur = -1;
                    Action? done = _tweenDone;
                    _tweenDone = null;
                    done?.Invoke();
                }
            }

            // run due actions (copy first: actions may schedule more)
            var due = _schedule.Where(a => a.due <= now).ToList();
            _schedule.RemoveAll(a => a.due <= now);
            foreach (var a in due) a.action();

            // idle -> standby rhino
            if (Interactive && !Sleeping && now - _lastActivity > IdleSeconds)
            {
                Sleeping = true;
                Mood = Mood.Standby;
                Step? s = Step;
                string psst = s == null ? "" :
                    s.Kind == StepKind.Point ? $"Psst: click the {StreetLessons.NameOf(s.Group).ToLowerInvariant()} ring." :
                    s.Kind == StepKind.Command ? $"Psst: type {s.Command} + Enter." : "Psst: press Enter.";
                Bubble = "Zzz... (no click for 5 s)  " + psst;
            }

            MissMarks.RemoveAll(m => now - m.t > 0.9);
            FindView()?.Redraw();
        }

        private void At(double delay, Action action) => _schedule.Add((Now + delay, action));

        private void Animate(double seconds, Action<double> apply, Action done)
        {
            double t0 = Now;
            Action? step = null;
            step = () =>
            {
                double k = Clamp((Now - t0) / seconds, 0, 1);
                apply(k);
                _doc.Views.Redraw();
                if (k < 1) At(0, step!); else done();
            };
            step();
        }

        private void Activity()
        {
            _lastActivity = Now;
            if (Sleeping)
            {
                Sleeping = false;
                Mood = Mood.Win;
                Bubble = "I'm up! I'm up!";
            }
        }

        private void Say(string text)
        {
            Bubble = text;
            if (!Sleeping && Phase != Phase.Lose) Mood = Mood.Win;
        }

        private void Caption(string title, string sub)
        {
            BigCaption = title;
            BigSub = sub;
        }

        private static void Log(string s) => RhinoApp.WriteLine("[RhinoOnTheStreet] " + s);

        // ================= view + camera =================

        private void CreateView()
        {
            var rect = new System.Drawing.Rectangle(120, 120, 1200, 760);
            try
            {
                Eto.Forms.Window? main = RhinoEtoApp.MainWindow;
                if (main != null)
                {
                    Eto.Drawing.Rectangle b = main.Bounds;
                    float scale = Rhino.Runtime.HostUtils.RunningOnWindows ? (main.Screen?.LogicalPixelSize ?? 1f) : 1f;
                    rect = new System.Drawing.Rectangle(
                        (int)((b.X + b.Width * 0.1) * scale), (int)((b.Y + b.Height * 0.14) * scale),
                        (int)(b.Width * 0.72 * scale), (int)(b.Height * 0.74 * scale));
                }
            }
            catch { /* fall back to the default rectangle */ }

            _view = _doc.Views.Add(ViewTitle, DefinedViewportProjection.Perspective, rect, true);
            if (_view == null)
            {
                _view = _doc.Views.ActiveView;
                RhinoApp.WriteLine("[RhinoOnTheStreet] Couldn't open a floating view; using the active view.");
            }
            if (_view == null) return;
            _viewportId = _view.ActiveViewportID;
            _view.ActiveViewport.Name = ViewTitle;
            _view.ActiveViewport.Camera35mmLensLength = 28;
            _fullSize = _view.Size;
            if (_view.Floating && _fullSize.Width > 0)
                _popStart = Now;
        }

        private RhinoView? FindView()
        {
            if (_view == null) return null;
            return _doc.Views.Find(_viewportId);
        }

        private void SetCamera(Point3d eye, Point3d tgt)
        {
            _camEye = eye;
            _camTgt = tgt;
            RhinoView? v = FindView();
            if (v == null) return;
            RhinoViewport vp = v.ActiveViewport;
            if (!vp.IsPerspectiveProjection)
                vp.ChangeToPerspectiveProjection(true, 28);
            vp.SetCameraLocations(tgt, eye);
        }

        private void CamTo((Point3d eye, Point3d tgt) to, double seconds, Action? done = null)
        {
            _fromEye = _camEye;
            _fromTgt = _camTgt;
            _toEye = to.eye;
            _toTgt = to.tgt;
            _tweenStart = Now;
            _tweenDur = seconds;
            _tweenDone = done;
        }

        // ================= document helpers =================

        private int EnsureLayer()
        {
            Layer? existing = _doc.Layers.FindName(ViewTitle);
            if (existing != null) return existing.Index;
            return _doc.Layers.Add(new Layer { Name = ViewTitle, Color = StreetLessons.Orange });
        }

        private void ShowTargets()
        {
            ClearTargets();
            if (Lesson == null) return;
            _adding = true;
            try
            {
                foreach (Point3d p in Lesson.Targets)
                {
                    // Locked point objects: Osnap Point can snap to them, but they can't be picked or moved.
                    var attrs = new ObjectAttributes { LayerIndex = _layerIndex, Mode = ObjectMode.Locked, Name = "RhinoOnTheStreet target" };
                    Guid id = _doc.Objects.AddPoint(p, attrs);
                    if (id != Guid.Empty) _targetIds.Add(id);
                }
            }
            finally { _adding = false; }
            _doc.Views.Redraw();
        }

        private void ClearTargets()
        {
            foreach (Guid id in _targetIds)
            {
                RhinoObject? o = _doc.Objects.FindId(id);
                if (o != null)
                {
                    _doc.Objects.Unlock(id, true);
                    _doc.Objects.Delete(id, true);
                }
            }
            _targetIds.Clear();
        }

        private void AddToModel(Brep? brep, string name)
        {
            if (brep == null) return;
            _adding = true;
            try
            {
                _doc.Objects.AddBrep(brep, new ObjectAttributes { LayerIndex = _layerIndex, Name = name });
            }
            finally { _adding = false; }
            _doc.Views.Redraw();
        }

        // ================= math =================

        public static double Clamp(double x, double a, double b) => x < a ? a : x > b ? b : x;
        public static double Ease(double t) => t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
        public static double Smooth(double a, double b, double x)
        {
            double t = Clamp((x - a) / (b - a), 0, 1);
            return t * t * (3 - 2 * t);
        }
    }

    /// <summary>Forwards mouse-down in any Rhino view to the tutorial.</summary>
    internal sealed class ClickWatcher : MouseCallback
    {
        public Action<MouseCallbackEventArgs>? Down;

        protected override void OnMouseDown(MouseCallbackEventArgs e) => Down?.Invoke(e);
    }
}
