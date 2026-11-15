using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;
using RhinoGotchi.Levels;
using RhinoGotchi.Tutorials.Evaluators;
using static RhinoGotchi.Tutorials.Content.Shapes;

namespace RhinoGotchi.Tutorials.Content
{
    /// <summary>
    /// The 24 cards: for each tutorial, easiest first, a core card (input prepared, Run picks it,
    /// at most three steps) and a challenge card (the learner prepares the input too).
    /// </summary>
    internal static class Cards
    {
        private const string LiftWhy = "tutorial copies float above the real roof so you never pick the original";

        public static IReadOnlyList<Tutorial> Build()
        {
            var all = new List<Tutorial>();
            void Add((Tutorial Core, Tutorial Challenge) pair)
            {
                all.Add(pair.Core);
                all.Add(pair.Challenge);
            }
            Add(Plate());
            Add(Handrail());
            Add(Glass());
            Add(SettingOut());
            Add(Balustrade());
            Add(Slices());
            Add(RibProfile());
            Add(EdgeBeam());
            Add(Ramp());
            Add(Joists());
            Add(RoofLoft());
            Add(RoofNetwork());
            return all;
        }

        // ================================================================ helpers

        private static Tutorial Card(int number, bool challenge, string command, string element, string title, string intro,
            string? macro, string? bonus, params string[] steps)
        {
            var t = new Tutorial
            {
                Id = $"tut-{number:00}-{command.ToLowerInvariant()}{(challenge ? "-challenge" : "")}",
                Number = number,
                IsChallenge = challenge,
                MainCommand = command,
                Element = element,
                Title = title,
                Intro = intro,
                RunMacro = macro,
                BonusText = bonus,
            };
            foreach (string s in steps)
                t.Steps.Add(new TutorialStep(s));
            return t;
        }

        private static IEnumerable<ClickMark> Marks(params (string Label, Point3d At)[] marks) =>
            marks.Select(m => new ClickMark(m.Label, m.At));

        private static string Pick(params string[] names) => string.Join(" ", names.Select(n => "_SelID {" + n + "}"));

        // ================================================================ 1 BooleanDifference

        private static (Tutorial, Tutorial) Plate()
        {
            var core = Card(1, false, "BooleanDifference", "Bolted steel plate",
                "Drill the steel plate.", "A connection plate needs four bolt holes.",
                Pick("Plate") + " _BooleanDifference", "Cut all four holes in one go",
                "Press Run: the plate is picked for you",
                "Click the 4 yellow cutters",
                "Press Enter");
            core.SetUp = c =>
            {
                c.AddInput(new Box(PlateBox).ToBrep(), "Plate");
                for (int i = 0; i < Holes.Length; i++)
                    c.AddInput(Cutter(i), $"Cutter {i + 1}");
            };
            core.Clicks = c => Enumerable.Range(0, Holes.Length).Select(i => new ClickMark("cutter", HoleBase(i) + new Vector3d(0, 0, CutterHeight)));

            var challenge = Card(1, true, "BooleanDifference", "Bolted steel plate",
                "Make the plate yourself.", "A box, four cylinders, then the cut.",
                "_Box", null,
                $"Box from ① to @{PlateWidth},{PlateLength}, height {PlateThickness}",
                $"Cylinder at each hole point: radius {HoleRadius}, height {CutterHeight}",
                "BooleanDifference: the plate, Enter, the cutters, Enter");
            challenge.SupportingCommands.AddRange(new[] { "Box", "Cylinder" });
            challenge.SetUp = c =>
            {
                c.AddGuide(new Point(PlateCorner), "Plate corner");
                for (int i = 0; i < Holes.Length; i++)
                    c.AddGuide(new Point(HoleBase(i)), $"Hole {i + 1}");
            };
            challenge.Clicks = c => Marks(("corner", PlateCorner))
                .Concat(Enumerable.Range(0, Holes.Length).Select(i => new ClickMark($"hole {i + 1}", HoleBase(i))));

            foreach (var t in new[] { core, challenge })
            {
                t.ReferenceLayers.AddRange(new[] { ReferenceLayers.Plates, ReferenceLayers.Dowels });
                t.Check = c => new PlateCheck(c.Tutorial, PlateBox, HoleVolume, Holes.Length);
                t.AssumedDimensions.Add(new AssumedDimension("Hole layout", Holes.Length, "holes", "two at each end, 0.10 m from the short edges"));
            }
            return (core, challenge);
        }

