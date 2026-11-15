using System;
using Eto.Drawing;
using Eto.Forms;

namespace RhinoGotchi.UI
{
    /// <summary>The part of the Gumball a level is about.</summary>
    internal enum GumballPart { Move, Plane, Rotate, Scale, Type, Extrude, Relocate, Align, Copy }

    /// <summary>
    /// A small looping picture of the Gumball doing the thing a level teaches: the handle to grab is
    /// bright and pulsing, the rest is faded, and an orange piece shows what dragging it does.
    /// </summary>
    internal sealed class GumballHint : Drawable
    {
        private enum Handle { XArrow, PlaneSquare, Arc, XScale, GreenArrow, Centre, Ball }

        private const float BoxW = 240, BoxH = 124;
        private static readonly PointF GreenDir = new PointF(-0.78f, 0.62f);

        private readonly GumballPart _part;
        private readonly Handle _handle;
        private readonly string _label;
        private readonly bool _threeD;
        private double _t;

        public GumballHint(GumballPart part, string label, bool threeD)
        {
            _part = part;
            _label = label.ToUpperInvariant();
            _threeD = threeD;
            _handle = part switch
            {
                GumballPart.Move => Handle.XArrow,
                GumballPart.Plane => Handle.PlaneSquare,
                GumballPart.Copy => Handle.PlaneSquare,
                GumballPart.Rotate => Handle.Arc,
                GumballPart.Type => Handle.Arc,
                GumballPart.Scale => Handle.XScale,
                GumballPart.Extrude => Handle.GreenArrow,
                GumballPart.Relocate => Handle.Centre,
                _ => Handle.Ball,
            };
            Height = 132;
            SizeChanged += (s, e) => Invalidate();
        }

        public static GumballPart PartFor(string hint) => hint switch
        {
            "plane" => GumballPart.Plane,
            "rotate" => GumballPart.Rotate,
            "scale" => GumballPart.Scale,
            "type" => GumballPart.Type,
            "extrude" => GumballPart.Extrude,
            "relocate" => GumballPart.Relocate,
            "align" => GumballPart.Align,
            "copy" => GumballPart.Copy,
            _ => GumballPart.Move,
        };

        public void Tick(double t)
        {
            _t = t;
            Invalidate();
        }

