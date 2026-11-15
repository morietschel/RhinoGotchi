using System;
using System.Collections.Generic;
using Rhino.Geometry;

namespace RhinoGotchi.Water
{
    /// <summary>A solid the water bounces off: one triangle mesh and its box.</summary>
    internal sealed class WaterCollider
    {
        public WaterCollider(Mesh mesh, bool hazard, bool movable)
        {
            Mesh = mesh;
            Box = mesh.GetBoundingBox(true);
            Hazard = hazard;
            Movable = movable;
        }

        public Mesh Mesh { get; }

        public BoundingBox Box { get; }

        /// <summary>Nothing may touch this one: not the water, not a piece the player moves.</summary>
        public bool Hazard { get; }

        /// <summary>A piece the player may move (not locked scenery).</summary>
        public bool Movable { get; }
    }

    /// <summary>A volume that collects the drops that reach it. Open shapes fall back to their bounding box.</summary>
    internal sealed class WaterGoal
    {
        private readonly Mesh? _closed;

        public WaterGoal(Mesh mesh)
        {
            Box = mesh.GetBoundingBox(true);
            _closed = mesh.IsClosed ? mesh : null;
        }

        public BoundingBox Box { get; }

        public bool Contains(Point3d p)
        {
            if (!Box.Contains(p))
                return false;
            if (_closed == null)
                return true;
            try
            {
                return _closed.IsPointInside(p, 1e-6, false);
            }
            catch (InvalidOperationException)
            {
                return true; // closed but not manifold: the box is good enough
            }
        }
    }

    /// <summary>
    /// Very basic water: drops are small balls that fall, push each other apart (so they pile up and
    /// fill things) and bounce off meshes. Positions are solved first and velocities follow from how
    /// far a drop really moved, which keeps piles calm. No Rhino document in here.
    /// </summary>
    internal sealed class WaterSim
    {
        private const int Substeps = 4;
        private const double Restitution = 0.3;   // how much of an impact comes back as a bounce
        private const double Friction = 0.02;      // speed lost per substep while touching a solid
        private const double Stiffness = 0.5;      // how hard overlapping drops push apart per pass

        private Point3d[] _p = new Point3d[256];
        private Point3d[] _prev = new Point3d[256];
        private Vector3d[] _v = new Vector3d[256];
        private Vector3d[] _normal = new Vector3d[256]; // contact normal this substep, zero if none
        private double[] _impact = new double[256];     // speed into the solid before the contact
        private int[] _next = new int[256];
        private long[] _born = new long[256];           // spawn order, to find the oldest drop
        private long _clock;
        private readonly Dictionary<long, int> _cells = new Dictionary<long, int>();

        /// <summary>Drop radius in model units. Everything else (gravity, top speed) scales with it.</summary>
        public double Radius = 0.25;

        public int MaxDrops = 2500;

        /// <summary>Keeps the water between two invisible panes at y = -Depth and y = +Depth. 0 = no panes.</summary>
        public double Depth;

        /// <summary>2D water: every drop stays in the plane y = 0.</summary>
        public bool Flat;

        /// <summary>Where drops reached a goal during the last Step.</summary>
        public readonly List<Point3d> Arrivals = new List<Point3d>();

        /// <summary>Where a drop touched a hazard during the last Step, if one did.</summary>
        public Point3d? HazardHit;

        public List<WaterCollider> Colliders = new List<WaterCollider>();
        public List<WaterGoal> Goals = new List<WaterGoal>();

        /// <summary>Drops that leave this box are gone (spilled).</summary>
        public BoundingBox Bounds = BoundingBox.Empty;

        public int Count { get; private set; }

        public int Poured { get; private set; }

        public int Spilled { get; private set; }

        /// <summary>Drops a goal has collected so far.</summary>
        public int InGoal { get; private set; }

        public double Gravity => Radius * 150;

        /// <summary>The fastest a drop may go: under one radius per substep, so it can't skip through a wall.</summary>
        public double MaxSpeed(double frame) => 0.8 * Radius / (frame / Substeps);

        public void Clear()
        {
            Count = Poured = Spilled = InGoal = 0;
            Arrivals.Clear();
            HazardHit = null;
        }

