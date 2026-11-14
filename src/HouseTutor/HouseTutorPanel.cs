using System.Runtime.InteropServices;
using Eto.Drawing;
using Eto.Forms;
using Rhino;

namespace HouseTutor
{
    /// <summary>
    /// Dockable panel that walks the user through building a house from a box
    /// and a pyramid, one step at a time. Built with Eto so it works on Windows and Mac.
    /// </summary>
    [Guid("af6a3f86-87cf-4cfe-a9aa-abb01bd0ff70")]
    public class HouseTutorPanel : Panel
    {
        private readonly TutorialStep[] _steps = TutorialStep.House;
        private int _index;

        private readonly Label _progress = new Label { TextColor = Colors.Gray };
        private readonly Label _title = new Label { Font = SystemFonts.Bold(SystemFonts.Default().Size + 3) };
        private readonly Label _body = new Label { Wrap = WrapMode.Word };
        private readonly Button _action = new Button();
        private readonly Button _back = new Button { Text = "< Back" };
        private readonly Button _next = new Button { Text = "Next >" };

        public HouseTutorPanel()
        {
            _action.Click += (s, e) => RunStepMacro();
            _back.Click += (s, e) => ShowStep(_index - 1);
            _next.Click += (s, e) => ShowStep(_index + 1);

            var nav = new TableLayout
            {
                Spacing = new Size(6, 0),
                Rows = { new TableRow(new TableCell(_back), new TableCell(null, true), new TableCell(_next)) },
            };

            Content = new TableLayout
            {
                Padding = new Padding(10),
                Spacing = new Size(0, 8),
                Rows =
                {
                    _progress,
                    _title,
                    new TableRow(new Scrollable { Content = _body, Border = BorderType.None }) { ScaleHeight = true },
                    _action,
                    nav,
                },
            };

            ShowStep(0);
        }

        private void ShowStep(int index)
        {
            if (index < 0 || index >= _steps.Length)
                return;

            _index = index;
            TutorialStep step = _steps[index];

            _progress.Text = $"Page {index + 1} of {_steps.Length}";
            _title.Text = step.Title;
            _body.Text = step.Body;

            _action.Visible = step.ButtonText != null;
            _action.Text = step.ButtonText ?? "";

            _back.Enabled = index > 0;
            _next.Enabled = index < _steps.Length - 1;
        }

        private void RunStepMacro()
        {
            string? macro = _steps[_index].Macro;
            if (string.IsNullOrEmpty(macro))
                return;

            // "!" cancels any command that is already running before starting this one.
            RhinoApp.RunScript("!" + macro, true);
        }
    }
}