        private static float Smooth(double u)
        {
            float x = (float)Math.Max(0, Math.Min(1, u));
            return x * x * (3 - 2 * x);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var p = Palette.Current;
            g.AntiAlias = true;

            g.DrawText(Ui.Bold(8.5f), p.SubtleText, 0, 0, _label);

            float scale = Math.Min(1.5f, Math.Min(Width / BoxW, Height / BoxH));
            if (scale <= 0.05f)
                return;
            g.SaveTransform();
            g.TranslateTransform((Width - BoxW * scale) / 2, (Height - BoxH * scale) / 2 + 4);
            g.ScaleTransform(scale);

            // One loop: wait, drag, hold, let go.
            double time = _t % 3.2;
            float k = time < 2.7 ? Smooth((time - 0.5) / 1.2) : 1 - Smooth((time - 2.7) / 0.5);
            float pulse = (float)(_t * 1.4 % 1.0);

            Color piece = p.Orange;
            Color ghost = new Color(piece, 0.22f);
            var o = new PointF(92, 74);

            switch (_part)
            {
                case GumballPart.Move:
                    Plank(g, o, 84, 10, 0, ghost);
                    o.X += 46 * k;
                    Plank(g, o, 84, 10, 0, piece);
                    Gumball(g, o, 0, pulse);
                    break;

                case GumballPart.Plane:
                    Plank(g, o, 84, 10, 0, ghost);
                    o = new PointF(o.X + 40 * k, o.Y - 24 * k);
                    Plank(g, o, 84, 10, 0, piece);
                    Gumball(g, o, 0, pulse);
                    break;

                case GumballPart.Rotate:
                    Plank(g, o, 96, 10, 0, ghost);
                    Plank(g, o, 96, 10, -28 * k, piece);
                    Gumball(g, o, 0, pulse);
                    break;

                case GumballPart.Type:
                    Plank(g, o, 76, 10, 0, ghost);
                    Plank(g, o, 76, 10, -75 * k, piece);
                    Gumball(g, o, 0, pulse);
                    Key(g, "75 ⏎", p);
                    break;

                case GumballPart.Scale:
                    Plank(g, o, 60, 10, 0, ghost);
                    Plank(g, new PointF(o.X + 24 * k, o.Y), 60 + 48 * k, 10, 0, piece);
                    Gumball(g, o, 0, pulse);
                    break;

                case GumballPart.Extrude:
                {
                    var a = new PointF(o.X - 42, o.Y);
                    var b = new PointF(o.X + 42, o.Y);
                    float d = 38 * k;
                    if (k > 0.01f)
                        g.FillPolygon(new Color(piece, 0.85f), a, b,
                            new PointF(b.X + GreenDir.X * d, b.Y + GreenDir.Y * d),
                            new PointF(a.X + GreenDir.X * d, a.Y + GreenDir.Y * d));
                    using (var pen = new Pen(piece, 3))
                        g.DrawLine(pen, a, b);
                    Gumball(g, o, 0, pulse);
                    Key(g, Mod, p);
                    break;
                }

                case GumballPart.Relocate:
                    Plank(g, o, 96, 10, 0, piece);
                    g.FillEllipse(p.DisabledText, o.X - 48 - 6, o.Y - 6, 12, 12); // the pin
                    Gumball(g, new PointF(o.X - 48 * k, o.Y), 0, pulse);
                    Key(g, Mod, p);
                    break;

                case GumballPart.Align:
                    Plank(g, o, 110, 10, 24, piece);
                    Gumball(g, o, 24 * k, pulse);
                    break;

                default: // Copy
                    o = new PointF(60, 54);
                    Plank(g, o, 64, 10, 0, piece);
                    var copy = new PointF(o.X + 62 * k, o.Y + 26 * k);
                    if (k > 0.02f)
                        Plank(g, copy, 64, 10, 0, piece);
                    Gumball(g, copy, 0, pulse);
                    Key(g, "alt", p);
                    break;
            }

            g.RestoreTransform();
        }

        private static string Mod => Rhino.Runtime.HostUtils.RunningOnOSX ? "cmd" : "ctrl";

        private static void Plank(Graphics g, PointF centre, float length, float thickness, float angle, Color color)
        {
            g.SaveTransform();
            g.TranslateTransform(centre.X, centre.Y);
            if (angle != 0)
                g.RotateTransform(angle);
            g.FillPath(color, GraphicsPath.GetRoundRect(new RectangleF(-length / 2, -thickness / 2, length, thickness), 3));
            g.RestoreTransform();
        }

        /// <summary>A keyboard key in the corner: the modifier to hold, or the number to type.</summary>
        private static void Key(Graphics g, string text, Palette p)
        {
            var font = Ui.Bold(9.5f);
            float w = font.MeasureString(text).Width + 16;
            var rect = new RectangleF(BoxW - w - 2, 4, w, 24);
            g.FillPath(p.Border, GraphicsPath.GetRoundRect(new RectangleF(rect.X, rect.Y + 3, rect.Width, rect.Height), 6));
            g.FillPath(p.Border, GraphicsPath.GetRoundRect(rect, 6));
            g.FillPath(p.TileFace, GraphicsPath.GetRoundRect(new RectangleF(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4), 4));
            g.DrawText(font, p.Text, rect.X + 8, rect.Y + (rect.Height - font.LineHeight) / 2, text);
        }

        private Color Tone(Color color, Handle handle) => handle == _handle ? color : new Color(color, 0.3f);

        private float Weight(Handle handle) => handle == _handle ? 3.5f : 2f;

