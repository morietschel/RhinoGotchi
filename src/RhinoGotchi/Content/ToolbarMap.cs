using System.Linq;
using System.Text.RegularExpressions;

namespace RhinoGotchi.Content
{
    /// <summary>
    /// One place a command's button lives: a root toolbar (a tab or the Main sidebar) and its button,
    /// plus optionally a flyout opened from that button and the button inside it.
    /// </summary>
    internal sealed class ToolbarSpot
    {
        public ToolbarSpot(string root, int rootIndex, string? flyout, int flyoutIndex)
        {
            Root = root;
            RootIndex = rootIndex;
            Flyout = flyout;
            FlyoutIndex = flyoutIndex;
        }

        public string Root { get; }
        public int RootIndex { get; }
        public string? Flyout { get; }
        public int FlyoutIndex { get; }
    }

    /// <summary>Where commands sit in Rhino's default toolbars. Data is in ToolbarMap.g.cs (generated).</summary>
    internal static partial class ToolbarMap
    {
        public static ToolbarSpot[] Find(string command) =>
            Locations.TryGetValue(command, out var spots) ? spots : new ToolbarSpot[0];

        public static string[] Buttons(string toolbar) =>
            Toolbars.TryGetValue(toolbar, out var tips) ? tips : new string[0];

        public static string Tip(string toolbar, int index)
        {
            var tips = Buttons(toolbar);
            return index >= 0 && index < tips.Length ? tips[index].Trim() : "";
        }

        /// <summary>Icon files are named after tooltips, with characters Windows can't use replaced by "_".</summary>
        public static string IconFileForTip(string tip) => Regex.Replace(tip.Trim(), "[:/\\\\*?\"<>|]", "_");

        /// <summary>Plain-language directions, e.g. "Transform tab → 1st button (Move)".</summary>
        public static string Describe(ToolbarSpot spot)
        {
            string root = RootLabels.TryGetValue(spot.Root, out var label) ? label : spot.Root;
            string text = $"{root} → {Ordinal(spot.RootIndex + 1)} button ({Tip(spot.Root, spot.RootIndex)})";
            if (spot.Flyout != null)
                text += $". Hold it down (or click its small corner triangle) to open the {spot.Flyout} flyout → " +
                        $"{Ordinal(spot.FlyoutIndex + 1)} button ({Tip(spot.Flyout, spot.FlyoutIndex)})";
            return text + ".";
        }

        private static string Ordinal(int n)
        {
            string suffix = (n % 100) is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
            return n + suffix;
        }

        public static bool IsTab(string root) => root != "Main" && Toolbars.ContainsKey(root);

        public static string[] Roots => RootLabels.Keys.ToArray();
    }
}
