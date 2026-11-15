using System.Linq;
using Eto.Forms;
using RhinoGotchi.Core;
using RhinoGotchi.Lessons;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// Learn Projects: three projects, each one a small building job taught command by command, in this
    /// order: Build a road, Build a facade, Build a building. Each one cleans up the geometry it made.
    /// Each project is a painted card; a finished one gets a check. There are no PLAY tags.
    /// </summary>
    internal sealed class ProjectsView : StackLayout, INavPage
    {
        public ProjectsView(RhinoGotchiPanel host)
        {
            Theme.SetUpPage(this, host, "Learn Projects", "");

            // Painted cards in the Games tab's two-column grid, in this order. A finished project gets a check.
            string chapter = BuildingView.Chapter;
            int levels = LessonCatalog.All.Count(l => l.Chapter == chapter);
            int built = GotchiState.Game.Progress.ChapterDone(chapter);
            var tiles = new[]
            {
                // Build a road (Rhino on the Street): the street, then the archway, in one viewport.
                new GameTile("card-build-a-road", "Build a road", () => host.Navigate(new RoadView(host))),
                // Build a facade (Fin Facade): six levels, one box wrapped in fins.
                new GameTile("card-build-a-facade", "Build a facade", () => host.Navigate(new FinFacadeView(host)))
                {
                    Done = GotchiFins.FinSession.EverCompleted,
                },
                // Build a building: twelve commands, one real building.
                new GameTile("card-build-a-building", "Build a building", host.ShowBuilding)
                {
                    Done = levels > 0 && built >= levels,
                },
            };
            Items.Add(new GameGrid(tiles));
        }

        public NavTab Tab => NavTab.Home;
    }
}
