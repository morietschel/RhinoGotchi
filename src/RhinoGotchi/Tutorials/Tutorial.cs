using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Geometry;
using RhinoGotchi.Levels;

namespace RhinoGotchi.Tutorials
{
    /// <summary>
    /// One mini tutorial built on the open Klosterneuburg model: one building element,
    /// one main Rhino command. Text is written for beginners; learners use Rhino
    /// commands only (no scripts, no Grasshopper).
    /// </summary>
    public sealed class Tutorial
    {
        /// <summary>Stable id, also used as the level id and the progress key, e.g. "tut-01-loft".</summary>
        public string Id { get; set; } = "";

        /// <summary>1-based position in the series. Also names the layer: "RhinoGotchi Tutorials::01 Loft".</summary>
        public int Number { get; set; }

        /// <summary>The part of the series: "A" to "E".</summary>
        public string Part { get; set; } = "";

        /// <summary>The part's heading, e.g. "Roof surface".</summary>
        public string PartTitle { get; set; } = "";

        /// <summary>What the learner sees, e.g. "Undulating roof".</summary>
        public string Title { get; set; } = "";

        /// <summary>The command the tutorial is about, by English name, e.g. "Loft".</summary>
        public string MainCommand { get; set; } = "";

        /// <summary>Commands used along the way, e.g. "Rebuild" in the NetworkSrf tutorial.</summary>
        public List<string> SupportingCommands { get; } = new List<string>();

        /// <summary>The building element it covers, e.g. "Wavy roof patch".</summary>
        public string Element { get; set; } = "";

        /// <summary>
        /// Where the reference geometry lives in the Klosterneuburg model, as the end of a
        /// layer full path, e.g. "00.3 ROOF::00.3.1 BASE SRF::Roof". A layer matches when its
        /// full path ends with the fragment, or when it is a sublayer of a layer that does.
        /// Match with ordinal comparison: the names contain ä, ü and ß.
        /// </summary>
        public List<string> ReferenceLayers { get; } = new List<string>();

        public string Goal { get; set; } = "";

        /// <summary>What has to exist before the learner starts (made by Set up or by the learner).</summary>
        public string InputGeometry { get; set; } = "";

        public List<TutorialStep> Steps { get; } = new List<TutorialStep>();

        /// <summary>The command options worth explaining, one per entry.</summary>
        public List<string> KeyOptions { get; } = new List<string>();

        /// <summary>Exactly two common mistakes.</summary>
        public List<string> CommonMistakes { get; } = new List<string>();

        /// <summary>The simple check learners use to judge their result.</summary>
        public string Check { get; set; } = "";

        /// <summary>Every size that is not taken from the model, labelled "assumed" in the UI.</summary>
        public List<AssumedDimension> AssumedDimensions { get; } = new List<AssumedDimension>();

        /// <summary>Adds the tutorial's start geometry to its Input layer. Filled in later.</summary>
        public Action<TutorialContext>? SetUp { get; set; }

        /// <summary>Builds the level that runs and checks this tutorial. Filled in later.</summary>
        public Func<TutorialContext, Level>? CreateLevel { get; set; }

        /// <summary>The root layer for this tutorial's own geometry.</summary>
        public string LayerPath => $"{TutorialCatalog.RootLayer}::{Number:00} {MainCommand}";

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

    /// <summary>One numbered modelling step.</summary>
    public sealed class TutorialStep
    {
        public TutorialStep(string text, string? command = null, string? viewHint = null)
        {
            Text = text;
            Command = command;
            ViewHint = viewHint;
        }

        public string Text { get; }

        /// <summary>A command the step's Try button runs, or null.</summary>
        public string? Command { get; }

        /// <summary>Which viewport to look in, e.g. "Top" or "Perspective", or null.</summary>
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

    /// <summary>What a tutorial's set-up and level factory get to work with.</summary>
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

        /// <summary>Adds start geometry to the tutorial's Input layer. Only valid during set-up.</summary>
        public Guid AddInput(GeometryBase geometry, string? name = null) => Workspace.AddInput(geometry, name);
    }
}
