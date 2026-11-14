using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Rhino;
using Rhino.Geometry;

namespace RhinoGotchi.Content
{
    /// <summary>One bundled model: a real Rhino file from a McNeel forum topic, reduced to its render mesh and curves.</summary>
    internal sealed class DeepCutModel
    {
        public DeepCutModel(string id, string title, string source)
        {
            Id = id;
            Title = title;
            Source = source;
        }

        public string Id { get; }
        /// <summary>Title of the forum topic the file came from.</summary>
        public string Title { get; }
        public string Source { get; }

        public Mesh? Mesh { get; private set; }
        public Line[] Lines { get; private set; } = new Line[0];
        /// <summary>Where the bulk of the geometry is, so a stray object doesn't shrink the view.</summary>
        public BoundingBox Focus { get; private set; } = BoundingBox.Empty;
        public bool Loaded { get; private set; }

        /// <summary>
        /// Reads DeepCut/Models/{id}.dcm: gzip of "DCM1", origin xyz + scale (float32), then triangle
        /// vertices (uint16 xyz), triangles (uint32 abc), line vertices (uint16 xyz), segments (uint32 ab).
        /// </summary>
        public bool Load()
        {
            if (Loaded)
                return true;
            try
            {
                string path = Path.Combine(DeepCutModels.Folder, Id + ".dcm");
                using (var file = File.OpenRead(path))
                using (var gzip = new GZipStream(file, CompressionMode.Decompress))
                using (var buffer = new MemoryStream())
                {
                    gzip.CopyTo(buffer);
                    buffer.Position = 0;
                    using (var r = new BinaryReader(buffer))
                    {
                        if (new string(r.ReadChars(4)) != "DCM1")
                            return false;
                        float ox = r.ReadSingle(), oy = r.ReadSingle(), oz = r.ReadSingle(), scale = r.ReadSingle();
                        Point3f[] ReadPoints()
                        {
                            var points = new Point3f[r.ReadUInt32()];
                            for (int i = 0; i < points.Length; i++)
                                points[i] = new Point3f(ox + r.ReadUInt16() * scale, oy + r.ReadUInt16() * scale, oz + r.ReadUInt16() * scale);
                            return points;
                        }

                        var vertices = ReadPoints();
                        var faces = new MeshFace[r.ReadUInt32()];
                        for (int i = 0; i < faces.Length; i++)
                            faces[i] = new MeshFace((int)r.ReadUInt32(), (int)r.ReadUInt32(), (int)r.ReadUInt32());
                        var linePoints = ReadPoints();
                        var lines = new Line[r.ReadUInt32()];
                        for (int i = 0; i < lines.Length; i++)
                            lines[i] = new Line(linePoints[r.ReadUInt32()], linePoints[r.ReadUInt32()]);

                        if (faces.Length > 0)
                        {
                            var mesh = new Mesh();
                            mesh.Vertices.AddVertices(vertices);
                            mesh.Faces.AddFaces(faces);
                            mesh.Normals.ComputeNormals();
                            Mesh = mesh;
                        }
                        Lines = lines;
                        Focus = Bulk(faces.Length > 0 ? vertices : linePoints);
                    }
                }
                Loaded = Focus.IsValid;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine("RhinoGotchi: could not load model {0} ({1}).", Id, ex.Message);
            }
            return Loaded;
        }

        /// <summary>The 2nd to 98th percentile box of a sample of the points.</summary>
        private static BoundingBox Bulk(Point3f[] points)
        {
            if (points.Length == 0)
                return BoundingBox.Empty;
            int step = Math.Max(1, points.Length / 20000);
            var xs = new List<float>();
            var ys = new List<float>();
            var zs = new List<float>();
            for (int i = 0; i < points.Length; i += step)
            {
                xs.Add(points[i].X);
                ys.Add(points[i].Y);
                zs.Add(points[i].Z);
            }
            xs.Sort();
            ys.Sort();
            zs.Sort();
            int lo = (int)(xs.Count * 0.02), hi = Math.Max(lo, (int)Math.Ceiling(xs.Count * 0.98) - 1);
            return new BoundingBox(new Point3d(xs[lo], ys[lo], zs[lo]), new Point3d(xs[hi], ys[hi], zs[hi]));
        }
    }

