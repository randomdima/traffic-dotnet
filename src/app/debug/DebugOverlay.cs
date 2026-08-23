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
    /// <b>Everything this overlay draws is drawn at a size in metres</b>, so it zooms with the town under
    /// it exactly as a kerb or a car does. A mark every few metres and a short one: a run of small marks
    /// close together says which way the line runs without burying the line itself.
    /// </summary>
    const float MarkPitchM = 1.5f;

    /// <summary>How long a mark is against the pitch it stands at, and how heavy against the line it sits on. Heavier than the line, because a mark drawn at the line's own width reads as a kink in it.</summary>
    const float MarkSizeFraction = 0.24f;

    const float MarkWidthFactor = 1.25f;

    /// <summary>
    /// Under this a mark on screen is a smudge and not a direction, so none is drawn. It is what keeps
    /// the town layer inside <see cref="TownQuadCapacity"/> at a district framing, where a metric pitch
    /// otherwise puts three marks on the ground for every one that can be read.
    /// </summary>
    const float MarkVisiblePx = 2f;

    /// <summary>
    /// The pitch to walk a line at, or <see cref="float.PositiveInfinity"/> where the marks have shrunk
    /// out of sight — which is a pitch no mark is ever reached at, and the loops need no second branch.
    /// A metric pitch puts marks a few metres apart however far the camera is, and at a town-wide
    /// framing that is tens of thousands of quads nobody can see.
    /// </summary>
    static float MarkPitchAt(float pixelsPerMetre) =>
        MarkPitchM * MarkSizeFraction * pixelsPerMetre >= MarkVisiblePx ? MarkPitchM : float.PositiveInfinity;

    /// <summary>
    /// What a path is drawn at, whoever is drawing it: an agent's own route and the town's network under
    /// it. <b>One width and one mark</b> — the layers are telling one another's picture apart by colour,
    /// and a line that is also a little thicker reads as a different kind of line rather than as a
    /// different owner of the same one.
    /// </summary>
    const float PathLineM = 0.09f;

    /// <summary>A collision shape is not a path, and the one line here that is drawn heavier says so.</summary>
    const float CollisionLineM = 0.2f;

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
    /// The dot where two pieces of a route meet, and the dot where the drawing stops. Sized off the line
    /// they sit on rather than off the body, because a car and a walker draw the same picture and only the
    /// bodies differ.
    /// </summary>
    const float JoinDiscM = PathLineM * 1.5f;

    const float EndDiscM = PathLineM * 2.2f;

    /// <summary>
    /// How far a chord drawn across a bend may bow off it, on screen — the one figure here that is not a
    /// size on the ground, because it is a fidelity and not a mark. A quarter of a pixel is less than a
    /// line this wide can show at any framing.
    /// </summary>
    /// <remarks>
    /// It is a sag and not a step: a step chosen in pixels faceted a junction join at a close framing —
    /// the drawn corner was <em>tighter</em> than the one the car drives — while chopping a straight lane
    /// into a hundred quads that one quad draws. What each piece is stepped at is
    /// <see cref="Spline.ChordForSagM"/>, off its own curvature.
    /// </remarks>
    const float SagPx = 0.25f;

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

    /// <summary>How many quads the town's own graphs came to, for the frame read-out that prices the instrument.</summary>
    public int TownQuads => _townQuads;

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
            LaneIndex(ref ground, world, viewCentreM, viewSpanM, pixelsPerMetre);
            FootIndex(ref ground, world, viewCentreM, viewSpanM, pixelsPerMetre);
        }

        if (switches.WalkerLines) WalkerLines(ref draw, world, config, viewCentreM, viewSpanM, pixelsPerMetre);

        if (switches.CarLines) CarLines(ref draw, world, config, viewCentreM, viewSpanM, pixelsPerMetre);

        if (switches.Collision) CollisionShapes(ref draw, world, config, viewCentreM, viewSpanM);
    }

    /// <summary>
    /// The walkers: the line each is actually holding, drawn ahead of it as chevrons, and the place
    /// it is holding it to.
    /// </summary>
    /// <remarks>
    /// The line runs from the body and not from where the leg started, so the picture answers "where is
    /// it going". What is drawn is the walk that is left and not the point in hand: a picture of the aim
    /// point alone says a walker is heading into a building whenever the pavement bends round one, and
    /// the swerves and cut corners this layer exists to show are all in the run <em>after</em> it.
    /// </remarks>
    static bool OnScreen(Vector2 pointM, Vector2 viewCentreM, Vector2 viewSpanM, float reachM)
    {
        var offset = Vector2.Abs(pointM - viewCentreM);
        return offset.X <= viewSpanM.X * 0.5f + reachM && offset.Y <= viewSpanM.Y * 0.5f + reachM;
    }

    /// <summary>
    /// One stretch of a chain of arcs, as the run of quads that draws it: <b>every piece stepped at the
    /// chord its own curvature affords</b>, so a straight is one quad however long it is and a junction
    /// join gets the points its bend needs. Everything this overlay draws along the ground goes through
    /// here, whatever width it is drawn at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stepped piece by piece and not by distance along the whole chain, because the chain a route is
    /// made of is a straight lane and then a biarc through the box: a step taken from the curvature under
    /// the last point would carry a straight's chord into the bend that follows it.
    /// </para>
    /// <para>
    /// <b>Each piece is cut square to the line at both ends</b> (<see cref="ScreenDraw.BandM"/>), so the
    /// pieces share their cuts and a band is one shape. Butted as rectangles they pivot about the
    /// centreline instead, and a lane-wide band round a junction join comes out as a fan of blocks with a
    /// notch outside every joint and a double-blended wedge inside it — the wider the band the worse, and
    /// a reservation is drawn at the lane's own width.
    /// </para>
    /// </remarks>
    static void Banded(
        ref ScreenDraw draw, scoped ReadOnlySpan<ArcSeg> arcs, float fromM, float toM, float sagM, float widthM,
        Vector4 colour)
    {
        if (arcs.Length == 0 || toM <= fromM) return;

        var previousM = Spline.SampleAt(arcs, fromM).PositionM;
        var pieceStartM = 0f;
        foreach (var arc in arcs)
        {
            var lastM = MathF.Min(toM, pieceStartM + arc.LengthM);

            // Never shorter than the line is wide: a chord that fine says nothing a quad can show, and a
            // curvature out of a degenerate arc would otherwise ask for chords of no length at all.
            var stepM = MathF.Max(PathLineM, Spline.ChordForSagM(arc.Curvature, sagM));
            for (var atM = MathF.Max(fromM, pieceStartM); atM < lastM;)
            {
                var onwardM = MathF.Min(lastM, atM + stepM);
                var onM = arc.PointAtM(onwardM - pieceStartM);
                draw.BandM(previousM, onM, arc.Curvature * (onwardM - atM), widthM, colour);
                previousM = onM;
                atM = onwardM;
            }

            pieceStartM = pieceStartM + arc.LengthM;
        }
    }

    /// <summary>
    /// The marks down one stretch of a chain, at a pitch counted from <paramref name="anchorM"/> — a place
    /// on the ground and not a place on a body, so the marks stand still while an agent moves through them
    /// and two agents on one stretch put theirs on the same stones.
    /// </summary>
    /// <remarks>
    /// A pass of its own and not a mark dropped as the line is walked: a mark stands where the metres say,
    /// and where the chords drawing the line happen to fall is a question about the zoom.
    /// </remarks>
    static void Marks(
        ref ScreenDraw draw, scoped ReadOnlySpan<ArcSeg> arcs, float fromM, float toM, float anchorM, float pitchM,
        Vector4 colour)
    {
        if (arcs.Length == 0) return;

        for (var atM = fromM + FirstMarkM(fromM - anchorM, pitchM); atM <= toM; atM += pitchM)
        {
            var mark = Spline.SampleAt(arcs, atM);
            draw.ChevronM(
                mark.PositionM, mark.Direction, pitchM * MarkSizeFraction, PathLineM * MarkWidthFactor, colour);
        }
    }

    /// <summary>One stretch of a chain as a path: the line it is, and the marks that say which way it runs.</summary>
    static void Chained(
        ref ScreenDraw draw, scoped ReadOnlySpan<ArcSeg> arcs, float fromM, float toM, float anchorM, float pitchM,
        float sagM, Vector4 colour)
    {
        Banded(ref draw, arcs, fromM, toM, sagM, PathLineM, colour);
        Marks(ref draw, arcs, fromM, toM, anchorM, pitchM, colour);
    }

    /// <summary>How far into a run its first mark stands, given how far the run's start is past the anchor the pitch is counted from.</summary>
    static float FirstMarkM(float sinceAnchorM, float pitchM) => pitchM - (sinceAnchorM % pitchM);
}
