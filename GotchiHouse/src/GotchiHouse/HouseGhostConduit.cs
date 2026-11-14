using System.Drawing;
using Rhino.Display;
using Rhino.Geometry;

namespace GotchiHouse
{
    /// <summary>
    /// Draws the guide layer in the viewports. Nothing here is added to the document.
    /// - Parts not built yet: a green ghost. The part being built has dashed black edges.
    /// - The points to click: glowing numbered dots.
    /// - Arrows from one click to the next, and a label pill next to each point.
    /// </summary>
    internal sealed class HouseGhostConduit : DisplayConduit
    {
        private const int LabelTextHeight = 13;

        private readonly DisplayMaterial _currentFill = new DisplayMaterial(Palette.RhinoGreen, 0.7);
        private readonly DisplayMaterial _laterFill = new DisplayMaterial(Palette.RhinoGreen, 0.88);
        private static readonly Color Halo = Color.FromArgb(90, Palette.RhinoGreen);

        private static string FontFace => GotchiFonts.IsKorolev ? GotchiFonts.Family.Name : "Arial";

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

            if (current.HasValue)
                DrawGuides(e, TutorialPage.ForPart(current.Value));
        }

        private static void DrawGuides(DrawEventArgs e, TutorialPage page)
        {
            // Arrows first, so the points sit on top of their ends
            foreach (var (from, to) in page.Arrows)
            {
                Point3d a = page.Inputs[from].At, b = page.Inputs[to].At;
                Vector3d d = b - a;
                var line = new Line(a + d * 0.1, b - d * 0.1);
                e.Display.DrawLine(line, Palette.RhinoInk, 2);
                e.Display.DrawArrow(line, Palette.RhinoInk, 16, 0);
            }

            // Labels: black pills with white text (screen-space text dots), pushed away from
            // each point in the label's direction, with a short leader line.
            for (int i = 0; i < page.Inputs.Length; i++)
            {
                TutorialInput input = page.Inputs[i];
                string text = $"{i + 1}  {input.Label}";
                Point2d screen = e.Viewport.WorldToClient(input.At);
                Rectangle size = e.Display.Measure2dText(text, new Point2d(0, 0), false, 0, LabelTextHeight, FontFace);
                float w = size.Width + 16, h = size.Height + 10;
                var center = new PointF(
                    (float)(screen.X + input.LabelX * (w / 2 + 24)),
                    (float)(screen.Y + input.LabelY * (h / 2 + 18)));

                e.Display.Draw2dLine(new PointF((float)screen.X, (float)screen.Y), center, Palette.RhinoInk, 1.5f);
                e.Display.DrawDot(center.X, center.Y, text, Palette.RhinoInk, Palette.RhinoPaper);
            }

            // Glowing numbered points on top
            for (int i = 0; i < page.Inputs.Length; i++)
            {
                Point3d at = page.Inputs[i].At;
                e.Display.DrawPoint(at, PointStyle.RoundSimple, 22f, Halo);
                e.Display.DrawPoint(at, PointStyle.RoundSimple, 13f, Palette.RhinoInk);
                e.Display.DrawPoint(at, PointStyle.RoundSimple, 11f, Palette.RhinoGreen);
                e.Display.Draw2dText((i + 1).ToString(), Palette.RhinoInk, at, true, 12, FontFace);
            }
        }
    }
}