        // ================================================================ 2 Pipe

        private static (Tutorial, Tutorial) Handrail()
        {
            var core = Card(2, false, "Pipe", "Stair handrail",
                "Wrap the handrail.", "Turn the centreline into a round rail.",
                Pick("Centreline") + " _Pipe", "Set Cap=Round for rounded ends",
                "Press Run: the centreline is picked",
                $"Type {RailRadius} and press Enter, then Enter again",
                "Press Enter to finish");
            core.SetUp = c => c.AddInput(HandrailCentreline(c), "Centreline");
            core.Clicks = c => Marks(("centreline", Mid(HandrailCentreline(c))));

            var challenge = Card(2, true, "Pipe", "Stair handrail",
                "Draw the handrail's line too.", "Run, bend and return, then Pipe.",
                "_InterpCrv", null,
                "InterpCrv through points 1 to 5; Line from ⑥ to ⑦",
                "BlendCrv across the gap (Curvature), then Join all three",
                $"Pipe the joined line, radius {RailRadius}");
            challenge.SupportingCommands.AddRange(new[] { "InterpCrv", "Line", "BlendCrv", "Join" });
            challenge.SetUp = c =>
            {
                var hr = HandrailRun(c);
                for (int i = 0; i < hr.Points.Count; i++)
                    c.AddGuide(new Point(hr.Points[i]), $"Rail point {i + 1}");
                c.AddGuide(new Point(hr.ReturnStart), "Return start");
                c.AddGuide(new Point(hr.ReturnEnd), "Return end");
            };
            challenge.Clicks = c =>
            {
                var hr = HandrailRun(c);
                return hr.Points.Select(p => new ClickMark("rail", p))
                    .Concat(Marks(("return start", hr.ReturnStart), ("return end", hr.ReturnEnd)));
            };

            foreach (var t in new[] { core, challenge })
            {
                t.ReferenceLayers.Add(ReferenceLayers.StairRail);
                t.Ghosts = c => HandrailRun(c).Ghost is Brep g ? new GeometryBase[] { g } : new GeometryBase[0];
                t.Check = c => new PipeCheck(c.Tutorial, HandrailCentreline(c), RailRadius);
                t.AssumedDimensions.Add(new AssumedDimension("Moved east", DetailShift.X, "m", "keeps the tutorial clear of the real stair"));
            }
            return (core, challenge);
        }

        // ================================================================ 3 OffsetSrf

        private static (Tutorial, Tutorial) Glass()
        {
            var core = Card(3, false, "OffsetSrf", "Facade glass panel",
                "Give the glass thickness.", "One curved facade pane becomes a solid.",
                Pick("Glass") + " _OffsetSrf", "Grow it inward, keeping the outer face in place",
                "Press Run: the glass is picked",
                $"Click Distance and type {GlassThickness}; set Solid=Yes",
                "Click FlipAll so the arrows point inward, then Enter");
            var challenge = Card(3, true, "OffsetSrf", "Facade glass panel",
                "Glass from a 24 mm spec.", "The drawing says 24 mm; the model is in metres.",
                "_OffsetSrf", null,
                "Run OffsetSrf on the glass surface",
                "Work out the distance in metres; set Solid=Yes",
                "Measure the thickness with Distance");

            foreach (var t in new[] { core, challenge })
            {
                t.ReferenceLayers.Add(ReferenceLayers.Glass);
                t.SetUp = c => c.AddInput(EastGlassFace(c), "Glass");
                t.Clicks = c =>
                {
                    Brep f = EastGlassFace(c);
                    return Marks(("glass", AreaMassProperties.Compute(f)?.Centroid ?? f.GetBoundingBox(true).Center));
                };
                t.Check = c => new GlassCheck(c.Tutorial, EastGlassFace(c), GlassThickness);
                t.AssumedDimensions.Add(new AssumedDimension("Glass thickness", GlassThickness, "m", "an example value, not the project's specification"));
            }
            return (core, challenge);
        }

