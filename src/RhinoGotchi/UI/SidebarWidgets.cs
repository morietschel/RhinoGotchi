using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;

namespace RhinoGotchi.UI
{
    internal enum NavTab
    {
        Home,
        Trophies,
        Stats,
    }

    /// <summary>Small drawing helpers shared by the sidebar widgets.</summary>
    internal static class Ui
    {
        private static readonly Dictionary<(bool, float), Font> Fonts = new Dictionary<(bool, float), Font>();

        public static Font Bold(float size) => Get(true, size);

        public static Font Regular(float size) => Get(false, size);

        private static Font Get(bool bold, float size)
        {
            if (!Fonts.TryGetValue((bold, size), out var f))
                Fonts[(bold, size)] = f = new Font(bold ? SystemFont.Bold : SystemFont.Default, size);
            return f;
        }

        /// <summary>A rounded face sitting on a darker lip: the Duolingo-style chunky look.</summary>
        public static void Chunky(Graphics g, RectangleF rect, float radius, Color face, Color border, float lip = 4, float lift = 0, float stroke = 2)
        {
            g.FillPath(border, GraphicsPath.GetRoundRect(new RectangleF(rect.X, rect.Y + lip, rect.Width, rect.Height - lip), radius));
            var top = new RectangleF(rect.X, rect.Y + lift, rect.Width, rect.Height - lip);
            g.FillPath(border, GraphicsPath.GetRoundRect(top, radius));
            g.FillPath(face, GraphicsPath.GetRoundRect(new RectangleF(top.X + stroke, top.Y + stroke, top.Width - 2 * stroke, top.Height - 2 * stroke), Math.Max(1, radius - stroke)));
        }

        /// <summary>Cuts <paramref name="text"/> with "…" so it fits in <paramref name="width"/>.</summary>
        public static string Ellipsize(Font font, string text, float width)
        {
            if (width <= 0)
                return "";
            if (font.MeasureString(text).Width <= width)
                return text;
            for (int n = text.Length - 1; n > 0; n--)
            {
                string t = text.Substring(0, n).TrimEnd() + "…";
                if (font.MeasureString(t).Width <= width)
                    return t;
            }
            return "…";
        }

        public static List<string> Wrap(Font font, string text, float maxWidth)
        {
            var lines = new List<string>();
            string line = "";
            foreach (string word in text.Split(' '))
            {
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && font.MeasureString(candidate).Width > maxWidth)
                {
                    lines.Add(line);
                    line = word;
                }
                else
                    line = candidate;
            }
            if (line.Length > 0)
                lines.Add(line);
            return lines;
        }

        /// <summary>Small capitals with extra letter spacing, e.g. "LEARN RHINO". Returns the width.</summary>
        public static float Eyebrow(Graphics g, string text, float x, float y, Color color, float size = 8.5f, float spacing = 1.3f)
        {
            Font f = Bold(size);
            float cx = x;
            foreach (char ch in text)
            {
                string s = ch.ToString();
                g.DrawText(f, color, cx, y, s);
                cx += f.MeasureString(s).Width + spacing;
            }
            return cx - x;
        }

        public static float EyebrowWidth(string text, float size = 8.5f, float spacing = 1.3f) =>
            text.Sum(ch => Bold(size).MeasureString(ch.ToString()).Width + spacing);

        /// <summary>A small rounded chip with bold text. Returns its width.</summary>
        public static float Chip(Graphics g, float x, float y, string text, Color face, Color ink, float height = 22)
        {
            Font f = Bold(8.5f);
            SizeF s = f.MeasureString(text);
            float w = s.Width + 16;
            g.FillPath(face, GraphicsPath.GetRoundRect(new RectangleF(x, y, w, height), height / 2));
            g.DrawText(f, ink, x + 8, y + (height - s.Height) / 2, text);
            return w;
        }

        public static float ChipWidth(string text) => Bold(8.5f).MeasureString(text).Width + 16;

