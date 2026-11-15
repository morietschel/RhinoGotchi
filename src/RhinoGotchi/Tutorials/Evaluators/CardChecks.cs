using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using RhinoGotchi.Levels;

namespace RhinoGotchi.Tutorials.Evaluators
{
    // The live checks, one class per tutorial, in card order (easiest first). Each returns the ✓/✗
    // lines the card shows and whether its bonus is met. The geometry tests are the ones verified
    // against the Klosterneuburg model when the tutorials were first built.

    /// <summary>1 BooleanDifference: a closed plate with every hole right through it.</summary>
    public sealed class PlateCheck : TutorialEvaluator
    {
        private readonly BoundingBox _plate;
        private readonly double _plateVolume, _holeVolume;
        private readonly int _holes;

        public PlateCheck(Tutorial t, BoundingBox plate, double holeVolume, int holes) : base(t)
        {
            _plate = plate;
            _plateVolume = plate.Diagonal.X * plate.Diagonal.Y * plate.Diagonal.Z;
            _holeVolume = holeVolume;
            _holes = holes;
        }

        protected override Outcome Check(LevelAttempt attempt, List<RhinoObject> work, double tolerance)
        {
            BoundingBox near = _plate;
            near.Inflate(0.01);
            var plates = Measure.Breps(work).Select(x => x.Brep)
                .Where(b => near.Contains(b.GetBoundingBox(true)) && b.GetBoundingBox(true).Diagonal.Y > 0.5 * _plate.Diagonal.Y)
                .ToList();
            var o = new Outcome();
            if (plates.Count == 0)
                return o.Add("A plate", false, "No plate yet: make the box from the Plate corner point.")
                        .Add("Closed solid", false).Add($"{_holes} holes right through", false);

            // The best plate: the one with the most holes cut cleanly.
            var best = plates.Select(b =>
            {
                double holes = (_plateVolume - Measure.Volume(b)) / _holeVolume;
                return (Brep: b, Holes: holes, Whole: (int)Math.Round(holes), Clean: Math.Abs(holes - Math.Round(holes)) < 0.1);
            }).OrderByDescending(x => x.Clean && x.Whole == _holes).ThenByDescending(x => x.Holes).First();

            o.Add("A plate", true);
            o.Add("Closed solid", best.Brep.IsSolid, "The plate isn't closed. Undo and cut again.");
            bool holesOk = best.Clean && best.Whole == _holes;
            string hint = best.Holes < 0.1 ? "No holes yet: BooleanDifference, plate first, then the cutters."
                        : !best.Clean ? "One cut stopped inside the plate (a pocket). Undo it; cutters must pass right through."
                        : $"{best.Whole} of {_holes} holes so far: pick every cutter.";
            o.Add($"{_holes} holes right through", holesOk, hint);
            o.Bonus = holesOk && Count(attempt, "BooleanDifference") == 1;
            return o;
        }
    }

    /// <summary>2 Pipe: a tube of the right radius along the centreline, closed, not folding into itself.</summary>
    public sealed class PipeCheck : TutorialEvaluator
    {
        private readonly Curve _centreline;
        private readonly double _radius;

        public PipeCheck(Tutorial t, Curve centreline, double radius) : base(t)
        {
            _centreline = centreline;
            _radius = radius;
        }

