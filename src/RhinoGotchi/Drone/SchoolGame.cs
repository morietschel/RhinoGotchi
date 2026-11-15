using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using Rhino;
using Rhino.ApplicationSettings;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using Rhino.UI;
using Point = System.Drawing.Point;

namespace RhinoGotchi.Drone
{
    internal enum ShotResult { Popped, NotLocked, Miss }

    /// <summary>
    /// Runs one Drone School lesson in a perspective viewport. The mouse move is the point: the game
    /// reads what the camera did (zoom, pan, orbit) or how your drone moved (Gumball, nudge), shows a
    /// live mouse picture in the corner of the view, and only lets a drone be popped once you've done
    /// the move. Nothing is added to the document except your own drone on the flying lessons.
    /// </summary>
    internal sealed class SchoolGame : IDisposable
    {
        private readonly SchoolConduit _conduit;
        private readonly Input _input;
        private readonly Eto.Forms.UITimer _timer = new Eto.Forms.UITimer { Interval = 1.0 / 30 };
        private readonly Stopwatch _clock = new Stopwatch();
        private (Point3d Location, Point3d Target, Vector3d Direction) _camera;
        private Vector3d _startDirection;
        private bool _disposed;

        // Flying lessons.
        private bool _reverting;
        private Point3d _lastGood;
        private string? _pendingCrash;
        private bool _idleHooked;

        public SchoolGame(RhinoDoc doc, SchoolLevel level)
        {
            Doc = doc;
            Level = level;
            Origin = PlaceBesideModel(doc);
            View = FindPerspective(doc);
            Drones = level.Targets.Select(t => new LiveDrone(t)).ToList();

            if (level.Move == Move.Nudge)
                BuildMaze();
            else if (level.Course != null)
                Course = level.Course;

            _conduit = new SchoolConduit(this);
            _input = new Input(this);
            if (View != null)
            {
                doc.Views.ActiveView = View;
                var vp = View.ActiveViewport;
                vp.PushViewProjection();
                var target = L(MazeCameraTarget ?? level.CameraTarget);
                vp.SetCameraLocations(target, target + (MazeCameraOffset ?? level.CameraOffset));
                _camera = (vp.CameraLocation, vp.CameraTarget, vp.CameraDirection);
                _startDirection = vp.CameraDirection;
            }
            if (Course != null)
                AddFlyer();

            Update();
            _conduit.Enabled = true;
            _input.Enabled = true;
            RhinoDoc.ReplaceRhinoObject += OnReplace;
            _timer.Elapsed += (s, e) => Tick();
            _clock.Start();
            _timer.Start();
            View?.Redraw();
        }

        public RhinoDoc Doc { get; }
        public SchoolLevel Level { get; }
        public RhinoView? View { get; }
        public RhinoViewport? Viewport => View?.ActiveViewport;
        public Point3d Origin { get; }
        public List<LiveDrone> Drones { get; }
        public SchoolCourse? Course { get; private set; }
        public List<BoundingBox> MazeWalls { get; } = new List<BoundingBox>();
        public List<Explosion> Pops { get; } = new List<Explosion>();

        /// <summary>Your own drone (flying lessons).</summary>
        public Guid FlyerId { get; private set; }

        public int Shots { get; private set; }
        public int Crashes { get; private set; }
        public int GoodMoves { get; private set; }

        /// <summary>The camera or drone move just seen, and whether it was the one the lesson asks for.</summary>
        public Move? LastMove { get; private set; }
        public bool LastMoveRight { get; private set; }
        public double LastMoveTime { get; private set; } = -10;
        public string LastMoveText { get; private set; } = "";

        /// <summary>Whether the lesson's move has been done correctly at least once.</summary>
        public bool MoveDone { get; private set; }

        public bool Finished { get; private set; }

        /// <summary>Lessons with a clock: true once it runs out before the drones are all popped.</summary>
        public bool TimeUp { get; private set; }

        public double TimeLeft => Level.TimeLimit is double limit ? Math.Max(0, limit - Seconds) : double.PositiveInfinity;

        /// <summary>Mouse state for the live picture.</summary>
        public bool LeftDown { get; set; }
        public bool RightDown { get; set; }
        public bool ShiftDown { get; set; }
        public Point? Aim { get; set; }

        public double Seconds => FrozenTime ?? _clock.Elapsed.TotalSeconds;

        /// <summary>When set, the clock reads this (tests and pictures).</summary>
        public double? FrozenTime { get; set; }

        /// <summary>Raised when something the panel shows changes: a move, a lock-on, a pop, a crash, the finish.</summary>
        public event Action? Changed;

        public Point3d L(Point3d local) => local + (Origin - Point3d.Origin);

