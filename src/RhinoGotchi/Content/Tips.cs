using System;

namespace RhinoGotchi.Content
{
    internal static class Tips
    {
        public static readonly string[] All =
        {
            "Press Enter (or right-click in a viewport) with nothing typed to repeat the last command.",
            "Hold Shift while picking points to switch Ortho on or off temporarily.",
            "While picking a point, press Tab to lock the direction toward the cursor.",
            "SelLast reselects whatever the last command created.",
            "On the Gumball, drag arrows to move, arcs to rotate and squares to scale. Hold Alt (Option on Mac) while you drag to make a copy.",
            "Ctrl+Shift+click (Cmd+Shift on Mac) selects a single face, edge or control point inside a polysurface.",
            "ShowEdges with the Naked option highlights open edges, which is the usual reason a polysurface won't close.",
            "If a Boolean fails, check that both objects are closed solids. What and ShowEdges will tell you.",
            "Turn on Record History before Loft or Sweep, and the surface updates when you edit its curves.",
            "Rebuild gives a curve fewer, evenly spaced control points, which makes smoother surfaces.",
            "ExtractSrf pulls one face out of a polysurface so you can edit or replace it.",
            "Join turns touching curves or surfaces into one object. Explode takes them apart again.",
            "Use Zoom with the Selected option to frame only the objects you have selected.",
            "Lock keeps objects visible and snappable while stopping you from selecting them by accident.",
            "Most commands have options in the command line. Click one, or type its underlined letter.",
        };

        public static int TodayIndex => DateTime.Today.DayOfYear % All.Length;
    }
}
