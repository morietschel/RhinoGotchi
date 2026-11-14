using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Geometry;
using RhinoGotchi.Levels;

namespace RhinoGotchi.Tutorials
{
    /// <summary>
    /// One tutorial card built on the Klosterneuburg model, played like FinGotchi: a headline, one
    /// short line, at most three steps, a Run button that starts the command with the right objects
    /// already picked, yellow click marks in the viewport, and a live check list.
    /// Each of the twelve tutorials has a core card (input prepared, one command) and a challenge
    /// card (the learner prepares the input too).
    /// </summary>
    public sealed class Tutorial
    {
        /// <summary>Stable id, also the level id and the progress key, e.g. "tut-01-booleandifference" (+ "-challenge").</summary>
        public string Id { get; set; } = "";

        /// <summary>1–12, easiest first. Also names the layer: "RhinoGotchi Tutorials::01 BooleanDifference".</summary>
        public int Number { get; set; }

        /// <summary>False: the core card. True: the harder challenge card of the same tutorial.</summary>
        public bool IsChallenge { get; set; }

        /// <summary>The headline, e.g. "Drill the steel plate."</summary>
        public string Title { get; set; } = "";

        /// <summary>One short line under the headline.</summary>
        public string Intro { get; set; } = "";

        /// <summary>The command the card teaches, by English name, e.g. "BooleanDifference".</summary>
        public string MainCommand { get; set; } = "";

        /// <summary>Other commands the card uses (challenge cards mostly).</summary>
        public List<string> SupportingCommands { get; } = new List<string>();

        /// <summary>The building element, e.g. "Bolted steel plate".</summary>
        public string Element { get; set; } = "";

        /// <summary>
        /// Where the reference geometry lives in the model, as the end of a layer full path. A layer
        /// matches when its full path ends with the fragment, or is a sublayer of one that does.
        /// </summary>
        public List<string> ReferenceLayers { get; } = new List<string>();

        /// <summary>At most three short steps.</summary>
        public List<TutorialStep> Steps { get; } = new List<TutorialStep>();

        /// <summary>
        /// The macro the Run button runs. "{Name}" is replaced by the id of the Input object with that
        /// name, so "_SelID {Plate} _BooleanDifference" starts with the plate picked. Null: Run just
        /// starts <see cref="MainCommand"/>.
        /// </summary>
        public string? RunMacro { get; set; }

        /// <summary>The bonus-star goal, e.g. "Cap it into a closed solid", or null.</summary>
        public string? BonusText { get; set; }

        /// <summary>Every size not taken from the model.</summary>
        public List<AssumedDimension> AssumedDimensions { get; } = new List<AssumedDimension>();

        /// <summary>Adds the card's start geometry to its Input layer.</summary>
        public Action<TutorialContext>? SetUp { get; set; }

        /// <summary>The points to click, drawn as yellow numbered marks (computed from the same context as the set-up).</summary>
        public Func<TutorialContext, IEnumerable<ClickMark>>? Clicks { get; set; }

        /// <summary>See-through targets shown while the card runs, e.g. the real roof patch.</summary>
        public Func<TutorialContext, IEnumerable<GeometryBase>>? Ghosts { get; set; }

        /// <summary>The live check.</summary>
        public Func<TutorialContext, ILevelEvaluator?>? Check { get; set; }

        /// <summary>"TUTORIAL 3 OF 12" or "CHALLENGE 3 OF 12".</summary>
        public string Kicker => $"{(IsChallenge ? "CHALLENGE" : "TUTORIAL")} {Number} OF {TutorialCatalog.Count}";

        /// <summary>The root layer for this card's geometry.</summary>
        public string LayerPath => $"{TutorialCatalog.RootLayer}::{Number:00} {MainCommand}{(IsChallenge ? " challenge" : "")}";

        public IEnumerable<string> AllCommands
        {
            get
            {
                yield return MainCommand;
                foreach (string c in SupportingCommands)
                    yield return c;
            }
        }
    }

    /// <summary>One numbered step.</summary>
    public sealed class TutorialStep
    {
        public TutorialStep(string text, string? command = null, string? viewHint = null)
        {
            Text = text;
            Command = command;
            ViewHint = viewHint;
        }

        public string Text { get; }

        /// <summary>A command the step uses, or null.</summary>
        public string? Command { get; }

        /// <summary>Which viewport to look in, e.g. "Top", or null.</summary>
        public string? ViewHint { get; }
    }

    /// <summary>A size that is not taken from the model.</summary>
    public sealed class AssumedDimension
    {
        public AssumedDimension(string name, double value, string unit, string why)
        {
            Name = name;
            Value = value;
            Unit = unit;
            Why = why;
        }

        public string Name { get; }
        public double Value { get; }
        public string Unit { get; }
        public string Why { get; }

        public override string ToString() => FormattableString.Invariant($"{Name}: {Value} {Unit} (assumed: {Why})");
    }

    /// <summary>What a card's set-up, click marks and check get to work with.</summary>
    public sealed class TutorialContext
    {
        internal TutorialContext(TutorialWorkspace workspace, ReferenceModel? reference)
        {
            Workspace = workspace;
            Reference = reference;
        }

        public RhinoDoc Doc => Workspace.Doc;

        public Tutorial Tutorial => Workspace.Tutorial;

        public TutorialWorkspace Workspace { get; }

        /// <summary>The Klosterneuburg model, or null in standalone mode (the model isn't open).</summary>
        public ReferenceModel? Reference { get; }

        public bool IsStandalone => Reference == null;

        /// <summary>Adds start geometry to the card's Input layer. Only valid during set-up.</summary>
        public Guid AddInput(GeometryBase geometry, string? name = null) => Workspace.AddInput(geometry, name, false);

        /// <summary>Adds a locked snap guide (e.g. a point under a click mark) to the Input layer. Only valid during set-up.</summary>
        public Guid AddGuide(GeometryBase geometry, string? name = null) => Workspace.AddInput(geometry, name, true);
    }
}
