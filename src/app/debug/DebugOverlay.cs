using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.App.Render;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Runtime;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Debug;

/// <summary>
/// The debug layers: everything that acts on an agent while it moves, drawn where it happens.
/// </summary>
/// <remarks>
/// <para>
/// The overlay reads the producers, never a copy of the shape: a lane it draws is the lane the
/// follower is on, a node is the node the router plans over, a collision circle is the radius the
/// solver was given. A second copy eventually disagrees with the first, and the picture then argues
/// with the simulation instead of showing it.
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
    /// How far apart the two directions of a stretch have to be laid before the picture may call them two
    /// lines. Under it they are one stroke at any framing — which is what a pavement too narrow for a lane
    /// either side of it actually is (<see cref="World.Foot.WalkingNetwork.LaneOffsetM"/>, and every
    /// crossing in the town) — and the marks down it are ticks rather than chevrons.
    /// </summary>
    const float OneLineApartM = PathMarks.PathLineM;

    /// <summary>
    /// <b>A hairline, and the thinnest thing this overlay draws.</b> The collision layer is the one layer
    /// whose whole reading is <em>where the edge falls against the picture underneath it</em> — how far a
    /// shape sits inside the bodywork, or past it — and a stroke wide enough to see from across the town
    /// is a stroke wide enough to hide a hand's width of that answer at the framing it is read at.
    /// </summary>
    const float CollisionLineM = 0.04f;

    /// <summary>
    /// It is held to this on screen, though, however far out the camera is: below about a pixel a thin
    /// line stops being thin and starts being dotted, and a shape drawn in dashes is not a shape.
    /// </summary>
    const float CollisionLineFloorPx = 1.5f;

    /// <summary>
    /// A node stands on the ground its network joins over, so it is drawn at the size of that ground
    /// rather than at a size on screen — but only as much of it as marks the place. At the width of the
    /// junction it stands in it covered the movements through it, which are the thing worth seeing there.
    /// </summary>
    const float NodeDiscM = 0.5f;

    /// <summary>
    /// How many pieces of its own route an agent is drawn: the one it is on, and the one it has planned to
    /// take off the end of it. <b>Two, whatever the pieces are</b> — a lane and the junction off it, a
    /// junction and the lane it lands on, a pavement and the crossing at the kerb — because one piece
    /// leaves out the decision the agent is about to act on and three are the route again.
    /// </summary>
    const int StretchesDrawn = 2;

    /// <summary>
    /// Below this a body is a few pixels across and a label over it is a bar of unreadable text over
    /// the thing it names, so none is drawn. It is why a town-wide framing carries no labels at all.
    /// </summary>
    const float LabelPixelsPerMetre = 10f;

    readonly OverlayQuad[] _town = new OverlayQuad[TownQuadCapacity];
    int _townQuads;
    int _drawnGeneration = -1;
    float _drawnPixelsPerMetre = -1f;
    Vector2 _drawnCentreM;
    Vector2 _drawnSpanM;

    /// <summary>Whether the last frame had to lay the town's graphs again, which is the cost this cache exists to make rare.</summary>
    public bool Relaid { get; private set; }

    public void TownChanged()
    {
        _townQuads = 0;
        _drawnGeneration = -1;
    }

    /// <summary>
    /// Everything the switches ask for, into the frame's own buffer. The bodies are walked every
    /// frame because they move; the town's graphs are copied out of the cache unless the cache is
    /// stale.
    /// </summary>
    /// <param name="ground">
    /// <b>What is drawn under the bodies</b> — the marks that are about the ground rather than about
    /// anybody standing on it. A reservation is a stretch of road and a network is the road itself, so a
    /// car has to read over both; drawn with the rest, the wash tints every sprite it covers.
    /// </param>
    public void Draw(
        ref ScreenDraw draw, ref ScreenDraw ground, TownWorld world, SimConfig config, DebugSwitches switches,
        Vector2 viewCentreM, Vector2 viewSpanM, float pixelsPerMetre)
    {
        Relaid = false;
        if (switches.NeedsNetworks)
        {
            RelayIfStale(world, config, switches, viewCentreM, viewSpanM, pixelsPerMetre);
            ground.Take(_town.AsSpan(0, _townQuads));
        }

        // <b>Laid every frame and never into the cache above it.</b> The graphs the cache holds do not move
        // once the town is laid; the two books are re-laid from the bodies every tick, so a block copied out
        // of a stale buffer would be a reservation the town gave up several frames ago.
        //
        // <b>After the network graphs, so it is drawn over them where both are on.</b> The graphs say where
        // anything *could* go and the blocks say who has the ground now, so a chevron punching through a
        // reservation reads as the lane still being open. The agents' own lines are in the other buffer and
        // so stay above both: a car's route running out of the front of its block is the picture the two make.
        if (switches.Reservations)
        {
            // The bays first, because a bay is ground several stretches of way lie across and the stretches
            // are the finer reading of the two.
            TakenBays(ref ground, world, config, viewCentreM, viewSpanM);
            LaneIndex(ref ground, world, viewCentreM, viewSpanM, pixelsPerMetre);
            FootIndex(ref ground, world, viewCentreM, viewSpanM, pixelsPerMetre);
        }

        if (switches.WalkerLines) WalkerLines(ref draw, world, config, viewCentreM, viewSpanM, pixelsPerMetre);

        if (switches.CarLines) CarLines(ref draw, world, config, viewCentreM, viewSpanM, pixelsPerMetre);

        if (switches.Collision) CollisionShapes(ref draw, world, config, viewCentreM, viewSpanM, pixelsPerMetre);

        // Last, so the construction stands over the lines and the shapes: what it is read against is the
        // track written on the ground under all of them, and a chevron through the arc reads as the arc.
        if (switches.TurnCircles) TurnCircles(ref draw, world, viewCentreM, viewSpanM, pixelsPerMetre);
    }

    /// <summary>OBS-2b's cull, coarsely: whether a place is inside the view once the body standing there has been allowed its own reach.</summary>
    static bool OnScreen(Vector2 pointM, Vector2 viewCentreM, Vector2 viewSpanM, float reachM)
    {
        var offset = Vector2.Abs(pointM - viewCentreM);
        return offset.X <= viewSpanM.X * 0.5f + reachM && offset.Y <= viewSpanM.Y * 0.5f + reachM;
    }
}
