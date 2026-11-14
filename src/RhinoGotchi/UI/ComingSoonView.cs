using Eto.Drawing;
using Eto.Forms;
using RhinoGotchi.Content;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// Placeholder page for a mode from the RhinoGotchi canvas that isn't built yet:
    /// what it is, how it will play, and which hat it will unlock.
    /// </summary>
    internal sealed class ComingSoonView : StackLayout
    {
        public ComingSoonView(RhinoGotchiPanel host, UpcomingGame game)
        {
            Theme.SetUpPage(this, host, game.Title, game.Blurb);
            var p = Palette.Current;

            Items.Add(new SleepingPet { Height = 150 });
            Items.Add(new TipCard("COMING SOON", "This game isn't built yet. Gotchi is saving a spot for it."));
            Items.Add(new SectionLabel("HOW IT WILL PLAY"));
            for (int i = 0; i < game.HowToPlay.Length; i++)
                Items.Add(new Label
                {
                    Text = $"{i + 1}.  {game.HowToPlay[i]}",
                    Wrap = WrapMode.Word,
                    Font = new Font(SystemFont.Default, 10.5f),
                    TextColor = p.Text,
                });
            if (game.Unlocks.Length > 0)
            {
                Items.Add(new SectionLabel("WILL UNLOCK"));
                Items.Add(Theme.Muted(game.Unlocks));
            }
        }

        /// <summary>Gotchi asleep, waiting for the game.</summary>
        private sealed class SleepingPet : Drawable
        {
            public SleepingPet()
            {
                SizeChanged += (s, e) => Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                float size = System.Math.Min(Height, Width);
                Mascot.Draw(e.Graphics, new RectangleF((Width - size) / 2, 0, size, size), Mood.Sleepy);
            }
        }
    }
}