        public bool Paused
        {
            get => !_timer.Started;
            set
            {
                if (value) { _timer.Stop(); _clock.Stop(); }
                else if (!_disposed) { _clock.Start(); _timer.Start(); }
            }
        }

        public IEnumerable<BoundingBox> WallsNow =>
            Level.Walls.Select(w => { var b = w.At(Seconds); b.Transform(Transform.Translation(Origin - Point3d.Origin)); return b; }).Concat(MazeWalls);

        /// <summary>One frame: drones fly, walls slide, camera moves are read, locks update, the flyer is checked.</summary>
        public void Tick()
        {
            if (_disposed)
                return;
            Update();
            Pops.RemoveAll(x => Seconds - x.Start > x.Duration);
            View?.Redraw();
        }

        private void Update()
        {
            double t = Seconds;
            foreach (var d in Drones)
                d.Position = L(d.Target.Path(t));
            ReadCamera();

            bool changed = false;
            foreach (var d in Drones.Where(d => d.Alive))
            {
                var (locked, hint) = LockState(d);
                if (locked != d.Locked)
                {
                    d.Locked = locked;
                    changed = true;
                }
                d.Hint = hint;
            }

            if (Course != null && !Finished && FlyerCenter is Point3d c)
            {
                var finish = L(Course.Finish);
                var check = Course.FlatFinish ? new Point3d(c.X, c.Y, finish.Z) : c;
                if (check.DistanceTo(finish) < Course.FinishRadius)
                {
                    Finished = true;
                    Pops.Add(new Explosion(finish, Seconds, 1.2, ExplosionKind.Pop));
                    changed = true;
                }
            }
            if (Level.TimeLimit is double limit && !Finished && !TimeUp && Seconds >= limit)
            {
                TimeUp = true;
                changed = true;
            }
            if (changed)
                Changed?.Invoke();
        }

        // ---------------- Reading the camera ----------------

        /// <summary>Works out whether the view was just zoomed, panned or orbited, and whether that's the lesson's move.</summary>
        private void ReadCamera()
        {
            var vp = Viewport;
            if (vp == null)
                return;
            var now = (vp.CameraLocation, vp.CameraTarget, vp.CameraDirection);
            var before = _camera;
            _camera = now;

            double distBefore = before.Location.DistanceTo(before.Target), distNow = now.CameraLocation.DistanceTo(now.CameraTarget);
            double angle = Vector3d.VectorAngle(before.Direction, now.CameraDirection) * 180 / Math.PI;
            double scale = Math.Max(1e-6, distNow);
            Move? move = null;
            if (angle > 0.4)
                move = Move.Orbit;
            else if (Math.Abs(distNow - distBefore) / Math.Max(1e-6, distBefore) > 0.004)
                move = distNow < distBefore ? Move.ZoomIn : Move.ZoomOut;
            else if (before.Target.DistanceTo(now.CameraTarget) / scale > 0.002)
                move = Move.Pan;
            if (move != null)
                SawMove(move.Value);
        }

        private void SawMove(Move move, string? detail = null)
        {
            var wanted = Level.Move;
            bool right = wanted == move || wanted == Move.Any && (move == Move.ZoomIn || move == Move.ZoomOut || move == Move.Pan || move == Move.Orbit);
            // On the zoom lessons, the other zoom direction is fine too (you'll overshoot and come back).
            if ((wanted == Move.ZoomIn || wanted == Move.ZoomOut) && (move == Move.ZoomIn || move == Move.ZoomOut))
                right = true;
            bool news = LastMove != move || Seconds - LastMoveTime > 0.6;
            LastMove = move;
            LastMoveRight = right;
            LastMoveTime = Seconds;
            LastMoveText = detail ?? Name(move);
            if (right && !MoveDone)
                MoveDone = true;
            if (news)
            {
                if (right) GoodMoves++;
                Changed?.Invoke();
            }
        }

        public static string Name(Move move) => move switch
        {
            Move.ZoomIn => "ZOOM IN",
            Move.ZoomOut => "ZOOM OUT",
            Move.Pan => "PAN",
            Move.Orbit => "ORBIT",
            Move.Gumball => "GUMBALL MOVE",
            Move.Nudge => "NUDGE",
            Move.Click => "CLICK",
            _ => "MOVE",
        };

        /// <summary>A gentle correction when the wrong move was used.</summary>
        public string WrongMoveHint => (Level.Move, LastMove) switch
        {
            (Move.Pan, Move.Orbit) => "That turned the view (orbit). Hold Shift while you right-drag to pan. Press Home to undo the view.",
            (Move.Orbit, Move.Pan) => "That slid the view (pan). Let go of Shift: a plain right-drag orbits.",
            (Move.Pan, _) => "For this one, hold Shift and drag with the right button.",
            (Move.Orbit, _) => "For this one, drag with the right button (no Shift).",
            (Move.ZoomIn, _) or (Move.ZoomOut, _) => "For this one, roll the mouse wheel.",
            _ => "",
        };

