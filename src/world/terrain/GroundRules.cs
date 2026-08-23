namespace TrafficSimulation.World.Terrain;

/// <summary>
/// What a kind of ground permits, as a set rather than as a name (TER-2a). The kinds themselves are the
/// plan's vocabulary — <c>CityGen.Ground</c> — and what each one declares is <see cref="GroundCatalog"/>.
/// </summary>
[Flags]
internal enum GroundRules : byte
{
    None = 0,

    /// <summary>A person is permitted here under the soft rules.</summary>
    Walkable = 1 << 0,

    /// <summary>A car is permitted here under the soft rules.</summary>
    Drivable = 1 << 1,

    /// <summary>Walkable, and priced below a walker's other ground.</summary>
    Preferred = 1 << 2,

    /// <summary>A lane direction runs underneath, so the cell carries one.</summary>
    Directional = 1 << 3,
}
