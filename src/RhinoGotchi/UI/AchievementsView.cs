using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using RhinoGotchi.Lessons;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// Trophies: what Gotchi is wearing, the wardrobe (tap an unlocked hat to wear it), and every
    /// achievement with its progress. Drawn by hand; the wardrobe grid reflows with the panel width.
    /// </summary>
    internal sealed class AchievementsView : Drawable, INavPage
    {
        private const float Pad = 16;
        private const float TileH = 98;
        private const float TileMinW = 82;
        private const float Gap = 8;

        private readonly List<(RectangleF Rect, string Id, Action Action)> _hits = new List<(RectangleF, string, Action)>();
        private readonly UITimer _timer = new UITimer { Interval = 1 / 24.0 };
        private readonly DateTime _born = DateTime.Now;
        private string? _hover, _pressed;
        private string _note = "";
        private DateTime _wornAt = DateTime.MinValue;
        private Rectangle _petArea;
        private float _ox;

        public AchievementsView(RhinoGotchiPanel host)
        {
            Height = 900;
            _timer.Elapsed += (s, e) => { if (_petArea.Width > 0) Invalidate(_petArea); else Invalidate(); };
            LoadComplete += (s, e) => _timer.Start();
            UnLoad += (s, e) => _timer.Stop();
            SizeChanged += (s, e) => Invalidate();
            MouseMove += (s, e) =>
            {
                string? id = HitAt(e.Location)?.Id;
                if (id != _hover)
                {
                    _hover = id;
                    Cursor = id != null ? Cursors.Pointer : Cursors.Default;
                    Invalidate();
                }
            };
            MouseLeave += (s, e) => { _hover = _pressed = null; Invalidate(); };
            MouseDown += (s, e) => { _pressed = HitAt(e.Location)?.Id; Invalidate(); };
            MouseUp += (s, e) =>
            {
                var hit = HitAt(e.Location);
                string? pressed = _pressed;
                _pressed = null;
                if (hit != null && hit.Value.Id == pressed)
                    hit.Value.Action();
                Invalidate();
            };
        }

        public NavTab Tab => NavTab.Trophies;

        private (RectangleF Rect, string Id, Action Action)? HitAt(PointF p)
        {
            for (int i = _hits.Count - 1; i >= 0; i--)
                if (_hits[i].Rect.Contains(p))
                    return _hits[i];
            return null;
        }

        private void Wear(Hat hat)
        {
            var game = GotchiState.Game;
            if (game.Wear(hat))
            {
                _wornAt = DateTime.Now;
                _note = hat == Hat.None ? "Gotchi took its hat off." : $"Gotchi is wearing the {Achievements.ForHat(hat)!.HatName}!";
            }
            else
            {
                var a = Achievements.ForHat(hat)!;
                _note = a.IsComingSoon
                    ? $"Locked. The {a.HatName} unlocks in {a.ComingWith}, coming soon."
                    : $"Locked. {a.Condition} to unlock the {a.HatName}.";
            }
        }

        private void ToggleClothes(Clothes piece)
        {
            var game = GotchiState.Game;
            var a = Achievements.ForClothes(piece)!;
            if (game.ToggleClothes(piece))
            {
                _wornAt = DateTime.Now;
                _note = game.Progress.EquippedClothes.HasFlag(piece) ? $"Gotchi put on the {a.RewardName}!" : $"Gotchi took off the {a.RewardName}.";
            }
            else
                _note = $"Locked. {a.Condition} to unlock the {a.RewardName}.";
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var p = Palette.Current;
            g.AntiAlias = true;
            _hits.Clear();

            float w = Width;
            g.FillRectangle(p.Background, 0, 0, w, Height);
            // Keep a comfortable column on wide panels.
            float colW = Math.Min(w, 560);
            float ox = (w - colW) / 2;
            _ox = ox;
            g.TranslateTransform(ox, 0);
            float needed = PaintPage(g, p, colW);
            g.TranslateTransform(-ox, 0);
            for (int i = 0; i < _hits.Count; i++)
            {
                var h = _hits[i];
                _hits[i] = (new RectangleF(h.Rect.X + ox, h.Rect.Y, h.Rect.Width, h.Rect.Height), h.Id, h.Action);
            }

            int min = (int)Math.Ceiling(needed);
            if (Math.Abs(Height - min) > 1)
                Application.Instance.AsyncInvoke(() => Height = min);
        }

        private float PaintPage(Graphics g, Palette p, float w)
        {
            var game = GotchiState.Game;
            var stats = game.Stats;
            Hat worn = game.Progress.EquippedHat;
            int unlocked = Achievements.UnlockedCount(stats);
            float y = 14;

            g.DrawText(Ui.Bold(20), p.Text, Pad, y, "Trophies");
            y += Ui.Bold(20).LineHeight + 2;
            g.DrawText(Ui.Regular(10), p.SubtleText, Pad, y, Ui.Ellipsize(Ui.Regular(10), $"{unlocked} of {Achievements.All.Count} unlocked · hats and clothes", w - 2 * Pad));
            y += Ui.Regular(10).LineHeight + 14;

            // Wearing card
            float cardH = 118;
            var card = new RectangleF(Pad, y, w - 2 * Pad, cardH);
            Ui.Chunky(g, card, 16, p.Warm, p.WarmBorder, lip: 4);
            double t = (DateTime.Now - _born).TotalSeconds;
            float breathe = (float)(0.5 + 0.5 * Math.Sin(t * 2 * Math.PI / 2.4));
            double since = (DateTime.Now - _wornAt).TotalSeconds;
            float hop = since < 0.5 ? (float)Math.Sin(since / 0.5 * Math.PI) * -12 : 0;
            _petArea = new Rectangle((int)(card.X + _ox), (int)card.Y - 10, 120, (int)cardH + 10);
            Mascot.Draw(g, new RectangleF(card.X + 8, card.Y + 8, 100, 100), since < 1.2 ? Mood.Celebrate : Mood.Happy, hop, worn, breathe);
            float tx = card.X + 120, tw = card.Right - tx - 14;
            var from = Achievements.ForHat(worn);
            Ui.Eyebrow(g, "WEARING", tx, card.Y + 26, p.SubtleText, 8);
            g.DrawText(Ui.Bold(15), p.Text, tx, card.Y + 42, Ui.Ellipsize(Ui.Bold(15), from?.HatName ?? "No hat", tw));
            g.DrawText(Ui.Regular(9.5f), p.SubtleText, tx, card.Y + 64,
                Ui.Ellipsize(Ui.Regular(9.5f), from != null ? $"from “{from.Name}”" : "Unlock hats with achievements", tw));
            y += cardH + 18;

            // Wardrobe
            Ui.Eyebrow(g, "WARDROBE · TAP TO WEAR", Pad, y, p.SubtleText);
            y += 20;
            var hats = new List<Hat> { Hat.None };
            hats.AddRange(Achievements.Hats.Select(a => a.Hat));
            float inner = w - 2 * Pad;
            int cols = Math.Max(3, Math.Min(6, (int)((inner + Gap) / (TileMinW + Gap))));
            float tileW = (inner - (cols - 1) * Gap) / cols;
            for (int i = 0; i < hats.Count; i++)
            {
                var rect = new RectangleF(Pad + (i % cols) * (tileW + Gap), y + (i / cols) * (TileH + Gap), tileW, TileH);
                WardrobeTile(g, p, rect, hats[i], worn, stats);
            }
            int rows = (hats.Count + cols - 1) / cols;
            y += rows * (TileH + Gap) + 10;

            // Clothes: tap to put on or take off (tracksuit and bomber swap)
            Ui.Eyebrow(g, "CLOTHES · TAP TO PUT ON OR TAKE OFF", Pad, y, p.SubtleText);
            y += 20;
            var pieces = new[] { Clothes.Chain, Clothes.Tracksuit, Clothes.Bomber, Clothes.Jeans };
            int ccols = Math.Max(2, Math.Min(4, (int)((inner + Gap) / (TileMinW + Gap))));
            float cW = (inner - (ccols - 1) * Gap) / ccols;
            for (int i = 0; i < pieces.Length; i++)
            {
                var rect = new RectangleF(Pad + (i % ccols) * (cW + Gap), y + (i / ccols) * (TileH + Gap), cW, TileH);
                ClothesTile(g, p, rect, pieces[i], stats);
            }
            y += ((pieces.Length + ccols - 1) / ccols) * (TileH + Gap) + 2;

            if (_note.Length > 0)
            {
                foreach (string line in Ui.Wrap(Ui.Bold(10), _note, inner))
                {
                    g.DrawText(Ui.Bold(10), p.AccentText, Pad, y, line);
                    y += Ui.Bold(10).LineHeight;
                }
                y += 6;
            }
            y += 12;

            // All achievements
            Ui.Eyebrow(g, "ALL ACHIEVEMENTS", Pad, y, p.SubtleText);
            y += 20;
            foreach (var a in Achievements.All)
                y = AchievementRow(g, p, w, y, a, stats) + 8;
            return y + Pad;
        }

        private void WardrobeTile(Graphics g, Palette p, RectangleF rect, Hat hat, Hat worn, AchievementStats stats)
        {
            string id = "hat:" + hat;
            bool on = hat == worn;
            bool locked = !Achievements.IsHatUnlocked(hat, stats);
            bool hover = _hover == id, pressed = _pressed == id && !locked;
            string name = hat == Hat.None ? "No hat" : Achievements.ForHat(hat)!.HatName;

            if (locked)
            {
                var path = GraphicsPath.GetRoundRect(new RectangleF(rect.X, rect.Y, rect.Width, rect.Height - 4), 14);
                g.FillPath(p.Quiet, path);
                g.DrawPath(new Pen(p.Border, 1.6f) { DashStyle = DashStyles.Dash }, path);
            }
            else
            {
                Color face = on ? p.AccentSoft : hover ? p.TileHover : p.TileFace;
                Color border = on ? p.Accent : p.Border;
                Ui.Chunky(g, rect, 14, face, border, lip: 4, lift: pressed ? 3 : 0, stroke: on ? 2.5f : 2);
            }

            float lift = pressed ? 3 : 0;
            float pet = Math.Min(rect.Width - 16, 58);
            var petRect = new RectangleF(rect.X + (rect.Width - pet) / 2, rect.Y + 8 + lift, pet, pet);
            Mascot.Draw(g, petRect, on ? Mood.Happy : Mood.Neutral, hat: hat);
            if (locked)
            {
                g.FillPath(Color.FromArgb(p.Quiet.Rb, p.Quiet.Gb, p.Quiet.Bb, 175), GraphicsPath.GetRoundRect(new RectangleF(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 8), 12));
                var badge = new RectangleF(rect.Right - 26, rect.Y + 6, 20, 20);
                g.FillEllipse(p.TileFace, badge);
                Ui.Lock(g, new RectangleF(badge.X + 2, badge.Y + 1, 16, 16), p.SubtleText);
            }
            Font f = Ui.Bold(9);
            string label = Ui.Ellipsize(f, name, rect.Width - 8);
            SizeF s = f.MeasureString(label);
            g.DrawText(f, locked ? p.DisabledText : on ? p.AccentText : p.Text, rect.X + (rect.Width - s.Width) / 2, rect.Bottom - 4 - 8 - s.Height + lift, label);
            _hits.Add((rect, id, () => Wear(hat)));
        }

        private void ClothesTile(Graphics g, Palette p, RectangleF rect, Clothes piece, AchievementStats stats)
        {
            string id = "clothes:" + piece;
            var a = Achievements.ForClothes(piece)!;
            bool on = GotchiState.Game.Progress.EquippedClothes.HasFlag(piece);
            bool locked = !a.IsUnlocked(stats);
            bool hover = _hover == id, pressed = _pressed == id && !locked;
            if (locked)
            {
                var path = GraphicsPath.GetRoundRect(new RectangleF(rect.X, rect.Y, rect.Width, rect.Height - 4), 14);
                g.FillPath(p.Quiet, path);
                g.DrawPath(new Pen(p.Border, 1.6f) { DashStyle = DashStyles.Dash }, path);
            }
            else
                Ui.Chunky(g, rect, 14, on ? p.AccentSoft : hover ? p.TileHover : p.TileFace, on ? p.Accent : p.Border, lip: 4, lift: pressed ? 3 : 0, stroke: on ? 2.5f : 2);
            float lift = pressed ? 3 : 0;
            float pet = Math.Min(rect.Width - 16, 58);
            Mascot.Draw(g, new RectangleF(rect.X + (rect.Width - pet) / 2, rect.Y + 8 + lift, pet, pet), Mood.Neutral, hat: Hat.None, clothes: piece);
            if (locked)
            {
                g.FillPath(Color.FromArgb(p.Quiet.Rb, p.Quiet.Gb, p.Quiet.Bb, 175), GraphicsPath.GetRoundRect(new RectangleF(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 8), 12));
                var badge = new RectangleF(rect.Right - 26, rect.Y + 6, 20, 20);
                g.FillEllipse(p.TileFace, badge);
                Ui.Lock(g, new RectangleF(badge.X + 2, badge.Y + 1, 16, 16), p.SubtleText);
            }
            Font f = Ui.Bold(9);
            string label = Ui.Ellipsize(f, a.RewardName, rect.Width - 8);
            SizeF s = f.MeasureString(label);
            g.DrawText(f, locked ? p.DisabledText : on ? p.AccentText : p.Text, rect.X + (rect.Width - s.Width) / 2, rect.Bottom - 4 - 8 - s.Height + lift, label);
            _hits.Add((rect, id, () => ToggleClothes(piece)));
        }

        private static float AchievementRow(Graphics g, Palette p, float w, float y, Achievement a, AchievementStats stats)
        {
            bool done = a.IsUnlocked(stats);
            int value = a.ProgressOf(stats);
            float rowH = 62;
            var rect = new RectangleF(Pad, y, w - 2 * Pad, rowH);
            Ui.Chunky(g, rect, 14, p.TileFace, p.Border, lip: 3);

            var badge = new RectangleF(rect.X + 12, rect.Y + (rowH - 3 - 32) / 2, 32, 32);
            g.FillEllipse(done ? p.Green : p.Track, badge);
            if (done)
                Ui.Check(g, new RectangleF(badge.X + 6, badge.Y + 6, 20, 20), Colors.White, 3f);
            else
                Ui.Lock(g, new RectangleF(badge.X + 7, badge.Y + 5, 18, 18), p.SubtleText);

            string right = done ? "Unlocked" : a.IsComingSoon ? "Soon" : $"{value} / {a.Goal}";
            Font rf = Ui.Bold(9.5f);
            float rw = rf.MeasureString(right).Width;
            g.DrawText(rf, done ? p.GreenText : p.SubtleText, rect.Right - 14 - rw, rect.Y + 12, right);

            float tx = badge.Right + 12, tw = rect.Right - 14 - rw - 10 - tx;
            g.DrawText(Ui.Bold(11), p.Text, tx, rect.Y + 10, Ui.Ellipsize(Ui.Bold(11), $"{a.Name} · {a.HatName}", tw));
            string cond = a.IsComingSoon ? $"{a.Condition} · comes with {a.ComingWith}" : a.Condition;
            g.DrawText(Ui.Regular(9.5f), p.SubtleText, tx, rect.Y + 28, Ui.Ellipsize(Ui.Regular(9.5f), cond, rect.Right - 14 - tx));

            if (!done && !a.IsComingSoon)
            {
                var bar = new RectangleF(tx, rect.Y + 46, rect.Right - 14 - tx, 5);
                g.FillPath(p.Track, GraphicsPath.GetRoundRect(bar, 2.5f));
                float fw = bar.Width * value / Math.Max(1, a.Goal);
                if (fw >= 5)
                    g.FillPath(p.Accent, GraphicsPath.GetRoundRect(new RectangleF(bar.X, bar.Y, fw, bar.Height), 2.5f));
            }
            return y + rowH;
        }
    }
}