        public static void Check(Graphics g, RectangleF r, Color color, float width = 2.6f)
        {
            var pen = new Pen(color, width) { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            g.DrawLines(pen,
                new PointF(r.X + r.Width * 0.22f, r.Y + r.Height * 0.52f),
                new PointF(r.X + r.Width * 0.43f, r.Y + r.Height * 0.72f),
                new PointF(r.X + r.Width * 0.78f, r.Y + r.Height * 0.3f));
        }

        public static void Lock(Graphics g, RectangleF r, Color color)
        {
            float cx = r.X + r.Width / 2, s = r.Width / 24f;
            var pen = new Pen(color, 2.2f * s);
            g.DrawArc(pen, cx - 4.5f * s, r.Y + 4 * s, 9 * s, 10 * s, 180, 180);
            g.DrawLine(pen, cx - 4.5f * s, r.Y + 9 * s, cx - 4.5f * s, r.Y + 11 * s);
            g.DrawLine(pen, cx + 4.5f * s, r.Y + 9 * s, cx + 4.5f * s, r.Y + 11 * s);
            g.FillPath(color, GraphicsPath.GetRoundRect(new RectangleF(cx - 7 * s, r.Y + 11 * s, 14 * s, 9.5f * s), 2 * s));
        }

        public static void House(Graphics g, PointF c, float s, Color color)
        {
            var pen = new Pen(color, 2.2f) { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            g.DrawLines(pen, new PointF(c.X - 9 * s, c.Y - 1 * s), new PointF(c.X, c.Y - 9 * s), new PointF(c.X + 9 * s, c.Y - 1 * s));
            g.DrawLines(pen, new PointF(c.X - 6 * s, c.Y - 3 * s), new PointF(c.X - 6 * s, c.Y + 8 * s), new PointF(c.X + 6 * s, c.Y + 8 * s), new PointF(c.X + 6 * s, c.Y - 3 * s));
        }

        public static void Trophy(Graphics g, PointF c, float s, Color color)
        {
            var pen = new Pen(color, 2.2f) { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            var cup = new GraphicsPath();
            cup.MoveTo(c.X - 6 * s, c.Y - 8 * s);
            cup.AddLine(new PointF(c.X - 6 * s, c.Y - 8 * s), new PointF(c.X + 6 * s, c.Y - 8 * s));
            cup.AddBezier(new PointF(c.X + 6 * s, c.Y - 8 * s), new PointF(c.X + 6 * s, c.Y + 1 * s), new PointF(c.X + 3 * s, c.Y + 3 * s), new PointF(c.X, c.Y + 3 * s));
            cup.AddBezier(new PointF(c.X, c.Y + 3 * s), new PointF(c.X - 3 * s, c.Y + 3 * s), new PointF(c.X - 6 * s, c.Y + 1 * s), new PointF(c.X - 6 * s, c.Y - 8 * s));
            g.DrawPath(pen, cup);
            g.DrawArc(pen, c.X - 10 * s, c.Y - 7 * s, 5 * s, 6 * s, 90, 180);
            g.DrawArc(pen, c.X + 5 * s, c.Y - 7 * s, 5 * s, 6 * s, 270, 180);
            g.DrawLine(pen, c.X, c.Y + 3 * s, c.X, c.Y + 7 * s);
            g.DrawLine(pen, c.X - 5 * s, c.Y + 8 * s, c.X + 5 * s, c.Y + 8 * s);
        }

        public static void Bars(Graphics g, PointF c, float s, Color color)
        {
            var pen = new Pen(color, 3f) { LineCap = PenLineCap.Round };
            g.DrawLine(pen, c.X - 6 * s, c.Y + 7 * s, c.X - 6 * s, c.Y + 1 * s);
            g.DrawLine(pen, c.X, c.Y + 7 * s, c.X, c.Y - 7 * s);
            g.DrawLine(pen, c.X + 6 * s, c.Y + 7 * s, c.X + 6 * s, c.Y - 2 * s);
        }

        public static Color Darken(Color c, float amount) =>
            new Color(Math.Max(0, c.R - amount), Math.Max(0, c.G - amount), Math.Max(0, c.B - amount), c.A);
    }

    /// <summary>
    /// Gotchi in a warm card: the pet (breathing, wearing its hat), a line from it, and chips for
    /// streak, hats and questions solved. Tap the pet and it hops. Wraps to the panel width.
    /// </summary>
    internal sealed class PetCard : Drawable
    {
        private const float PetSize = 76;
        private readonly UITimer _timer = new UITimer { Interval = 1 / 24.0 };
        private readonly DateTime _born = DateTime.Now;
        private DateTime _hopStart = DateTime.MinValue;
        private string _status = "";
        private string _message = "";
        private Mood _mood = Mood.Happy;
        private string[] _chips = new string[0];
        private int _accentChip = -1;

        public PetCard()
        {
            Height = 100;
            _timer.Elapsed += (s, e) => Invalidate();
            LoadComplete += (s, e) => _timer.Start();
            UnLoad += (s, e) => _timer.Stop();
            SizeChanged += (s, e) => { Relayout(); Invalidate(); };
            MouseDown += (s, e) =>
            {
                if (e.Location.X < PetSize + 12)
                {
                    _hopStart = DateTime.Now;
                    e.Handled = true;
                }
            };
        }

        /// <summary>Sets what Gotchi says and shows. <paramref name="accentChip"/> is drawn in orange (the streak).</summary>
        public void Set(Mood mood, string status, string message, int accentChip, params string[] chips)
        {
            _mood = mood;
            _status = status;
            _message = message;
            _chips = chips;
            _accentChip = accentChip;
            Relayout();
            Invalidate();
        }

        private float TextX => PetSize + 14;

        private float Layout(float width, Graphics? g)
        {
            var p = Palette.Current;
            float x = TextX, right = width - 14, w = right - x;
            float y = 14;
            if (w < 60)
                return 100;

            // Name + status
            if (g != null)
            {
                g.DrawText(Ui.Bold(13), p.Text, x, y, "Gotchi");
                float nameW = Ui.Bold(13).MeasureString("Gotchi").Width;
                string status = Ui.Ellipsize(Ui.Regular(9.5f), _status, w - nameW - 10);
                float sw = Ui.Regular(9.5f).MeasureString(status).Width;
                g.DrawText(Ui.Regular(9.5f), p.SubtleText, right - sw, y + 3, status);
            }
            y += Ui.Bold(13).LineHeight + 2;

            // Message
            var lines = Ui.Wrap(Ui.Regular(10), _message, w);
            foreach (string line in lines)
            {
                g?.DrawText(Ui.Regular(10), p.SubtleText, x, y, line);
                y += Ui.Regular(10).LineHeight;
            }
            y += 6;

            // Chips, wrapping
            float cx = x;
            for (int i = 0; i < _chips.Length; i++)
            {
                float cw = Ui.ChipWidth(_chips[i]);
                if (cx > x && cx + cw > right)
                {
                    cx = x;
                    y += 26;
                }
                if (g != null)
                    Ui.Chip(g, cx, y, _chips[i], p.Background, i == _accentChip ? p.AccentText : p.Text);
                cx += cw + 6;
            }
            y += 22 + 14;
            return Math.Max(PetSize + 22, y) + 4; // + lip
        }

        private void Relayout()
        {
            if (Width <= 0)
                return;
            int h = (int)Math.Ceiling(Layout(Width, null));
            if (h != Height)
                Application.Instance.AsyncInvoke(() => Height = h);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var p = Palette.Current;
            g.AntiAlias = true;
            float w = Width - 1, h = Height;
            if (w < 40)
                return;
            Ui.Chunky(g, new RectangleF(0, 0, w, h), 16, p.Warm, p.WarmBorder, lip: 4, stroke: 2);

            double t = (DateTime.Now - _born).TotalSeconds;
            float breathe = (float)(0.5 + 0.5 * Math.Sin(t * 2 * Math.PI / 2.4));
            double hopT = (DateTime.Now - _hopStart).TotalSeconds;
            float hop = hopT < 0.5 ? (float)Math.Sin(hopT / 0.5 * Math.PI) * -10 : 0;
            var mood = hopT < 0.8 ? Mood.Celebrate : _mood;
            float petY = Math.Max(6, (h - 4 - PetSize) / 2);
            Mascot.Draw(g, new RectangleF(6, petY, PetSize, PetSize), mood, hop, breathe: breathe);

            Layout(w, g);
        }
    }

    /// <summary>
    /// A 60px list row: a pastel icon tile, a bold title and one line of detail, and progress on the
    /// right ("2/10") or a tag ("DONE TODAY"). <see cref="Emphasis"/> gives it the black "start here" border.
    /// </summary>
    internal sealed class ListRow : Drawable
    {
        private bool _hover, _pressed;

        public ListRow(string title, string detail, Glyph glyph, int tint, Action onClick)
        {
            Title = title;
            Detail = detail;
            Glyph = glyph;
            Tint = tint;
            Height = 64;
            Cursor = Cursors.Pointer;
            MouseEnter += (s, e) => { _hover = true; Invalidate(); };
            MouseLeave += (s, e) => { _hover = _pressed = false; Invalidate(); };
            MouseDown += (s, e) => { _pressed = true; Invalidate(); e.Handled = true; };
            MouseUp += (s, e) =>
            {
                bool fire = _pressed;
                _pressed = false;
                Invalidate();
                if (fire)
                    onClick();
            };
            SizeChanged += (s, e) => Invalidate();
        }

        public string Title { get; set; }
        public string Detail { get; set; }
        public Glyph Glyph { get; set; }
        public int Tint { get; set; }
        public bool Emphasis { get; set; }

        /// <summary>Right side: plain progress text ("2/10").</summary>
        public string Right { get; set; } = "";
        public bool RightAccent { get; set; }

        /// <summary>Right side instead of <see cref="Right"/>: a green "DONE TODAY" or ink "PLAY" tag.</summary>
        public string TagText { get; set; } = "";
        public bool TagDone { get; set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var p = Palette.Current;
            g.AntiAlias = true;
            float w = Width - 1, h = Height;
            if (w < 60)
                return;

            float lift = _pressed ? 3 : 0;
            Color border = Emphasis ? p.Ink : p.Border;
            Color lip = Emphasis ? p.InkShadow : Ui.Darken(p.Border, 0.06f);
            g.FillPath(lip, GraphicsPath.GetRoundRect(new RectangleF(0, 4, w, h - 4), 14));
            var top = new RectangleF(0, lift, w, h - 4);
            g.FillPath(border, GraphicsPath.GetRoundRect(top, 14));
            g.FillPath(_hover ? p.TileHover : p.TileFace, GraphicsPath.GetRoundRect(new RectangleF(2, top.Y + 2, w - 4, top.Height - 4), 12));

            // Icon tile
            var tile = new RectangleF(12, top.Y + (top.Height - 40) / 2, 40, 40);
            Color tint = Emphasis ? p.Ink : p.Tints[Math.Abs(Tint) % p.Tints.Length];
            g.FillPath(tint, GraphicsPath.GetRoundRect(tile, 12));
            Doodles.Icon(g, new RectangleF(tile.X + 9, tile.Y + 9, 22, 22), Glyph, Emphasis ? p.OnInk : p.Text);

            // Right side
            float rightW = 0;
            if (TagText.Length > 0)
            {
                Font tf = Ui.Bold(8);
                rightW = tf.MeasureString(TagText).Width + 16;
                var r = new RectangleF(w - 12 - rightW, top.Y + (top.Height - 22) / 2, rightW, 22);
                Color face = TagDone ? p.GreenBanner : p.Ink;
                Color ink = TagDone ? p.GreenText : p.OnInk;
                g.FillPath(face, GraphicsPath.GetRoundRect(r, 11));
                if (TagDone)
                    g.DrawPath(new Pen(p.Green, 1.2f), GraphicsPath.GetRoundRect(r, 11));
                SizeF ts = tf.MeasureString(TagText);
                g.DrawText(tf, ink, r.X + (r.Width - ts.Width) / 2, r.Y + (r.Height - ts.Height) / 2, TagText);
            }
            else if (Right.Length > 0)
            {
                Font rf = Ui.Bold(10);
                rightW = rf.MeasureString(Right).Width;
                g.DrawText(rf, RightAccent ? p.AccentText : p.SubtleText, w - 14 - rightW, top.Y + (top.Height - rf.LineHeight) / 2, Right);
            }

            // Title + detail
            float tx = tile.Right + 12, tw = w - tx - 14 - (rightW > 0 ? rightW + 10 : 0);
            Font title = Ui.Bold(11.5f), detail = Ui.Regular(9.5f);
            float blockH = title.LineHeight + 2 + detail.LineHeight;
            float ty = top.Y + (top.Height - blockH) / 2;
            g.DrawText(title, p.Text, tx, ty, Ui.Ellipsize(title, Title, tw));
            g.DrawText(detail, p.SubtleText, tx, ty + title.LineHeight + 2, Ui.Ellipsize(detail, Detail, tw));
        }
    }

    /// <summary>"LEARN RHINO ········ from your first Move to a final exam".</summary>
    internal sealed class SectionLabel : Drawable
    {
        private readonly string _label;
        private readonly string _note;

        public SectionLabel(string label, string note = "")
        {
            _label = label;
            _note = note;
            Height = 20;
            SizeChanged += (s, e) => Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var p = Palette.Current;
            g.AntiAlias = true;
            float lw = Ui.Eyebrow(g, _label, 2, 3, p.SubtleText);
            if (_note.Length == 0)
                return;
            string note = Ui.Ellipsize(Ui.Regular(9.5f), _note, Width - lw - 16);
            float nw = Ui.Regular(9.5f).MeasureString(note).Width;
            g.DrawText(Ui.Regular(9.5f), p.SubtleText, Width - 2 - nw, 2, note);
        }
    }

    /// <summary>A quiet gray card with an ink "?" or bulb badge and wrapped text. Click to cycle.</summary>
    internal sealed class TipCard : Drawable
    {
        private string _text;
        private readonly string _title;

        public TipCard(string title, string text, Action? onClick = null)
        {
            _title = title;
            _text = text;
            Height = 80;
            SizeChanged += (s, e) => { Relayout(); Invalidate(); };
            if (onClick != null)
            {
                Cursor = Cursors.Pointer;
                MouseUp += (s, e) => onClick();
            }
        }

        public string Text
        {
            get => _text;
            set { _text = value; Relayout(); Invalidate(); }
        }

        private float Layout(float width, Graphics? g)
        {
            var p = Palette.Current;
            float x = 44, w = width - x - 14, y = 12;
            if (w < 40)
                return 80;
            if (g != null)
            {
                g.FillPath(p.Quiet, GraphicsPath.GetRoundRect(new RectangleF(0, 0, width - 1, Height - 1), 14));
                g.FillEllipse(p.Ink, 12, 12, 22, 22);
                SizeF qs = Ui.Bold(11).MeasureString("?");
                g.DrawText(Ui.Bold(11), p.OnInk, 23 - qs.Width / 2, 23 - qs.Height / 2, "?");
                Ui.Eyebrow(g, _title, x, y + 1, p.SubtleText, 8);
            }
            y += 18;
            foreach (string line in Ui.Wrap(Ui.Regular(10.5f), _text, w))
            {
                g?.DrawText(Ui.Regular(10.5f), p.Text, x, y, line);
                y += Ui.Regular(10.5f).LineHeight;
            }
            return y + 12;
        }

        private void Relayout()
        {
            if (Width <= 0)
                return;
            int h = (int)Math.Ceiling(Layout(Width, null));
            if (h != Height)
                Application.Instance.AsyncInvoke(() => Height = h);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.AntiAlias = true;
            Layout(Width, e.Graphics);
        }
    }

    /// <summary>Bottom navigation: Home · Trophies · Stats.</summary>
    internal sealed class NavBar : Drawable
    {
        private static readonly (NavTab Tab, string Label)[] Items =
        {
            (NavTab.Home, "Home"),
            (NavTab.Trophies, "Trophies"),
            (NavTab.Stats, "Stats"),
        };

        private int _hover = -1;
        private NavTab _current;

        public NavBar()
        {
            Height = 56;
            MouseMove += (s, e) => { int i = IndexAt(e.Location.X); if (i != _hover) { _hover = i; Invalidate(); } };
            MouseLeave += (s, e) => { _hover = -1; Invalidate(); };
            MouseUp += (s, e) =>
            {
                int i = IndexAt(e.Location.X);
                if (i >= 0)
                    Selected?.Invoke(Items[i].Tab);
            };
            SizeChanged += (s, e) => Invalidate();
        }

        public event Action<NavTab>? Selected;

        public NavTab Current
        {
            get => _current;
            set { _current = value; Invalidate(); }
        }

        private int IndexAt(float x) => Width <= 0 ? -1 : Math.Max(0, Math.Min(Items.Length - 1, (int)(x / (Width / (float)Items.Length))));

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var p = Palette.Current;
            g.AntiAlias = true;
            g.FillRectangle(p.Background, 0, 0, Width, Height);
            g.FillRectangle(p.Border, 0, 0, Width, 1.5f);

            float seg = Width / (float)Items.Length;
            for (int i = 0; i < Items.Length; i++)
            {
                var (tab, label) = Items[i];
                bool on = tab == _current;
                if (i == _hover && !on)
                    g.FillPath(p.TileHover, GraphicsPath.GetRoundRect(new RectangleF(i * seg + 6, 5, seg - 12, Height - 10), 10));
                Color ink = on ? p.Text : p.SubtleText;
                var c = new PointF(i * seg + seg / 2, 21);
                switch (tab)
                {
                    case NavTab.Home: Ui.House(g, c, 0.95f, ink); break;
                    case NavTab.Trophies: Ui.Trophy(g, c, 0.95f, ink); break;
                    default: Ui.Bars(g, c, 0.95f, ink); break;
                }
                Font f = on ? Ui.Bold(9) : Ui.Regular(9);
                SizeF s = f.MeasureString(label);
                g.DrawText(f, ink, c.X - s.Width / 2, 34, label);
                if (on)
                    g.FillPath(p.Accent, GraphicsPath.GetRoundRect(new RectangleF(c.X - 10, Height - 5, 20, 3), 1.5f));
            }
        }
    }
}
