using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace RhinoGotchi.Tutorials
{
    /// <summary>
    /// Where the tutorials' reference geometry lives in the Klosterneuburg model: the end of
    /// each layer's full path. Names contain ä, ü and ß, so they are compared ordinally.
    /// </summary>
    public static class ReferenceLayers
    {
        public const string Roof = "00.3 ROOF::00.3.1 BASE SRF::Roof";
        public const string RoofEdge = "00.3 ROOF::00.3.1 BASE SRF::Ceiling::Exterior::Edge";
        public const string MainBeamsLong = "03-3D::Hauptträger::lang";
        public const string MainBeamsShort = "03-3D::Hauptträger::kurz";
        public const string RingBeams = "03-3D::Ringträger";
        public const string Joists = "03-3D::Nebenträger";
        public const string Glass = "00.1 FACADE::00.1.2 DETAILED MODEL::Glass";
        public const string Ramp = "EXTERIOR::Terrace::Ramp";
        public const string TerraceOuter = "EXTERIOR::Terrace::Outer circle";
        public const string Plates = "Verbindung auf Stahlbau::Platte";
        public const string Dowels = "Verbindung auf Stahlbau::Dübel";
        public const string StairRail = "INTERIOR::Stair::Rail";
        public const string StairFence = "INTERIOR::Stair::Fence";

        /// <summary>The model's own layers all sit under this root. Tutorials never touch them.</summary>
        public const string ModelRoot = "0_3DMODEL";

        /// <summary>
        /// True when the layer's full path ends with <paramref name="fragment"/> at a "::" boundary,
        /// or the layer is a sublayer of such a layer.
        /// </summary>
        public static bool Matches(string fullPath, string fragment)
        {
            if (string.Equals(fullPath, fragment, StringComparison.Ordinal))
                return true;
            if (fullPath.EndsWith("::" + fragment, StringComparison.Ordinal))
                return true;
            string inner = fragment + "::";
            int at = fullPath.IndexOf(inner, StringComparison.Ordinal);
            return at == 0 || (at > 0 && fullPath.Substring(0, at).EndsWith("::", StringComparison.Ordinal));
        }

        /// <summary>True for any layer of the original model (under <see cref="ModelRoot"/>).</summary>
        public static bool IsModelLayer(string fullPath) =>
            string.Equals(fullPath, ModelRoot, StringComparison.Ordinal) ||
            fullPath.StartsWith(ModelRoot + "::", StringComparison.Ordinal);
    }

    /// <summary>
    /// Read-only access to the Klosterneuburg geometry in a document. Every lookup returns
    /// duplicated geometry, so nothing a tutorial does can change the original objects.
    /// Get one with <see cref="TryOpen"/>; it is a snapshot of the document at that time.
    /// </summary>
    public sealed class ReferenceModel
    {
        private readonly Dictionary<string, List<(Guid Id, GeometryBase Geometry)>> _byFragment =
            new Dictionary<string, List<(Guid, GeometryBase)>>(StringComparer.Ordinal);
        private readonly Dictionary<double, Surface> _patches = new Dictionary<double, Surface>();
        private readonly Brep _roofBrep;
        private readonly Surface _roof;
        private Point3d? _centre;

        private ReferenceModel(RhinoDoc doc, Brep roofBrep)
        {
            Doc = doc;
            _roofBrep = roofBrep;
            _roof = roofBrep.Faces[0].UnderlyingSurface();
        }

        public RhinoDoc Doc { get; }

        /// <summary>
        /// Finds the Klosterneuburg model in <paramref name="doc"/>. Returns null, with a reason
        /// the learner can read, when the roof layer is missing or doesn't hold one surface.
        /// </summary>
        public static ReferenceModel? TryOpen(RhinoDoc? doc, out string reason)
        {
            if (doc == null)
            {
                reason = "No Rhino document is open.";
                return null;
            }

            var roofObjects = FindObjects(doc, ReferenceLayers.Roof).ToList();
            if (roofObjects.Count == 0)
            {
                reason = "The Klosterneuburg model isn't open: there is no layer ending in \"" + ReferenceLayers.Roof + "\".";
                return null;
            }
            if (roofObjects.Count != 1)
            {
                reason = $"Expected one roof surface on the roof layer, found {roofObjects.Count} objects.";
                return null;
            }

            Brep? brep = AsBrep(roofObjects[0].Geometry);
            if (brep == null || brep.Faces.Count != 1)
            {
                reason = "The roof layer should hold one single-face surface.";
                return null;
            }

            reason = "";
            return new ReferenceModel(doc, brep.DuplicateBrep());
        }

        /// <summary>
        /// The roof as it is in the model (a copy): one face, trimmed to the wavy outline.
        /// Use this for anything the learner sees or works on.
        /// </summary>
        public Brep RoofBrep => _roofBrep.DuplicateBrep();

        /// <summary>
        /// The roof's untrimmed underlying surface (a copy). It reaches past the wavy outline,
        /// so use it only for maths (closest points, isocurves), not for display.
        /// </summary>
        public Surface Roof => (Surface)_roof.Duplicate();

        public IReadOnlyList<GeometryBase> RoofEdge => Get(ReferenceLayers.RoofEdge);
        public IReadOnlyList<GeometryBase> GlassPanels => Get(ReferenceLayers.Glass);
        public IReadOnlyList<GeometryBase> RingBeams => Get(ReferenceLayers.RingBeams);
        public IReadOnlyList<GeometryBase> MainBeams => Get(ReferenceLayers.MainBeamsLong, ReferenceLayers.MainBeamsShort);
        public IReadOnlyList<GeometryBase> Joists => Get(ReferenceLayers.Joists);
        public IReadOnlyList<GeometryBase> Ramp => Get(ReferenceLayers.Ramp);
        public IReadOnlyList<GeometryBase> TerraceOuter => Get(ReferenceLayers.TerraceOuter);
        public IReadOnlyList<GeometryBase> Plates => Get(ReferenceLayers.Plates);
        public IReadOnlyList<GeometryBase> Dowels => Get(ReferenceLayers.Dowels);
        public IReadOnlyList<GeometryBase> StairRails => Get(ReferenceLayers.StairRail);
        public IReadOnlyList<GeometryBase> StairFence => Get(ReferenceLayers.StairFence);

        /// <summary>
        /// The centre of the round pavilion: the plan centre of the facade glass, at Z = 0.
        /// Falls back to the roof's plan centre if there is no glass.
        /// </summary>
        public Point3d BuildingCentre
        {
            get
            {
                if (_centre == null)
                {
                    BoundingBox box = BoundingBox.Empty;
                    foreach (var item in Items(ReferenceLayers.Glass))
                        box.Union(item.Geometry.GetBoundingBox(true));
                    if (!box.IsValid)
                        box = _roofBrep.GetBoundingBox(true);
                    _centre = new Point3d(box.Center.X, box.Center.Y, 0);
                }
                return _centre.Value;
            }
        }

        /// <summary>Copies of every object on layers matching any of the fragments.</summary>
        public IReadOnlyList<GeometryBase> Get(params string[] fragments) =>
            fragments.SelectMany(Items).Select(item => item.Geometry.Duplicate()).ToList();

        /// <summary>Ids of the objects on matching layers, for display only (e.g. highlighting). Never edit them.</summary>
        public IReadOnlyList<Guid> Ids(params string[] fragments) =>
            fragments.SelectMany(Items).Select(item => item.Id).Distinct().ToList();

        /// <summary>Combined bounding box of the objects on matching layers.</summary>
        public BoundingBox Bounds(params string[] fragments)
        {
            BoundingBox box = BoundingBox.Empty;
            foreach (var item in fragments.SelectMany(Items))
                box.Union(item.Geometry.GetBoundingBox(true));
            return box;
        }

        /// <summary>
        /// A square piece of the roof, about <paramref name="size"/> by <paramref name="size"/> in plan,
        /// where the roof rises and falls the most. Chosen by sampling the roof and comparing how far each
        /// candidate square departs from its best-fit plane, so a plain slope doesn't count as a wave.
        /// Only squares that lie wholly inside the roof's trimmed outline are considered.
        /// Returns a copy (display it, or add it to a tutorial layer); the result is cached per size.
        /// </summary>
        public Surface RoofPatch(double size = 6.0)
        {
            if (!_patches.TryGetValue(size, out Surface? patch))
            {
                patch = FindRoofPatch(_roofBrep.Faces[0], size);
                _patches[size] = patch;
            }
            return (Surface)patch.Duplicate();
        }

        private IEnumerable<(Guid Id, GeometryBase Geometry)> Items(string fragment)
        {
            if (!_byFragment.TryGetValue(fragment, out var items))
            {
                items = FindObjects(Doc, fragment).Select(o => (o.Id, o.Geometry)).ToList();
                _byFragment[fragment] = items;
            }
            return items;
        }

        private static IEnumerable<RhinoObject> FindObjects(RhinoDoc doc, string fragment)
        {
            var layers = new HashSet<int>();
            foreach (Layer layer in doc.Layers)
                if (!layer.IsDeleted && ReferenceLayers.Matches(layer.FullPath, fragment))
                    layers.Add(layer.Index);
            if (layers.Count == 0)
                yield break;

            var settings = new ObjectEnumeratorSettings
            {
                HiddenObjects = true,
                LockedObjects = true,
                ReferenceObjects = true,
                NormalObjects = true,
                IncludeLights = false,
                IncludeGrips = false,
            };
            foreach (RhinoObject obj in doc.Objects.GetObjectList(settings))
                if (layers.Contains(obj.Attributes.LayerIndex) && obj.Geometry != null)
                    yield return obj;
        }

        private static Brep? AsBrep(GeometryBase? geometry) => geometry switch
        {
            Brep b => b,
            Extrusion e => e.ToBrep(),
            Surface s => s.ToBrep(),
            _ => null,
        };

        private static Surface FindRoofPatch(BrepFace face, double size)
        {
            const int Samples = 48;
            Surface roof = face.UnderlyingSurface();
            Interval u = roof.Domain(0), v = roof.Domain(1);

            var grid = new Point3d[Samples + 1, Samples + 1];
            var inside = new bool[Samples + 1, Samples + 1];
            for (int i = 0; i <= Samples; i++)
            {
                for (int j = 0; j <= Samples; j++)
                {
                    double pu = u.ParameterAt(i / (double)Samples), pv = v.ParameterAt(j / (double)Samples);
                    grid[i, j] = roof.PointAt(pu, pv);
                    inside[i, j] = face.IsPointOnFace(pu, pv) == PointFaceRelation.Interior;
                }
            }

            // How many samples a window spans in each direction to cover `size` in plan.
            double planU = PlanLength(grid, Samples, alongU: true);
            double planV = PlanLength(grid, Samples, alongU: false);
            int spanU = Clamp((int)Math.Round(Samples * size / Math.Max(planU, 1e-9)), 2, Samples);
            int spanV = Clamp((int)Math.Round(Samples * size / Math.Max(planV, 1e-9)), 2, Samples);

            double best = -1;
            int bestI = 0, bestJ = 0;
            var points = new List<Point3d>((spanU + 1) * (spanV + 1));
            for (int i0 = 0; i0 + spanU <= Samples; i0 += 2)
            {
                for (int j0 = 0; j0 + spanV <= Samples; j0 += 2)
                {
                    points.Clear();
                    bool allInside = true;
                    for (int i = i0; i <= i0 + spanU && allInside; i++)
                    {
                        for (int j = j0; j <= j0 + spanV; j++)
                        {
                            if (!inside[i, j])
                            {
                                allInside = false;
                                break;
                            }
                            points.Add(grid[i, j]);
                        }
                    }
                    if (!allInside)
                        continue;

                    if (Plane.FitPlaneToPoints(points, out Plane plane) != PlaneFitResult.Success)
                        continue;
                    double lo = double.MaxValue, hi = double.MinValue;
                    foreach (Point3d p in points)
                    {
                        double d = plane.DistanceTo(p);
                        lo = Math.Min(lo, d);
                        hi = Math.Max(hi, d);
                    }
                    if (hi - lo > best)
                    {
                        best = hi - lo;
                        bestI = i0;
                        bestJ = j0;
                    }
                }
            }

            if (best < 0)
                throw new InvalidOperationException($"No {size} m square fits inside the roof outline.");

            var subU = new Interval(u.ParameterAt(bestI / (double)Samples), u.ParameterAt((bestI + spanU) / (double)Samples));
            var subV = new Interval(v.ParameterAt(bestJ / (double)Samples), v.ParameterAt((bestJ + spanV) / (double)Samples));
            return roof.Trim(subU, subV) ?? (Surface)roof.Duplicate();
        }

        // Average plan (XY) length of the sample rows running in one parameter direction.
        private static double PlanLength(Point3d[,] grid, int samples, bool alongU)
        {
            double total = 0;
            for (int k = 0; k <= samples; k++)
            {
                double length = 0;
                for (int s = 1; s <= samples; s++)
                {
                    Point3d a = alongU ? grid[s - 1, k] : grid[k, s - 1];
                    Point3d b = alongU ? grid[s, k] : grid[k, s];
                    length += Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
                }
                total += length;
            }
            return total / (samples + 1);
        }

        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
    }
}
