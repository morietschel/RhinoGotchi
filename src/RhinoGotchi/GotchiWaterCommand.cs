using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using RhinoGotchi.Water;

namespace RhinoGotchi
{
    /// <summary>
    /// Water Run on the command line: start a level, switch between playing and editing it, build with
    /// sources, goals and hazards, save. The panel's Water Run page runs this command for anything that
    /// changes the model, so every step can be undone.
    /// </summary>
    [Guid("b3d9a6f2-41c7-4e0b-9a58-7c2e6f1d8b04")]
    public class GotchiWaterCommand : Command
    {
        public const string Name = "GotchiWater";

        public override string EnglishName => Name;

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var rate = new OptionInteger(WaterLab.Rate, 1, 600);
            var target = new OptionInteger(WaterLab.Target, 1, 100000);
            var size = new OptionDouble(WaterLab.DropSize, 0, 1e6);

            while (true)
            {
                var go = new GetOption();
                string state = WaterGame.Current is WaterLevel level ? $"level {level.Number}, {(WaterGame.Editing ? "editing" : "playing")}"
                    : WaterGame.Active ? "sandbox" : "no level";
                go.SetCommandPrompt($"Water Run ({state}: {WaterLab.ScoreLine()})");
                go.AcceptNothing(true);
                int pickLevel = go.AddOption("Level");
                int restart = go.AddOption("Restart");
                int next = go.AddOption("Next");
                int edit = go.AddOption(WaterGame.Editing ? "PlayMode" : "EditMode");
                int save = go.AddOption("SaveLevel");
                int revert = go.AddOption("RevertLevel");
                int source = go.AddOption("AddSource");
                int goal = go.AddOption("AddGoal");
                int hazard = go.AddOption("AddHazard");
                int makeFixed = go.AddOption("Fixed");
                int makeMovable = go.AddOption("Movable");
                int untag = go.AddOption("Untag");
                int play = go.AddOption(WaterLab.Running ? "Pause" : "Play");
                int reset = go.AddOption("ResetWater");
                int quit = go.AddOption("Quit");
                go.AddOptionInteger("Rate", ref rate, "Drops per second from each source");
                go.AddOptionInteger("GoalAmount", ref target, "Drops the goal needs");
                go.AddOptionDouble("DropSize", ref size, "Drop radius in model units (0 = automatic)");

                GetResult result = go.Get();
                if (result == GetResult.Nothing)
                    return Result.Success;
                if (result != GetResult.Option)
                    return go.CommandResult() == Result.Success ? Result.Cancel : go.CommandResult();

                WaterLab.Rate = rate.CurrentValue;
                WaterLab.Target = target.CurrentValue;
                bool resized = Math.Abs(WaterLab.DropSize - size.CurrentValue) > 1e-12;
                WaterLab.DropSize = size.CurrentValue;
                if (resized)
                    WaterLab.Reset();

                int choice = go.Option().Index;
                if (choice == pickLevel)
                {
                    int number = WaterGame.Current?.Number ?? 1;
                    Result got = RhinoGet.GetInteger("Level number (0 = empty sandbox)", false, ref number, 0, WaterLevelCatalog.All.Count);
                    if (got != Result.Success)
                        return got;
                    return Started(WaterGame.Start(doc, number));
                }
                if (choice == restart)
                    return Started(WaterGame.Start(doc, WaterGame.Current?.Number ?? 0));
                if (choice == next)
                {
                    int number = (WaterGame.Current?.Number ?? 0) + 1;
                    return Started(WaterGame.Start(doc, number > WaterLevelCatalog.All.Count ? 1 : number));
                }
                if (choice == edit)
                {
                    if (!WaterGame.Active)
                        return Started(WaterGame.Start(doc, 0));
                    WaterGame.SetMode(doc, !WaterGame.Editing);
                    return Result.Success;
                }
                if (choice == save)
                    return WaterGame.Save(doc) != null ? Result.Success : Result.Failure;
                if (choice == revert)
                {
                    if (WaterGame.Current == null)
                        return Result.Nothing;
                    int number = WaterGame.Current.Number;
                    WaterGame.Revert(number);
                    return Started(WaterGame.Start(doc, number));
                }
                if (choice == source)
                    return AddSource(doc);
                if (choice == goal)
                    return AddVolume(doc, PieceRole.Goal, "goal", "Water goal");
                if (choice == hazard)
                    return AddVolume(doc, PieceRole.Hazard, "hazard", "Hazard");
                if (choice == makeFixed || choice == makeMovable)
                    return SetFixed(doc, choice == makeFixed);
                if (choice == untag)
                    return Untag(doc);
                if (choice == play)
                {
                    if (WaterLab.Running)
                        WaterLab.Pause();
                    else
                        WaterLab.Start(doc);
                    return Result.Success;
                }
                if (choice == reset)
                {
                    WaterLab.Reset();
                    WaterLab.Start(doc);
                    return Result.Success;
                }
                if (choice == quit)
                {
                    WaterGame.Quit(doc);
                    return Result.Success;
                }
                // A setting was changed: show the options again.
            }
        }

