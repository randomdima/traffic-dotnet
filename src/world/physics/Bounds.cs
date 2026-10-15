using System.Numerics;

namespace TrafficSimulation.World.Physics;

/// <summary>A body's axis-aligned box, which is what the broad phase is laid over.</summary>
/// <remarks>
/// <b>Both corners in one record</b>: most of what a cell holds is a candidate the box turns away, and that is one cache
/// line read rather than two.
/// </remarks>
internal readonly record struct Bounds(Vector2 LeastM, Vector2 MostM)
{
    /// <summary>Whether this box misses another entirely.</summary>
    public bool Misses(Vector2 leastM, Vector2 mostM) =>
        MostM.X < leastM.X || LeastM.X > mostM.X ||
        MostM.Y < leastM.Y || LeastM.Y > mostM.Y;
}