        private void Gumball(Graphics g, PointF origin, float angle, float pulse)
        {
            var p = Palette.Current;
            Color red = Color.FromArgb(0xE0, 0x48, 0x3E);
            Color blue = Color.FromArgb(0x3B, 0x7D, 0xD8);
            Color green = Color.FromArgb(0x3F, 0xA3, 0x4D);

            g.SaveTransform();
            g.TranslateTransform(origin.X, origin.Y);
            if (angle != 0)
                g.RotateTransform(angle);

            // Up (blue): never the one to grab in these levels.
            using (var pen = new Pen(new Color(blue, 0.3f), 2))
                g.DrawLine(pen, 0, 0, 0, -56);
            g.FillPolygon(new Color(blue, 0.3f), new PointF(-6, -56), new PointF(0, -70), new PointF(6, -56));
            g.FillRectangle(new Color(blue, 0.3f), -3.5f, -43.5f, 7, 7);

            // The third arrow only exists once the levels go 3D.
            if (_threeD)
            {
                Color c = Tone(green, Handle.GreenArrow);
                float len = 44;
                var tip = new PointF(GreenDir.X * len, GreenDir.Y * len);
                var side = new PointF(-GreenDir.Y * 6, GreenDir.X * 6);
                using (var pen = new Pen(c, Weight(Handle.GreenArrow)))
                    g.DrawLine(pen, 0, 0, tip.X, tip.Y);
                g.FillPolygon(c,
                    new PointF(tip.X + side.X, tip.Y + side.Y),
                    new PointF(tip.X + GreenDir.X * 14, tip.Y + GreenDir.Y * 14),
                    new PointF(tip.X - side.X, tip.Y - side.Y));
                g.FillEllipse(c, GreenDir.X * 26 - 4, GreenDir.Y * 26 - 4, 8, 8);
            }

            // Right (red): the move arrow and its scale square.
            Color arrow = Tone(red, Handle.XArrow);
            using (var pen = new Pen(_handle == Handle.XScale ? red : arrow, Weight(Handle.XArrow)))
                g.DrawLine(pen, 0, 0, 56, 0);
            g.FillPolygon(arrow, new PointF(56, -6), new PointF(70, 0), new PointF(56, 6));
            float square = _handle == Handle.XScale ? 11 : 7;
            g.FillRectangle(Tone(red, Handle.XScale), 40 - square / 2, -square / 2, square, square);

            // The rotate arc and the plane square between the two arrows.
            using (var pen = new Pen(Tone(blue, Handle.Arc), Weight(Handle.Arc)))
                g.DrawArc(pen, -30, -30, 60, 60, 270, 90);
            using (var pen = new Pen(Tone(p.Text, Handle.PlaneSquare), Weight(Handle.PlaneSquare)))
                g.DrawRectangle(pen, 11, -22, 11, 11);
            if (_handle == Handle.PlaneSquare)
                g.FillRectangle(new Color(p.Gold, 0.55f), 11, -22, 11, 11);

            // The centre and the little menu ball.
            float centre = _handle == Handle.Centre ? 5.5f : 3.5f;
            g.FillEllipse(p.Text, -centre, -centre, centre * 2, centre * 2);
            float ball = _handle == Handle.Ball ? 6 : 4;
            g.FillEllipse(Tone(p.Text, Handle.Ball), -13 - ball, -13 - ball, ball * 2, ball * 2);
            g.FillEllipse(p.TileFace, -13 - ball + 2, -13 - ball + 2, ball * 2 - 4, ball * 2 - 4);

            // A ring that keeps pulsing on the handle to grab.
            PointF at = _handle switch
            {
                Handle.XArrow => new PointF(63, 0),
                Handle.PlaneSquare => new PointF(16.5f, -16.5f),
                Handle.Arc => new PointF(21.2f, -21.2f),
                Handle.XScale => new PointF(40, 0),
                Handle.GreenArrow => new PointF(GreenDir.X * 50, GreenDir.Y * 50),
                Handle.Centre => new PointF(0, 0),
                _ => new PointF(-13, -13),
            };
            float r = 9 + 9 * pulse;
            using (var pen = new Pen(new Color(p.Accent, 0.75f * (1 - pulse)), 2.5f))
                g.DrawEllipse(pen, at.X - r, at.Y - r, r * 2, r * 2);

            g.RestoreTransform();
        }
    }

    /// <summary>
    /// The score as a tank that fills with water: the count in big figures, waves that keep moving,
    /// gold once the goal amount is in.
    /// </summary>
    internal sealed class WaterGauge : Drawable
    {
        private const int Lip = 3;
        private int _count;
        private int _target = 1;
        private bool _done;
        private string _side = "";
        private float _level;
        private double _t;

        public WaterGauge()
        {
            Height = 72;
            SizeChanged += (s, e) => Invalidate();
        }