        private static Result Started(bool ok) => ok ? Result.Success : Result.Failure;

        /// <summary>A source is a point (water drips straight down) or an arrow (a jet along it).</summary>
        private static Result AddSource(RhinoDoc doc)
        {
            var first = new GetPoint();
            first.SetCommandPrompt("Water source: where does the water come out?");
            if (first.Get() != GetResult.Point)
                return first.CommandResult();
            Point3d from = first.Point();

            var second = new GetPoint();
            second.SetCommandPrompt("Aim the jet (a longer arrow is a stronger jet), or press Enter for a drip straight down");
            second.AcceptNothing(true);
            second.SetBasePoint(from, true);
            second.DrawLineFromPoint(from, true);
            GetResult aimed = second.Get();
            if (aimed != GetResult.Point && aimed != GetResult.Nothing)
                return second.CommandResult();

            ObjectAttributes attributes = WaterGame.Attributes(doc, PieceRole.Source, true, "Water source");
            if (aimed == GetResult.Point && second.Point().DistanceTo(from) > doc.ModelAbsoluteTolerance)
            {
                attributes.ObjectDecoration = ObjectDecoration.EndArrowhead;
                doc.Objects.AddLine(from, second.Point(), attributes);
            }
            else
                doc.Objects.AddPoint(from, attributes);

            WaterLab.Start(doc);
            WaterLab.MarkDirty();
            doc.Views.Redraw();
            return Result.Success;
        }

        /// <summary>
        /// Goals and hazards are any surface, solid or mesh. A goal stops being solid and collects the water
        /// that reaches it; a hazard stays solid and loses the level when water or a movable piece touches it.
        /// </summary>
        private static Result AddVolume(RhinoDoc doc, PieceRole role, string word, string name)
        {
            var go = new GetObject();
            go.SetCommandPrompt($"Select objects to turn into {word}s, or press Enter to draw a {word} box");
            go.GeometryFilter = ObjectType.Surface | ObjectType.Brep | ObjectType.Extrusion | ObjectType.Mesh | ObjectType.SubD;
            go.AcceptNothing(true);
            go.SubObjectSelect = false;
            GetResult result = go.GetMultiple(1, 0);

            if (result == GetResult.Object)
            {
                WaterGame.SetRole(doc, Picked(go), role, true);
                doc.Objects.UnselectAll();
            }
            else if (result == GetResult.Nothing)
            {
                Result boxResult = RhinoGet.GetBox(out Box box);
                if (boxResult != Result.Success)
                    return boxResult;
                Brep? brep = box.ToBrep();
                if (brep == null)
                    return Result.Failure;
                doc.Objects.AddBrep(brep, WaterGame.Attributes(doc, role, true, name));
            }
            else
                return go.CommandResult();

            WaterLab.Start(doc);
            WaterLab.MarkDirty();
            doc.Views.Redraw();
            return Result.Success;
        }

        /// <summary>Fixed pieces are locked while playing; movable ones are what the player works with.</summary>
        private static Result SetFixed(RhinoDoc doc, bool isFixed)
        {
            var go = new GetObject();
            go.SetCommandPrompt(isFixed ? "Select pieces the player may not move" : "Select pieces the player may move");
            go.SubObjectSelect = false;
            if (go.GetMultiple(1, 0) != GetResult.Object)
                return go.CommandResult();
            WaterGame.SetRole(doc, Picked(go), null, isFixed);
            doc.Objects.UnselectAll();
            if (!WaterGame.Editing && WaterGame.Active)
                WaterGame.SetMode(doc, false); // lock what was just fixed
            return Result.Success;
        }

        /// <summary>Turns sources, goals and hazards back into ordinary solid pieces.</summary>
        private static Result Untag(RhinoDoc doc)
        {
            var go = new GetObject();
            go.SetCommandPrompt("Select sources, goals or hazards to turn back into ordinary pieces");
            go.SetCustomGeometryFilter((obj, geometry, component) =>
                !string.IsNullOrEmpty(obj.Attributes.GetUserString(WaterLab.TagKey)));
            go.SubObjectSelect = false;
            if (go.GetMultiple(1, 0) != GetResult.Object)
                return go.CommandResult();
            WaterGame.SetRole(doc, Picked(go), PieceRole.Solid, false);
            doc.Objects.UnselectAll();
            return Result.Success;
        }

        private static List<RhinoObject> Picked(GetObject go) =>
            go.Objects().Select(reference => reference.Object()).Where(obj => obj != null).ToList();
    }
}