        // ---------------- Locking on ----------------

        public (Point2d Center, double Radius, double Size, double Offset, bool OnScreen)? OnScreen(LiveDrone d)
        {
            var vp = Viewport;
            if (vp == null || (d.Position - vp.CameraLocation) * vp.CameraDirection <= 0)
                return null;
            var size = vp.Size;
            double w = Math.Max(1, size.Width), h = Math.Max(1, size.Height);
            var c = vp.WorldToClient(d.Position);
            double radius = c.DistanceTo(vp.WorldToClient(d.Position + vp.CameraX * d.Target.Size));
            bool inside = c.X > -radius && c.X < w + radius && c.Y > -radius && c.Y < h + radius;
            double offset = Math.Sqrt(Math.Pow(c.X - w / 2, 2) + Math.Pow(c.Y - h / 2, 2)) / Math.Min(w, h);
            return (c, radius, 2 * radius / h, offset, inside);
        }

        public bool Blocked(LiveDrone d)
        {
            var vp = Viewport;
            if (vp == null)
                return false;
            var line = new Line(vp.CameraLocation, d.Position);
            return WallsNow.Any(w => SegmentHits(line, w, out _));
        }

        private (bool Locked, string Hint) LockState(LiveDrone d)
        {
            var s = OnScreen(d);
            bool on = s?.OnScreen == true;
            double size = s?.Size ?? 0, offset = s?.Offset ?? 1;
            string hint = Level.LockHint;
            switch (Level.Lock)
            {
                case LockRule.Always:
                    return (on, on ? "" : "Find the drone on screen.");
                case LockRule.Big:
                    return (on && size >= Level.LockSize, on ? hint : "Zoom back out to find the drone, then point at it and zoom in.");
                case LockRule.AllOnScreen:
                    bool all = Drones.Where(x => x.Alive).All(x => OnScreen(x)?.OnScreen == true);
                    return (all && on, hint);
                case LockRule.Ring:
                {
                    double turned = Vector3d.VectorAngle(_startDirection, Viewport?.CameraDirection ?? _startDirection) * 180 / Math.PI;
                    if (turned > 8)
                        return (false, "The view turned. Press Home to undo it, then hold Shift while you right-drag.");
                    return (on && offset <= Level.RingSize && size > 0.04, hint);
                }
                case LockRule.Visible:
                    return (on && !Blocked(d), hint);
                case LockRule.Front:
                    return (on && offset <= 0.3, hint);
                case LockRule.Everything:
                    if (on && Blocked(d))
                        return (false, "A wall is in the way. Orbit around it.");
                    if (on && size <= Level.MinLockSize)
                        return (false, "Too small to lock on. Point at it and zoom in.");
                    return (on && offset <= Level.RingSize && size > Level.MinLockSize, hint);
                default:
                    return (on, hint);
            }
        }

        /// <summary>A left-click in the game view: pops a locked-on drone, otherwise says what to do first.</summary>
        public (ShotResult Result, string Text) Shoot(Point click)
        {
            Shots++;
            if (Level.Move == Move.Click)
                SawMove(Move.Click);
            var p = new Point2d(click.X, click.Y);
            LiveDrone? best = null;
            double bestDistance = double.MaxValue;
            foreach (var d in Drones.Where(d => d.Alive))
            {
                var s = OnScreen(d);
                if (s == null)
                    continue;
                double distance = s.Value.Center.DistanceTo(p);
                if (distance <= Math.Max(s.Value.Radius * 1.7, 18) && distance < bestDistance)
                {
                    best = d;
                    bestDistance = distance;
                }
            }
            if (best == null)
                return (ShotResult.Miss, Drones.Any(d => d.Alive && d.Locked) ? "Missed. Click right on the green-ringed drone." : "");
            if (TimeUp)
                return (ShotResult.Miss, "Time's up!");
            if (!best.Locked)
                return (ShotResult.NotLocked, "Not locked on yet. " + best.Hint);

            best.Alive = false;
            bool last = Drones.All(d => !d.Alive);
            Pops.Add(new Explosion(best.Position, Seconds, best.Target.Size, last ? ExplosionKind.Big : ExplosionKind.Pop));
            if (last)
                Finished = true;
            Changed?.Invoke();
            View?.Redraw();
            return (ShotResult.Popped, last ? "All popped!" : $"Pop! {Drones.Count(d => d.Alive)} to go.");
        }

        // ---------------- Flying lessons (Gumball and Nudge) ----------------

        public double NudgeStep { get; private set; } = 1;

        private static readonly string[] Maze =
        {
            "#########",
            "#S..#...#",
            "###.#.#.#",
            "#...#.#.#",
            "#.###.#.#",
            "#.....#F#",
            "#########",
        };

