using System;
using System.Linq;
using Eto.Forms;
using Rhino;
using RhinoGotchi.Lessons;
using RhinoGotchi.Tutorials;

namespace RhinoGotchi.UI
{
    /// <summary>
    /// Klosterneuburg Tutorials, a separate game on the Learning tab (not part of the campaign map):
    /// twelve tutorials on a real building, easiest first. Tap one to play it as a short lesson (a quiz
    /// card, the core card, then the challenge card) on the same card screen as the campaign; leaving
    /// the lesson comes back to this list.
    /// </summary>
    internal sealed class KlosterneuburgView : Panel, IGameView, IFillView
    {
        private readonly RhinoGotchiPanel _host;
        private readonly GotchiGame _game = GotchiState.Game;
        private LessonCanvas? _canvas;
        private bool _playing;

        public KlosterneuburgView(RhinoGotchiPanel host)
        {
            _host = host;
            ShowList();
        }

        public void Enter() => _game.Changed += OnGameChanged;

        public void Leave()
        {
            _game.Changed -= OnGameChanged;
            _canvas?.Unsubscribe();
            if (_playing)
                _game.BackToMap(); // stops a running card
        }

        // ---------------- the list ----------------

        private void ShowList()
        {
            _playing = false;
            _canvas?.Unsubscribe();
            _canvas = null;

            var page = new StackLayout();
            Theme.SetUpPage(page, _host, "Klosterneuburg Tutorials",
                "Twelve Rhino commands on a real building, easiest first. Each one is a quick quiz, a guided card with a RUN button, and a challenge.");

            if (ReferenceModel.TryOpen(RhinoDoc.ActiveDoc, out string _) == null)
                page.Items.Add(Theme.Card(Palette.Current.Gold,
                    Theme.Heading("Open the Klosterneuburg model"),
                    Theme.Body("The tutorials use V18 P020 - Klosterneuburg_Final.3dm. Without it each card sets up a simplified stand-in.")));

            int cores = TutorialCatalog.Cores.Count(t => TutorialProgress.Get(t.Id).Completed);
            int challenges = TutorialCatalog.All.Count(t => t.IsChallenge && TutorialProgress.Get(t.Id).Completed);
            page.Items.Add(Theme.Muted($"{cores} of {TutorialCatalog.Count} done · {challenges} challenges done"));

            string[] parts = { "DETAILS", "STRUCTURE", "SURFACES" };
            string[] notes = { "plate, handrail, glass, setting-out", "balustrade, slices, ribs, edge beam", "ramp, joists, the wavy roof" };
            for (int part = 0; part < 3; part++)
            {
                page.Items.Add(new SectionLabel(parts[part], notes[part]));
                foreach (Tutorial core in TutorialCatalog.Cores.Where(t => (t.Number - 1) / 4 == part))
                    page.Items.Add(Row(core));
            }

            Content = page;
        }

        private Control Row(Tutorial core)
        {
            Tutorial? challenge = TutorialCatalog.ChallengeOf(core);
            bool coreDone = TutorialProgress.Get(core.Id).Completed;
            bool challengeDone = challenge != null && TutorialProgress.Get(challenge.Id).Completed;
            string status = challengeDone ? "✓✓" : coreDone ? "✓" : "";
            var row = new ListRow($"{core.Number} · {core.Title}", $"{core.MainCommand} · {core.Element}",
                coreDone ? Glyph.Check : Glyph.Star, core.Number % 6, () => Play(LessonCatalog.TutorialLesson(core)))
            {
                Emphasis = !coreDone,
                Right = status,
                RightAccent = true,
            };
            return row;
        }

        // ---------------- playing ----------------

        private void Play(Lesson lesson)
        {
            _canvas = new LessonCanvas(_game)
            {
                ExitHome = _host.ShowHome,
                OpenTrophies = _host.ShowTrophies,
            };
            Content = _canvas;
            _canvas.Subscribe();
            _playing = true;
            _game.PlayProject(lesson);
        }

        // Leaving the lesson (BACK, the close button, CONTINUE at the end) ends the session: show the list again.
        private void OnGameChanged()
        {
            if (_playing && !_game.PlayingProject)
                Application.Instance.AsyncInvoke(ShowList);
        }
    }
}
