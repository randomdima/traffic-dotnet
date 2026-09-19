using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>What is on the ground at a point</b>, solved against the shapes the town is drawn from — the road's
/// own curve, the lines a car is turned through a box on, the wedge its kerbs turn on, the rectangles a car
/// park is, and the rings the water is cut from. There is one geometry and this reads it (TER-7); nothing
/// here is quantised, so a kerb running at 40° is a kerb running at 40°.
/// </summary>
/// <remarks>
/// <para>
/// <b>The answer is <c>GroundMesh.Build</c>'s order, walked backwards.</b> Ground is drawn by laying each
/// piece over what stands there already, so what a point <em>is</em> is the last piece laid over it — and
/// the last piece laid over it is the first one met walking that order from the end. The two are one list
/// in two directions, which is the whole of why drawn and answered-for cannot drift apart — the zebra
/// included, which is struck last of all and so is asked about first.
/// </para>
/// <para>
/// <b>And the ground beside a road is one distance off the driven bands</b> (TER-7b): the tarmac is the
/// ground those bands lay, the concrete is the ground within a walk of them, and the grass is everything
/// further out. One figure answers all three, so a layer added to the picture is a row added to a table
/// here and never a shape to intersect — which is what makes the picture and the answer the same distance
/// rather than two constructions that agree for now.
/// </para>
/// <para>
/// <b>A crossing is paint on a carriageway and is nothing anywhere else</b> (TER-6), which is why it is a
/// stretch of the road it is painted across rather than a rectangle standing in the world: where that road
/// runs into a junction the paint runs in with it, and a walker crossing at the mouth is on a crossing
/// instead of in the road.
/// </para>
/// <para>
/// <b>Two frames, and the second one is straight.</b> Roads cannot overlap, so the road level answers
/// which road a point is on and projects onto it once; after that a carriageway, a walk, a deck and a
/// zebra are intervals along a line with the bend taken out of it. Curvature is paid once a query rather
/// than once a feature.
/// </para>
/// <para>
/// <b>It answers a kind and never a permission.</b> Who is allowed on each kind and what the surface is
/// worth is <c>World.Terrain.GroundCatalog</c>'s, which lives above this folder — so the generator can ask
/// where a thing may stand while a town is still being laid without the plan learning what an agent is.
/// </para>
/// <para>
/// Continuous position in, no snapping out. A point outside the town's own box is answered rather than
/// refused — anything can be pushed anywhere and a tick has nowhere to put an exception — and what it is
/// answered is whatever shape reaches it, which off the edge of a town is grass.
/// </para>
/// <para>
/// <b>Written once when it is made, and never afterwards.</b> The pieces it reads are finished before they
/// are handed over, so there is no state here that could drift from the geometry. The scratch a query uses
/// is the indexes' own and is not re-entrant, on the same footing as the solver's broad phase.
/// </para>
/// </remarks>
internal sealed partial class GroundShapes
{
    readonly GroundPieces _pieces;
    readonly Vector2 _worldSizeM;

    public GroundShapes(GroundPieces pieces, SimConfig config)
        : this(Paving.Lay(pieces, config), config)
    {
    }

    /// <summary>
    /// <b>Answered off the pavement the town was laid with</b> (<see cref="Paving"/>) rather than off a
    /// second reading of the same shapes. What is drawn and what is answered for are one list, so the
    /// question of whether they agree cannot be asked (TER-7).
    /// </summary>
    public GroundShapes(Paving paving, SimConfig config)
    {
        var pieces = paving.Of;
        _pieces = pieces;
        _worldSizeM = pieces.WorldSizeM;
        _paving = paving;
        _config = config;
        _walkOuterM = config.WalkOuterM;
        LayTheRoads(pieces, config);
        LayTheDriven(paving, config);
        LayTheShapes(paving, config);
        _ownScan = NewScan();
    }