        public void Spawn(Point3d position, Vector3d velocity)
        {
            int i = Count;
            if (Count >= MaxDrops)
            {
                // Full: the oldest drop makes room, so a source never runs dry.
                i = 0;
                for (int k = 1; k < Count; k++)
                    if (_born[k] < _born[i])
                        i = k;
            }
            else
            {
                if (Count == _p.Length)
                    Grow();
                Count++;
            }
            _p[i] = _prev[i] = position;
            _v[i] = velocity;
            _born[i] = _clock++;
            Poured++;
        }

        /// <summary>Moves the drops in <paramref name="box"/> along with a solid that was just moved, so a cup carries its water.</summary>
        public void Carry(BoundingBox box, Transform xform)
        {
            for (int i = 0; i < Count; i++)
            {
                if (!box.Contains(_p[i]))
                    continue;
                Point3d p = _p[i];
                p.Transform(xform);
                _p[i] = _prev[i] = p;
                Vector3d v = _v[i];
                v.Transform(xform);
                _v[i] = v;
            }
        }

        /// <summary>The drops sorted into groups from slow to fast, for drawing calm water darker than spray.</summary>
        public Point3d[][] SnapshotBySpeed(int groups)
        {
            var lists = new List<Point3d>[groups];
            for (int g = 0; g < groups; g++)
                lists[g] = new List<Point3d>();
            double fast = Radius * 60;
            for (int i = 0; i < Count; i++)
            {
                int g = (int)(_v[i].Length / fast * groups);
                lists[g >= groups ? groups - 1 : g].Add(_p[i]);
            }
            var result = new Point3d[groups][];
            for (int g = 0; g < groups; g++)
                result[g] = lists[g].ToArray();
            return result;
        }

        public void Step(double frame)
        {
            double h = frame / Substeps;
            double maxStep = 0.8 * Radius;
            double maxSpeed = maxStep / h;
            double g = Gravity;
            Arrivals.Clear();
            HazardHit = null;

            for (int s = 0; s < Substeps; s++)
            {
                for (int i = 0; i < Count; i++)
                {
                    Vector3d v = _v[i];
                    v.Z -= g * h;
                    Clamp(ref v, maxSpeed);
                    _v[i] = v;
                    _prev[i] = _p[i];
                    _p[i] += v * h;
                    _normal[i] = Vector3d.Zero;
                }

                BuildCells();
                Separate();
                Separate();

                for (int i = 0; i < Count; i++)
                {
                    // Never move more than part of a radius before the walls get their say.
                    Vector3d d = _p[i] - _prev[i];
                    if (Clamp(ref d, maxStep))
                        _p[i] = _prev[i] + d;
                    Collide(i);
                    if (Flat)
                        _p[i].Y = 0;
                    else if (Depth > 0)
                    {
                        double pane = Math.Max(0, Depth - Radius);
                        if (_p[i].Y > pane) _p[i].Y = pane;
                        else if (_p[i].Y < -pane) _p[i].Y = -pane;
                    }

                    Vector3d v = (_p[i] - _prev[i]) / h;
                    Vector3d n = _normal[i];
                    if (!n.IsZero)
                    {
                        double along = v * n;
                        double bounce = _impact[i] * Restitution;
                        if (_impact[i] > Radius * 4 && bounce > along)
                            v += n * (bounce - along);
                        v *= 1 - Friction;
                    }
                    Clamp(ref v, maxSpeed);
                    _v[i] = v;
                }
            }

            Sweep();
        }

        // ---------------- drops against drops ----------------

        private void BuildCells()
        {
            _cells.Clear();
            double size = Radius * 2;
            for (int i = 0; i < Count; i++)
            {
                long key = Key(Cell(_p[i].X, size), Cell(_p[i].Y, size), Cell(_p[i].Z, size));
                _next[i] = _cells.TryGetValue(key, out int head) ? head : -1;
                _cells[key] = i;
            }
        }