        // ================================================================ 4 ArrayPolar

        private static (Tutorial, Tutorial) SettingOut()
        {
            var core = Card(4, false, "ArrayPolar", "Radial roof setting-out",
                "Set out the roof beams.", "One plane, copied around the centre.",
                Pick("Reference plane") + " _ArrayPolar", null,
                "Press Run, then click the centre ①",
                $"Type {RadialCount}, press Enter",
                "Press Enter twice to fill 360°");
            var challenge = Card(4, true, "ArrayPolar", "Radial roof setting-out",
                "Compare two beam lines.", "Array the planes, then cut the roof with two of them.",
                Pick("Reference plane") + " _ArrayPolar", null,
                $"ArrayPolar the plane around ①, {RadialCount} items",
                "Intersect two neighbouring planes with the roof copy",
                "Compare the two profiles from the side");
            challenge.SupportingCommands.Add("Intersect");

            foreach (var t in new[] { core, challenge })
            {
                bool profiles = t.IsChallenge;
                t.ReferenceLayers.AddRange(new[] { ReferenceLayers.MainBeamsLong, ReferenceLayers.MainBeamsShort });
                t.SetUp = c =>
                {
                    Point3d centre = TutorialGeometry.Centre(c);
                    c.AddInput(UprightPlane(centre, 0, PlaneLength, PlaneHeight), "Reference plane");
                    c.AddGuide(new Point(centre), "Building centre");
                    if (profiles)
                        c.AddInput(TutorialGeometry.Roof(c), "Roof copy");
                };
                t.Clicks = c => Marks(("centre", TutorialGeometry.Centre(c)));
                t.Check = c => new SettingOutCheck(c.Tutorial, TutorialGeometry.Centre(c), RadialCount, "Reference plane",
                    profiles ? TutorialGeometry.Roof(c) : null);
            }
            return (core, challenge);
        }

        // ================================================================ 5 ArrayCrv

        private static (Tutorial, Tutorial) Balustrade()
        {
            var core = Card(5, false, "ArrayCrv", "Stair balustrade",
                "Hang the balustrade.", "One bar, repeated along the rail.",
                Pick("Bar") + " _ArrayCrv", "Fill the whole rail",
                "Press Run, click the bar top ①, then the rail near ②",
                $"Distance between items {BarSpacing}, Orientation No rotation",
                "Press OK");
            var challenge = Card(5, true, "ArrayCrv", "Stair balustrade",
                "Draw the bar first.", $"A {BarHeight} m bar hanging from the rail, then array it.",
                "_Line", null,
                $"Line from ① straight down {BarHeight} m (Vertical option)",
                "ArrayCrv the bar along the rail, base point ①",
                $"{BarSpacing} m apart, No rotation");
            challenge.SupportingCommands.Add("Line");

            foreach (var t in new[] { core, challenge })
            {
                bool giveBar = !t.IsChallenge;
                t.ReferenceLayers.AddRange(new[] { ReferenceLayers.StairFence, ReferenceLayers.StairRail });
                t.SetUp = c =>
                {
                    Curve rail = Interp(HandrailRun(c).Points);
                    c.AddInput(rail, "Rail");
                    Point3d top = rail.PointAtStart;
                    if (giveBar)
                        c.AddInput(new LineCurve(top, top - new Vector3d(0, 0, BarHeight)), "Bar");
                    else
                        c.AddGuide(new Point(top), "Bar top");
                };
                t.Clicks = c =>
                {
                    Curve rail = Interp(HandrailRun(c).Points);
                    return giveBar ? Marks(("bar top", rail.PointAtStart), ("rail", Mid(rail))) : Marks(("bar top", rail.PointAtStart));
                };
                t.Check = c => new BarsCheck(c.Tutorial, Interp(HandrailRun(c).Points), BarHeight, BarSpacing, "Bar");
                t.AssumedDimensions.Add(new AssumedDimension("Bar spacing", BarSpacing, "m", "the model's balustrade is sloping rods; vertical bars are simpler"));
            }
            return (core, challenge);
        }

