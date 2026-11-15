using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RhinoGotchi.Water
{
    // No Rhino types in this file: the levels are plain data, so tools/WaterTest can play them.

    /// <summary>
    /// One Water Run level: what it teaches, what to tell the player, how much water the goal needs,
    /// the scene it starts from, and one way to solve it (which the test tool plays to prove it works).
    /// A level saved from Rhino (see WaterGame.Save) replaces the scene and the numbers.
    /// </summary>
    internal sealed class WaterLevel
    {
        public int Number;
        public string Title = "";
        /// <summary>The part of the Gumball this level is about.</summary>
        public string Lesson = "";
        public string Instruction = "";
        /// <summary>Which Gumball handle the page animates: move, plane, rotate, scale, extrude, relocate, align or copy.</summary>
        public string Hint = "move";
        /// <summary>The level has red pieces that must not be touched.</summary>
        public bool Red;
        /// <summary>Drops the goal needs.</summary>
        public int Target = 300;
        /// <summary>Drops per second from each source.</summary>
        public int Rate = 160;
        public double DropSize = 0.07;
        /// <summary>A 2D level: the water stays in one plane, pieces may not leave it, and it is played in the Front view.</summary>
        public bool Flat;
        public Action<LevelBuilder> Build = b => { };
        /// <summary>One solution, as the Gumball edits a player would make.</summary>
        public Action<ISolver>? Solve;

        public WaterLevel Copy() => (WaterLevel)MemberwiseClone();
    }

    /// <summary>The Gumball edits a solution is made of. Turns are in degrees, right-handed around a world axis.</summary>
    internal interface ISolver
    {
        void Move(string piece, double dx, double dy, double dz);

        /// <summary>Moves the piece so the middle of its bounding box (where the Gumball sits) is here.</summary>
        void Place(string piece, double x, double y, double z);

        /// <summary>Turns the piece around the middle of its bounding box.</summary>
        void Turn(string piece, char axis, double degrees);

        /// <summary>Turns the piece around another point: the Gumball was moved there first.</summary>
        void TurnAbout(string piece, char axis, double degrees, double x, double y, double z);

        void Stretch(string piece, double sx, double sy, double sz);

        /// <summary>Alt-drag: leaves the piece where it is and makes a copy, moved by this much.</summary>
        void Copy(string piece, string copy, double dx, double dy, double dz);

        /// <summary>Extrudes a curve into a surface.</summary>
        void Extrude(string piece, double dx, double dy, double dz);

        void Wait(double seconds);
    }

    internal enum PieceRole { Solid, Source, Goal, Hazard }

    /// <summary>A box that may be turned: a corner and its three edges.</summary>
    internal struct Slab
    {
        private static readonly int[] Quads = { 0, 3, 2, 1, 4, 5, 6, 7, 0, 1, 5, 4, 1, 2, 6, 5, 2, 3, 7, 6, 3, 0, 4, 7 };

        public V3 O, U, V, W;

        public Slab(V3 o, V3 u, V3 v, V3 w)
        {
            O = o;
            U = u;
            V = v;
            W = w;
        }

        public static Slab Box(double x0, double x1, double y0, double y1, double z0, double z1) =>
            new Slab(new V3(x0, y0, z0), new V3(x1 - x0, 0, 0), new V3(0, y1 - y0, 0), new V3(0, 0, z1 - z0));

        public Slab Moved(Xf xf) => new Slab(xf.Point(O), xf.Vector(U), xf.Vector(V), xf.Vector(W));

        public V3[] Corners() => new[]
        {
            O, O + U, O + U + V, O + V,
            O + W, O + U + W, O + U + V + W, O + V + W,
        };

        /// <summary>Its twelve triangles, three points each.</summary>
        public void AddTriangles(List<V3> into)
        {
            V3[] c = Corners();
            for (int q = 0; q < Quads.Length; q += 4)
            {
                into.Add(c[Quads[q]]);
                into.Add(c[Quads[q + 1]]);
                into.Add(c[Quads[q + 2]]);
                into.Add(c[Quads[q]]);
                into.Add(c[Quads[q + 2]]);
                into.Add(c[Quads[q + 3]]);
            }
        }
    }

    /// <summary>One object of a level: its shape, what it is, and whether the player may move it.</summary>
    internal sealed class LevelPiece
    {
        public string Name = "";
        public PieceRole Role;
        public bool Movable;
        /// <summary>"stone" for the pale scenery around taps and goals; empty for the usual colours.</summary>
        public string Look = "";
        /// <summary>A solid is one or more boxes, joined into one object.</summary>
        public List<Slab> Slabs = new List<Slab>();
        /// <summary>Or a bare polyline: not solid until the player extrudes it.</summary>
        public List<V3> Curve = new List<V3>();
        /// <summary>A source: where the water comes out...</summary>
        public V3 At;
        /// <summary>...and the direction and speed of its jet. Zero = a drip straight down.</summary>
        public V3 Jet;

        public LevelPiece Clone()
        {
            var copy = (LevelPiece)MemberwiseClone();
            copy.Slabs = new List<Slab>(Slabs);
            copy.Curve = new List<V3>(Curve);
            return copy;
        }

        public void Apply(Xf xf)
        {
            for (int i = 0; i < Slabs.Count; i++)
                Slabs[i] = Slabs[i].Moved(xf);
            for (int i = 0; i < Curve.Count; i++)
                Curve[i] = xf.Point(Curve[i]);
            At = xf.Point(At);
            Jet = xf.Vector(Jet);
        }

        public Box3 Bounds()
        {
            Box3 box = Box3.Empty;
            foreach (Slab slab in Slabs)
                foreach (V3 corner in slab.Corners())
                    box.Add(corner);
            foreach (V3 point in Curve)
                box.Add(point);
            if (Role == PieceRole.Source)
                box.Add(At);
            return box;
        }

        public V3[] Triangles()
        {
            var list = new List<V3>();
            foreach (Slab slab in Slabs)
                slab.AddTriangles(list);
            return list.ToArray();
        }
    }

    /// <summary>
    /// Draws a level's scene out of boxes. x runs left to right, y away from you, z is up. Flat levels
    /// are side-on drawings: every piece is two units deep around the plane y = 0 the water lives in.
    /// </summary>
    internal sealed class LevelBuilder
    {
        public const double Thin = 0.14;      // walls of troughs and buckets
        public const double WallHeight = 0.6; // sides of a trough

        public List<LevelPiece> Pieces { get; } = new List<LevelPiece>();

        private LevelPiece Add(string name, PieceRole role, bool movable, string look, params Slab[] slabs)
        {
            var piece = new LevelPiece { Name = name, Role = role, Movable = movable, Look = look };
            piece.Slabs.AddRange(slabs);
            Pieces.Add(piece);
            return piece;
        }

        // ---------------- 3D pieces ----------------

        /// <summary>An old stone fountain head. Water drips straight down from the spout at (x, y, z).</summary>
        public void Tap(double x, double y, double z, double depth = 1.2)
        {
            double spout = Math.Min(depth, 0.45);
            Add("Fountain", PieceRole.Solid, false, "stone",
                Slab.Box(x - 1.2, x + 1.2, y - depth, y + depth, z + 1.0, z + 1.3),
                Slab.Box(x - 0.9, x + 0.9, y - depth * 0.75, y + depth * 0.75, z + 0.4, z + 1.0),
                Slab.Box(x - 0.45, x + 0.45, y - spout, y + spout, z + 0.15, z + 0.4));
            Pieces.Add(new LevelPiece { Name = "Water source", Role = PieceRole.Source, At = new V3(x, y, z) });
        }

        /// <summary>The goal: a stone basin, open at the top, with the green goal volume inside.</summary>
        public void Basin(double cx, double cy, double z0, double width = 3, double depth = 3, double height = 1.6)
        {
            double x0 = cx - width / 2, x1 = cx + width / 2, y0 = cy - depth / 2, y1 = cy + depth / 2, t = 0.3;
            Add("Basin", PieceRole.Solid, false, "stone",
                Slab.Box(x0 - t, x1 + t, y0 - t, y1 + t, z0 - t, z0),
                Slab.Box(x0 - t, x0, y0 - t, y1 + t, z0 - t, z0 + height),
                Slab.Box(x1, x1 + t, y0 - t, y1 + t, z0 - t, z0 + height),
                Slab.Box(x0 - t, x1 + t, y0 - t, y0, z0 - t, z0 + height),
                Slab.Box(x0 - t, x1 + t, y1, y1 + t, z0 - t, z0 + height));
            Add("Water goal", PieceRole.Goal, false, "", Slab.Box(x0, x1, y0, y1, z0, z0 + height * 0.75));
        }

        public void Stone(string name, double x0, double x1, double y0, double y1, double z0, double z1) =>
            Add(name, PieceRole.Solid, false, "stone", Slab.Box(x0, x1, y0, y1, z0, z1));

        public void Block(string name, double x0, double x1, double y0, double y1, double z0, double z1, bool movable = false) =>
            Add(name, PieceRole.Solid, movable, "", Slab.Box(x0, x1, y0, y1, z0, z1));

        /// <summary>A red block. If the water or a piece the player moves touches it, the level is lost.</summary>
        public void Hazard(double x0, double x1, double y0, double y1, double z0, double z1) =>
            Add("Hazard", PieceRole.Hazard, false, "", Slab.Box(x0, x1, y0, y1, z0, z1));

        /// <summary>
        /// A channel with two sides, from its high end to its low end (the middle of the floor at each
        /// end). With a back, the high end is closed so water that lands there can only run down.
        /// </summary>
        public LevelPiece Trough(string name, V3 high, V3 low, bool movable, double width = 1.6, bool back = true)
        {
            V3 run = low - high;
            double length = run.Length;
            V3 along = run / length;
            V3 side = V3.Cross(V3.UnitZ, along).Unit();
            V3 up = V3.Cross(along, side);
            V3 corner = high - side * (width / 2 + Thin) - up * Thin;
            LevelPiece piece = Add(name, PieceRole.Solid, movable, "",
                new Slab(corner, along * length, side * (width + 2 * Thin), up * Thin),
                new Slab(corner, along * length, side * Thin, up * (Thin + WallHeight)),
                new Slab(corner + side * (width + Thin), along * length, side * Thin, up * (Thin + WallHeight)));
            if (back)
                piece.Slabs.Add(new Slab(corner, along * Thin, side * (width + 2 * Thin), up * (Thin + WallHeight)));
            return piece;
        }

        /// <summary>A box without a lid, to carry water in. (cx, cy) is its middle, z0 its underside.</summary>
        public void Bucket(string name, double cx, double cy, double z0, double width = 1.3, double depth = 1.3, double height = 1.3)
        {
            double x0 = cx - width / 2 - Thin, x1 = cx + width / 2 + Thin, y0 = cy - depth / 2 - Thin, y1 = cy + depth / 2 + Thin;
            double z1 = z0 + Thin + height;
            Add(name, PieceRole.Solid, true, "",
                Slab.Box(x0, x1, y0, y1, z0, z0 + Thin),
                Slab.Box(x0, x0 + Thin, y0, y1, z0, z1),
                Slab.Box(x1 - Thin, x1, y0, y1, z0, z1),
                Slab.Box(x0, x1, y0, y0 + Thin, z0, z1),
                Slab.Box(x0, x1, y1 - Thin, y1, z0, z1));
        }

        /// <summary>A bare polyline. Water falls straight past it until it is extruded into a surface.</summary>
        public void Line(string name, params V3[] points)
        {
            var piece = new LevelPiece { Name = name, Role = PieceRole.Solid, Movable = true };
            piece.Curve.AddRange(points);
            Pieces.Add(piece);
        }

        // ---------------- flat (2D) pieces: two units deep around y = 0 ----------------

        public void FlatTap(double x, double z) => Tap(x, 0, z, 1);

        /// <summary>The goal in a flat level: a basin open to the front, so you can watch it fill.</summary>
        public void FlatBasin(double x0, double x1, double z0, double height = 1.8)
        {
            double t = 0.3;
            Add("Basin", PieceRole.Solid, false, "stone",
                Slab.Box(x0 - t, x1 + t, -1, 1, z0 - t, z0),
                Slab.Box(x0 - t, x0, -1, 1, z0 - t, z0 + height),
                Slab.Box(x1, x1 + t, -1, 1, z0 - t, z0 + height));
            Add("Water goal", PieceRole.Goal, false, "", Slab.Box(x0, x1, -1, 1, z0, z0 + height * 0.75));
        }

        public void FlatStone(string name, double x0, double x1, double z0, double z1) => Stone(name, x0, x1, -1, 1, z0, z1);

        /// <summary>A board whose top runs from (x0, z0) to (x1, z1).</summary>
        public void Board(string name, double x0, double z0, double x1, double z1, bool movable, double thickness = 0.3)
        {
            var start = new V3(x0, 0, z0);
            var run = new V3(x1 - x0, 0, z1 - z0);
            V3 along = run.Unit();
            V3 down = V3.Cross(V3.UnitY, along);
            Add(name, PieceRole.Solid, movable, "",
                new Slab(start - V3.UnitY + down * thickness, along * run.Length, V3.UnitY * 2, down * -thickness));
        }

        /// <summary>A bucket seen from the side: a floor and two walls.</summary>
        public void FlatBucket(string name, double cx, double z0, double width = 2, double height = 1.8)
        {
            double x0 = cx - width / 2 - Thin, x1 = cx + width / 2 + Thin, z1 = z0 + Thin + height;
            Add(name, PieceRole.Solid, true, "",
                Slab.Box(x0, x1, -1, 1, z0, z0 + Thin),
                Slab.Box(x0, x0 + Thin, -1, 1, z0, z1),
                Slab.Box(x1 - Thin, x1, -1, 1, z0, z1));
        }
    }

    /// <summary>
    /// The thirteen built-in levels. Three flat warm-ups in the Front view (slide, zigzag, bucket), then
    /// real 3D: chutes that have to be moved along all three arrows and turned around corners, buckets
    /// that are filled at the tap, carried and poured out, and red pieces that must not be touched.
    /// Orange pieces can be moved, gray and stone ones are locked. In every level doing nothing is safe.
    /// tools/WaterTest plays each level's Solve steps and checks that it wins.
    /// </summary>
    internal static class WaterLevelCatalog
    {
        public static string Key => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "Cmd" : "Ctrl";

        public static readonly IReadOnlyList<WaterLevel> All = new[]
        {
            new WaterLevel
            {
                Number = 1,
                Title = "Slide the lid",
                Lesson = "Move arrows",
                Hint = "move",
                Flat = true,
                Target = 150,
                Instruction = "Drag the red arrow.",
                Build = b =>
                {
                    b.FlatTap(0, 10);
                    b.FlatBasin(-1.5, 1.5, 0);
                    b.Board("Lid", -3, 5, 3, 5, true);
                },
                Solve = s => s.Move("Lid", 5.5, 0, 0),
            },
            new WaterLevel
            {
                Number = 2,
                Title = "Zigzag",
                Lesson = "Move and rotate",
                Hint = "rotate",
                Flat = true,
                Target = 150,
                Instruction = "Two boards: one over the roof, one under it.",
                Build = b =>
                {
                    b.FlatTap(2, 12);
                    b.Board("Roof", -2.5, 6.3, 6, 6.3, false);
                    b.FlatStone("Wall", -8.6, -8.2, 0, 9.5);
                    b.FlatBasin(0.5, 3.5, 0);
                    b.Board("Upper board", -13, 0.5, -4.5, 0.5, true);
                    b.Board("Lower board", 9, 0, 9, 8.5, true);
                },
                Solve = s =>
                {
                    s.Turn("Upper board", 'y', -14);
                    s.Place("Upper board", -0.5, 0, 8.5);
                    s.Turn("Lower board", 'y', 106);
                    s.Place("Lower board", -4.0, 0, 4.1);
                },
            },
            new WaterLevel
            {
                Number = 3,
                Title = "Fill, carry, pour",
                Lesson = "The bucket",
                Hint = "rotate",
                Flat = true,
                Target = 120,
                Instruction = "Fill the bucket. Carry it up. Tip it.",
                Build = b =>
                {
                    b.FlatTap(-6, 10);
                    b.FlatStone("Tower", 4.7, 9.3, 0, 5.2);
                    b.FlatBasin(5.5, 8.5, 5.5);
                    b.FlatBucket("Bucket", 0, 0);
                },
                Solve = s =>
                {
                    s.Place("Bucket", -6, 0, 6);
                    s.Wait(3);
                    s.Place("Bucket", 4.4, 0, 9.4);
                    s.Turn("Bucket", 'y', 115);
                },
            },
            new WaterLevel
            {
                Number = 4,
                Title = "The third arrow",
                Lesson = "3D: three arrows",
                Hint = "move",
                Instruction = "Now in 3D. Bring the chute under the spout.",
                Build = b =>
                {
                    b.Tap(-6, 0, 11);
                    b.Trough("Spout", new V3(-7.2, 0, 8.6), new V3(-2, 0, 7.4), false);
                    b.Basin(7.2, 0, 0);
                    b.Trough("Chute", new V3(1, 5, 2.6), new V3(8.2, 5, 1.0), true);
                },
                Solve = s => s.Move("Chute", -3.6, -5, 3.6),
            },
            new WaterLevel
            {
                Number = 5,
                Title = "Turn the corner",
                Lesson = "3D: the flat arc",
                Hint = "rotate",
                Instruction = "Turn the chute with the flat arc. Then move it in.",
                Build = b =>
                {
                    b.Tap(-6, 0, 11);
                    b.Trough("Spout", new V3(-7.2, 0, 8.6), new V3(-2, 0, 7.4), false);
                    b.Basin(-1.1, 8.2, 0);
                    b.Trough("Chute", new V3(2, -5, 1.9), new V3(9, -5, 0.5), true);
                },
                Solve = s =>
                {
                    s.Turn("Chute", 'z', 90);
                    s.Place("Chute", -1.1, 2.6, 5.5);
                },
            },
            new WaterLevel
            {
                Number = 6,
                Title = "High water",
                Lesson = "Bucket in 3D",
                Hint = "plane",
                Target = 350,
                Instruction = "The goal is above the tap. Fill, lift, pour.",
                Build = b =>
                {
                    b.Tap(-6, -3, 6);
                    b.Stone("Tower", 5, 8, 2, 5, 0, 7.7);
                    b.Basin(6.5, 3.5, 8, 2.4, 2.4);
                    b.Bucket("Bucket", 0, -4, 0);
                },
                Solve = s =>
                {
                    s.Place("Bucket", -6, -3, 3.5);
                    s.Wait(6);
                    s.Place("Bucket", 4.3, 3.5, 11.2);
                    s.Turn("Bucket", 'y', 115);
                },
            },
            new WaterLevel
            {
                Number = 7,
                Title = "Out of thin air",
                Lesson = "Extrude",
                Hint = "extrude",
                Instruction = Key + "-drag the green arrow, under the tap.",
                Build = b =>
                {
                    b.Tap(-6, 0.8, 11);
                    b.Line("Line", new V3(-7.5, -1.2, 8.4), new V3(3.5, -1.2, 5.4));
                    b.Basin(6.2, 0.8, 0, 3, 4.5);
                },
                Solve = s => s.Extrude("Line", 0, 4.4, 0),
            },
            new WaterLevel
            {
                Number = 8,
                Title = "Swing the boom",
                Lesson = "Moving the Gumball itself",
                Hint = "relocate",
                Instruction = Key + "-drag the centre to the post, then turn.",
                Build = b =>
                {
                    b.Tap(-5, 0, 11);
                    b.Stone("Post", -5.3, -4.7, -0.3, 0.3, 0, 7.7);
                    b.Trough("Boom", new V3(-5, 0.6, 8.2), new V3(-5, -7.4, 6.6), true);
                    b.Basin(5.5, 0, 0);
                },
                Solve = s => s.TurnAbout("Boom", 'z', 90, -5, 0, 8),
            },
            new WaterLevel
            {
                Number = 9,
                Title = "Stretch first",
                Lesson = "Scale handles",
                Hint = "scale",
                Red = true,
                Instruction = "Stretch it before you place it. Avoid red.",
                Build = b =>
                {
                    b.Tap(-7, 0, 11);
                    b.Trough("Spout", new V3(-8.2, 0, 8.6), new V3(-3.5, 0, 7.5), false);
                    b.Block("Baffle", -2.9, -2.6, -1, 1, 6.6, 8.8);
                    b.Hazard(0.6, 3.8, -2, 2, 0, 0.8);
                    b.Basin(6.6, 0, 0, 3.6);
                    b.Trough("Chute", new V3(-3, -5, 2.0), new V3(0.5, -5, 0.4), true, 1.6, false);
                },
                Solve = s =>
                {
                    s.Stretch("Chute", 2.4, 1, 1);
                    s.Place("Chute", 0.3, 0, 5.4);
                },
            },
            new WaterLevel
            {
                Number = 10,
                Title = "Along the slope",
                Lesson = "Gumball alignment",
                Hint = "align",
                Red = true,
                Instruction = "White ball → Align to Object. Slide it out. Avoid red.",
                Build = b =>
                {
                    b.Tap(-6, -3, 11);
                    b.Trough("Spout", new V3(-7, -3.6, 8.8), new V3(-2.5, -1.0, 7.6), false);
                    b.Trough("Slide", new V3(-6.5, -3.3, 7.6), new V3(-1.3, -0.3, 6.2), true, 2.2, false);
                    b.Basin(5.2, 3.5, 0);
                    b.Hazard(7.4, 9.8, 4.3, 6.7, 0, 2.5);
                },
                Solve = s => s.Move("Slide", 4.64, 2.67, -1.25),
            },
            new WaterLevel
            {
                Number = 11,
                Title = "Around the pillar",
                Lesson = "Alt-drag to copy",
                Hint = "copy",
                Red = true,
                Instruction = "One chute, two needed. Alt-drag a copy, turn it.",
                Build = b =>
                {
                    b.Tap(-7, 0, 11);
                    b.Trough("Spout", new V3(-8.2, 0, 8.8), new V3(-4, 0, 7.8), false);
                    b.Block("Baffle", -3.4, -3.1, -1, 1, 6.9, 9.1);
                    b.Hazard(-1.5, 1.5, 2.2, 5.2, 0, 10);
                    b.Basin(4.1, 8.0, 0);
                    b.Trough("Chute", new V3(-3.5, -5, 2.0), new V3(3.4, -5, 0.6), true, 2.0);
                },
                Solve = s =>
                {
                    s.Place("Chute", -0.85, 0, 6.15);
                    s.Copy("Chute", "Chute 2", 0, -4, -3);
                    s.Turn("Chute 2", 'z', 90);
                    s.Place("Chute 2", 3.8, 2.5, 4.1);
                },
            },
            new WaterLevel
            {
                Number = 12,
                Title = "Bucket relay",
                Lesson = "Everything",
                Hint = "rotate",
                Red = true,
                Target = 300,
                Instruction = "Aim the chute under the red roof. Bucket over the wall.",
                Build = b =>
                {
                    b.Tap(-7, 0, 3);
                    b.Hazard(-3.2, -2.7, -5, 5, 0, 4.6);
                    b.Basin(6.5, 4, 0);
                    b.Hazard(4.6, 8.4, 2.1, 5.9, 3.9, 4.2);
                    b.Stone("Post", 8.1, 8.4, 2.1, 2.4, 0, 3.9);
                    b.Stone("Post", 8.1, 8.4, 5.6, 5.9, 0, 3.9);
                    b.Trough("Chute", new V3(0, -5.5, 2.0), new V3(6.9, -5.5, 0.6), true, 2.0);
                    b.Bucket("Bucket", -5, -3.5, 0);
                },
                Solve = s =>
                {
                    s.Turn("Chute", 'z', 33.7);
                    s.Place("Chute", 1.73, 0.83, 3.73);
                    s.Place("Bucket", -7, 0, 1.3);
                    s.Wait(6);
                    s.Place("Bucket", -1.9, -0.9, 6.6);
                    s.Turn("Bucket", 'y', 115);
                },
            },
            new WaterLevel
            {
                Number = 13,
                Title = "The long way up",
                Lesson = "Hard: everything, twice",
                Hint = "copy",
                Red = true,
                Target = 900,
                Instruction = "Copy and turn the chute. Feed it by bucket. Two trips.",
                Build = b =>
                {
                    // The tap is low and walled off; the goal is high on a tower under a red roof. The only
                    // way in is a chute from the side, fed by a second chute that only a bucket can reach.
                    b.Tap(-8, -4, 3);
                    b.Hazard(-5, -4.5, -8, 4, 0, 6);
                    b.Stone("Tower", 4.5, 8.5, 4, 8, 0, 5);
                    b.Basin(6.5, 6, 5.3);
                    b.Hazard(4.6, 8.4, 4.1, 7.9, 8.8, 9.1);
                    b.Stone("Post", 8.1, 8.4, 4.1, 4.4, 5, 8.8);
                    b.Stone("Post", 8.1, 8.4, 7.6, 7.9, 5, 8.8);
                    b.Trough("Chute", new V3(-1, -7, 2.0), new V3(5.9, -7, 0.6), true, 2.0);
                    b.Bucket("Bucket", -6, -7, 0);
                },
                Solve = s =>
                {
                    s.Place("Chute", 1.8, -1.4, 10.83);
                    s.Copy("Chute", "Chute 2", 0, -5, 0);
                    s.Turn("Chute 2", 'z', 90);
                    s.Place("Chute 2", 6.5, 1.15, 8.53);
                    for (int trip = 0; trip < 2; trip++)
                    {
                        s.Place("Bucket", -8, -4, 1.3);
                        s.Wait(6);
                        s.Place("Bucket", -2.4, -1.4, 13.7);
                        s.Turn("Bucket", 'y', 115);
                        s.Wait(5);
                        s.Turn("Bucket", 'y', -115);
                    }
                },
            },
        };
    }
}
