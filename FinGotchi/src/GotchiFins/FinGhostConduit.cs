using System;
using System.Drawing;
using System.Linq;
using Rhino.Display;
using Rhino.Geometry;
using EtoRect = Eto.Drawing.RectangleF;
using EtoSize = Eto.Drawing.SizeF;

namespace GotchiFins
{
    /// <summary>
    /// Draws the game in the viewports. Nothing here is added to the document.
    /// In 3D: the finished facade as a faint ghost, the current target darker with dashed
    /// edges, yellow numbered click points with arrows and labels, and per-level marks (the
    /// extrude arrow, arrows along the array path, the orbit arrow).
    /// On top of the Axon view, like the RhinoGotchi mock-up: the lesson card (top left), the
    /// check card (top right), the yellow "type it up here" callout (top centre), the rhino pet
    /// with its speech bubble (bottom left) and the recorded steps (bottom centre).
    /// </summary>
    internal sealed class FinGhostConduit : DisplayConduit
    {
        private const int LabelTextHeight = 13;
        private const double PetFps = 8;

        private readonly DisplayMaterial _currentFill = new DisplayMaterial(Palette.RhinoInk, 0.82);
        private readonly DisplayMaterial _goalFill = new DisplayMaterial(Palette.RhinoInk, 0.94);
        private static readonly Color Halo = Color.FromArgb(110, Palette.RhinoAccent);

        private Mesh? _massMesh, _finMesh, _arrayMesh;

        private Mesh MassMesh => _massMesh ??= FinModel.Mass.ToMesh();
        private Mesh FinMesh => _finMesh ??= FinModel.Fin.ToMesh();

        private Mesh ArrayMesh
        {
            get
            {
                if (_arrayMesh == null)
                {
                    _arrayMesh = new Mesh();
                    foreach (Solid fin in FinModel.ArrayedFins)
                        _arrayMesh.Append(fin.ToMesh());
                }
                return _arrayMesh;
            }
        }

        protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e)
        {
            e.IncludeBoundingBox(FinModel.Bounds);
        }

        protected override void PostDrawObjects(DrawEventArgs e)
        {
            FinSession.NoteCamera(e.Viewport);

            Stage? current = FinSession.Current;
            if (!current.HasValue || current >= Stage.Sun)
                return;

            // The goal, faintly: whatever isn't built yet
            if (!FinSession.IsDone(Stage.Mass) && current != Stage.Mass)
                e.Display.DrawMeshShaded(MassMesh, _goalFill);
            if (current != Stage.Array)
                e.Display.DrawMeshShaded(ArrayMesh, _goalFill);

            // The current target, stronger
            switch (current.Value)
            {
                case Stage.Mass:
                    e.Display.DrawMeshShaded(MassMesh, _currentFill);
                    break;
                case Stage.Fin:
                    e.Display.DrawMeshShaded(FinMesh, _currentFill);
                    break;
                case Stage.Array:
                    e.Display.DrawMeshShaded(ArrayMesh, _currentFill);
                    break;
            }
        }

        protected override void DrawForeground(DrawEventArgs e)
        {
            Stage? current = FinSession.Current;
            if (current.HasValue)
            {
                GamePage page = GamePage.For(current.Value);
                DrawTarget(e, current.Value);
                DrawClicks(e, page);
            }

            if (e.Viewport.Name == FinSession.AxonViewName)
                DrawHud(e);
        }

        // ---------------------------------------------------------------- HUD