        protected override Outcome Check(LevelAttempt attempt, List<RhinoObject> work, double tolerance)
        {
            var probes = Measure.Samples(_centreline, 20).Skip(1).Take(19).ToList();
            var tubes = Measure.Breps(work).Select(x => x.Brep)
                .Where(b => probes.Count(p => Measure.DistanceTo(b, p) <= 0.2 || (b.IsSolid && b.IsPointInside(p, tolerance, false))) >= probes.Count * 0.8)
                .ToList();
            var o = new Outcome();
            if (tubes.Count == 0)
                return o.Add("Tube along the centreline", false, "No tube yet: press Run and give Pipe the radius.")
                        .Add($"Ø {2 * _radius:0.000} m", false).Add("Closed ends", false);

            double length = _centreline.GetLength();
            double straight = Math.PI * _radius * _radius * length;
            double rounded = straight + 4.0 / 3.0 * Math.PI * Math.Pow(_radius, 3);
            var best = tubes.Select(b =>
            {
                var d = probes.Select(p => Measure.DistanceTo(b, p)).ToList();
                double fraction = d.Count(x => Math.Abs(x - _radius) <= 0.002) / (double)d.Count;
                double typical = d.OrderBy(x => x).ElementAt(d.Count / 2);
                return (Brep: b, Fraction: fraction, Typical: typical, Volume: Measure.Volume(b));
            }).OrderByDescending(x => x.Fraction).First();

            bool sized = best.Fraction >= 0.9;
            bool noFold = Math.Abs(best.Volume - straight) <= 0.05 * straight || Math.Abs(best.Volume - rounded) <= 0.05 * rounded;
            o.Add("Tube along the centreline", true);
            o.Add($"Ø {2 * _radius:0.000} m", sized, Math.Abs(best.Typical - 2 * _radius) <= 0.002
                ? $"Twice too thick: Pipe asks for the radius. Type {_radius}."
                : $"The radius is about {best.Typical:0.000} m; type {_radius}.");
            o.Add("Closed ends", best.Brep.IsSolid && (!sized || noFold),
                best.Brep.IsSolid ? "The tube folds into itself at the bend." : "Set Cap to Round or Flat.");
            // Round caps: closed, and no flat face anywhere (flat caps are planar discs).
            o.Bonus = sized && best.Brep.IsSolid && best.Brep.Faces.All(f => !f.IsPlanar(1e-4));
            return o;
        }
    }

    /// <summary>3 OffsetSrf: a closed solid on the glass, the right thickness at five places.</summary>
    public sealed class GlassCheck : TutorialEvaluator
    {
        private readonly Brep _face;
        private readonly double _thickness;

        public GlassCheck(Tutorial t, Brep face, double thickness) : base(t)
        {
            _face = face;
            _thickness = thickness;
        }

        protected override Outcome Check(LevelAttempt attempt, List<RhinoObject> work, double tolerance)
        {
            BrepFace face = _face.Faces[0];
            var spots = new[] { (0.2, 0.2), (0.8, 0.2), (0.5, 0.5), (0.2, 0.8), (0.8, 0.8) }.Select(s =>
            {
                double u = face.Domain(0).ParameterAt(s.Item1), v = face.Domain(1).ParameterAt(s.Item2);
                return (P: face.PointAt(u, v), N: face.NormalAt(u, v));
            }).ToList();

            var panels = Measure.Breps(work).Select(x => x.Brep)
                .Where(b => spots.All(s => Measure.DistanceTo(b, s.P) <= 0.5)).ToList();
            var o = new Outcome();
            if (panels.Count == 0)
                return o.Add("A glass panel", false, "No panel yet: press Run, set the distance and Solid=Yes.")
                        .Add($"{_thickness * 1000:0} mm thick", false);

            var best = panels.Select(b =>
            {
                var thick = new List<double>();
                int onFace = 0;
                foreach (var (p, n) in spots)
                {
                    var probe = new LineCurve(p + n * 0.5, p - n * 0.5);
                    if (Intersection.CurveBrep(probe, b, tolerance, out _, out Point3d[] hits) && hits.Length >= 2)
                    {
                        var nearest = hits.OrderBy(h => h.DistanceTo(p)).Take(2).ToList();
                        thick.Add(nearest[0].DistanceTo(nearest[1]));
                        if (nearest[0].DistanceTo(p) < 0.001)
                            onFace++;
                    }
                }
                int right = thick.Count(x => Math.Abs(x - _thickness) <= 0.002);
                return (Brep: b, Right: right, Avg: thick.Count > 0 ? thick.Average() : 0, Inward: onFace == spots.Count && SideOf(b) < 0);
            }).OrderByDescending(x => x.Right).First();

            o.Add("A closed glass solid", best.Brep.IsSolid, "Set Solid=Yes and run OffsetSrf again.");
            o.Add($"{_thickness * 1000:0} mm thick", best.Right == spots.Count,
                best.Avg > 0 ? $"It's {best.Avg * 1000:0.#} mm. Type {_thickness} (this model is in metres)." : "Couldn't measure it yet.");
            o.Bonus = best.Inward && best.Right == spots.Count;
            return o;

            // Negative when the solid lies on the side the surface's normal points away from (inward).
            double SideOf(Brep b)
            {
                Point3d c = AreaMassProperties.Compute(b)?.Centroid ?? b.GetBoundingBox(true).Center;
                var (p, n) = spots[2];
                return (c - p) * n;
            }
        }
    }

