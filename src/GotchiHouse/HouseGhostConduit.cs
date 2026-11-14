using Rhino.Display;
using Rhino.Geometry;

namespace GotchiHouse
{
    /// <summary>
    /// Draws the parts of the house that aren't built yet as a green ghost in the
    /// viewports. The part to build now gets dashed black edges and dots showing
    /// where each typed value lands. Nothing is added to the document.
    /// </summary>
    internal sealed class HouseGhostConduit : DisplayConduit
    {
        private readonly DisplayMaterial _currentFill = new DisplayMaterial(Palette.RhinoGreen, 0.7);
        private readonly DisplayMaterial _laterFill = new DisplayMaterial(Palette.RhinoGreen, 0.88);

        protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e)
        {
            e.IncludeBoundingBox(HouseModel.Bounds);
        }

        protected override void PostDrawObjects(DrawEventArgs e)
        {
            HousePart? current = HouseSession.CurrentPart;
            foreach (PartShape part in HouseModel.Parts)
            {
                if (HouseSession.IsBuilt(part.Part))
                    continue;
                e.Display.DrawMeshShaded(part.Mesh, part.Part == current ? _currentFill : _laterFill);
            }
        }

        protected override void DrawForeground(DrawEventArgs e)
        {
            HousePart? current = HouseSession.CurrentPart;
            foreach (PartShape part in HouseModel.Parts)
            {
                if (HouseSession.IsBuilt(part.Part))
                    continue;

                bool isCurrent = part.Part == current;
                foreach (Line line in part.EdgeLines())
                {
                    if (isCurrent)
                        e.Display.DrawDottedLine(line, Palette.RhinoInk);
                    else
                        e.Display.DrawLine(line, Palette.RhinoGreen, 1);
                }
            }

            // Dots at the points the player types for the current part.
            int pageIndex = current == HousePart.Walls ? TutorialPage.WallsIndex
                          : current == HousePart.Roof ? TutorialPage.RoofIndex
                          : -1;
            if (pageIndex < 0)
                return;
            foreach (TutorialInput input in TutorialPage.All[pageIndex].Inputs)
                e.Display.DrawDot(input.At, input.Value, Palette.RhinoGreen, Palette.RhinoInk);
        }
    }
}
