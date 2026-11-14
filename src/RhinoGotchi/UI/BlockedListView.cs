using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using RhinoGotchi.Content;
using RhinoGotchi.Core;

namespace RhinoGotchi.UI
{
    /// <summary>Every Blocked! challenge with how many of its ways you've found. Tap one to play it.</summary>
    internal sealed class BlockedListView : StackLayout
    {
        public BlockedListView(RhinoGotchiPanel host)
        {
            Theme.SetUpPage(this, host, "All challenges & ways",
                "Each challenge can be solved many ways. Collect them all!");
            var p = Palette.Current;

            int totalKnown = BlockedChallenges.All.Sum(c => c.Ways.Length);
            int totalFound = BlockedChallenges.All.Sum(c => c.Ways.Count(w => Progress.WaysFound(c.Id).Contains(w.Id)));
            int totalCustom = BlockedChallenges.All.Sum(c => Progress.WaysFound(c.Id).Count(f => f.StartsWith("custom:")));
            Items.Add(Theme.Box(new StackLayout
            {
                Spacing = 6,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Items =
                {
                    Theme.Heading($"{totalFound} of {totalKnown} ways found" + (totalCustom > 0 ? $" · +{totalCustom} of your own" : "")),
                    new ProgressBar(totalKnown == 0 ? 0 : (double)totalFound / totalKnown, p.Green),
                },
            }));

            foreach (var challenge in BlockedChallenges.All)
            {
                var c = challenge;
                var found = Progress.WaysFound(c.Id);
                int foundKnown = c.Ways.Count(w => found.Contains(w.Id));
                int custom = found.Count(f => f.StartsWith("custom:"));

                var pills = Theme.Wrap(c.Blocked.Take(2).Select(b => (Control)new Pill(b, p.Red, Glyph.Ban))
                    .Concat(c.Blocked.Length > 2 ? new Control[] { new Pill($"+{c.Blocked.Length - 2}", p.Red, Glyph.Ban) } : new Control[0])
                    .ToArray());
                var play = Theme.Primary(foundKnown == 0 ? "Play" : "Find more ways", () => host.Navigate(new BlockedView(host, practice: true, c.Id)));
                play.Fill = true;

                Items.Add(Theme.Box(new StackLayout
                {
                    Spacing = 8,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Items =
                    {
                        new TableLayout
                        {
                            Spacing = new Size(10, 0),
                            Rows = { new TableRow(new Badge(Glyph.Ban, p.Red, 40), new TableCell(new StackLayout
                            {
                                Spacing = 2,
                                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                                Items =
                                {
                                    Theme.Heading(c.Title),
                                    Theme.Muted($"{foundKnown} of {c.Ways.Length} ways" + (custom > 0 ? $" · +{custom} of your own" : "")),
                                },
                            }, true)) },
                        },
                        new ProgressBar(c.Ways.Length == 0 ? 0 : (double)foundKnown / c.Ways.Length, p.Green),
                        pills,
                        play,
                    },
                }, padding: 12));
            }
        }
    }
}