        // ================================================================ 6 Contour

        private static (Tutorial, Tutorial) Slices()
        {
            var core = Card(6, false, "Contour", "Roof section profiles",
                "Slice the roof.", "Evenly spaced profiles through a roof patch.",
                Pick("Roof patch") + " _Contour", "Contour the solid copy too, and compare",
                "Press Run, then click ① and ②",
                $"Type {ContourSpacing}, press Enter");
            var challenge = Card(6, true, "Contour", "Roof section profiles",
                "Slice it your way.", "Choose the base point, direction and spacing.",
                "_Contour", null,
                "Contour the roof patch",
                "Base on its west edge, direction east (Ortho on)",
                $"Spacing {ContourSpacing}");

            foreach (var t in new[] { core, challenge })
            {
                bool marks = !t.IsChallenge;
                t.ReferenceLayers.Add(ReferenceLayers.Roof);
                t.SetUp = c =>
                {
                    Surface patch = TutorialGeometry.Patch(c);
                    c.AddInput(patch.ToBrep(), "Roof patch");
                    Brep? solid = TutorialGeometry.Thicken(patch, ContourThickness, c.Doc.ModelAbsoluteTolerance);
                    if (solid != null)
                    {
                        solid.Transform(Transform.Translation(ContourShift, 0, 0));
                        c.AddInput(solid, "Solid patch");
                    }
                    if (marks)
                    {
                        var (a, b) = ContourPoints(patch);
                        c.AddGuide(new Point(a), "Base point");
                        c.AddGuide(new Point(b), "Direction point");
                    }
                };
                if (marks)
                    t.Clicks = c =>
                    {
                        var (a, b) = ContourPoints(TutorialGeometry.Patch(c));
                        return Marks(("base", a), ("direction", b));
                    };
                t.Check = c => new ContourCheck(c.Tutorial);
                t.AssumedDimensions.Add(new AssumedDimension("Spacing", ContourSpacing, "m", "a readable rib spacing for a 6 m patch"));
            }
            return (core, challenge);
        }

        // The middle of the patch's west and east edges.
        private static (Point3d, Point3d) ContourPoints(Surface patch)
        {
            Interval u = patch.Domain(0), v = patch.Domain(1);
            return (patch.PointAt(u.Min, v.Mid), patch.PointAt(u.Max, v.Mid));
        }

        // ================================================================ 7 Intersect