        public void Set(int count, int target, bool done, string side)
        {
            if (count == _count && target == _target && done == _done && side == _side)
                return;
            _count = count;
            _target = Math.Max(1, target);
            _done = done;
            _side = side;
            Invalidate();
        }

        public void Tick(double t)
        {
            _t = t;
            float want = _done ? 1 : Math.Min(1f, _count / (float)_target);
            _level += (want - _level) * 0.15f; // the water rises smoothly, it doesn't jump
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var p = Palette.Current;
            g.AntiAlias = true;
            float w = Width - 1, h = Height - 1 - Lip;
            if (w <= 8 || h <= 8)
                return;

            Color edge = _done ? p.Gold : p.Border;
            g.FillPath(edge, GraphicsPath.GetRoundRect(new RectangleF(0, Lip, w, h), 12));
            g.FillPath(edge, GraphicsPath.GetRoundRect(new RectangleF(0, 0, w, h), 12));
            var inner = GraphicsPath.GetRoundRect(new RectangleF(2, 2, w - 4, h - 4), 10);
            g.FillPath(p.TileFace, inner);

            // Water: two waves sliding past each other, never quite empty so it reads as a tank.
            Color water = _done ? p.Gold : p.Blue;
            float top = 2 + (h - 4) * (1 - (0.07f + 0.93f * _level));
            g.SetClip(inner);
            Wave(g, w, h, top + 2, 0.050f, 1.7, 2.4f, new Color(water, 0.22f));
            Wave(g, w, h, top + 4, 0.036f, -2.3, 3.2f, new Color(water, 0.30f));
            g.ResetClip();

            var big = Ui.Bold(21);
            var small = Ui.Regular(10.5f);
            string count = _count.ToString();
            float y = (h - big.LineHeight) / 2;
            g.DrawText(big, p.Text, 14, y, count);
            float x = 14 + big.MeasureString(count).Width + 5;
            g.DrawText(small, p.SubtleText, x, y + big.LineHeight - small.LineHeight - 4, "/ " + _target);
            if (_done)
                Ui.Check(g, new RectangleF(x + small.MeasureString("/ " + _target).Width + 8, (h - 20) / 2, 20, 20), p.Green);

            if (_side.Length > 0)
            {
                var font = Ui.Regular(9.5f);
                var size = font.MeasureString(_side);
                g.DrawText(font, p.SubtleText, w - 14 - size.Width, (h - size.Height) / 2, _side);
            }
        }

        private void Wave(Graphics g, float w, float h, float top, float frequency, double speed, float height, Color color)
        {
            int steps = Math.Max(2, (int)(w / 6));
            var points = new PointF[steps + 3];
            for (int i = 0; i <= steps; i++)
            {
                float x = w * i / steps;
                points[i] = new PointF(x, top + (float)Math.Sin(x * frequency + _t * speed) * height);
            }
            points[steps + 1] = new PointF(w, h);
            points[steps + 2] = new PointF(0, h);
            g.FillPolygon(color, points);
        }
    }

    /// <summary>
    /// How the level ended, as a picture and three words: a spinning gold star in falling confetti for
    /// a win, a shaking red cross for touching red.
    /// </summary>
    internal sealed class ResultBanner : Drawable
    {
        private const int Lip = 3;
        private readonly bool _won;
        private readonly string _title;
        private readonly string _line;
        private double _start = -1;
        private double _t;

        public ResultBanner(bool won, string title, string line)
        {
            _won = won;
            _title = title;
            _line = line;
            Height = 66;
            SizeChanged += (s, e) => Invalidate();
        }

        public void Tick(double t)
        {
            if (_start < 0)
                _start = t;
            _t = t - _start;
            Invalidate();
        }