        private Point3d? MazeCameraTarget;
        private Vector3d? MazeCameraOffset;

        private void BuildMaze()
        {
            NudgeStep = ModelAidSettings.NudgeKeyStep;
            if (NudgeStep <= 0 || double.IsNaN(NudgeStep))
                NudgeStep = 1;
            double cell = NudgeStep;
            int rows = Maze.Length;
            var course = new SchoolCourse { DroneSize = cell * 0.32, FinishRadius = cell * 0.4, FlatFinish = true };
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < Maze[r].Length; c++)
                {
                    var center = new Point3d(c * cell, (rows - 1 - r) * cell, 0);
                    switch (Maze[r][c])
                    {
                        case '#':
                            var wall = new BoundingBox(center + new Vector3d(-cell / 2, -cell / 2, 0), center + new Vector3d(cell / 2, cell / 2, cell * 0.8));
                            wall.Transform(Transform.Translation(Origin - Point3d.Origin));
                            MazeWalls.Add(wall);
                            break;
                        case 'S':
                            course.Start = center + new Vector3d(0, 0, cell * 0.3);
                            break;
                        case 'F':
                            course.Finish = center + new Vector3d(0, 0, cell * 0.3);
                            break;
                    }
                }
            course.Bounds = new BoundingBox(new Point3d(-cell, -cell, -cell), new Point3d(9 * cell, rows * cell, 2 * cell));
            Course = course;
            MazeCameraTarget = new Point3d(4 * cell, 3 * cell, 0);
            MazeCameraOffset = new Vector3d(0, -4 * cell, 13 * cell);
        }

        private void AddFlyer()
        {
            var attributes = Doc.CreateDefaultAttributes();
            attributes.Name = "RhinoGotchi drone";
            attributes.ColorSource = ObjectColorSource.ColorFromObject;
            attributes.ObjectColor = Color.FromArgb(240, 140, 30);
            var start = L(Course!.Start);
            FlyerId = Doc.Objects.AddMesh(DroneShapes.Drone(start, Course.DroneSize), attributes);
            _lastGood = start;
            Doc.Objects.UnselectAll();
            Doc.Objects.Select(FlyerId);
        }

        public Point3d? FlyerCenter => FlyerId == Guid.Empty ? null : Doc.Objects.FindId(FlyerId)?.Geometry.GetBoundingBox(true).Center;

        public bool FlyerSelected => FlyerId != Guid.Empty && Doc.Objects.FindId(FlyerId)?.IsSelected(false) > 0;

        private void OnReplace(object? sender, RhinoReplaceObjectEventArgs e)
        {
            if (_reverting || e.Document != Doc || e.ObjectId != FlyerId || FlyerId == Guid.Empty || Course == null)
                return;
            var from = e.OldRhinoObject.Geometry.GetBoundingBox(true).Center;
            var to = e.NewRhinoObject.Geometry.GetBoundingBox(true).Center;
            if (from.DistanceTo(to) < 1e-9)
                return;

            string? crash = null;
            var path = new Line(from, to);
            double grow = Course.DroneSize * 0.5;
            foreach (var wall in WallsNow)
            {
                var g = wall;
                g.Inflate(grow, grow, Level.Move == Move.Nudge ? 0 : grow);
                if (SegmentHits(path, g, out _))
                {
                    crash = "💥 Bonk! You hit a wall and bounced back. Go around it.";
                    break;
                }
            }
            var bounds = Course.Bounds;
            bounds.Transform(Transform.Translation(Origin - Point3d.Origin));
            if (crash == null && !bounds.Contains(to))
                crash = "💥 Out of bounds! You bounced back. Stay in the course.";

            if (crash == null)
            {
                _lastGood = to;
                SawMove(Level.Move, Describe(to - from));
                return;
            }

            // The move hasn't finished inside this event: send the drone back once Rhino is idle.
            Crashes++;
            _pendingCrash = crash;
            if (!_idleHooked)
            {
                _idleHooked = true;
                RhinoApp.Idle += OnIdle;
            }
        }

        private string Describe(Vector3d move)
        {
            double x = Math.Abs(move.X), y = Math.Abs(move.Y), z = Math.Abs(move.Z);
            string verb = Level.Move == Move.Nudge ? "NUDGED" : "MOVED";
            if (z >= x && z >= y) return $"{verb} {(move.Z > 0 ? "UP ↑" : "DOWN ↓")}";
            if (x >= y) return $"{verb} {(move.X > 0 ? "RIGHT →" : "LEFT ←")}";
            return $"{verb} {(move.Y > 0 ? "FORWARD" : "BACK")}";
        }

        public string? LastCrash { get; private set; }

        private void OnIdle(object? sender, EventArgs e) => ApplyPendingCrash();

