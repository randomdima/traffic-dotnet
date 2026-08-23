namespace TrafficSimulation.Core.Config;

/// <summary>
/// The contact solver's own model.
/// </summary>
/// <remarks>
/// Sixteen iterations and a 0.8 contact bias are Box2D v3's, which is what the picture was matched
/// against; no restitution appears at all, because a collision in this town is a crash rather than a
/// break shot. The allowance and the speculative distance are a pair — the allowance is the depth an
/// overlap settles to, the speculative distance is how early a pair is given a manifold, which is what
/// stops an approach in the tick it would otherwise have crossed and is why nothing is swept.
/// </remarks>
internal sealed class SolverFigures
{
    public int VelocityIterations { get; init; } = 16;

    /// <summary>Passes over the discarded push accumulator, which converges far faster than the velocity solve because it carries no momentum.</summary>
    public int PositionIterations { get; init; } = 4;

    public float ContactBias { get; init; } = 0.8f;
    public float AllowedPenetrationM { get; init; } = 0.005f;
    public float Friction { get; init; } = 1f;

    /// <summary>The ceiling on a positional correction, so a body found deep inside another is walked out rather than fired out.</summary>
    public float MaxPushOutMps { get; init; } = 3f;

    public float LinearDamping { get; init; } = 0.1f;
    public float AngularDamping { get; init; } = 1f;
}

/// <summary>The timeline every agent and every body shares.</summary>
internal sealed class SimFigures
{
    public int TickRateHz { get; init; } = 60;

    /// <summary>How stale an agent's decision may be. A floor on the rate, never a ceiling; 0 makes every agent think every tick.</summary>
    public float AgentDecisionIntervalS { get; init; } = 0.1f;

    /// <summary>A time scale above this integrates the physics coarsely and manufactures collisions the model never had.</summary>
    public float SoakMaxTimeScale { get; init; } = 4f;
}

/// <summary>What the town is looked at through, and the grids the art was cut on. Nothing here is simulated.</summary>
internal sealed class ViewFigures
{
    public float CameraDefaultViewM { get; init; } = 70f;
    public float CameraZoomPerNotch { get; init; } = 1.15f;
    public float CameraPanPxPerS { get; init; } = 300f;
    public int WindowWidthPx { get; init; } = 1920;
    public int WindowHeightPx { get; init; } = 1080;
    public float GroundPeriodGrassM { get; init; } = 12f;
    public float GroundPeriodTarmacM { get; init; } = 6f;
    public float GroundPeriodPavementM { get; init; } = 4f;
    public float GroundPeriodDeckM { get; init; } = 8f;
    public float GroundPeriodWaterM { get; init; } = 18f;

    /// <summary>21 art pixels per metre blown back up ×3, which is the grid every asset was cut on.</summary>
    public float ArtPixelsPerMetre { get; init; } = 63f;

    /// <summary>1:1 at the closest camera zoom, and a finer grid than the ground's on purpose.</summary>
    public float CarSpritePixelsPerMetre { get; init; } = 96f;
}
