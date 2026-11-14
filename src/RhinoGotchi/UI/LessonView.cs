using System;
using System.Linq;
using Eto.Forms;
using RhinoGotchi.Lessons;

namespace RhinoGotchi.UI
{
    /// <summary>The one lesson game shared by every RhinoGotchi panel (Rhino on Mac makes one panel per window).</summary>
    internal static class GotchiState
    {
        private static GotchiGame? _game;

        public static GotchiGame Game
        {
            get
            {
                if (_game == null)
                {
                    var game = new GotchiGame(Lessons.Progress.Load());
                    game.StatsProvider = () => StatsFor(game.Progress);
                    Mascot.WornHat = () => game.Progress.EquippedHat;
                    Mascot.WornClothes = () => game.Progress.EquippedClothes;
                    _game = game;
                }
                return _game;
            }
        }

        /// <summary>Achievement stats from lessons plus the daily games.</summary>
        public static AchievementStats StatsFor(Lessons.Progress progress)
        {
            var games = Core.Games.All.Select(Core.Progress.Get).ToList();
            return progress.Stats(games.Sum(s => s.Wins), games.Count == 0 ? 0 : games.Max(s => s.BestStreak));
        }

        /// <summary>Current streak across lessons and daily games.</summary>
        public static int Streak =>
            Math.Max(Game.Progress.Streak, Core.Games.All.Select(g => Core.Progress.Get(g).CurrentStreak).DefaultIfEmpty(0).Max());

        /// <summary>True if the player learned or played anything today.</summary>
        public static bool ActiveToday =>
            Game.Progress.ActiveToday || Core.Games.All.Any(g => Core.Progress.Get(g).PlayedToday);
    }

    /// <summary>A view that fills the panel's height instead of scrolling to its own size.</summary>
    internal interface IFillView
    {
    }

    /// <summary>The Learning Campaign: welcome, level map, lessons and the placement test.</summary>
    internal sealed class LessonView : Panel, IGameView, IFillView
    {
        private readonly LessonCanvas _canvas;

        public LessonView(RhinoGotchiPanel host)
        {
            _canvas = new LessonCanvas(GotchiState.Game)
            {
                ExitHome = host.ShowHome,
                OpenTrophies = host.ShowTrophies,
            };
            Content = _canvas;
        }

        public void Enter() => _canvas.Subscribe();

        public void Leave() => _canvas.Unsubscribe();
    }
}