        private static void DrawHud(DrawEventArgs e)
        {
            float s = Math.Max(1f, (float)e.Display.DpiScale);
            System.Drawing.Rectangle view = e.Viewport.Bounds;
            int margin = (int)(14 * s);
            Stage? current = FinSession.Current;
            PetMood mood = FinSession.Mood;
            bool celebrating = mood == PetMood.Win || mood == PetMood.Final;

            // Lesson card, top left
            int top = margin;
            if (current.HasValue)
            {
                GamePage page = GamePage.For(current.Value);
                int misses = FinSession.Misses(current.Value);
                int w = (int)(250 * s);
                string key = $"lesson:{page.Kicker}:{misses}:{s}";
                var lesson = Hud.Get(key, w + (int)(8 * s), (int)(400 * s), g =>
                    new EtoSize(w + 8 * s, Cards.Lesson(g, 1, 1, w, s, page, -1, misses, false) + 8 * s));
                if (lesson.HasValue)
                    e.Display.DrawBitmap(lesson.Value.Bitmap, margin, top);
            }
            else if (FinSession.Complete)
            {
                int w = (int)(250 * s);
                string key = $"lesson:done:{FinSession.FirstTryCount}:{s}";
                var done = Hud.Get(key, w + (int)(8 * s), (int)(300 * s), g =>
                    new EtoSize(w + 8 * s, Cards.Lesson(g, 1, 1, w, s, GamePage.All[GamePage.DoneIndex], -1, 0, true) + 8 * s));
                if (done.HasValue)
                    e.Display.DrawBitmap(done.Value.Bitmap, margin, top);
            }

            // Check card, top right
            if (current.HasValue && FinSession.Checks.Count > 0)
            {
                int w = (int)(220 * s);
                string checks = string.Join("|", FinSession.Checks.Select(c => $"{c.Label}{c.Pass}{c.Detail}"));
                string key = $"check:{checks}:{FinSession.Match:0.000}:{s}";
                var card = Hud.Get(key, w + (int)(8 * s), (int)(300 * s), g =>
                    new EtoSize(w + 8 * s, Cards.Check(g, 1, 1, w, s, FinSession.Checks, FinSession.Match, false) + 8 * s));
                if (card.HasValue && view.Width > 700 * s)
                    e.Display.DrawBitmap(card.Value.Bitmap, view.Width - margin - card.Value.Width, top);
            }

            // Callout, top centre: where to type the command
            if (current.HasValue && !celebrating)
            {
                string command = GamePage.For(current.Value).Command ?? "";
                string text = current == Stage.Sun
                    ? $"Type *{command}* up here and tick On, then right-drag to orbit."
                    : current == Stage.Material
                    ? $"Pick a finish in the panel, then type *{command}* up here: box size 2 × 2 × 2."
                    : $"Type *{command}* in the command line up here, or click the yellow {command} icon.";
                int maxW = (int)Math.Min(420 * s, view.Width * 0.42);
                var callout = Hud.Get($"callout:{text}:{maxW}:{s}", maxW, (int)(120 * s), g => Cards.Callout(g, 1, 1, s, text, maxW - 6 * s));
                if (callout.HasValue)
                    e.Display.DrawBitmap(callout.Value.Bitmap, (view.Width - callout.Value.Width) / 2, (int)(8 * s));
            }

            // Pet and bubble, bottom left
            int petW = (int)((FinSession.Complete ? 180 : 120) * s); // bigger for the finale dance
            var pet = PetFrame(mood, FinSession.MoodTime, petW, s);
            int petBottom = view.Height - margin - (int)(34 * s);
            if (pet.HasValue)
                e.Display.DrawBitmap(pet.Value.Bitmap, margin, petBottom - pet.Value.Height);
            string speech = FinSession.Speech;
            if (!string.IsNullOrEmpty(speech))
            {
                int maxW = (int)(230 * s);
                var bubble = Hud.Get($"bubble:{speech}:{s}", maxW, (int)(160 * s), g => Cards.Bubble(g, 1, 1, s, speech, maxW - 4 * s));
                if (bubble.HasValue)
                    e.Display.DrawBitmap(bubble.Value.Bitmap, margin + petW + (int)(6 * s), petBottom - (int)(60 * s) - bubble.Value.Height / 2);
            }

            // Recorded steps, bottom centre
            if (FinSession.Started)
            {
                var recorded = FinSession.Recorded;
                string key = "recorded:" + string.Join(">", recorded) + ":" + s;
                var pill = Hud.Get(key, (int)(720 * s), (int)(50 * s), g => Cards.Recorded(g, 1, 1, s, recorded) + new EtoSize(4 * s, 6 * s));
                if (pill.HasValue && view.Width > pill.Value.Width + petW * 2)
                    e.Display.DrawBitmap(pill.Value.Bitmap, (view.Width - pill.Value.Width) / 2, view.Height - margin - pill.Value.Height);
            }
        }

