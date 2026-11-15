using System;
using System.Collections.Generic;
using Rhino.Geometry;

namespace RhinoGotchi.Water
{
    /// <summary>
    /// One Water Run level: what it teaches, what to tell the player, how much water the goal needs,
    /// and the scene it starts from. A level saved from Rhino (see WaterGame.Save) replaces the scene
    /// and the numbers; the built-in ones below are the starting point.
    /// </summary>
    internal sealed class WaterLevel
    {
        public int Number;
        public string Title = "";
        /// <summary>The part of the Gumball this level is about.</summary>
        public string Lesson = "";
        public string Instruction = "";
        /// <summary>Drops the goal needs.</summary>
        public int Target = 150;
        /// <summary>Drops per second from each source.</summary>
        public int Rate = 40;
        public double DropSize = 0.35;
        /// <summary>Half the depth of the scene (in Y). Water is kept between two invisible panes this far from the middle.</summary>
        public double Depth = 2;
        /// <summary>A 2D level: the water stays in one plane, pieces may not leave it, and it is played in the Front view.</summary>
        public bool Flat;
        public Action<LevelBuilder> Build = b => { };

        public WaterLevel Copy() => (WaterLevel)MemberwiseClone();
    }

    internal enum PieceRole { Solid, Source, Goal, Hazard }

    /// <summary>One object of a level: its shape, what it is, and whether the player may move it.</summary>
    internal sealed class LevelPiece
    {
        public LevelPiece(GeometryBase geometry, PieceRole role, bool movable, string name, string look = "")
        {
            Look = look;
            Geometry = geometry;
            Role = role;
            Movable = movable;
            Name = name;
        }

        public GeometryBase Geometry { get; }

        public PieceRole Role { get; }

        public bool Movable { get; }

        public string Name { get; }

        /// <summary>"stone" for the pale scenery around taps and goals; empty for the usual colours.</summary>
        public string Look { get; }
    }

    /// <summary>
    /// Draws a level's scene. Levels are side-on: x runs left to right, z is up, and every piece is
    /// as deep (in y) as the level, so the scene reads like a drawing in the Front view.
    /// </summary>
    internal sealed class LevelBuilder
    {
        private readonly double _depth;

        public LevelBuilder(double depth)
        {
            _depth = depth;
        }

        public List<LevelPiece> Pieces { get; } = new List<LevelPiece>();

        /// <summary>An old stone fountain head: a capstone, a block and a short spout. Water drips straight down from the spout.</summary>
        public void Source(double x, double z)
        {
            Stone(x - 3, x + 3, z + 2.4, z + 3.1, "Fountain");
            Stone(x - 2.2, x + 2.2, z + 0.9, z + 2.4, "Fountain");
            Stone(x - 1.1, x + 1.1, z + 0.35, z + 0.9, "Fountain");
            Pieces.Add(new LevelPiece(new Point(new Point3d(x, 0, z)), PieceRole.Source, false, "Water source"));
        }

        /// <summary>The goal, standing in a low stone basin (open to the front, so you can watch it fill).</summary>
        public void Goal(double x0, double x1, double z0, double z1)
        {
            double rim = z0 + (z1 - z0) * 0.6;
            Stone(x0 - 0.8, x1 + 0.8, z0 - 0.8, z0, "Basin");
            Stone(x0 - 0.8, x0, z0, rim, "Basin");
            Stone(x1, x1 + 0.8, z0, rim, "Basin");
            Add(BoxBrep(Plane.WorldXY, new Interval(x0, x1), new Interval(-_depth, _depth), new Interval(z0, z1)), PieceRole.Goal, false, "Water goal");
        }

        private void Stone(double x0, double x1, double z0, double z1, string name)
        {
            Brep? box = BoxBrep(Plane.WorldXY, new Interval(x0, x1), new Interval(-_depth, _depth), new Interval(z0, z1));
            if (box != null)
                Pieces.Add(new LevelPiece(box, PieceRole.Solid, false, name, "stone"));
        }

        /// <summary>A red block. If the water or a piece the player moves touches it, the level is lost.</summary>
        public void Hazard(double x0, double x1, double z0, double z1) =>
            Add(BoxBrep(Plane.WorldXY, new Interval(x0, x1), new Interval(-_depth, _depth), new Interval(z0, z1)), PieceRole.Hazard, false, "Hazard");

