using System;
using System.Collections.Generic;
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
                    Mascot.WornFace = () => game.Progress.EquippedFace;
                    _game = game;
                }
                return _game;
            }
        }

        /// <summary>Achievement stats from lessons plus the daily games.</summary>
        public static AchievementStats StatsFor(Lessons.Progress progress)
        {
            var games = Core.Games.All.Select(Core.Progress.Get).ToList();
            var stats = progress.Stats(games.Sum(s => s.Wins), games.Count == 0 ? 0 : games.Max(s => s.BestStreak));
            stats.OrderWins = Core.Progress.Get(Core.Games.Order).Wins;
            stats.ShapeWins = Core.Progress.Get(Core.Games.Shape).Wins;
            stats.DeepWins = Core.Progress.Get(Core.Games.Deep).Wins;
            stats.BlockedWins = Core.Progress.Get(Core.Games.Blocked).Wins;
            stats.AllDailiesOneDay = AllDailiesWonOnOneDay(games) ? 1 : 0;
            return stats;
        }

        private const string AllDailiesKey = "ach.allDailiesOneDay";

        /// <summary>
        /// True once every daily was won on the same day. Remembered in the plugin settings,
        /// since each game only keeps its last win day.
        /// </summary>
        private static bool AllDailiesWonOnOneDay(List<Core.GameStats> games)
        {
            var settings = RhinoGotchiPlugin.Instance?.Settings;
            if (settings?.GetBool(AllDailiesKey, false) == true)
                return true;
            bool today = games.Count > 0 && games.All(g => g.LastWinDay == Core.Daily.TodayKey);
            if (today && settings != null)
            {
                settings.SetBool(AllDailiesKey, true);
                RhinoGotchiPlugin.Instance?.SaveSettings();
            }
            return today;
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

    /// <summary>
    /// Build a building (Learn Projects): the twelve Klosterneuburg levels without the campaign around them.
    /// Opening it adds the built-in Klosterneuburg model to the document (unless it is already there) and
    /// starts the next level; leaving it ends the level (its geometry is deleted) and removes the model again.
    /// </summary>
    internal sealed class BuildingView : Panel, IGameView, IFillView
    {
        public const string Chapter = "KLOSTERNEUBURG";

        private readonly RhinoGotchiPanel _host;
        private readonly LessonCanvas _canvas;

        public BuildingView(RhinoGotchiPanel host)
        {
            _host = host;
            _canvas = new LessonCanvas(GotchiState.Game)
            {
                ExitHome = host.ShowProjects,
                OpenTrophies = host.ShowTrophies,
                ProjectOnly = true,
            };
            Content = _canvas;
        }

        public void Enter()
        {
            var game = GotchiState.Game;
            game.ProjectExited -= OnExited;
            game.ProjectExited += OnExited;
            _canvas.Subscribe();
            if (game.Project == Chapter && game.Session != null)
                return;
            Rhino.RhinoDoc? doc = Rhino.RhinoDoc.ActiveDoc;
            if (doc == null || !Tutorials.KlosterneuburgModel.Add(doc))
            {
                Application.Instance.AsyncInvoke(_host.ShowProjects);
                return;
            }
            game.StartProject(Chapter);
            if (game.Mode != GameMode.Playing)
                Application.Instance.AsyncInvoke(_host.ShowProjects); // nothing to play: never leave a blank page
        }

        public void Leave()
        {
            var game = GotchiState.Game;
            game.ProjectExited -= OnExited;
            _canvas.Unsubscribe();
            if (game.Project != null)
                game.ExitProject(); // stops the level: its geometry is deleted
            if (Rhino.RhinoDoc.ActiveDoc is Rhino.RhinoDoc doc)
            {
                Tutorials.TutorialWorkspace.RemoveRoot(doc);
                Tutorials.KlosterneuburgModel.Remove(doc);
            }
        }

        private void OnExited() => Application.Instance.AsyncInvoke(_host.ShowProjects);
    }
}