        private static (Tutorial, Tutorial) RibProfile()
        {
            var core = Card(7, false, "Intersect", "Radial roof rib",
                "Find a rib's profile.", "Where an upright plane cuts the roof.",
                Pick("Cutting plane", "Roof copy") + " _Intersect", "Get the same curve with Project: project the radial line onto the roof (Top view)",
                "Press Run",
                "Look at the curve from the Front view");
            var challenge = Card(7, true, "Intersect", "Radial roof rib",
                "Make the cutting plane.", "An upright plane from the centre, then Intersect.",
                "_Plane", null,
                $"Plane, Vertical: click ①, then @{PlaneLength}<0, height {PlaneHeight}",
                "Intersect the plane with the roof copy",
                "Check it from the Front view");
            challenge.SupportingCommands.Add("Plane");
            core.SupportingCommands.Add("Project");

            foreach (var t in new[] { core, challenge })
            {
                bool givePlane = !t.IsChallenge;
                t.ReferenceLayers.Add(ReferenceLayers.Roof);
                t.SetUp = c =>
                {
                    Point3d centre = TutorialGeometry.Centre(c);
                    c.AddInput(TutorialGeometry.Roof(c), "Roof copy");
                    if (givePlane)
                    {
                        c.AddInput(UprightPlane(centre, 0, PlaneLength, PlaneHeight), "Cutting plane");
                        c.AddInput(new LineCurve(centre, centre + new Vector3d(PlaneLength, 0, 0)), "Radial line");
                    }
                    else
                    {
                        c.AddGuide(new Point(centre), "Building centre");
                    }
                };
                if (!givePlane)
                    t.Clicks = c => Marks(("centre", TutorialGeometry.Centre(c)));
                t.Check = c => new ProfileCheck(c.Tutorial, TutorialGeometry.Roof(c), TutorialGeometry.Centre(c));
                t.AssumedDimensions.Add(new AssumedDimension("Lift above the real roof", TutorialGeometry.Lift, "m", LiftWhy));
            }
            return (core, challenge);
        }

        // ================================================================ 8 Sweep1

        private static (Tutorial, Tutorial) EdgeBeam()
        {
            var core = Card(8, false, "Sweep1", "Roof-edge beam",
                "Run a beam along the edge.", "A section swept along the roof edge.",
                "_Sweep1", "Cap it into a closed solid",
                "Press Run, click the rail ①, then the section ②, Enter",
                "Frame style Roadlike Top, OK");
            var challenge = Card(8, true, "Sweep1", "Roof-edge beam",
                "Take the rail from the roof.", "DupBorder and Split, then sweep and cap.",
                "_DupBorder", null,
                "DupBorder the roof copy; Split the border at ① and ②",
                $"Rectangle, AroundCurve, at the rail's start: {EdgeBeamWidth} × {EdgeBeamDepth}",
                "Sweep1 with Roadlike Top, then Cap");
            challenge.SupportingCommands.AddRange(new[] { "DupBorder", "Split", "Rectangle", "Cap" });
            core.SupportingCommands.Add("Cap");

            core.SetUp = c =>
            {
                Curve? rail = EdgeRail(c);
                if (rail == null)
                    return;
                c.AddInput(rail, "Rail");
                if (EdgeSection(rail) is Curve section)
                    c.AddInput(section, "Section");
            };
            core.Clicks = c =>
            {
                Curve? rail = EdgeRail(c);
                if (rail == null)
                    return new ClickMark[0];
                Curve? section = EdgeSection(rail);
                return Marks(("rail", Mid(rail))).Concat(section != null ? Marks(("section", Mid(section))) : new ClickMark[0]);
            };
            challenge.SetUp = c =>
            {
                Brep roof = TutorialGeometry.Roof(c);
                c.AddInput(roof, "Roof copy");
                Curve? border = RoofBorder(roof);
                if (border == null)
                    return;
                Point3d centre = TutorialGeometry.Centre(c);
                c.AddGuide(new Point(PointAtBearing(border, centre, -45)), "Split point (south-east)");
                c.AddGuide(new Point(PointAtBearing(border, centre, 45)), "Split point (north-east)");
            };
            challenge.Clicks = c =>
            {
                Curve? border = RoofBorder(TutorialGeometry.Roof(c));
                if (border == null)
                    return new ClickMark[0];
                Point3d centre = TutorialGeometry.Centre(c);
                return Marks(("split", PointAtBearing(border, centre, -45)), ("split", PointAtBearing(border, centre, 45)));
            };

            foreach (var t in new[] { core, challenge })
            {
                bool closed = t.IsChallenge;
                t.ReferenceLayers.Add(ReferenceLayers.RoofEdge);
                t.Check = c => EdgeRail(c) is Curve rail ? new EdgeBeamCheck(c.Tutorial, rail, EdgeBeamWidth, EdgeBeamDepth, closed) : null;
                t.AssumedDimensions.Add(new AssumedDimension("Beam width", EdgeBeamWidth, "m", "the model shows the edge strip, not the beam"));
            }
            return (core, challenge);
        }