        /// <summary>A board whose top runs from (x0, z0) to (x1, z1). Draw it from left to right.</summary>
        public void Plank(double x0, double z0, double x1, double z1, bool movable, string name, double thickness = 0.7)
        {
            var start = new Point3d(x0, 0, z0);
            var along = new Vector3d(x1 - x0, 0, z1 - z0);
            double length = along.Length;
            along.Unitize();
            var plane = new Plane(start, along, Vector3d.YAxis);
            Add(BoxBrep(plane, new Interval(0, length), new Interval(-_depth, _depth), new Interval(-thickness, 0)), PieceRole.Solid, movable, name);
        }

        public void Block(double x0, double x1, double z0, double z1, bool movable, string name) =>
            Add(BoxBrep(Plane.WorldXY, new Interval(x0, x1), new Interval(-_depth, _depth), new Interval(z0, z1)), PieceRole.Solid, movable, name);

        /// <summary>A box without a lid, to carry water in.</summary>
        public void Cup(double x0, double x1, double z0, double z1, bool movable, string name)
        {
            Brep? box = BoxBrep(Plane.WorldXY, new Interval(x0, x1), new Interval(-_depth, _depth), new Interval(z0, z1));
            if (box == null)
                return;
            int top = 0;
            for (int i = 1; i < box.Faces.Count; i++)
                if (box.Faces[i].GetBoundingBox(true).Center.Z > box.Faces[top].GetBoundingBox(true).Center.Z)
                    top = i;
            box.Faces.RemoveAt(top);
            box.Compact();
            Add(box, PieceRole.Solid, movable, name);
        }

        /// <summary>A bare line along the front pane. Water falls straight past it until it is extruded into a surface.</summary>
        public void Line(double x0, double z0, double x1, double z1, string name) =>
            Add(new LineCurve(new Point3d(x0, -_depth, z0), new Point3d(x1, -_depth, z1)), PieceRole.Solid, true, name);

        private void Add(GeometryBase? geometry, PieceRole role, bool movable, string name)
        {
            if (geometry != null)
                Pieces.Add(new LevelPiece(geometry, role, movable, name));
        }

        private static Brep? BoxBrep(Plane plane, Interval x, Interval y, Interval z) => new Box(plane, x, y, z).ToBrep();
    }

    /// <summary>
    /// The ten built-in levels. Each one is about a single part of the Gumball. Levels 1 to 5 are flat
    /// (2D, in the Front view); from level 6 on the game is 3D. The last three have red obstacles the
    /// water must not hit, and the last two are staircases built by Alt-copying one step.
    /// Orange pieces can be moved, gray ones are locked, red ones must not be touched.
    /// </summary>
    internal static class WaterLevelCatalog
    {
        public const string Modifier = "Ctrl (Cmd on a Mac)";

