using System;
using Eto.Drawing;
using Eto.Forms;
using RhinoGotchi.Core;

namespace RhinoGotchi.UI
{
    /// <summary>Where the player picks the name shown on the leaderboard. The icon is their Gotchi, as it looks now.</summary>
    internal sealed class ProfileView : StackLayout
    {
        private readonly RhinoGotchiPanel _host;
        private readonly TextBox _name;
        private readonly AvatarView _preview;
        private readonly Words _message = Theme.Muted("");
        private readonly ChunkyButton _save;
        private readonly string _icon = Avatars.Snapshot();

        public ProfileView(RhinoGotchiPanel host)
        {
            _host = host;
            Theme.SetUpPage(this, host, "My profile",
                "Your name and your Gotchi are public.",
                "← Leaderboard", BackToBoards);

            _name = new TextBox
            {
                Text = Leaderboard.Name,
                MaxLength = LeaderboardClient.MaxNameLength,
                PlaceholderText = "Your name",
            };
            _preview = new AvatarView(Leaderboard.Name, Leaderboard.PlayerId, 64, Avatars.Decode(_icon));
            _name.TextChanged += (s, e) => _preview.PlayerName = _name.Text;

            _save = Theme.Primary("Save", Save);

            Items.Add(new StackLayoutItem(_preview, HorizontalAlignment.Center));
            Items.Add(Theme.Heading("Name"));
            Items.Add(_name);
            Items.Add(_message);
            Items.Add(new StackLayoutItem(_save, HorizontalAlignment.Left));
        }

        private void BackToBoards() => _host.Navigate(new LeaderboardView(_host));

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