        /// <summary>The pet at this moment, from a cache of 8-per-second frames. Awake is a still picture.</summary>
        private static (DisplayBitmap Bitmap, int Width, int Height)? PetFrame(PetMood mood, double time, int width, float s)
        {
            int frames = Math.Max(1, (int)Math.Round(Pet.LoopSeconds(mood) * PetFps));
            int frame = mood == PetMood.Awake ? 0 : (int)(time * PetFps) % frames;
            // The dance jumps higher and leans further, so it gets more room around it
            bool dance = mood == PetMood.Dance;
            int pad = dance ? (int)(width * 0.12) : 0;
            int inner = width - 2 * pad;
            int height = (int)(inner / Pet.Aspect(mood));
            int headroom = (int)(height * (dance ? 0.5 : 0.3));
            return Hud.Get($"pet:{mood}:{frame}:{width}", width, height + headroom, g =>
            {
                Pet.Draw(g, new EtoRect(pad, headroom, inner, height), mood, frame / PetFps);
                return new EtoSize(width, height + headroom);
            });
        }

        // ---------------------------------------------------------------- level diagrams

        private static void DrawTarget(DrawEventArgs e, Stage stage)
        {
            switch (stage)
            {
                case Stage.Mass:
                    DrawDashed(e, FinModel.Mass);
                    break;

                case Stage.Profile:
                    var outline = new Polyline(FinModel.Profile.Concat(new[] { FinModel.Profile[0] }));
                    for (int i = 0; i < outline.Count - 1; i++)
                        e.Display.DrawDottedLine(outline[i], outline[i + 1], Palette.RhinoInk);
                    break;

                case Stage.Fin:
                {
                    DrawDashed(e, FinModel.Fin);
                    // An arrow showing which way the profile is pushed, and by how much
                    Point3d from = new Point3d(0, FinModel.Profile[3].Y - 0.6, FinModel.Height / 2);
                    var push = new Line(from, from + new Vector3d(1.4, 0, 0));
                    e.Display.DrawLine(push, Palette.RhinoInk, 2);
                    e.Display.DrawArrow(push, Palette.RhinoInk, 14, 0);
                    DrawPill(e, push.To, 1, 0.4, "push " + FinModel.ThicknessText);
                    break;
                }

                case Stage.Array:
                {
                    // Little arrows along the path show the way the fins will march
                    for (int k = 0; k < 8; k++)
                    {
                        double s = (k + 0.5) * FinModel.Perimeter / 8;
                        var (at, angle) = FinModel.PathFrame(s);
                        double r = angle * Math.PI / 180;
                        var tangent = new Vector3d(Math.Cos(r), Math.Sin(r), 0);
                        e.Display.DrawArrow(new Line(at - tangent * 1.2, at + tangent * 1.2), Palette.RhinoInk, 12, 0);
                    }
                    Point3d mid = new Point3d(FinModel.Width / 2, 0, 0);
                    e.Display.DrawPoint(mid, PointStyle.RoundSimple, 14f, Palette.RhinoAccent);
                    DrawPill(e, mid, 0.6, 1, "click the path");
                    break;
                }

                case Stage.Material:
                {
                    // The texture box to aim for: a dashed 2 × 2 × 2 cube on the first fin
                    double h = Finishes.MappingSize / 2;
                    var c = new Point3d(FinModel.Thickness / 2, -1.25, FinModel.Height / 2);
                    var cube = new BoundingBox(c - new Vector3d(h, h, h), c + new Vector3d(h, h, h));
                    foreach (Line edge in cube.GetEdges())
                        e.Display.DrawDottedLine(edge, Palette.RhinoInk);
                    DrawPill(e, c + new Vector3d(0, 0, h), -0.8, -1, "texture box 2 × 2 × 2");
                    break;
                }

                case Stage.Sun:
                    DrawOrbitArrow(e);
                    break;
            }
        }

        private static void DrawDashed(DrawEventArgs e, Solid solid)
        {
            foreach (Line line in solid.EdgeLines())
                e.Display.DrawDottedLine(line, Palette.RhinoInk);
        }

