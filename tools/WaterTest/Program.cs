using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using RhinoGotchi.Water;

// Plays every Water Run level the way Rhino would: the same water, the same pieces, and each
// level's Solve steps standing in for the player's Gumball.


int only = 0;
string? renderTo = null;
int jitterRuns = 0;
double idleSeconds = 12, winSeconds = 45;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "-render" && i + 1 < args.Length)
        renderTo = args[++i];
    else if (args[i] == "-jitter" && i + 1 < args.Length)
        jitterRuns = int.Parse(args[++i]);
    else if (int.TryParse(args[i], out int n))
        only = n;
}
if (renderTo != null)
    Directory.CreateDirectory(renderTo);

int failures = 0;
foreach (WaterLevel level in WaterLevelCatalog.All)
{
    if (only != 0 && level.Number != only)
        continue;
    var game = new Game(level, renderTo);

    // 1. Leave it alone: the level must not win or lose by itself.
    game.Run(idleSeconds);
    game.Snapshot("idle");
    string idle = game.Lost != null ? "FAIL (loses by itself: " + game.Lost + ")"
        : game.Sim.InGoal >= level.Target ? "FAIL (wins by itself)"
        : $"ok ({game.Sim.InGoal} in goal)";
    bool bad = game.Lost != null || game.Sim.InGoal >= level.Target;

    // 2. Play the solution.
    string solved = "no solution";
    if (level.Solve != null)
    {
        game = new Game(level, renderTo);
        game.Run(2);
        level.Solve(game);
        double took = game.RunUntilWon(winSeconds);
        game.Snapshot("end");
        if (game.Lost != null)
        {
            solved = "FAIL (" + game.Lost + ")";
            bad = true;
        }
        else if (took < 0)
        {
            solved = $"FAIL ({game.Sim.InGoal}/{level.Target} after {winSeconds:0} s, {game.Sim.Spilled} spilled)";
            bad = true;
        }
        else
            solved = $"won {took:0.0} s after the last move ({game.Sim.Spilled} spilled)";
    }

    // 3. Play it again, sloppily: a player never puts a piece exactly where the solution says.
    string sloppy = "";
    if (jitterRuns > 0 && level.Solve != null)
    {
        int wins = 0;
        var random = new Random(level.Number * 977);
        for (int run = 0; run < jitterRuns; run++)
        {
            var attempt = new Game(level, only != 0 ? renderTo : null) { Jitter = random };
            attempt.Run(2);
            level.Solve(attempt);
            if (attempt.RunUntilWon(winSeconds) >= 0 && attempt.Lost == null)
                wins++;
            else if (only != 0)
            {
                attempt.Snapshot("sloppy" + run);
                Console.WriteLine($"   sloppy run {run}: {attempt.Lost ?? $"{attempt.Sim.InGoal}/{level.Target}, {attempt.Sim.Spilled} spilled"}");
            }
        }
        sloppy = $"  sloppy: {wins}/{jitterRuns}";
        if (wins * 10 < jitterRuns * 7)
            bad = true; // too fiddly: it should work for most reasonable placements
    }

    if (bad)
        failures++;
    Console.WriteLine($"{level.Number,2} {level.Title,-20} idle: {idle,-34} solution: {solved}  [{game.MsPerFrame:0.0} ms/frame, max {game.MaxDrops} drops]{sloppy}");
}
Console.WriteLine(failures == 0 ? "All levels pass." : $"{failures} level(s) FAILED.");
return failures == 0 ? 0 : 1;

/// <summary>One level being played: the pieces, the water, and the Gumball edits.</summary>
internal sealed class Game : ISolver
{
    private const double Frame = 1.0 / 30;

    private readonly WaterLevel _level;
    private readonly string? _renderTo;
    private readonly List<LevelPiece> _pieces;
    private readonly Stopwatch _watch = new Stopwatch();
    private int _frames;
    private int _shots;

    public readonly WaterSim Sim = new WaterSim();
    public string? Lost;
    public int MaxDrops;
    /// <summary>When set, every edit is a little off: up to 0.35 units, 6 degrees or 8 percent.</summary>
    public Random? Jitter;

    private double Off(double amount) => Jitter == null ? 0 : (Jitter.NextDouble() * 2 - 1) * amount;

    private V3 OffMove() => new V3(Off(0.35), _level.Flat ? 0 : Off(0.35), Off(0.35));

