using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Eto.Forms;
using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

namespace RhinoGotchi.Water
{
    /// <summary>
    /// The water itself. It pours from tagged source objects, every other visible object in the model
    /// is solid, tagged goal objects collect the drops that reach them, and tagged hazards must not be
    /// touched. Sources, goals and hazards are ordinary Rhino objects (tagged with a user string), so
    /// they can be moved with the Gumball and are saved with the file. The drops are only drawn.
    /// </summary>
    internal static class WaterLab
    {
        public const string TagKey = "RhinoGotchi.Water";
        public const string SourceTag = "source";
        public const string GoalTag = "goal";
        public const string HazardTag = "hazard";

        private const double Frame = 1.0 / 30;

        private static readonly WaterSim Sim = new WaterSim();
        private static readonly WaterConduit Conduit = new WaterConduit();
        private static readonly Dictionary<uint, Mesh?> MeshCache = new Dictionary<uint, Mesh?>();
        private static readonly Dictionary<Guid, double> Owed = new Dictionary<Guid, double>(); // part-drops each source still owes
        private static readonly Random Random = new Random(7);
        private static readonly List<Source> Sources = new List<Source>();

        private static UITimer? _timer;
        private static RhinoDoc? _doc;
        private static bool _dirty;
        private static bool _subscribed;
        private static int _tick;
        private static BoundingBox _scene = BoundingBox.Empty;
        private static Point3d? _pieceOnHazard; // where a movable piece crosses a hazard, as of the last Rebuild

        // ---------------- settings ----------------

        /// <summary>Drops per second from each source.</summary>
        public static int Rate = 60;

        /// <summary>How many drops the goal needs. The progress bar is full when it has them.</summary>
        public static int Target = 300;

        /// <summary>Drop radius in model units. 0 = pick one that suits the size of the scene.</summary>
        public static double DropSize;

        /// <summary>Half the depth of the scene: water stays between two invisible panes this far from y = 0. 0 = no panes.</summary>
        public static double Depth
        {
            get => Sim.Depth;
            set => Sim.Depth = value;
        }

        /// <summary>2D water: every drop stays in the plane y = 0.</summary>
        public static bool Flat
        {
            get => Sim.Flat;
            set => Sim.Flat = value;
        }

        // ---------------- state ----------------

        /// <summary>Raised a few times a second while water runs, and whenever the water starts, stops or resets.</summary>
        public static event Action? Changed;

        /// <summary>Raised once, when the goal has collected its amount.</summary>
        public static event Action? GoalReached;

        /// <summary>Raised (every frame it is true) when water or a movable piece touches a hazard: what touched, and where.</summary>
        public static event Action<string, Point3d>? HazardTouched;

        public static bool Running => _timer != null;

        public static int InGoal => Sim.InGoal;

        public static int Spilled => Sim.Spilled;

        public static int Flowing => Sim.Count;

        public static int SourceCount => Sources.Count;

        public static int GoalCount => Sim.Goals.Count;

        public static double Radius => Sim.Radius;

        /// <summary>How full the goal is, 0 to 1.</summary>
        public static double Progress => Target <= 0 ? 1 : Math.Min(1.0, (double)Sim.InGoal / Target);

        public static bool Reached => Sim.InGoal >= Target;

        public static string ScoreLine() =>
            Reached ? $"Goal reached! {Sim.InGoal} / {Target} drops" : $"{Sim.InGoal} / {Target} drops";

        // ---------------- controls ----------------

        public static void Start(RhinoDoc doc)
        {
            if (_doc != null && _doc != doc)
                Stop();
            if (Running)
                return; // paused water (same document) carries on from where it was

            _doc = doc;
            Subscribe();
            Rebuild();
            if (Sim.Count == 0)
                PickDropSize();
            Conduit.Enabled = true;
            _timer = new UITimer { Interval = Frame };
            _timer.Elapsed += OnTick;
            _timer.Start();
            Changed?.Invoke();
        }

        /// <summary>Pauses the water where it is. Start carries on.</summary>
        public static void Pause()
        {
            if (_timer == null)
                return;
            _timer.Stop();
            _timer.Elapsed -= OnTick;
            _timer.Dispose();
            _timer = null;
            Changed?.Invoke();
        }

