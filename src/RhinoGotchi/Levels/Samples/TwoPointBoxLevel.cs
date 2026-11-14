using Rhino.Geometry;

namespace RhinoGotchi.Levels.Samples
{
    /// <summary>
    /// Sample level: two points are in the scene, a ghost box shows the goal,
    /// and the player builds that box with Box > Diagonal (two opposite corners).
    /// </summary>
    public static class TwoPointBoxLevel
    {
        public static readonly Point3d CornerA = new Point3d(0, 0, 0);
        public static readonly Point3d CornerB = new Point3d(10, 6, 4);

        public static Level Create()
        {
            var level = new Level
            {
                Id = "box-2pt-01",
                Title = "Build the box between the two points",
                Command = "Box", // taught through its Diagonal option
                Evaluator = new TwoPointBoxEvaluator(CornerA, CornerB),
            };

            level.StartGeometry.Add(new Point(CornerA));
            level.StartGeometry.Add(new Point(CornerB));

            var target = new Box(new BoundingBox(CornerA, CornerB));
            level.GhostGeometry.Add(target.ToBrep());

            return level;
        }
    }
}
