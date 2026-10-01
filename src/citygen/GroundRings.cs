using System.Numerics;
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
/// <b>The town's ground beside a road, as the one boundary read at a handful of distances</b> (TER-3c.3,
/// TER-7b): the driven ground itself (<see cref="LaneShell"/>) and the walk beyond it, each standing on the
/// ground within one figure of that boundary — with the two kerbs the town carries beside a road handed over
/// as the lines they are.
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
/// <b>And every one of them is rounded at the one radius</b> (TER-3c.10,
/// <see cref="RoadFigures.LineRoundedM"/>). A line laid along a corner a fold cut is a line nothing walks
/// and no kerbstone is bent to; what rounds it is the move itself, run again at that radius
/// (<see cref="ArcOutset.Of"/>), and <b>it is one figure for every layer because two layers rounded
/// differently disagree about the same corner</b> — the concrete between them would then be wider on one
/// bend than on the next.
/// </para>
/// <para>
/// <b>The boundary itself is rounded too, and that is a cut and not a growth</b>: at no distance at all the
/// radius is past the distance, so a corner the town turns away at is taken off rather than filled
/// (<see cref="ArcOutset.Of"/>) — up to 0.41 of the radius at a right angle. What buys that back is the
/// figure being under half a lane: nothing a car is driven through is narrow enough to be closed over and
/// no ribbon of tarmac is thin enough to be swallowed.
/// </para>
/// <para>
/// <b>It knows nothing about what any of it is for.</b> Which surface a layer wears and how thick a line is
/// drawn are the picture's (<c>App.Render.GroundMesh</c>); what is stated here is that all of it is one
/// construction read at two figures.
/// </para>
/// </remarks>
internal sealed class GroundRings
{
    readonly GroundLayer[] _layers;
    readonly GridLevel _kerbLevel;
    readonly Lock _indexing = new();

    GroundRings(GroundLayer carriageway, GroundLayer walk, ArcSeg[][] walkEdge, GridLevel kerbLevel)
    {
        Carriageway = carriageway;
        Walk = walk;
        WalkEdge = walkEdge;
        _kerbLevel = kerbLevel;
        _layers = [walk];
    }

    /// <summary>
    /// The driven ground itself, at nought: the boundary the lines lay, kerb to kerb, rounded at the one
    /// radius every line here is. Its rings are the fill the carriageway is drawn as <em>and</em> the line
    /// the town's kerb is struck along.
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
    public int LooseCount => Carriageway.Loose.Length + Walk.Loose.Length;

    ChainIndex? _kerb;

    /// <summary>
    /// <b>Whether any of the town's paving stands within <paramref name="reachM"/> of a point</b> (GEN-6b),
    /// which is the boundary and the walk struck off it read as one shape.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Measured to the boundary and a layer's figure past it, never to the outer face.</b> The face is
    /// that boundary with its pockets closed (<see cref="ArcOutset.Of"/>): a strip of grass between two
    /// roads narrower than two walks is concrete end to end, and the face runs round the outside of the
    /// whole block rather than into it. Read off the face, a place in such a strip is tens of metres from
    /// the paving and a metre from a carriageway.
    /// </para>
    /// <para>
    /// <b>Laid on the first ask and over the boundary's own pieces</b> (<see cref="ChainIndex.OfPieces"/>):
    /// a town's boundary is a handful of rings of a hundred thousand pieces, and an index of rings would
    /// answer every query with all of them. It is behind a gate for the reason <see cref="Paving"/>'s
    /// products are — laid once however many askers there are.
    /// </para>
    /// </remarks>
    public bool PavedWithin(Vector2 pointM, float reachM) => PavedWithin(null, pointM, reachM);

    /// <inheritdoc cref="PavedWithin(Vector2, float)"/>
    /// <param name="scan">
    /// This caller's own working set over the boundary's pieces (<see cref="NewKerbScan"/>), for an ask off its
    /// own thread; none asks on the index's own.
    /// </param>
    public bool PavedWithin(ChainIndex.Scan? scan, Vector2 pointM, float reachM)
    {
        // Whether anything is near at all, so one slot is all the room the answer needs.
        Span<int> near = stackalloc int[1];
        Span<float> alongM = stackalloc float[1];
        var radiusM = reachM + Walk.OutwardM;
        return (scan is null
            ? Kerb.Near(pointM, radiusM, near, alongM)
            : Kerb.Near(scan, pointM, radiusM, near, alongM)) > 0;
    }

    /// <summary>A working set over the boundary's pieces, for a caller that means to ask <see cref="PavedWithin(ChainIndex.Scan?, Vector2, float)"/> off its own thread.</summary>
    public ChainIndex.Scan NewKerbScan() => Kerb.NewScan();

    /// <remarks>
    /// <b>The gate is taken only while the index is missing.</b> Once laid it is never replaced, so every
    /// later ask reads it without one — the building stage asks from every thread at once, a point at a time.
    /// </remarks>
    ChainIndex Kerb => Volatile.Read(ref _kerb) ?? LayTheKerb();

    ChainIndex LayTheKerb()
    {
        lock (_indexing)
        {
            if (_kerb is null) Volatile.Write(ref _kerb, ChainIndex.OfPieces(ArcRings.Flat(Carriageway.Rings), _kerbLevel));

            return _kerb;
        }
    }

    RingSides? _walkSides;

    /// <summary>
    /// <b>Which side of the walk's outer face a point stands on</b> (<see cref="RingSides"/>): the lattice the
    /// ground answers the walk off, laid on the first ask and read by whatever else must keep to the walk — the
    /// connections a crossing is walked through (WLK-15) — so the two cannot disagree about one metre of it.
    /// </summary>
    /// <remarks><b>It encloses the carriageway</b>, the layer being the offset whole (TER-7b).</remarks>
    public RingSides WalkSides(SimConfig config)
    {
        lock (_indexing) return _walkSides ??= RingSides.Of(Walk.Rings, config.ShellLevel);
    }

    /// <summary>
    /// <b>The town's ground beside a road, struck in one move of its outline and nothing else.</b>
    /// <paramref name="shell"/> is the town's own (<see cref="Paving.Perimeter"/>) rather than one laid for
    /// this, because a second merge of the same lines is a second answer about the same ground.
    /// </summary>
    public static GroundRings Of(BandShell shell, SimConfig config)
    {
        var roundedM = config.Road.LineRoundedM;
        var walkM = config.WalkOuterM;
        var (inner, innerLoose) = shell.Outset(0f, roundedM);
        var (outer, loose) = shell.Outset(walkM, roundedM);

        // <b>A layer is a region, so it is filled from rings shut across whatever its move left open</b>
        // (<see cref="ArcRings.Shut"/>), and the open runs are handed back beside them as the fault they are.
        if (innerLoose.Length > 0) inner = [.. inner, .. ArcRings.Shut(innerLoose)];
        if (loose.Length > 0) outer = [.. outer, .. ArcRings.Shut(loose)];

        return new GroundRings(
            new GroundLayer("carriageway", 0f, inner, innerLoose),
            new GroundLayer("walk", walkM, outer, loose),
            outer,
            config.Grid.Main);
    }
}
