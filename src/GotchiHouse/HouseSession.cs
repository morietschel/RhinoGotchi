using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.ApplicationSettings;
using Rhino.Commands;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace GotchiHouse
{
    /// <summary>
    /// Tutorial state shared by the panel and the viewport ghost: whether it has
    /// started and which parts of the house the document already contains.
    /// While a part is being built, locked guide points sit where the player
    /// should click, and Point osnap is on, so clicks land exactly.
    /// </summary>
    public static class HouseSession
    {
        public const string DisplayModeName = "GotchiHouse";
        public const string AxonViewName = "Axon";
        private const string GuideLayerName = "GotchiHouse guides";

        private static HouseGhostConduit? _ghost;
        private static bool _dirty;

        private static readonly List<Guid> _guideIds = new List<Guid>();
        private static HousePart? _guidesFor;

        private static bool? _savedOsnap;
        private static OsnapModes _savedOsnapModes;

        public static bool Started { get; private set; }
        public static bool WallsBuilt { get; private set; }
        public static bool RoofBuilt { get; private set; }
        public static bool Complete => WallsBuilt && RoofBuilt;

        public static bool IsBuilt(HousePart part) => part == HousePart.Walls ? WallsBuilt : RoofBuilt;

        /// <summary>The part to build next, or null when the house is complete.</summary>
        public static HousePart? CurrentPart => !WallsBuilt ? HousePart.Walls : !RoofBuilt ? HousePart.Roof : (HousePart?)null;

        /// <summary>The page the player should be on right now.</summary>
        public static int CurrentPageIndex =>
            !Started ? TutorialPage.WelcomeIndex
            : !WallsBuilt ? TutorialPage.WallsIndex
            : !RoofBuilt ? TutorialPage.RoofIndex
            : TutorialPage.DoneIndex;

        /// <summary>Raised on the UI thread when Started or the built parts change.</summary>
        public static event Action? Changed;

        public static void Initialize()
        {
            if (_ghost != null)
                return;
            _ghost = new HouseGhostConduit();

            RhinoDoc.AddRhinoObject += (s, e) => _dirty = true;
            RhinoDoc.DeleteRhinoObject += (s, e) => _dirty = true;
            RhinoDoc.UndeleteRhinoObject += (s, e) => _dirty = true;
            RhinoDoc.ReplaceRhinoObject += (s, e) => _dirty = true;
            RhinoDoc.EndOpenDocument += (s, e) => _dirty = true;
            RhinoDoc.NewDocument += (s, e) => _dirty = true;
            RhinoDoc.CloseDocument += (s, e) => Stop();
            // React as soon as Box / Pyramid / Undo finish, not only when Rhino goes idle.
            Command.EndCommand += (s, e) =>
            {
                if (e.Document != null && Started)
                    Refresh(e.Document, forceNotify: false);
            };
            RhinoApp.Idle += OnIdle;
        }

        /// <summary>Sets up the axon view, snapping and guides, turns the ghost on and checks progress.</summary>
        public static void Start(RhinoDoc doc)
        {
            Started = true;
            if (_ghost != null)
                _ghost.Enabled = true;
            SetUpAxonView(doc);
            Refresh(doc, forceNotify: true);
        }

        public static void Stop()
        {
            if (_ghost != null)
                _ghost.Enabled = false;
            _guideIds.Clear(); // the document is going away; nothing to delete
            _guidesFor = null;
            RestoreSnapping();
            bool wasStarted = Started;
            Started = false;
            if (wasStarted)
                Changed?.Invoke();
        }

        public static void ZoomToHouse(RhinoDoc doc)
        {
            RhinoView? view = FindAxonView(doc) ?? doc.Views.ActiveView;
            if (view == null)
                return;
            BoundingBox box = HouseModel.Bounds;
            box.Inflate(5);
            view.ActiveViewport.ZoomBoundingBox(box);
            view.Redraw();
        }

        private static void OnIdle(object? sender, EventArgs e)
        {
            if (!_dirty)
                return;
            _dirty = false;
            RhinoDoc? doc = RhinoDoc.ActiveDoc;
            if (doc != null)
                Refresh(doc, forceNotify: false);
        }

        private static void Refresh(RhinoDoc doc, bool forceNotify)
        {
            bool wasComplete = Complete;
            (bool walls, bool roof) = Scan(doc);
            bool changed = walls != WallsBuilt || roof != RoofBuilt;
            WallsBuilt = walls;
            RoofBuilt = roof;

            if (Started)
            {
                if (Complete)
                    RestoreSnapping();
                else
                    EnableSnapping();
            }
            bool guidesChanged = UpdateGuides(doc);
            _dirty = false; // our own guide edits don't need another pass

            if (Started && !wasComplete && Complete)
                RhinoApp.WriteLine("[GotchiHouse] Home sweet home! You built the whole house.");

            if (changed || guidesChanged || forceNotify)
            {
                doc.Views.Redraw();
                Changed?.Invoke();
            }
        }

        /// <summary>Looks for solids that match the walls, the roof, or both joined together.</summary>
        private static (bool walls, bool roof) Scan(RhinoDoc doc)
        {
            double tolerance = Math.Max(0.25, doc.ModelAbsoluteTolerance * 10);
            BoundingBox whole = HouseModel.Bounds;
            bool walls = false, roof = false;

            var settings = new ObjectEnumeratorSettings
            {
                NormalObjects = true,
                LockedObjects = true,
                HiddenObjects = false,
                DeletedObjects = false,
                ObjectTypeFilter = ObjectType.Brep | ObjectType.Extrusion,
            };

            foreach (RhinoObject obj in doc.Objects.GetObjectList(settings))
            {
                Brep? brep = obj.Geometry switch
                {
                    Brep b => b,
                    Extrusion x => x.ToBrep(),
                    _ => null,
                };
                if (brep == null)
                    continue;

                BoundingBox box = brep.GetBoundingBox(true);
                if (!walls && HouseModel.Matches(brep, box, HouseModel.Walls.Bounds, HouseModel.Walls.Volume, tolerance))
                    walls = true;
                else if (!roof && HouseModel.Matches(brep, box, HouseModel.Roof.Bounds, HouseModel.Roof.Volume, tolerance))
                    roof = true;
                else if (HouseModel.Matches(brep, box, whole, HouseModel.Walls.Volume + HouseModel.Roof.Volume, tolerance))
                    walls = roof = true; // walls and roof joined with BooleanUnion

                if (walls && roof)
                    break;
            }
            return (walls, roof);
        }

        // ------------------------------------------------------------ guide points

        /// <summary>
        /// Keeps one locked point object at each click location of the part being built.
        /// Osnaps snap to locked objects, so a click near the glowing point lands on it exactly.
        /// Returns true when the guides changed.
        /// </summary>
        private static bool UpdateGuides(RhinoDoc doc)
        {
            HousePart? want = Started ? CurrentPart : null;
            bool intact = _guideIds.All(id => doc.Objects.FindId(id) != null);
            if (want == _guidesFor && intact)
                return false;

            RemoveGuides(doc);
            _guidesFor = want;
            if (want == null)
            {
                int layerIndex = doc.Layers.FindName(GuideLayerName)?.Index ?? -1;
                if (layerIndex >= 0)
                    doc.Layers.Delete(layerIndex, true);
                return true;
            }

            var attributes = new ObjectAttributes
            {
                LayerIndex = GuideLayer(doc),
                Mode = ObjectMode.Locked,
                Name = "GotchiHouse guide",
            };
            foreach (TutorialInput input in TutorialPage.ForPart(want.Value).Inputs)
            {
                Guid id = doc.Objects.AddPoint(input.At, attributes);
                if (id != Guid.Empty)
                    _guideIds.Add(id);
            }
            return true;
        }

        private static void RemoveGuides(RhinoDoc doc)
        {
            foreach (Guid id in _guideIds)
            {
                if (doc.Objects.FindId(id) == null)
                    continue;
                doc.Objects.Unlock(id, true);
                doc.Objects.Delete(id, true);
            }
            _guideIds.Clear();
        }

        private static int GuideLayer(RhinoDoc doc)
        {
            Layer? layer = doc.Layers.FindName(GuideLayerName);
            if (layer != null)
                return layer.Index;
            return doc.Layers.Add(new Layer { Name = GuideLayerName, Color = Palette.RhinoGreen });
        }

        // ------------------------------------------------------------ snapping

        /// <summary>Turns osnap on with Point added; remembers the player's settings once.</summary>
        private static void EnableSnapping()
        {
            if (_savedOsnap == null)
            {
                _savedOsnap = ModelAidSettings.Osnap;
                _savedOsnapModes = ModelAidSettings.OsnapModes;
            }
            ModelAidSettings.Osnap = true;
            ModelAidSettings.OsnapModes = _savedOsnapModes | OsnapModes.Point;
        }

        private static void RestoreSnapping()
        {
            if (_savedOsnap == null)
                return;
            ModelAidSettings.Osnap = _savedOsnap.Value;
            ModelAidSettings.OsnapModes = _savedOsnapModes;
            _savedOsnap = null;
        }

        // ------------------------------------------------------------ view

        private static RhinoView? FindAxonView(RhinoDoc doc) =>
            doc.Views.Find(AxonViewName, false);

        /// <summary>
        /// Turns the Perspective viewport into a parallel (axonometric) view from the
        /// south-east, names it Axon, and gives it the GotchiHouse display mode.
        /// </summary>
        private static void SetUpAxonView(RhinoDoc doc)
        {
            RhinoView? view = FindAxonView(doc) ?? doc.Views.Find("Perspective", false) ?? doc.Views.ActiveView;
            if (view == null)
                return;

            RhinoViewport vp = view.ActiveViewport;
            vp.ChangeToParallelProjection(true);
            Point3d target = HouseModel.Bounds.Center;
            vp.SetCameraLocations(target, target - HouseModel.ViewDirection * 100);
            vp.CameraUp = Vector3d.ZAxis;
            vp.Name = AxonViewName;

            DisplayModeDescription? mode = EnsureDisplayMode();
            if (mode != null)
                vp.DisplayMode = mode;

            doc.Views.ActiveView = view;
            ZoomToHouse(doc);
        }

        /// <summary>
        /// A minimal monochrome display mode in the GotchiHouse palette: white solids,
        /// black edges, white background, soft green shadows, no grid.
        /// </summary>
        private static DisplayModeDescription? EnsureDisplayMode()
        {
            DisplayModeDescription? mode = DisplayModeDescription.FindByName(DisplayModeName);
            if (mode == null)
            {
                DisplayModeDescription? source = DisplayModeDescription.FindByName("Monochrome")
                                                 ?? DisplayModeDescription.FindByName("Shaded");
                if (source == null)
                    return null;
                Guid id = DisplayModeDescription.CopyDisplayMode(source.Id, DisplayModeName);
                mode = DisplayModeDescription.GetDisplayMode(id);
                if (mode == null)
                    return null;
            }

            DisplayPipelineAttributes a = mode.DisplayAttributes;
            a.FillMode = DisplayPipelineAttributes.FrameBufferFillMode.SolidColor;
            a.SetFill(Palette.RhinoPaper);
            a.ShowIsoCurves = false;
            a.ShowSurfaceEdges = true;
            a.SurfaceEdgeThickness = 2;
            a.UseSingleCurveColor = true;
            a.CurveColor = Palette.RhinoInk;
            a.CastShadows = true;
            a.ShadowColor = Palette.RhinoDeepGreen;
            a.ViewSpecificAttributes.DrawGrid = false;
            a.ViewSpecificAttributes.DrawGridAxes = false;
            a.ViewSpecificAttributes.DrawWorldAxes = false;
            DisplayModeDescription.UpdateDisplayMode(mode);
            return mode;
        }
    }
}