    /// <summary>
    /// <b>One caller's working set for the three indexes <see cref="At"/> reads</b>, so that two threads may
    /// ask about two places at once (<see cref="ChainIndex.Scan"/>). A scan belongs to one thread at a time.
    /// </summary>
    /// <remarks>
    /// <b>Three and not one, because the question is asked of three sets</b>: the roads, the driven ground,
    /// and the ways into a bay. What is <em>not</em> here is the slabs and the water, which are boxes and
    /// rings walked in place and hold nothing between one ask and the next.
    /// </remarks>
    internal sealed class Scan(ChainIndex.Scan roads, ChainIndex.Scan driven, ChainIndex.Scan bays)
    {
        internal ChainIndex.Scan Roads { get; } = roads;

        internal ChainIndex.Scan Driven { get; } = driven;

        internal ChainIndex.Scan Bays { get; } = bays;
    }

    /// <summary>A scan of this ground's own indexes, for a caller that means to ask off its own thread.</summary>
    public Scan NewScan() => new(_roadIndex.NewScan(), _driven.NewScan(), _bays.NewScan());

    /// <summary>The scan every ask that names none of its own runs on — the tick's, and every other reader's.</summary>
    readonly Scan _ownScan;

    readonly Paving _paving;
    readonly SimConfig _config;

    /// <summary>
    /// <b>How far off the driven ground the pavement's outer face stands</b>
    /// (<see cref="SimConfig.WalkOuterM"/>, TER-3c.3) — the one figure that parts the concrete from the
    /// grass, held here because it is read once a query and not once a town.
    /// </summary>
    readonly float _walkOuterM;

    /// <summary>
    /// The last piece of ground laid over the point, found by asking the pieces in the reverse of the
    /// order they are laid in and taking the first that covers it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The lines say which tarmac a point is and the boundary says where the tarmac stops</b> (TER-3c.3,
    /// TER-5). They are one construction asked two ways: the boundary is the outline of these same bands
    /// (<see cref="LaneShell"/>), so a point the lines claim is a point inside it and the two can no more
    /// disagree than a shape can disagree with its own edge.
    /// </para>
    /// <para>
    /// <b>The lanes and never the roads.</b> A road's own band runs the whole length between the junctions
    /// at its ends while its lanes are cut back from them, so a sliver at every mouth in the town is road
    /// and not carriageway — and read off the road it was carriageway with the pavement, laid off the
    /// boundary, standing on top of it.
    /// </para>
    /// <para>
    /// <b>And the concrete is the same distance one step further out</b> (TER-7b): the walk is the ground
    /// within <see cref="SimConfig.WalkOuterM"/> of those bands, which is what the picture fills it as, so
    /// the pavement is answered without a second shape being asked and a walker standing on it is standing
    /// on what it can see it is standing on.
    /// </para>
    /// </remarks>
    public Ground At(Vector2 pointM) => At(_ownScan, pointM);

    /// <inheritdoc cref="At(Vector2)"/>
    /// <param name="scan">This caller's own working set (<see cref="NewScan"/>), for an ask off its own thread.</param>
    public Ground At(Scan scan, Vector2 pointM)
    {
        var roads = Roads(scan.Roads, pointM);
        if (roads.Crossing) return Ground.Crosswalk;

        var offTheDrivenM = _driven.OffM(scan.Driven, pointM, _walkOuterM);
        if (offTheDrivenM <= 0f) return Ground.Road;

        var offTheBaysM = _bays.OffM(scan.Bays, pointM, _walkOuterM);
        if (offTheBaysM <= 0f || SlabReaches(pointM)) return Ground.Parking;

        if (roads.Deck) return Ground.Sidewalk;
        if (_water.Covers(pointM)) return Ground.Water;
        if (_shore.Covers(pointM)) return Ground.Sidewalk;

        // The walk is asked after the water and not before it, because it is drawn before it: a bank the
        // town happens to pave up to is water where the two overlap, and the order is the whole of what
        // states that (TER-7).
        if (MathF.Min(offTheDrivenM, offTheBaysM) <= _walkOuterM) return Ground.Sidewalk;

        return Ground.Grass;
    }

    /// <summary>Whether the point is inside the town's own box, for a caller that wants to know before it asks.</summary>
    public bool Contains(Vector2 pointM) =>
        pointM.X >= 0f && pointM.Y >= 0f && pointM.X < _worldSizeM.X && pointM.Y < _worldSizeM.Y;

