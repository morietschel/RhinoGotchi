using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Rhino;
using Rhino.ApplicationSettings;
using Rhino.Commands;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoGotchi.Fin;

namespace GotchiFins
{
    /// <summary>One line of the check card: what is checked, whether it passes, and a value.</summary>
    public sealed class Check
    {
        public Check(string label, bool pass, string detail)
        {
            Label = label;
            Pass = pass;
            Detail = detail;
        }

        public string Label { get; }
        public bool Pass { get; }
        public string Detail { get; }
    }

    /// <summary>
    /// Game state shared by the panel, the toolbar and the viewport overlay: whether the game
    /// has started, how far the document has got, how the player is doing (misses, first
    /// tries, idle time) and therefore how the rhino pet feels. Levels are checked in order,
    /// so the current level is the first one whose result isn't in the document yet.
    /// While a level is played, locked guide points sit where the player should click, Point
    /// osnap is on, and in level 4 a path curve runs around the mass.
    /// </summary>
    public static class FinSession
    {
        public const string DisplayModeName = "GotchiFins Rendered";
        public const string AxonViewName = "Axon";
        public const string ToolbarFileName = "GotchiFins";
        public const string ToolbarGroupName = "RhinoGotchi";
        private const string GuideLayerName = "GotchiFins guides";

        /// <summary>How long a win, loss or final dance lasts, and how long before the pet dozes off.</summary>
        public const double MoodSeconds = 4.5, StandbySeconds = 5;

        /// <summary>Misses on one level before the rhino loses (and the level is no longer first try).</summary>
        public const int MissesToLose = 3;

        private const double OrbitDegrees = 40;

        private static FinGhostConduit? _ghost;
        private static ActivityWatcher? _activity;
        private static Eto.Forms.UITimer? _ticker;
        private static bool _dirty;

        private static readonly List<Guid> _guideIds = new List<Guid>();
        private static Stage? _guidesFor;

        private static bool? _savedOsnap;
        private static OsnapModes _savedOsnapModes;
        private static (bool Enabled, bool ShadowOnly, bool AutoAltitude, double Altitude)? _savedGround;
        private static bool _toolbarShown;

        // Level 5: the camera direction when the level began, how far the player has orbited
        private static Vector3d? _orbitStart;
        private static double _orbitDegrees;
        private static bool _lastSun;

        // How the player is doing
        private static readonly int[] _misses = new int[GamePage.LevelCount];
        private static readonly bool[] _lost = new bool[GamePage.LevelCount];
        private static readonly List<string> _recorded = new List<string>();
        private static bool _inCommand, _changedInCommand;

        // The pet's clocks: one for timed moods, one for idle time, one that just runs
        private static readonly System.Diagnostics.Stopwatch _moodClock = new System.Diagnostics.Stopwatch();
        private static readonly System.Diagnostics.Stopwatch _idleClock = System.Diagnostics.Stopwatch.StartNew();
        private static readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
        private static PetMood? _timedMood;
        private static PetMood _lastMood = PetMood.Awake;

        public static bool Started { get; private set; }

        /// <summary>Levels finished, 0..5. Level n is done when n &lt; Completed.</summary>
        public static int Completed { get; private set; }

        public static bool Complete => Completed >= GamePage.LevelCount;

        public static bool IsDone(Stage stage) => (int)stage < Completed;

        /// <summary>The level to play next, or null when the facade is complete.</summary>
        public static Stage? Current => Complete ? (Stage?)null : (Stage)Completed;

        /// <summary>The page the player should be on right now.</summary>
        public static int CurrentPageIndex => !Started ? GamePage.WelcomeIndex : 1 + Completed;

        /// <summary>A nudge when something is close but not right, e.g. a fin of the wrong thickness.</summary>
        public static string? Hint { get; private set; }

        /// <summary>What the check card shows for the current level.</summary>
        public static IReadOnlyList<Check> Checks { get; private set; } = new Check[0];

        /// <summary>Share of the current level's checks that pass, 0..1.</summary>
        public static double Match => Checks.Count == 0 ? 0 : Checks.Count(c => c.Pass) / (double)Checks.Count;

        /// <summary>Unsolved attempts on a level so far.</summary>
        public static int Misses(Stage stage) => _misses[(int)stage];

        /// <summary>Whether a level was solved without the rhino losing on it.</summary>
        public static bool FirstTry(Stage stage) => !_lost[(int)stage];

        public static int FirstTryCount => Enumerable.Range(0, GamePage.LevelCount).Count(i => !_lost[i]);

        public static bool Perfect => FirstTryCount == GamePage.LevelCount;

        /// <summary>The last few commands that changed the model, oldest first.</summary>
        public static IReadOnlyList<string> Recorded => _recorded;

        /// <summary>The player's profile curve and fin, used by the Go macros.</summary>
        public static Guid ProfileId { get; private set; }
        public static Guid FinId { get; private set; }

        /// <summary>The path curve for level 4, if it is in the document.</summary>
        public static Guid PathId { get; private set; }

        /// <summary>The level the last win or loss was about, for the pet's speech bubble.</summary>
        public static Stage? MoodStage { get; private set; }

        /// <summary>Raised on the UI thread when progress, hints or the pet's mood change.</summary>
        public static event Action? Changed;

        // ------------------------------------------------------------ the pet

        /// <summary>How the rhino feels right now.</summary>
        public static PetMood Mood
        {
            get
            {
                if (_timedMood.HasValue && _moodClock.Elapsed.TotalSeconds < MoodSeconds)
                    return _timedMood.Value;
                if (Started && Complete)
                    return PetMood.Dance; // the finale: the winning dance, forever
                if (Started && _idleClock.Elapsed.TotalSeconds > StandbySeconds)
                    return PetMood.Standby;
                return PetMood.Awake;
            }
        }