        /// <summary>A wide curved arrow under the building: "drag this way to orbit".</summary>
        private static void DrawOrbitArrow(DrawEventArgs e)
        {
            System.Drawing.Rectangle bounds = e.Viewport.Bounds;
            float cx = bounds.Width / 2f, cy = bounds.Height * 0.74f;
            float rx = bounds.Width * 0.26f, ry = bounds.Height * 0.07f;
            PointF At(double a) => new PointF(cx + (float)(rx * Math.Cos(a)), cy + (float)(ry * Math.Sin(a)));

            const double start = 0.15 * Math.PI, end = 0.85 * Math.PI;
            const int n = 32;
            for (int i = 0; i < n; i++)
                e.Display.Draw2dLine(At(start + (end - start) * i / n), At(start + (end - start) * (i + 1) / n), Palette.RhinoInk, 3f);

            // Arrow heads at both ends: you can orbit either way
            foreach (var (tip, back) in new[] { (At(start), At(start + 0.12)), (At(end), At(end - 0.12)) })
            {
                float dx = tip.X - back.X, dy = tip.Y - back.Y;
                float len = (float)Math.Sqrt(dx * dx + dy * dy);
                dx /= len;
                dy /= len;
                e.Display.Draw2dLine(tip, new PointF(tip.X - 14 * dx + 7 * dy, tip.Y - 14 * dy - 7 * dx), Palette.RhinoInk, 3f);
                e.Display.Draw2dLine(tip, new PointF(tip.X - 14 * dx - 7 * dy, tip.Y - 14 * dy + 7 * dx), Palette.RhinoInk, 3f);
            }
            e.Display.DrawDot(cx, cy + ry + 18, "right-drag to orbit", Palette.RhinoInk, Palette.RhinoPaper);
        }

        private static void DrawClicks(DrawEventArgs e, GamePage page)
        {
            Click[] clicks = page.Clicks;

            // Arrows first, so the points sit on top of their ends
            if (page.ArrowsBetweenClicks)
            {
                for (int i = 0; i + 1 < clicks.Length; i++)
                {
                    Point3d a = clicks[i].At, b = clicks[i + 1].At;
                    Vector3d d = b - a;
                    var line = new Line(a + d * 0.15, b - d * 0.15);
                    e.Display.DrawLine(line, Palette.RhinoInk, 1);
                    e.Display.DrawArrow(line, Palette.RhinoInk, 12, 0);
                }
            }

            for (int i = 0; i < clicks.Length; i++)
                DrawPill(e, clicks[i].At, clicks[i].LabelX, clicks[i].LabelY, $"{i + 1}  {clicks[i].Label}");

            // Yellow numbered points on top
            for (int i = 0; i < clicks.Length; i++)
            {
                Point3d at = clicks[i].At;
                e.Display.DrawPoint(at, PointStyle.RoundSimple, 22f, Halo);
                e.Display.DrawPoint(at, PointStyle.RoundSimple, 14f, Palette.RhinoInk);
                e.Display.DrawPoint(at, PointStyle.RoundSimple, 12f, Palette.RhinoAccent);
                e.Display.Draw2dText((i + 1).ToString(), Palette.RhinoInk, at, true, 11, Typeface.ViewportFace);
            }
        }

        /// <summary>A black label pill pushed away from a point in screen direction (dx, dy), with a leader line.</summary>
        private static void DrawPill(DrawEventArgs e, Point3d at, double dx, double dy, string text)
        {
            Point2d screen = e.Viewport.WorldToClient(at);
            System.Drawing.Rectangle size = e.Display.Measure2dText(text, new Point2d(0, 0), false, 0, LabelTextHeight, Typeface.ViewportFace);
            float w = size.Width + 16, h = size.Height + 10;
            var center = new PointF(
                (float)(screen.X + dx * (w / 2 + 22)),
                (float)(screen.Y + dy * (h / 2 + 16)));

            e.Display.Draw2dLine(new PointF((float)screen.X, (float)screen.Y), center, Palette.RhinoInk, 1f);
            e.Display.DrawDot(center.X, center.Y, text, Palette.RhinoInk, Palette.RhinoPaper);
        }
    }
}
