using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;
using RhinoGotchi.Fin;

namespace GotchiFins
{
    /// <summary>The six levels, in the order they're played.</summary>
    public enum Stage
    {
        Mass,
        Profile,
        Fin,
        Array,
        Sun,
        Material,
    }

    /// <summary>A polyhedron for drawing: corners and faces (counter-clockwise seen from outside).</summary>
    public sealed class Solid
    {
        public Solid(Point3d[] vertices, int[][] faces)
        {
            Vertices = vertices;
            Faces = faces;
            Edges = BuildEdges(faces);
        }

        public Point3d[] Vertices { get; }
        public int[][] Faces { get; }
        public (int A, int B)[] Edges { get; }

        public Solid Transform(Func<Point3d, Point3d> map) =>
            new Solid(Vertices.Select(map).ToArray(), Faces);

        public IEnumerable<Line> EdgeLines()
        {
            foreach (var (a, b) in Edges)
                yield return new Line(Vertices[a], Vertices[b]);
        }

        public Mesh ToMesh()
        {
            var mesh = new Mesh();
            foreach (int[] face in Faces)
            {
                // Fan each face into its own vertices so normals stay flat. Caps of the fin are
                // not convex, so triangulate them with the ear-clipping MeshPolygon helper.
                int start = mesh.Vertices.Count;
                foreach (int i in face)
                    mesh.Vertices.Add(Vertices[i]);
                if (face.Length == 4)
                    mesh.Faces.AddFace(start, start + 1, start + 2, start + 3);
                else if (face.Length == 3)
                    mesh.Faces.AddFace(start, start + 1, start + 2);
                else
                    foreach (var (a, b, c) in Triangulate(face.Select(i => Vertices[i]).ToArray()))
                        mesh.Faces.AddFace(start + a, start + b, start + c);
            }
            mesh.Normals.ComputeNormals();
            mesh.Compact();
            return mesh;
        }

        /// <summary>
        /// Extrudes a planar polygon by <paramref name="offset"/>. The polygon is reversed if
        /// needed so that every face winds counter-clockwise seen from outside.
        /// </summary>
        public static Solid Extrude(IList<Point3d> polygon, Vector3d offset)
        {
            Point3d[] p = polygon.ToArray();
            if (FinModel.Newell(p) * offset < 0)
                System.Array.Reverse(p);

            int n = p.Length;
            var vertices = p.Concat(p.Select(v => v + offset)).ToArray();
            var faces = new List<int[]>
            {
                Enumerable.Range(0, n).Reverse().ToArray(),   // bottom, facing -offset
                Enumerable.Range(n, n).ToArray(),             // top, facing +offset
            };
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                faces.Add(new[] { i, j, n + j, n + i });
            }
            return new Solid(vertices, faces.ToArray());
        }

        private static (int, int)[] BuildEdges(int[][] faces)
        {
            var edges = new HashSet<(int, int)>();
            foreach (int[] face in faces)
            {
                for (int i = 0; i < face.Length; i++)
                {
                    int a = face[i], b = face[(i + 1) % face.Length];
                    edges.Add(a < b ? (a, b) : (b, a));
                }
            }
            return edges.ToArray();
        }

        /// <summary>Ear clipping for a simple planar polygon wound counter-clockwise around its Newell normal.</summary>
        private static IEnumerable<(int, int, int)> Triangulate(Point3d[] poly)
        {
            Vector3d normal = FinModel.Newell(poly);
            var left = Enumerable.Range(0, poly.Length).ToList();
            int guard = 0;
            while (left.Count > 3 && guard++ < 1000)
            {
                bool clipped = false;
                for (int k = 0; k < left.Count; k++)
                {
                    int a = left[(k + left.Count - 1) % left.Count], b = left[k], c = left[(k + 1) % left.Count];
                    if (Vector3d.CrossProduct(poly[b] - poly[a], poly[c] - poly[b]) * normal <= 0)
                        continue; // reflex corner
                    if (left.Any(i => i != a && i != b && i != c && Inside(poly[i], poly[a], poly[b], poly[c], normal)))
                        continue;
                    yield return (a, b, c);
                    left.RemoveAt(k);
                    clipped = true;
                    break;
                }
                if (!clipped)
                    break;
            }
            if (left.Count == 3)
                yield return (left[0], left[1], left[2]);
        }

