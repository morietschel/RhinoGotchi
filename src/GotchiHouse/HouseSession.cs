using System;
using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace GotchiHouse
{
    /// <summary>
    /// Tutorial state shared by the panel and the viewport ghost: whether it has
    /// started and which parts of the house the document already contains.
    /// </summary>
    public static class HouseSession
    {
        public const string DisplayModeName = "GotchiHouse";
        public const string AxonViewName = "Axon";

        private static HouseGhostConduit? _ghost;
        private static bool _dirty;

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
            RhinoApp.Idle += OnIdle;
        }

        /// <summary>Sets up the axon view, turns the ghost on and checks progress.</summary>
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
            box.Inflate(4);
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

            if (Started && !wasComplete && Complete)
                RhinoApp.WriteLine("[GotchiHouse] Home sweet home! You built the whole house.");

            if (changed || forceNotify)
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

        private static RhinoView? FindAxonView(RhinoDoc doc) =>
            doc.Views.Find(AxonViewName, false);

        /// <summary>
        /// Turns the Perspective viewport into a parallel (axonometric) view from the
        /// south-west, names it Axon, and gives it the GotchiHouse display mode.
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
