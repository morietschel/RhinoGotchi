using System;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using RhinoGotchi.Content;
using RhinoGotchi.Tutorials;

namespace RhinoGotchi.UI
{
    /// <summary>The Klosterneuburg tutorials, grouped by part, with each one's status. Click one to open it.</summary>
    internal sealed class TutorialListView : StackLayout, IGameView
    {
        private readonly RhinoGotchiPanel _host;
        private readonly StackLayout _list = new StackLayout { Spacing = 2, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        private readonly Panel _banner = new Panel();

        public TutorialListView(RhinoGotchiPanel host)
        {
            _host = host;
            Theme.SetUpPage(this, host, "Tutorials",
                "Model parts of the Klosterneuburg pavilion, one command at a time. Your work goes on its own layers; the original model is never changed.");
            Items.Add(_banner);
            Items.Add(_list);
            Refresh();
        }

        public void Enter() => Refresh();

        public void Leave() { }

        private void Refresh()
        {
            _banner.Content = TutorialUi.ModelBanner(RhinoDoc.ActiveDoc);

            _list.Items.Clear();
            foreach (var part in TutorialCatalog.ByPart())
            {
                _list.Items.Add(new StackLayoutItem(Theme.Heading($"Part {part.Key} · {TutorialCatalog.PartTitle(part.Key)}")));
                foreach (Tutorial t in part)
                    _list.Items.Add(Row(t));
            }
        }

        private Control Row(Tutorial tutorial)
        {
            var icon = new ImageView { Image = TutorialUi.Icon(tutorial.MainCommand, 24), Size = new Size(24, 24) };
            var name = new Label { Text = $"{tutorial.Number:00}  {tutorial.Title}", Font = Theme.HeadingFont };
            var detail = Theme.Muted($"{tutorial.MainCommand} · {tutorial.Element}");
            var status = TutorialUi.StatusChip(tutorial);

            var text = new StackLayout { Spacing = 1, HorizontalContentAlignment = HorizontalAlignment.Stretch, Items = { name, detail } };
            var layout = new TableLayout
            {
                Spacing = new Size(8, 0),
                Padding = new Padding(4, 4),
                Rows = { new TableRow(new TableCell(icon), new TableCell(text, true), new TableCell(status)) },
            };
            var row = new Panel { Content = layout };

            EventHandler<MouseEventArgs> open = (s, e) => _host.Navigate(new TutorialView(_host, tutorial, this));
            foreach (var control in new Control[] { row, layout, icon, name, detail, text, status })
                control.MouseDown += open;
            return row;
        }
    }

    /// <summary>Small pieces shared by the tutorial views.</summary>
    internal static class TutorialUi
    {
        public static Image? Icon(string command, int size)
        {
            CommandEntry? entry = CommandCatalog.Find(command);
            return entry != null ? CommandIcons.Get(entry, size) : CommandIcons.GetFile(command, size);
        }

        /// <summary>Not started (gray), In progress (gold) or Done with the best score (teal).</summary>
        public static Panel StatusChip(Tutorial tutorial)
        {
            TutorialStatus status = TutorialProgress.Get(tutorial.Id);
            if (status.Completed)
                return Theme.Chip($"Done {status.BestScore:P0}", Theme.Hit, Theme.Light, 84, 22);
            if (status.Started)
                return Theme.Chip("In progress", Theme.Near, Theme.TileText, 84, 22);
            return Theme.Chip("Not started", Theme.Tile, Theme.TileText, 84, 22);
        }

        /// <summary>A notice when the Klosterneuburg model isn't open, or null when it is.</summary>
        public static Control? ModelBanner(RhinoDoc? doc)
        {
            if (ReferenceModel.TryOpen(doc, out string reason) != null)
                return null;
            return Theme.Card(Theme.Near,
                new Label { Text = "Open the Klosterneuburg model for the full tutorial", Font = Theme.HeadingFont },
                Theme.Body(reason + " Set up still works without it: you get a simplified standalone version."));
        }

        /// <summary>True when the text is still the catalog's placeholder.</summary>
        public static bool IsTodo(string? text) =>
            string.IsNullOrWhiteSpace(text) || string.Equals(text, TutorialCatalog.Todo, StringComparison.Ordinal);
    }
}