        public static readonly IReadOnlyList<WaterLevel> All = new[]
        {
            new WaterLevel
            {
                Number = 1,
                Title = "Slide the lid",
                Lesson = "Move arrows",
                Flat = true,
                Instruction = "The lid is in the way. Click it, then drag the red arrow to slide it sideways until the water falls into the goal.",
                Build = b =>
                {
                    b.Source(0, 24);
                    b.Goal(-3, 3, 0, 4);
                    b.Plank(-6, 9, 6, 9, true, "Lid");
                },
            },
            new WaterLevel
            {
                Number = 2,
                Title = "Up and over",
                Lesson = "Plane handle",
                Flat = true,
                Instruction = "The ramp is too low and too far left. Drag the small square between the two arrows to move it up and across in one go, so it carries the water to the goal.",
                Build = b =>
                {
                    b.Source(-10, 26);
                    b.Goal(9, 16, 0, 5);
                    b.Plank(-25, 7, -3, 0, true, "Ramp"); // belongs at (-13, 16) to (9, 9)
                },
            },
            new WaterLevel
            {
                Number = 3,
                Title = "Tip it",
                Lesson = "Rotate arc",
                Flat = true,
                Instruction = "The board slopes the wrong way. Drag the arc to turn it until the water runs off the right-hand end into the goal.",
                Build = b =>
                {
                    b.Source(-8, 26);
                    b.Goal(6, 13, 0, 5);
                    b.Plank(-12, 10, 4, 14, true, "Board");
                },
            },
            new WaterLevel
            {
                Number = 4,
                Title = "Stretch",
                Lesson = "Scale handles",
                Flat = true,
                Instruction = "The orange ramp is too short. Drag the small square at the tip of the red arrow to stretch it toward the goal.",
                Build = b =>
                {
                    b.Source(-10, 26);
                    b.Goal(10, 17, 0, 5);
                    b.Plank(-14, 17, -3, 13, false, "Fixed ramp");
                    b.Plank(-2, 10.5, 3, 9.5, true, "Short ramp");
                },
            },
            new WaterLevel
            {
                Number = 5,
                Title = "By the numbers",
                Lesson = "Typing a value",
                Flat = true,
                Instruction = "The orange board has to lie down as a bridge. Click the arc once (don't drag), type 75 and press Enter. If it tips the wrong way, undo and type -75.",
                Build = b =>
                {
                    b.Source(-9, 26);
                    b.Goal(11, 18, 0, 5);
                    b.Plank(-13, 15.5, -2.5, 12, false, "Fixed ramp");
                    b.Plank(4, 3, 4, 15, true, "Bridge");
                },
            },
            new WaterLevel
            {
                Number = 6,
                Title = "Out of thin air",
                Lesson = "3D: extrude",
                Instruction = "Welcome to 3D: the Gumball now has a third (green) arrow. That orange line is only a line, so the water falls straight past it. Select it, hold " + Modifier + " and drag the green arrow (or drag the dot on the green arrow) to extrude it into a ramp.",
                Build = b =>
                {
                    b.Source(-10, 26);
                    b.Goal(7, 14, 0, 5);
                    b.Line(-13, 16, 6, 9, "Line");
                },
            },
            new WaterLevel
            {
                Number = 7,
                Title = "Swing",
                Lesson = "Moving the Gumball itself",
                Instruction = "The board hangs from the pin and has to swing up around it, not around its middle. Hold " + Modifier + " and drag the Gumball's centre up to the pin, let go, then turn the board with an arc.",
                Build = b =>
                {
                    b.Source(-10, 26);
                    b.Goal(3, 10, 0, 6);
                    b.Block(-13.4, -12.2, 16.2, 17.2, false, "Pin");
                    b.Plank(-12, 2, -12, 16, true, "Board"); // swings up to about (-12, 16) to (1.5, 12.4)
                },
            },
            new WaterLevel
            {
                Number = 8,
                Title = "Along the slope",
                Lesson = "Gumball alignment",
                Instruction = "The orange chute has to slide out along its own slope: click the small white ball on the Gumball, choose Align to Object, then drag the arrow that points down the slope. New rule: red is deadly. Slide too far and the water overshoots onto the red block, and you lose.",
                Build = b =>
                {
                    b.Source(-10, 26);
                    b.Goal(9, 16, 0, 5);
                    b.Plank(-13, 18, -3, 14, false, "Fixed chute");
                    b.Plank(-12, 16.6, -2, 12.6, true, "Sliding chute"); // slides out to (-2, 12.6) to (8, 8.6)
                    b.Hazard(17, 24, 0, 7);
                },
            },
            new WaterLevel
            {
                Number = 9,
                Title = "Two steps",
                Lesson = "Alt-drag to copy",
                Instruction = "The water needs two steps down to the goal and you have one. Hold Alt while you drag it with the Gumball to leave a copy. Careful: water that falls off a step lands on the red floor, so put the step nearest the goal in first.",
                Build = b =>
                {
                    b.Source(-10, 28);
                    b.Goal(14, 21, 0, 5);
                    b.Plank(-13, 20, -5, 18, false, "Fixed ramp");
                    b.Plank(-13, 8, -6, 6.5, true, "Step"); // steps belong at (-3, 16.5)-(4, 15) and (6, 13.5)-(13, 12)
                    b.Hazard(4, 13.5, 0, 2);
                },
            },
            new WaterLevel
            {
                Number = 10,
                Title = "Staircase",
                Lesson = "Copy, copy, copy",
                Instruction = "Four steps this time, and still only one to start with. Alt-drag copies into a staircase from the gray ramp down to the goal. The red floor is waiting under every gap, so build from the goal back up.",
                Target = 200,
                Build = b =>
                {
                    b.Source(-16, 30);
                    b.Goal(26, 33, 0, 5);
                    b.Plank(-19, 22, -11, 20, false, "Fixed ramp");
                    b.Plank(-19, 8, -12, 6.5, true, "Step"); // steps belong at (-9 + 9k, 18.5 - 3k) to (-2 + 9k, 17 - 3k), k = 0..3
                    b.Hazard(0, 25, 0, 2);
                },
            },
        };
    }
}
