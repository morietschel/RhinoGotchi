using System;
using System.Collections.Generic;

namespace RhinoGotchi.Water
{
    // Everything in this file is plain C# with no Rhino types, so the water and the levels can be run
    // and tested outside Rhino (see tools/WaterTest).

    /// <summary>A point or a direction.</summary>
    internal struct V3
    {
        public double X, Y, Z;

        public V3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static V3 Zero => default;

        public static V3 UnitX => new V3(1, 0, 0);

        public static V3 UnitY => new V3(0, 1, 0);

        public static V3 UnitZ => new V3(0, 0, 1);

        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

        public double Length2 => X * X + Y * Y + Z * Z;

        public V3 Unit()
        {
            double length = Length;
            return length < 1e-12 ? Zero : this / length;
        }

        public static V3 operator +(V3 a, V3 b) => new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static V3 operator -(V3 a, V3 b) => new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static V3 operator -(V3 a) => new V3(-a.X, -a.Y, -a.Z);

        public static V3 operator *(V3 a, double s) => new V3(a.X * s, a.Y * s, a.Z * s);

        public static V3 operator /(V3 a, double s) => new V3(a.X / s, a.Y / s, a.Z / s);

        public static double Dot(V3 a, V3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static V3 Cross(V3 a, V3 b) => new V3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    }

    /// <summary>A move, a turn or a stretch: p' = M p + T.</summary>
    internal struct Xf
    {
        public double M00, M01, M02, M10, M11, M12, M20, M21, M22;
        public V3 T;

        public static Xf Identity => new Xf { M00 = 1, M11 = 1, M22 = 1 };

        public static Xf Move(V3 by)
        {
            Xf xf = Identity;
            xf.T = by;
            return xf;
        }

        /// <summary>A turn around the line through <paramref name="pivot"/> along <paramref name="axis"/> (right-handed).</summary>
        public static Xf Turn(V3 axis, double radians, V3 pivot)
        {
            V3 k = axis.Unit();
            double c = Math.Cos(radians), s = Math.Sin(radians), t = 1 - c;
            var xf = new Xf
            {
                M00 = c + k.X * k.X * t, M01 = k.X * k.Y * t - k.Z * s, M02 = k.X * k.Z * t + k.Y * s,
                M10 = k.Y * k.X * t + k.Z * s, M11 = c + k.Y * k.Y * t, M12 = k.Y * k.Z * t - k.X * s,
                M20 = k.Z * k.X * t - k.Y * s, M21 = k.Z * k.Y * t + k.X * s, M22 = c + k.Z * k.Z * t,
            };
            xf.T = pivot - xf.Vector(pivot);
            return xf;
        }

        /// <summary>A stretch along the world axes, away from <paramref name="pivot"/>.</summary>
        public static Xf Stretch(V3 pivot, double sx, double sy, double sz)
        {
            var xf = new Xf { M00 = sx, M11 = sy, M22 = sz };
            xf.T = pivot - xf.Vector(pivot);
            return xf;
        }

        public V3 Point(V3 p) => Vector(p) + T;

        public V3 Vector(V3 v) => new V3(
            M00 * v.X + M01 * v.Y + M02 * v.Z,
            M10 * v.X + M11 * v.Y + M12 * v.Z,
            M20 * v.X + M21 * v.Y + M22 * v.Z);
    }

    /// <summary>A box along the world axes.</summary>
    internal struct Box3
    {
        public V3 Min, Max;

        public Box3(V3 min, V3 max)
        {
            Min = min;
            Max = max;
        }

        public static Box3 Empty => new Box3(
            new V3(double.MaxValue, double.MaxValue, double.MaxValue),
            new V3(double.MinValue, double.MinValue, double.MinValue));

        public bool IsValid => Min.X <= Max.X && Min.Y <= Max.Y && Min.Z <= Max.Z;

        public V3 Center => (Min + Max) * 0.5;

        public V3 Size => Max - Min;

        public void Add(V3 p)
        {
            if (p.X < Min.X) Min.X = p.X;
            if (p.Y < Min.Y) Min.Y = p.Y;
            if (p.Z < Min.Z) Min.Z = p.Z;
            if (p.X > Max.X) Max.X = p.X;
            if (p.Y > Max.Y) Max.Y = p.Y;
            if (p.Z > Max.Z) Max.Z = p.Z;
        }

        public void Add(Box3 other)
        {
            if (!other.IsValid)
                return;
            Add(other.Min);
            Add(other.Max);
        }

