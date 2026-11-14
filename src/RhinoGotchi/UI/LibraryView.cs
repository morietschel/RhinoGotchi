using System;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using RhinoGotchi.Content;

namespace RhinoGotchi.UI
{
    /// <summary>Browse Curves, Solids and Transform commands. Click one to see how it works.</summary>
    internal sealed class LibraryView : StackLayout
    {
        private readonly RhinoGotchiPanel _host;
        private readonly Panel _tabs = new Panel();
        private readonly ChunkyButton[] _tabButtons;
        private int _tab;
        private readonly TextBox _search = new TextBox { PlaceholderText = "Search commands" };
        private readonly CheckBox _demosOnly = new CheckBox { Text = "Only commands with a demo" };
        private readonly Label _blurb = Theme.Muted("");
        private readonly Label _count = Theme.Muted("");
        private readonly StackLayout _list = new StackLayout { Spacing = 6, HorizontalContentAlignment = HorizontalAlignment.Stretch };

        public LibraryView(RhinoGotchiPanel host)
        {
            _host = host;
            Theme.SetUpPage(this, host, "Command Library",
                "Pick a command to see how it works, step by step in the viewport, then try it yourself. ▶ marks commands with a demo.");

            var names = CommandCatalog.Categories.Select(c => c.Name).Append(RecipesTab).ToArray();
            _tabButtons = names.Select((name, i) =>
            {
                var tab = new ChunkyButton(name, ButtonKind.Outline, 32);
                tab.Click += (s, e) => { _tab = i; Refresh(); };
                return tab;
            }).ToArray();
            _tabs.Content = Theme.Grid(4, _tabButtons.ToArray<Control>());
            _search.TextChanged += (s, e) => Refresh();
            _demosOnly.CheckedChanged += (s, e) => Refresh();

            Items.Add(_tabs);
            Items.Add(_blurb);
            Items.Add(_search);
            Items.Add(_demosOnly);
            Items.Add(_count);
            Items.Add(_list);
            Refresh();
        }

        private const string RecipesTab = "Recipes";

        private void Refresh()
        {
            string query = _search.Text.Trim();
            for (int i = 0; i < _tabButtons.Length; i++)
                _tabButtons[i].Kind = i == _tab ? ButtonKind.Selected : ButtonKind.Outline;
            bool recipes = _tab >= CommandCatalog.Categories.Count;
            _demosOnly.Visible = !recipes;
            if (recipes)
            {
                ShowRecipes(query);
                return;
            }

            var category = CommandCatalog.Categories[_tab];
            var shown = category.Commands
                .Where(e => query.Length == 0
                            || e.Command.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                            || e.Description.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                .Where(e => _demosOnly.Checked != true || e.HasDemo)
                .ToList();

            _blurb.Text = category.Blurb;
            int demos = category.Commands.Count(e => e.HasDemo);
            _count.Text = $"{shown.Count} of {category.Commands.Count} commands · {demos} with a demo";

            _list.Items.Clear();
            foreach (var entry in shown)
                _list.Items.Add(Row(entry));
        }

        private void ShowRecipes(string query)
        {
            var shown = Recipes.All
                .Where(r => query.Length == 0
                            || r.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                            || r.ChainText.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
            _blurb.Text = "How commands string together: each recipe chains Curves, Solids and Transform commands to build something.";
            _count.Text = $"{shown.Count} of {Recipes.All.Length} recipes";
            _list.Items.Clear();
            foreach (var recipe in shown)
            {
                var r = recipe;
                var title = Theme.Heading(r.Title);
                var chain = new Label { Text = r.ChainText, TextColor = Palette.Current.GreenText, Font = new Font(SystemFont.Bold, 9) };
                var summary = Theme.Muted(r.Summary);
                var stack = new StackLayout
                {
                    Spacing = 1,
                    Padding = new Padding(4, 4),
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Items = { title, chain, summary },
                };
                var row = new StyledCard(stack, padding: 8) { Hoverable = true };
                EventHandler<MouseEventArgs> open = (s, e) => _host.Navigate(new RecipeView(_host, r, this));
                foreach (var control in new Control[] { row, stack, title, chain, summary })
                    control.MouseDown += open;
                _list.Items.Add(row);
            }
        }

        private Control Row(CommandEntry entry)
        {
            var icon = new ImageView { Image = CommandIcons.Get(entry, 24), Size = new Size(24, 24) };
            var name = Theme.Heading(entry.Command);
            var description = Theme.Muted(entry.Description);
            var demo = new Label { Text = entry.HasDemo ? "▶ demo" : "", TextColor = Palette.Current.GreenText, Font = new Font(SystemFont.Bold, 9) };

            var text = new StackLayout { Spacing = 1, HorizontalContentAlignment = HorizontalAlignment.Stretch, Items = { name, description } };
            var layout = new TableLayout
            {
                Spacing = new Size(8, 0),
                Padding = new Padding(4, 4),
                Rows = { new TableRow(new TableCell(icon), new TableCell(text, true), new TableCell(demo)) },
            };
            var row = new StyledCard(layout, padding: 6) { Hoverable = true };

            EventHandler<MouseEventArgs> open = (s, e) => _host.Navigate(new CommandView(_host, entry, this));
            foreach (var control in new Control[] { row, layout, icon, name, description, demo, text })
                control.MouseDown += open;
            return row;
        }
    }
}
