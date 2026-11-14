using System;
using System.Collections.Generic;
using Rhino.Geometry;

namespace GotchiHouse
{
    public enum HousePart
    {
        Walls,
        Roof,
    }

    /// <summary>A convex part of the house: its corners, faces and edges.</summary>
    public sealed class PartShape
    {
        public PartShape(HousePart part, Point3d[] vertices, int[][] faces, double volume)
        {
            Part = part;
            Vertices = vertices;
            Faces = faces;
            Volume = volume;
            Bounds = new BoundingBox(vertices);
            Edges = BuildEdges(faces);
        }

        private Mesh? _mesh;

        public HousePart Part { get; }

        public Point3d[] Vertices { get; }

        /// <summary>Faces as vertex indices, counter-clockwise seen from outside.</summary>
        public int[][] Faces { get; }

        public (int A, int B)[] Edges { get; }

        public BoundingBox Bounds { get; }

        public double Volume { get; }

        /// <summary>Built on first use (only the viewport ghost needs it).</summary>
        public Mesh Mesh => _mesh ??= BuildMesh(Vertices, Faces);

        public IEnumerable<Line> EdgeLines()
        {
            foreach (var (a, b) in Edges)
                yield return new Line(Vertices[a], Vertices[b]);
        }

        private static (int, int)[] BuildEdges(int[][] faces)
        {
            var edges = new List<(int, int)>();
            foreach (int[] face in faces)
            {
                for (int i = 0; i < face.Length; i++)
                {
                    int a = face[i], b = face[(i + 1) % face.Length];
                    var edge = a < b ? (a, b) : (b, a);
                    if (!edges.Contains(edge))
                        edges.Add(edge);
                }
            }
            return edges.ToArray();
        }

        private static Mesh BuildMesh(Point3d[] vertices, int[][] faces)
        {
            var mesh = new Mesh();
            foreach (Point3d v in vertices)
                mesh.Vertices.Add(v);
            foreach (int[] f in faces)
            {
                if (f.Length == 4)
                    mesh.Faces.AddFace(f[0], f[1], f[2], f[3]);
                else
                    mesh.Faces.AddFace(f[0], f[1], f[2]);
            }
            mesh.Normals.ComputeNormals();
            mesh.Compact();
            return mesh;
        }
    }

    /// <summary>
    /// The goal: a 10 x 10 x 8 box for the walls and a square pyramid roof that
    /// overhangs by 1 and rises 8. These numbers match the tutorial text.
    /// </summary>
    public static class HouseModel
    {
        public const double WallHeight = 8;
        public const double RoofHeight = 8;

        public static readonly PartShape Walls = new PartShape(
            HousePart.Walls,
            new[]
            {
                new Point3d(0, 0, 0), new Point3d(10, 0, 0), new Point3d(10, 10, 0), new Point3d(0, 10, 0),
                new Point3d(0, 0, 8), new Point3d(10, 0, 8), new Point3d(10, 10, 8), new Point3d(0, 10, 8),
            },
            new[]
            {
                new[] { 0, 3, 2, 1 }, // bottom
                new[] { 4, 5, 6, 7 }, // top
                new[] { 0, 1, 5, 4 }, // front (y = 0)
                new[] { 1, 2, 6, 5 }, // right (x = 10)
                new[] { 2, 3, 7, 6 }, // back
                new[] { 3, 0, 4, 7 }, // left
            },
            volume: 10 * 10 * 8);

        public static readonly PartShape Roof = new PartShape(
            HousePart.Roof,
            new[]
            {
                new Point3d(-1, -1, 8), new Point3d(11, -1, 8), new Point3d(11, 11, 8), new Point3d(-1, 11, 8),
                new Point3d(5, 5, 16),
            },
            new[]
            {
                new[] { 0, 3, 2, 1 }, // base
                new[] { 0, 1, 4 },
                new[] { 1, 2, 4 },
                new[] { 2, 3, 4 },
                new[] { 3, 0, 4 },
            },
            volume: 12 * 12 * 8 / 3.0);

        public static readonly PartShape[] Parts = { Walls, Roof };

        public static BoundingBox Bounds
        {
            get
            {
                BoundingBox box = Walls.Bounds;
                box.Union(Roof.Bounds);
                return box;
            }
        }

        public static PartShape Get(HousePart part) => part == HousePart.Walls ? Walls : Roof;

        /// <summary>The direction the axon camera looks in (from the south-west, above).</summary>
        public static readonly Vector3d ViewDirection = Normalized(new Vector3d(1, 1, -1));

        /// <summary>Screen right and up for <see cref="ViewDirection"/>.</summary>
        public static readonly Vector3d ViewRight = Normalized(Vector3d.CrossProduct(ViewDirection, Vector3d.ZAxis));
        public static readonly Vector3d ViewUp = Normalized(Vector3d.CrossProduct(ViewRight, ViewDirection));

        /// <summary>Unit vector, in plain managed math (no native Rhino call).</summary>
        public static Vector3d Normalized(Vector3d v)
        {
            double length = Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
            return length > 0 ? new Vector3d(v.X / length, v.Y / length, v.Z / length) : v;
        }

        /// <summary>
        /// True when the brep matches the target within <paramref name="tolerance"/>:
        /// same bounding box and (almost) the same volume.
        /// </summary>
        public static bool Matches(Brep brep, BoundingBox brepBox, BoundingBox target, double targetVolume, double tolerance)
        {
            if (brepBox.Min.DistanceTo(target.Min) > tolerance || brepBox.Max.DistanceTo(target.Max) > tolerance)
                return false;
            if (!brep.IsSolid)
                return false;
            double volume = Math.Abs(brep.GetVolume());
            return Math.Abs(volume - targetVolume) <= 0.02 * targetVolume;
        }
    }
}
