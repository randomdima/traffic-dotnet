using System.Numerics;
using TrafficSimulation.App.Render;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Debug;

/// <summary>
/// The debug layers: everything that acts on an agent while it moves, drawn where it happens.
/// </summary>
/// <remarks>
/// <para>
/// <b>What the engine holds is drawn by the engine layers and not here</b> (<see cref="EngineLayers"/>): every body's
/// shape, every link, every way, every claim and where every agent means to go, off the engine's own tables on the device,
/// whatever kind each thing is. What is left here is what only one driver has — a car's pass, its grant, a walker's step
/// aside — and the town's construction, which the engine does not step: its outline, its grid, its mesh, its zones.
/// </para>
/// <para>
/// The overlay reads the producers, never a copy of the shape. A second copy eventually disagrees with the first, and the
/// picture then argues with the simulation instead of showing it.
/// </para>
/// <para>
/// Two performance rules it is written around: nothing is drawn about a body that is not on screen,
/// and the town's own graphs are re-emitted only when the zoom, the window or a switch changes — they
/// do not move once the town is laid, and re-emitting them every tick is the most expensive thing in
/// the frame at a district framing. The quads themselves are cached.
/// </para>
/// </remarks>
internal sealed partial class DebugOverlay
{
    /// <summary>
    /// How much wider than the window the town's graphs are laid, as a fraction of the view. Panning
    /// inside the margin costs nothing; leaving it lays them again.
    /// </summary>
    const float MarginFraction = 0.5f;

    /// <summary>
    /// Room for the town's own graphs at one framing; more than this is drawn truncated rather than
    /// slowly. Three quarters of the buffer, because the two town layers together over a district of
    /// Odesa come to 45 000 quads and half the buffer dropped the second layer entirely.
    /// </summary>
    const int TownQuadCapacity = TownRenderer.OverlayCapacity * 3 / 4;

    /// <summary>
    /// <b>A hairline, and the thinnest thing the overlay draws.</b> The collision layer is the one layer
    /// whose whole reading is <em>where the edge falls against the picture underneath it</em> — how far a
    /// shape sits inside the bodywork, or past it — and a stroke wide enough to see from across the town
    /// is a stroke wide enough to hide a hand's width of that answer at the framing it is read at.
    /// </summary>
    internal const float HairlineM = 0.04f;

    /// <summary>
    /// It is held to this on screen, though, however far out the camera is: below about a pixel a thin
    /// line stops being thin and starts being dotted, and a shape drawn in dashes is not a shape.
    /// </summary>
    internal const float HairlineFloorPx = 1.5f;

    /// <summary>The same share of the room on the level above (<see cref="TownRenderer.UnderlayAboveCapacity"/>).</summary>
    const int TownAboveQuadCapacity = TownRenderer.UnderlayAboveCapacity * 3 / 4;

    readonly OverlayQuad[] _town = new OverlayQuad[TownQuadCapacity];

    /// <summary>
    /// <b>The graphs of the level above, cached beside the ground's</b> (TER-7b, PHY-1a): a bridge's lanes drawn with
    /// the ground's would be drawn under its own deck.
    /// </summary>
    readonly OverlayQuad[] _townAbove = new OverlayQuad[TownAboveQuadCapacity];

    int _townQuads;
    int _townAboveQuads;
    int _drawnGeneration = -1;
    float _drawnPixelsPerMetre = -1f;
    Vector2 _drawnCentreM;
    Vector2 _drawnSpanM;

    /// <summary>Whether the last frame had to lay the town's graphs again, which is the cost this cache exists to make rare.</summary>
    public bool Relaid { get; private set; }

    public void TownChanged()
    {
        _townQuads = 0;
        _townAboveQuads = 0;
        _drawnGeneration = -1;
    }