        /// <summary>Stops and removes the water. Sources, goals and the model stay.</summary>
        public static void Stop()
        {
            Pause();
            Unsubscribe();
            Conduit.Enabled = false;
            Sim.Clear();
            Owed.Clear();
            Conduit.Clear();
            _doc?.Views.Redraw();
            _doc = null;
            Changed?.Invoke();
        }

        /// <summary>Removes the water and empties the goal, for another try.</summary>
        public static void Reset()
        {
            Sim.Clear();
            Owed.Clear();
            Conduit.Clear();
            if (_doc != null)
            {
                Rebuild();
                PickDropSize();
                _doc.Views.Redraw();
            }
            Changed?.Invoke();
        }

        public static void MarkDirty() => _dirty = true;

        /// <summary>A big red message in the viewport (a failed level), with a cross where it went wrong. Reset clears it.</summary>
        public static void ShowAlert(string text, Point3d where)
        {
            Conduit.Alert = text;
            Conduit.AlertPoint = where;
            _doc?.Views.Redraw();
        }

        // ---------------- the loop ----------------

        private static void OnTick(object? sender, EventArgs e)
        {
            RhinoDoc? doc = _doc;
            if (doc == null)
                return;
            try
            {
                if (_dirty)
                    Rebuild();

                foreach (Source source in Sources)
                {
                    Owed.TryGetValue(source.Id, out double owed);
                    owed += Rate * Frame;
                    for (; owed >= 1; owed -= 1)
                        Pour(source);
                    Owed[source.Id] = owed;
                }

                bool reachedBefore = Reached;
                Sim.Step(Frame);
                Conduit.Advance(Frame);
                foreach (Point3d arrival in Sim.Arrivals)
                    Conduit.Pop(arrival);
                if (Sim.Arrivals.Count > 0)
                    Conduit.SetProgress(Progress, Reached);
                if (Reached && !reachedBefore)
                {
                    Conduit.Celebrate(Random);
                    GoalReached?.Invoke();
                }
                Conduit.Drops = Sim.SnapshotBySpeed(WaterConduit.Shades);
                Conduit.Hud = ScoreLine();

                if (_pieceOnHazard is Point3d crossing)
                    HazardTouched?.Invoke("A piece touched a red block", crossing);
                else if (Sim.HazardHit is Point3d splash)
                    HazardTouched?.Invoke("Water touched a red block", splash);

                doc.Views.Redraw();
                if (++_tick % 6 == 0)
                    Changed?.Invoke();
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine("RhinoGotchi: the water stopped ({0}).", ex.Message);
                Stop();
            }
        }

        /// <summary>
        /// Adds one drop at a source. Drops leave with some speed, spread over a nozzle just wide enough
        /// for the flow, so new drops don't land on top of each other and burst apart.
        /// </summary>
        private static void Pour(Source source)
        {
            double r = Sim.Radius;
            Vector3d direction = source.Velocity;
            double speed = direction.Length;
            if (speed < 1e-9)
            {
                direction = -Vector3d.ZAxis; // a point source drips straight down
                speed = r * 40;
            }
            else
            {
                direction /= speed;
                speed = Math.Max(r * 10, Math.Min(Sim.MaxSpeed(Frame), speed));
            }

            Point3d position;
            if (Sim.Flat)
            {
                // In 2D the nozzle is a slot across the stream, in the plane of the level.
                Vector3d across = Vector3d.CrossProduct(direction, Vector3d.YAxis);
                if (!across.Unitize())
                    across = Vector3d.XAxis;
                double half = r * Math.Max(0.5, Math.Min(8, 2.4 * r * Rate / speed));
                position = source.Position + across * ((Random.NextDouble() * 2 - 1) * half);
                position.Y = 0;
                direction.Y = 0;
            }
            else
            {
                double nozzle = r * Math.Sqrt(Math.Max(0.25, Math.Min(16, 3 * r * Rate / speed)));
                double angle = Random.NextDouble() * Math.PI * 2;
                double offset = nozzle * Math.Sqrt(Random.NextDouble());
                position = new Plane(source.Position, direction).PointAt(Math.Cos(angle) * offset, Math.Sin(angle) * offset);
            }
            position += direction * (Random.NextDouble() * speed * Frame);
            Sim.Spawn(position, direction * speed);
        }

        // ---------------- reading the model ----------------