    public double MsPerFrame => _frames == 0 ? 0 : _watch.Elapsed.TotalMilliseconds / _frames;

    public Game(WaterLevel level, string? renderTo)
    {
        _level = level;
        _renderTo = renderTo;
        var builder = new LevelBuilder();
        level.Build(builder);
        _pieces = builder.Pieces;
        Sim.Radius = level.DropSize;
        Sim.Rate = level.Rate;
        Sim.Flat = level.Flat;
        Rebuild();
    }

    // ---------------- running ----------------

    public void Run(double seconds)
    {
        for (int i = 0; i < seconds / Frame && Lost == null; i++)
            Tick();
    }

    /// <summary>Seconds until the goal has its amount, or -1.</summary>
    public double RunUntilWon(double seconds)
    {
        for (int i = 0; i < seconds / Frame && Lost == null; i++)
        {
            if (Sim.InGoal >= _level.Target)
                return i * Frame;
            Tick();
        }
        return Sim.InGoal >= _level.Target && Lost == null ? seconds : -1;
    }

    private void Tick()
    {
        _watch.Start();
        Sim.Step(Frame);
        _watch.Stop();
        _frames++;
        MaxDrops = Math.Max(MaxDrops, Sim.Count);
        if (Sim.Scorched >= WaterSim.ScorchLimit && Lost == null && Sim.Sizzles.Count > 0)
        {
            V3 hit = Sim.Sizzles[0];
            Lost = $"water touched red at ({hit.X:0.0}, {hit.Y:0.0}, {hit.Z:0.0})";
        }
    }

    /// <summary>The same thing WaterLab.Rebuild does in Rhino: pieces to solids, goals and sources.</summary>
    private void Rebuild()
    {
        var colliders = new List<WaterCollider>();
        var goals = new List<WaterGoal>();
        var old = Sim.Sources.ToList();
        Sim.Sources.Clear();
        Box3 scene = Box3.Empty;
        int sourceIndex = 0;
        foreach (LevelPiece piece in _pieces)
        {
            scene.Add(piece.Bounds());
            if (piece.Role == PieceRole.Source)
            {
                var source = new WaterSource { At = piece.At, Jet = piece.Jet };
                if (sourceIndex < old.Count)
                {
                    source.Owed = old[sourceIndex].Owed;
                    source.Emitted = old[sourceIndex].Emitted;
                }
                sourceIndex++;
                Sim.Sources.Add(source);
                continue;
            }
            if (piece.Slabs.Count == 0)
                continue; // a bare curve is not solid
            V3[] triangles = piece.Triangles();
            if (piece.Role == PieceRole.Goal)
                goals.Add(new WaterGoal(triangles, true));
            else
                colliders.Add(new WaterCollider(triangles, piece.Role == PieceRole.Hazard, piece.Movable));
        }
        Sim.SetScene(colliders, goals, SceneBounds(scene));

        // A piece the player moved into a red block loses the level.
        foreach (LevelPiece hazard in _pieces.Where(p => p.Role == PieceRole.Hazard))
            foreach (LevelPiece piece in _pieces.Where(p => p.Movable && p.Role == PieceRole.Solid))
                foreach (Slab a in hazard.Slabs)
                    foreach (Slab b in piece.Slabs)
                        if (Lost == null && Overlap(a, b))
                            Lost = piece.Name + " touched red";
    }

    public static Box3 SceneBounds(Box3 scene)
    {
        if (!scene.IsValid)
            return scene;
        return new Box3(scene.Min - new V3(4, 4, 1.5), scene.Max + new V3(4, 4, 4));
    }

    /// <summary>Do two (possibly sheared) boxes overlap? Separating axes: face normals and edge crosses.</summary>
    private static bool Overlap(Slab a, Slab b)
    {
        V3[] ca = a.Corners(), cb = b.Corners();
        V3[] ea = { a.U, a.V, a.W }, eb = { b.U, b.V, b.W };
        var axes = new List<V3>
        {
            V3.Cross(a.U, a.V), V3.Cross(a.V, a.W), V3.Cross(a.W, a.U),
            V3.Cross(b.U, b.V), V3.Cross(b.V, b.W), V3.Cross(b.W, b.U),
        };
        foreach (V3 x in ea)
            foreach (V3 y in eb)
                axes.Add(V3.Cross(x, y));
        foreach (V3 axis in axes)
        {
            if (axis.Length2 < 1e-12)
                continue;
            double minA = double.MaxValue, maxA = double.MinValue, minB = double.MaxValue, maxB = double.MinValue;
            foreach (V3 c in ca) { double d = V3.Dot(c, axis); minA = Math.Min(minA, d); maxA = Math.Max(maxA, d); }
            foreach (V3 c in cb) { double d = V3.Dot(c, axis); minB = Math.Min(minB, d); maxB = Math.Max(maxB, d); }
            if (maxA < minB || maxB < minA)
                return false;
        }
        return true;
    }

