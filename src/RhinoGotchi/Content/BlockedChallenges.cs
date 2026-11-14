using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;
using RhinoGotchi.Levels;

namespace RhinoGotchi.Content
{
    /// <summary>One known way to solve a challenge, recognized by the commands it needs.</summary>
    internal sealed class BlockedWay
    {
        public BlockedWay(string id, string name, string[] keyCommands, string how)
        {
            Id = id;
            Name = name;
            KeyCommands = keyCommands;
            How = how;
        }

        public string Id { get; }

        /// <summary>Short route, e.g. "Rectangle → ExtrudeCrv".</summary>
        public string Name { get; }

        /// <summary>Commands that must all appear in the player's route for this way to count.</summary>
        public string[] KeyCommands { get; }

        /// <summary>How to do it, shown when revealed.</summary>
        public string How { get; }

        public int Steps => Name.Split('→').Length;
    }

    /// <summary>
    /// A "Blocked!" challenge: build the ghost shape, but the obvious command is banned,
    /// so the player has to find another route. There are many routes; the game counts them.
    /// </summary>
    internal sealed class BlockedChallenge
    {
        private readonly Func<Brep> _target;
        private readonly Func<IEnumerable<Point3d>> _points;

        public BlockedChallenge(string id, string title, string goal, string[] blocked, BlockedWay[] ways,
            Func<Brep> target, Func<IEnumerable<Point3d>> points)
        {
            Id = id;
            Title = title;
            Goal = goal;
            Blocked = blocked;
            Ways = ways;
            _target = target;
            _points = points;
        }

        public string Id { get; }
        public string Title { get; }
        public string Goal { get; }

        /// <summary>Commands that are banned for this challenge.</summary>
        public string[] Blocked { get; }

        /// <summary>
        /// Known ways, most specific first: the first way whose key commands all appear in the
        /// player's route is the one they found.
        /// </summary>
        public BlockedWay[] Ways { get; }

        public BlockedWay? Match(IEnumerable<string> route)
        {
            var used = new HashSet<string>(route, StringComparer.OrdinalIgnoreCase);
            return Ways.FirstOrDefault(w => w.KeyCommands.All(used.Contains));
        }

        /// <summary>Builds a level for the team's LevelRunner: snap points, the ghost, and a shape check.</summary>
        public (Level Level, ShapeMatchEvaluator Evaluator) CreateLevel()
        {
            var target = _target();
            var evaluator = new ShapeMatchEvaluator(target);
            var level = new Level
            {
                Id = "blocked-" + Id,
                Title = Title,
                Command = "", // any command except the blocked ones
                Evaluator = evaluator,
            };
            foreach (var p in _points())
                level.StartGeometry.Add(new Point(p));
            level.GhostGeometry.Add(target);
            return (level, evaluator);
        }
    }

    internal static class BlockedChallenges
    {
        private static Point3d P(double x, double y, double z = 0) => new Point3d(x, y, z);

        public static BlockedChallenge? Find(string id) => All.FirstOrDefault(c => c.Id == id);