        /// <summary>Reads the model again: which objects are sources, goals, hazards and solids.</summary>
        private static void Rebuild()
        {
            _dirty = false;
            RhinoDoc? doc = _doc;
            if (doc == null)
                return;

            var colliders = new List<WaterCollider>();
            var goals = new List<WaterGoal>();
            var seen = new HashSet<uint>();
            BoundingBox scene = BoundingBox.Empty;
            Sources.Clear();

            var settings = new ObjectEnumeratorSettings
            {
                NormalObjects = true,
                LockedObjects = true,
                HiddenObjects = false,
                DeletedObjects = false,
                IncludeLights = false,
                IncludeGrips = false,
            };
            foreach (RhinoObject obj in doc.Objects.GetObjectList(settings))
            {
                if (!obj.Visible)
                    continue;
                string? tag = obj.Attributes.GetUserString(TagKey);

                if (tag == SourceTag)
                {
                    if (ReadSource(obj) is Source source)
                    {
                        Sources.Add(source);
                        scene.Union(source.Position);
                    }
                    continue;
                }

                uint serial = obj.RuntimeSerialNumber;
                seen.Add(serial);
                if (!MeshCache.TryGetValue(serial, out Mesh? mesh))
                    MeshCache[serial] = mesh = ToMesh(obj.Geometry);
                if (mesh == null)
                    continue;

                scene.Union(mesh.GetBoundingBox(false));
                if (tag == GoalTag)
                    goals.Add(new WaterGoal(mesh));
                else
                    colliders.Add(new WaterCollider(mesh, tag == HazardTag,
                        movable: !obj.IsLocked && obj.Attributes.GetUserString(WaterGame.FixedKey) != "1"));
            }

            foreach (uint serial in MeshCache.Keys.Where(k => !seen.Contains(k)).ToList())
                MeshCache.Remove(serial);

            Sim.Colliders = colliders;
            Sim.Goals = goals;
            _pieceOnHazard = FindPieceOnHazard(colliders);

            // Water that falls well clear of the scene is gone.
            _scene = scene;
            BoundingBox bounds = scene.IsValid ? scene : new BoundingBox(new Point3d(-10, -10, -10), new Point3d(10, 10, 10));
            bounds.Inflate(Math.Max(bounds.Diagonal.Length * 0.5, Sim.Radius * 40));
            Sim.Bounds = bounds;
            Conduit.Bounds = bounds;
            Conduit.SourcePoints = Sources.Select(s => s.Position).ToArray();
            Conduit.GoalBoxes = goals.Select(g => g.Box).ToArray();
            Conduit.HazardBoxes = colliders.Where(c => c.Hazard).Select(c => c.Box).ToArray();
            Conduit.Radius = Sim.Radius;
            Conduit.SetProgress(Progress, Reached);
        }

        /// <summary>Where a piece the player can move cuts into a hazard, if one does.</summary>
        private static Point3d? FindPieceOnHazard(List<WaterCollider> colliders)
        {
            foreach (WaterCollider hazard in colliders)
            {
                if (!hazard.Hazard)
                    continue;
                foreach (WaterCollider piece in colliders)
                {
                    if (piece.Hazard || !piece.Movable)
                        continue;
                    BoundingBox overlap = BoundingBox.Intersection(hazard.Box, piece.Box);
                    if (!overlap.IsValid)
                        continue;
                    try
                    {
#pragma warning disable CS0618 // the fast test is all this needs, and it exists in every Rhino 8
                        Line[]? cuts = Intersection.MeshMeshFast(hazard.Mesh, piece.Mesh);
#pragma warning restore CS0618
                        if (cuts != null && cuts.Length > 0)
                            return cuts[0].From;
                        // No crossing surfaces, but the piece may sit wholly inside the hazard.
                        if (hazard.Mesh.IsClosed && hazard.Mesh.IsPointInside(piece.Box.Center, 1e-6, true))
                            return piece.Box.Center;
                    }
                    catch (Exception)
                    {
                        // A mesh Rhino can't intersect is not worth failing the level over.
                    }
                }
            }
            return null;
        }

        private static void PickDropSize()
        {
            double size = DropSize;
            if (size <= 0)
                size = _scene.IsValid && _scene.Diagonal.Length > 1e-6 ? _scene.Diagonal.Length / 120 : 0.25;
            Sim.Radius = size;
            Conduit.Radius = size;
            _dirty = true; // the spill box depends on the drop size
        }