        /// <summary>Seconds into the current mood's animation.</summary>
        public static double MoodTime => _timedMood.HasValue && _moodClock.IsRunning ? _moodClock.Elapsed.TotalSeconds : _clock.Elapsed.TotalSeconds;

        /// <summary>What the rhino says, for the speech bubble.</summary>
        public static string Speech
        {
            get
            {
                Stage? current = Current;
                string? command = current.HasValue ? GamePage.For(current.Value).Command : null;
                switch (Mood)
                {
                    case PetMood.Win when MoodStage.HasValue:
                        string learned = GamePage.For(MoodStage.Value).Command ?? "";
                        return command != null ? $"Yes! You learned {learned}. Next up: {command}." : $"Yes! You learned {learned}.";
                    case PetMood.Win:
                        return "Yes!";
                    case PetMood.Dance:
                        return Perfect
                            ? "PERFECT RUN! Six commands, six first tries. Look at those fins!"
                            : $"You built it! {FirstTryCount} of {GamePage.LevelCount} first try. Play again for a perfect run?";
                    case PetMood.Final:
                        return "PERFECT RUN! Box, Polyline, ExtrudeCrv, ArrayCrv, Sun, ApplyBoxMapping.";
                    case PetMood.Lose:
                        return Hint ?? $"Oof, {MissesToLose} misses. Undo and try {command} again.";
                    case PetMood.Standby:
                        return command != null ? $"Zzz... type {command} to wake me up." : "Zzz...";
                    default:
                        if (!Started)
                            return "Press Start and I'll show you around.";
                        if (Complete)
                            return $"Done! {FirstTryCount} of {GamePage.LevelCount} first try. Go again for a perfect run?";
                        return command != null ? $"Try {command}. Type it up top." : "";
                }
            }
        }

        /// <summary>Marks the player as active: wakes the pet and resets the standby clock.</summary>
        public static void Touch()
        {
            bool wasAsleep = Mood == PetMood.Standby;
            _idleClock.Restart();
            if (wasAsleep)
                NotifyMood();
        }

        private static void SetMood(PetMood mood, Stage? about)
        {
            _timedMood = mood;
            MoodStage = about;
            _moodClock.Restart();
            NotifyMood();
        }

        private static void NotifyMood()
        {
            _lastMood = Mood;
            RhinoDoc.ActiveDoc?.Views.Redraw();
            Changed?.Invoke();
        }

        /// <summary>Animates the viewport pet and notices mood changes (dozing off, a dance ending).</summary>
        private static void OnTick(object? sender, EventArgs e)
        {
            if (!Started)
                return;
            PetMood mood = Mood;
            if (mood != _lastMood)
            {
                if (_timedMood.HasValue && _moodClock.Elapsed.TotalSeconds >= MoodSeconds)
                {
                    _timedMood = null;
                    _moodClock.Reset();
                }
                NotifyMood();
            }
            else if (mood != PetMood.Awake)
            {
                FindAxonView(RhinoDoc.ActiveDoc)?.Redraw();
            }
        }

        // ------------------------------------------------------------ lifetime

        public static void Initialize()
        {
            if (_ghost != null)
                return;
            _ghost = new FinGhostConduit();

            RhinoDoc.AddRhinoObject += (s, e) => OnModelChanged();
            RhinoDoc.DeleteRhinoObject += (s, e) => OnModelChanged();
            RhinoDoc.UndeleteRhinoObject += (s, e) => OnModelChanged();
            RhinoDoc.ReplaceRhinoObject += (s, e) => OnModelChanged();
            RhinoDoc.EndOpenDocument += (s, e) => _dirty = true;
            RhinoDoc.NewDocument += (s, e) => _dirty = true;
            RhinoDoc.CloseDocument += (s, e) => Stop();
            Command.BeginCommand += OnBeginCommand;
            Command.EndCommand += OnEndCommand;
            RhinoApp.KeyboardEvent += key => Touch();
            RhinoApp.Idle += OnIdle;

            try
            {
                _activity = new ActivityWatcher { Enabled = true };
                _ticker = new Eto.Forms.UITimer { Interval = 1.0 / 8 };
                _ticker.Elapsed += OnTick;
                _ticker.Start();
            }
            catch
            {
                // No UI (scripted runs): the game still works, the pet just doesn't doze off.
            }
        }

        /// <summary>Sets up the axon view, display, snapping and guides, turns the overlay on and checks progress.</summary>
        public static void Start(RhinoDoc doc)
        {
            if (!Started)
            {
                Array.Clear(_misses, 0, _misses.Length);
                Array.Clear(_lost, 0, _lost.Length);
                _recorded.Clear();
                _timedMood = null;
            }
            Started = true;
            if (_ghost != null)
                _ghost.Enabled = true;
            _idleClock.Restart();
            ShowToolbar();
            SetUpGround(doc);
            SetUpAxonView(doc);
            Refresh(doc, forceNotify: true);
        }

        public static void Stop()
        {
            if (_ghost != null)
                _ghost.Enabled = false;
            _guideIds.Clear(); // the document is going away; nothing to delete
            _guidesFor = null;
            PathId = Guid.Empty;
            _orbitStart = null;
            _orbitDegrees = 0;
            _timedMood = null;
            RestoreSnapping();
            RestoreGround(RhinoDoc.ActiveDoc);
            bool wasStarted = Started;
            Started = false;
            if (wasStarted)
                Changed?.Invoke();
        }

        // ------------------------------------------------------------ running commands

