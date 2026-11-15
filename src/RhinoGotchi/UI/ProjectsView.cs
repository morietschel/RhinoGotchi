using System.Linq;
using Eto.Forms;
using RhinoGotchi.Core;
using RhinoGotchi.Lessons;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// Learn Projects: three projects, each one a small building job taught command by command, in this
    /// order: Build a road, Build a facade, Build a building. Each one cleans up the geometry it made.
    /// A row shows progress once a project is started; there are no PLAY tags.
    /// </summary>
    internal sealed class ProjectsView : StackLayout, INavPage
    {
        public ProjectsView(RhinoGotchiPanel host)
        {
            Theme.SetUpPage(this, host, "Learn Projects", "");

            // Build a road (Rhino on the Street): the street, then the archway, in one viewport.
            Items.Add(new ListRow("Build a road", "Street, then archway", Glyph.Curve, 0,
                () => RhinoActions.Run(RhinoOnTheStreetCommand.Name)));

            // Build a facade (Fin Facade): six levels, one box wrapped in fins.
            Items.Add(new ListRow("Build a facade", "6 levels, one facade", Glyph.Chart, 4,
                () => host.Navigate(new FinFacadeView(host)))
            {
                TagText = GotchiFins.FinSession.EverCompleted ? "DONE" : "",
                TagDone = GotchiFins.FinSession.EverCompleted,
            });

            // Build a building (Klosterneuburg): twelve commands, one real building.
            string chapter = BuildingView.Chapter;
            int levels = LessonCatalog.All.Count(l => l.Chapter == chapter);
            int built = GotchiState.Game.Progress.ChapterDone(chapter);
            Items.Add(new ListRow("Build a building", "12 levels, one building", Glyph.Blocks, 3, host.ShowBuilding)
            {
                TagText = built >= levels && levels > 0 ? "DONE" : built == 0 ? "" : $"{built}/{levels}",
                TagDone = built >= levels && levels > 0,
            });
        }

        public NavTab Tab => NavTab.Home;
    }
}
