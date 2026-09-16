using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>One layer of the ground beside a road</b>: the ground out to its own distance off the driven ground's
/// boundary, and the runs that distance could not close.
/// </summary>
/// <remarks>
/// <b>A region and not a band</b> (TER-7b). A layer runs from the boundary itself out to one figure, so it
/// encloses every layer inside it and the picture states the difference between two of them by laying the
/// inner one over the outer. Struck instead as the band between two figures, two layers abut along a curve
/// each of them carries its own copy of, and the copies are sampled and thinned apart.
/// <b>And the open runs travel with the layer that left them</b>, because a closed shape moved off itself is
/// closed: a run with two ends is a crossing the move did not find, and which layer it was is the whole of
/// what a reader chasing it needs.
/// </remarks>
/// <param name="Named">
/// What a reader calls this layer — the one place they are named, so an instrument reporting one and a
/// pointer standing over it say the same word.
/// </param>
internal readonly record struct GroundLayer(string Named, float OutwardM, ArcSeg[][] Rings, ArcSeg[][] Loose);

/// <summary>
/// <b>The town's ground beside a road, as the one boundary read at two distances</b> (TER-3c.3, TER-7b):
/// the driven ground itself (<see cref="LaneShell"/>) and the walk beyond it, each standing on the ground
/// within one figure of that boundary — with the two lines the town carries beside a road handed over as
/// the lines they are.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every distance is measured from the boundary and never from the ring before it.</b> That is what
/// TER-7b states — a layer is <em>the</em> boundary moved by one figure — and it is also the only reading
/// that holds: an offset taken off an offset inherits whatever the first one rounded, so the ground between
/// two of them stops being the difference between the figures that struck them.
/// </para>
/// <para>
/// <b>A layer is the offset itself and nothing is taken back out of it</b> (TER-7b): it reaches the boundary
/// by enclosing everything inside it, so the driven ground it covers is covered again by the carriageway laid
/// over it and the order is the whole of what states the difference. Cut to the carriageway's complement, the
/// layer carried that boundary a second time — twice the corners, a second thinning of the same curve to
/// disagree with the first along, and a crossing to find at every mouth.
/// </para>
/// <para>
/// <b>A kerb is a line and not a layer</b> (TER-3d), and there are two of them: <see cref="Carriageway"/>,
/// where the carriageway hands over to the walk, and <see cref="WalkEdge"/>, where the walk hands over to
/// the grass. Each is handed over as the closed line it is, at no width and with nothing struck off it —
/// whoever draws one gives it the thickness a kerb has and builds the mesh for it, because an offset costs
/// what an offset costs and a line needs the figure rather than the shape.
/// </para>
/// <para>
/// <b>And nothing is smoothed</b> (TER-3c.3). The corners the answer turns are the corners the boundary
/// turns, each inherited at its own radius; rounding one out here would put a curve in the concrete that no
/// line of the town is the edge of.
/// </para>
/// <para>
/// <b>It knows nothing about what any of it is for.</b> Which surface a layer wears and how thick a line is
/// drawn are the picture's (<c>App.Render.GroundMesh</c>); what is stated here is that all of it is one
/// construction read at two figures.
/// </para>
/// </remarks>
internal sealed class GroundRings
{
    /// <summary>
    /// <b>How round the corners that turn in on the town are asked to be: not at all</b> (TER-3c.3). A
    /// layer's edge is the boundary at a distance, and a distance is the whole of what it is.
    /// </summary>
    const float NoSmoothing = 0f;

    readonly GroundLayer[] _layers;

    GroundRings(GroundLayer carriageway, GroundLayer walk, ArcSeg[][] walkEdge)
    {
        Carriageway = carriageway;
        Walk = walk;
        WalkEdge = walkEdge;
        _layers = [walk];
    }

    /// <summary>
    /// The driven ground itself, at nought: the boundary the lines lay, kerb to kerb. Its rings are the fill
    /// the carriageway is drawn as <em>and</em> the line the town's kerb is struck along.
    /// </summary>
    public GroundLayer Carriageway { get; }

    /// <summary>
    /// The walk: the ground from that boundary out to the pavement's outer face
    /// (<see cref="SimConfig.WalkOuterM"/>), which is the concrete a walker walks down the middle of.
    /// </summary>
    public GroundLayer Walk { get; }

    /// <summary>
    /// <b>The walk's outer face as the closed line it is</b> — the boundary moved by
    /// <see cref="SimConfig.WalkOuterM"/> and nothing taken out of it, which is what the walk's own kerb is
    /// struck along and what it ends at (<see cref="SimConfig.WalkKerbOuterM"/>).
    /// </summary>
    /// <remarks>
    /// <b>The same chains <see cref="Walk"/> is filled from, named for what a stroke wants of them.</b> A
    /// layer is its offset, so its rings and its outer face are one set of lines; what differs is that a fill
    /// reads them as a region and a stroke reads them as a line to lay a kerbstone about.
    /// </remarks>
    public ArcSeg[][] WalkEdge { get; }

    /// <summary>
    /// The layers struck off the boundary, in the order their figures grow, for a reader that walks them
    /// rather than naming one — the carriageway is not among them, being the boundary itself and not a shape
    /// struck off it.
    /// </summary>
    public ReadOnlySpan<GroundLayer> Layers => _layers;

    /// <summary>How many runs no distance could close, which is nought in a town with nothing wrong with it.</summary>
    public int LooseCount => Walk.Loose.Length;

    /// <summary>
    /// <b>The town's ground beside a road, struck in one move of its outline and nothing else.</b>
    /// <paramref name="shell"/> is the town's own (<see cref="Paving.Perimeter"/>) rather than one laid for
    /// this, because a second merge of the same lines is a second answer about the same ground.
    /// </summary>
    public static GroundRings Of(BandShell shell, SimConfig config)
    {
        var walkM = config.WalkOuterM;
        var (outer, loose) = shell.Outset(walkM, NoSmoothing);

        return new GroundRings(
            new GroundLayer("carriageway", 0f, shell.Chains.ToArray(), []),
            new GroundLayer("walk", walkM, outer, loose),
            outer);
    }
}