    // ---------------- the Gumball ----------------

    private LevelPiece Find(string name) =>
        _pieces.LastOrDefault(p => p.Name == name && p.Movable) ?? throw new InvalidOperationException($"Level {_level.Number}: no movable piece called '{name}'.");

    /// <summary>A rigid edit: the water in and around the piece goes along, as in Rhino.</summary>
    private void Edit(LevelPiece piece, Xf xf, bool rigid)
    {
        if (_level.Flat)
        {
            // Flat levels undo any move that leaves the plane.
            Box3 before = piece.Bounds();
            LevelPiece test = piece.Clone();
            test.Apply(xf);
            Box3 after = test.Bounds();
            if (Math.Abs(before.Min.Y - after.Min.Y) > 1e-6 || Math.Abs(before.Max.Y - after.Max.Y) > 1e-6)
                throw new InvalidOperationException($"Level {_level.Number}: the solution moves '{piece.Name}' out of the plane.");
        }
        Box3 box = piece.Bounds().Grown(Sim.Radius * 2);
        piece.Apply(xf);
        if (rigid)
            Sim.Carry(box, xf);
        Rebuild();
    }

    private static V3 Axis(char axis) => axis == 'x' ? V3.UnitX : axis == 'y' ? V3.UnitY : V3.UnitZ;

    public void Move(string piece, double dx, double dy, double dz) => Edit(Find(piece), Xf.Move(new V3(dx, dy, dz) + OffMove()), true);

    public void Place(string piece, double x, double y, double z)
    {
        LevelPiece p = Find(piece);
        Edit(p, Xf.Move(new V3(x, y, z) - p.Bounds().Center + OffMove()), true);
    }

    public void Turn(string piece, char axis, double degrees)
    {
        LevelPiece p = Find(piece);
        Edit(p, Xf.Turn(Axis(axis), (degrees + Off(6)) * Math.PI / 180, p.Bounds().Center), true);
    }

    public void TurnAbout(string piece, char axis, double degrees, double x, double y, double z) =>
        Edit(Find(piece), Xf.Turn(Axis(axis), (degrees + Off(6)) * Math.PI / 180, new V3(x, y, z) + OffMove() * 0.5), true);

    public void Stretch(string piece, double sx, double sy, double sz)
    {
        LevelPiece p = Find(piece);
        double off = 1 + Off(0.08);
        Edit(p, Xf.Stretch(p.Bounds().Center, sx == 1 ? 1 : sx * off, sy == 1 ? 1 : sy * off, sz == 1 ? 1 : sz * off), false);
    }

    public void Copy(string piece, string copy, double dx, double dy, double dz)
    {
        LevelPiece made = Find(piece).Clone();
        made.Name = copy;
        made.Apply(Xf.Move(new V3(dx, dy, dz)));
        _pieces.Add(made);
        Rebuild();
    }

    public void Extrude(string piece, double dx, double dy, double dz)
    {
        LevelPiece curve = Find(piece);
        var sheet = new LevelPiece { Name = piece + " surface", Role = PieceRole.Solid, Movable = true };
        for (int i = 0; i + 1 < curve.Curve.Count; i++)
            sheet.Slabs.Add(new Slab(curve.Curve[i], curve.Curve[i + 1] - curve.Curve[i], new V3(dx, dy, dz) * (1 + Off(0.15)), V3.Zero));
        _pieces.Add(sheet);
        Rebuild();
    }

    public void Wait(double seconds)
    {
        Run(seconds);
        Snapshot("wait");
    }

    // ---------------- pictures ----------------