        public bool Contains(V3 p) =>
            p.X >= Min.X && p.X <= Max.X && p.Y >= Min.Y && p.Y <= Max.Y && p.Z >= Min.Z && p.Z <= Max.Z;

        public Box3 Grown(double by) => new Box3(Min - new V3(by, by, by), Max + new V3(by, by, by));

        public static Box3 Of(IEnumerable<V3> points)
        {
            Box3 box = Empty;
            foreach (V3 p in points)
                box.Add(p);
            return box;
        }
    }

    /// <summary>Triangle helpers shared by solids and goals.</summary>
    internal static class Tri
    {
        public static V3 Closest(V3 p, V3 a, V3 b, V3 c)
        {
            V3 ab = b - a, ac = c - a, ap = p - a;
            double d1 = V3.Dot(ab, ap), d2 = V3.Dot(ac, ap);
            if (d1 <= 0 && d2 <= 0)
                return a;
            V3 bp = p - b;
            double d3 = V3.Dot(ab, bp), d4 = V3.Dot(ac, bp);
            if (d3 >= 0 && d4 <= d3)
                return b;
            double vc = d1 * d4 - d3 * d2;
            if (vc <= 0 && d1 >= 0 && d3 <= 0)
                return a + ab * (d1 / (d1 - d3));
            V3 cp = p - c;
            double d5 = V3.Dot(ab, cp), d6 = V3.Dot(ac, cp);
            if (d6 >= 0 && d5 <= d6)
                return c;
            double vb = d5 * d2 - d1 * d6;
            if (vb <= 0 && d2 >= 0 && d6 <= 0)
                return a + ac * (d2 / (d2 - d6));
            double va = d3 * d6 - d5 * d4;
            if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0)
                return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
            double denominator = 1 / (va + vb + vc);
            return a + ab * (vb * denominator) + ac * (vc * denominator);
        }