    /// <summary>The Deep Cut model list and the commands a guess can be.</summary>
    internal static class DeepCutModels
    {
        /// <summary>The .dcm files are copied next to the plugin, like the command icons.</summary>
        public static readonly string Folder = Path.Combine(
            Path.GetDirectoryName(typeof(DeepCutModels).Assembly.Location) ?? "", "DeepCut", "Models");

        private static List<DeepCutModel>? _all;
        private static string[]? _commands;

        /// <summary>Every model whose file is present.</summary>
        public static IReadOnlyList<DeepCutModel> All => _all ??= ReadLines("RhinoGotchi.DeepCut.models.tsv")
            .Select(line => line.Split('\t'))
            .Where(f => f.Length >= 3 && File.Exists(Path.Combine(Folder, f[0] + ".dcm")))
            .Select(f => new DeepCutModel(f[0], f[1], f[2]))
            .ToList();

        /// <summary>The Rhino geometry commands a guess is matched against, in alphabetical order.</summary>
        public static string[] Commands => _commands ??= ReadLines("RhinoGotchi.DeepCut.commands.txt").ToArray();

        /// <summary>
        /// Turns whatever was typed into a real command: exact, then starts-with, then contains,
        /// then the closest spelling. Null only for empty input.
        /// </summary>
        public static string? Resolve(string typed)
        {
            string q = typed.Trim().TrimStart('_').ToLowerInvariant();
            if (q.Length == 0)
                return null;
            var commands = Commands;
            var low = commands.Select(c => c.ToLowerInvariant()).ToArray();
            int k = Array.IndexOf(low, q);
            if (k < 0) k = Array.FindIndex(low, c => c.StartsWith(q, StringComparison.Ordinal));
            if (k < 0) k = Array.FindIndex(low, c => c.Contains(q));
            if (k < 0)
            {
                double best = double.MaxValue;
                for (int i = 0; i < low.Length; i++)
                {
                    string c = low[i];
                    double d = Math.Min(Distance(q, c), Distance(q, c.Substring(0, Math.Min(c.Length, q.Length))) + 0.5);
                    if (d < best)
                    {
                        best = d;
                        k = i;
                    }
                }
            }
            return k < 0 ? null : commands[k];
        }

        /// <summary>Up to <paramref name="max"/> commands that start with or contain the typed text.</summary>
        public static List<string> Suggest(string typed, int max)
        {
            string q = typed.Trim().TrimStart('_').ToLowerInvariant();
            if (q.Length < 2)
                return new List<string>();
            var starts = Commands.Where(c => c.ToLowerInvariant().StartsWith(q, StringComparison.Ordinal));
            var contains = Commands.Where(c => !c.ToLowerInvariant().StartsWith(q, StringComparison.Ordinal) && c.ToLowerInvariant().Contains(q));
            return starts.Concat(contains).Take(max).ToList();
        }

        private static int Distance(string a, string b)
        {
            var prev = Enumerable.Range(0, b.Length + 1).ToArray();
            for (int i = 1; i <= a.Length; i++)
            {
                var cur = new int[b.Length + 1];
                cur[0] = i;
                for (int j = 1; j <= b.Length; j++)
                    cur[j] = Math.Min(Math.Min(prev[j] + 1, cur[j - 1] + 1), prev[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
                prev = cur;
            }
            return prev[b.Length];
        }

        private static IEnumerable<string> ReadLines(string resource)
        {
            using (var stream = typeof(DeepCutModels).Assembly.GetManifestResourceStream(resource))
            {
                if (stream == null)
                    return new string[0];
                using (var reader = new StreamReader(stream))
                    return reader.ReadToEnd().Split('\n').Select(s => s.Trim('\r', ' ')).Where(s => s.Length > 0).ToList();
            }
        }
    }
}
