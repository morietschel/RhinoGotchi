using System;
using System.Collections.Generic;
using RhinoGotchi.Levels.Samples;

namespace RhinoGotchi.Levels
{
    /// <summary>All playable levels, in order. Each entry builds a fresh Level.</summary>
    public static class LevelCatalog
    {
        public static readonly IReadOnlyList<Func<Level>> All = new Func<Level>[]
        {
            TwoPointBoxLevel.Create,
        };
    }
}
