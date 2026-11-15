using System;
using System.Diagnostics;

namespace RhinoGotchi.Fin
{
    /// <summary>
    /// Whether the player is active, for the pet's standby (FinGotchi's rule): a mouse-down in any
    /// Rhino viewport, a key press, or a click in the panel wakes the pet; after
    /// <see cref="StandbySeconds"/> without any of those it dozes off.
    /// </summary>
    internal static class PetActivity
    {
        public const double StandbySeconds = 5;

        private static readonly Stopwatch Idle = Stopwatch.StartNew();
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static Watcher? _watcher;

        /// <summary>Seconds since the panel was created, for the pet's animation loops.</summary>
        public static double Time => Clock.Elapsed.TotalSeconds;

        public static bool Dozing => Idle.Elapsed.TotalSeconds >= StandbySeconds;

        /// <summary>Starts listening to viewport clicks and key presses (once).</summary>
        public static void Start()
        {
            if (_watcher != null)
                return;
            try
            {
                _watcher = new Watcher { Enabled = true };
                Rhino.RhinoApp.KeyboardEvent += key => Touch();
            }
            catch
            {
                // No UI (scripted runs): the pet just never dozes off.
            }
        }

        /// <summary>The player did something: wake the pet.</summary>
        public static void Touch() => Idle.Restart();

        /// <summary>Clicks in any viewport count as activity.</summary>
        private sealed class Watcher : Rhino.UI.MouseCallback
        {
            protected override void OnMouseDown(Rhino.UI.MouseCallbackEventArgs e) => Touch();
        }
    }
}