        public static readonly BlockedChallenge[] All =
        {
            new BlockedChallenge("box", "A box, without Box",
                "Build the ghost box between the points. The Box command is blocked!",
                new[] { "Box" },
                new[]
                {
                    new BlockedWay("loft", "Rectangle → Copy → Loft → Cap", new[] { "Loft", "Cap" },
                        "Draw the bottom Rectangle, Copy it up to the top, Loft the two, then Cap the open ends."),
                    new BlockedWay("faces", "SrfPt × 6 → Join", new[] { "SrfPt", "Join" },
                        "Make each of the six faces with SrfPt (snap to the corners), then Join them into a closed solid."),
                    new BlockedWay("lines", "Line → Join → ExtrudeCrv", new[] { "Line", "Join", "ExtrudeCrv" },
                        "Draw the four base edges with Line, Join them into a closed curve, then ExtrudeCrv with Solid=Yes."),
                    new BlockedWay("cap", "Rectangle → ExtrudeCrv → Cap", new[] { "ExtrudeCrv", "Cap" },
                        "ExtrudeCrv a Rectangle with Solid=No to get open walls, then Cap the top and bottom."),
                    new BlockedWay("planarsrf", "Rectangle → PlanarSrf → ExtrudeSrf", new[] { "PlanarSrf", "ExtrudeSrf" },
                        "Fill a Rectangle with PlanarSrf, then ExtrudeSrf the surface up with Solid=Yes."),
                    new BlockedWay("srfpt", "SrfPt → ExtrudeSrf", new[] { "SrfPt", "ExtrudeSrf" },
                        "Make the bottom face with SrfPt from its four corners, then ExtrudeSrf it up with Solid=Yes."),
                    new BlockedWay("plane", "Plane → ExtrudeSrf", new[] { "Plane", "ExtrudeSrf" },
                        "Draw a flat Plane on the base corners, then ExtrudeSrf it up with Solid=Yes."),
                    new BlockedWay("polyline", "Polyline → ExtrudeCrv", new[] { "Polyline", "ExtrudeCrv" },
                        "Draw the base as a closed Polyline, then ExtrudeCrv it with Solid=Yes."),
                    new BlockedWay("rect", "Rectangle → ExtrudeCrv", new[] { "Rectangle", "ExtrudeCrv" },
                        "Draw a Rectangle on the bottom corners, then ExtrudeCrv it up with Solid=Yes."),
                },
                () => new Box(new BoundingBox(P(0, 0), P(10, 6, 4))).ToBrep(),
                () => new[] { P(0, 0), P(10, 6), P(10, 6, 4) }),

            new BlockedChallenge("cylinder", "A cylinder, without Cylinder",
                "Build the ghost cylinder: base center, radius point and top center are marked. Cylinder is blocked!",
                new[] { "Cylinder", "Tube" },
                new[]
                {
                    new BlockedWay("loft", "Circle → Copy → Loft → Cap", new[] { "Loft", "Cap" },
                        "Draw the base Circle, Copy it to the top, Loft the two circles, then Cap the ends."),
                    new BlockedWay("cap", "Circle → ExtrudeCrv → Cap", new[] { "ExtrudeCrv", "Cap" },
                        "ExtrudeCrv the Circle with Solid=No to get a tube wall, then Cap both ends."),
                    new BlockedWay("planarsrf", "Circle → PlanarSrf → ExtrudeSrf", new[] { "PlanarSrf", "ExtrudeSrf" },
                        "Fill the Circle with PlanarSrf, then ExtrudeSrf the disk up with Solid=Yes."),
                    new BlockedWay("revolve", "Rectangle → Revolve", new[] { "Revolve" },
                        "Draw a standing Rectangle from the axis out to the radius, then Revolve it 360 degrees around the axis."),
                    new BlockedWay("pipe", "Line → Pipe", new[] { "Pipe" },
                        "Draw a Line up the axis and Pipe it with radius 3 and Cap=Flat."),
                    new BlockedWay("extrude", "Circle → ExtrudeCrv", new[] { "Circle", "ExtrudeCrv" },
                        "Draw a Circle at the base, then ExtrudeCrv it up to the top point with Solid=Yes."),
                },
                () => new Cylinder(new Circle(Point3d.Origin, 3), 8).ToBrep(true, true),
                () => new[] { P(0, 0), P(3, 0), P(0, 0, 8) }),

            new BlockedChallenge("sphere", "A sphere, without Sphere",
                "Build the ghost sphere: the center and a point on its surface are marked. Sphere and Ellipsoid are blocked!",
                new[] { "Sphere", "Ellipsoid" },
                new[]
                {
                    new BlockedWay("arc", "Arc → Revolve", new[] { "Arc", "Revolve" },
                        "Draw a half-circle Arc from the bottom point to the top point, then Revolve it 360 degrees around the vertical axis."),
                    new BlockedWay("circle", "Circle → Revolve", new[] { "Circle", "Revolve" },
                        "Draw a Circle standing up (Vertical option), then Revolve it 180 degrees around its vertical diameter."),
                },
                () => new Sphere(Point3d.Origin, 4).ToBrep(),
                () => new[] { P(0, 0), P(4, 0), P(0, 0, 4), P(0, 0, -4) }),

            new BlockedChallenge("plate", "A plate with a hole, without Booleans",
                "Build the ghost plate with a round hole. Every Boolean and hole command is blocked!",
                new[] { "BooleanDifference", "BooleanSplit", "BooleanUnion", "BooleanIntersection", "MakeHole", "RoundHole" },
                new[]
                {
                    new BlockedWay("split", "Box + Cylinder → Split → Join", new[] { "Split", "Join" },
                        "The long way: Split the box with a cylinder and the cylinder with the box, delete the pieces you don't need, then Join the rest."),
                    new BlockedWay("planarsrf", "Rectangle + Circle → PlanarSrf → ExtrudeSrf", new[] { "PlanarSrf", "ExtrudeSrf" },
                        "PlanarSrf the rectangle and the circle together (the circle becomes a hole), then ExtrudeSrf it with Solid=Yes."),
                    new BlockedWay("extrudecrv", "Rectangle + Circle → ExtrudeCrv", new[] { "Circle", "ExtrudeCrv" },
                        "Draw the Rectangle and the Circle, select both, and ExtrudeCrv them with Solid=Yes. The inner curve becomes a hole."),
                },
                () =>
                {
                    var outer = new Rectangle3d(Plane.WorldXY, P(0, 0), P(10, 10)).ToNurbsCurve();
                    var hole = new Circle(P(5, 5), 2).ToNurbsCurve();
                    var face = Brep.CreatePlanarBreps(new Curve[] { outer, hole }, 0.001)?[0];
                    var path = new LineCurve(Point3d.Origin, P(0, 0, 2));
                    var plate = face?.Faces[0].CreateExtrusion(path, true) ?? new Box(new BoundingBox(P(0, 0), P(10, 10, 2))).ToBrep();
                    // Make sure the solid faces outward so its volume is positive.
                    if (plate.SolidOrientation == BrepSolidOrientation.Inward)
                        plate.Flip();
                    return plate;
                },
                () => new[] { P(0, 0), P(10, 10), P(5, 5), P(7, 5), P(0, 0, 2) }),
        };
    }
}
