using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using RhinoGotchi.Core;
using RhinoGotchi.Lessons;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// Trophies: the leaderboard, the wardrobe room (Gotchi eating on a loop in its outfit, then
    /// Face · Hat · Clothes with one item each), every achievement, and the daily-game stats.
    /// Drawn by hand; grids reflow with the panel width.
    /// </summary>
    internal sealed class AchievementsView : Drawable, INavPage
    {
        private const float Pad = 16;
        private const float TileH = 98;
        private const float TileMinW = 82;
        private const float Gap = 8;
        private const double FrameSeconds = 0.12;

        private enum Slot { Face, Hat, Clothes }

        /// <summary>Remembered so coming back to Trophies opens the same wardrobe tab.</summary>
        private static Slot _slot = Slot.Hat;

        private static readonly Random Rng = new Random();

        private readonly List<(RectangleF Rect, string Id, Action Action)> _hits = new List<(RectangleF, string, Action)>();
        private readonly UITimer _timer = new UITimer { Interval = 1 / 24.0 };
        private readonly DateTime _born = DateTime.Now;
        private readonly RhinoGotchiPanel _host;
        private string? _hover, _pressed;
        private string _note = "";
        private Rectangle _petArea;
        private float _ox;

        public AchievementsView(RhinoGotchiPanel host)
        {
            _host = host;
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

        // ------------------------------------------------------------------ outfit actions

        private void WearHat(Hat hat)
        {
            var game = GotchiState.Game;
            if (game.Wear(hat))
                _note = hat == Hat.None ? "Gotchi took its hat off." : $"Gotchi is wearing the {Outfit.Name(hat)}!";
            else
            {
                var a = Achievements.ForHat(hat)!;
                _note = $"Locked. {a.Condition} to unlock the {Outfit.Name(hat)}.";
            }
        }

        private void WearClothes(Clothes piece)
        {
            var game = GotchiState.Game;
            if (game.WearClothes(piece))
                _note = piece == Clothes.None ? "Gotchi took its clothes off." : $"Gotchi put on the {Outfit.Name(piece)}!";
            else
            {
                var a = Achievements.ForClothes(piece)!;
                _note = $"Locked. {a.Condition} to unlock the {Outfit.Name(piece)}.";
            }
        }

        private void WearFace(Face face)
        {
            GotchiState.Game.WearFace(face);
            _note = face == Face.None ? "Gotchi's face is bare." : $"Gotchi put on the {Outfit.Name(face)}!";
        }

        /// <summary>Surprise me: a random unlocked item in each slot.</summary>
        private void Surprise()
        {
            var game = GotchiState.Game;
            var stats = game.Stats;
            T Pick<T>(IEnumerable<T> items) { var list = items.ToList(); return list[Rng.Next(list.Count)]; }
            game.WearFace(Pick(Enum.GetValues(typeof(Face)).Cast<Face>()));
            game.Wear(Pick(Enum.GetValues(typeof(Hat)).Cast<Hat>().Where(h => Achievements.IsHatUnlocked(h, stats))));
            game.WearClothes(Pick(Enum.GetValues(typeof(Clothes)).Cast<Clothes>().Where(c => Achievements.AreClothesUnlocked(c, stats))));
            _note = "Surprise! A fresh outfit for Gotchi.";
        }

        private void Undress()
        {
            var game = GotchiState.Game;
            game.WearFace(Face.None);
            game.Wear(Hat.None);
            game.WearClothes(Clothes.None);
            _note = "Back to basics.";
        }

        // ------------------------------------------------------------------ paint

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
            int unlocked = Achievements.UnlockedCount(stats);
            float y = 14;

            g.DrawText(Ui.Bold(20), p.Text, Pad, y, "Trophies");
            y += Ui.Bold(20).LineHeight + 2;
            g.DrawText(Ui.Regular(10), p.SubtleText, Pad, y, Ui.Ellipsize(Ui.Regular(10), $"{unlocked} of {Achievements.All.Count} unlocked · hats and clothes", w - 2 * Pad));
            y += Ui.Regular(10).LineHeight + 14;

            y = LeaderboardRow(g, p, w, y) + 18;
            y = WardrobeRoom(g, p, w, y, stats) + 18;

            // All achievements
            Ui.Eyebrow(g, "ALL ACHIEVEMENTS", Pad, y, p.SubtleText);
            y += 20;
            foreach (var a in Achievements.All)
                y = AchievementRow(g, p, w, y, a, stats) + 8;

            // My stats (daily games only; practice rounds don't count)
            y += 10;
            Ui.Eyebrow(g, "MY STATS · DAILY GAMES", Pad, y, p.SubtleText);
            y += 20;
            foreach (var daily in Games.All)
                y = StatsRow(g, p, w, y, daily) + 8;
            return y + Pad;
        }

        // ------------------------------------------------------------------ wardrobe room

        private float WardrobeRoom(Graphics g, Palette p, float w, float y, AchievementStats stats)
        {
            var progress = GotchiState.Game.Progress;
            Ui.Eyebrow(g, "WARDROBE", Pad, y, p.SubtleText);
            y += 20;

            // Preview: Gotchi eats on a loop, a new food each round, outfit on.
            float inner = w - 2 * Pad;
            float pet = Math.Min(170, inner * 0.5f);
            float cardH = pet + 24;
            var card = new RectangleF(Pad, y, inner, cardH);
            Ui.Chunky(g, card, 16, p.Warm, p.WarmBorder, lip: 4);
            var petRect = new RectangleF(card.X + 10, card.Y + 8, pet, pet);
            _petArea = new Rectangle((int)(petRect.X + _ox) - 2, (int)petRect.Y - 2, (int)pet + 4, (int)pet + 4);
            double t = (DateTime.Now - _born).TotalSeconds;
            double loop = Mascot.EatFrames * FrameSeconds + 1.0; // eat, then rest a second
            int round = (int)(t / loop);
            double inLoop = t - round * loop;
            if (inLoop < Mascot.EatFrames * FrameSeconds)
                Mascot.DrawEating(g, petRect, (Food)(round % 7), (int)(inLoop / FrameSeconds));
            else
                Mascot.Draw(g, petRect, Mood.Happy, breathe: (float)(0.5 + 0.5 * Math.Sin(t * 2 * Math.PI / 2.4)));

            // What it's wearing, and the two quick buttons.
            float tx = petRect.Right + 12, tw = card.Right - tx - 12;
            float ty = card.Y + 14;
            foreach (var (label, value) in new[]
            {
                ("FACE", Outfit.Name(progress.EquippedFace)),
                ("HAT", Outfit.Name(progress.EquippedHat)),
                ("CLOTHES", Outfit.Name(progress.EquippedClothes)),
            })
            {
                Ui.Eyebrow(g, label, tx, ty, p.SubtleText, 7.5f, 1.1f);
                g.DrawText(Ui.Bold(11), p.Text, tx, ty + 13, Ui.Ellipsize(Ui.Bold(11), value, tw));
                ty += 34;
            }
            float bw = Math.Max(0, (tw - 6) / 2);
            if (bw > 40)
            {
                float by = Math.Max(ty + 2, card.Bottom - 4 - 12 - 32);
                SmallButton(g, p, new RectangleF(tx, by, bw, 32), "Surprise me", "surprise", Surprise, primary: true);
                SmallButton(g, p, new RectangleF(tx + bw + 6, by, bw, 32), "Undress", "undress", Undress, primary: false);
            }
            y = card.Bottom + 12;

            // Face · Hat · Clothes
            var segs = new[] { (Slot.Face, "Face"), (Slot.Hat, "Hat"), (Slot.Clothes, "Clothes") };
            var track = new RectangleF(Pad, y, inner, 40);
            g.FillPath(p.Track, GraphicsPath.GetRoundRect(track, 12));
            float segW = (inner - 8) / segs.Length;
            for (int i = 0; i < segs.Length; i++)
            {
                var (slot, name) = segs[i];
                var r = new RectangleF(track.X + 4 + i * segW, track.Y + 4, segW, 32);
                bool on = slot == _slot;
                if (on)
                    Ui.Chunky(g, new RectangleF(r.X, r.Y, r.Width, r.Height), 10, p.TileFace, p.Border, lip: 3, stroke: 1.5f);
                Font f = Ui.Bold(10);
                SizeF s = f.MeasureString(name);
                g.DrawText(f, on ? p.Text : p.SubtleText, r.X + (r.Width - s.Width) / 2, r.Y + (r.Height - 3 - s.Height) / 2, name);
                var target = slot;
                _hits.Add((r, "slot:" + name, () => { _slot = target; _note = ""; }));
            }
            y = track.Bottom + 10;

            // Items for the chosen slot: each tile shows Gotchi in the current outfit with that item swapped in.
            var items = new List<(string Id, string Name, bool Locked, bool On, Action Wear, Action<RectangleF> Draw)>();
            if (_slot == Slot.Face)
                foreach (Face f in Enum.GetValues(typeof(Face)))
                    items.Add(("face:" + f, Outfit.Name(f), false, progress.EquippedFace == f, () => WearFace(f),
                        r => Mascot.Draw(g, r, Mood.Neutral, face: f)));
            else if (_slot == Slot.Hat)
                foreach (Hat h in Enum.GetValues(typeof(Hat)))
                    items.Add(("hat:" + h, Outfit.Name(h), !Achievements.IsHatUnlocked(h, stats), progress.EquippedHat == h, () => WearHat(h),
                        r => Mascot.Draw(g, r, Mood.Neutral, hat: h)));
            else
                foreach (Clothes c in Enum.GetValues(typeof(Clothes)))
                    items.Add(("clothes:" + c, Outfit.Name(c), !Achievements.AreClothesUnlocked(c, stats), progress.EquippedClothes == c, () => WearClothes(c),
                        r => Mascot.Draw(g, r, Mood.Neutral, clothes: c)));

            int cols = Math.Max(3, Math.Min(6, (int)((inner + Gap) / (TileMinW + Gap))));
            float tileW = (inner - (cols - 1) * Gap) / cols;
            for (int i = 0; i < items.Count; i++)
            {
                var rect = new RectangleF(Pad + (i % cols) * (tileW + Gap), y + (i / cols) * (TileH + Gap), tileW, TileH);
                Tile(g, p, rect, items[i].Id, items[i].Name, items[i].Locked, items[i].On, items[i].Wear, items[i].Draw);
            }
            y += ((items.Count + cols - 1) / cols) * (TileH + Gap);

            if (_note.Length > 0)
            {
                foreach (string line in Ui.Wrap(Ui.Bold(10), _note, inner))
                {
                    g.DrawText(Ui.Bold(10), p.AccentText, Pad, y, line);
                    y += Ui.Bold(10).LineHeight;
                }
            }
            return y;
        }

        private void Tile(Graphics g, Palette p, RectangleF rect, string id, string name, bool locked, bool on, Action wear, Action<RectangleF> draw)
        {
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
            float pet = Math.Min(rect.Width - 12, 64);
            draw(new RectangleF(rect.X + (rect.Width - pet) / 2, rect.Y + 4 + lift, pet, pet));
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
            _hits.Add((rect, id, wear));
        }

        private void SmallButton(Graphics g, Palette p, RectangleF rect, string text, string id, Action action, bool primary)
        {
            bool pressed = _pressed == id, hover = _hover == id;
            float lift = pressed ? 3 : 0;
            Color face = primary ? p.Ink : hover ? p.TileHover : p.TileFace;
            Color border = primary ? p.InkShadow : p.Border;
            if (primary)
            {
                g.FillPath(border, GraphicsPath.GetRoundRect(new RectangleF(rect.X, rect.Y + 3, rect.Width, rect.Height - 3), 10));
                g.FillPath(face, GraphicsPath.GetRoundRect(new RectangleF(rect.X, rect.Y + lift, rect.Width, rect.Height - 3), 10));
            }
            else
                Ui.Chunky(g, rect, 10, face, border, lip: 3, lift: lift, stroke: 1.5f);
            Font f = Ui.Bold(9.5f);
            string label = Ui.Ellipsize(f, text, rect.Width - 8);
            SizeF s = f.MeasureString(label);
            g.DrawText(f, primary ? p.OnInk : p.Text, rect.X + (rect.Width - s.Width) / 2, rect.Y + lift + (rect.Height - 3 - s.Height) / 2, label);
            _hits.Add((rect, id, action));
        }

        // ------------------------------------------------------------------ rows

        /// <summary>The leaderboard lives on the Trophies page: one row that opens the boards.</summary>
        private float LeaderboardRow(Graphics g, Palette p, float w, float y)
        {
            const string id = "leaderboard";
            var rect = new RectangleF(Pad, y, w - 2 * Pad, 64);
            bool hover = _hover == id, pressed = _pressed == id;
            Ui.Chunky(g, rect, 14, hover ? p.TileHover : p.TileFace, p.Ink, lip: 4, lift: pressed ? 3 : 0);
            float lift = pressed ? 3 : 0;
            var tile = new RectangleF(rect.X + 12, rect.Y + 10 + lift, 40, 40);
            g.FillPath(p.Ink, GraphicsPath.GetRoundRect(tile, 12));
            Ui.Trophy(g, new PointF(tile.X + 20, tile.Y + 20), 1f, p.OnInk);

            string right = $"{Leaderboard.Total:N0} pts";
            Font rf = Ui.Bold(10);
            float rw = rf.MeasureString(right).Width;
            g.DrawText(rf, p.AccentText, rect.Right - 14 - rw, rect.Y + 22 + lift, right);
            float tx = tile.Right + 12, tw = rect.Right - 14 - rw - 10 - tx;
            g.DrawText(Ui.Bold(11.5f), p.Text, tx, rect.Y + 14 + lift, Ui.Ellipsize(Ui.Bold(11.5f), Leaderboard.HasProfile ? "Leaderboard" : "Join the leaderboard", tw));
            g.DrawText(Ui.Regular(9.5f), p.SubtleText, tx, rect.Y + 32 + lift, Ui.Ellipsize(Ui.Regular(9.5f), "Global and per-game boards", tw));
            _hits.Add((rect, id, () => _host.Navigate(new LeaderboardView(_host))));
            return rect.Bottom;
        }

        private static float StatsRow(Graphics g, Palette p, float w, float y, string game)
        {
            var s = Core.Progress.Get(game);
            int winRate = s.Played == 0 ? 0 : (int)Math.Round(100.0 * s.Wins / s.Played);
            bool last = s.LastResult.Length > 0;
            float rowH = last ? 66 : 50;
            var rect = new RectangleF(Pad, y, w - 2 * Pad, rowH);
            Ui.Chunky(g, rect, 14, p.TileFace, p.Border, lip: 3);
            float tx = rect.X + 14, tw = rect.Width - 28;
            g.DrawText(Ui.Bold(11), p.Text, tx, rect.Y + 9, Ui.Ellipsize(Ui.Bold(11), Games.Title(game), tw));
            g.DrawText(Ui.Regular(9.5f), p.SubtleText, tx, rect.Y + 27,
                Ui.Ellipsize(Ui.Regular(9.5f), $"Played {s.Played} · Won {winRate}% · Streak {s.CurrentStreak} · Best {s.BestStreak}", tw));
            if (last)
                g.DrawText(Ui.Regular(9.5f), p.SubtleText, tx, rect.Y + 43,
                    Ui.Ellipsize(Ui.Regular(9.5f), $"Last ({s.LastPlayedDay}): {s.LastResult}", tw));
            return y + rowH;
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