        /// <summary>Puts a crashed drone back where it last was safe (runs when Rhino is idle after the move).</summary>
        public void ApplyPendingCrash()
        {
            if (_idleHooked)
            {
                _idleHooked = false;
                RhinoApp.Idle -= OnIdle;
            }
            string? crash = _pendingCrash;
            _pendingCrash = null;
            if (crash == null || _disposed)
                return;
            if (FlyerCenter is Point3d now && now.DistanceTo(_lastGood) > 1e-9)
            {
                _reverting = true;
                try
                {
                    Doc.Objects.Transform(FlyerId, Transform.Translation(_lastGood - now), true);
                    Doc.Objects.Select(FlyerId);
                }
                finally
                {
                    _reverting = false;
                }
                Pops.Add(new Explosion(_lastGood, Seconds, Course!.DroneSize * 0.8, ExplosionKind.Bonk));
                View?.Redraw();
            }
            LastCrash = crash;
            Changed?.Invoke();
        }

        // ---------------- helpers ----------------

        /// <summary>True if the segment passes through the box (Intersection.LineBox treats lines as infinite).</summary>
        public static bool SegmentHits(Line segment, BoundingBox box, out Point3d where)
        {
            where = Point3d.Unset;
            if (!Intersection.LineBox(segment, box, 1e-9, out Interval t) || t.Max < 0 || t.Min > 1)
                return false;
            where = segment.PointAt(Math.Max(0, t.Min));
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _timer.Stop();
            _input.Enabled = false;
            _conduit.Enabled = false;
            RhinoDoc.ReplaceRhinoObject -= OnReplace;
            if (_idleHooked)
            {
                _idleHooked = false;
                RhinoApp.Idle -= OnIdle;
            }
            if (FlyerId != Guid.Empty)
                Doc.Objects.Delete(FlyerId, true);
            Viewport?.PopViewProjection();
            Doc.Views.Redraw();
        }

        private static Point3d PlaceBesideModel(RhinoDoc doc)
        {
            var box = doc.Objects.BoundingBox;
            return box.IsValid ? new Point3d(box.Max.X + 0.25 * box.Diagonal.Length + 30, box.Center.Y, box.Min.Z) : Point3d.Origin;
        }

        private static RhinoView? FindPerspective(RhinoDoc doc)
        {
            var active = doc.Views.ActiveView;
            if (active != null && active.ActiveViewport.IsPerspectiveProjection)
                return active;
            return doc.Views.GetStandardRhinoViews().FirstOrDefault(v => v.ActiveViewport.IsPerspectiveProjection) ?? active;
        }

        /// <summary>
        /// Watches the mouse in the game view: lights up the buttons on the corner picture, moves the crosshair,
        /// and turns a left-click on a drone into a pop (so it doesn't select anything). Navigation still works.
        /// </summary>
        private sealed class Input : MouseCallback
        {
            private readonly SchoolGame _game;

            public Input(SchoolGame game) => _game = game;

            private bool Mine(MouseCallbackEventArgs e) =>
                !_game._disposed && e.View != null && _game.View != null && e.View.RuntimeSerialNumber == _game.View.RuntimeSerialNumber;

            protected override void OnMouseDown(MouseCallbackEventArgs e)
            {
                if (!Mine(e))
                    return;
                _game.ShiftDown = e.ShiftKeyDown;
                if (e.MouseButton == MouseButton.Right) _game.RightDown = true;
                if (e.MouseButton == MouseButton.Left) _game.LeftDown = true;
                _game.View?.Redraw();
                // Shooting: only on lessons with drones, never while a command (like a Gumball drag) runs.
                if (e.MouseButton == MouseButton.Left && _game.Drones.Count > 0 && !_game.Paused && !Rhino.Commands.Command.InCommand())
                {
                    e.Cancel = true;
                    var (result, text) = _game.Shoot(e.ViewportPoint);
                    _game.ShotText = text;
                    _game.ShotResultLast = result;
                    _game.Changed?.Invoke();
                }
            }

            protected override void OnMouseUp(MouseCallbackEventArgs e)
            {
                if (!Mine(e))
                    return;
                if (e.MouseButton == MouseButton.Right) _game.RightDown = false;
                if (e.MouseButton == MouseButton.Left) _game.LeftDown = false;
                _game.View?.Redraw();
            }

            protected override void OnMouseMove(MouseCallbackEventArgs e)
            {
                if (!Mine(e))
                    return;
                _game.Aim = e.ViewportPoint;
                _game.ShiftDown = e.ShiftKeyDown;
            }
        }

