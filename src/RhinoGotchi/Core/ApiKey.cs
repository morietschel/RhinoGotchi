namespace RhinoGotchi.Core
{
    /// <summary>
    /// The OpenAI key Deep Cut's judge uses: the player's own key from Settings if they saved one,
    /// otherwise the key built into the plugin. That key is committed on purpose: it is short-lived, and
    /// when it expires the game asks the player for their own.
    /// </summary>
    internal static class ApiKey
    {
        private const string SettingName = "openai.key";

        private const string Bundled =
            "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";

        public static string Own => RhinoGotchiPlugin.Instance?.Settings.GetString(SettingName, "") ?? "";

        public static bool UsingOwn => Own.Length > 0;

        public static bool HasBundled => Bundled.Length > 0;

        public static string Current => UsingOwn ? Own : Bundled;

        /// <summary>Saves the player's own key. An empty key goes back to the built-in one.</summary>
        public static void SetOwn(string key)
        {
            var plugin = RhinoGotchiPlugin.Instance;
            if (plugin == null)
                return;
            key = key.Trim();
            if (key.Length == 0)
                plugin.Settings.DeleteItem(SettingName);
            else
                plugin.Settings.SetString(SettingName, key);
            plugin.SaveSettings();
        }

        /// <summary>"sk-…abcd", for showing which key is saved without showing the key.</summary>
        public static string Masked(string key) =>
            key.Length <= 8 ? "…" : key.Substring(0, 3) + "…" + key.Substring(key.Length - 4);
    }
}