        // ================================================================ 9 Sweep2

        private static (Tutorial, Tutorial) Ramp()
        {
            var core = Card(9, false, "Sweep2", "Terrace ramp",
                "Lay the ramp.", "Two rising edges, two landings.",
                "_Sweep2", null,
                "Press Run, click rail ① and rail ②",
                "Click lines ③ and ④, press Enter",
                "Press OK");
            var challenge = Card(9, true, "Sweep2", "Terrace ramp",
                "Draw the ramp's edges.", "Six points, two rails, two lines.",
                "_InterpCrv", null,
                "InterpCrv each edge through its 3 points, both from the low end",
                "Line across each end",
                "Sweep2: the two rails, then the two lines");
            challenge.SupportingCommands.AddRange(new[] { "InterpCrv", "Line" });

            core.SetUp = c =>
            {
                var (a, b) = RampPoints(c);
                c.AddInput(Interp(a), "Rail A");
                c.AddInput(Interp(b), "Rail B");
                c.AddInput(new LineCurve(a[0], b[0]), "Low landing");
                c.AddInput(new LineCurve(a[2], b[2]), "High landing");
            };
            core.Clicks = c =>
            {
                var (a, b) = RampPoints(c);
                return Marks(("rail", a[1]), ("rail", b[1]), ("line", (a[0] + b[0]) / 2), ("line", (a[2] + b[2]) / 2));
            };
            challenge.SetUp = c =>
            {
                var (a, b) = RampPoints(c);
                string[] where = { "start", "middle", "end" };
                for (int i = 0; i < 3; i++)
                {
                    c.AddGuide(new Point(a[i]), "Edge A " + where[i]);
                    c.AddGuide(new Point(b[i]), "Edge B " + where[i]);
                }
            };
            challenge.Clicks = c =>
            {
                var (a, b) = RampPoints(c);
                return a.Select(p => new ClickMark("edge A", p)).Concat(b.Select(p => new ClickMark("edge B", p)));
            };

            foreach (var t in new[] { core, challenge })
            {
                t.ReferenceLayers.AddRange(new[] { ReferenceLayers.Ramp, ReferenceLayers.TerraceOuter });
                t.Ghosts = c =>
                {
                    Brep? ramp = c.Reference?.Ramp.Select(AsBrep).FirstOrDefault();
                    if (ramp == null)
                        return new GeometryBase[0];
                    ramp.Transform(Transform.Translation(0, 0, RampLift));
                    return new GeometryBase[] { ramp };
                };
                t.Check = c =>
                {
                    var (a, b) = RampPoints(c);
                    return new RampCheck(c.Tutorial, new[] { a[0], b[0], a[2], b[2] });
                };
                t.AssumedDimensions.Add(new AssumedDimension("Lift above the terrace", RampLift, "m", "keeps the tutorial clear of the real ramp"));
            }
            return (core, challenge);
        }

        // ================================================================ 10 FlowAlongSrf