    /// <summary>4 ArrayPolar: N upright planes through the centre's vertical axis, 360/N° apart; the challenge also wants roof profiles.</summary>
    public sealed class SettingOutCheck : TutorialEvaluator
    {
        private readonly Point3d _centre;
        private readonly int _count;
        private readonly string _originalName;
        private readonly Brep? _roof;

        public SettingOutCheck(Tutorial t, Point3d centre, int count, string originalName, Brep? roofForProfiles) : base(t)
        {
            _centre = centre;
            _count = count;
            _originalName = originalName;
            _roof = roofForProfiles;
        }

        protected override Outcome Check(LevelAttempt attempt, List<RhinoObject> work, double tolerance)
        {
            var candidates = Measure.Breps(work).Select(x => x.Brep)
                .Concat(StartObjects(attempt, _originalName).Select(Measure.AsBrep).Where(b => b != null).Cast<Brep>());
            var bearings = new List<double>();
            int offAxis = 0;
            foreach (Brep b in candidates)
            {
                if (b.Faces.Count != 1 || !b.Faces[0].TryGetPlane(out Plane plane, 1e-4) || Math.Abs(plane.Normal.Z) > 0.01)
                    continue;
                if (Math.Abs(plane.DistanceTo(_centre)) > 0.01)
                {
                    offAxis++;
                    continue;
                }
                Point3d c = AreaMassProperties.Compute(b)?.Centroid ?? b.GetBoundingBox(true).Center;
                bearings.Add((Math.Atan2(c.Y - _centre.Y, c.X - _centre.X) * 180 / Math.PI + 360) % 360);
            }
            bearings.Sort();
            var unique = new List<double>();
            foreach (double b in bearings)
                if (unique.Count == 0 || Math.Abs(b - unique[unique.Count - 1]) >= 0.5)
                    unique.Add(b);
            if (unique.Count > 1 && 360 - unique[unique.Count - 1] + unique[0] < 0.5)
                unique.RemoveAt(unique.Count - 1);
            int doubled = bearings.Count - unique.Count;

            double step = 360.0 / _count;
            bool even = unique.Count > 1 && unique.Zip(unique.Skip(1), (a, b) => b - a)
                .Append(360 - unique[unique.Count - 1] + unique[0]).All(g => Math.Abs(g - step) <= 0.5);

            var o = new Outcome();
            o.Add($"{_count} planes", unique.Count == _count && doubled == 0,
                unique.Count > _count ? $"{unique.Count} planes: type {_count} (the count includes the original)."
                : doubled > 0 ? "Some planes sit on top of each other: type the count once, including the original."
                : $"{unique.Count} so far: ArrayPolar with {_count} items.");
            o.Add($"{step:0.#}° apart", even, $"The gaps aren't even: fill 360° with {_count} items.");
            o.Add("Around the building centre", unique.Count > 1 && offAxis == 0, "Use the yellow centre point as the array centre.");
            if (_roof != null)
            {
                int profiles = Measure.Curves(work).Count(x => x.Curve.GetLength() > 1 &&
                    Measure.Samples(x.Curve, 10).All(p => Measure.DistanceTo(_roof, p) <= 0.01));
                o.Add("2 roof profiles", profiles >= 2, "Intersect two neighbouring planes with the roof copy.");
            }
            return o;
        }
    }

    /// <summary>5 ArrayCrv: vertical bars of the right height hanging from the rail, evenly spaced.</summary>
    public sealed class BarsCheck : TutorialEvaluator
    {
        private readonly Curve _rail;
        private readonly double _height, _spacing;
        private readonly string _originalName;

        public BarsCheck(Tutorial t, Curve rail, double height, double spacing, string originalName) : base(t)
        {
            _rail = rail;
            _height = height;
            _spacing = spacing;
            _originalName = originalName;
        }

