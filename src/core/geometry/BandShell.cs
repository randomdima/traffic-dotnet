using System.Numerics;

namespace TrafficSimulation.Core.Geometry;

/// <summary>
/// <b>The outside of a union of bands, as the closed lines it is</b>: every line taken as the ribbon of
/// ground it covers (<see cref="ArcRibbon"/>), and all of those ribbons merged into one shape.
/// </summary>
/// <remarks>
/// <para>
/// <b>A band is an area and not a line.</b> The ground a line lays is the band half a width either side of
/// it with a square end at either end, and what this merges is the union of those bands — so the outside of
/// the union is the outside of that area and nothing else. Said instead as the stretches of the lines that
/// happen to be outermost, the answer was a walk: every line sampled against every band near it, the stops
/// paired up by how near they stood, and the ring shut with whatever straight the pairing left over.
/// </para>
/// <para>
/// <b>What comes back carries only ribbon in it.</b> Every piece of every ring is a piece of some ribbon's
/// own edge or of its square end, cut where another ribbon's boundary crosses it — so an arc stays the arc
/// it was, at its own radius, and nothing is a chord across a bend it was meant to follow. A boundary that
/// is straightened anywhere cuts inside the very area it is the edge of.
/// </para>
/// <para>
/// <b>A piece of ribbon is the outside where nothing else covers it.</b> That is the whole of the merge:
/// each piece is cut at every crossing it has with every other ribbon, and what is left of it is kept where
/// the ground a hair outside it is on no ribbon at all. No pairing, no ends to match up, no straight drawn
/// across ground no band covers — a place is inside the union or it is not, and the merge asks the bands
/// themselves rather than reasoning about which line ought to hand over to which.
/// </para>
/// <para>
/// <b>The covered ground is on the walker's right the whole way round</b>, on the ring round the outside and
/// on the ring round every hole it encloses alike, because every piece kept its own ribbon's order
/// (<see cref="ArcRibbon"/>) and a merge only ever drops pieces. So the inward normal of any place on the
/// boundary is read off the boundary's own direction, with nothing to look up and no ring to identify as
/// the outer one — which is also what fixes which way <see cref="Outset"/> moves.
/// </para>
/// <para>
/// <b>A ring turns only where the shape does.</b> The merge cuts a ribbon at every crossing anything has
/// with it, most of which are places the boundary carries straight on down the same curve — so what comes
/// back is joined (<see cref="Spline.JoinedInto"/>): consecutive stretches of one circle are the one
/// stretch they are, a ring's seam included. A point in a ring is a place the edge really turns or changes
/// radius, and a reader counting them is counting corners.
/// </para>
/// <para>
/// <b>A run that will not close is handed back apart</b> (<see cref="Loose"/>) rather than dropped in silence.
/// A merge that leaves one is a merge that lost a crossing somewhere, and the length of it is the reading
/// that says so. <b>And it is shut across its holes as well</b> (<see cref="ArcRings.Shut"/>), because
/// everything struck off the shape wants the ground it bounds: one ring round the outside of a town left
/// open was every square metre of its carriageway.
/// </para>
/// <para>
/// <b>It knows nothing about what laid the lines.</b> Lanes, movements and the ways into a parking bay are
/// one caller's reading of "a line with a width" (<c>CityGen.LaneShell</c>); a footway, a plot boundary or a
/// single drawn stroke is another's. What the merge is given is chains and widths, and what the examples in
/// here name — a carriageway's two lanes, a car park's bundle of ways — are the shapes that decided each
/// tolerance rather than shapes this type knows the names of.
/// </para>
/// </remarks>
internal sealed partial class BandShell
{
    /// <summary>The rings as they were merged, about <see cref="_originM"/> and not about the world's origin.</summary>
    readonly ArcSeg[][] _chains;
    readonly ArcSeg[][] _loose;

    /// <summary>
    /// <b>Where the merge was worked about</b>: the world's origin for a shape inside 8 192 m, and the middle
    /// of the shape, on a line of the grid, for one reaching further (<see cref="LineTolerance.Coarseness"/>).
    /// </summary>
    /// <remarks>
    /// <b>A float's step doubles with the distance from the origin, and the merge and every move off it are
    /// asked to a millimetre.</b> Worked where it stands, a town thirty kilometres across is worked in
    /// two-millimetre steps at its far edge and its ring round the outside came back open; worked about its
    /// middle it is never further out than half of that. The rings are handed out where they stand in the
    /// world, and every move off them is worked about the same place (<see cref="Outset"/>).
    /// </remarks>
    readonly Vector2 _originM;

    /// <summary>The grid the caller's index is laid on, which everything struck off the shape is indexed on too.</summary>
    readonly WorldGrid _grid;

