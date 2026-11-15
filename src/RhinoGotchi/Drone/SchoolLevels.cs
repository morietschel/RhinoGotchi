using System;
using System.Collections.Generic;
using Rhino.Geometry;

namespace RhinoGotchi.Drone
{
    /// <summary>The mouse or keyboard move a level teaches.</summary>
    internal enum Move { Click, ZoomIn, ZoomOut, Pan, Orbit, Gumball, Nudge, Any }

    /// <summary>Which part of the mouse a level is about (drawn on the mouse pictures).</summary>
    [Flags]
    internal enum MouseParts
    {
        None = 0,
        Left = 1,
        Right = 2,
        Wheel = 4,
    }

    /// <summary>When a drone becomes "locked on" (shootable): the proof that the mouse move was done.</summary>
    internal enum LockRule
    {
        /// <summary>Right away (the click lesson).</summary>
        Always,
        /// <summary>Its height on screen is at least the level's <see cref="SchoolLevel.LockSize"/> (zoom in).</summary>
        Big,
        /// <summary>Every drone in the level is on screen at once (zoom out).</summary>
        AllOnScreen,
        /// <summary>It sits inside the center ring without the view having turned (pan).</summary>
        Ring,
        /// <summary>It is on screen and no wall is in the way (orbit).</summary>
        Visible,
        /// <summary>It is on screen near the middle (follow it with orbit).</summary>
        Front,
        /// <summary>Centered in the ring, big enough and not behind a wall (final exam).</summary>
        Everything,
    }

    /// <summary>A drone to lock on to and pop: how it flies (position over time, in level coordinates) and its size.</summary>
    internal sealed class SchoolTarget
    {
        public SchoolTarget(Func<double, Point3d> path, double size = 1.2)
        {
            Path = path;
            Size = size;
        }

        public Func<double, Point3d> Path { get; }
        public double Size { get; }
    }

    /// <summary>A wall that blocks shots or flying. Optionally it slides over time.</summary>
    internal sealed class SchoolWall
    {
        public SchoolWall(BoundingBox box, Func<double, Vector3d>? motion = null)
        {
            Box = box;
            Motion = motion;
        }

        public BoundingBox Box { get; }
        public Func<double, Vector3d>? Motion { get; }

        public BoundingBox At(double t)
        {
            var b = Box;
            if (Motion != null)
                b.Transform(Transform.Translation(Motion(t)));
            return b;
        }
    }

    /// <summary>A flying course for the Gumball and Nudge lessons: your own drone, a start, a finish and the space you may use.</summary>
    internal sealed class SchoolCourse
    {
        public Point3d Start { get; set; }
        public Point3d Finish { get; set; }
        public double DroneSize { get; set; } = 1.0;
        public double FinishRadius { get; set; } = 1.6;
        public BoundingBox Bounds { get; set; }
        public bool FlatFinish { get; set; }
    }

    internal sealed class SchoolLevel
    {
        public SchoolLevel(string id, string title, Move move, MouseParts mouse, string[] keys, string label, string how, string why)
        {
            Id = id;
            Title = title;
            Move = move;
            Mouse = mouse;
            Keys = keys;
            Label = label;
            How = how;
            Why = why;
        }

        public string Id { get; }
        public string Title { get; }
        public Move Move { get; }
        public MouseParts Mouse { get; }
        public string[] Keys { get; }

        /// <summary>The line under the mouse picture, e.g. "SHIFT + RIGHT-DRAG = PAN".</summary>
        public string Label { get; }
        public string How { get; }
        public string Why { get; }

        public List<SchoolTarget> Targets { get; } = new List<SchoolTarget>();
        public List<SchoolWall> Walls { get; } = new List<SchoolWall>();
        public SchoolCourse? Course { get; set; }

        public LockRule Lock { get; set; } = LockRule.Always;
        public double LockSize { get; set; } = 0.3;
        public double RingSize { get; set; } = 0.2;

        /// <summary>Seconds to finish in, or null for no clock.</summary>
        public double? TimeLimit { get; set; }

        /// <summary>The smallest a drone may look on screen and still lock on (fraction of the view height).</summary>
        public double MinLockSize { get; set; } = 0.05;

        /// <summary>What to tell the player while a drone isn't locked on yet.</summary>
        public string LockHint { get; set; } = "";

        public Point3d CameraTarget { get; set; } = new Point3d(0, 0, 4);
        public Vector3d CameraOffset { get; set; } = new Vector3d(0, -18, 4);

        public SchoolLevel Add(SchoolTarget target) { Targets.Add(target); return this; }