        /// <summary>Is the point inside a closed set of triangles? Counts how often a ray going up crosses them.</summary>
        public static bool Inside(V3 p, V3[] triangles)
        {
            var direction = new V3(0.0131, 0.0277, 1); // a little off the axis, so it rarely runs along an edge
            int crossings = 0;
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                V3 a = triangles[t], e1 = triangles[t + 1] - a, e2 = triangles[t + 2] - a;
                V3 h = V3.Cross(direction, e2);
                double det = V3.Dot(e1, h);
                if (det > -1e-12 && det < 1e-12)
                    continue;
                double inverse = 1 / det;
                V3 s = p - a;
                double u = V3.Dot(s, h) * inverse;
                if (u < 0 || u > 1)
                    continue;
                V3 q = V3.Cross(s, e1);
                double v = V3.Dot(direction, q) * inverse;
                if (v < 0 || u + v > 1)
                    continue;
                if (V3.Dot(e2, q) * inverse > 0)
                    crossings++;
            }
            return (crossings & 1) == 1;
        }
    }

    /// <summary>A solid the water bounces off: a list of triangles (three points each).</summary>
    internal sealed class WaterCollider
    {
        public WaterCollider(V3[] triangles, bool hazard, bool movable, bool closed = false)
        {
            Triangles = triangles;
            Box = Box3.Of(triangles);
            Hazard = hazard;
            Movable = movable;
            Closed = closed;
        }

        public V3[] Triangles { get; }

        public Box3 Box { get; }

        /// <summary>Nothing may touch this one: not the water, not a piece the player moves.</summary>
        public bool Hazard { get; }

        /// <summary>A piece the player may move (not locked scenery).</summary>
        public bool Movable { get; }

        /// <summary>The triangles are one watertight skin, so "inside" means something.</summary>
        public bool Closed { get; }
    }

    /// <summary>A volume that collects the drops that reach it. Open shapes fall back to their bounding box.</summary>
    internal sealed class WaterGoal
    {
        private readonly V3[]? _closed;

        public WaterGoal(V3[] triangles, bool closed)
        {
            Box = Box3.Of(triangles);
            _closed = closed ? triangles : null;
        }

        public Box3 Box { get; }

        public bool Contains(V3 p) => Box.Contains(p) && (_closed == null || Tri.Inside(p, _closed));
    }

    /// <summary>Where water comes from: a drip straight down, or a jet if it has a direction.</summary>
    internal sealed class WaterSource
    {
        public V3 At;
        /// <summary>Direction and speed of a jet. Zero = a drip straight down.</summary>
        public V3 Jet;
        /// <summary>The part of a drop this source still owes.</summary>
        public double Owed;
        public int Emitted;
    }

    /// <summary>An exact map from a grid cell to a number, cleared in no time. Used for drops and for triangles.</summary>
    internal sealed class CellMap
    {
        private long[] _keys = new long[1024];
        private int[] _values = new int[1024];
        private int[] _stamp = new int[1024];
        private int _generation = 1;
        private int _mask = 1023;

        public static long Key(int x, int y, int z) =>
            ((long)((x + 1048576) & 0x1FFFFF) << 42) | ((long)((y + 1048576) & 0x1FFFFF) << 21) | (long)((z + 1048576) & 0x1FFFFF);

        /// <summary>Empties the map and makes room for <paramref name="entries"/> cells.</summary>
        public void Clear(int entries)
        {
            int size = _keys.Length;
            while (size < entries * 3 && size < 1 << 24)
                size *= 2;
            if (size != _keys.Length)
            {
                _keys = new long[size];
                _values = new int[size];
                _stamp = new int[size];
                _mask = size - 1;
                _generation = 0;
            }
            if (++_generation == int.MaxValue)
            {
                Array.Clear(_stamp, 0, _stamp.Length);
                _generation = 1;
            }
        }

        private int Slot(long key) => (int)((ulong)(key * unchecked((long)0x9E3779B97F4A7C15)) >> 40) & _mask;

        public bool TryGet(long key, out int value)
        {
            for (int s = Slot(key); _stamp[s] == _generation; s = (s + 1) & _mask)
            {
                if (_keys[s] == key)
                {
                    value = _values[s];
                    return true;
                }
            }
            value = -1;
            return false;
        }

        public void Set(long key, int value)
        {
            int s = Slot(key);
            while (_stamp[s] == _generation && _keys[s] != key)
                s = (s + 1) & _mask;
            _stamp[s] = _generation;
            _keys[s] = key;
            _values[s] = value;
        }
    }

    /// <summary>
    /// Very basic water: drops are small balls that fall, push each other apart (so they pile up and
    /// fill things) and slide along triangles. A drop's path is checked in steps shorter than the drop,
    /// so drops can be small and fast without falling through a wall.
    /// </summary>
    internal sealed class WaterSim
    {
        private const int Substeps = 4;
        private const double Restitution = 0.1;   // how much of an impact comes back as a bounce
        private const double Friction = 0.012;     // speed lost per substep while touching a solid
        private const double Stiffness = 0.5;      // how hard overlapping drops push apart per pass
        private const double Viscosity = 0.05;     // how much neighbours even out their speeds: keeps a stream together

        private static readonly int[] Forward = BuildForward();

        private V3[] _p = new V3[1024];
        private V3[] _prev = new V3[1024];
        private V3[] _v = new V3[1024];
        private V3[] _normal = new V3[1024];        // contact normal this substep, zero if none
        private int[] _next = new int[1024];
        private bool[] _burnt = new bool[1024];     // touched a hazard: gone at the end of this step
        private long[] _born = new long[1024];      // spawn order, to find the oldest drop
        private long _clock;
        private readonly CellMap _dropCells = new CellMap();
        private int[] _pairA = new int[4096], _pairB = new int[4096];
        private int _pairs;

        // All triangles of all solids, and a grid that says which ones are near a cell.
        private V3[] _ta = Array.Empty<V3>(), _tb = Array.Empty<V3>(), _tc = Array.Empty<V3>(), _tn = Array.Empty<V3>();
        private bool[] _tHazard = Array.Empty<bool>();
        private readonly CellMap _triCells = new CellMap();
        private int[] _cellStart = Array.Empty<int>(), _cellCount = Array.Empty<int>(), _cellTris = Array.Empty<int>();
        private double _triCell = 1;
        private int[] _near = new int[256];

        /// <summary>Drop radius in model units. Gravity and speeds scale with it.</summary>
        public double Radius = 0.07;

        /// <summary>Drops per second from each source.</summary>
        public double Rate = 160;

        public int MaxDrops = 6000;

        /// <summary>2D water: every drop stays in the plane y = 0.</summary>
        public bool Flat;

        /// <summary>Where drops reached a goal during the last Step.</summary>
        public readonly List<V3> Arrivals = new List<V3>();

        /// <summary>A stray splash on a hazard only sizzles. This many drops and the level is lost.</summary>
        public const int ScorchLimit = 12;

        /// <summary>Where drops touched a hazard (and boiled away) during the last Step.</summary>
        public readonly List<V3> Sizzles = new List<V3>();

        /// <summary>Drops that have boiled away on a hazard so far.</summary>
        public int Scorched { get; private set; }

        public readonly List<WaterSource> Sources = new List<WaterSource>();

        public List<WaterCollider> Colliders { get; private set; } = new List<WaterCollider>();

        public List<WaterGoal> Goals { get; private set; } = new List<WaterGoal>();

        /// <summary>Drops that leave this box are gone (spilled).</summary>
        public Box3 Bounds = Box3.Empty;

        public int Count { get; private set; }

        public int Poured { get; private set; }

        public int Spilled { get; private set; }

        /// <summary>Drops a goal has collected so far.</summary>
        public int InGoal { get; private set; }

        public double Gravity => Radius * 430;

        public double MaxSpeed => Radius * 320;

        /// <summary>The speed of drops that count as spray when the water is drawn.</summary>
        public double FastSpeed => Radius * 170;

        public V3 PositionAt(int i) => _p[i];

        public double SpeedAt(int i) => _v[i].Length;

        public void Clear()
        {
            Count = Poured = Spilled = InGoal = Scorched = 0;
            Arrivals.Clear();
            Sizzles.Clear();
            foreach (WaterSource source in Sources)
                source.Owed = 0;
        }

        public int CountIn(Box3 box)
        {
            int n = 0;
            for (int i = 0; i < Count; i++)
                if (box.Contains(_p[i]))
                    n++;
            return n;
        }

        // ---------------- the scene ----------------

        /// <summary>Sets the solids and goals, and sorts every triangle into a grid for fast lookups.</summary>
        public void SetScene(List<WaterCollider> colliders, List<WaterGoal> goals, Box3 bounds)
        {
            Colliders = colliders;
            Goals = goals;
            Bounds = bounds;

            int count = 0;
            foreach (WaterCollider collider in colliders)
                count += collider.Triangles.Length / 3;
            _ta = new V3[count];
            _tb = new V3[count];
            _tc = new V3[count];
            _tn = new V3[count];
            _tHazard = new bool[count];

            Box3 scene = Box3.Empty;
            foreach (WaterCollider collider in colliders)
                scene.Add(collider.Box);
            double r = Radius;
            _triCell = Math.Max(r * 4, scene.IsValid ? scene.Size.Length / 80 : r * 4);

            var cells = new Dictionary<long, List<int>>();
            int t = 0;
            foreach (WaterCollider collider in colliders)
            {
                V3[] tris = collider.Triangles;
                for (int k = 0; k + 2 < tris.Length; k += 3)
                {
                    V3 a = tris[k], b = tris[k + 1], c = tris[k + 2];
                    V3 normal = V3.Cross(b - a, c - a);
                    if (normal.Length2 < 1e-18)
                        continue; // no area, nothing to hit
                    normal = normal.Unit();
                    _ta[t] = a;
                    _tb[t] = b;
                    _tc[t] = c;
                    _tn[t] = normal;
                    _tHazard[t] = collider.Hazard;

                    Box3 box = Box3.Empty;
                    box.Add(a);
                    box.Add(b);
                    box.Add(c);
                    box = box.Grown(r);
                    int x0 = Cell(box.Min.X, _triCell), x1 = Cell(box.Max.X, _triCell);
                    int y0 = Cell(box.Min.Y, _triCell), y1 = Cell(box.Max.Y, _triCell);
                    int z0 = Cell(box.Min.Z, _triCell), z1 = Cell(box.Max.Z, _triCell);
                    double reach = _triCell * 0.8661 + r; // half a cell's diagonal, plus a drop
                    for (int x = x0; x <= x1; x++)
                    for (int y = y0; y <= y1; y++)
                    for (int z = z0; z <= z1; z++)
                    {
                        // A big sloping triangle only belongs to the cells its plane passes through.
                        var centre = new V3((x + 0.5) * _triCell, (y + 0.5) * _triCell, (z + 0.5) * _triCell);
                        if (Math.Abs(V3.Dot(centre - a, normal)) > reach)
                            continue;
                        long key = CellMap.Key(x, y, z);
                        if (!cells.TryGetValue(key, out List<int>? list))
                            cells[key] = list = new List<int>(4);
                        list.Add(t);
                    }
                    t++;
                }
            }

            _triCells.Clear(cells.Count);
            _cellStart = new int[cells.Count];
            _cellCount = new int[cells.Count];
            int total = 0;
            foreach (List<int> list in cells.Values)
                total += list.Count;
            _cellTris = new int[total];
            int index = 0, at = 0;
            foreach (KeyValuePair<long, List<int>> cell in cells)
            {
                _triCells.Set(cell.Key, index);
                _cellStart[index] = at;
                _cellCount[index] = cell.Value.Count;
                cell.Value.CopyTo(_cellTris, at);
                at += cell.Value.Count;
                index++;
            }

            // Water that ended up inside a solid (a piece was put down on it) is gone.
            for (int i = Count - 1; i >= 0; i--)
            {
                foreach (WaterCollider collider in colliders)
                {
                    if (!collider.Closed || !collider.Box.Contains(_p[i]) || !Tri.Inside(_p[i], collider.Triangles))
                        continue;
                    RemoveAt(i);
                    break;
                }
            }
        }

        // ---------------- drops in, drops along ----------------

        public void Spawn(V3 position, V3 velocity)
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
            if (Flat)
            {
                position.Y = 0;
                velocity.Y = 0;
            }
            _p[i] = _prev[i] = position;
            _v[i] = velocity;
            _burnt[i] = false;
            _born[i] = _clock++;
            Poured++;
        }

        /// <summary>
        /// A steady stream: the drops of one frame leave evenly spaced along the stream, over a nozzle
        /// just wide enough for the flow, in a fixed pattern instead of at random.
        /// </summary>
        private void Emit(double frame)
        {
            double r = Radius;
            foreach (WaterSource source in Sources)
            {
                V3 direction = source.Jet;
                double speed = direction.Length;
                if (speed < 1e-9)
                {
                    direction = new V3(0, 0, -1);
                    speed = r * 80;
                }
                else
                {
                    direction = direction / speed;
                    speed = Math.Max(r * 40, Math.Min(MaxSpeed * 0.8, speed));
                }
                if (Flat)
                {
                    direction.Y = 0;
                    direction = direction.Unit();
                    if (direction.Length2 < 0.5)
                        direction = new V3(0, 0, -1);
                }

                V3 across = V3.Cross(direction, V3.UnitY);
                if (across.Length2 < 1e-6)
                    across = V3.UnitX;
                across = across.Unit();
                V3 other = V3.Cross(direction, across);

                source.Owed += Rate * frame;
                int n = (int)source.Owed;
                source.Owed -= n;
                for (int k = 0; k < n; k++)
                {
                    int id = source.Emitted++;
                    V3 offset;
                    if (Flat)
                    {
                        double half = Math.Max(r * 0.5, Math.Min(r * 8, Rate * 4 * r * r / speed / 2));
                        offset = across * ((id * 0.6180339887 % 1 * 2 - 1) * half);
                    }
                    else
                    {
                        double nozzle = Math.Sqrt(Rate * 6 * r * r * r / speed / Math.PI);
                        nozzle = Math.Max(r * 0.5, Math.Min(r * 6, nozzle));
                        double angle = id * 2.39996323, ring = nozzle * Math.Sqrt((id * 7 % 12 + 0.5) / 12);
                        offset = across * (Math.Cos(angle) * ring) + other * (Math.Sin(angle) * ring);
                    }
                    double age = (k + 0.5) / n * frame; // earlier drops of this frame are already further along
                    Spawn(source.At + offset + direction * (speed * age), direction * speed);
                }
            }
        }

        /// <summary>Moves the drops in <paramref name="box"/> along with a solid that was just moved, so a bucket carries its water.</summary>
        public void Carry(Box3 box, Xf xform)
        {
            for (int i = 0; i < Count; i++)
            {
                if (!box.Contains(_p[i]))
                    continue;
                _p[i] = _prev[i] = xform.Point(_p[i]);
                _v[i] = xform.Vector(_v[i]);
            }
        }

        public void Step(double frame)
        {
            Arrivals.Clear();
            Sizzles.Clear();
            Emit(frame);

            double h = frame / Substeps;
            double maxSpeed = MaxSpeed;
            double g = Gravity;
            double bounceFrom = Radius * 30;

            for (int s = 0; s < Substeps; s++)
            {
                for (int i = 0; i < Count; i++)
                {
                    V3 v = _v[i];
                    v.Z -= g * h;
                    Clamp(ref v, maxSpeed);
                    _v[i] = v;
                    _prev[i] = _p[i];
                    _p[i] += v * h;
                }

                FindPairs();
                Separate();
                Separate();

                for (int i = 0; i < Count; i++)
                {
                    Collide(i);
                    if (Flat)
                        _p[i].Y = 0;

                    V3 v = (_p[i] - _prev[i]) / h;
                    V3 n = _normal[i];
                    if (n.Length2 > 0)
                    {
                        n = n.Unit();
                        double impact = Math.Max(0, -V3.Dot(_v[i], n));
                        double along = V3.Dot(v, n);
                        double bounce = impact * Restitution;
                        if (impact > bounceFrom && bounce > along)
                            v += n * (bounce - along);
                        v = v * (1 - Friction);
                    }
                    Clamp(ref v, maxSpeed);
                    _v[i] = v;
                }

                Calm();
            }

            Sweep();
        }

        // ---------------- drops against drops ----------------

        private static int[] BuildForward()
        {
            // The 13 neighbour cells "after" a cell, so every pair of cells is looked at once.
            var list = new List<int>();
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            for (int z = -1; z <= 1; z++)
                if (x > 0 || (x == 0 && (y > 0 || (y == 0 && z > 0))))
                {
                    list.Add(x);
                    list.Add(y);
                    list.Add(z);
                }
            return list.ToArray();
        }

        /// <summary>Lists every two drops that are close enough to touch during this substep.</summary>
        private void FindPairs()
        {
            double size = Radius * 2.4;
            double reach2 = size * size;
            _dropCells.Clear(Count);
            for (int i = 0; i < Count; i++)
            {
                long key = CellMap.Key(Cell(_p[i].X, size), Cell(_p[i].Y, size), Cell(_p[i].Z, size));
                _next[i] = _dropCells.TryGet(key, out int head) ? head : -1;
                _dropCells.Set(key, i);
            }

            _pairs = 0;
            for (int i = 0; i < Count; i++)
            {
                V3 p = _p[i];
                for (int j = _next[i]; j >= 0; j = _next[j])
                    if ((p - _p[j]).Length2 < reach2)
                        AddPair(i, j);

                int cx = Cell(p.X, size), cy = Cell(p.Y, size), cz = Cell(p.Z, size);
                for (int f = 0; f < Forward.Length; f += 3)
                {
                    if (!_dropCells.TryGet(CellMap.Key(cx + Forward[f], cy + Forward[f + 1], cz + Forward[f + 2]), out int j))
                        continue;
                    for (; j >= 0; j = _next[j])
                        if ((p - _p[j]).Length2 < reach2)
                            AddPair(i, j);
                }
            }
        }

        private void AddPair(int i, int j)
        {
            if (_pairs == _pairA.Length)
            {
                Array.Resize(ref _pairA, _pairs * 2);
                Array.Resize(ref _pairB, _pairs * 2);
            }
            _pairA[_pairs] = i;
            _pairB[_pairs] = j;
            _pairs++;
        }

        private void Separate()
        {
            double size = Radius * 2;
            double rest2 = size * size;
            for (int k = 0; k < _pairs; k++)
            {
                int i = _pairA[k], j = _pairB[k];
                V3 d = _p[i] - _p[j];
                double dist2 = d.Length2;
                if (dist2 >= rest2)
                    continue;
                double dist = Math.Sqrt(dist2);
                if (dist < 1e-9)
                {
                    // Two drops on the same spot: nudge them apart in a direction that depends on who they are.
                    d = new V3(Math.Cos(i * 2.4), Flat ? 0 : Math.Sin(i * 2.4), 0.3);
                    dist = d.Length;
                    d = d * (Radius * 0.01 / dist);
                    dist = Radius * 0.01;
                }
                V3 push = d * (0.5 * Stiffness * (size - dist) / dist);
                _p[i] += push;
                _p[j] -= push;
                // Only half of the push turns into speed; otherwise crowded drops fly apart like sparks.
                _prev[i] += push * 0.5;
                _prev[j] -= push * 0.5;
            }
        }

        /// <summary>Touching drops even out their speeds a little, so a stream holds together instead of spraying.</summary>
        private void Calm()
        {
            double reach = Radius * 2.2;
            double reach2 = reach * reach;
            for (int k = 0; k < _pairs; k++)
            {
                int i = _pairA[k], j = _pairB[k];
                if ((_p[i] - _p[j]).Length2 > reach2)
                    continue;
                V3 dv = (_v[j] - _v[i]) * Viscosity;
                _v[i] += dv;
                _v[j] -= dv;
            }
        }

        private static int Cell(double v, double size) => (int)Math.Floor(v / size);

        // ---------------- drops against solids ----------------

        /// <summary>Walks the drop from where it was to where it wants to be, in steps shorter than itself.</summary>
        private void Collide(int i)
        {
            _normal[i] = V3.Zero;
            V3 from = _prev[i], to = _p[i];
            int near = Gather(from, to);
            if (near == 0)
                return;

            V3 d = to - from;
            double length = d.Length, stride = Radius * 0.7;
            int steps = length <= stride ? 1 : Math.Min(32, (int)Math.Ceiling(length / stride));
            V3 step = d / steps;
            V3 at = from, normal = V3.Zero;
            bool hazard = false;
            for (int k = 0; k < steps; k++)
            {
                at += step;
                Resolve(ref at, near, ref normal, ref step, ref hazard);
            }
            _p[i] = at;
            _normal[i] = normal;
            if (hazard)
                _burnt[i] = true;
        }

        /// <summary>Collects the triangles near the path from a to b. Returns how many (some may be listed twice).</summary>
        private int Gather(V3 a, V3 b)
        {
            double r = Radius, cell = _triCell;
            int x0 = Cell(Math.Min(a.X, b.X) - r, cell), x1 = Cell(Math.Max(a.X, b.X) + r, cell);
            int y0 = Cell(Math.Min(a.Y, b.Y) - r, cell), y1 = Cell(Math.Max(a.Y, b.Y) + r, cell);
            int z0 = Cell(Math.Min(a.Z, b.Z) - r, cell), z1 = Cell(Math.Max(a.Z, b.Z) + r, cell);
            int n = 0;
            for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
            for (int z = z0; z <= z1; z++)
            {
                if (!_triCells.TryGet(CellMap.Key(x, y, z), out int index))
                    continue;
                int start = _cellStart[index], count = _cellCount[index];
                if (n + count > _near.Length)
                    Array.Resize(ref _near, Math.Max(_near.Length * 2, n + count));
                Array.Copy(_cellTris, start, _near, n, count);
                n += count;
            }
            return n;
        }

        /// <summary>Pushes the drop out of every nearby triangle it overlaps, on the side it is on.</summary>
        private void Resolve(ref V3 p, int near, ref V3 normal, ref V3 step, ref bool hazard)
        {
            double r = Radius, r2 = r * r * 0.9999;
            for (int pass = 0; pass < 3; pass++)
            {
                bool moved = false;
                for (int k = 0; k < near; k++)
                {
                    int t = _near[k];
                    V3 q = Tri.Closest(p, _ta[t], _tb[t], _tc[t]);
                    V3 d = p - q;
                    double dist2 = d.Length2;
                    if (dist2 >= r2)
                        continue;
                    double dist = Math.Sqrt(dist2);
                    V3 n = dist > 1e-9 ? d / dist : _tn[t];
                    p = q + n * r;
                    normal += n;
                    double into = V3.Dot(step, n);
                    if (into < 0)
                        step -= n * into; // slide along the surface for the rest of the way
                    if (_tHazard[t])
                        hazard = true;
                    moved = true;
                }
                if (!moved)
                    break;
            }
        }

        // ---------------- goals and spills ----------------

        private void Sweep()
        {
            bool bounded = Bounds.IsValid;
            for (int i = Count - 1; i >= 0; i--)
            {
                V3 p = _p[i];
                if (_burnt[i])
                {
                    Scorched++;
                    Sizzles.Add(p);
                    RemoveAt(i);
                    continue;
                }
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
            _burnt[i] = _burnt[last];
            _born[i] = _born[last];
        }

        private static void Clamp(ref V3 v, double max)
        {
            double length2 = v.Length2;
            if (length2 > max * max)
                v = v * (max / Math.Sqrt(length2));
        }

        private void Grow()
        {
            int size = _p.Length * 2;
            Array.Resize(ref _p, size);
            Array.Resize(ref _prev, size);
            Array.Resize(ref _v, size);
            Array.Resize(ref _normal, size);
            Array.Resize(ref _next, size);
            Array.Resize(ref _burnt, size);
            Array.Resize(ref _born, size);
        }
    }
}