    BandShell(ArcSeg[][] chains, ArcSeg[][] loose, Vector2 originM, WorldGrid grid)
    {
        _chains = chains;
        _loose = loose;
        _originM = originM;
        _grid = grid;
        _worldChains = Shifted(chains, originM);
        _worldLoose = Shifted(loose, originM);
    }

    readonly ArcSeg[][] _worldChains;
    readonly ArcSeg[][] _worldLoose;

    /// <summary>
    /// <b>The outside of the merged shape as closed rings</b> — one round the outside of it and one round
    /// every hole it encloses, each walked with the covered ground on its right.
    /// </summary>
    public ReadOnlySpan<ArcSeg[]> Chains => _worldChains;

    /// <summary>
    /// <b>The runs the merge could not close</b>, each a chain with two ends. It is a fault in the merge
    /// rather than a shape the bands have: the boundary of a union of closed bands is closed, so a run with
    /// ends is a crossing that was not found or a piece that was kept when it should have been covered.
    /// </summary>
    public ReadOnlySpan<ArcSeg[]> Loose => _worldLoose;

    /// <summary>
    /// <b>The place a shape spread this far is worked about</b>: nowhere but the world's origin while every
    /// corner of it is inside the ground a float answers to a millimetre, and the middle of it on a line of
    /// the main cell otherwise — so the move there and back shifts cells by whole cells (SIM-8).
    /// </summary>
    static Vector2 Origin(Vector2 leastM, Vector2 mostM, WorldGrid grid)
    {
        if (LineTolerance.Coarseness(leastM) <= 1f && LineTolerance.Coarseness(mostM) <= 1f) return Vector2.Zero;

        var (x, y) = grid.Main.CellOf((leastM + mostM) * 0.5f);
        return grid.Main.CornerM(x, y);
    }

    /// <summary>Every piece of every chain moved by one offset, or the chains themselves where it is none.</summary>
    static ArcSeg[][] Shifted(ArcSeg[][] chains, Vector2 byM)
    {
        if (byM == Vector2.Zero) return chains;

        var moved = new ArcSeg[chains.Length][];
        for (var chain = 0; chain < chains.Length; chain++)
        {
            moved[chain] = new ArcSeg[chains[chain].Length];
            for (var piece = 0; piece < chains[chain].Length; piece++)
            {
                moved[chain][piece] = chains[chain][piece] with { StartM = chains[chain][piece].StartM + byM };
            }
        }

        return moved;
    }

    /// <summary>The grid the shape was merged on (SIM-8), which anything asked of it afterwards is indexed on too.</summary>
    public WorldGrid Grid => _grid;