    /// <summary>
    /// Whether every point of a disc, walked at <paramref name="stepM"/>, is the ground given — what a prop
    /// asks for the girth it stands on (GEN-6a), and what keeps one on the map at all (GEN-2b), since off
    /// the town is not grass.
    /// </summary>
    /// <remarks>
    /// <b>The last step of every walk is the shape's own edge</b> rather than the last whole step that fits
    /// inside it: a girth whose rim falls between two steps is a rim nothing asked about, which is a bench
    /// hanging over the kerb it was cleared of.
    /// </remarks>
    public bool IsAll(Vector2 centreM, float radiusM, float stepM, Ground ground)
    {
        for (var acrossM = -radiusM; ; acrossM += stepM)
        {
            var atAcrossM = MathF.Min(acrossM, radiusM);
            var reachM = MathF.Sqrt(MathF.Max(0f, (radiusM * radiusM) - (atAcrossM * atAcrossM)));
            for (var alongM = -reachM; ; alongM += stepM)
            {
                var atAlongM = MathF.Min(alongM, reachM);
                if (!Is(centreM + new Vector2(atAlongM, atAcrossM), ground)) return false;
                if (atAlongM >= reachM) break;
            }

            if (atAcrossM >= radiusM) break;
        }

        return true;
    }

    /// <summary>The same of a rectangle standing on a bearing — what a building and a car park ask.</summary>
    public bool IsAll(Vector2 centreM, Vector2 axis, Vector2 halfExtentM, float stepM, Ground ground) =>
        IsAll(centreM, axis, halfExtentM, stepM, ground, ground);

    /// <summary>And of either of two grounds: a car park reaches back over the pavement it fronts (GEN-4b).</summary>
    public bool IsAll(
        Vector2 centreM, Vector2 axis, Vector2 halfExtentM, float stepM, Ground ground, Ground orGround)
    {
        var side = new Vector2(-axis.Y, axis.X);
        for (var alongM = -halfExtentM.X; ; alongM += stepM)
        {
            var atAlongM = MathF.Min(alongM, halfExtentM.X);
            for (var acrossM = -halfExtentM.Y; ; acrossM += stepM)
            {
                var atAcrossM = MathF.Min(acrossM, halfExtentM.Y);
                var atM = centreM + (axis * atAlongM) + (side * atAcrossM);
                if (!Is(atM, ground) && !Is(atM, orGround)) return false;

                if (atAcrossM >= halfExtentM.Y) break;
            }

            if (atAlongM >= halfExtentM.X) break;
        }

        return true;
    }

    /// <summary>
    /// <b>Whether any of the town's paving stands within reach of a point</b> — the walk's outer face, a
    /// slab, or the shore the water is set in. What a prop asks to be <em>well clear</em> of (GEN-6b).
    /// </summary>
    /// <remarks>
    /// <b>Off the boundary and not off the road records</b> (TER-7b, <see cref="GroundRings.PavedWithin"/>).
    /// A road's own half says where its lanes were laid and not where the concrete ends, and it says nothing
    /// at all about a junction's corner or a roundabout's island; the boundary says all of it at once, and
    /// it is the boundary the verge is measured off too, so the strip between the two passes is one figure
    /// wide everywhere.
    /// </remarks>
    public bool PavingWithin(Vector2 pointM, float reachM) =>
        _paving.Rings(_config).PavedWithin(pointM, reachM)
        || SlabWithin(pointM, reachM)
        || _shore.Within(pointM, reachM);

    /// <summary>
    /// Whether one point is the ground given, <b>and on the map at all</b>. It is the whole of GEN-2b as
    /// the stages that place things read it: a shape that hangs over the edge of the town hangs over
    /// nothing, so off the town is not grass — even though <see cref="At"/> answers grass out there, which
    /// is what a body pushed off the map needs.
    /// </summary>
    bool Is(Vector2 pointM, Ground ground) => Contains(pointM) && At(pointM) == ground;

}
