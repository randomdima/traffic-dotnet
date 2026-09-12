using System.Numerics;
using TrafficSimulation.Core.Config;

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
        _walkM = paving.WalkM;
        _paving = paving;
        _config = config;
        LayTheRoads(pieces, config, paving.WalkM);
        LayTheTurns(paving, pieces.WorldSizeM, config);
        LayTheShapes(paving, config);
    }

    readonly Paving _paving;
    readonly SimConfig _config;
    GroundRings? _rings;

    /// <summary>
    /// <b>The town's boundary and the distances off it</b> (<see cref="GroundRings"/>), laid on the first
    /// ask rather than with the shapes.
    /// </summary>
    /// <remarks>
    /// <b>Laid late because a town being laid asks this of itself</b>: the generator remakes the ground as
    /// each stage adds its shapes, and a stage that only wants to know where the water is should not pay for
    /// a walk of a road network that is not finished being laid. What it is laid <em>off</em> is the same
    /// pieces the rest of this reads, so the boundary is always the boundary of the shapes there are.
    /// </remarks>
    GroundRings Boundary => _rings ??= GroundRings.Of(_paving, _config);

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
    /// </remarks>
    public Ground At(Vector2 pointM)
    {
        var roads = Roads(pointM);
        if (roads.Crossing) return Ground.Crosswalk;
        if (Turns(pointM, 0f)) return Ground.Intersection;
        if (Lanes(pointM, 0f)) return Ground.Road;
        if (BayWays(pointM, 0f)) return Ground.Parking;
        if (SlabReaches(pointM)) return Ground.Parking;

        // Inside the boundary and claimed by no line: the wedge a junction's corner is paved back over
        // (TER-5). An intersection has no shape of its own, so what is left of its ground once every
        // movement through it has taken what it sweeps is exactly this — and there is no fillet to lay,
        // the boundary having turned the corner itself (<see cref="LaneShell.Rounded"/>).
        var offTheKerbM = Boundary.OffTheKerbM(pointM);
        if (offTheKerbM <= 0f) return Ground.Intersection;

        if (roads.Deck) return Ground.Sidewalk;
        if (_water.Covers(pointM)) return Ground.Water;
        if (_shore.Covers(pointM)) return Ground.Sidewalk;

        return offTheKerbM <= _walkM ? Ground.Sidewalk : Ground.Grass;
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
    /// <b>Whether any of the town's paving stands within reach of a point</b> — a road's carriageway or
    /// walk, the ground a junction's arms share and the ring round it, a car park and its wrap, a slab, or
    /// the shore the water is set in. What a prop asks to be <em>well clear</em> of (GEN-6b).
    /// </summary>
    /// <remarks>
    /// <b>One distance and one figure</b> (<see cref="GroundRings.OffTheKerbM"/>): the paving reaches a walk
    /// beyond the kerb, so a point within a reach of any of it is one standing no further off the kerb than
    /// those two together. Asked of the shapes piece by piece instead — a road's band grown, a movement's
    /// grown, a fillet taken as the circle round it — the answer was generous by however much each piece's
    /// own arithmetic was, and no two pieces were generous by the same amount.
    /// </remarks>
    public bool PavingWithin(Vector2 pointM, float reachM) =>
        Boundary.OffTheKerbM(pointM) <= _walkM + reachM
        || SlabWithin(pointM, reachM)
        || _shore.Within(pointM, reachM);

    /// <summary>Whether a car park's own tarmac stands within reach — which is what tells a street tree from a bench.</summary>
    public bool ParkingWithin(Vector2 pointM, float reachM) =>
        BayWays(pointM, reachM);

    /// <summary>
    /// Whether one point is the ground given, <b>and on the map at all</b>. It is the whole of GEN-2b as
    /// the stages that place things read it: a shape that hangs over the edge of the town hangs over
    /// nothing, so off the town is not grass — even though <see cref="At"/> answers grass out there, which
    /// is what a body pushed off the map needs.
    /// </summary>
    bool Is(Vector2 pointM, Ground ground) => Contains(pointM) && At(pointM) == ground;

}