        public SchoolLevel Wall(double x0, double y0, double z0, double x1, double y1, double z1, Func<double, Vector3d>? motion = null)
        {
            Walls.Add(new SchoolWall(new BoundingBox(new Point3d(x0, y0, z0), new Point3d(x1, y1, z1)), motion));
            return this;
        }

        public SchoolLevel Camera(Point3d target, Vector3d offset) { CameraTarget = target; CameraOffset = offset; return this; }

        public SchoolLevel Locks(LockRule rule, string hint) { Lock = rule; LockHint = hint; return this; }
    }

    /// <summary>Flight paths: a position for a time in seconds.</summary>
    internal static class Fly
    {
        public static Func<double, Point3d> Hover(double x, double y, double z, double phase = 0) =>
            t => new Point3d(x, y, z + 0.4 * Math.Sin(1.5 * t + phase));

        public static Func<double, Point3d> SideToSide(double x, double y, double z, double amplitude, double speed, double phase = 0) =>
            t => new Point3d(x + amplitude * Math.Sin(speed * t + phase), y, z + 0.3 * Math.Sin(2 * t + phase));

        public static Func<double, Point3d> UpDown(double x, double y, double z, double amplitude, double speed, double phase = 0) =>
            t => new Point3d(x, y, z + amplitude * Math.Sin(speed * t + phase));

        public static Func<double, Point3d> Circle(double cx, double cy, double cz, double radius, double speed, double phase = 0) =>
            t => new Point3d(cx + radius * Math.Cos(speed * t + phase), cy + radius * Math.Sin(speed * t + phase), cz + 0.5 * Math.Sin(1.3 * t + phase));

        /// <summary>A wild, looping path: several wobbles mixed together so it never quite repeats.</summary>
        public static Func<double, Point3d> Chaos(double cx, double cy, double cz, double size, double speed, double phase = 0) =>
            t =>
            {
                double w = speed * t + phase;
                return new Point3d(
                    cx + size * (Math.Sin(1.3 * w) + 0.45 * Math.Sin(3.7 * w)),
                    cy + size * 0.6 * (Math.Cos(0.9 * w) + 0.35 * Math.Sin(2.9 * w)),
                    cz + size * 0.5 * (Math.Sin(2.3 * w) + 0.3 * Math.Cos(5.1 * w)));
            };
    }

    internal static class SchoolLevels
    {
        private static Point3d P(double x, double y, double z) => new Point3d(x, y, z);

        /// <summary>The sliding gate's vertical offset over time (level 10).</summary>
        public static double GateOffset(double t) => 3.5 * Math.Sin(0.5 * t);

