using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>The town's ground said as one boundary and a table of distances off it</b> (TER-3c.3, TER-7): the
/// shell's rings (<see cref="LaneShell"/>) with every kerb corner turned on them, and every line the ground
/// has — the kerb, the kerb line, the lane a walker follows, the pavement's outer edge — that one ring moved
/// by a figure.
/// </summary>
/// <remarks>
/// <para>
/// <b>One curve, so a width is a subtraction.</b> Two of these lines are offsets of the same ring by amounts
/// differing by a constant, so the band between them is exactly that constant wide wherever the fold rule
/// left both of them standing. Laid as separate unions of separate pieces, the same two lines were a walk
/// apart only where nothing conspired against it, and nothing could measure whether they were.
/// </para>
/// <para>
/// <b>Nought is the kerb.</b> The rings run down the lines cars are driven on and the edge of what they lay
/// stands half a band out, so every distance here is measured from that edge — the only place a reader with
/// a ruler could measure one from. A negative distance is inside the tarmac and is how a kerb line is
/// struck; a positive one is out over the pavement and the grass.
/// </para>
/// <para>
/// <b>The corner is turned on the ring and not laid beside it</b> (TER-5). A kerb turns a corner on an arc
/// tangent to both carriageways, and a ring corner pre-turned on that radius less its own half-width comes
/// out, once moved, as exactly that arc — so the fillet is a property of the boundary rather than a shape
/// somebody else has to remember to draw. Every distance inherits it, each at its own radius.
/// </para>
/// <para>
/// <b>Build-time to lay, tick-time to ask.</b> Laying the rings walks the town; <see cref="OffTheKerbM"/> is
/// asked per wheel per tick and allocates nothing.
/// </para>
/// </remarks>
internal sealed class GroundRings
{
    /// <summary>
    /// How far the lines are smoothed over. <b>Nought: a distance that is smoothed is not the distance it
    /// says it is</b>, and every reader of these lines measures against the figure it asked for. What
    /// smoothing is for is a picture of a whole town read at a glance, which is the debug layer's own ask
    /// and carries its own window (<c>DebugOverlay.ExtrudedSmoothM</c>).
    /// </summary>
    public const float SmoothM = 0f;

    readonly LaneShell _shell;
    readonly float _reachM;
    readonly RingField _kerb;
    readonly SimConfig _config;

    GroundRings(LaneShell shell, float reachM, RingField kerb, SimConfig config)
    {
        _shell = shell;
        _reachM = reachM;
        _kerb = kerb;
        _config = config;
    }

    /// <summary>
    /// The rings, with every corner the ground turns already turned on them, and the kerb indexed for the
    /// one question a tick asks.
    /// </summary>
    public static GroundRings Of(Paving paving, SimConfig config)
    {
        var shell = paving.Boundary(config);
        var reachM = config.RoadFootprintM;
        return new GroundRings(shell, reachM, new RingField(shell.Extruded(0f, SmoothM), reachM), config);
    }

    /// <summary>
    /// <b>The distance one named line stands off the kerb</b> (<see cref="GroundLine"/>) — the name joined
    /// to its figure, here and nowhere else.
    /// </summary>
    /// <remarks>
    /// <b>Static, because a caller with the boundary in hand should not have to index the town to read a
    /// distance.</b> The debug layer draws a named line every frame off the shell it already holds
    /// (<c>Paving.Boundary</c>), and building the kerb's index to look the figure up would be a grid a
    /// frame. The name is the caller's, the figure is <c>SimConfig</c>'s, and this is the join.
    /// </remarks>
    public static float OutM(GroundLine line, SimConfig config) => line switch
    {
        GroundLine.Roadside => config.RoadsidePerimeterOutM,
        _ => 0f,
    };

    /// <summary>
    /// <b>The rings that stand <paramref name="outM"/> off the kerb</b>, one for one with the shell's own
    /// and empty where the distance left a ring nothing.
    /// </summary>
    public ReadOnlySpan<ArcSeg[]> At(float outM) => _shell.Extruded(outM, SmoothM);

    /// <summary>
    /// <b>The rings of one named line</b>, each walked with the driven ground on its right, so the right of
    /// travel is the inward normal throughout (<see cref="GroundLine"/>).
    /// </summary>
    public ReadOnlySpan<ArcSeg[]> At(GroundLine line) => At(OutM(line, _config));

    /// <summary>The shell the distances are taken off, for a reader that wants the lines themselves.</summary>
    public LaneShell Shell => _shell;

    /// <summary>
    /// <b>How far a point stands off the kerb</b> — negative on the tarmac, positive off it, and
    /// <see cref="Reach"/> where the boundary is further away than the answer distinguishes.
    /// </summary>
    /// <remarks>
    /// <b>The sign is the nearest boundary's own hand and not a crossing count.</b> A ring walks with the
    /// ground on its right throughout, on the ring round the town and on the ring round every block it
    /// encloses (<see cref="LaneShell"/>), so which side of the nearest piece of boundary a point stands is
    /// the whole of the answer — and it needs no ray, no ordering of the rings and no knowing which of them
    /// is the outermost. Where the nearest place is a corner rather than a piece, the two pieces meeting
    /// there answer together, which is what keeps the sign right in the wedge outside a sharp one.
    /// </remarks>
    public float OffTheKerbM(Vector2 pointM) => _kerb.OffM(pointM, _reachM);

    /// <summary>
    /// How far off the kerb the answer is still measured. <b>Past it the question is not asked</b>: nothing
    /// the table names stands further off the kerb than a road and its two pavements, so a point further
    /// than that from every kerb in the town is grass wherever it is, and a caller that needs to know
    /// whether deep tarmac is tarmac asks the bands that lay it rather than this.
    /// </summary>
    public float Reach => _reachM;

}
