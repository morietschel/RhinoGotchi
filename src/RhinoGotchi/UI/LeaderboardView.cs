using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using RhinoGotchi.Core;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// The shared leaderboard: one tab for the total across all games, then one per game.
    /// Rows load from the server; player icons are fetched after the rows and cached.
    /// </summary>
    internal sealed class LeaderboardView : StackLayout
    {
        private const int TopCount = 25;

        /// <summary>Remembered so coming back from the profile page returns to the same board.</summary>
        private static int _lastTab;

        private readonly RhinoGotchiPanel _host;
        private readonly string[] _boards = new[] { LeaderboardClient.GlobalBoard }.Concat(Games.All).ToArray();
        private readonly Panel _list = new Panel();
        private int _loadId;

        public LeaderboardView(RhinoGotchiPanel host)
        {
            _host = host;
            Theme.SetUpPage(this, host, "Leaderboard",
                "Your best score in each game. Global adds them up.");

            if (!Leaderboard.IsConfigured)
            {
                Items.Add(Theme.Card(Theme.Tile,
                    Theme.Heading("Not online yet"),
                    Theme.Body($"This build isn't connected to a leaderboard. Your best scores are saved on this computer and will show up once it is.")));
                return;
            }

            var p = Palette.Current;
            var glyphs = new Dictionary<string, (Glyph, Color)>
            {
                [LeaderboardClient.GlobalBoard] = (Glyph.Trophy, p.Gold),
                [Games.Order] = (Glyph.Blocks, p.Orange),
                [Games.Shape] = (Glyph.Eye, p.Blue),
                [Games.Deep] = (Glyph.Cube, p.Purple),
            };
            if (_lastTab >= _boards.Length)
                _lastTab = 0;
            var tabs = new TabBar(_lastTab, _boards.Select(b =>
            {
                var (glyph, color) = glyphs.TryGetValue(b, out var g) ? g : (Glyph.Star, p.Purple);
                return (b == LeaderboardClient.GlobalBoard ? "Global" : Games.ShortTitle(b), glyph, color);
            }).ToArray());
            tabs.SelectedChanged += (s, e) => LoadBoard(tabs.Selected);

            Items.Add(ProfileCard());
            Items.Add(tabs);
            Items.Add(_list);
            Items.Add(new StackLayoutItem(Theme.Button("Refresh", () => LoadBoard(_lastTab)), HorizontalAlignment.Left));
            LoadBoard(_lastTab);
        }

        /// <summary>The "New best" line shown when a round beats the player's record, with a way to the boards.</summary>
        public static Control NewBestRow(RhinoGotchiPanel host, int score) => Theme.Row(
            new Pill($"New best: {score:N0}", Palette.Current.Gold, Glyph.Trophy),
            Theme.Button(Leaderboard.HasProfile ? "Leaderboard" : "Join the leaderboard", () => host.Navigate(new LeaderboardView(host))));

        private Control ProfileCard()
        {
            var p = Palette.Current;
            if (!Leaderboard.HasProfile)
            {
                return Theme.Card(p.Gold,
                    Theme.Heading("Join the leaderboard"),
                    Theme.Body($"Pick a name and an icon to appear here. Your best scores add up to {Leaderboard.Total:N0}."),
                    Theme.Primary("Set up my profile", EditProfile));
            }

            var avatar = new AvatarView(Leaderboard.Name, Leaderboard.PlayerId, 40, Avatars.Decode(Leaderboard.Icon));
            return Theme.Box(new TableLayout
            {
                Spacing = new Size(10, 0),
                Rows =
                {
                    new TableRow(
                        new TableCell(Centered(avatar)),
                        new TableCell(new StackLayout
                        {
                            Spacing = 4,
                            HorizontalContentAlignment = HorizontalAlignment.Stretch,
                            Items =
                            {
                                Theme.Heading(Leaderboard.Name),
                                new StackLayoutItem(new Pill($"{Leaderboard.Total:N0} points", p.Gold, Glyph.Trophy), HorizontalAlignment.Left),
                            },
                        }, true),
                        new TableCell(Centered(Theme.Button("Edit", EditProfile)))),
                },
            });
        }

        private void EditProfile() => _host.Navigate(new ProfileView(_host));

        private static Control Centered(Control control) => new StackLayout
        {
            Orientation = Orientation.Horizontal,
            VerticalContentAlignment = VerticalAlignment.Center,
            Items = { control },
        };

        private async void LoadBoard(int tab)
        {
            _lastTab = tab;
            int loadId = ++_loadId;
            string board = _boards[tab];
            var client = Leaderboard.Client;
            if (client == null)
                return;

            _list.Content = Theme.Muted("Loading…");
            List<BoardEntry>? entries = null;
            try
            {
                entries = await client.GetTopAsync(board, TopCount).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Shown below as a friendly message.
            }

            Application.Instance.AsyncInvoke(() =>
            {
                if (loadId != _loadId)
                    return; // a newer load replaced this one
                _list.Content = entries == null
                    ? Theme.Card(Palette.Current.Red,
                        Theme.Heading("Couldn't load the leaderboard"),
                        Theme.Body("Check your internet connection, then press Refresh."))
                    : Rows(board, entries);
            });
        }

        private Control Rows(string board, List<BoardEntry> entries)
        {
            var p = Palette.Current;
            string me = Leaderboard.PlayerId;
            var stack = new StackLayout { Spacing = 8, HorizontalContentAlignment = HorizontalAlignment.Stretch };

            if (entries.Count == 0)
            {
                stack.Items.Add(Theme.Card(Theme.Tile,
                    Theme.Heading("No scores yet"),
                    Theme.Body("Play today's game and be the first on this board!")));
                return stack;
            }

            var table = new TableLayout { Spacing = new Size(8, 8) };
            var waiting = new List<(BoardEntry Entry, AvatarView View)>();
            int rank = 0, shown = 0, previous = int.MinValue;
            foreach (var entry in entries)
            {
                // Equal scores share a rank.
                shown++;
                if (entry.Score != previous)
                    rank = shown;
                previous = entry.Score;

                bool mine = entry.PlayerId == me;
                var avatar = new AvatarView(entry.Name, entry.PlayerId, 30);
                if (mine)
                    avatar.Image = Avatars.Decode(Leaderboard.Icon);
                else if (entry.IconVersion.Length > 0)
                {
                    if (Avatars.TryGetCached(entry.PlayerId, entry.IconVersion, out var cached))
                        avatar.Image = cached;
                    else
                        waiting.Add((entry, avatar));
                }

                var rankColor = rank == 1 ? p.Gold : rank <= 3 ? p.Orange : p.SubtleText;
                table.Rows.Add(new TableRow(
                    new TableCell(Centered(new Label { Text = rank.ToString(), Font = Theme.HeadingFont, TextColor = rankColor, Width = 24, TextAlignment = TextAlignment.Center })),
                    new TableCell(avatar),
                    new TableCell(Centered(new Label
                    {
                        Text = mine ? entry.Name + " (you)" : entry.Name,
                        Font = mine ? Theme.HeadingFont : Theme.BodyFont,
                        TextColor = mine ? p.Blue : p.Text,
                    }), true),
                    new TableCell(Centered(new Label { Text = entry.Score.ToString("N0"), Font = Theme.HeadingFont, TextColor = p.Text }))));
            }
            stack.Items.Add(Theme.Box(table, padding: 12));

            if (Leaderboard.HasProfile && entries.All(e => e.PlayerId != me))
            {
                int mine = board == LeaderboardClient.GlobalBoard ? Leaderboard.Total : Leaderboard.Best(board);
                stack.Items.Add(Theme.Muted(mine > 0
                    ? $"You have {mine:N0} points here. Keep playing to reach the top {TopCount}!"
                    : "You have no points on this board yet."));
            }

            foreach (var (entry, view) in waiting)
                LoadIcon(entry, view);
            return stack;
        }

        private static async void LoadIcon(BoardEntry entry, AvatarView view)
        {
            var client = Leaderboard.Client;
            if (client == null)
                return;

            string? data = null;
            try
            {
                data = await client.GetIconAsync(entry.PlayerId).ConfigureAwait(false);
            }
            catch (Exception)
            {
                return; // keep the letter icon
            }

            Application.Instance.AsyncInvoke(() =>
            {
                var image = Avatars.Decode(data ?? "");
                Avatars.Store(entry.PlayerId, entry.IconVersion, image);
                if (image != null && !view.IsDisposed)
                    view.Image = image;
            });
        }
    }
}