        protected override Outcome Check(LevelAttempt attempt, List<RhinoObject> work, double tolerance)
        {
            var lines = Measure.Curves(work).Select(x => x.Curve)
                .Concat(StartObjects(attempt, _originalName).OfType<Curve>())
                .Where(c => c.IsLinear(1e-4) && Math.Abs(c.GetLength() - _height) <= 0.01);
            var bars = new List<(double Along, double Tilt)>();
            foreach (Curve c in lines)
            {
                bool startHigher = c.PointAtStart.Z > c.PointAtEnd.Z;
                Point3d top = startHigher ? c.PointAtStart : c.PointAtEnd, bottom = startHigher ? c.PointAtEnd : c.PointAtStart;
                if (!_rail.ClosestPoint(top, out double t) || _rail.PointAt(t).DistanceTo(top) > 0.01)
                    continue;
                double along = _rail.GetLength(new Interval(_rail.Domain.Min, t));
                if (bars.Any(b => Math.Abs(b.Along - along) < 1e-3))
                    continue;
                bars.Add((along, Vector3d.VectorAngle(top - bottom, Vector3d.ZAxis) * 180 / Math.PI));
            }
            bars.Sort((a, b) => a.Along.CompareTo(b.Along));
            bool vertical = bars.Count >= 2 && bars.All(b => b.Tilt <= 1.0);
            bool even = bars.Count > 1 && bars.Zip(bars.Skip(1), (a, b) => b.Along - a.Along).All(g => Math.Abs(g - _spacing) <= 0.02 * _spacing);
            int expected = (int)Math.Floor(_rail.GetLength() / _spacing + 1e-6) + 1;

            var o = new Outcome();
            o.Add("5 or more bars on the rail", bars.Count >= 5, bars.Count <= 1
                ? "No copies yet: ArrayCrv the bar, with its top as the base point."
                : $"{bars.Count} so far: use Distance between items = {_spacing}.");
            o.Add("All vertical", vertical, "Some bars lean: set Orientation to No rotation.");
            o.Add($"{_spacing} m apart", even, $"The gaps aren't all {_spacing} m: Distance between items = {_spacing}.");
            o.Bonus = bars.Count >= expected && vertical && even;
            return o;
        }
    }

    /// <summary>6 Contour: 5+ parallel, evenly spaced profiles; the bonus is cutting the solid too.</summary>
    public sealed class ContourCheck : TutorialEvaluator
    {
        public ContourCheck(Tutorial t) : base(t) { }

        protected override Outcome Check(LevelAttempt attempt, List<RhinoObject> work, double tolerance)
        {
            var curves = Measure.Curves(work).Select(x => x.Curve).ToList();
            var open = Assess(curves.Where(c => !c.IsClosed).ToList(), tolerance);
            var closed = Assess(curves.Where(c => c.IsClosed).ToList(), tolerance);
            var best = open.Count >= closed.Count ? open : closed;

            var o = new Outcome();
            o.Add("5 or more profiles", best.Count >= 5, best.Count == 0 ? "No profiles yet: Contour the roof patch." : $"{best.Count} so far: use a 0.5 m spacing.");
            o.Add("Parallel, evenly spaced", best.Count >= 2 && best.Even, "Give one base point, one direction and one spacing.");
            o.Bonus = open.Count >= 5 && open.Even && closed.Count >= 5 && closed.Even;
            return o;
        }

        private static (int Count, bool Even) Assess(List<Curve> curves, double tolerance)
        {
            var planes = new List<Plane>();
            foreach (Curve c in curves)
                if (c.TryGetPlane(out Plane p, Math.Max(tolerance * 10, 1e-3)))
                    planes.Add(p);
            if (planes.Count == 0)
                return (0, false);
            Vector3d dir = Vector3d.Unset;
            var group = new List<Plane>();
            foreach (Plane seed in planes)
            {
                var members = planes.Where(p => Vector3d.VectorAngle(p.Normal, seed.Normal) < 0.0175 ||
                                                Vector3d.VectorAngle(p.Normal, -seed.Normal) < 0.0175).ToList();
                if (members.Count > group.Count)
                {
                    group = members;
                    dir = seed.Normal;
                }
            }
            var offsets = group.Select(p => (Vector3d)p.Origin * dir).OrderBy(x => x).ToList();
            var distinct = new List<double>();
            foreach (double v in offsets)
                if (distinct.Count == 0 || v - distinct[distinct.Count - 1] > 1e-3)
                    distinct.Add(v);
            if (distinct.Count < 2)
                return (distinct.Count, false);
            var gaps = distinct.Zip(distinct.Skip(1), (a, b) => b - a).ToList();
            double median = gaps.OrderBy(g => g).ElementAt(gaps.Count / 2);
            bool even = median > 0 && gaps.All(g => Math.Abs(g / median - Math.Round(g / median)) <= 0.01);
            return (distinct.Count, even);
        }
    }