    /// <summary>A front view (x, z) above a top view (x, y): outlines of the pieces and a dot for every drop.</summary>
    public void Snapshot(string label)
    {
        if (_renderTo == null)
            return;
        const int scale = 34, width = 32 * scale, frontH = 17 * scale, topH = 22 * scale;
        var image = new Canvas(width, frontH + topH);
        Func<V3, (int, int)> front = p => ((int)((p.X + 16) * scale), (int)((15 - p.Z) * scale));
        Func<V3, (int, int)> top = p => ((int)((p.X + 16) * scale), frontH + (int)((11 - p.Y) * scale));
        image.Line(0, frontH, width, frontH, 0x999999);

        int[] edges = { 0, 1, 1, 2, 2, 3, 3, 0, 4, 5, 5, 6, 6, 7, 7, 4, 0, 4, 1, 5, 2, 6, 3, 7 };
        foreach (LevelPiece piece in _pieces)
        {
            int color = piece.Role == PieceRole.Hazard ? 0xE03020 : piece.Role == PieceRole.Goal ? 0x20A060
                : piece.Movable ? 0xF08020 : piece.Look == "stone" ? 0xA09880 : 0x505860;
            foreach (Slab slab in piece.Slabs)
            {
                V3[] c = slab.Corners();
                for (int e = 0; e < edges.Length; e += 2)
                {
                    var (x0, y0) = front(c[edges[e]]); var (x1, y1) = front(c[edges[e + 1]]);
                    image.Line(x0, y0, x1, y1, color);
                    (x0, y0) = top(c[edges[e]]); (x1, y1) = top(c[edges[e + 1]]);
                    image.Line(x0, y0, x1, y1, color);
                }
            }
            for (int i = 0; i + 1 < piece.Curve.Count; i++)
            {
                var (x0, y0) = front(piece.Curve[i]); var (x1, y1) = front(piece.Curve[i + 1]);
                image.Line(x0, y0, x1, y1, 0xF08020);
                (x0, y0) = top(piece.Curve[i]); (x1, y1) = top(piece.Curve[i + 1]);
                image.Line(x0, y0, x1, y1, 0xF08020);
            }
        }
        int dot = Math.Max(1, (int)Math.Round(Sim.Radius * scale));
        for (int i = 0; i < Sim.Count; i++)
        {
            V3 p = Sim.PositionAt(i);
            var (x, y) = front(p);
            image.Dot(x, y, dot, 0x2070F0);
            (x, y) = top(p);
            image.Dot(x, y, dot, 0x2070F0);
        }
        image.Save(Path.Combine(_renderTo, $"level-{_level.Number:00}-{++_shots}-{label}.bmp"));
    }
}

/// <summary>A picture you can draw lines and dots on and save as a .bmp.</summary>
internal sealed class Canvas
{
    private readonly int _width, _height;
    private readonly byte[] _pixels;

    public Canvas(int width, int height)
    {
        _width = width;
        _height = height;
        _pixels = new byte[width * height * 3];
        Array.Fill(_pixels, (byte)255);
    }

    private void Set(int x, int y, int color)
    {
        if (x < 0 || y < 0 || x >= _width || y >= _height)
            return;
        int i = (y * _width + x) * 3;
        _pixels[i] = (byte)color;
        _pixels[i + 1] = (byte)(color >> 8);
        _pixels[i + 2] = (byte)(color >> 16);
    }

    public void Dot(int x, int y, int radius, int color)
    {
        for (int dx = -radius; dx <= radius; dx++)
            for (int dy = -radius; dy <= radius; dy++)
                if (dx * dx + dy * dy <= radius * radius)
                    Set(x + dx, y + dy, color);
    }

    public void Line(int x0, int y0, int x1, int y1, int color)
    {
        int steps = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
        if (steps > 10000)
            return;
        for (int i = 0; i <= steps; i++)
            Set(x0 + (steps == 0 ? 0 : (x1 - x0) * i / steps), y0 + (steps == 0 ? 0 : (y1 - y0) * i / steps), color);
    }

    public void Save(string path)
    {
        int row = (_width * 3 + 3) & ~3;
        using var file = new BinaryWriter(File.Create(path));
        file.Write((byte)'B'); file.Write((byte)'M');
        file.Write(54 + row * _height); file.Write(0); file.Write(54);
        file.Write(40); file.Write(_width); file.Write(_height); file.Write((short)1); file.Write((short)24);
        file.Write(0); file.Write(row * _height); file.Write(2835); file.Write(2835); file.Write(0); file.Write(0);
        var line = new byte[row];
        for (int y = _height - 1; y >= 0; y--)
        {
            Array.Copy(_pixels, y * _width * 3, line, 0, _width * 3);
            file.Write(line);
        }
    }
}
