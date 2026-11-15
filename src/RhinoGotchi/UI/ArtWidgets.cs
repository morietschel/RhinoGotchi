using System;
using System.Collections.Generic;
using System.IO;
using Eto.Drawing;
using Eto.Forms;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// The panel's painted art (Resources/Art): game cards, the Learning/Games switch and the bottom-bar
    /// icons. Each picture is loaded once and kept.
    /// </summary>
    internal static class Art
    {
        private static readonly Dictionary<string, Bitmap?> Images = new Dictionary<string, Bitmap?>();

        public static Bitmap? Get(string name)
        {
            if (Images.TryGetValue(name, out var cached))
                return cached;
            Bitmap? bmp = null;
            try
            {
                using Stream? s = typeof(Art).Assembly.GetManifestResourceStream("RhinoGotchi.Art." + name + ".png");
                if (s != null)
                    bmp = new Bitmap(s);
            }
            catch (Exception)
            {
                bmp = null;
            }
            Images[name] = bmp;
            return bmp;
        }
    }

    /// <summary>One game in the <see cref="GameGrid"/>: its card picture, a short status line and what a click opens.</summary>
    internal sealed class GameTile
    {
        public GameTile(string card, string title, string status, Action open)
        {
            Card = card;
            Title = title;
            Status = status;
            Open = open;
        }

        /// <summary>The card picture's name in Resources/Art, e.g. "card-deep-cut".</summary>
        public string Card { get; }

        /// <summary>Drawn only if the picture is missing (the card art already carries the name).</summary>
        public string Title { get; }

        public string Status { get; }
        public bool StatusDone { get; set; }
        public bool StatusTimed { get; set; }
        public Action Open { get; }
    }

    /// <summary>
    /// The Games tab: a two-column grid of game cards. The whole card is the button (no Play button);
    /// a status line sits under each card. Its height follows its width, so the cards keep their shape.
    /// </summary>
    internal sealed class GameGrid : Drawable
    {
        private const float Gap = 10;
        private const float LabelH = 22;
        private const float CardRatio = 500f / 700f;

        private readonly IReadOnlyList<GameTile> _tiles;
        private int _hover = -1, _pressed = -1;

        public GameGrid(IReadOnlyList<GameTile> tiles)
        {
            _tiles = tiles;
            Height = 300;
            SizeChanged += (s, e) => { FitHeight(); Invalidate(); };
            MouseMove += (s, e) =>
            {
                int i = IndexAt(e.Location);
                if (i != _hover)
                {
                    _hover = i;
                    Cursor = i >= 0 ? Cursors.Pointer : Cursors.Default;
                    Invalidate();
                }
            };
            MouseLeave += (s, e) => { _hover = _pressed = -1; Invalidate(); };
            MouseDown += (s, e) => { _pressed = IndexAt(e.Location); Invalidate(); e.Handled = true; };
            MouseUp += (s, e) =>
            {
                int i = IndexAt(e.Location), pressed = _pressed;
                _pressed = -1;
                Invalidate();
                if (i >= 0 && i == pressed)
                    _tiles[i].Open();
            };
        }

        private float CardW => Math.Max(1, (Width - Gap) / 2f);
        private float CardH => CardW * CardRatio;

        private void FitHeight()
        {
            if (Width <= 0)
                return;
            int rows = (_tiles.Count + 1) / 2;
            int h = (int)Math.Ceiling(rows * (CardH + LabelH) + Math.Max(0, rows - 1) * Gap);
            if (Math.Abs(Height - h) > 1)
                Application.Instance.AsyncInvoke(() => Height = h);
        }

        private RectangleF CardRect(int i)
        {
            int col = i % 2, row = i / 2;
            return new RectangleF(col * (CardW + Gap), row * (CardH + LabelH + Gap), CardW, CardH);
        }

        private int IndexAt(PointF p)
        {
            for (int i = 0; i < _tiles.Count; i++)
            {
                var r = CardRect(i);
                if (new RectangleF(r.X, r.Y, r.Width, r.Height + LabelH).Contains(p))
                    return i;
            }
            return -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var p = Palette.Current;
            g.AntiAlias = true;
            g.FillRectangle(p.Background, 0, 0, Width, Height);
            if (Width < 60)
                return;

            for (int i = 0; i < _tiles.Count; i++)
            {
                var tile = _tiles[i];
                var r = CardRect(i);
                float lift = i == _pressed ? 2 : i == _hover ? -2 : 0;
                var card = new RectangleF(r.X, r.Y + lift, r.Width, r.Height);

                Bitmap? art = Art.Get(tile.Card);
                if (art != null)
                {
                    g.ImageInterpolation = ImageInterpolation.High;
                    g.DrawImage(art, card);
                    g.ImageInterpolation = ImageInterpolation.Default;
                }
                else
                {
                    // No picture: a plain card with the game's name, so the grid still works.
                    g.FillPath(p.TileFace, GraphicsPath.GetRoundRect(card, 14));
                    g.DrawPath(new Pen(p.Border, 2), GraphicsPath.GetRoundRect(card, 14));
                    Font tf = Ui.Bold(11);
                    SizeF ts = tf.MeasureString(tile.Title);
                    g.DrawText(tf, p.Text, card.X + (card.Width - ts.Width) / 2, card.Y + (card.Height - ts.Height) / 2, tile.Title);
                }
                if (i == _hover)
                    g.DrawPath(new Pen(p.Ink, 2.5f), GraphicsPath.GetRoundRect(new RectangleF(card.X - 1, card.Y - 1, card.Width + 2, card.Height + 2), 13));

                if (tile.Status.Length > 0)
                {
                    Font f = Ui.Bold(9);
                    Color ink = tile.StatusDone ? p.GreenText : tile.StatusTimed ? p.AccentText : p.SubtleText;
                    string text = Ui.Ellipsize(f, tile.Status, r.Width - 4);
                    g.DrawText(f, ink, r.X + 3, r.Bottom + 4, text);
                }
            }
        }
    }

    /// <summary>
    /// The Learning / Games switch drawn with its painted art (one picture per selected side). A click on
    /// either half selects it. Falls back to plain text if the pictures are missing.
    /// </summary>
    internal sealed class ArtTabSwitch : Drawable
    {
        private const float Ratio = 120f / 900f;
        private static readonly string[] Pictures = { "switch-learning", "switch-games" };
        private static readonly string[] Labels = { "Learning", "Games" };
        private int _selected;

        public ArtTabSwitch(int selected)
        {
            _selected = Math.Max(0, Math.Min(1, selected));
            Height = 48;
            Cursor = Cursors.Pointer;
            SizeChanged += (s, e) =>
            {
                if (Width > 0)
                {
                    int h = (int)Math.Round(Width * Ratio);
                    if (Math.Abs(Height - h) > 1)
                        Application.Instance.AsyncInvoke(() => Height = h);
                }
                Invalidate();
            };
            MouseDown += (s, e) =>
            {
                int i = Width <= 0 ? 0 : e.Location.X < Width / 2f ? 0 : 1;
                if (i != _selected)
                {
                    _selected = i;
                    Invalidate();
                    SelectedChanged?.Invoke(this, EventArgs.Empty);
                }
            };
        }

        public event EventHandler<EventArgs>? SelectedChanged;

        public int Selected => _selected;

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var p = Palette.Current;
            g.AntiAlias = true;
            g.FillRectangle(p.Background, 0, 0, Width, Height);
            Bitmap? art = Art.Get(Pictures[_selected]);
            if (art != null)
            {
                g.ImageInterpolation = ImageInterpolation.High;
                g.DrawImage(art, new RectangleF(0, 0, Width, Height));
                g.ImageInterpolation = ImageInterpolation.Default;
                return;
            }
            float half = Width / 2f;
            for (int i = 0; i < 2; i++)
            {
                Font f = i == _selected ? Ui.Bold(11) : Ui.Regular(11);
                SizeF s = f.MeasureString(Labels[i]);
                g.DrawText(f, i == _selected ? p.Text : p.SubtleText, i * half + (half - s.Width) / 2, (Height - s.Height) / 2, Labels[i]);
            }
        }
    }
}
