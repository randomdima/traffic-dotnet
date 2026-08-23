namespace TrafficSimulation.World.Physics;

/// <summary>
/// A body's handle: its slot in <see cref="PhysicsWorld"/>'s table, counted from one so that a default
/// handle is no body at all.
/// </summary>
/// <remarks>
/// A slot is never reused and never moves. Bodies are added while the town is built and never destroyed
/// — a wreck and a corpse stay in the world, and a contained walker is disabled rather than removed —
/// so an index is a stable name for the whole run. Every ordering inside the solver derives from these
/// indices, which is what makes a digest reproducible.
/// </remarks>
internal readonly record struct BodyId(int Index1)
{
    public static BodyId None => default;

    public bool Exists => Index1 > 0;

    /// <summary>The row this body occupies in the body table.</summary>
    internal int Index => Index1 - 1;
}