    /// <summary>
    /// <b>The bands merged into one shape.</b> <paramref name="index"/> is those same
    /// <paramref name="lines"/> under those same numbers — the merge asks it which lines are near a place,
    /// which is how it decides what covers what. The ribbons the bands come to are binned in an index of
    /// their own, piece by piece and at the finest level of the index's grid worth laying
    /// (<see cref="ChainIndex.FinestCellM"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The index is the caller's and not this type's</b>, because a caller that holds a set of lines
    /// generally holds an index of them already and a second copy of one is the expensive kind of duplicate.
    /// <b>Its numbering is the contract</b>: what it hands back for a place is <c>n</c>, and that is
    /// <c>lines[n]</c> at <c>widthM[n]</c>.
    /// </para>
    /// <para>
    /// <b>A line with no pieces is a line all the same, and the index does not hold one</b>
    /// (<see cref="ChainIndex.Builder.Add"/>): a chain with no arcs reaches no cell, so an index of
    /// <c>n</c> lines has <see cref="ChainIndex.ChainCount"/> of however many of them are drawn. That costs
    /// the merge nothing — the empty line's ribbon is empty too, so it covers nothing and cuts nothing — and
    /// it is why the count is checked as a bound rather than an equality. A town where nothing meets at a
    /// node lays a movement with no line at every arm of it (<c>IdlePlan</c>), which is half its driven
    /// lines.
    /// </para>
    /// </remarks>
    public static BandShell Of(ReadOnlySpan<ArcSeg[]> lines, ReadOnlySpan<float> widthM, ChainIndex index)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index.ChainCount, lines.Length);

        var grid = index.Window.Level.Grid;
        var window = index.Window;
        var originM = window.Count == 0
            ? Vector2.Zero
            : Origin(window.Level.CornerM(window.FromX, window.FromY), window.Level.CornerM(window.ToX + 1, window.ToY + 1), grid);

        var along = Shifted(lines.ToArray(), -originM);
        if (originM != Vector2.Zero)
        {
            // The caller's index answers where the lines stand in the world; the merge asks where they stand
            // about its own origin, so it is laid again over the lines as they were moved, at the same level.
            var building = new ChainIndex.Builder();
            for (var line = 0; line < along.Length; line++)
            {
                building.Add(line, along[line], Spline.TotalLengthM(along[line]));
            }

            index = building.Seal(window.Level);
        }

        var halfM = new float[lines.Length];
        var lengthM = new float[lines.Length];
        var ribbons = new ArcSeg[lines.Length][];
        var mostHalfM = 0f;

        for (var line = 0; line < lines.Length; line++)
        {
            halfM[line] = widthM[line] * 0.5f;
            lengthM[line] = Spline.TotalLengthM(along[line]);
            mostHalfM = MathF.Max(mostHalfM, halfM[line]);

            ribbons[line] = ArcRibbon.Of(along[line], halfM[line], LineTolerance.RoundingM);
        }

        var merge = new Merge(along, index, ribbons, halfM, lengthM, mostHalfM, originM, grid);
        var (chains, loose) = merge.Run();
        return new BandShell(loose.Length == 0 ? chains : [.. chains, .. ArcRings.Shut(loose)], loose, originM, grid);
    }

    /// <summary>
    /// <b>The same shape without the rings narrower on the whole than <paramref name="meanWidthM"/></b> — twice the ground
    /// one encloses over its length round: a sliver of ground or a hole in it too thin to be either, which the caller
    /// says the width of.
    /// </summary>
    public BandShell WithoutRingsNarrowerThan(float meanWidthM)
    {
        var kept = new List<ArcSeg[]>(_chains.Length);
        foreach (var ring in _chains)
        {
            if (Math.Abs(Spline.EnclosedM2(ring)) * 2.0 >= Spline.TotalLengthM(ring) * meanWidthM) kept.Add(ring);
        }

        return kept.Count == _chains.Length ? this : new BandShell([.. kept], _loose, _originM, _grid);
    }

    /// <summary>
    /// <b>The merged shape moved off itself</b> (<see cref="ArcOutset"/>), at one distance and one corner
    /// radius. What goes in is <see cref="Chains"/> and never <see cref="Loose"/>: an outset is a fact
    /// about a closed shape, and a run with two ends has no outside.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Outward is the walker's left, and that is a fact this type holds rather than the caller.</b> Every
    /// ring is walked with the covered ground on its right, the ring round a hole included — so one signed
    /// offset moves the outer ring away from the shape and the hole's ring into it, which is what makes the
    /// two of them one shape grown by a distance rather than two shapes each moved some way.
    /// </para>
    /// <para>
    /// <b>All the rings at once, because the answer is about the shape and not about any ring of it.</b> Two
    /// rings nearer than twice the distance come back as one and a ring smaller than it comes back as none,
    /// so the count that goes in has nothing to do with the count that comes out.
    /// </para>
    /// <para>
    /// <b>A line a body is held on asks for the fill without the cut</b>
    /// (<see cref="ArcOutset.Corners.Filled"/>): the ground the shape is built of is the ball's own line, but
    /// a course is the shape moved and may not be pulled back towards it by a radius.
    /// </para>
    /// </remarks>
    public (ArcSeg[][] Rings, ArcSeg[][] Loose) Outset(
        float outwardM, float roundedM, ArcOutset.Corners corners = ArcOutset.Corners.Rolled)
    {
        var (rings, loose) = ArcOutset.Of(_chains, outwardM, roundedM, _grid, corners, _originM);
        return (Shifted(rings, _originM), Shifted(loose, _originM));
    }

    /// <summary>
    /// <b>The merged shape cut into the triangles that cover it</b> (<see cref="ShellFill"/>), at one
    /// tolerance on how far a chord may bow off the bend it stands for, one budget for taking back out
    /// the corners the line either side of them already stands for, and one cap on how much of a turn a
    /// single chord may stand for. <see cref="Chains"/> and never
    /// <see cref="Loose"/>, for the reason <see cref="Outset"/> is: a run with two ends bounds nothing, so
    /// there is no inside of it to fill.
    /// </summary>
    /// <remarks>
    /// <b>Every ring at once, the holes included</b>, because which of them is a hole is a fact about the
    /// shape rather than about any ring — and it is one this type's winding already carries, so the fill
    /// reads it off the rings instead of being told.
    /// </remarks>
    public (Vector2[] PointsM, int[] Triangles) Fill(float sagM, float thriftM = 0f, float turnRad = 0f)
    {
        var (pointsM, triangles) = ShellFill.Of(_chains, sagM, thriftM, turnRad);
        if (_originM != Vector2.Zero)
        {
            for (var point = 0; point < pointsM.Length; point++) pointsM[point] += _originM;
        }

        return (pointsM, triangles);
    }
}
