using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.Geometry;

namespace GotchiHouse
{
    /// <summary>
    /// The panel content, drawn as an egg-shaped virtual-pet device: a green shell,
    /// an LCD screen with the key axon of the house and the current instructions,
    /// and three buttons (BACK, GO, NEXT).
    /// The axon shows built parts solid and the rest as a ghost.
    /// </summary>
    internal sealed class GotchiDevice : Drawable
    {
        private const float EggAspect = 1.34f; // height / width
        private const int Back = 0, Go = 1, Next = 2;

        private int _page;
        private int _lastCurrentPage;
        private int _hover = -1;
        private readonly PointF[] _buttonCenters = new PointF[3];
        private float _buttonRadius;

        public GotchiDevice()
        {
            BackgroundColor = Palette.Paper;
            MinimumSize = new Size(200, 290);
            _page = _lastCurrentPage = HouseSession.CurrentPageIndex;
            HouseSession.Changed += OnSessionChanged;
            SizeChanged += (s, e) => Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                HouseSession.Changed -= OnSessionChanged;
            base.Dispose(disposing);
        }

        private TutorialPage Page => TutorialPage.All[_page];

        /// <summary>The player can browse back, but not past the step they're on.</summary>
        private static int MaxPage => HouseSession.CurrentPageIndex;

        private void OnSessionChanged()
        {
            int current = HouseSession.CurrentPageIndex;
            if (current != _lastCurrentPage)
            {
                _page = current; // progress moved: jump to the new step
                _lastCurrentPage = current;
            }
            _page = Math.Min(_page, MaxPage);
            Invalidate();
        }

        // ---------------------------------------------------------------- input

        private bool IsEnabled(int button) => button switch
        {
            Back => _page > 0,
            Next => _page < MaxPage,
            _ => true,
        };

