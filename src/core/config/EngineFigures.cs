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

    /// <summary>How far a notch of the wheel turns the town when the turn is asked for rather than the zoom (OBS-1c).</summary>
    public float CameraTurnPerNotchDeg { get; init; } = 6f;

    /// <summary>
    /// How far two fingers have to twist before the town starts turning under them (OBS-1c). A pinch is
    /// never exactly square, so without it every zoom leaves the town a degree or two off north.
    /// </summary>
    public float CameraTwistDeadZoneDeg { get; init; } = 8f;

    /// <summary>
    /// How far apart two fingers must be before the distance between them is worth dividing by
    /// (OBS-1c) — two contacts reported at the same place would otherwise scale the view by infinity.
    /// </summary>
    public float TouchLeastSpreadPx { get; init; } = 24f;
    /// <summary>The size a run opens at when <c>--size</c> names none: the window it restores to, and the frame a shot is taken at.</summary>
    public int WindowWidthPx { get; init; } = 1600;

    public int WindowHeightPx { get; init; } = 900;
    public float GroundPeriodGrassM { get; init; } = 12f;
    public float GroundPeriodTarmacM { get; init; } = 6f;
    public float GroundPeriodPavementM { get; init; } = 4f;
    public float GroundPeriodDeckM { get; init; } = 8f;
    public float GroundPeriodWaterM { get; init; } = 18f;

    /// <summary>
    /// The grid the ground, the buildings and the props are cut on. <b>The default view is 70 m over
    /// the short side, so a metre is about 13 screen pixels</b> and this is two and a half times what
    /// a standing town shows; the headroom is the zoom's, and it stops at one texel to one pixel.
    /// </summary>
    /// <remarks>Moved with <c>qq art --fix --art=…</c>, which is what puts the sheets on it.</remarks>
    public float ArtPixelsPerMetre { get; init; } = 31.5f;

    /// <summary>
    /// The grid the car art was cut on, and a finer one than the ground's on purpose. <b>Three times
    /// the ground's rather than one and a half</b>: CAR-12 asks that a variant's tyres show past its own
    /// bodywork by a few millimetres, measured off the silhouette in the picture, and a texel here is
    /// 10 mm of car. Coarser and there is nowhere to draw the thing the rule is about.
    /// </summary>
    public float CarSpritePixelsPerMetre { get; init; } = 96f;

    /// <summary>
    /// How far past 1:1 the zoom may run: at 1 it stops where one art texel is one display pixel, above
    /// that the sprites are magnified and their texels become visible.
    /// </summary>
    public float CameraMaxSpriteMagnification { get; init; } = 6f;

    /// <summary>
    /// How many units one selection may hold (CTL-1b). A bound and not a preference: the set is one
    /// array laid with the town, and a box drawn round a district has to stop somewhere.
    /// </summary>
    public int SelectionMaxUnits { get; init; } = 32;

    /// <summary>
    /// How far the pointer travels with the button down before a click becomes a drag (CTL-1b). <b>One
    /// figure for every surface a press can start a gesture on</b> — the town under a drag and the map
    /// list under a finger scrolling it — so a tap means the same travel wherever it lands.
    /// </summary>
    public float PointerDragPx { get; init; } = 6f;

    /// <summary>
    /// The window the interface is never laid out on fewer pixels than (OBS-2k). <b>It is the narrowest
    /// window the panels are still laid out for and not the width they would like</b>: a panel narrower
    /// than its rows want is laid narrower, and only below this does the density give way instead — the
    /// point past which a label is drawn under a pixel a glyph and there is nothing left to read.
    /// </summary>
    public float InterfaceLeastWidthPx { get; init; } = 360f;

    public float InterfaceLeastHeightPx { get; init; } = 300f;
}

/// <summary>
/// What a player's order to a car is measured against (CTL-8). <b>Nothing that drives itself reads any
/// of it</b>: an ordered car runs the same manoeuvres on the same road, and what is here is only where
/// the order says it has arrived and how far behind another car it is asked to sit.
/// </summary>
internal sealed class ControlFigures
{
    /// <summary>
    /// How near the ordered place the car has to have come to rest before the order is finished (CTL-8a).
    /// A car length and a half: near enough that a driver would say it had got there, and loose enough
    /// that stopping short of it behind a queue that then cleared is not an order left running for ever.
    /// </summary>
    public float PlaceReachInCarLengths { get; init; } = 1.5f;

    /// <summary>
    /// How far back along the road an ordered car is aimed at the one it is following (CTL-8c) — the same
    /// shape as a rescue's standoff, and for the same reason: a place on the road is the only place a
    /// vehicle can be made to stand.
    /// </summary>
    public float FollowGapInCarLengths { get; init; } = 2f;

    /// <summary>
    /// And how far the followed car moves before the route after it is drawn again. <b>A bound on the
    /// searching and not on the following</b>: the gap is held by the road the follower is granted
    /// (S-2a) every tick, while re-planning is what costs a search, so a leader creeping forward in a
    /// queue is not a route search a second.
    /// </summary>
    public float FollowRedrawInCarLengths { get; init; } = 3f;
}
