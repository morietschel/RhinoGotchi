using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;

namespace RhinoGotchi.Content
{
    /// <summary>
    /// One picture to draw in the viewport: what you start with (inputs, orange), what the command
    /// produced (results, blue), helper lines (guides) and numbered pick points (marks).
    /// </summary>
    internal sealed class ShapeGeometry
    {
        public List<GeometryBase> Inputs { get; } = new List<GeometryBase>();
        public List<GeometryBase> Results { get; } = new List<GeometryBase>();
        public List<Line> Guides { get; } = new List<Line>();
        public List<(Point3d Point, string Label)> Marks { get; } = new List<(Point3d, string)>();

        public BoundingBox GetBoundingBox()
        {
            var box = BoundingBox.Empty;
            foreach (var g in Inputs.Concat(Results))
                box.Union(g.GetBoundingBox(true));
            foreach (var l in Guides)
                box.Union(l.BoundingBox);
            foreach (var m in Marks)
                box.Union(m.Point);
            return box;
        }

        /// <summary>A copy whose lists can be changed without touching this one. Geometry is shared.</summary>
        public ShapeGeometry Clone()
        {
            var copy = new ShapeGeometry();
            copy.Inputs.AddRange(Inputs);
            copy.Results.AddRange(Results);
            copy.Guides.AddRange(Guides);
            copy.Marks.AddRange(Marks);
            return copy;
        }

        /// <summary>A deep copy moved by a transform.</summary>
        public ShapeGeometry Transformed(Transform xf)
        {
            var copy = new ShapeGeometry();
            copy.Inputs.AddRange(Inputs.Select(g => Moved(g, xf)));
            copy.Results.AddRange(Results.Select(g => Moved(g, xf)));
            foreach (var l in Guides)
            {
                var line = l;
                line.Transform(xf);
                copy.Guides.Add(line);
            }
            foreach (var m in Marks)
            {
                var p = m.Point;
                p.Transform(xf);
                copy.Marks.Add((p, m.Label));
            }
            return copy;
        }

        public static GeometryBase Moved(GeometryBase g, Transform xf)
        {
            var copy = g.Duplicate();
            copy.Transform(xf);
            return copy;
        }
    }
}