        private static Source? ReadSource(RhinoObject obj)
        {
            switch (obj.Geometry)
            {
                case Rhino.Geometry.Point point:
                    return new Source(obj.Id, point.Location, Vector3d.Zero);
                case Curve curve:
                    // An arrow: water leaves its start, toward its end. A longer arrow is a stronger jet.
                    return new Source(obj.Id, curve.PointAtStart, (curve.PointAtEnd - curve.PointAtStart) * 3);
                default:
                    return null;
            }
        }

        /// <summary>One triangle mesh for anything with a surface. Curves, points, text and blocks are not solid.</summary>
        private static Mesh? ToMesh(GeometryBase geometry)
        {
            try
            {
                Mesh? mesh = null;
                switch (geometry)
                {
                    case Mesh m:
                        mesh = m.DuplicateMesh();
                        break;
                    case Extrusion extrusion:
                        mesh = FromBrep(extrusion.ToBrep());
                        break;
                    case Brep brep:
                        mesh = FromBrep(brep);
                        break;
                    case Surface surface:
                        mesh = FromBrep(surface.ToBrep());
                        break;
                    case SubD subd:
                        mesh = Mesh.CreateFromSubD(subd, 2);
                        break;
                }
                if (mesh == null || mesh.Faces.Count == 0)
                    return null;
                mesh.Faces.ConvertQuadsToTriangles();
                mesh.Compact();
                return mesh;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static Mesh? FromBrep(Brep? brep)
        {
            if (brep == null)
                return null;
            Mesh[]? parts = Mesh.CreateFromBrep(brep, MeshingParameters.FastRenderMesh);
            if (parts == null || parts.Length == 0)
                return null;
            var joined = new Mesh();
            foreach (Mesh part in parts)
                joined.Append(part);
            // Weld the faces together so a closed solid gives a closed mesh (needed for the goal's inside test).
            joined.Vertices.CombineIdentical(true, true);
            return joined;
        }

        // ---------------- model events ----------------

        private static void Subscribe()
        {
            if (_subscribed)
                return;
            _subscribed = true;
            RhinoDoc.AddRhinoObject += OnObjectChanged;
            RhinoDoc.DeleteRhinoObject += OnObjectChanged;
            RhinoDoc.UndeleteRhinoObject += OnObjectChanged;
            RhinoDoc.ReplaceRhinoObject += OnReplaceObject;
            RhinoDoc.ModifyObjectAttributes += OnAttributesChanged;
            RhinoDoc.LayerTableEvent += OnLayersChanged;
            RhinoDoc.CloseDocument += OnCloseDocument;
        }

        private static void Unsubscribe()
        {
            if (!_subscribed)
                return;
            _subscribed = false;
            RhinoDoc.AddRhinoObject -= OnObjectChanged;
            RhinoDoc.DeleteRhinoObject -= OnObjectChanged;
            RhinoDoc.UndeleteRhinoObject -= OnObjectChanged;
            RhinoDoc.ReplaceRhinoObject -= OnReplaceObject;
            RhinoDoc.ModifyObjectAttributes -= OnAttributesChanged;
            RhinoDoc.LayerTableEvent -= OnLayersChanged;
            RhinoDoc.CloseDocument -= OnCloseDocument;
        }

        private static void OnObjectChanged(object? sender, RhinoObjectEventArgs e) => _dirty = true;

        private static void OnAttributesChanged(object? sender, RhinoModifyObjectAttributesEventArgs e) => _dirty = true;

        private static void OnLayersChanged(object? sender, Rhino.DocObjects.Tables.LayerTableEventArgs e) => _dirty = true;

        private static void OnCloseDocument(object? sender, DocumentEventArgs e)
        {
            if (e.Document == _doc)
                Stop();
        }

        /// <summary>
        /// Moving an object (Gumball, Move, Rotate...) replaces it. Work out how it moved and take the
        /// water inside it along, so a filled cup can be carried and tipped out.
        /// </summary>
        private static void OnReplaceObject(object? sender, RhinoReplaceObjectEventArgs e)
        {
            _dirty = true;
            try
            {
                if (e.Document != _doc || Sim.Count == 0)
                    return;
                RhinoObject before = e.OldRhinoObject, after = e.NewRhinoObject;
                if (before?.Geometry == null || after?.Geometry == null)
                    return;
                if (!string.IsNullOrEmpty(before.Attributes.GetUserString(TagKey)))
                    return; // sources, goals and hazards hold no water

                BoundingBox box = before.Geometry.GetBoundingBox(true);
                if (!box.IsValid || !TryFindMotion(before.Geometry, after.Geometry, out Transform motion) || motion.IsIdentity)
                    return;
                box.Inflate(Sim.Radius * 2);
                Sim.Carry(box, motion);
            }
            catch (Exception)
            {
                // Carrying water is a nicety; never let it break an edit.
            }
        }

        /// <summary>The rigid motion that takes one shape to the other, read from three of its corners.</summary>
        private static bool TryFindMotion(GeometryBase before, GeometryBase after, out Transform motion)
        {
            motion = Transform.Identity;
            BoundingBox a = before.GetBoundingBox(true), b = after.GetBoundingBox(true);
            if (!a.IsValid || !b.IsValid)
                return false;
            double size = a.Diagonal.Length;
            double tolerance = Math.Max(size * 1e-5, 1e-9);

            List<Point3d>? from = Corners(before), to = Corners(after);
            if (from != null && to != null && from.Count == to.Count && from.Count >= 3)
            {
                int i0 = 0, i1 = 0, i2 = -1;
                double best = 0;
                for (int i = 1; i < from.Count; i++)
                {
                    double d = from[i].DistanceTo(from[i0]);
                    if (d > best) { best = d; i1 = i; }
                }
                if (best > tolerance)
                {
                    var axis = new Line(from[i0], from[i1]);
                    best = size * 1e-3;
                    for (int i = 0; i < from.Count; i++)
                    {
                        double d = axis.DistanceTo(from[i], false);
                        if (d > best) { best = d; i2 = i; }
                    }
                }
                if (i2 >= 0
                    && Math.Abs(from[i0].DistanceTo(from[i1]) - to[i0].DistanceTo(to[i1])) < tolerance
                    && Math.Abs(from[i0].DistanceTo(from[i2]) - to[i0].DistanceTo(to[i2])) < tolerance
                    && Math.Abs(from[i1].DistanceTo(from[i2]) - to[i1].DistanceTo(to[i2])) < tolerance)
                {
                    var planeFrom = new Plane(from[i0], from[i1], from[i2]);
                    var planeTo = new Plane(to[i0], to[i1], to[i2]);
                    if (planeFrom.IsValid && planeTo.IsValid)
                    {
                        motion = Transform.PlaneToPlane(planeFrom, planeTo);
                        return true;
                    }
                }
            }

            // No usable corners (a sphere, say): at least follow a plain move.
            if (Math.Abs(size - b.Diagonal.Length) > tolerance)
                return false;
            motion = Transform.Translation(b.Center - a.Center);
            return true;
        }

        private static List<Point3d>? Corners(GeometryBase geometry)
        {
            switch (geometry)
            {
                case Extrusion extrusion:
                    return extrusion.ToBrep()?.Vertices.Select(v => v.Location).ToList();
                case Brep brep:
                    return brep.Vertices.Select(v => v.Location).ToList();
                case Mesh mesh:
                    var points = new List<Point3d>();
                    int step = Math.Max(1, mesh.Vertices.Count / 200);
                    for (int i = 0; i < mesh.Vertices.Count; i += step)
                        points.Add(mesh.Vertices.Point3dAt(i));
                    return points;
                default:
                    return null;
            }
        }

        // ---------------- pieces ----------------

        private sealed class Source
        {
            public Source(Guid id, Point3d position, Vector3d velocity)
            {
                Id = id;
                Position = position;
                Velocity = velocity;
            }

            public Guid Id { get; }

            public Point3d Position { get; }

            public Vector3d Velocity { get; }
        }

        /// <summary>
        /// Draws the water (calm drops dark, fast drops pale, each with a small highlight), the sources,
        /// the goals filling up, a sparkle for every drop that arrives, the hazards, and the progress bar.
        /// </summary>
        private sealed class WaterConduit : DisplayConduit
        {
            public const int Shades = 4;
            private const double PopLife = 0.45;
            private const int MaxPops = 160;

            private static readonly Color[] WaterColors =
            {
                Color.FromArgb(20, 96, 230), Color.FromArgb(38, 136, 250), Color.FromArgb(96, 180, 255), Color.FromArgb(176, 222, 255),
            };
            private static readonly Color Highlight = Color.FromArgb(232, 246, 255);
            private static readonly Color SourceColor = Color.FromArgb(40, 140, 255);
            private static readonly Color GoalColor = Color.FromArgb(40, 190, 110);
            private static readonly Color HazardColor = Color.FromArgb(230, 55, 45);
            private static readonly Color Gold = Color.FromArgb(255, 204, 51);
            private static readonly Color BarBack = Color.FromArgb(235, 238, 242);
            private static readonly Color BarFrame = Color.FromArgb(60, 66, 74);
            private readonly DisplayMaterial _fillMaterial = new DisplayMaterial(Color.FromArgb(60, 160, 255), 0.45);
            private readonly DisplayMaterial _doneMaterial = new DisplayMaterial(Color.FromArgb(60, 210, 120), 0.4);

            private readonly List<Sparkle> _pops = new List<Sparkle>();
            private Mesh[] _fill = Array.Empty<Mesh>();
            private double _progress;
            private bool _reached;
            private double _pulse; // 1 right after a drop arrives, fading to 0

            public Point3d[][] Drops = Array.Empty<Point3d[]>();
            public Point3d[] SourcePoints = Array.Empty<Point3d>();
            public BoundingBox[] GoalBoxes = Array.Empty<BoundingBox>();
            public BoundingBox[] HazardBoxes = Array.Empty<BoundingBox>();
            public BoundingBox Bounds = BoundingBox.Empty;
            public double Radius = 0.25;
            public string Hud = "";
            public string Alert = "";
            public Point3d AlertPoint;

            public void Clear()
            {
                Drops = Array.Empty<Point3d[]>();
                _pops.Clear();
                _pulse = 0;
                Alert = "";
                SetProgress(0, false);
            }

            public void Advance(double seconds)
            {
                _pulse = Math.Max(0, _pulse - seconds * 4);
                for (int i = _pops.Count - 1; i >= 0; i--)
                {
                    Sparkle pop = _pops[i];
                    pop.Age += seconds;
                    if (pop.Age > PopLife)
                        _pops.RemoveAt(i);
                    else
                        _pops[i] = pop;
                }
            }

            /// <summary>A drop arrived: a sparkle where it landed and a pulse on the goal and the bar.</summary>
            public void Pop(Point3d where)
            {
                _pulse = 1;
                if (_pops.Count >= MaxPops)
                    _pops.RemoveAt(0);
                _pops.Add(new Sparkle { Where = where });
            }

            /// <summary>The goal amount was reached: a shower of big sparkles all over the goals.</summary>
            public void Celebrate(Random random)
            {
                foreach (BoundingBox box in GoalBoxes)
                    for (int i = 0; i < 30; i++)
                        _pops.Add(new Sparkle
                        {
                            Where = box.PointAt(random.NextDouble(), random.NextDouble(), random.NextDouble()),
                            Age = -random.NextDouble() * 0.8, // they go off one after another
                            Big = true,
                        });
            }

            /// <summary>The goals fill with water as the count goes up.</summary>
            public void SetProgress(double progress, bool reached)
            {
                _progress = progress;
                _reached = reached;
                var fill = new List<Mesh>();
                if (progress > 0)
                {
                    foreach (BoundingBox box in GoalBoxes)
                    {
                        Vector3d inset = box.Diagonal * 0.02;
                        Point3d low = box.Min + inset;
                        var high = new Point3d(box.Max.X - inset.X, box.Max.Y - inset.Y, low.Z + (box.Max.Z - inset.Z - low.Z) * progress);
                        Mesh? mesh = Mesh.CreateFromBox(new BoundingBox(low, high), 1, 1, 1);
                        if (mesh != null)
                            fill.Add(mesh);
                    }
                }
                _fill = fill.ToArray();
            }

            protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e)
            {
                if (Bounds.IsValid)
                    e.IncludeBoundingBox(Bounds);
            }

            protected override void PostDrawObjects(DrawEventArgs e)
            {
                DisplayPipeline d = e.Display;
                Color goalColor = Blend(_reached ? Gold : GoalColor, Color.White, _pulse * 0.6);
                foreach (BoundingBox box in GoalBoxes)
                    d.DrawBox(box, goalColor, 2 + (int)Math.Round(_pulse * 3));
                foreach (Mesh mesh in _fill)
                    d.DrawMeshShaded(mesh, _reached ? _doneMaterial : _fillMaterial);
                foreach (BoundingBox box in HazardBoxes)
                    d.DrawBox(box, HazardColor, Alert.Length > 0 ? 5 : 2);
                foreach (Point3d point in SourcePoints)
                    d.DrawPoint(point, PointStyle.RoundControlPoint, 6, SourceColor);

                // Points are sized in pixels, so work out how big a drop is on screen in this view.
                Point3d reference = Bounds.IsValid ? Bounds.Center : Point3d.Origin;
                double pixels = 3;
                if (e.Viewport.GetWorldToScreenScale(reference, out double pixelsPerUnit))
                    pixels = Math.Max(2, Math.Min(40, Radius * pixelsPerUnit));
                Vector3d right = e.Viewport.CameraX, up = e.Viewport.CameraY, toCamera = e.Viewport.CameraZ;

                Point3d[][] drops = Drops;
                for (int shade = 0; shade < drops.Length && shade < WaterColors.Length; shade++)
                    if (drops[shade].Length > 0)
                        d.DrawPoints(drops[shade], PointStyle.RoundSimple, (int)Math.Round(pixels), WaterColors[shade]);

                // A small glint up and to the left of each drop, a touch nearer the camera so it sits on top.
                if (pixels >= 4)
                {
                    Vector3d offset = (up * 0.3 - right * 0.3 + toCamera * 0.7) * Radius;
                    var glints = new List<Point3d>();
                    foreach (Point3d[] group in drops)
                        foreach (Point3d p in group)
                            glints.Add(p + offset);
                    if (glints.Count > 0)
                        d.DrawPoints(glints, PointStyle.RoundSimple, Math.Max(1, (int)Math.Round(pixels * 0.3)), Highlight);
                }

                // Sparkles: a gold dot that rises and shrinks inside a ring that spreads and fades.
                foreach (Sparkle pop in _pops)
                {
                    if (pop.Age < 0)
                        continue;
                    double t = pop.Age / PopLife;
                    double scale = pop.Big ? 2.2 : 1;
                    Point3d at = pop.Where + up * (Radius * scale * (1 + 5 * t)) + toCamera * Radius;
                    int size = (int)Math.Round(pixels * scale * 1.5 * (1 - t)) + 2;
                    d.DrawPoint(at, PointStyle.RoundSimple, size, Gold);
                    d.DrawPoint(at + toCamera * (Radius * 0.5), PointStyle.RoundSimple, Math.Max(1, size / 2), Color.White);
                    var ring = new Circle(new Plane(pop.Where + toCamera * Radius, right, up), Radius * scale * (1.5 + 5 * t));
                    d.DrawCircle(ring, Blend(Gold, Color.White, t), t < 0.5 ? 2 : 1);
                }

                // Where the level was lost.
                if (Alert.Length > 0)
                {
                    d.DrawPoint(AlertPoint + toCamera * Radius * 2, PointStyle.X, 14, HazardColor);
                    d.DrawCircle(new Circle(new Plane(AlertPoint + toCamera * Radius * 2, right, up), Radius * 5), HazardColor, 3);
                }
            }

            protected override void DrawForeground(DrawEventArgs e)
            {
                if (Hud.Length == 0 && Alert.Length == 0)
                    return;

                // The progress bar: it swells a little with every drop and turns gold when the goal is reached.
                int grow = (int)Math.Round(_pulse * 3);
                const int left = 16, top = 40, width = 280, height = 18;
                var back = new Rectangle(left - grow, top - grow, width + grow * 2, height + grow * 2);
                e.Display.Draw2dRectangle(back, BarFrame, 2, BarBack);
                int filled = (int)Math.Round((back.Width - 4) * _progress);
                if (filled > 0)
                {
                    Color color = Blend(_reached ? Gold : GoalColor, Color.White, _pulse * 0.5);
                    e.Display.Draw2dRectangle(new Rectangle(back.X + 2, back.Y + 2, filled, back.Height - 4), color, 0, color);
                }
                e.Display.Draw2dText(Hud, _reached ? Gold : SourceColor, new Point2d(left, top + height + 10), false, 16);
                if (Alert.Length > 0)
                    e.Display.Draw2dText(Alert, HazardColor, new Point2d(left, top + height + 36), false, 26);
            }

            private static Color Blend(Color a, Color b, double t)
            {
                t = Math.Max(0, Math.Min(1, t));
                return Color.FromArgb(
                    (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
            }

            private struct Sparkle
            {
                public Point3d Where;
                public double Age;
                public bool Big;
            }
        }
    }
}