        /// <summary>
        /// Runs a command the game teaches, from the panel or the toolbar. If it is the current
        /// level's command it starts the way the level wants (profile or fin picked, direction
        /// set); otherwise it simply runs the command. It waits until Rhino is idle, so it can be
        /// called from inside another command (the toolbar's GotchiFinsRun).
        /// </summary>
        public static void RunCommand(string command)
        {
            Touch();
            RhinoDoc? doc = RhinoDoc.ActiveDoc;
            if (doc == null)
                return;
            if (!Started)
                Start(doc);

            GamePage? page = Current.HasValue ? GamePage.For(Current.Value) : null;
            bool levelCommand = page != null && string.Equals(page.Command, command, StringComparison.OrdinalIgnoreCase) && page.Macro != null;
            string macro = levelCommand
                ? page!.Macro!.Replace("{profile}", ProfileId.ToString()).Replace("{fin}", FinId.ToString())
                : "_" + command;

            // "{fins}": start the command with every fin selected (level 6)
            Guid[] select = new Guid[0];
            if (macro.Contains("{fins}"))
            {
                macro = macro.Replace("{fins}", "").Trim();
                select = FinIds.ToArray();
            }
            RunWhenIdle(macro, select);
        }

        /// <summary>Gives every fin a finish, from the panel's swatches.</summary>
        public static void ApplyFinish(Finish finish)
        {
            Touch();
            if (RhinoDoc.ActiveDoc is RhinoDoc doc && FinIds.Count > 0)
                Finishes.Apply(doc, finish, FinIds);
        }

        /// <summary>The fins standing around the mass (from the last check), for level 6.</summary>
        public static IReadOnlyList<Guid> FinIds { get; private set; } = new Guid[0];

        /// <summary>The finish most of the fins have, if any.</summary>
        public static Finish? ChosenFinish { get; private set; }

        /// <summary>Runs the Go macro of a level, with the player's profile or fin filled in.</summary>
        public static void RunLevel(RhinoDoc doc, GamePage page)
        {
            if (page.Command != null)
                RunCommand(page.Command);
        }

        /// <summary>Cancels whatever is running, selects <paramref name="select"/>, then runs the macro, once Rhino is idle.</summary>
        private static void RunWhenIdle(string script, Guid[] select)
        {
            void Once(object? s, EventArgs e)
            {
                RhinoApp.Idle -= Once;
                RhinoApp.RunScript("!", false);
                RhinoDoc? doc = RhinoDoc.ActiveDoc;
                if (doc != null)
                {
                    doc.Objects.UnselectAll();
                    if (select.Length > 0)
                        doc.Objects.Select(select, true);
                }
                RhinoApp.RunScript(script, true);
            }
            RhinoApp.Idle += Once;
        }

        public static void ZoomToAll(RhinoDoc doc) => ZoomTo(doc, FinModel.Bounds, 3);

        private static void ZoomTo(RhinoDoc doc, BoundingBox box, double margin)
        {
            RhinoView? view = FindAxonView(doc) ?? doc.Views.ActiveView;
            if (view == null)
                return;
            box.Inflate(margin);
            view.ActiveViewport.ZoomBoundingBox(box);
            view.Redraw();
        }

        // ------------------------------------------------------------ watching the player

