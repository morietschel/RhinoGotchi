using System;
using Rhino;
using Rhino.Display;
using RhinoGotchi.UI;
using EtoRect = Eto.Drawing.RectangleF;
using EtoSize = Eto.Drawing.SizeF;

namespace RhinoGotchi.Display
{
    /// <summary>
    /// The Gotchi in the viewport, the same in every game: bottom left of the view the game plays in, with a
    /// speech bubble beside it, wearing the player's hat, face and clothes. It is awake while you play, hops
    /// when you win, faints when you miss, and dozes after 5 s without a click or key press.
    ///
    /// A game calls <see cref="Show"/> when it starts, <see cref="Say"/>, <see cref="Cheer"/> and
    /// <see cref="Groan"/> while it runs, and <see cref="Hide"/> when it ends. Games with their own viewport
    /// HUD (Build a building, Build a facade) draw the same pet with <see cref="DrawPet"/>.
    /// </summary>
    internal static class ViewportPet
    {
        private const double Fps = 8;
        private const double ReactSeconds = 3;

        private static readonly Conduit Overlay = new Conduit();
        private static Eto.Forms.UITimer? _timer;
        private static string? _owner;
        private static string _speech = "";
        private static Mood _reaction = Mood.Happy;
        private static DateTime _reactionUntil = DateTime.MinValue;
        private static Guid _viewport;
        private static Mood _lastDrawn = Mood.Happy;

        /// <summary>Shows the pet for <paramref name="owner"/> (a game id), in the active view or <paramref name="viewportId"/>.</summary>
        public static void Show(string owner, string speech, Guid viewportId = default)
        {
            _owner = owner;
            _speech = speech;
            _reaction = Mood.Happy;
            _reactionUntil = DateTime.MinValue;
            _viewport = viewportId;
            Fin.PetActivity.Start();
            Fin.PetActivity.Touch();
            Overlay.Enabled = true;
            StartTimer();
            Redraw();
        }

        /// <summary>Changes what the pet says (only for the game that showed it).</summary>
        public static void Say(string owner, string speech)
        {
            if (_owner != owner || _speech == speech)
                return;
            _speech = speech;
            Redraw();
        }

        /// <summary>The pet hops and cheers for a few seconds.</summary>
        public static void Cheer(string owner, string speech) => React(owner, Mood.Celebrate, speech);

        /// <summary>The pet faints for a few seconds (a miss, a crash, a wrong answer).</summary>
        public static void Groan(string owner, string speech) => React(owner, Mood.Sad, speech);

        /// <summary>Takes the pet away (only for the game that showed it).</summary>
        public static void Hide(string owner)
        {
            if (_owner != owner)
                return;
            _owner = null;
            Overlay.Enabled = false;
            _timer?.Stop();
            Redraw();
        }

        public static bool IsShownBy(string owner) => _owner == owner;

        private static void React(string owner, Mood mood, string speech)
        {
            if (_owner != owner)
                return;
            _reaction = mood;
            _reactionUntil = DateTime.Now.AddSeconds(ReactSeconds);
            _speech = speech;
            Fin.PetActivity.Touch();
            Redraw();
        }

        private static Mood CurrentMood()
        {
            if (DateTime.Now < _reactionUntil)
                return _reaction;
            return Fin.PetActivity.Dozing ? Mood.Sleepy : Mood.Happy;
        }

        private static void StartTimer()
        {
            if (_timer == null)
            {
                try
                {
                    _timer = new Eto.Forms.UITimer { Interval = 1 / Fps };
                    _timer.Elapsed += (s, e) =>
                    {
                        // Only the moods that move need a redraw every frame; otherwise only on a change.
                        Mood mood = CurrentMood();
                        if (mood != Mood.Happy || mood != _lastDrawn)
                            Redraw();
                        _lastDrawn = mood;
                    };
                }
                catch
                {
                    return; // no UI (scripted runs)
                }
            }
            _timer.Start();
        }

        private static void Redraw()
        {
            RhinoDoc? doc = RhinoDoc.ActiveDoc;
            if (doc == null)
                return;
            RhinoView? view = _viewport != Guid.Empty ? doc.Views.Find(_viewport) : doc.Views.ActiveView;
            view?.Redraw();
        }

        /// <summary>
        /// Draws the pet and its bubble bottom left of the viewport being drawn. Shared by every game, so the
        /// Gotchi looks and moves the same everywhere.
        /// </summary>
        public static void DrawPet(DrawEventArgs e, Mood mood, string speech)
        {
            float s = Math.Max(1f, (float)e.Display.DpiScale);
            System.Drawing.Rectangle view = e.Viewport.Bounds;
            int margin = (int)(14 * s);
            int size = (int)(110 * s);
            double time = Fin.PetActivity.Time;
            int frame = mood == Mood.Happy ? 0 : (int)(time * Fps) % (int)(2.4 * Fps);
            string outfit = $"{Mascot.WornHat()}.{Mascot.WornClothes()}.{Mascot.WornFace()}";
            var pet = Fin.Hud.Get($"vpet:{mood}:{frame}:{size}:{outfit}", size, size, g =>
            {
                double t = frame / Fps;
                float breathe = (float)(0.5 + 0.5 * Math.Sin(t * 2 * Math.PI / 2.4));
                float hop = mood == Mood.Celebrate ? (float)Math.Abs(Math.Sin(t * Math.PI * 2.5)) * size * 0.12f : 0;
                Mascot.Draw(g, new EtoRect(0, size * 0.12f, size, size * 0.88f), mood, bob: -hop, breathe: breathe);
                return new EtoSize(size, size);
            });
            int petBottom = view.Height - margin;
            if (pet.HasValue)
                e.Display.DrawBitmap(pet.Value.Bitmap, margin, petBottom - pet.Value.Height);

            if (string.IsNullOrEmpty(speech))
                return;
            int maxW = (int)Math.Min(240 * s, Math.Max(120 * s, view.Width - size - 3 * margin));
            var bubble = Fin.Hud.Get($"vbubble:{speech}:{maxW}:{s}", maxW, (int)(160 * s), g => Fin.Cards.Bubble(g, 1, 1, s, speech, maxW - 4 * s));
            if (bubble.HasValue)
                e.Display.DrawBitmap(bubble.Value.Bitmap, margin + size + (int)(4 * s), petBottom - size / 2 - bubble.Value.Height / 2);
        }

        private sealed class Conduit : DisplayConduit
        {
            protected override void DrawForeground(DrawEventArgs e)
            {
                if (_owner == null)
                    return;
                RhinoDoc? doc = RhinoDoc.ActiveDoc;
                Guid target = _viewport != Guid.Empty ? _viewport : doc?.Views.ActiveView?.ActiveViewportID ?? Guid.Empty;
                if (e.Viewport.Id != target)
                    return;
                DrawPet(e, CurrentMood(), CurrentMood() == Mood.Sleepy ? "Zzz... click to wake me." : _speech);
            }
        }
    }
}