        private int HitButton(PointF p)
        {
            for (int i = 0; i < 3; i++)
            {
                float dx = p.X - _buttonCenters[i].X, dy = p.Y - _buttonCenters[i].Y;
                if (dx * dx + dy * dy <= _buttonRadius * _buttonRadius * 1.3f)
                    return i;
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int hit = HitButton(e.Location);
            if (hit != _hover)
            {
                _hover = hit;
                Cursor = hit >= 0 && IsEnabled(hit) ? Cursors.Pointer : Cursors.Default;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            _hover = -1;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            int hit = HitButton(e.Location);
            if (hit >= 0 && IsEnabled(hit))
            {
                Press(hit);
                e.Handled = true;
            }
            base.OnMouseDown(e);
        }

        private void Press(int button)
        {
            if (button == Back)
            {
                _page--;
            }
            else if (button == Next)
            {
                _page++;
            }
            else
            {
                RhinoDoc? doc = RhinoDoc.ActiveDoc;
                if (doc == null)
                    return;
                switch (Page.Kind)
                {
                    case PageKind.Welcome:
                        HouseSession.Start(doc);
                        break;
                    case PageKind.Build:
                        // "!" cancels any running command first.
                        RhinoApp.RunScript("!" + Page.Macro, true);
                        break;
                    case PageKind.Done:
                        HouseSession.ZoomToHouse(doc);
                        break;
                }
            }
            Invalidate();
        }

        // ---------------------------------------------------------------- drawing

        protected override void OnPaint(PaintEventArgs e) => Render(e.Graphics, Width, Height);

        /// <summary>Draws the whole device into a <paramref name="width"/> x <paramref name="height"/> area.</summary>
        internal void Render(Graphics g, float Width, float Height)
        {
            g.AntiAlias = true;
            g.FillRectangle(Palette.Paper, 0, 0, Width, Height);

            const float margin = 12;
            float w = Math.Max(160, Math.Min(Width - 2 * margin, (Height - 2 * margin - 24) / EggAspect));
            float h = w * EggAspect;
            float cx = Width / 2f;
            float top = margin + 22;
            float s = w / 300f; // scale relative to a 300 px wide device

            var fonts = new List<Font>();
            Font F(float size, bool bold = false)
            {
                Font f = GotchiFonts.Get(Math.Max(6f, size * s), bold);
                fonts.Add(f);
                return f;
            }

            DrawShell(g, cx, top, w, h, s, F(15, true));

            var screen = new RectangleF(cx - 0.36f * w, top + 0.17f * h, 0.72f * w, 0.53f * h);
            DrawScreen(g, screen, s, F);

            DrawButtons(g, cx, top + 0.82f * h, w, h, s, F(8, true));

            foreach (Font f in fonts)
                f.Dispose();
        }

        private static void DrawShell(Graphics g, float cx, float top, float w, float h, float s, Font titleFont)
        {
            // Key-chain loop
            g.DrawEllipse(new Pen(Palette.Ink, 3 * s), cx - 9 * s, top - 20 * s, 18 * s, 26 * s);

            IGraphicsPath egg = EggPath(cx, top, w, h);
            g.FillPath(Palette.Shell, egg);

            // Soft shading at the bottom and a highlight at the top left
            g.FillPath(Palette.ShellDark, EggPath(cx + 0.02f * w, top + 0.06f * h, w * 0.96f, h * 0.94f));
            g.FillPath(Palette.Shell, EggPath(cx - 0.01f * w, top + 0.005f * h, w * 0.95f, h * 0.95f));
            g.FillEllipse(Color.FromArgb(255, 255, 255, 70), cx - 0.30f * w, top + 0.07f * h, 0.16f * w, 0.07f * h);

            g.DrawPath(new Pen(Palette.Ink, 3 * s), egg);

            DrawCentered(g, titleFont, Palette.Ink, "GotchiHouse", cx, top + 0.085f * h);
        }

        /// <summary>An egg: narrower at the top, widest a little below the middle.</summary>
        private static IGraphicsPath EggPath(float cx, float top, float w, float h)
        {
            const float k = 0.5523f;
            float r = w / 2, bottom = top + h, mid = top + 0.57f * h;
            var path = new GraphicsPath();
            path.AddBezier(new PointF(cx, top), new PointF(cx + k * r * 1.1f, top), new PointF(cx + r, mid - k * (mid - top)), new PointF(cx + r, mid));
            path.AddBezier(new PointF(cx + r, mid), new PointF(cx + r, mid + k * (bottom - mid)), new PointF(cx + k * r, bottom), new PointF(cx, bottom));
            path.AddBezier(new PointF(cx, bottom), new PointF(cx - k * r, bottom), new PointF(cx - r, mid + k * (bottom - mid)), new PointF(cx - r, mid));
            path.AddBezier(new PointF(cx - r, mid), new PointF(cx - r, mid - k * (mid - top)), new PointF(cx - k * r * 1.1f, top), new PointF(cx, top));
            path.CloseFigure();
            return path;
        }

        private void DrawScreen(Graphics g, RectangleF screen, float s, Func<float, bool, Font> font)
        {
            // Black bezel, then the LCD
            g.FillPath(Palette.Ink, GraphicsPath.GetRoundRect(screen, 14 * s));
            RectangleF lcd = screen;
            lcd.Inflate(-6 * s, -6 * s);
            g.FillPath(Palette.Screen, GraphicsPath.GetRoundRect(lcd, 9 * s));

            RectangleF c = lcd;
            c.Inflate(-8 * s, -7 * s);
            g.SaveTransform();
            g.SetClip(c);

            TutorialPage page = Page;
            bool done = page.Kind == PageKind.Done;

            // Header: page title + build progress
            Font header = font(10f, true);
            g.DrawText(header, Palette.Ink, c.X, c.Y, page.Title);
            int built = (HouseSession.WallsBuilt ? 1 : 0) + (HouseSession.RoofBuilt ? 1 : 0);
            Font small = font(7.5f, true);
            DrawPips(g, c.Right, c.Y + header.LineHeight / 2, s, built);
            float y = c.Y + header.LineHeight + 3 * s;
            g.DrawLine(new Pen(Palette.Ink, 1.2f * s), c.X, y, c.Right, y);
            y += 4 * s;

            // Key axon of the house
            var art = new RectangleF(c.X, y, c.Width, c.Height * 0.44f);
            DrawHouse(g, art, s, done);
            y = art.Bottom + 4 * s;

            if (done)
            {
                DrawCentered(g, font(14f, true), Palette.Leaf, "YOU DID IT!", c.X + c.Width / 2, y);
                y += font(14f, true).LineHeight + 2 * s;
            }

            // Instructions
            Font body = font(8.5f, false);
            string intro = page.Part.HasValue && HouseSession.IsBuilt(page.Part.Value)
                ? "Built! Press NEXT to continue."
                : page.Intro;
            Color introColor = intro == page.Intro ? Palette.Ink : Palette.Leaf;
            foreach (string line in Wrap(g, body, intro, c.Width))
            {
                g.DrawText(body, introColor, c.X, y, line);
                y += body.LineHeight;
            }
            y += 3 * s;

            if (page.Inputs.Length > 0)
            {
                Font value = font(9.5f, true);
                Font hint = font(8f, false);
                float valueWidth = page.Inputs.Max(i => g.MeasureString(value, i.Value).Width) + 10 * s;
                int step = 1;
                foreach (TutorialInput input in page.Inputs)
                {
                    string number = $"{step++}";
                    g.FillEllipse(Palette.Ink, c.X, y + 1.5f * s, 12 * s, 12 * s);
                    DrawCentered(g, small, Palette.Paper, number, c.X + 6 * s, y + 1.5f * s + (12 * s - small.LineHeight) / 2);
                    g.DrawText(value, Palette.Ink, c.X + 17 * s, y, input.Value);
                    g.DrawText(hint, Palette.Leaf, c.X + 17 * s + valueWidth, y + (value.LineHeight - hint.LineHeight) / 2, input.Hint);
                    y += value.LineHeight + 2 * s;
                }
            }

            g.RestoreTransform();
            g.ResetClip();
        }

        /// <summary>Two squares, filled for each built part.</summary>
        private static void DrawPips(Graphics g, float right, float centerY, float s, int built)
        {
            float size = 8 * s, gap = 4 * s;
            for (int i = 0; i < 2; i++)
            {
                var r = new RectangleF(right - (2 - i) * size - (1 - i) * gap, centerY - size / 2, size, size);
                if (i < built)
                    g.FillRectangle(Palette.Ink, r);
                else
                    g.DrawRectangle(new Pen(Palette.Ink, 1.2f * s), r);
            }
        }

        /// <summary>
        /// Draws the house as a key axon, seen from the same direction as the Axon
        /// viewport. Built parts are solid (white faces, black edges); parts still to
        /// build are a dashed ghost, the current one in black, later ones in green.
        /// </summary>
        private static void DrawHouse(Graphics g, RectangleF area, float s, bool celebrate)
        {
            Vector3d right = HouseModel.ViewRight, up = HouseModel.ViewUp, dir = HouseModel.ViewDirection;

            // Fit the projected house into the area
            var all = HouseModel.Parts.SelectMany(p => p.Vertices).ToArray();
            float minX = all.Min(p => Dot(p, right)), maxX = all.Max(p => Dot(p, right));
            float minY = all.Min(p => -Dot(p, up)), maxY = all.Max(p => -Dot(p, up));
            float scale = Math.Min(area.Width / (maxX - minX), area.Height / (maxY - minY)) * 0.92f;
            float ox = area.X + area.Width / 2 - (minX + maxX) / 2 * scale;
            float oy = area.Y + area.Height / 2 - (minY + maxY) / 2 * scale;
            PointF P(Point3d p) => new PointF(ox + Dot(p, right) * scale, oy - Dot(p, up) * scale);

            HousePart? current = HouseSession.CurrentPart;

            // 1. Ghost fills
            foreach (PartShape part in HouseModel.Parts.Where(p => !HouseSession.IsBuilt(p.Part)))
                foreach (int[] face in part.Faces.Where(f => Facing(part, f, dir)))
                    g.FillPolygon(Palette.GhostFill, face.Select(i => P(part.Vertices[i])).ToArray());

            // 2. Built faces, far to near
            var faces = new List<(double depth, PointF[] points, Color fill)>();
            foreach (PartShape part in HouseModel.Parts.Where(p => HouseSession.IsBuilt(p.Part)))
            {
                foreach (int[] face in part.Faces.Where(f => Facing(part, f, dir)))
                {
                    Point3d centroid = Centroid(part, face);
                    faces.Add((Dot(centroid, dir), face.Select(i => P(part.Vertices[i])).ToArray(), Shade(Normal(part, face))));
                }
            }
            var edgePen = new Pen(Palette.Ink, 1.6f * s) { LineJoin = PenLineJoin.Round };
            foreach (var f in faces.OrderByDescending(f => f.depth))
            {
                g.FillPolygon(f.fill, f.points);
                g.DrawPolygon(edgePen, f.points);
            }

            // 3. Ghost edges, dashed
            foreach (PartShape part in HouseModel.Parts.Where(p => !HouseSession.IsBuilt(p.Part)))
            {
                bool isCurrent = part.Part == current && HouseSession.Started;
                var pen = new Pen(isCurrent ? Palette.Ink : Palette.Leaf, (isCurrent ? 1.3f : 1f) * s)
                {
                    DashStyle = DashStyles.Dash,
                };
                foreach (var (a, b) in part.Edges)
                    g.DrawLine(pen, P(part.Vertices[a]), P(part.Vertices[b]));
            }

            if (celebrate)
            {
                DrawSparkle(g, area.X + area.Width * 0.12f, area.Y + area.Height * 0.25f, 6 * s);
                DrawSparkle(g, area.X + area.Width * 0.88f, area.Y + area.Height * 0.20f, 7 * s);
                DrawSparkle(g, area.X + area.Width * 0.82f, area.Y + area.Height * 0.78f, 5 * s);
                DrawSparkle(g, area.X + area.Width * 0.18f, area.Y + area.Height * 0.80f, 4 * s);
            }
        }

        private static float Dot(Point3d p, Vector3d v) => (float)(p.X * v.X + p.Y * v.Y + p.Z * v.Z);

        private static Point3d Centroid(PartShape part, int[] face)
        {
            var sum = Point3d.Origin;
            foreach (int i in face)
                sum += part.Vertices[i];
            return sum / face.Length;
        }

        /// <summary>Newell's method; faces are counter-clockwise from outside, so this points out.</summary>
        private static Vector3d Normal(PartShape part, int[] face)
        {
            var n = Vector3d.Zero;
            for (int i = 0; i < face.Length; i++)
            {
                Point3d a = part.Vertices[face[i]], b = part.Vertices[face[(i + 1) % face.Length]];
                n.X += (a.Y - b.Y) * (a.Z + b.Z);
                n.Y += (a.Z - b.Z) * (a.X + b.X);
                n.Z += (a.X - b.X) * (a.Y + b.Y);
            }
            return HouseModel.Normalized(n);
        }

        private static bool Facing(PartShape part, int[] face, Vector3d dir) => Normal(part, face) * dir < 0;

        private static readonly Vector3d Light = HouseModel.Normalized(new Vector3d(-0.2, -0.7, 0.7));

        private static Color Shade(Vector3d normal)
        {
            double lit = normal * Light;
            return lit > 0.85 ? Palette.Paper : lit > 0.5 ? Palette.Mint : Palette.Moss;
        }

        private static void DrawSparkle(Graphics g, float x, float y, float r)
        {
            var pen = new Pen(Palette.Leaf, Math.Max(1, r / 3)) { LineCap = PenLineCap.Round };
            g.DrawLine(pen, x - r, y, x + r, y);
            g.DrawLine(pen, x, y - r, x, y + r);
        }

        private void DrawButtons(Graphics g, float cx, float cy, float w, float h, float s, Font labelFont)
        {
            _buttonRadius = 0.062f * w;
            _buttonCenters[Back] = new PointF(cx - 0.23f * w, cy - 0.01f * h);
            _buttonCenters[Go] = new PointF(cx, cy + 0.02f * h);
            _buttonCenters[Next] = new PointF(cx + 0.23f * w, cy - 0.01f * h);
            string[] labels = { "BACK", Page.GoLabel, "NEXT" };

            for (int i = 0; i < 3; i++)
            {
                PointF c = _buttonCenters[i];
                bool enabled = IsEnabled(i);
                float r = _buttonRadius;

                Color fill = !enabled ? Palette.Shell : i == _hover ? Palette.Mint : Palette.Paper;
                Color ink = enabled ? Palette.Ink : Palette.Disabled;

                // Drop shadow, then the button
                g.FillEllipse(Palette.ShellDark, c.X - r, c.Y - r + 3 * s, 2 * r, 2 * r);
                g.FillEllipse(fill, c.X - r, c.Y - r, 2 * r, 2 * r);
                g.DrawEllipse(new Pen(ink, 2.5f * s), c.X - r, c.Y - r, 2 * r, 2 * r);

                float t = r * 0.38f;
                if (i == Back)
                    g.FillPolygon(ink, new PointF(c.X - t, c.Y), new PointF(c.X + t * 0.7f, c.Y - t), new PointF(c.X + t * 0.7f, c.Y + t));
                else if (i == Next)
                    g.FillPolygon(ink, new PointF(c.X + t, c.Y), new PointF(c.X - t * 0.7f, c.Y - t), new PointF(c.X - t * 0.7f, c.Y + t));
                else
                    g.FillEllipse(ink, c.X - t * 0.8f, c.Y - t * 0.8f, t * 1.6f, t * 1.6f);

                DrawCentered(g, labelFont, ink, labels[i], c.X, c.Y + r + 6 * s);
            }
        }

        private static void DrawCentered(Graphics g, Font font, Color color, string text, float cx, float y)
        {
            SizeF size = g.MeasureString(font, text);
            g.DrawText(font, color, cx - size.Width / 2, y, text);
        }

        private static IEnumerable<string> Wrap(Graphics g, Font font, string text, float maxWidth)
        {
            foreach (string paragraph in text.Split('\n'))
            {
                string line = "";
                foreach (string word in paragraph.Split(' '))
                {
                    string candidate = line.Length == 0 ? word : line + " " + word;
                    if (line.Length > 0 && g.MeasureString(font, candidate).Width > maxWidth)
                    {
                        yield return line;
                        line = word;
                    }
                    else
                    {
                        line = candidate;
                    }
                }
                yield return line;
            }
        }
    }
}