        private static (Tutorial, Tutorial) Joists()
        {
            string[] joists = Enumerable.Range(1, JoistCount).Select(i => $"Joist {i}").ToArray();
            var core = Card(10, false, "FlowAlongSrf", "Roof joists",
                "Bend the joists onto the roof.", "Model flat, then flow onto the curve.",
                Pick(joists) + " _FlowAlongSrf", $"Flow all {JoistCount}",
                $"Press Run: the {JoistCount} joists are picked",
                "Click the base near ①",
                "Click the roof near ②");
            var challenge = Card(10, true, "FlowAlongSrf", "Roof joists",
                "Make the joists too.", "Box, Array, then FlowAlongSrf.",
                "_Box", null,
                $"Box from ①: @{JoistWidth},5.88, height {JoistDepth}",
                $"Array {JoistCount} in X, {JoistSpacing} m apart",
                "FlowAlongSrf: the base near ①, the roof near ②");
            challenge.SupportingCommands.AddRange(new[] { "Box", "Array" });

            foreach (var t in new[] { core, challenge })
            {
                bool giveJoists = !t.IsChallenge;
                t.ReferenceLayers.Add(ReferenceLayers.Joists);
                t.SetUp = c =>
                {
                    Surface patch = TutorialGeometry.Patch(c);
                    Brep flat = FlatBase(patch);
                    c.AddInput(patch.ToBrep(), "Roof patch");
                    c.AddInput(flat, "Base");
                    if (giveJoists)
                    {
                        var boxes = FlatJoists(flat);
                        for (int i = 0; i < boxes.Count; i++)
                            c.AddInput(boxes[i], joists[i]);
                    }
                    else
                    {
                        c.AddGuide(new Point(SouthWest(flat)), "Base corner");
                    }
                };
                t.Clicks = c =>
                {
                    Surface patch = TutorialGeometry.Patch(c);
                    return Marks(("base corner", SouthWest(FlatBase(patch))), ("roof corner", SouthWest(patch.ToBrep())));
                };
                t.Check = c => new JoistsCheck(c.Tutorial, TutorialGeometry.Patch(c), JoistCount);
                t.AssumedDimensions.Add(new AssumedDimension("Joist section", JoistWidth, "m wide", $"by {JoistDepth} m deep, roughly the model's joists"));
            }
            return (core, challenge);
        }

        // ================================================================ 11 Loft

        private static (Tutorial, Tutorial) RoofLoft()
        {
            string[] sections = { "Section 1", "Section 2", "Section 3", "Section 4" };
            var core = Card(11, false, "Loft", "Wavy roof patch",
                "Skin the wavy roof.", "Four sections become one surface.",
                Pick(sections) + " _Loft", "Loft with History on, then drag a curve point",
                "For the bonus, turn on Record History first",
                "Press Run: the sections are picked in order",
                "Style Normal, OK");
            var challenge = Card(11, true, "Loft", "Wavy roof patch",
                "Draw the sections.", "Sixteen points, four curves, one loft.",
                "_InterpCrv", "Loft with History on, then drag a curve point",
                "InterpCrv each row of 4 points, all from the same end",
                "Loft the four curves in order, Style Normal",
                "Compare with the blue ghost");
            challenge.SupportingCommands.Add("InterpCrv");
            core.SupportingCommands.Add("History");

            foreach (var t in new[] { core, challenge })
            {
                bool giveCurves = !t.IsChallenge;
                t.ReferenceLayers.Add(ReferenceLayers.Roof);
                t.SetUp = c =>
                {
                    var rows = TutorialGeometry.Grid(TutorialGeometry.Patch(c), 4, 4);
                    for (int i = 0; i < rows.Count; i++)
                    {
                        if (giveCurves)
                            c.AddInput(Interp(rows[i]), sections[i]);
                        else
                            for (int j = 0; j < rows[i].Length; j++)
                                c.AddGuide(new Point(rows[i][j]), $"Row {i + 1} point {j + 1}");
                    }
                };
                t.Clicks = c => TutorialGeometry.Grid(TutorialGeometry.Patch(c), 4, 4).Select((row, i) => new ClickMark($"row {i + 1}", row[0]));
                t.Ghosts = c => new GeometryBase[] { TutorialGeometry.Patch(c).ToBrep() };
                t.Check = c => new LoftCheck(c.Tutorial, TutorialGeometry.Patch(c));
                t.AssumedDimensions.Add(new AssumedDimension("Fit tolerance", 0.10, "m", "how close counts as matching the real roof"));
            }
            return (core, challenge);
        }