    /// <summary>
    /// Everything the switches ask for, into the frame's own buffer. The bodies are walked every
    /// frame because they move; the town's graphs are copied out of the cache unless the cache is
    /// stale.
    /// </summary>
    /// <param name="ground">
    /// <b>What is drawn under the bodies</b> — the marks that are about the ground rather than about
    /// anybody standing on it. A claim is a stretch of road and a network is the road itself, so a
    /// car has to read over both; drawn with the rest, the wash tints every sprite it covers.
    /// </param>
    /// <param name="groundAbove">
    /// <b>And what is drawn under the bodies on the level above</b> (TER-7b, PHY-1a): the same marks, about a bridge's
    /// own ways, drawn over its deck rather than under it.
    /// </param>
    /// <param name="mesh">
    /// The ground the renderer was handed, for the one layer that draws how the town is <em>made</em>
    /// rather than what it is doing (OBS-2o). Null where nothing has been laid yet.
    /// </param>
    /// <param name="pick">What the pointer is over and what a click has pinned (OBS-2t), both answered here.</param>
    /// <param name="picked">
    /// The bodies the engine's own table found under the pointer and pinned, on the last frame that asked
    /// (<see cref="EngineLayers"/>): what a body is found by, whatever kind it is.
    /// </param>
    /// <param name="pointerM">
    /// Where the pointer stands on the town, and <paramref name="pointerPx"/> where it stands on the glass —
    /// the first is what the readings are asked about and the second is where they are written.
    /// </param>
    public void Draw(
        ref ScreenDraw draw, ref ScreenDraw ground, ref ScreenDraw groundAbove, TownWorld world, GroundMesh? mesh,
        SimConfig config, DebugSwitches switches, DebugPick pick, Vector2 pointerM, Vector2 pointerPx, Vector2 uiPx,
        Vector2 viewCentreM, Vector2 viewSpanM, float pixelsPerMetre, in EngineLayers.Picks picked = default)
    {
        Relaid = false;

        if (switches.NeedsTownGeometry)
        {
            RelayIfStale(world, mesh, config, switches, viewCentreM, viewSpanM, pixelsPerMetre);
            ground.Take(_town.AsSpan(0, _townQuads));
            groundAbove.Take(_townAbove.AsSpan(0, _townAboveQuads));
        }

        // <b>Under every other mark</b> (OBS-2z): a zone is the broadest thing drawn, a tint the rest is read over.
        if (switches.Zones) Zones(ref ground, world, config, viewCentreM, viewSpanM, pixelsPerMetre);

        // <b>Laid every frame and never into the cache above it.</b> The graphs the cache holds do not move
        // once the town is laid; both networks' claims are re-laid from the bodies every tick, so a block
        // copied out of a stale buffer would be a claim the town gave up several frames ago.
        //
        // <b>After the network graphs, so it is drawn over them where both are on.</b> The graphs say where
        // anything *could* go and the blocks say who has the ground now, so a chevron punching through a
        // claim reads as the lane still being open. The agents' own lines are in the other buffer and
        // so stay above both: a car's route running out of the front of its block is the picture the two make.
        // <b>Before the claims and over the cache</b>: it is machinery rather than something the town did
        // (OBS-2x), and the cells of the moving grid are reindexed twice a step, so they cannot be cached
        // with the static half they are drawn against.
        if (switches.SolverGrid) SolverMovers(ref ground, world, viewCentreM, viewSpanM, pixelsPerMetre);

        // The stretches of way every claim holds are the engine layers' (EngineLayers), drawn off the claims table itself;
        // what is left here is the one hold that is not a piece of way, a bay.
        if (switches.Claims) TakenBays(ref ground, world, config, viewCentreM, viewSpanM);

        if (switches.WalkerLines) WalkerLines(ref draw, world, config, viewCentreM, viewSpanM, pixelsPerMetre);

        if (switches.CarLines) CarLines(ref draw, world, config, viewCentreM, viewSpanM, pixelsPerMetre);

        // Last, so the construction stands over the lines and the shapes: what it is read against is the
        // track written on the ground under all of them, and a chevron through the arc reads as the arc.
        if (switches.TurnCircles) TurnCircles(ref draw, world, viewCentreM, viewSpanM, pixelsPerMetre);

        // <b>After everything, because it is an answer about what is already drawn</b> (OBS-2t): a highlight
        // under the layer it picks out of is a line somebody has to look for.
        Pointer(ref draw, world, config, switches, pick, picked, pointerM, pointerPx, uiPx, pixelsPerMetre);
    }

    /// <summary>OBS-2b's cull, coarsely: whether a place is inside the view once the body standing there has been allowed its own reach.</summary>
    static bool OnScreen(Vector2 pointM, Vector2 viewCentreM, Vector2 viewSpanM, float reachM)
    {
        var offset = Vector2.Abs(pointM - viewCentreM);
        return offset.X <= viewSpanM.X * 0.5f + reachM && offset.Y <= viewSpanM.Y * 0.5f + reachM;
    }
}
