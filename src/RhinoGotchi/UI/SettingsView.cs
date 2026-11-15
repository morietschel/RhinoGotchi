using Eto.Forms;
using RhinoGotchi.Core;

namespace RhinoGotchi.UI
{
    /// <summary>Plugin settings. For now: which OpenAI key Deep Cut's judge uses.</summary>
    internal sealed class SettingsView : StackLayout
    {
        private readonly Words _status = Theme.Body("");

        public SettingsView(RhinoGotchiPanel host)
        {
            // Settings opens from the My Gotchi tab, so its back button returns there.
            Theme.SetUpPage(this, host, "Settings", "Deep Cut asks an OpenAI model to judge each guess.",
                "← My Gotchi", host.ShowTrophies);

            var keyBox = new PasswordBox();
            Items.Add(Theme.Heading("OpenAI API key"));
            Items.Add(_status);
            Items.Add(Theme.Muted("Paste your own key if the built-in one stops working. It is saved as plain text in the RhinoGotchi plugin settings on this computer and only sent to api.openai.com."));
            Items.Add(keyBox);
            Items.Add(Theme.Row(
                Theme.Primary("Save my key", () =>
                {
                    if (keyBox.Text.Trim().Length == 0)
                        return;
                    ApiKey.SetOwn(keyBox.Text);
                    keyBox.Text = "";
                    Refresh();
                }),
                Theme.Button("Use the built-in key", () =>
                {
                    ApiKey.SetOwn("");
                    keyBox.Text = "";
                    Refresh();
                })));
            Refresh();
        }

        private void Refresh() =>
            _status.Text = ApiKey.UsingOwn ? $"Using your own key ({ApiKey.Masked(ApiKey.Own)})."
                : ApiKey.HasBundled ? "Using the key built into the plugin."
                : "No key yet. This build has no built-in key, so add your own to play Deep Cut.";
    }
}