    /// <summary>7 Intersect: a curve on the roof, in an upright plane through the centre; the bonus is a second one (Project).</summary>
    public sealed class ProfileCheck : TutorialEvaluator
    {
        private readonly Brep _roof;
        private readonly Point3d _centre;

        public ProfileCheck(Tutorial t, Brep roof, Point3d centre) : base(t)
        {
            _roof = roof;
            _centre = centre;
        }

        protected override Outcome Check(LevelAttempt attempt, List<RhinoObject> work, double tolerance)
        {
            int onRoof = 0, good = 0;
            foreach (Curve c in Measure.Curves(work).Select(x => x.Curve).Where(c => c.GetLength() > 1))
            {
                var samples = Measure.Samples(c, 20);
                if (!samples.All(p => Measure.DistanceTo(_roof, p) <= 0.01))
                    continue;
                onRoof++;
                if (Plane.FitPlaneToPoints(samples, out Plane plane) == PlaneFitResult.Success &&
                    Math.Abs(plane.Normal.Z) < 0.02 && Math.Abs(plane.DistanceTo(_centre)) <= 0.05)
                    good++;
            }
            var o = new Outcome();
            o.Add("A curve on the roof", onRoof > 0, "Intersect the plane with the orange roof copy.");
            o.Add("Through the building centre", good > 0, "The plane must stand on the building centre point.");
            o.Bonus = good >= 2;
            return o;
        }
    }

    /// <summary>8 Sweep1: a beam along the edge rail, the right section, staying upright; closed for the challenge (bonus on the core card).</summary>
    public sealed class EdgeBeamCheck : TutorialEvaluator
    {
        private readonly Curve _rail;
        private readonly double _width, _depth;
        private readonly bool _mustBeClosed;

        public EdgeBeamCheck(Tutorial t, Curve rail, double width, double depth, bool mustBeClosed) : base(t)
        {
            _rail = rail;
            _width = width;
            _depth = depth;
            _mustBeClosed = mustBeClosed;
        }

        protected override Outcome Check(LevelAttempt attempt, List<RhinoObject> work, double tolerance)
        {
            var railPoints = Measure.Samples(_rail, 15);
            var beams = Measure.Breps(work).Select(x => x.Brep)
                .Where(b => railPoints.Count(p => Measure.DistanceTo(b, p) <= 0.01) >= railPoints.Count * 0.8).ToList();
            var o = new Outcome();
            if (beams.Count == 0)
            {
                o.Add("Follows the roof edge", false, "No beam yet: Sweep1 the section along the rail.")
                 .Add($"Section {_width:0.00} × {_depth:0.00} m", false).Add("Stays upright", false);
                if (_mustBeClosed)
                    o.Add("Closed solid", false);
                return o;
            }

            double area = _width * _depth;
            var best = beams.Select(beam =>
            {
                var areas = new List<double>();
                var rolls = new List<double>();
                foreach (double f in new[] { 0.1, 0.3, 0.5, 0.7, 0.9 })
                {
                    _rail.NormalizedLengthParameter(f, out double t);
                    if (!_rail.PerpendicularFrameAt(t, out Plane frame) ||
                        !Intersection.BrepPlane(beam, frame, tolerance, out Curve[] cuts, out _) || cuts.Length == 0)
                        continue;
                    Curve? section = Curve.JoinCurves(cuts, tolerance * 10).OrderByDescending(c => c.GetLength()).FirstOrDefault();
                    var props = section != null && section.IsClosed ? AreaMassProperties.Compute(section) : null;
                    if (props == null)
                        continue;
                    areas.Add(props.Area);
                    Vector3d up = Vector3d.ZAxis - (Vector3d.ZAxis * frame.Normal) * frame.Normal;
                    Vector3d toC = props.Centroid - frame.Origin;
                    toC -= (toC * frame.Normal) * frame.Normal;
                    if (up.Unitize() && toC.Unitize())
                        rolls.Add(Vector3d.VectorAngle(up, toC, new Plane(frame.Origin, frame.Normal)) * 180 / Math.PI);
                }
                double spread = 0;
                if (rolls.Count > 1)
                {
                    var rel = rolls.Select(r => Math.IEEERemainder(r - rolls[0], 360)).ToList();
                    spread = rel.Max() - rel.Min();
                }
                bool areaOk = areas.Count > 0 && areas.All(a => Math.Abs(a - area) <= 0.1 * area);
                return (Brep: beam, AreaOk: areaOk, Area: areas.Count > 0 ? areas.Average() : 0, Upright: rolls.Count > 1 && spread <= 5, Spread: spread);
            }).OrderByDescending(x => (x.AreaOk ? 2 : 0) + (x.Upright ? 1 : 0)).First();

            o.Add("Follows the roof edge", true);
            o.Add($"Section {_width:0.00} × {_depth:0.00} m", best.AreaOk, $"The section is about {best.Area:0.0000} m², not {area:0.0000} m².");
            o.Add("Stays upright", best.Upright, $"It rolls about {best.Spread:0}° along the way: use Frame style Roadlike Top.");
            if (_mustBeClosed)
                o.Add("Closed solid", best.Brep.IsSolid, "Run Cap on the beam.");
            else
                o.Bonus = best.Brep.IsSolid && best.AreaOk && best.Upright;
            return o;
        }
    }

