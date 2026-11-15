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

    /// <summary>One card in a <see cref="GameGrid"/>: its picture and what a click opens.</summary>
    internal sealed class GameTile
    {
        public GameTile(string card, string title, Action open)
        {
            Card = card;
            Title = title;
            Open = open;
        }

        /// <summary>The card picture's name in Resources/Art, e.g. "card-deep-cut".</summary>
        public string Card { get; }

        /// <summary>Drawn only if the picture is missing (the card art already carries the name).</summary>
        public string Title { get; }

        /// <summary>Today's round is played (or every level is done): a small green check on the card.</summary>
        public bool Done { get; set; }

        public Action Open { get; }
    }

    /// <summary>
    /// A two-column grid of painted cards (the Games and Learning tabs). The whole card is the button;
    /// there are no Play buttons or captions. Normally its height follows its width. With
    /// <see cref="FillHeight"/> it takes the height it is given and shrinks the cards to fit inside it, so
    /// the page never has to scroll.
    /// </summary>
    internal sealed class GameGrid : Drawable
    {
        private const float Gap = 10;
        private const float CardRatio = 500f / 700f;

        private readonly IReadOnlyList<GameTile> _tiles;
        private int _hover = -1, _pressed = -1;

        public GameGrid(IReadOnlyList<GameTile> tiles, bool fillHeight = false)
        {
            _tiles = tiles;
            FillHeight = fillHeight;
            // Filling: start small so the page fits the panel; the layout then hands over the free height.
            Height = fillHeight ? 120 : 140;
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

        public bool FillHeight { get; }

        private int Rows => (_tiles.Count + 1) / 2;

        /// <summary>Card width: half the width, or less when filling a height that can't hold that.</summary>
        private float CardW
        {
            get
            {
                float byWidth = Math.Max(1, (Width - Gap) / 2f);
                if (!FillHeight || Height <= 0)
                    return byWidth;
                float byHeight = (Height - (Rows - 1) * Gap) / Rows / CardRatio;
                return Math.Max(1, Math.Min(byWidth, byHeight));
            }
        }

        private float CardH => CardW * CardRatio;

        private void FitHeight()
        {
            if (FillHeight || Width <= 0)
                return;
            int h = (int)Math.Ceiling(Rows * CardH + (Rows - 1) * Gap);
            if (Math.Abs(Height - h) > 1)
                Application.Instance.AsyncInvoke(() => Height = h);
        }

        private RectangleF CardRect(int i)
        {
            float w = CardW, h = CardH;
            float left = (Width - (2 * w + Gap)) / 2; // centred when the cards are height-limited
            int col = i % 2, row = i / 2;
            return new RectangleF(left + col * (w + Gap), row * (h + Gap), w, h);
        }

        private int IndexAt(PointF p)
        {
            for (int i = 0; i < _tiles.Count; i++)
                if (CardRect(i).Contains(p))
                    return i;
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

                if (tile.Done)
                {
                    // A small green check in the top-left corner (the sky there is empty on every card).
                    float d = Math.Max(16, Math.Min(22, card.Width * 0.11f));
                    var dot = new RectangleF(card.X + 6, card.Y + 6, d, d);
                    g.FillEllipse(p.Green, dot);
                    g.DrawEllipse(new Pen(Colors.White, 1.5f), dot);
                    var pen = new Pen(Colors.White, Math.Max(2, d * 0.13f)) { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
                    g.DrawLines(pen, new PointF(dot.X + d * 0.27f, dot.Y + d * 0.52f), new PointF(dot.X + d * 0.44f, dot.Y + d * 0.68f), new PointF(dot.X + d * 0.74f, dot.Y + d * 0.34f));
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
