using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;

namespace RhinoGotchi.Levels
{
    /// <summary>
    /// Bans some commands while a challenge runs. Rhino can't refuse to start a command, so
    /// whatever a banned command adds is remembered and deleted as soon as it ends.
    /// Evaluators should ignore <see cref="IsBlockedObject"/> objects in case they run first.
    /// </summary>
    public sealed class CommandBlocker : IDisposable
    {
        private readonly RhinoDoc _doc;
        private readonly HashSet<string> _blocked;
        private readonly HashSet<Guid> _blockedObjects = new HashSet<Guid>();
        private readonly List<Guid> _pending = new List<Guid>();
        private string? _running;

        public CommandBlocker(RhinoDoc doc, IEnumerable<string> blockedCommands)
        {
            _doc = doc;
            _blocked = new HashSet<string>(blockedCommands, StringComparer.OrdinalIgnoreCase);
            Command.BeginCommand += OnBegin;
            Command.EndCommand += OnEnd;
            RhinoDoc.AddRhinoObject += OnAdd;
        }

        /// <summary>Raised (on the UI thread) after a banned command ran and its objects were removed.</summary>
        public event Action<string>? Blocked;

        public IReadOnlyCollection<string> BlockedCommands => _blocked;

        public bool IsBlocked(string command) => _blocked.Contains(command);

        public bool IsBlockedObject(Guid id) => _blockedObjects.Contains(id);

        public void Dispose()
        {
            Command.BeginCommand -= OnBegin;
            Command.EndCommand -= OnEnd;
            RhinoDoc.AddRhinoObject -= OnAdd;
        }

        private void OnBegin(object? sender, CommandEventArgs e)
        {
            if (e.Document != _doc || !_blocked.Contains(e.CommandEnglishName))
                return;
            _running = e.CommandEnglishName;
            _pending.Clear();
            RhinoApp.WriteLine("[RhinoGotchi] {0} is blocked in this challenge! Anything it makes will be removed. Try another way.", _running);
        }

        private void OnAdd(object? sender, RhinoObjectEventArgs e)
        {
            if (_running == null || e.TheObject.Document != _doc)
                return;
            _pending.Add(e.ObjectId);
            _blockedObjects.Add(e.ObjectId);
        }

        private void OnEnd(object? sender, CommandEventArgs e)
        {
            if (_running == null || e.Document != _doc || !string.Equals(e.CommandEnglishName, _running, StringComparison.OrdinalIgnoreCase))
                return;
            string command = _running;
            var made = _pending.ToList();
            _running = null;
            _pending.Clear();

            // Delete after the command has fully finished.
            RhinoApp.InvokeOnUiThread((Action)(() =>
            {
                if (made.Count > 0)
                {
                    _doc.Objects.Delete(made, true);
                    _doc.Views.Redraw();
                }
                Blocked?.Invoke(command);
            }));
        }
    }
}