        private static float Hash(int i, int salt)
        {
            double v = Math.Sin(i * 127.1 + salt * 311.7) * 43758.5453;
            return (float)(v - Math.Floor(v));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var p = Palette.Current;
            g.AntiAlias = true;
            float w = Width - 1, h = Height - 1 - Lip;
            if (w <= 8 || h <= 8)
                return;

            Color edge = _won ? p.Green : p.Red;
            g.FillPath(edge, GraphicsPath.GetRoundRect(new RectangleF(0, Lip, w, h), 12));
            g.FillPath(edge, GraphicsPath.GetRoundRect(new RectangleF(0, 0, w, h), 12));
            var inner = GraphicsPath.GetRoundRect(new RectangleF(2, 2, w - 4, h - 4), 10);
            g.FillPath(_won ? p.GreenBanner : p.RedBanner, inner);

            float cx = 36, cy = h / 2;
            if (_won)
            {
                g.SetClip(inner);
                Color[] colors = { p.Gold, p.Green, p.Blue, p.Orange, p.Purple };
                for (int i = 0; i < 26; i++)
                {
                    float x = Hash(i, 1) * w;
                    float y = (float)((Hash(i, 2) * h + _t * (22 + 30 * Hash(i, 3))) % (h + 8)) - 4;
                    float s = 3 + 3 * Hash(i, 4);
                    g.FillRectangle(new Color(colors[i % colors.Length], 0.75f), x, y, s, s);
                }
                g.ResetClip();

                float grow = Math.Min(1f, (float)_t * 3) * (1 + 0.06f * (float)Math.Sin(_t * 5));
                var star = new PointF[10];
                for (int i = 0; i < 10; i++)
                {
                    double a = -Math.PI / 2 + i * Math.PI / 5 + Math.Sin(_t * 1.6) * 0.18;
                    float r = (i % 2 == 0 ? 19 : 8.5f) * grow;
                    star[i] = new PointF(cx + (float)Math.Cos(a) * r, cy + (float)Math.Sin(a) * r);
                }
                g.FillPolygon(p.GoldShadow, Array.ConvertAll(star, s => new PointF(s.X, s.Y + 2)));
                g.FillPolygon(p.Gold, star);
            }
            else
            {
                float shake = (float)(Math.Sin(_t * 42) * 5 * Math.Max(0, 1 - _t * 1.6));
                g.FillEllipse(p.RedShadow, cx - 18 + shake, cy - 16, 36, 36);
                g.FillEllipse(p.Red, cx - 18 + shake, cy - 18, 36, 36);
                using (var pen = new Pen(Colors.White, 4.5f) { LineCap = PenLineCap.Round })
                {
                    g.DrawLine(pen, cx - 7 + shake, cy - 7, cx + 7 + shake, cy + 7);
                    g.DrawLine(pen, cx + 7 + shake, cy - 7, cx - 7 + shake, cy + 7);
                }
            }

            var bold = Ui.Bold(13);
            var small = Ui.Regular(9.5f);
            float textX = 68;
            float block = bold.LineHeight + (_line.Length > 0 ? small.LineHeight : 0);
            float y0 = (h - block) / 2;
            g.DrawText(bold, _won ? p.GreenText : p.RedText, textX, y0, _title);
            if (_line.Length > 0)
                g.DrawText(small, p.SubtleText, textX, y0 + bold.LineHeight, Ui.Ellipsize(small, _line, w - textX - 10));
        }
    }

    /// <summary>The ten levels as a row of dots: green when done, ringed for the next one to play.</summary>
    internal sealed class LevelDots : Drawable
    {
        private readonly bool[] _done;

        public LevelDots(bool[] done)
        {
            _done = done;
            Height = 30;
            SizeChanged += (s, e) => Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var p = Palette.Current;
            g.AntiAlias = true;
            int n = _done.Length;
            if (n == 0 || Width < 40)
                return;
            float gap = 5;
            float d = Math.Min(26, (Width - gap * (n - 1)) / n);
            int next = Array.IndexOf(_done, false);
            var font = Ui.Bold(Math.Max(7, d * 0.36f));
            for (int i = 0; i < n; i++)
            {
                float x = i * (d + gap), y = (Height - d) / 2;
                if (_done[i])
                    g.FillEllipse(p.Green, x, y, d, d);
                else
                {
                    g.FillEllipse(i == next ? p.Accent : p.Border, x, y, d, d);
                    g.FillEllipse(p.TileFace, x + 2, y + 2, d - 4, d - 4);
                }
                string text = (i + 1).ToString();
                var size = font.MeasureString(text);
                g.DrawText(font, _done[i] ? Colors.White : i == next ? p.AccentText : p.DisabledText,
                    x + (d - size.Width) / 2, y + (d - size.Height) / 2, text);
            }
        }
    }
}
