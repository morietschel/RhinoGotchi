using System;
using Eto.Drawing;
using Eto.Forms;
using RhinoGotchi.Core;

namespace RhinoGotchi.UI
{
    /// <summary>Where the player picks the name and icon shown on the leaderboard.</summary>
    internal sealed class ProfileView : StackLayout
    {
        private readonly RhinoGotchiPanel _host;
        private readonly TextBox _name;
        private readonly AvatarView _preview;
        private readonly Words _message = Theme.Muted("");
        private readonly ChunkyButton _save;
        private string _icon = Leaderboard.Icon;

        public ProfileView(RhinoGotchiPanel host)
        {
            _host = host;
            Theme.SetUpPage(this, host, "My profile",
                "Your name and icon are public: everyone sees them on the leaderboard.",
                "← Leaderboard", BackToBoards);

            _name = new TextBox
            {
                Text = Leaderboard.Name,
                MaxLength = LeaderboardClient.MaxNameLength,
                PlaceholderText = "Your name",
            };
            _preview = new AvatarView(Leaderboard.Name, Leaderboard.PlayerId, 64, Avatars.Decode(_icon));
            _name.TextChanged += (s, e) => _preview.PlayerName = _name.Text;

            var remove = Theme.Button("Remove", () => SetIcon(""));
            _save = Theme.Primary("Save", Save);

            Items.Add(Theme.Heading("Name"));
            Items.Add(_name);
            Items.Add(Theme.Heading("Icon"));
            Items.Add(new TableLayout
            {
                Spacing = new Size(12, 0),
                Rows =
                {
                    new TableRow(
                        new TableCell(_preview),
                        new TableCell(new StackLayout
                        {
                            Spacing = 6,
                            HorizontalContentAlignment = HorizontalAlignment.Stretch,
                            Items =
                            {
                                Theme.Muted($"Use Gotchi in today's outfit, or any picture (cropped to a square and shrunk to {Avatars.Pixels}×{Avatars.Pixels} pixels)."),
                                Theme.Row(Theme.Secondary("Use my Gotchi", UseGotchi), Theme.Button("Choose image…", ChooseImage), remove),
                            },
                        }, true)),
                },
            });
            Items.Add(_message);
            Items.Add(new StackLayoutItem(_save, HorizontalAlignment.Left));
        }

        private void BackToBoards() => _host.Navigate(new LeaderboardView(_host));

        private void ChooseImage()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Choose an icon",
                Filters = { new FileFilter("Images", ".png", ".jpg", ".jpeg", ".gif", ".bmp") },
            };
            if (dialog.ShowDialog(this) != DialogResult.Ok)
                return;

            string? data = Avatars.Encode(dialog.FileName);
            if (data == null)
            {
                _message.Text = "That file couldn't be read as a picture. Try a PNG or JPG.";
                return;
            }
            SetIcon(data);
        }

        /// <summary>Draws Gotchi, dressed as it is in the wardrobe, into the icon. Save to upload it.</summary>
        private void UseGotchi()
        {
            string? data = Avatars.EncodeGotchi();
            if (data == null)
            {
                _message.Text = "Couldn't draw Gotchi as an icon. Try a picture instead.";
                return;
            }
            SetIcon(data);
            _message.Text = "Gotchi is your icon (press Save). Change the outfit in Trophies, then come back to update it.";
        }

        private void SetIcon(string data)
        {
            _icon = data;
            _preview.Image = Avatars.Decode(data);
            _message.Text = "";
        }

        private async void Save()
        {
            string name = Leaderboard.CleanName(_name.Text ?? "");
            if (name.Length == 0)
            {
                _message.Text = "Pick a name first.";
                return;
            }

            Leaderboard.SetProfile(name, _icon);
            if (!Leaderboard.IsConfigured)
            {
                BackToBoards();
                return;
            }

            // Wait for the upload so the player sees their own row when the boards open.
            _save.Enabled = false;
            _message.Text = "Saving…";
            bool ok = await Leaderboard.SyncAsync().ConfigureAwait(false);
            Application.Instance.AsyncInvoke(() =>
            {
                if (ok)
                {
                    BackToBoards();
                    return;
                }
                _save.Enabled = true;
                _message.Text = "Saved on this computer, but the leaderboard couldn't be reached. It will be sent next time.";
            });
        }
    }
}