    /// <summary>9 Sweep2: a ramp surface meeting the four landing points, under 1% sideways slope.</summary>
    public sealed class RampCheck : TutorialEvaluator
    {
        private readonly Point3d[] _corners;

        public RampCheck(Tutorial t, IEnumerable<Point3d> corners) : base(t)
        {
            _corners = corners.ToArray();
        }

        protected override Outcome Check(LevelAttempt attempt, List<RhinoObject> work, double tolerance)
        {
            var surfaces = Measure.Breps(work).Where(x => x.Brep.Faces.Count == 1).Select(x => x.Brep).ToList();
            var o = new Outcome();
            if (surfaces.Count == 0)
                return o.Add("Meets all 4 landing points", false, "No ramp yet: Sweep2 the two rails and the two landing lines.")
                        .Add("No sideways tilt", false);

            var best = surfaces.Select(s =>
            {
                int meets = _corners.Count(c => Measure.DistanceTo(s, c) <= 0.01);
                Surface srf = s.Faces[0].UnderlyingSurface();
                Interval d0 = srf.Domain(0), d1 = srf.Domain(1);
                int across = (srf.IsoCurve(0, d1.Mid)?.GetLength() ?? 0) < (srf.IsoCurve(1, d0.Mid)?.GetLength() ?? 0) ? 0 : 1;
                Interval along = across == 0 ? d1 : d0;
                var slopes = new List<double>();
                foreach (double f in new[] { 0.1, 0.3, 0.5, 0.7, 0.9 })
                {
                    Curve? c = srf.IsoCurve(across, along.ParameterAt(f));
                    if (c == null)
                        continue;
                    Point3d a = c.PointAtStart, b = c.PointAtEnd;
                    double plan = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
                    if (plan > 1e-6)
                        slopes.Add(Math.Abs(b.Z - a.Z) / plan);
                }
                return (Meets: meets, Worst: slopes.Count > 0 ? slopes.Max() : 1);
            }).OrderByDescending(x => x.Meets).ThenBy(x => x.Worst).First();

            o.Add("Meets all 4 landing points", best.Meets == _corners.Length, $"It meets {best.Meets} of 4: pick both rails, then both landing lines.");
            o.Add("No sideways tilt", best.Worst < 0.01, $"It tilts sideways by {best.Worst * 100:0.#}%: both rails must run the same way.");
            return o;
        }
    }

    /// <summary>10 FlowAlongSrf: 5+ joists sitting on the roof patch, none overlapping; the bonus is all of them.</summary>
    public sealed class JoistsCheck : TutorialEvaluator
    {
        private readonly Surface _target;
        private readonly int _expected;

        public JoistsCheck(Tutorial t, Surface target, int expected) : base(t)
        {
            _target = target;
            _expected = expected;
        }

