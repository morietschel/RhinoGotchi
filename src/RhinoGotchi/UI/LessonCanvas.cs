using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using RhinoGotchi.Lessons;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// The whole lesson UI, drawn by hand so it looks the same on Mac and Windows:
    /// progress bar and hearts on top, the card in the middle, a chunky button or
    /// a green/red verdict banner at the bottom. Keys: 1-4 pick, Enter checks/continues.
    /// </summary>
    internal sealed partial class LessonCanvas : Drawable
    {
        private const float Pad = 16f;
        private const float Gap = 12f;
        private const float ButtonH = 46f;
        private const double AnimSeconds = 0.32;
        private const float MaxColumn = 520f;

        private static readonly string[] Thinking =
        {
            "Hmm... these all look so alike!",
            "Take your time. Look at the curves!",
            "Orange = rails, purple = profiles.",
            "I always mix these two up...",
            "You've got this!",
        };

        private readonly GotchiGame _game;
        private readonly List<(RectangleF rect, string id, Action action)> _hits = new List<(RectangleF, string, Action)>();
        private readonly UITimer _timer = new UITimer { Interval = 1 / 60.0 };
        private readonly Dictionary<(bool bold, float size), Font> _fonts = new Dictionary<(bool, float), Font>();

        private string? _hover;
        private string? _pressed;
        private DateTime _animStart = DateTime.MinValue;
        private int _popIndex = -1;
        private bool _bannerAnim;
        private bool _wasChecked, _wasSolved;
        private int _wasSelected = -1;
        private SessionScreen _wasScreen;
        private GameMode _wasMode;
        private string? _toast;
        private DateTime _toastUntil;
        private object? _wasStep;
        private string _bubble = Thinking[0];
        private int _bubbleSeed;
        private Palette _t = Palette.Light;

        private bool _subscribed;
        private float _ox;

        public LessonCanvas(GotchiGame game)
        {
            _game = game;
            CanFocus = true;
            MinimumSize = new Size(260, 560);
            Subscribe();
            SizeChanged += (s, e) => Invalidate();
            _timer.Elapsed += (s, e) =>
            {
                if (AnimProgress >= 1)
                    _timer.Stop();
                Invalidate();
            };
            _wasMode = game.Mode;
        }

        /// <summary>The lesson being played. Only valid while <see cref="GotchiGame.Mode"/> is Playing.</summary>
        private LessonSession _session => _game.Session!;

        /// <summary>Back to the sidebar's home page.</summary>
        public Action? ExitHome { get; set; }

        /// <summary>Opens the Trophies page (from an "achievement unlocked" card).</summary>
        public Action? OpenTrophies { get; set; }

        public void Subscribe()
        {
            if (_subscribed)
                return;
            _subscribed = true;
            _game.Changed += OnGameChanged;
            Invalidate();
        }

        public void Unsubscribe()
        {
            if (!_subscribed)
                return;
            _subscribed = false;
            _game.Changed -= OnGameChanged;
            _timer.Stop();
        }

        private double AnimProgress => Math.Min(1, (DateTime.Now - _animStart).TotalSeconds / AnimSeconds);

        private static float Ease(double t) => (float)(1 - Math.Pow(1 - t, 3));

        // ------------------------------------------------------------------ state → animation

        private void OnGameChanged()
        {
            bool animate = false;
            if (_game.Mode != _wasMode)
            {
                _wasMode = _game.Mode;
                _bannerAnim = false;
                _popIndex = -1;
                _animStart = DateTime.Now;
                _timer.Start();
            }
            if (_game.Session == null)
            {
                _wasStep = null;
                _wasChecked = _wasSolved = false;
                _wasSelected = -1;
                Application.Instance.AsyncInvoke(Invalidate);
                return;
            }

            if (_session.Screen != _wasScreen || !ReferenceEquals(_session.Step, _wasStep))
            {
                _bubble = Thinking[_bubbleSeed++ % Thinking.Length];
                _popIndex = -1;
                _bannerAnim = false;
                animate = true;
            }
            if (_session.SelectedIndex != _wasSelected && _session.SelectedIndex >= 0)
            {
                _popIndex = _session.SelectedIndex;
                animate = true;
            }
            if ((_session.Checked && !_wasChecked) || (_session.BuildSolved && !_wasSolved))
            {
                _bannerAnim = true;
                _popIndex = -1;
                animate = true;
            }

            _wasScreen = _session.Screen;
            _wasStep = _session.Step;
            _wasSelected = _session.SelectedIndex;
            _wasChecked = _session.Checked;
            _wasSolved = _session.BuildSolved;

            if (animate)
            {
                _animStart = DateTime.Now;
                _timer.Start();
            }
            Application.Instance.AsyncInvoke(Invalidate);
        }

        // ------------------------------------------------------------------ input

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            string? hover = HitAt(e.Location)?.id;
            if (hover != _hover)
            {
                _hover = hover;
                Cursor = hover != null ? Cursors.Pointer : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = null;
            _pressed = null;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            _pressed = HitAt(e.Location)?.id;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            var hit = HitAt(e.Location);
            string? pressed = _pressed;
            _pressed = null;
            Invalidate();
            if (hit != null && hit.Value.id == pressed)
                hit.Value.action();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            int n = e.Key switch
            {
                Keys.D1 or Keys.Keypad1 => 0,
                Keys.D2 or Keys.Keypad2 => 1,
                Keys.D3 or Keys.Keypad3 => 2,
                Keys.D4 or Keys.Keypad4 => 3,
                _ => -1,
            };
            if (n >= 0 && _game.Session != null)
            {
                _session.Select(n);
                e.Handled = true;
            }
            else if (e.Key == Keys.Enter)
            {
                _hits.FirstOrDefault(h => h.id == "primary").action?.Invoke();
                e.Handled = true;
            }
        }

        private (RectangleF rect, string id, Action action)? HitAt(PointF p)
        {
            p = new PointF(p.X - _ox, p.Y);
            for (int i = _hits.Count - 1; i >= 0; i--)
                if (_hits[i].rect.Contains(p))
                    return _hits[i];
            return null;
        }

        // ------------------------------------------------------------------ paint

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.AntiAlias = true;
            _t = Palette.Current;
            _hits.Clear();

            float h = Height;
            g.FillRectangle(_t.Background, 0, 0, Width, h);
            // On a wide panel, keep a comfortable centered column (hit tests subtract the offset).
            float w = Math.Min(Width, MaxColumn);
            _ox = (float)Math.Floor((Width - w) / 2);
            g.TranslateTransform(_ox, 0);

            float needed = _game.Mode switch
            {
                GameMode.Welcome => PaintWelcome(g, w, h),
                GameMode.Map => PaintMap(g, w, h),
                GameMode.PlacementResult => PaintPlacementResult(g, w, h),
                _ => PaintPlaying(g, w, h),
            };

            int min = (int)Math.Ceiling(needed);
            if (Math.Abs(MinimumSize.Height - min) > 1)
                Application.Instance.AsyncInvoke(() => MinimumSize = new Size(260, min));
        }

        private float PaintPlaying(Graphics g, float w, float h)
        {
            if (_game.Session == null)
                return h;
            return _session.Screen switch
            {
                SessionScreen.Intro => PaintIntro(g, w, h),
                SessionScreen.Quiz => PaintQuiz(g, w, h),
                SessionScreen.Build => PaintBuild(g, w, h),
                SessionScreen.Complete => PaintComplete(g, w, h),
                _ => PaintOutOfHearts(g, w, h),
            };
        }

        private float PaintIntro(Graphics g, float w, float h)
        {
            float bob = (float)Math.Sin(AnimProgress * Math.PI) * -8;
            float y = Pad + 12;
            Mascot.Draw(g, new RectangleF(w / 2 - 65, y, 130, 130), Mood.Happy, bob);
            y += 140;
            y += Centered(g, "RhinoGotchi", Bold(26), _t.Text, w, y) + 4;
            y += Centered(g, _session.Lesson.Title, Bold(16), _t.Text, w, y) + 16;

            int cards = _session.Lesson.Steps.Count;
            int quizzes = _session.Lesson.Steps.OfType<IconQuizStep>().Count();
            y = InfoRow(g, w, y, _t.Purple, $"{quizzes} icon quizzes");
            y = InfoRow(g, w, y, _t.Orange, $"{cards - quizzes} build challenge{(cards - quizzes == 1 ? "" : "s")} in the viewport");
            y = InfoRow(g, w, y, _t.Red, $"{LessonSession.MaxHearts} hearts. Mistakes come back at the end!");

            return Footer(g, w, h, y + Gap, () =>
                ChunkyButton(g, FooterButtonRect(w, h, y + Gap), "START LESSON", _t.Ink, _t.InkShadow, _t.OnInk, "primary", _session.Begin));
        }

        private float PaintQuiz(Graphics g, float w, float h)
        {
            float y = TopBar(g, w);
            var quiz = (IconQuizStep)_session.Step!;

            string kindTag = quiz.Kind switch
            {
                QuizKind.NameIt => "NAME THE ICON",
                QuizKind.Scenario => "SOLVE THE TASK",
                _ => "SELECT THE ICON",
            };
            string tag = _session.IsPlacement ? $"PLACEMENT · LEVEL {quiz.Tier + 1} · {kindTag}"
                : _session.IsRetry ? $"PREVIOUS MISTAKE · {kindTag}" : kindTag;
            Color tagColor = _session.IsPlacement ? _t.AccentText : _session.IsRetry ? _t.Orange : _t.SubtleText;
            y += Wrapped(g, Pad, y, w - 2 * Pad, Bold(12), tagColor, tag) + 6;
            if (_session.IsPlacement)
                y = DifficultyDots(g, y, quiz.Kind);

            switch (quiz.Kind)
            {
                case QuizKind.NameIt:
                    y += Wrapped(g, Pad, y, w - 2 * Pad, Bold(21), _t.Text, "What is this command called?") + 10;
                    break;
                case QuizKind.Scenario:
                    y += Wrapped(g, Pad, y, w - 2 * Pad, Bold(18), _t.Text, quiz.Prompt) + 10;
                    break;
                default:
                    y += Rich(g, Pad, y, w - 2 * Pad, Bold(21),
                        ("Which one of these is", _t.Text), (quiz.Command, _t.Accent), ("?", _t.Text)) + 10;
                    break;
            }

            Mood mood = !_session.Checked ? Mood.Thinking : _session.AnsweredCorrectly ? Mood.Celebrate : Mood.Sad;
            string bubble = !_session.Checked ? _bubble : _session.AnsweredCorrectly ? "Yes! That's the one!" : "Oops! Look closely...";
            float hop = _session.Checked && _session.AnsweredCorrectly && _bannerAnim ? (float)Math.Sin(AnimProgress * Math.PI) * -10 : 0;
            y = MascotRow(g, w, y, mood, bubble, hop) + 14;

            bool names = quiz.Kind == QuizKind.NameIt;
            if (names)
            {
                // The one icon being asked about
                float side = Math.Min(w - 2 * Pad, 170);
                var card = new RectangleF((w - side) / 2, y, side, side);
                Card(g, card, _t.TileFace, _t.Border);
                LessonIcons.Draw(g, quiz.Command, new RectangleF(card.X + 14, card.Y + 14, side - 28, side - 28));
                y += side + 4 + Gap + 4;
            }

            // 2x2 grid: icons, or names for Name-it
            float tw = (w - 2 * Pad - Gap) / 2;
            float th = names ? 60 : Math.Min(tw, 158);
            for (int i = 0; i < _session.Options.Count; i++)
            {
                int index = i;
                var rect = new RectangleF(Pad + (i % 2) * (tw + Gap), y + (i / 2) * (th + Gap), tw, th);
                IconTile(g, rect, index, names);
                if (!_session.Checked)
                    _hits.Add((rect, $"tile{index}", () => _session.Select(index)));
            }
            y += 2 * th + Gap + 4;

            if (!_session.Checked)
            {
                bool enabled = _session.SelectedIndex >= 0;
                return Footer(g, w, h, y + Gap, () =>
                    ChunkyButton(g, FooterButtonRect(w, h, y + Gap), "CHECK",
                        enabled ? _t.Ink : _t.Disabled, enabled ? _t.InkShadow : _t.Disabled,
                        enabled ? _t.OnInk : _t.DisabledText, enabled ? "primary" : null, _session.Check));
            }

            bool ok = _session.AnsweredCorrectly;
            string title = ok ? (_session.IsRetry ? "Got it this time!" : "Nice job!") : "Correct answer:";
            string body = ok ? CommandInfo.Tip(quiz.Command) : $"{quiz.Command}. {CommandInfo.Tip(quiz.Command)}";
            string xp = ok && quiz.Xp > 0 ? $"+{quiz.Xp} XP" : "";
            string button = _session.IsPlacement ? (ok ? "CONTINUE" : "SEE MY LEVEL") : ok ? "CONTINUE" : "GOT IT";
            return Verdict(g, w, h, y + Gap, ok, title, body, xp, button, _session.Continue);
        }

        private float PaintBuild(Graphics g, float w, float h)
        {
            if (_session.Step is TutorialBuildStep tutorial)
                return PaintTutorialBuild(g, w, h, tutorial);

            float y = TopBar(g, w);
            var level = _session.BuildLevel;
            string command = level?.Command ?? "";

            g.DrawText(Bold(12), _t.Orange, Pad, y, "BUILD IT IN THE VIEWPORT");
            y += 22;
            y += Wrapped(g, Pad, y, w - 2 * Pad, Bold(21), _t.Text, level?.Title ?? "") + 10;

            Mood mood = _session.BuildSolved ? Mood.Celebrate : Mood.Thinking;
            string bubble = _session.BuildSolved ? "Woohoo! Perfect build!"
                : _session.BuildRunning ? "Snap to the points. Match the ghost!"
                : $"Time to use {command} for real!";
            y = MascotRow(g, w, y, mood, bubble, 0) + 14;

            // Command card: icon + tip
            float cardH = 104;
            var card = new RectangleF(Pad, y, w - 2 * Pad, cardH);
            Card(g, card, _t.TileFace, _t.Border);
            var iconRect = new RectangleF(card.X + 8, card.Y + 8, cardH - 16, cardH - 16);
            LessonIcons.Draw(g, command, iconRect);
            float tx = iconRect.Right + 8, tW = card.Right - tx - 12;
            g.DrawText(Bold(16), _t.Text, tx, card.Y + 14, command);
            Wrapped(g, tx, card.Y + 38, tW, Regular(12), _t.SubtleText, CommandInfo.Tip(command), maxLines: 4);
            y += cardH + 4 + Gap;

            // Live status
            if (_session.BuildRunning || _session.BuildResult != null)
            {
                Pill(g, Pad, y, _session.BuildRunning ? "LIVE IN VIEWPORT" : "LEVEL STOPPED", _session.BuildRunning ? _t.Green : _t.SubtleText);
                y += 30;
                var result = _session.BuildResult;
                string feedback = result?.Feedback ?? "Waiting for your first command...";
                y += Wrapped(g, Pad, y, w - 2 * Pad, Regular(13), _t.Text, feedback) + 8;
                double score = result?.Score ?? 0;
                Bar(g, new RectangleF(Pad, y, w - 2 * Pad - 50, 14), score, _t.Gold);
                g.DrawText(Bold(13), _t.Gold, w - Pad - 42, y - 2, $"{score:P0}");
                y += 22;
            }
            else
            {
                y += Wrapped(g, Pad, y, w - 2 * Pad, Regular(13), _t.SubtleText,
                    "Press START: two points and a blue ghost box appear in the viewport. Build the box with Box > Diagonal.") + 8;
            }

            if (_session.BuildSolved)
            {
                var r = _session.BuildResult;
                int xp = (int)Math.Round(_session.Step!.Xp * (r?.Score ?? 0));
                return Verdict(g, w, h, y + Gap, true, "Level solved!", r?.Feedback ?? "", xp > 0 ? $"+{xp} XP" : "", "CONTINUE", _session.Continue);
            }

            // START / CHECK + SKIP
            float footerTop = y + Gap;
            float footerH = Pad + ButtonH + 4 + 10 + 40 + Pad;
            float top = Math.Max(footerTop, h - footerH);
            g.DrawLine(new Pen(_t.Border, 2), 0, top, w, top);
            var main = new RectangleF(Pad, top + Pad, w - 2 * Pad, ButtonH);
            if (_session.BuildRunning)
                ChunkyButton(g, main, "CHECK MY MODEL", _t.Ink, _t.InkShadow, _t.OnInk, "primary", () => Levels.LevelRunner.Current?.Evaluate());
            else
                ChunkyButton(g, main, "START LEVEL", _t.Ink, _t.InkShadow, _t.OnInk, "primary", _session.StartBuild);
            var skip = new RectangleF(Pad, main.Bottom + 4 + 10, w - 2 * Pad, 40);
            OutlineButton(g, skip, "SKIP", "skip", _session.Continue);
            return footerTop + footerH;
        }

        private float PaintComplete(Graphics g, float w, float h)
        {
            float y = Pad + 8;
            Confetti(g, w, y);
            Mascot.Draw(g, new RectangleF(w / 2 - 60, y + 6, 120, 120), Mood.Celebrate, (float)Math.Sin(AnimProgress * Math.PI) * -12);
            y += 136;
            y += Centered(g, "Lesson complete!", Bold(24), _t.Text, w, y) + 18;

            int asked = Math.Max(1, _session.QuizzesAsked);
            int acc = (int)Math.Round(100.0 * _session.FirstTryCorrect / asked);
            var stats = new[]
            {
                ("TOTAL XP", _session.Xp.ToString(), _t.Gold),
                ("ACCURACY", $"{acc}%", _t.Green),
                ("HEARTS", $"{_session.Hearts}/{LessonSession.MaxHearts}", _t.Red),
            };
            float cw = (w - 2 * Pad - 2 * 8) / 3;
            for (int i = 0; i < stats.Length; i++)
            {
                var (label, value, color) = stats[i];
                var r = new RectangleF(Pad + i * (cw + 8), y, cw, 78);
                g.FillPath(color, GraphicsPath.GetRoundRect(r, 12));
                g.DrawText(Bold(10), Colors.White, r.X + (r.Width - Bold(10).MeasureString(label).Width) / 2, r.Y + 5, label);
                var inner = new RectangleF(r.X + 2, r.Y + 24, r.Width - 4, r.Height - 26);
                g.FillPath(_t.Background, GraphicsPath.GetRoundRect(inner, 10));
                SizeF vs = Bold(20).MeasureString(value);
                g.DrawText(Bold(20), color, inner.X + (inner.Width - vs.Width) / 2, inner.Y + (inner.Height - vs.Height) / 2, value);
            }
            y += 78 + Gap;
            y = UnlockCards(g, w, y + 4);

            return Footer(g, w, h, y + Gap, () =>
                ChunkyButton(g, FooterButtonRect(w, h, y + Gap), "CONTINUE", _t.Ink, _t.InkShadow, _t.OnInk, "primary", _game.FinishLesson));
        }

        private float PaintOutOfHearts(Graphics g, float w, float h)
        {
            float y = Pad + 12;
            Mascot.Draw(g, new RectangleF(w / 2 - 60, y, 120, 120), Mood.Sad);
            y += 130;
            Heart(g, new PointF(w / 2, y + 22), 44, _t.Disabled);
            y += 56;
            y += Centered(g, "Out of hearts!", Bold(24), _t.Red, w, y) + 8;
            y += Wrapped(g, Pad + 12, y, w - 2 * Pad - 24, Regular(14), _t.SubtleText,
                "Every mistake costs a heart. Shake it off and take another run at it!", center: true) + 8;
            float footerTop = y + Gap;
            float footerH = Pad + ButtonH + 4 + 10 + 40 + Pad;
            float top = Math.Max(footerTop, h - footerH);
            g.DrawLine(new Pen(_t.Border, 2), 0, top, w, top);
            ChunkyButton(g, new RectangleF(Pad, top + Pad, w - 2 * Pad, ButtonH), "TRY AGAIN", _t.Ink, _t.InkShadow, _t.OnInk, "primary",
                _game.RetryLesson);
            OutlineButton(g, new RectangleF(Pad, top + Pad + ButtonH + 14, w - 2 * Pad, 40), "BACK TO MAP", "map", _game.BackToMap);
            return footerTop + footerH;
        }

        // ------------------------------------------------------------------ pieces

        /// <summary>Close button, progress bar and hearts. Returns the y below it.</summary>
        private float TopBar(Graphics g, float w)
        {
            float y = 14;
            // Close (back to the intro)
            var close = new RectangleF(Pad - 4, y - 2, 26, 26);
            var pen = new Pen(_hover == "close" ? _t.Text : _t.SubtleText, 2.6f) { LineCap = PenLineCap.Round };
            g.DrawLine(pen, close.X + 7, close.Y + 7, close.Right - 7, close.Bottom - 7);
            g.DrawLine(pen, close.Right - 7, close.Y + 7, close.X + 7, close.Bottom - 7);
            _hits.Add((close, "close", _game.BackToMap));

            // Hearts (none in the placement test)
            float hx = w - Pad + 30;
            if (!_session.IsPlacement)
            {
                string hearts = _session.Hearts.ToString();
                SizeF hs = Bold(16).MeasureString(hearts);
                hx = w - Pad - hs.Width;
                g.DrawText(Bold(16), _t.Red, hx, y + 1, hearts);
                Heart(g, new PointF(hx - 14, y + 11), 20, _t.Red);
            }

            // Progress
            float px = close.Right + 8, pw = hx - 30 - px;
            Bar(g, new RectangleF(px, y + 4, pw, 16), _session.Progress, _t.Green);
            return y + 40;
        }

        private float MascotRow(Graphics g, float w, float y, Mood mood, string text, float hop)
        {
            const float m = 70;
            Mascot.Draw(g, new RectangleF(Pad - 4, y, m, m), mood, hop);
            float bx = Pad + m + 10, bw = w - bx - Pad;
            Font f = Regular(13);
            List<string> lines = Wrap(f, text, bw - 24);
            float lh = f.LineHeight;
            float bh = Math.Max(40, lines.Count * lh + 20);
            float by = y + (m - bh) / 2;
            var bubble = new RectangleF(bx, by, bw, bh);
            var path = GraphicsPath.GetRoundRect(bubble, 12);
            g.FillPath(_t.Background, path);
            g.DrawPath(new Pen(_t.Border, 2), path);
            // tail
            float cy = by + bh / 2;
            var tail = new[] { new PointF(bx + 1, cy - 7), new PointF(bx - 9, cy), new PointF(bx + 1, cy + 7) };
            g.FillPolygon(_t.Background, tail);
            g.DrawLines(new Pen(_t.Border, 2) { LineJoin = PenLineJoin.Round }, tail);
            for (int i = 0; i < lines.Count; i++)
                g.DrawText(f, _t.Text, bx + 12, by + 10 + i * lh, lines[i]);
            return Math.Max(y + m, by + bh);
        }

        private void IconTile(Graphics g, RectangleF rect, int index, bool textOnly)
        {
            string command = _session.Options[index];
            bool selected = _session.SelectedIndex == index;
            bool isCorrect = index == _session.CorrectIndex;
            bool hover = _hover == $"tile{index}" && !_session.Checked;
            bool pressed = _pressed == $"tile{index}";

            Color face = _t.TileFace, border = _t.Border, label = _t.SubtleText;
            if (_session.Checked)
            {
                if (isCorrect) { face = _t.GreenBanner; border = _t.Green; label = _t.GreenText; }
                else if (selected) { face = _t.RedBanner; border = _t.Red; label = _t.RedText; }
            }
            else if (selected) { face = _t.SelectedFace; border = _t.SelectedBorder; label = _t.SelectedText; }
            else if (hover) { face = _t.TileHover; }

            // Little bounce when picked
            if (index == _popIndex && AnimProgress < 1)
            {
                float s = 1 + 0.05f * (float)Math.Sin(AnimProgress * Math.PI);
                rect = new RectangleF(rect.X - rect.Width * (s - 1) / 2, rect.Y - rect.Height * (s - 1) / 2, rect.Width * s, rect.Height * s);
            }

            float lift = pressed ? 3 : 0;
            var shadow = new RectangleF(rect.X, rect.Y + 4, rect.Width, rect.Height);
            var top = new RectangleF(rect.X, rect.Y + lift, rect.Width, rect.Height);
            g.FillPath(border, GraphicsPath.GetRoundRect(shadow, 14));
            g.FillPath(border, GraphicsPath.GetRoundRect(top, 14));
            var inner = new RectangleF(top.X + 2, top.Y + 2, top.Width - 4, top.Height - 4);
            g.FillPath(face, GraphicsPath.GetRoundRect(inner, 12));

            // Key hint
            var badge = textOnly
                ? new RectangleF(inner.X + 7, inner.Y + (inner.Height - 20) / 2, 20, 20)
                : new RectangleF(inner.X + 7, inner.Y + 7, 20, 20);
            var badgePath = GraphicsPath.GetRoundRect(badge, 6);
            g.DrawPath(new Pen(border, 2), badgePath);
            string n = (index + 1).ToString();
            SizeF ns = Bold(11).MeasureString(n);
            g.DrawText(Bold(11), label, badge.X + (badge.Width - ns.Width) / 2, badge.Y + (badge.Height - ns.Height) / 2, n);

            if (textOnly)
            {
                FitLabel(g, command, new RectangleF(badge.Right + 4, inner.Y + 4, inner.Right - badge.Right - 10, inner.Height - 8), 15, selected || _session.Checked ? label : _t.Text);
                if (_session.Checked && !isCorrect && !selected)
                    g.FillPath(Color.FromArgb(_t.Background.Rb, _t.Background.Gb, _t.Background.Bb, 110), GraphicsPath.GetRoundRect(inner, 12));
                return;
            }

            // The icon. After Check, every tile shows its name: wrong answers teach too.
            float labelH = _session.Checked ? 22 : 0;
            var iconRect = new RectangleF(inner.X + 12, inner.Y + 14, inner.Width - 24, inner.Height - 22 - labelH);
            LessonIcons.Draw(g, command, iconRect);

            if (_session.Checked)
            {
                FitLabel(g, command, new RectangleF(inner.X + 4, inner.Bottom - 26, inner.Width - 8, 22), 12, label, singleLine: true);
                if (!isCorrect && !selected)
                    g.FillPath(Color.FromArgb(_t.Background.Rb, _t.Background.Gb, _t.Background.Bb, 110), GraphicsPath.GetRoundRect(inner, 12));
            }
        }

        /// <summary>Placement only: three dots showing how hard this question type is.</summary>
        private float DifficultyDots(Graphics g, float y, QuizKind kind)
        {
            int level = kind switch { QuizKind.NameIt => 2, QuizKind.Scenario => 3, _ => 1 };
            string word = level switch { 1 => "EASY", 2 => "MEDIUM", _ => "HARD" };
            Color c = level switch { 1 => _t.Green, 2 => _t.Orange, _ => _t.Red };
            for (int i = 0; i < 3; i++)
                g.FillEllipse(i < level ? c : _t.Track, Pad + i * 12, y + 3, 8, 8);
            g.DrawText(Bold(10), c, Pad + 40, y, word);
            return y + 20;
        }

        /// <summary>
        /// Draws a command name centered in <paramref name="rect"/>, shrinking the font to fit,
        /// and splitting long CamelCase names over two lines (Boolean / Intersection) when allowed.
        /// </summary>
        private void FitLabel(Graphics g, string text, RectangleF rect, float maxSize, Color color, bool singleLine = false)
        {
            for (float size = maxSize; size >= 9; size -= 1)
            {
                Font f = Bold(size);
                SizeF s = f.MeasureString(text);
                if (s.Width <= rect.Width || (singleLine && size <= 9))
                {
                    g.DrawText(f, color, rect.X + (rect.Width - s.Width) / 2, rect.Y + (rect.Height - s.Height) / 2, text);
                    return;
                }
                if (!singleLine && size <= maxSize - 2)
                {
                    int split = SplitPoint(text);
                    if (split > 0)
                    {
                        string a = text.Substring(0, split), b = text.Substring(split);
                        Font f2 = Bold(maxSize - 1);
                        while (f2.Size > 9 && Math.Max(f2.MeasureString(a).Width, f2.MeasureString(b).Width) > rect.Width)
                            f2 = Bold(f2.Size - 1);
                        float lh = f2.LineHeight;
                        float top = rect.Y + (rect.Height - 2 * lh) / 2;
                        g.DrawText(f2, color, rect.X + (rect.Width - f2.MeasureString(a).Width) / 2, top, a);
                        g.DrawText(f2, color, rect.X + (rect.Width - f2.MeasureString(b).Width) / 2, top + lh, b);
                        return;
                    }
                }
            }
        }

        /// <summary>Index of the CamelCase break nearest the middle, or -1.</summary>
        private static int SplitPoint(string text)
        {
            int best = -1;
            for (int i = 1; i < text.Length; i++)
                if (char.IsUpper(text[i]) && char.IsLower(text[i - 1]) && (best < 0 || Math.Abs(i - text.Length / 2) < Math.Abs(best - text.Length / 2)))
                    best = i;
            return best;
        }

        /// <summary>Green or red banner from the bottom with the explanation and a button.</summary>
        private float Verdict(Graphics g, float w, float h, float contentBottom, bool ok, string title, string body, string xp, string button, Action action)
        {
            Font tf = Bold(19), bf = Regular(13);
            List<string> lines = Wrap(bf, body, w - 2 * Pad);
            float bannerH = Pad + 30 + lines.Count * bf.LineHeight + 14 + ButtonH + 4 + Pad;
            float top = Math.Max(contentBottom, h - bannerH);
            float slide = _bannerAnim && AnimProgress < 1 ? (1 - Ease(AnimProgress)) * bannerH : 0;
            float y = top + slide;

            g.FillRectangle(ok ? _t.GreenBanner : _t.RedBanner, 0, y, w, Math.Max(bannerH, h - y) + 4);
            Color ink = ok ? _t.GreenText : _t.RedText;

            // Badge + title
            var badge = new RectangleF(Pad, y + Pad, 26, 26);
            g.FillEllipse(Colors.White, badge);
            var mark = new Pen(ink, 3.4f) { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            if (ok)
                g.DrawLines(mark, new PointF(badge.X + 7, badge.Y + 13.5f), new PointF(badge.X + 11.5f, badge.Y + 18), new PointF(badge.X + 19.5f, badge.Y + 8.5f));
            else
            {
                g.DrawLine(mark, badge.X + 8.5f, badge.Y + 8.5f, badge.Right - 8.5f, badge.Bottom - 8.5f);
                g.DrawLine(mark, badge.Right - 8.5f, badge.Y + 8.5f, badge.X + 8.5f, badge.Bottom - 8.5f);
            }
            g.DrawText(tf, ink, badge.Right + 10, y + Pad + 1, title);
            if (!string.IsNullOrEmpty(xp))
            {
                SizeF xs = Bold(14).MeasureString(xp);
                g.DrawText(Bold(14), _t.Gold, w - Pad - xs.Width, y + Pad + 5, xp);
            }

            float ly = y + Pad + 34;
            foreach (string line in lines)
            {
                g.DrawText(bf, ink, Pad, ly, line);
                ly += bf.LineHeight;
            }

            var btn = new RectangleF(Pad, ly + 14, w - 2 * Pad, ButtonH);
            ChunkyButton(g, btn, button, ok ? _t.Green : _t.Red, ok ? _t.GreenShadow : _t.RedShadow, Colors.White, "primary", action);
            return contentBottom + bannerH;
        }

        /// <summary>Plain footer: a top rule and one big button. Returns the total height needed.</summary>
        private float Footer(Graphics g, float w, float h, float contentBottom, Action drawButton)
        {
            float footerH = Pad + ButtonH + 4 + Pad;
            float top = Math.Max(contentBottom, h - footerH);
            g.DrawLine(new Pen(_t.Border, 2), 0, top, w, top);
            drawButton();
            return contentBottom + footerH;
        }

        private RectangleF FooterButtonRect(float w, float h, float contentBottom)
        {
            float footerH = Pad + ButtonH + 4 + Pad;
            float top = Math.Max(contentBottom, h - footerH);
            return new RectangleF(Pad, top + Pad, w - 2 * Pad, ButtonH);
        }

        private void ChunkyButton(Graphics g, RectangleF rect, string text, Color face, Color shadow, Color ink, string? id, Action action, float fontSize = 15)
        {
            bool pressed = id != null && _pressed == id;
            bool hover = id != null && _hover == id;
            float lift = pressed ? 4 : 0;
            g.FillPath(shadow, GraphicsPath.GetRoundRect(new RectangleF(rect.X, rect.Y + 4, rect.Width, rect.Height), 14));
            var top = new RectangleF(rect.X, rect.Y + lift, rect.Width, rect.Height);
            Color f = hover && !pressed ? Brighten(face, 0.06f) : face;
            g.FillPath(f, GraphicsPath.GetRoundRect(top, 14));
            // gloss stripe
            g.FillPath(Color.FromArgb(255, 255, 255, 40), GraphicsPath.GetRoundRect(new RectangleF(top.X + 10, top.Y + 5, top.Width - 20, 6), 3));
            Font font = Bold(fontSize);
            SizeF s = font.MeasureString(text);
            g.DrawText(font, ink, top.X + (top.Width - s.Width) / 2, top.Y + (top.Height - s.Height) / 2, text);
            if (id != null)
                _hits.Add((new RectangleF(rect.X, rect.Y, rect.Width, rect.Height + 4), id, action));
        }

        private void OutlineButton(Graphics g, RectangleF rect, string text, string id, Action action)
        {
            bool pressed = _pressed == id;
            float lift = pressed ? 3 : 0;
            g.FillPath(_t.Border, GraphicsPath.GetRoundRect(new RectangleF(rect.X, rect.Y + 3, rect.Width, rect.Height), 12));
            var top = new RectangleF(rect.X, rect.Y + lift, rect.Width, rect.Height);
            g.FillPath(_t.Border, GraphicsPath.GetRoundRect(top, 12));
            g.FillPath(_hover == id ? _t.TileHover : _t.Background, GraphicsPath.GetRoundRect(new RectangleF(top.X + 2, top.Y + 2, top.Width - 4, top.Height - 4), 10));
            Font font = Bold(14);
            SizeF s = font.MeasureString(text);
            g.DrawText(font, _t.SubtleText, top.X + (top.Width - s.Width) / 2, top.Y + (top.Height - s.Height) / 2, text);
            _hits.Add((rect, id, action));
        }

        private void Bar(Graphics g, RectangleF rect, double value, Color color)
        {
            float r = rect.Height / 2;
            g.FillPath(_t.Track, GraphicsPath.GetRoundRect(rect, r));
            float fw = (float)(rect.Width * Math.Max(0, Math.Min(1, value)));
            if (fw < rect.Height * 0.9f)
                return;
            var fill = new RectangleF(rect.X, rect.Y, fw, rect.Height);
            g.FillPath(color, GraphicsPath.GetRoundRect(fill, r));
            g.FillPath(Color.FromArgb(255, 255, 255, 70), GraphicsPath.GetRoundRect(new RectangleF(fill.X + 6, fill.Y + 3, Math.Max(0, fill.Width - 12), 4), 2));
        }

        private void Card(Graphics g, RectangleF rect, Color face, Color border)
        {
            g.FillPath(border, GraphicsPath.GetRoundRect(new RectangleF(rect.X, rect.Y + 4, rect.Width, rect.Height), 14));
            g.FillPath(border, GraphicsPath.GetRoundRect(rect, 14));
            g.FillPath(face, GraphicsPath.GetRoundRect(new RectangleF(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4), 12));
        }

        private void Pill(Graphics g, float x, float y, string text, Color color)
        {
            Font f = Bold(11);
            SizeF s = f.MeasureString(text);
            var r = new RectangleF(x, y, s.Width + 30, 22);
            g.FillPath(Color.FromArgb(color.Rb, color.Gb, color.Bb, 40), GraphicsPath.GetRoundRect(r, 11));
            g.FillEllipse(color, x + 8, y + 7, 8, 8);
            g.DrawText(f, color, x + 22, y + (22 - s.Height) / 2, text);
        }

        private float InfoRow(Graphics g, float w, float y, Color dot, string text)
        {
            g.FillEllipse(dot, Pad + 8, y + 5, 10, 10);
            float hgt = Wrapped(g, Pad + 28, y, w - 2 * Pad - 28, Regular(14), _t.Text, text);
            return y + Math.Max(20, hgt) + 8;
        }

        private static void Heart(Graphics g, PointF c, float size, Color color)
        {
            float s = size / 20f;
            var p = new GraphicsPath();
            p.MoveTo(c.X, c.Y + 7 * s);
            p.AddBezier(new PointF(c.X, c.Y + 7 * s), new PointF(c.X - 10 * s, c.Y), new PointF(c.X - 9 * s, c.Y - 8 * s), new PointF(c.X - 4.5f * s, c.Y - 8 * s));
            p.AddBezier(new PointF(c.X - 4.5f * s, c.Y - 8 * s), new PointF(c.X - 2 * s, c.Y - 8 * s), new PointF(c.X, c.Y - 6 * s), new PointF(c.X, c.Y - 4.5f * s));
            p.AddBezier(new PointF(c.X, c.Y - 4.5f * s), new PointF(c.X, c.Y - 6 * s), new PointF(c.X + 2 * s, c.Y - 8 * s), new PointF(c.X + 4.5f * s, c.Y - 8 * s));
            p.AddBezier(new PointF(c.X + 4.5f * s, c.Y - 8 * s), new PointF(c.X + 9 * s, c.Y - 8 * s), new PointF(c.X + 10 * s, c.Y), new PointF(c.X, c.Y + 7 * s));
            p.CloseFigure();
            g.FillPath(color, p);
            g.FillEllipse(Color.FromArgb(255, 255, 255, 110), c.X - 6 * s, c.Y - 5.5f * s, 3 * s, 2.4f * s);
        }

        private void Confetti(Graphics g, float w, float y)
        {
            var rnd = new Random(7);
            Color[] colors = { _t.Green, _t.Blue, _t.Gold, _t.Purple, _t.Red, _t.Orange };
            float fall = Ease(AnimProgress) * 12;
            for (int i = 0; i < 26; i++)
            {
                float x = (float)rnd.NextDouble() * w;
                float yy = y + (float)rnd.NextDouble() * 120 + fall;
                float s = 4 + (float)rnd.NextDouble() * 5;
                Color c = colors[i % colors.Length];
                if (i % 3 == 0)
                    g.FillEllipse(c, x, yy, s, s);
                else
                    g.FillRectangle(c, x, yy, s * 1.6f, s * 0.7f);
            }
        }

        /// <summary>"ACHIEVEMENT UNLOCKED" cards for anything the last lesson or test unlocked, each with WEAR IT.</summary>
        private float UnlockCards(Graphics g, float w, float y)
        {
            foreach (var a in _game.NewUnlocks)
            {
                var rect = new RectangleF(Pad, y, w - 2 * Pad, 84);
                Card(g, rect, _t.AccentSoft, _t.Accent);
                Mascot.Draw(g, new RectangleF(rect.X + 8, rect.Y + 6, 66, 66), Mood.Happy, hat: a.Hat);
                float tx = rect.X + 82;
                float btnW = 84;
                float tw = rect.Right - tx - btnW - 20;
                g.DrawText(Bold(10), _t.AccentText, tx, rect.Y + 12, "ACHIEVEMENT UNLOCKED");
                g.DrawText(Bold(14), _t.Text, tx, rect.Y + 28, Ellipsize(Bold(14), $"{a.Name} · {a.HatName}", tw));
                g.DrawText(Regular(11), _t.SubtleText, tx, rect.Y + 50, Ellipsize(Regular(11), a.Condition, tw));
                bool wearing = _game.Progress.EquippedHat == a.Hat;
                var btn = new RectangleF(rect.Right - btnW - 12, rect.Y + 22, btnW, 34);
                if (wearing)
                {
                    SizeF ws = Bold(12).MeasureString("WEARING");
                    g.DrawText(Bold(12), _t.GreenText, btn.X + (btn.Width - ws.Width) / 2, btn.Y + 8, "WEARING");
                }
                else
                {
                    var hat = a.Hat;
                    ChunkyButton(g, btn, "WEAR IT", _t.Accent, _t.AccentShadow, Colors.White, "wear" + a.Id, () => _game.Wear(hat), fontSize: 12);
                }
                y = rect.Bottom + 4 + Gap;
            }
            return y;
        }

        private static string Ellipsize(Font font, string text, float width) => Ui.Ellipsize(font, text, width);

        // ------------------------------------------------------------------ text

        private Font Bold(float size) => GetFont(true, size);

        private Font Regular(float size) => GetFont(false, size);

        private Font GetFont(bool bold, float size)
        {
            if (!_fonts.TryGetValue((bold, size), out Font? f))
            {
                f = new Font(bold ? SystemFont.Bold : SystemFont.Default, size);
                _fonts[(bold, size)] = f;
            }
            return f;
        }

        private static List<string> Wrap(Font font, string text, float maxWidth)
        {
            var lines = new List<string>();
            foreach (string para in text.Split('\n'))
            {
                string line = "";
                foreach (string word in para.Split(' '))
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
                lines.Add(line);
            }
            return lines;
        }

        private static float Wrapped(Graphics g, float x, float y, float maxWidth, Font font, Color color, string text, int maxLines = int.MaxValue, bool center = false)
        {
            List<string> lines = Wrap(font, text, maxWidth);
            if (lines.Count > maxLines)
            {
                lines = lines.Take(maxLines).ToList();
                lines[maxLines - 1] += "…";
            }
            float lh = font.LineHeight;
            for (int i = 0; i < lines.Count; i++)
            {
                float lx = center ? x + (maxWidth - font.MeasureString(lines[i]).Width) / 2 : x;
                g.DrawText(font, color, lx, y + i * lh, lines[i]);
            }
            return lines.Count * lh;
        }

        private static float Centered(Graphics g, string text, Font font, Color color, float w, float y)
        {
            SizeF s = font.MeasureString(text);
            g.DrawText(font, color, (w - s.Width) / 2, y, text);
            return s.Height;
        }

        /// <summary>Word-wrapped text where each run has its own color.</summary>
        private static float Rich(Graphics g, float x, float y, float maxWidth, Font font, params (string text, Color color)[] runs)
        {
            float space = font.MeasureString(" ").Width;
            float lh = font.LineHeight;
            float cx = x, cy = y;
            foreach (var (text, color) in runs)
            {
                string[] words = text.Split(' ');
                for (int i = 0; i < words.Length; i++)
                {
                    string word = words[i];
                    if (word.Length == 0)
                        continue;
                    bool glue = word == "?" || word == "." || word == "!";
                    float ww = font.MeasureString(word).Width;
                    float start = cx == x || glue ? cx : cx + space;
                    if (start + ww > x + maxWidth && cx > x && !glue)
                    {
                        cx = x;
                        cy += lh;
                        start = x;
                    }
                    g.DrawText(font, color, start, cy, word);
                    cx = start + ww;
                }
            }
            return cy - y + lh;
        }

        private static Color Brighten(Color c, float amount) =>
            new Color(Math.Min(1, c.R + amount), Math.Min(1, c.G + amount), Math.Min(1, c.B + amount), c.A);
    }
}