        private static bool Inside(Point3d p, Point3d a, Point3d b, Point3d c, Vector3d n) =>
            Vector3d.CrossProduct(b - a, p - a) * n >= 0 &&
            Vector3d.CrossProduct(c - b, p - b) * n >= 0 &&
            Vector3d.CrossProduct(a - c, p - c) * n >= 0;
    }

    /// <summary>
    /// The goal: a 24 x 10 x 8 mass wrapped in thin vertical fins with a zig-zag outer
    /// edge, one every unit around the perimeter. The numbers match the level text.
    /// </summary>
    public static class FinModel
    {
        public const double Width = 24, Depth = 10, Height = 8;

        /// <summary>Fin thickness, the number the player types in level 3.</summary>
        public const double Thickness = 0.3;

        /// <summary>The thickness as the player types it, always with a "." decimal point.</summary>
        public static string ThicknessText => Thickness.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Fins around the perimeter (2 x (24 + 10) = 68, one every unit).</summary>
        public const int FinCount = 68;

        public static double Perimeter => 2 * (Width + Depth);

        // ------------------------------------------------------------ level 1: mass

        public static readonly Point3d MassCornerA = new Point3d(0, 0, 0);
        public static readonly Point3d MassCornerB = new Point3d(Width, Depth, 0);
        public static readonly Point3d MassTop = new Point3d(Width, Depth, Height);

        public static readonly BoundingBox MassBounds = new BoundingBox(MassCornerA, MassTop);
        public const double MassVolume = Width * Depth * Height;

        public static readonly Solid Mass = Solid.Extrude(
            new[] { MassCornerA, new Point3d(Width, 0, 0), MassCornerB, new Point3d(0, Depth, 0) },
            new Vector3d(0, 0, Height));

        // ------------------------------------------------------------ level 2: profile

        /// <summary>
        /// The fin profile, in the plane x = 0 at the mass's front-left corner. It starts on the
        /// wall at the bottom, zig-zags out to 2.5 in front of the wall and comes back at the top.
        /// The player clicks these in order, then the first one again to close the polyline.
        /// </summary>
        public static readonly Point3d[] Profile =
        {
            new Point3d(0, 0, 0),
            new Point3d(0, -1.5, 0),
            new Point3d(0, -2.5, 2),
            new Point3d(0, -1.0, 4),
            new Point3d(0, -2.5, 6),
            new Point3d(0, -1.5, 8),
            new Point3d(0, 0, 8),
        };

        public static readonly BoundingBox ProfileBounds = new BoundingBox(Profile);

        /// <summary>Area of the profile (shoelace formula in the y-z plane).</summary>
        public static readonly double ProfileArea = Math.Abs(Enumerable.Range(0, Profile.Length).Sum(i =>
        {
            Point3d a = Profile[i], b = Profile[(i + 1) % Profile.Length];
            return a.Y * b.Z - b.Y * a.Z;
        }) / 2);

        // ------------------------------------------------------------ level 3: fin

        public static readonly Solid Fin = Solid.Extrude(Profile, new Vector3d(Thickness, 0, 0));
        public static readonly BoundingBox FinBounds = new BoundingBox(Fin.Vertices);
        public static double FinVolume => ProfileArea * Thickness;

        // ------------------------------------------------------------ level 4: array

        /// <summary>
        /// The path the fins are arrayed along: the foot of the mass, starting at the fin and
        /// running counter-clockwise (front, right, back, left), so the fin's outward side
        /// stays outward when ArrayCrv turns it around the corners.
        /// </summary>
        public static readonly Point3d[] PathCorners =
        {
            new Point3d(0, 0, 0), new Point3d(Width, 0, 0), new Point3d(Width, Depth, 0), new Point3d(0, Depth, 0), new Point3d(0, 0, 0),
        };

        public static Polyline PathPolyline => new Polyline(PathCorners);

        /// <summary>Where along the path (0..Perimeter) a point is, and the direction there (degrees about Z).</summary>
        public static (Point3d At, double Angle) PathFrame(double s)
        {
            s = ((s % Perimeter) + Perimeter) % Perimeter;
            if (s < Width) return (new Point3d(s, 0, 0), 0);
            s -= Width;
            if (s < Depth) return (new Point3d(Width, s, 0), 90);
            s -= Depth;
            if (s < Width) return (new Point3d(Width - s, Depth, 0), 180);
            s -= Width;
            return (new Point3d(0, Depth - s, 0), 270);
        }

        /// <summary>The fin copied to arc length <paramref name="s"/> along the path, turned like ArrayCrv's Roadlike/Top.</summary>
        public static Solid FinAt(double s)
        {
            var (at, angle) = PathFrame(s);
            double r = angle * Math.PI / 180, cos = Math.Cos(r), sin = Math.Sin(r);
            return Fin.Transform(p => new Point3d(at.X + p.X * cos - p.Y * sin, at.Y + p.X * sin + p.Y * cos, at.Z + p.Z));
        }

        private static Solid[]? _arrayed;

        /// <summary>All the fins of the finished facade.</summary>
        public static Solid[] ArrayedFins => _arrayed ??= Enumerable.Range(0, FinCount).Select(i => FinAt(i * Perimeter / FinCount)).ToArray();

        /// <summary>Everything, finished: the mass and its fins.</summary>
        public static BoundingBox Bounds
        {
            get
            {
                BoundingBox box = MassBounds;
                foreach (Solid fin in ArrayedFins)
                    box.Union(new BoundingBox(fin.Vertices));
                return box;
            }
        }

        /// <summary>The corner of the facade where the profile and the fin are made, for close-ups.</summary>
        public static BoundingBox FinCloseUp => new BoundingBox(new Point3d(-1.0, -3.0, 0), new Point3d(1.5, 0.5, Height));

        // ------------------------------------------------------------ view

        /// <summary>
        /// The axon camera looks from the south-east, above, so the front (finned) face and the
        /// right face both show, and the fins stand out in front of the wall.
        /// </summary>
        public static readonly Vector3d ViewDirection = Normalized(new Vector3d(-1, 1.25, -0.9));
        public static readonly Vector3d ViewRight = Normalized(Vector3d.CrossProduct(ViewDirection, Vector3d.ZAxis));
        public static readonly Vector3d ViewUp = Normalized(Vector3d.CrossProduct(ViewRight, ViewDirection));

        /// <summary>View direction turned about Z by <paramref name="degrees"/>, for orbiting.</summary>
        public static Vector3d ViewDirectionTurned(double degrees)
        {
            double r = degrees * Math.PI / 180, c = Math.Cos(r), s = Math.Sin(r);
            Vector3d d = ViewDirection;
            return new Vector3d(d.X * c - d.Y * s, d.X * s + d.Y * c, d.Z);
        }

        /// <summary>Outward direction of the side of the mass a fin at arc length <paramref name="s"/> stands on.</summary>
        public static Vector3d SideNormal(double s)
        {
            double r = PathFrame(s).Angle * Math.PI / 180;
            return new Vector3d(Math.Sin(r), -Math.Cos(r), 0);
        }

        /// <summary>Sun direction used by the panel's demo: light from the front-right, shadows falling back-left.</summary>
        public static readonly Vector3d DemoSun = Normalized(new Vector3d(-0.8, 0.5, -0.7));

        // ------------------------------------------------------------ math

        /// <summary>Unit vector, in plain managed math (no native Rhino call).</summary>
        public static Vector3d Normalized(Vector3d v)
        {
            double length = Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
            return length > 0 ? new Vector3d(v.X / length, v.Y / length, v.Z / length) : v;
        }

        /// <summary>Newell's method: the normal of a polygon, pointing so that it winds counter-clockwise around it.</summary>
        public static Vector3d Newell(IList<Point3d> poly)
        {
            var n = Vector3d.Zero;
            for (int i = 0; i < poly.Count; i++)
            {
                Point3d a = poly[i], b = poly[(i + 1) % poly.Count];
                n.X += (a.Y - b.Y) * (a.Z + b.Z);
                n.Y += (a.Z - b.Z) * (a.X + b.X);
                n.Z += (a.X - b.X) * (a.Y + b.Y);
            }
            return Normalized(n);
        }

        public static bool SameBox(BoundingBox a, BoundingBox b, double tolerance) =>
            a.Min.DistanceTo(b.Min) <= tolerance && a.Max.DistanceTo(b.Max) <= tolerance;
    }
}