        private void Separate()
        {
            double size = Radius * 2;
            double rest2 = size * size;
            for (int i = 0; i < Count; i++)
            {
                int cx = Cell(_p[i].X, size), cy = Cell(_p[i].Y, size), cz = Cell(_p[i].Z, size);
                for (int x = cx - 1; x <= cx + 1; x++)
                for (int y = cy - 1; y <= cy + 1; y++)
                for (int z = cz - 1; z <= cz + 1; z++)
                {
                    if (!_cells.TryGetValue(Key(x, y, z), out int j))
                        continue;
                    for (; j >= 0; j = _next[j])
                    {
                        if (j <= i)
                            continue;
                        Vector3d d = _p[i] - _p[j];
                        double dist2 = d.SquareLength;
                        if (dist2 >= rest2)
                            continue;
                        double dist = Math.Sqrt(dist2);
                        if (dist < 1e-9)
                        {
                            // Two drops on the same spot: nudge them apart in a direction that depends on who they are.
                            d = new Vector3d(Math.Cos(i * 2.4), Math.Sin(i * 2.4), 0.3);
                            dist = d.Length;
                            d *= Radius * 0.01 / dist;
                            dist = Radius * 0.01;
                        }
                        Vector3d push = d * (0.5 * Stiffness * (size - dist) / dist);
                        _p[i] += push;
                        _p[j] -= push;
                        // Only half of the push turns into speed; otherwise crowded drops fly apart like sparks.
                        _prev[i] += push * 0.5;
                        _prev[j] -= push * 0.5;
                    }
                }
            }
        }

        private static int Cell(double v, double size) => (int)Math.Floor(v / size);

        private static long Key(int x, int y, int z) => (x * 73856093L) ^ (y * 19349663L) ^ (z * 83492791L);

        // ---------------- drops against solids ----------------

        private void Collide(int i)
        {
            double r = Radius;
            for (int c = 0; c < Colliders.Count; c++)
            {
                WaterCollider collider = Colliders[c];
                Point3d p = _p[i];
                BoundingBox box = collider.Box;
                if (p.X < box.Min.X - r || p.X > box.Max.X + r ||
                    p.Y < box.Min.Y - r || p.Y > box.Max.Y + r ||
                    p.Z < box.Min.Z - r || p.Z > box.Max.Z + r)
                    continue;

                MeshPoint? hit = collider.Mesh.ClosestMeshPoint(p, r);
                if (hit == null)
                    continue;
                if (collider.Hazard && HazardHit == null)
                    HazardHit = hit.Point;

                // Push the drop out on the side it is on, so a single surface is solid from both sides.
                Vector3d n = p - hit.Point;
                double dist = n.Length;
                if (dist < 1e-9)
                    n = Vector3d.ZAxis;
                else
                    n /= dist;

                _p[i] = hit.Point + n * r;
                _normal[i] = n;
                _impact[i] = Math.Max(0, -(_v[i] * n));
            }
        }

        // ---------------- goals and spills ----------------

        private void Sweep()
        {
            bool bounded = Bounds.IsValid;
            for (int i = Count - 1; i >= 0; i--)
            {
                Point3d p = _p[i];
                if (bounded && !Bounds.Contains(p))
                {
                    Spilled++;
                    RemoveAt(i);
                    continue;
                }

                for (int k = 0; k < Goals.Count; k++)
                {
                    if (!Goals[k].Contains(p))
                        continue;
                    InGoal++;
                    Arrivals.Add(p);
                    RemoveAt(i);
                    break;
                }
            }
        }

        private void RemoveAt(int i)
        {
            int last = --Count;
            _p[i] = _p[last];
            _prev[i] = _prev[last];
            _v[i] = _v[last];
            _born[i] = _born[last];
        }

        private static bool Clamp(ref Vector3d v, double max)
        {
            double length2 = v.SquareLength;
            if (length2 <= max * max)
                return false;
            v *= max / Math.Sqrt(length2);
            return true;
        }

        private void Grow()
        {
            int size = _p.Length * 2;
            Array.Resize(ref _p, size);
            Array.Resize(ref _prev, size);
            Array.Resize(ref _v, size);
            Array.Resize(ref _normal, size);
            Array.Resize(ref _impact, size);
            Array.Resize(ref _next, size);
            Array.Resize(ref _born, size);
        }
    }
}