        public string ShotText { get; set; } = "";
        public ShotResult? ShotResultLast { get; set; }
    }

    internal sealed class LiveDrone
    {
        public LiveDrone(SchoolTarget target) => Target = target;

        public SchoolTarget Target { get; }
        public Point3d Position { get; set; }
        public bool Alive { get; set; } = true;
        public bool Locked { get; set; }
        public string Hint { get; set; } = "";
    }

    internal enum ExplosionKind { Pop, Big, Bonk }

    /// <summary>A small celebration: a pop of confetti-colored sparks and a word.</summary>
    internal sealed class Explosion
    {
        private static int _count;

        public Explosion(Point3d at, double start, double size, ExplosionKind kind)
        {
            At = at;
            Start = start;
            Size = size;
            Kind = kind;
            Seed = _count++;
            Word = kind == ExplosionKind.Bonk ? "BONK!" : kind == ExplosionKind.Big ? "ALL CLEAR!" : "POP!";
            Duration = kind == ExplosionKind.Big ? 1.1 : 0.6;
        }

        public Point3d At { get; }
        public double Start { get; }
        public double Size { get; }
        public ExplosionKind Kind { get; }
        public int Seed { get; }
        public string Word { get; }
        public double Duration { get; }
    }

    /// <summary>Draws the lesson: drones (green ring when locked on), walls, the finish, pops, the crosshair and the live mouse picture.</summary>
    internal sealed class SchoolConduit : DisplayConduit
    {
        private static readonly Color DroneColor = Color.FromArgb(240, 140, 30);
        private static readonly Color LockColor = Color.FromArgb(47, 154, 79);
        private static readonly Color WallColor = Color.FromArgb(150, 150, 158);
        private static readonly Color Accent = Color.FromArgb(232, 89, 12);
        private static readonly Color Ink = Color.FromArgb(34, 34, 34);
        private static readonly Color[] Confetti =
        {
            Color.FromArgb(232, 89, 12), Color.FromArgb(47, 154, 79), Color.FromArgb(63, 111, 166),
            Color.FromArgb(224, 168, 0), Color.FromArgb(117, 100, 196), Color.FromArgb(214, 69, 61),
        };

        private readonly SchoolGame _game;
        private readonly Dictionary<double, Mesh> _droneMeshes = new Dictionary<double, Mesh>();

        public SchoolConduit(SchoolGame game) => _game = game;

        private Mesh DroneMesh(double size)
        {
            if (!_droneMeshes.TryGetValue(size, out var mesh))
                _droneMeshes[size] = mesh = DroneShapes.Drone(Point3d.Origin, size);
            return mesh;
        }

        protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e)
        {
            var box = BoundingBox.Empty;
            foreach (var d in _game.Drones)
                box.Union(new BoundingBox(d.Position - new Vector3d(1, 1, 1) * d.Target.Size * 3, d.Position + new Vector3d(1, 1, 1) * d.Target.Size * 3));
            foreach (var w in _game.WallsNow)
                box.Union(w);
            if (box.IsValid)
                e.IncludeBoundingBox(box);
        }

        protected override void PostDrawObjects(DrawEventArgs e)
        {
            var d = e.Display;
            double now = _game.Seconds;

            foreach (var wall in _game.WallsNow)
            {
                var brep = new Box(wall).ToBrep();
                d.DrawBrepShaded(brep, new DisplayMaterial(WallColor, 0.2));
                d.DrawBrepWires(brep, Color.FromArgb(90, 90, 96), -1);
            }

            if (_game.Course != null)
            {
                var finish = _game.L(_game.Course.Finish);
                var normal = _game.Level.Move == Move.Nudge ? Vector3d.ZAxis : Vector3d.XAxis;
                double pulse = 1 + 0.08 * Math.Sin(now * 4);
                d.DrawCircle(new Circle(new Plane(finish, normal), _game.Course.FinishRadius * pulse), LockColor, 5);
            }

            foreach (var drone in _game.Drones.Where(x => x.Alive))
            {
                var xf = Transform.Translation(drone.Position - Point3d.Origin)
                         * Transform.Rotation(now * 1.5 + drone.Target.Size, Vector3d.ZAxis, Point3d.Origin)
                         * Transform.Rotation(0.12 * Math.Sin(now * 3 + drone.Target.Size), Vector3d.XAxis, Point3d.Origin);
                d.PushModelTransform(xf);
                d.DrawMeshShaded(DroneMesh(drone.Target.Size), new DisplayMaterial(DroneColor, 0));
                d.PopModelTransform();
                if (drone.Locked)
                {
                    // Locked on: a pulsing green ring facing the camera.
                    double r = drone.Target.Size * (1.6 + 0.15 * Math.Sin(now * 6));
                    d.DrawCircle(new Circle(new Plane(drone.Position, e.Viewport.CameraDirection), r), LockColor, 4);
                }
            }

            foreach (var x in _game.Pops)
                DrawPop(d, x, Math.Max(0, Math.Min(1, (now - x.Start) / x.Duration)));
        }

        /// <summary>Confetti-colored sparks bursting out; a gray puff for a bonk.</summary>
        private static void DrawPop(DisplayPipeline d, Explosion x, double a)
        {
            var rnd = new Random(x.Seed);
            int count = x.Kind == ExplosionKind.Big ? 40 : x.Kind == ExplosionKind.Bonk ? 10 : 22;
            int alpha = (int)(255 * (1 - a));
            for (int i = 0; i < count; i++)
            {
                var dir = new Vector3d(rnd.NextDouble() * 2 - 1, rnd.NextDouble() * 2 - 1, rnd.NextDouble() * 1.6 - 0.4);
                if (!dir.Unitize()) dir = Vector3d.ZAxis;
                double speed = x.Size * (x.Kind == ExplosionKind.Bonk ? 1.2 : 2.5 + 2 * rnd.NextDouble());
                var tip = x.At + dir * speed * a + new Vector3d(0, 0, -x.Size * 2.5 * a * a);
                var tail = x.At + dir * speed * Math.Max(0, a - 0.12) + new Vector3d(0, 0, -x.Size * 2.5 * Math.Pow(Math.Max(0, a - 0.12), 2));
                var color = x.Kind == ExplosionKind.Bonk ? Color.FromArgb(alpha, 160, 160, 160) : Color.FromArgb(alpha, Confetti[i % Confetti.Length]);
                d.DrawLine(tail, tip, color, 4);
            }
        }

        protected override void DrawForeground(DrawEventArgs e)
        {
            if (_game.View == null || e.Viewport.Id != _game.View.ActiveViewportID)
                return;
            var d = e.Display;
            var size = e.Viewport.Size;
            double now = _game.Seconds;

            // Pop words.
            foreach (var x in _game.Pops)
            {
                double a = Math.Max(0, Math.Min(1, (now - x.Start) / x.Duration));
                var p = e.Viewport.WorldToClient(x.At);
                int alpha = (int)(255 * (1 - a * a));
                int h = (int)((x.Kind == ExplosionKind.Big ? 34 : 26) * (a < 0.12 ? 0.5 + a * 4.2 : 1));
                var at = new Point2d(p.X, p.Y - 30 - 40 * a);
                d.Draw2dText(x.Word, Color.FromArgb(alpha, 40, 40, 40), new Point2d(at.X + 2, at.Y + 2), true, h);
                d.Draw2dText(x.Word, x.Kind == ExplosionKind.Bonk ? Color.FromArgb(alpha, 200, 200, 200) : Color.FromArgb(alpha, 255, 200, 40), at, true, h);
            }

            // The ring to aim for on the pan and final lessons.
            if (_game.Level.Lock == LockRule.Ring || _game.Level.Lock == LockRule.Everything)
            {
                float cx = size.Width / 2f, cy = size.Height / 2f;
                float r = (float)(_game.Level.RingSize * Math.Min(size.Width, size.Height));
                bool any = _game.Drones.Any(x => x.Alive && x.Locked);
                var color = any ? LockColor : Color.FromArgb(150, 60, 60, 60);
                for (int i = 0; i < 72; i += 2)
                {
                    double a0 = i * Math.PI / 36, a1 = (i + 1) * Math.PI / 36;
                    d.Draw2dLine(new PointF(cx + r * (float)Math.Cos(a0), cy + r * (float)Math.Sin(a0)),
                        new PointF(cx + r * (float)Math.Cos(a1), cy + r * (float)Math.Sin(a1)), color, 3);
                }
            }

            // Crosshair on lessons with drones.
            if (_game.Drones.Count > 0 && _game.Aim is Point aim)
            {
                var color = Color.FromArgb(200, 214, 69, 61);
                float x0 = aim.X, y0 = aim.Y;
                d.Draw2dLine(new PointF(x0 - 16, y0), new PointF(x0 - 5, y0), color, 2);
                d.Draw2dLine(new PointF(x0 + 5, y0), new PointF(x0 + 16, y0), color, 2);
                d.Draw2dLine(new PointF(x0, y0 - 16), new PointF(x0, y0 - 5), color, 2);
                d.Draw2dLine(new PointF(x0, y0 + 5), new PointF(x0, y0 + 16), color, 2);
            }

            DrawMouseCard(d, size);
        }

        /// <summary>
        /// The live mouse picture in the bottom-right corner: which button or keys this lesson uses (orange),
        /// lit green while you press them, the name of the move, and what your last move was.
        /// </summary>
        private void DrawMouseCard(DisplayPipeline d, Size view)
        {
            var level = _game.Level;
            const float w = 250, h = 150, margin = 14;
            float left = view.Width - w - margin, top = view.Height - h - margin;
            if (left < 0 || top < 0)
                return;
            var card = Color.FromArgb(235, 255, 255, 255);
            d.DrawRoundedRectangle(new PointF(left + w / 2, top + h / 2), w, h, 14, Color.FromArgb(200, 200, 200), 1.5f, card);

            // Mouse.
            float mx = left + 46, my = top + 58, mw = 46, mh = 72;
            bool recentMove = _game.Seconds - _game.LastMoveTime < 0.5;
            bool zooming = recentMove && (_game.LastMove == Move.ZoomIn || _game.LastMove == Move.ZoomOut);
            d.DrawRoundedRectangle(new PointF(mx, my), mw, mh, 22, Ink, 2.5f, Color.White);
            Color Button(bool wanted, bool pressed) => pressed ? LockColor : wanted ? Accent : Color.FromArgb(235, 235, 235);
            bool wantLeft = level.Mouse.HasFlag(MouseParts.Left), wantRight = level.Mouse.HasFlag(MouseParts.Right), wantWheel = level.Mouse.HasFlag(MouseParts.Wheel);
            d.DrawRoundedRectangle(new PointF(mx - 11, my - 20), 18, 26, 6, Ink, 1.5f, Button(wantLeft, _game.LeftDown));
            d.DrawRoundedRectangle(new PointF(mx + 11, my - 20), 18, 26, 6, Ink, 1.5f, Button(wantRight, _game.RightDown));
            d.DrawRoundedRectangle(new PointF(mx, my - 22), 7, 14, 3.5f, Ink, 1.5f, zooming ? LockColor : wantWheel ? Accent : Color.White);

            // Keys (Shift, Alt...) to the right of the mouse.
            float kx = left + 92;
            foreach (var key in level.Keys)
            {
                bool lit = key == "Shift" && _game.ShiftDown;
                float kw = Math.Max(40, key.Length * 9 + 18);
                d.DrawRoundedRectangle(new PointF(kx + kw / 2, my - 8), kw, 28, 6, Accent, 2, lit ? LockColor : Color.FromArgb(255, 243, 230));
                d.Draw2dText(key, lit ? Color.White : Color.FromArgb(194, 65, 12), new Point2d(kx + kw / 2, my - 8), true, 14);
                kx += kw + 8;
            }
            if (level.Keys.Length == 0 && level.Move != Move.Click)
                d.Draw2dText(level.Move == Move.Gumball ? "drag" : level.Mouse.HasFlag(MouseParts.Wheel) ? "roll" : "drag", Color.FromArgb(140, 140, 140), new Point2d(left + 120, my - 8), true, 14);

            // The move's name, and what you just did.
            d.Draw2dText(level.Label, Ink, new Point2d(left + w / 2, top + h - 40), true, 12);
            string status;
            Color statusColor;
            if (_game.LastMove == null || _game.Seconds - _game.LastMoveTime > 2.5)
            {
                status = level.Move == Move.Click ? "Point at the drone and click" : "Your turn!";
                statusColor = Color.FromArgb(120, 120, 120);
            }
            else if (_game.LastMoveRight)
            {
                status = "✓ " + _game.LastMoveText;
                statusColor = LockColor;
            }
            else
            {
                status = "That was " + _game.LastMoveText;
                statusColor = Accent;
            }
            d.Draw2dText(status, statusColor, new Point2d(left + w / 2, top + h - 18), true, 14);
        }
    }

    /// <summary>Little meshes for the scene.</summary>
    internal static class DroneShapes
    {
        /// <summary>A cute quadcopter: round body, crossed arms and four rotor discs.</summary>
        public static Mesh Drone(Point3d c, double size)
        {
            var mesh = new Mesh();
            mesh.Append(Mesh.CreateFromSphere(new Sphere(c, size * 0.42), 16, 12));
            var frame = new Plane(c, Vector3d.ZAxis);
            foreach (double angle in new[] { Math.PI / 4, -Math.PI / 4 })
            {
                var armPlane = frame;
                armPlane.Rotate(angle, Vector3d.ZAxis);
                mesh.Append(Mesh.CreateFromBox(new Box(armPlane, new Interval(-size, size), new Interval(-size * 0.07, size * 0.07), new Interval(-size * 0.05, size * 0.05)), 1, 1, 1));
            }
            for (int i = 0; i < 4; i++)
            {
                double a = Math.PI / 4 + i * Math.PI / 2;
                var hub = new Point3d(c.X + size * Math.Cos(a), c.Y + size * Math.Sin(a), c.Z + size * 0.05);
                var disc = new Cylinder(new Circle(new Plane(hub, Vector3d.ZAxis), size * 0.38), size * 0.06);
                mesh.Append(Mesh.CreateFromCylinder(disc, 1, 20));
            }
            mesh.Normals.ComputeNormals();
            mesh.Compact();
            return mesh;
        }
    }
}