        // ================================================================ 12 NetworkSrf

        private static (Tutorial, Tutorial) RoofNetwork()
        {
            string[] edges = { "Edge 1", "Edge 2", "Edge 3", "Edge 4" };
            string[] curves = { "Curve 1", "Curve 2", "Curve 3", "Curve 4" };
            var core = Card(12, false, "NetworkSrf", "Wavy roof patch",
                "Net the roof patch.", "Curves in two directions, one surface.",
                Pick(edges.Concat(curves).ToArray()) + " _NetworkSrf", "Rebuild a copy to 10 × 10 points or fewer",
                "Press Run: all 8 curves are picked",
                "Press OK",
                "Bonus: Rebuild a copy to 6 × 6 points");
            var challenge = Card(12, true, "NetworkSrf", "Wavy roof patch",
                "Draw the network.", "Four curves through the guide points, then NetworkSrf.",
                "_InterpCrv", "Rebuild a copy to 10 × 10 points or fewer",
                "InterpCrv 2 curves each way through the guide points",
                "NetworkSrf: the 4 edges and your 4 curves",
                "Rebuild a copy for the bonus");
            challenge.SupportingCommands.AddRange(new[] { "InterpCrv", "Rebuild" });
            core.SupportingCommands.Add("Rebuild");

            foreach (var t in new[] { core, challenge })
            {
                bool giveCurves = !t.IsChallenge;
                t.ReferenceLayers.Add(ReferenceLayers.Roof);
                t.SetUp = c =>
                {
                    Surface patch = TutorialGeometry.Patch(c);
                    int e = 0;
                    foreach (Curve b in TutorialGeometry.Boundary(patch))
                        c.AddInput(b, edges[e++]);
                    var pts = TutorialGeometry.Grid(patch, 4, 4);
                    if (giveCurves)
                    {
                        var net = Network(pts);
                        for (int i = 0; i < net.Count; i++)
                            c.AddInput(net[i], curves[i]);
                    }
                    else
                    {
                        for (int i = 0; i < 4; i++)
                            for (int j = 0; j < 4; j++)
                                c.AddGuide(new Point(pts[i][j]), $"Guide {i + 1}-{j + 1}");
                    }
                };
                t.Clicks = c =>
                {
                    var pts = TutorialGeometry.Grid(TutorialGeometry.Patch(c), 4, 4);
                    return Marks(("curve", pts[1][0]), ("curve", pts[2][0]), ("curve", pts[0][1]), ("curve", pts[0][2]));
                };
                t.Ghosts = c => new GeometryBase[] { TutorialGeometry.Patch(c).ToBrep() };
                t.Check = c =>
                {
                    Surface patch = TutorialGeometry.Patch(c);
                    var given = TutorialGeometry.Boundary(patch).ToList();
                    if (giveCurves)
                        given.AddRange(Network(TutorialGeometry.Grid(patch, 4, 4)));
                    return new NetworkCheck(c.Tutorial, patch, given);
                };
                t.AssumedDimensions.Add(new AssumedDimension("Fit tolerance", 0.08, "m", "a 4 × 4 net lands a few centimetres from the real roof"));
            }
            return (core, challenge);
        }

        // Two curves each way through the inner rows and columns of a 4 × 4 grid.
        private static List<Curve> Network(List<Point3d[]> pts) => new List<Curve>
        {
            Interp(pts[1]),
            Interp(pts[2]),
            Interp(new[] { pts[0][1], pts[1][1], pts[2][1], pts[3][1] }),
            Interp(new[] { pts[0][2], pts[1][2], pts[2][2], pts[3][2] }),
        };
    }
}