        public static readonly SchoolLevel[] All =
        {
            new SchoolLevel("click", "Left-click", Move.Click, MouseParts.Left, new string[0],
                    "LEFT-CLICK = PICK",
                    "The left button picks things in Rhino. A drone is hovering in front of you: point at it and left-click to pop it.",
                    "Every selection in Rhino starts with a left-click.")
                .Add(new SchoolTarget(Fly.Hover(0, 0, 4)))
                .Locks(LockRule.Always, "")
                .Camera(P(0, 0, 4), new Vector3d(0, -16, 3)),

            new SchoolLevel("zoomin", "Zoom in", Move.ZoomIn, MouseParts.Wheel, new string[0],
                    "ROLL WHEEL TOWARD YOU = ZOOM IN",
                    "The drone is a speck far away. Point at it and roll the wheel toward you to zoom in. When it's big, it locks on: then click to pop it.",
                    "Rhino zooms toward the cursor, so point at what you want first.")
                .Add(new SchoolTarget(Fly.SideToSide(0, 0, 4, 1.5, 0.5)))
                .Locks(LockRule.Big, "Still too small. Point at the drone and roll the wheel toward you.")
                .Camera(P(0, 0, 4), new Vector3d(60, -170, 50)),

            new SchoolLevel("zoomout", "Zoom out", Move.ZoomOut, MouseParts.Wheel, new string[0],
                    "ROLL WHEEL AWAY = ZOOM OUT",
                    "You're so close you can only see one drone. Roll the wheel away from you to zoom out until all three are on screen, then pop them.",
                    "Zooming out shows the whole model so you don't lose your place.")
                .Add(new SchoolTarget(Fly.SideToSide(-12, 0, 4, 2, 0.4)))
                .Add(new SchoolTarget(Fly.UpDown(0, 0, 8, 1.5, 0.6)))
                .Add(new SchoolTarget(Fly.SideToSide(12, 0, 2, 2, 0.5, 1)))
                .Locks(LockRule.AllOnScreen, "Zoom out until all three drones are on screen.")
                .Camera(P(0, 0, 8), new Vector3d(0, -7, 1)),

            new SchoolLevel("pan", "Pan sideways", Move.Pan, MouseParts.Right, new[] { "Shift" },
                    "SHIFT + RIGHT-DRAG = PAN",
                    "The drone is bobbing off to the right. Hold Shift and drag with the right button to slide the view until it's inside the ring, then click it.",
                    "Panning slides the view without turning it, so your model keeps its angle.")
                .Add(new SchoolTarget(Fly.UpDown(28, 0, 4, 2, 0.6)))
                .Locks(LockRule.Ring, "Hold Shift and right-drag to slide the drone into the ring.")
                .Camera(P(0, 0, 4), new Vector3d(0, -22, 3)),

            new SchoolLevel("panup", "Pan up and down", Move.Pan, MouseParts.Right, new[] { "Shift" },
                    "SHIFT + RIGHT-DRAG UP OR DOWN = PAN",
                    "This drone is flying high above you. Shift + right-drag downward to slide the view up to it.",
                    "Pan works in every direction: drag the way you want the model to move.")
                .Add(new SchoolTarget(Fly.SideToSide(0, 0, 18, 3, 0.5)))
                .Locks(LockRule.Ring, "Hold Shift and right-drag down to bring the drone into the ring.")
                .Camera(P(0, 0, 4), new Vector3d(0, -20, 0)),

            new SchoolLevel("orbit", "Orbit around", Move.Orbit, MouseParts.Right, new string[0],
                    "RIGHT-DRAG = ORBIT",
                    "A wall is hiding the drone. Drag with the right mouse button (no Shift) to fly around the wall. Once you can see it, it locks on.",
                    "Orbiting is how you check the back and sides of your model.")
                .Add(new SchoolTarget(Fly.SideToSide(0, 6, 5, 3, 0.5)))
                .Wall(-9, -0.5, 0, 9, 0.5, 11)
                .Locks(LockRule.Visible, "The wall is in the way. Right-drag to orbit around it.")
                .Camera(P(0, 2, 5), new Vector3d(0, -32, 5)),

            new SchoolLevel("follow", "Follow with orbit", Move.Orbit, MouseParts.Right, new string[0],
                    "RIGHT-DRAG = ORBIT",
                    "This drone circles the scene. Right-drag to orbit along with it and bring it to the middle of the view.",
                    "Small orbits keep a moving part of your model in front of you.")
                .Add(new SchoolTarget(Fly.Circle(0, 0, 5, 12, 0.3)))
                .Locks(LockRule.Front, "Orbit with right-drag to bring the circling drone to the middle.")
                .Camera(P(0, 0, 5), new Vector3d(0, -30, 8)),

            new SchoolLevel("zoompoint", "Zoom where you point", Move.ZoomIn, MouseParts.Wheel, new string[0],
                    "POINT, THEN ROLL = ZOOM THERE",
                    "Two tiny drones, far apart. Point at one and roll the wheel to zoom right to it, pop it, zoom back out, then do the other.",
                    "Where the cursor is when you roll decides where you zoom.")
                .Add(new SchoolTarget(Fly.Hover(-24, 0, 4)))
                .Add(new SchoolTarget(Fly.Hover(24, 0, 7, 1)))
                .Locks(LockRule.Big, "Point right at a drone, then roll the wheel toward you.")
                .Camera(P(0, 0, 5), new Vector3d(0, -150, 40)),

            new SchoolLevel("gumball", "Fly with the Gumball", Move.Gumball, MouseParts.Left, new string[0],
                    "LEFT-DRAG A GUMBALL ARROW = MOVE",
                    "Now you fly! Your drone is selected; turn on Gumball in the status bar. Drag the blue arrow to go up or down and the red arrow to go sideways. Go up through the high gap, down through the low one, to the green ring. Walls bounce you back.",
                    "Gumball arrows move things along one direction at a time.")
                .Wall(5.5, -5, 0, 6.5, 5, 7).Wall(5.5, -5, 10, 6.5, 5, 12)
                .Wall(11.5, -5, 0, 12.5, 5, 1).Wall(11.5, -5, 4, 12.5, 5, 12)
                .Camera(P(9, 0, 5), new Vector3d(4, -34, 10)),

            new SchoolLevel("gate", "Time the moving gate", Move.Gumball, MouseParts.Left, new string[0],
                    "LEFT-DRAG A GUMBALL ARROW = MOVE",
                    "This wall's gap slides up and down. Line your drone up with the gap (blue arrow), wait until the gap is in front of you, then drag the red arrow through.",
                    "Watching before you move saves a lot of Undo.")
                .Wall(7.5, -5, -20, 8.5, 5, 3.5, t => new Vector3d(0, 0, GateOffset(t)))
                .Wall(7.5, -5, 6.5, 8.5, 5, 30, t => new Vector3d(0, 0, GateOffset(t)))
                .Camera(P(8, 0, 5), new Vector3d(3, -32, 6)),

            new SchoolLevel("nudge", "Nudge maze", Move.Nudge, MouseParts.None, new[] { "Alt", "← ↑ → ↓" },
                    "ALT + ARROW KEYS = NUDGE",
                    "Keep the drone selected and press Alt + an arrow key to nudge it one square at a time through the maze to the green ring.",
                    "Nudging makes tiny exact moves. Set the step in Options > Modeling Aids."),

            new SchoolLevel("final", "Final exam", Move.Any, MouseParts.Wheel | MouseParts.Right | MouseParts.Left, new[] { "Shift" },
                    "ZOOM · PAN · ORBIT",
                    "Three drones: one far away, one off to the side and one behind a wall. Zoom, pan and orbit to put each one in the ring, then pop it.",
                    "That's the whole toolkit for getting around Rhino.")
                .Add(new SchoolTarget(Fly.Hover(0, 60, 5)))
                .Add(new SchoolTarget(Fly.UpDown(30, 0, 4, 1.5, 0.7)))
                .Add(new SchoolTarget(Fly.SideToSide(-6, 8, 5, 2, 0.5)))
                .Wall(-12, 2.5, 0, 0, 3.5, 11)
                .Locks(LockRule.Everything, "Get the drone in the ring: zoom, Shift + right-drag to pan, or right-drag to orbit.")
                .Camera(P(0, 0, 5), new Vector3d(0, -26, 4)),

            new SchoolLevel("storm", "Drone Storm", Move.Any, MouseParts.Wheel | MouseParts.Right | MouseParts.Left, new[] { "Shift" },
                    "ZOOM · PAN · ORBIT · FAST!",
                    "Very hard. Six small, fast drones are flying wild: one far away, one up high, one diving, one circling behind you and one darting behind a sliding wall. Use every move to trap each one in the small ring and pop it before the 2-minute clock runs out.",
                    "Real modeling mixes all the moves without thinking. This is the practice.")
                .Add(new SchoolTarget(Fly.Chaos(0, 0, 5, 5, 0.9), size: 0.9))            // zigzags in front of you
                .Add(new SchoolTarget(Fly.Chaos(0, 90, 6, 7, 0.7, 1), size: 0.9))         // far away: zoom
                .Add(new SchoolTarget(Fly.Chaos(0, 4, 24, 4, 0.8, 2), size: 0.9))         // up high: pan
                .Add(new SchoolTarget(t => new Point3d(14 + 3 * Math.Sin(1.1 * t), 2, 6 + 7 * Math.Sin(1.6 * t)), size: 0.9)) // dives
                .Add(new SchoolTarget(Fly.Circle(0, 0, 6, 28, 0.55, 3), size: 0.9))        // circles behind you: orbit
                .Add(new SchoolTarget(Fly.Chaos(-14, 10, 5, 3, 1.0, 4), size: 0.9))        // behind the sliding wall
                .Wall(-20, 5.5, 0, -8, 6.5, 12, t => new Vector3d(8 * Math.Sin(0.7 * t), 0, 0))
                .Locks(LockRule.Everything, "Trap it in the small ring: zoom, Shift + right-drag, right-drag. Get closer if it looks tiny.")
                .Camera(P(0, 0, 6), new Vector3d(0, -30, 6)),
        };

        static SchoolLevels()
        {
            // Flight courses (set here because they need the walls above).
            All[8].Course = new SchoolCourse
            {
                Start = P(0, 0, 2.5), Finish = P(18, 0, 2.5),
                Bounds = new BoundingBox(P(-4, -5, -0.5), P(22, 5, 12.5)),
            };
            All[9].Course = new SchoolCourse
            {
                Start = P(0, 0, 5), Finish = P(16, 0, 5),
                Bounds = new BoundingBox(P(-4, -5, -3), P(20, 5, 13)),
            };
            All[10].Course = new SchoolCourse { FlatFinish = true }; // the maze is built from the nudge step at start
            All[0].LockSize = 0;
            All[1].LockSize = 0.3;
            All[7].LockSize = 0.18;

            var storm = All[12];
            // Hard: every drone has to be zoomed in close and centered; nothing locks on from the start view.
            storm.RingSize = 0.12;
            storm.MinLockSize = 0.17;
            storm.TimeLimit = 120;
        }
    }
}