        protected override Outcome Check(LevelAttempt attempt, List<RhinoObject> work, double tolerance)
        {
            var onRoof = new List<Brep>();
            foreach (var (_, b) in Measure.Breps(work))
            {
                if (!b.IsSolid)
                    continue;
                var d = b.Vertices.Select(v => Measure.DistanceTo(_target, v.Location)).OrderBy(x => x).ToList();
                if (d.Count > 0 && d.Take(Math.Max(1, d.Count / 2)).All(x => x <= 0.03))
                    onRoof.Add(b);
            }
            int overlaps = 0;
            for (int i = 0; i < onRoof.Count; i++)
                for (int j = i + 1; j < onRoof.Count; j++)
                    if (BoundingBox.Intersection(onRoof[i].GetBoundingBox(true), onRoof[j].GetBoundingBox(true)).IsValid &&
                        Intersection.BrepBrep(onRoof[i], onRoof[j], tolerance, out Curve[] cs, out _) && cs.Length > 0)
                        overlaps++;

            var o = new Outcome();
            o.Add("5 or more joists on the roof", onRoof.Count >= 5, onRoof.Count == 0
                ? "No joists on the roof yet: flow them from the base onto the roof patch."
                : $"{onRoof.Count} so far.");
            o.Add("No overlaps", onRoof.Count > 0 && overlaps == 0, $"{overlaps} pairs overlap: keep them 0.5 m apart before flowing.");
            o.Bonus = onRoof.Count >= _expected && overlaps == 0;
            return o;
        }
    }

    /// <summary>11 Loft: a surface covering the patch, within 10 cm of the roof; the bonus is History.</summary>
    public sealed class LoftCheck : TutorialEvaluator
    {
        private readonly Surface _target;
        private readonly double _area;

        public LoftCheck(Tutorial t, Surface target) : base(t)
        {
            _target = target;
            _area = Measure.Area(target);
        }

        protected override Outcome Check(LevelAttempt attempt, List<RhinoObject> work, double tolerance)
        {
            var best = Measure.Breps(work).Where(x => Measure.Area(x.Brep) >= 0.8 * _area)
                .Select(x => (x.Obj, Dev: Measure.MaxDeviation(x.Brep, _target)))
                .OrderBy(x => x.Dev).FirstOrDefault();
            var o = new Outcome();
            o.Add("A surface over the patch", best.Obj != null, "No surface yet: Loft the four sections.");
            o.Add("Within 10 cm of the roof", best.Obj != null && best.Dev <= 0.10,
                best.Obj == null ? null : $"It strays {best.Dev:0.00} m: use Style Normal and keep the curves in order.");
            o.Bonus = best.Obj != null && best.Dev <= 0.10 &&
                      ((attempt.HasHistory.TryGetValue(best.Obj.Id, out bool h) && h) || best.Obj.HasHistoryRecord());
            return o;
        }
    }

    /// <summary>12 NetworkSrf: through every curve, within 8 cm of the roof; the bonus is a rebuilt copy of ≤ 10 × 10 points.</summary>
    public sealed class NetworkCheck : TutorialEvaluator
    {
        private readonly Surface _target;
        private readonly double _area;
        private readonly Curve[] _curves;

        public NetworkCheck(Tutorial t, Surface target, IEnumerable<Curve> givenCurves) : base(t)
        {
            _target = target;
            _area = Measure.Area(target);
            _curves = givenCurves.ToArray();
        }

        protected override Outcome Check(LevelAttempt attempt, List<RhinoObject> work, double tolerance)
        {
            var surfaces = Measure.Breps(work).Where(x => x.Brep.Faces.Count == 1 && Measure.Area(x.Brep) >= 0.8 * _area).Select(x => x.Brep).ToList();
            var curves = _curves.Concat(Measure.Curves(work).Select(x => x.Curve)).ToList();
            bool through = surfaces.Any(s => curves.All(c => Measure.Samples(c, 8).All(p => Measure.DistanceTo(s, p) <= 0.01)));
            double dev = surfaces.Count == 0 ? double.MaxValue : surfaces.Min(s => Measure.MaxDeviation(s, _target));
            var o = new Outcome();
            o.Add("Passes through every curve", through, surfaces.Count == 0 ? "No surface yet: NetworkSrf all the curves." : "Every curve must touch the curves it crosses.");
            o.Add("Within 8 cm of the roof", dev <= 0.08, surfaces.Count == 0 ? null : $"It strays {dev:0.00} m.");
            o.Bonus = surfaces.Any(s =>
            {
                NurbsSurface? n = s.Faces[0].UnderlyingSurface().ToNurbsSurface();
                return n != null && n.Points.CountU <= 10 && n.Points.CountV <= 10 && Measure.MaxDeviation(s, _target) <= 0.08;
            });
            return o;
        }
    }
}