        private static bool IsOurs(string command) =>
            command.StartsWith("GotchiFins", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("SelID", StringComparison.OrdinalIgnoreCase);

        private static void OnModelChanged()
        {
            _dirty = true;
            if (_inCommand)
                _changedInCommand = true;
        }

        private static void OnBeginCommand(object? sender, CommandEventArgs e)
        {
            Touch();
            if (IsOurs(e.CommandEnglishName))
                return;
            _inCommand = true;
            _changedInCommand = false;
        }

        /// <summary>
        /// After a command: check progress straight away, record it if it changed the model, and
        /// count a miss when it changed the model but didn't solve the level.
        /// </summary>
        private static void OnEndCommand(object? sender, CommandEventArgs e)
        {
            Touch();
            string name = e.CommandEnglishName;
            bool changed = _inCommand && _changedInCommand;
            _inCommand = false;
            _changedInCommand = false;
            if (e.Document == null || !Started || IsOurs(name))
                return;

            Stage? level = Current;
            int before = Completed;
            if (changed)
            {
                _recorded.Add(name);
                if (_recorded.Count > 6)
                    _recorded.RemoveAt(0);
            }
            Refresh(e.Document, forceNotify: false);

            bool counts = changed && e.CommandResult == Result.Success && level.HasValue && level != Stage.Sun &&
                          Completed <= before && !name.Equals("Undo", StringComparison.OrdinalIgnoreCase) &&
                          !name.Equals("Redo", StringComparison.OrdinalIgnoreCase) && !name.Equals("Delete", StringComparison.OrdinalIgnoreCase);
            if (counts)
            {
                int i = (int)level!.Value;
                _misses[i]++;
                if (_misses[i] % MissesToLose == 0)
                {
                    _lost[i] = true;
                    SetMood(PetMood.Lose, level);
                }
                else
                {
                    Changed?.Invoke();
                }
            }
        }

        /// <summary>
        /// Called by the overlay on every redraw of the Axon view, so level 5 notices how far the
        /// player has orbited from where the level began.
        /// </summary>
        internal static void NoteCamera(RhinoViewport viewport)
        {
            if (!Started || Current != Stage.Sun || viewport.Name != AxonViewName)
                return;
            Vector3d now = FinModel.Normalized(viewport.CameraDirection);
            if (_orbitStart == null)
            {
                _orbitStart = now;
                return;
            }
            double cos = Math.Max(-1, Math.Min(1, now * _orbitStart.Value));
            double degrees = Math.Acos(cos) * 180 / Math.PI;
            if (degrees > _orbitDegrees + 1)
            {
                bool crossed = _orbitDegrees < OrbitDegrees && degrees >= OrbitDegrees;
                _orbitDegrees = degrees;
                _dirty = true; // refresh on the next idle, outside the draw
                if (crossed)
                    Touch();
            }
        }

        private static void OnIdle(object? sender, EventArgs e)
        {
            // Turning the Sun on doesn't touch any object, so level 5 watches it here.
            if (Started && Current == Stage.Sun && RhinoDoc.ActiveDoc is RhinoDoc active && SunOn(active) != _lastSun)
                _dirty = true;
            if (!_dirty)
                return;
            _dirty = false;
            RhinoDoc? doc = RhinoDoc.ActiveDoc;
            if (doc != null)
                Refresh(doc, forceNotify: false);
        }

        // ------------------------------------------------------------ progress

        private static void Refresh(RhinoDoc doc, bool forceNotify)
        {
            bool wasComplete = Complete;
            Stage? before = Current;
            string? hintBefore = Hint;
            double matchBefore = Match;

            Scan scan = Scan.Of(doc);
            ProfileId = scan.ProfileId;
            FinId = scan.FinId;
            FinIds = scan.FinIds;
            ChosenFinish = scan.MainFinish;

            // Later results imply earlier ones: a finished fin means the profile was drawn,
            // even if the curve has since been deleted. The mass is always needed though.
            bool array = scan.ArrayDone;
            bool fin = array || scan.FinId != Guid.Empty;
            bool profile = fin || scan.ProfileId != Guid.Empty;
            bool sun = SunOn(doc);
            _lastSun = sun;
            bool orbited = _orbitDegrees >= OrbitDegrees;
            int completed = !scan.MassFound ? 0 : !profile ? 1 : !fin ? 2 : !array ? 3 : !(sun && orbited) ? 4 : !scan.Dressed ? 5 : 6;
            bool changed = completed != Completed;
            int previous = Completed;
            Completed = completed;
            Hint = !Started ? null : Current == Stage.Sun ? SunHint(sun, orbited) : scan.HintFor(Current);
            Checks = !Current.HasValue ? new Check[0]
                     : Current == Stage.Sun ? SunChecks(sun)
                     : scan.ChecksFor(Current.Value);

            // A level was just solved: the rhino celebrates (the final dance if it was all first try)
            if (Started && !forceNotify && completed > previous)
            {
                var solved = (Stage)(completed - 1);
                SetMood(completed == GamePage.LevelCount && Perfect ? PetMood.Final : PetMood.Win, solved);
            }

            // Entering level 5: switch the Axon view to perspective (right-drag then orbits)
            if (Started && Current == Stage.Sun && before != Stage.Sun)
            {
                _orbitStart = null;
                _orbitDegrees = 0;
                FindAxonView(doc)?.ActiveViewport.ChangeToPerspectiveProjection(true, 35);
            }

            // Entering level 6: put the three finishes in the document (they then also show in Properties → Material)
            if (Started && Current == Stage.Material && before != Stage.Material)
                Finishes.Ensure(doc);

            if (Started)
            {
                if (Complete)
                    RestoreSnapping();
                else
                    EnableSnapping();
            }
            bool guidesChanged = UpdateGuides(doc);
            _dirty = false; // our own guide edits don't need another pass

            if (Started && Current != before && Current.HasValue)
                ZoomForLevel(doc, Current.Value);
            if (Started && !wasComplete && Complete)
            {
                RhinoApp.WriteLine($"[GotchiFins] Splendid! The facade is finished, {FirstTryCount} of {GamePage.LevelCount} first try.");
                ZoomToAll(doc);
            }

            if (changed || guidesChanged || forceNotify || Hint != hintBefore || Math.Abs(Match - matchBefore) > 1e-9)
            {
                doc.Views.Redraw();
                Changed?.Invoke();
            }
        }

        private static bool SunOn(RhinoDoc doc)
        {
            try
            {
                return doc.Lights.Sun.Enabled;
            }
            catch
            {
                return false;
            }
        }

        private static string? SunHint(bool sun, bool orbited) =>
            sun && !orbited ? "The sun is on. Now right-drag to orbit around the fins."
            : !sun && orbited ? "Nice orbit! Now tick On in the Sun panel."
            : null;

        private static Check[] SunChecks(bool sun) => new[]
        {
            new Check("Sun", sun, sun ? "on" : "off"),
            new Check("Orbit", _orbitDegrees >= OrbitDegrees, $"{_orbitDegrees:0}° / {OrbitDegrees:0}°"),
        };

        /// <summary>The profile and fin are small, so zoom in on them; the rest show everything.</summary>
        private static void ZoomForLevel(RhinoDoc doc, Stage stage)
        {
            if (stage == Stage.Profile || stage == Stage.Fin)
                ZoomTo(doc, FinModel.FinCloseUp, 2);
            else
                ZoomToAll(doc);
        }

        // ------------------------------------------------------------ checking the document

        /// <summary>What the document contains, as far as the levels are concerned.</summary>
        private sealed class Scan
        {
            public bool MassFound;
            public Guid ProfileId;
            public Guid FinId;
            public bool ArrayDone;

            /// <summary>Thickness of a fin that has the right outline but the wrong thickness.</summary>
            public double? WrongThickness;
            public bool ProfileOpen;
            public int[] FinsPerSide = new int[4]; // front, right, back, left
            public int FinsAround;

            // Level 6: the fins, their finishes and texture boxes
            public List<Guid> FinIds = new List<Guid>();
            private readonly int[] _finishCount = new int[3];
            private int _mapped, _mappedRight;
            private Vector3d? _mappingSize;

            private const double SizeTolerance = 0.25;
            private bool Most(int n) => FinsAround > 0 && n >= Math.Ceiling(FinsAround * 0.9);

            /// <summary>The finish most fins have.</summary>
            public Finish? MainFinish
            {
                get
                {
                    int best = Array.IndexOf(_finishCount, _finishCount.Max());
                    return _finishCount[best] > 0 ? (Finish)best : (Finish?)null;
                }
            }

            /// <summary>Level 6 is solved: (almost) every fin has a finish and a 2 × 2 × 2 texture box.</summary>
            public bool Dressed => Most(_finishCount.Sum()) && Most(_mappedRight);

            private static string Size(Vector3d v) =>
                $"{v.X.ToString("0.0", CultureInfo.InvariantCulture)} × {v.Y.ToString("0.0", CultureInfo.InvariantCulture)} × {v.Z.ToString("0.0", CultureInfo.InvariantCulture)}";

            // The closest attempt at each level, for the check card
            private double _massError = double.MaxValue, _profileError = double.MaxValue;
            private bool _massSolid, _massVolume, _profileClosed, _profileArea, _finSolid;
            private double _profileAreaValue, _finThickness = -1;

            private static string F(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

            public string? HintFor(Stage? stage)
            {
                switch (stage)
                {
                    case Stage.Profile when ProfileOpen:
                        return "Almost: close the polyline by clicking point 1 again.";
                    case Stage.Fin when WrongThickness.HasValue:
                        return "That fin is " + WrongThickness.Value.ToString("0.0#", CultureInfo.InvariantCulture) + " thick. Undo, and type " + FinModel.ThicknessText + ".";
                    case Stage.Array when FinsAround > 1:
                        int sides = FinsPerSide.Count(n => n >= 3);
                        return sides < 4
                            ? $"{FinsAround} fins, but only on {sides} of 4 sides. Set Orientation to Roadlike."
                            : $"{FinsAround} fins so far. Set Number of items to {FinModel.FinCount}.";
                    case Stage.Material when Most(_finishCount.Sum()) && _mapped > 0 && _mappedRight < _mapped && _mappingSize.HasValue:
                        return $"Your texture box is {Size(_mappingSize.Value)}. Undo, and make it 2 × 2 × 2.";
                    case Stage.Material when !Most(_finishCount.Sum()) && _mapped > 0:
                        return "Nice mapping! Now pick a finish for the fins.";
                    default:
                        return null;
                }
            }

            public Check[] ChecksFor(Stage stage)
            {
                switch (stage)
                {
                    case Stage.Mass:
                    {
                        bool any = _massError < double.MaxValue;
                        return new[]
                        {
                            new Check("Solid box", any && _massSolid, any ? (_massSolid ? "closed" : "open") : "none yet"),
                            new Check("Corners", any && _massError <= 0.1, any ? "Δ " + F(_massError) : "—"),
                            new Check("Size", any && _massVolume, "24 × 10 × 8"),
                        };
                    }
                    case Stage.Profile:
                    {
                        bool any = _profileError < double.MaxValue;
                        return new[]
                        {
                            new Check("On the points", any && _profileError <= 0.1, any ? "Δ " + F(_profileError) : "none yet"),
                            new Check("Closed", any && _profileClosed, any ? (_profileClosed ? "yes" : "open") : "—"),
                            new Check("Area", any && _profileArea, any && _profileAreaValue > 0 ? F(_profileAreaValue) + " / " + F(FinModel.ProfileArea) : "—"),
                        };
                    }
                    case Stage.Fin:
                    {
                        bool any = _finThickness >= 0;
                        bool thick = any && Math.Abs(_finThickness - FinModel.Thickness) <= 0.05;
                        return new[]
                        {
                            new Check("Outline", any, any ? "matches profile" : "none yet"),
                            new Check("Solid", any && _finSolid, any ? (_finSolid ? "closed" : "open") : "—"),
                            new Check("Thickness", thick, any ? F(_finThickness) + (thick ? "" : " → " + F(FinModel.Thickness)) : "→ " + F(FinModel.Thickness)),
                        };
                    }
                    case Stage.Array:
                    {
                        int sides = FinsPerSide.Count(n => n >= 3);
                        return new[]
                        {
                            new Check("Fins", FinsAround >= FinModel.FinCount * 0.6, $"{FinsAround} / {FinModel.FinCount}"),
                            new Check("Around the corners", sides == 4, $"{sides} / 4 sides"),
                        };
                    }
                    case Stage.Material:
                    {
                        int finished = _finishCount.Sum();
                        Finish? main = MainFinish;
                        return new[]
                        {
                            new Check("Finish", Most(finished), main.HasValue ? $"{Finishes.Label(main.Value)} · {finished}/{FinsAround}" : "none yet"),
                            new Check("Box mapping", Most(_mapped), $"{_mapped} / {FinsAround}"),
                            new Check("Texture size", Most(_mappedRight), _mappingSize.HasValue ? Size(_mappingSize.Value) : "→ 2 × 2 × 2"),
                        };
                    }
                    default:
                        return new Check[0];
                }
            }

            public static Scan Of(RhinoDoc doc)
            {
                var scan = new Scan();
                double tolerance = Math.Max(0.1, doc.ModelAbsoluteTolerance * 10);

                var settings = new ObjectEnumeratorSettings
                {
                    NormalObjects = true,
                    LockedObjects = true,
                    HiddenObjects = false,
                    DeletedObjects = false,
                    ObjectTypeFilter = ObjectType.Brep | ObjectType.Extrusion | ObjectType.Curve,
                };

                foreach (RhinoObject obj in doc.Objects.GetObjectList(settings))
                {
                    if (obj.Geometry is Curve curve)
                    {
                        if (!_guideIds.Contains(obj.Id) && obj.Id != PathId)
                            scan.CheckProfile(obj.Id, curve, tolerance);
                        continue;
                    }

                    Brep? brep = obj.Geometry switch
                    {
                        Brep b => b,
                        Extrusion x => x.ToBrep(),
                        _ => null,
                    };
                    if (brep == null)
                        continue;

                    BoundingBox box = brep.GetBoundingBox(true);
                    if (scan.CheckMass(brep, box, tolerance))
                        continue;
                    scan.CheckFin(obj, brep, box, tolerance);
                }

                int sides = scan.FinsPerSide.Count(n => n >= 3);
                scan.ArrayDone = sides == 4 && scan.FinsAround >= FinModel.FinCount * 0.6;
                return scan;
            }

            private static double BoxError(BoundingBox a, BoundingBox b) =>
                Math.Max(a.Min.DistanceTo(b.Min), a.Max.DistanceTo(b.Max));

            /// <summary>Is this the mass? Also remembers the closest box-like attempt for the check card.</summary>
            private bool CheckMass(Brep brep, BoundingBox box, double tolerance)
            {
                // Only things roughly the size of the mass count as attempts at it
                if (box.Diagonal.X < FinModel.Width * 0.3 || box.Diagonal.Y < FinModel.Depth * 0.3)
                    return false;
                double error = BoxError(box, FinModel.MassBounds);
                bool solid = brep.IsSolid;
                bool volume = solid && Math.Abs(Math.Abs(brep.GetVolume()) - FinModel.MassVolume) <= 0.02 * FinModel.MassVolume;
                if (error < _massError)
                {
                    _massError = error;
                    _massSolid = solid;
                    _massVolume = volume;
                }
                if (!MassFound && error <= tolerance && solid && volume)
                {
                    MassFound = true;
                    return true;
                }
                return error <= tolerance;
            }

            /// <summary>A closed planar curve with the profile's extents and area.</summary>
            private void CheckProfile(Guid id, Curve curve, double tolerance)
            {
                BoundingBox box = curve.GetBoundingBox(true);
                double error = BoxError(box, FinModel.ProfileBounds);
                if (error > 3)
                    return; // nowhere near the profile
                bool closed = curve.IsClosed;
                double area = 0;
                if (closed && curve.IsPlanar(tolerance))
                    area = AreaMassProperties.Compute(curve)?.Area ?? 0;
                bool areaOk = Math.Abs(area - FinModel.ProfileArea) <= 0.05 * FinModel.ProfileArea;

                if (ProfileId == Guid.Empty && error < _profileError + 1e-9)
                {
                    _profileError = error;
                    _profileClosed = closed;
                    _profileArea = areaOk;
                    _profileAreaValue = area;
                }
                if (error > tolerance)
                    return;
                if (!closed)
                {
                    ProfileOpen = true;
                    return;
                }
                if (areaOk && ProfileId == Guid.Empty)
                {
                    ProfileId = id;
                    _profileError = error;
                    _profileClosed = true;
                    _profileArea = true;
                    _profileAreaValue = area;
                }
            }

            /// <summary>
            /// Sorts a solid into: the fin in place (extruded either way from the profile), a fin
            /// of the wrong thickness, or an arrayed fin on one of the four sides of the mass.
            /// </summary>
            private void CheckFin(RhinoObject obj, Brep brep, BoundingBox box, double tolerance)
            {
                Vector3d size = box.Diagonal;
                if (Math.Abs(size.Z - FinModel.Height) > tolerance)
                    return;

                // The fin where it was made: same y/z outline as the profile, thin in x, touching x = 0.
                bool outlineInPlace =
                    Math.Abs(box.Min.Y - FinModel.ProfileBounds.Min.Y) <= tolerance &&
                    Math.Abs(box.Max.Y - FinModel.ProfileBounds.Max.Y) <= tolerance &&
                    (Math.Abs(box.Min.X) <= tolerance || Math.Abs(box.Max.X) <= tolerance);
                bool solid = brep.IsSolid;
                double volume = solid ? Math.Abs(brep.GetVolume()) : 0;
                bool finVolume = solid && Math.Abs(volume - FinModel.FinVolume) <= 0.04 * FinModel.FinVolume;

                if (outlineInPlace && FinId == Guid.Empty && size.X > 0.01)
                {
                    bool right = finVolume && Math.Abs(size.X - FinModel.Thickness) <= 0.05;
                    if (right || _finThickness < 0)
                    {
                        _finThickness = size.X;
                        _finSolid = solid;
                    }
                    if (right)
                        FinId = obj.Id;
                    else
                        WrongThickness = size.X;
                }

                if (!finVolume)
                    return;

                // Which side of the mass is it on? Fins stand outside the mass.
                Point3d c = box.Center;
                int side = c.Y < 0 ? 0 : c.X > FinModel.Width ? 1 : c.Y > FinModel.Depth ? 2 : c.X < 0 ? 3 : -1;
                if (side < 0)
                    return; // inside the mass: arrayed without turning
                FinsPerSide[side]++;
                FinsAround++;
                FinIds.Add(obj.Id);
                CheckDressing(obj);
            }

            /// <summary>Level 6: which finish the fin has, and the size of its texture box, if it has one.</summary>
            private void CheckDressing(RhinoObject obj)
            {
                try
                {
                    Finish? finish = Finishes.FromName(obj.GetRenderMaterial(true)?.Name);
                    if (finish.HasValue)
                        _finishCount[(int)finish.Value]++;

                    Rhino.Render.TextureMapping? mapping = obj.GetTextureMapping(1);
                    if (mapping != null && mapping.TryGetMappingBox(out Plane _, out Interval dx, out Interval dy, out Interval dz))
                    {
                        var v = new Vector3d(Math.Abs(dx.Length), Math.Abs(dy.Length), Math.Abs(dz.Length));
                        _mapped++;
                        _mappingSize ??= v;
                        bool right = Math.Abs(v.X - Finishes.MappingSize) <= SizeTolerance &&
                                     Math.Abs(v.Y - Finishes.MappingSize) <= SizeTolerance &&
                                     Math.Abs(v.Z - Finishes.MappingSize) <= SizeTolerance;
                        if (right)
                            _mappedRight++;
                        else
                            _mappingSize = v; // show the wrong size, it's the one to fix
                    }
                }
                catch
                {
                    // An object without render info just doesn't count as dressed
                }
            }
        }

        // ------------------------------------------------------------ guide points and path

        /// <summary>
        /// Keeps one locked point object at each click location of the level being played, and
        /// in level 4 a selectable path curve around the mass. Osnaps snap to locked objects, so
        /// a click near a yellow point lands on it exactly. Returns true when the guides changed.
        /// </summary>
        private static bool UpdateGuides(RhinoDoc doc)
        {
            Stage? want = Started ? Current : null;
            bool intact = _guideIds.All(id => doc.Objects.FindId(id) != null) &&
                          (want != Stage.Array || doc.Objects.FindId(PathId) != null);
            if (want == _guidesFor && intact)
                return false;

            RemoveGuides(doc);
            _guidesFor = want;
            if (want == null)
            {
                int layerIndex = doc.Layers.FindName(GuideLayerName)?.Index ?? -1;
                if (layerIndex >= 0)
                    doc.Layers.Delete(layerIndex, true);
                return true;
            }

            int layer = GuideLayer(doc);
            var locked = new ObjectAttributes { LayerIndex = layer, Mode = ObjectMode.Locked, Name = "GotchiFins guide" };
            foreach (Click click in GamePage.For(want.Value).Clicks)
            {
                Guid id = doc.Objects.AddPoint(click.At, locked);
                if (id != Guid.Empty)
                    _guideIds.Add(id);
            }

            if (want == Stage.Array)
            {
                // The player picks this curve in ArrayCrv, so it is a normal, selectable object.
                var pathAttributes = new ObjectAttributes
                {
                    LayerIndex = layer,
                    Name = "GotchiFins path",
                    ColorSource = ObjectColorSource.ColorFromObject,
                    ObjectColor = Palette.RhinoAccent,
                };
                PathId = doc.Objects.AddPolyline(FinModel.PathPolyline, pathAttributes);
            }
            return true;
        }

        private static void RemoveGuides(RhinoDoc doc)
        {
            foreach (Guid id in _guideIds)
            {
                if (doc.Objects.FindId(id) == null)
                    continue;
                doc.Objects.Unlock(id, true);
                doc.Objects.Delete(id, true);
            }
            _guideIds.Clear();

            if (PathId != Guid.Empty && doc.Objects.FindId(PathId) != null)
                doc.Objects.Delete(PathId, true);
            PathId = Guid.Empty;
        }

        private static int GuideLayer(RhinoDoc doc)
        {
            Layer? layer = doc.Layers.FindName(GuideLayerName);
            if (layer != null)
                return layer.Index;
            return doc.Layers.Add(new Layer { Name = GuideLayerName, Color = Palette.RhinoGrey });
        }

        // ------------------------------------------------------------ snapping

        /// <summary>Turns osnap on with Point added; remembers the player's settings once.</summary>
        private static void EnableSnapping()
        {
            if (_savedOsnap == null)
            {
                _savedOsnap = ModelAidSettings.Osnap;
                _savedOsnapModes = ModelAidSettings.OsnapModes;
            }
            ModelAidSettings.Osnap = true;
            ModelAidSettings.OsnapModes = _savedOsnapModes | OsnapModes.Point;
        }

        private static void RestoreSnapping()
        {
            if (_savedOsnap == null)
                return;
            ModelAidSettings.Osnap = _savedOsnap.Value;
            ModelAidSettings.OsnapModes = _savedOsnapModes;
            _savedOsnap = null;
        }

        // ------------------------------------------------------------ toolbar

        /// <summary>Shows the RhinoGotchi toolbar that Rhino loaded from GotchiFins.rui, once per session.</summary>
        private static void ShowToolbar()
        {
            if (_toolbarShown)
                return;
            _toolbarShown = true;
            try
            {
                Rhino.UI.ToolbarFile? file = RhinoApp.ToolbarFiles.FindByName(ToolbarFileName, true);
                if (file == null)
                {
                    string? folder = System.IO.Path.GetDirectoryName(typeof(FinSession).Assembly.Location);
                    string path = System.IO.Path.Combine(folder ?? "", ToolbarFileName + ".rui");
                    if (System.IO.File.Exists(path))
                        file = RhinoApp.ToolbarFiles.Open(path);
                }
                Rhino.UI.ToolbarGroup? group = file?.GetGroup(ToolbarGroupName);
                if (group != null)
                    group.Visible = true;
            }
            catch
            {
                // The panel has the same buttons
            }
        }

        // ------------------------------------------------------------ ground and view

        /// <summary>A white, shadow-only ground at z = 0, so the sun's shadows show in level 5.</summary>
        private static void SetUpGround(RhinoDoc doc)
        {
            try
            {
                Rhino.Render.RenderSettings settings = doc.RenderSettings;
                Rhino.Render.GroundPlane ground = settings.GroundPlane;
                _savedGround ??= (ground.Enabled, ground.ShadowOnly, ground.AutoAltitude, ground.Altitude);
                ground.Enabled = true;
                ground.ShadowOnly = true;
                ground.AutoAltitude = false;
                ground.Altitude = 0;
                doc.RenderSettings = settings;
            }
            catch
            {
                // No ground plane: shadows still fall on the mass
            }
        }

        private static void RestoreGround(RhinoDoc? doc)
        {
            if (doc == null || _savedGround == null)
                return;
            try
            {
                Rhino.Render.RenderSettings settings = doc.RenderSettings;
                Rhino.Render.GroundPlane ground = settings.GroundPlane;
                var saved = _savedGround.Value;
                ground.Enabled = saved.Enabled;
                ground.ShadowOnly = saved.ShadowOnly;
                ground.AutoAltitude = saved.AutoAltitude;
                ground.Altitude = saved.Altitude;
                doc.RenderSettings = settings;
            }
            catch
            {
                // Leave it
            }
            _savedGround = null;
        }

        private static RhinoView? FindAxonView(RhinoDoc? doc) =>
            doc?.Views.Find(AxonViewName, false);

        /// <summary>
        /// Turns the Perspective viewport into a parallel (axonometric) view from the
        /// south-east, names it Axon, and gives it the GotchiFins Rendered display mode.
        /// </summary>
        private static void SetUpAxonView(RhinoDoc doc)
        {
            RhinoView? view = FindAxonView(doc) ?? doc.Views.Find("Perspective", false) ?? doc.Views.ActiveView;
            if (view == null)
                return;

            RhinoViewport vp = view.ActiveViewport;
            if (Current == Stage.Sun)
                vp.ChangeToPerspectiveProjection(true, 35);
            else
                vp.ChangeToParallelProjection(true);
            Point3d target = FinModel.Bounds.Center;
            vp.SetCameraLocations(target, target - FinModel.ViewDirection * 100);
            vp.CameraUp = Vector3d.ZAxis;
            vp.Name = AxonViewName;

            DisplayModeDescription? mode = EnsureDisplayMode();
            if (mode != null)
                vp.DisplayMode = mode;

            doc.Views.ActiveView = view;
            if (Current.HasValue)
                ZoomForLevel(doc, Current.Value);
            else
                ZoomToAll(doc);
        }

        /// <summary>
        /// A copy of Rhino's Rendered mode made to look like a drawing: white background, black
        /// edges, no grid, real lighting and shadows. With the document's shadow-only ground,
        /// the sun's shadows fall on white.
        /// </summary>
        private static DisplayModeDescription? EnsureDisplayMode()
        {
            DisplayModeDescription? mode = DisplayModeDescription.FindByName(DisplayModeName);
            if (mode == null)
            {
                DisplayModeDescription? source = DisplayModeDescription.FindByName("Rendered")
                                                 ?? DisplayModeDescription.FindByName("Shaded");
                if (source == null)
                    return null;
                Guid id = DisplayModeDescription.CopyDisplayMode(source.Id, DisplayModeName);
                mode = DisplayModeDescription.GetDisplayMode(id);
                if (mode == null)
                    return null;
            }

            DisplayPipelineAttributes a = mode.DisplayAttributes;
            a.FillMode = DisplayPipelineAttributes.FrameBufferFillMode.SolidColor;
            a.SetFill(Palette.RhinoPaper);
            a.ShowIsoCurves = false;
            a.ShowSurfaceEdges = true;
            a.ShowTangentEdges = false;
            a.SurfaceEdgeThickness = 2;
            a.UseSingleCurveColor = false; // keep the yellow path yellow
            a.CurveThickness = 2;
            a.CastShadows = true;
            a.ShadowColor = Palette.RhinoShadow;
            a.ViewSpecificAttributes.DrawGrid = false;
            a.ViewSpecificAttributes.DrawGridAxes = false;
            a.ViewSpecificAttributes.DrawWorldAxes = false;

            // Newer Rhino 8 settings, set by name so the plugin still loads on the first Rhino 8 release
            SetByName(a, "LightingScheme", "SceneLighting");
            SetByName(a, "SurfaceEdgeColorUsage", "SingleColorForAll");
            SetByName(a, "SurfaceEdgeColor", Palette.RhinoInk);
            SetByName(a, "SurfaceNakedEdgeColor", Palette.RhinoInk);
            SetByName(a, "GroundPlaneUsage", "ByDocument");
            SetByName(a, "ShadowsOn", true);

            DisplayModeDescription.UpdateDisplayMode(mode);
            return mode;
        }

        /// <summary>Sets a display attribute that may not exist in older Rhino 8 builds. Enum values are given by name.</summary>
        private static void SetByName(DisplayPipelineAttributes attributes, string property, object value)
        {
            try
            {
                var p = typeof(DisplayPipelineAttributes).GetProperty(property);
                if (p == null || !p.CanWrite)
                    return;
                object v = p.PropertyType.IsEnum && value is string name ? Enum.Parse(p.PropertyType, name) : value;
                p.SetValue(attributes, v);
            }
            catch
            {
                // Not available in this Rhino: keep the copied value
            }
        }

        // ------------------------------------------------------------ activity

        /// <summary>Clicks in any viewport count as activity, so the pet wakes up.</summary>
        private sealed class ActivityWatcher : Rhino.UI.MouseCallback
        {
            protected override void OnMouseDown(Rhino.UI.MouseCallbackEventArgs e) => Touch();
        }
    }
}
