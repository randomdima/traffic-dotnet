using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>The ground the town is driven along, as one shape</b> — a band its full width for every road, every
/// line a car is turned through a box on and every way into a bay — answered as a single distance from a
/// point.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the shape the town's boundary is the outside of</b> (<see cref="LaneShell"/>), and that is the
/// whole of what it is for: the shell is said in the lines themselves, so a walk of a ring has to be able
/// to ask whether the straight it is about to take runs over driven ground or off it. Nothing is laid off
/// this — every line beside the road is the boundary moved by its own distance
/// (<see cref="GroundRings"/>) — and nothing here grows anything: it is the ground a car drives on at the
/// size it is drawn.
/// </para>
/// <para>
/// <b>The bands and nothing that was paved around them</b> — not a junction's corner apron and not a car
/// park's slab. A fillet stands outside the corner where two roads' kerbs cross, so counted in, the
/// outside leaves the lanes at every mouth and no lane section is left to carry it round; left out, the two
/// arms' own bands meet at that crossing point and the boundary goes round the junction on the lanes
/// themselves. A slab is paved under a whole car park, so counted in, its bays are buried and the outside
/// of one is the slab's own edge, which no car is driven along.
/// </para>
/// <para>
/// <b>Build-time work.</b> It allocates freely and answers thousands of points while a town is laid; it
/// is never asked anything on a tick.
/// </para>
/// </remarks>
internal sealed class Kerbs
{
    /// <summary>
    /// How far from a piece a query may stand and still be answered about it. Nothing asks about ground
    /// further off the road than a pavement and a road are wide together, and the index is built to reach
    /// exactly this far so that a query never has to walk the town.
    /// </summary>
    public const float ReachM = 12f;

    readonly List<Piece> _pieces;
    readonly List<Shard> _shards;
    readonly ShardGrid _grid;

    Kerbs(List<Piece> pieces, List<Shard> shards, ShardGrid grid)
    {
        _pieces = pieces;
        _shards = shards;
        _grid = grid;
    }

    /// <param name="through">
    /// The junctions a road runs through as one line (<see cref="RoadCuts.RunsThrough"/>), whose movements
    /// are no pieces of the tarmac's outline: every one of them lies inside the two arms it joins.
    /// </param>
    public static Kerbs Of(GroundPieces plan, LaneLines lanes, BayLines bays, bool[] through)
    {
        var pieces = Lay(plan, lanes, bays, through);
        var shards = Shatter(pieces);
        return new Kerbs(pieces, shards, new ShardGrid(pieces, shards, plan.WorldSizeM));
    }

    /// <summary>
    /// <b>How far a point stands off the ground the town is driven <em>along</em></b>, negative within it,
    /// and <see cref="ReachM"/> where nothing is near enough to have an opinion.
    /// </summary>
    public float OffTheDrivenM(Vector2 pointM)
    {
        var nearestM = ReachM;
        foreach (var shard in _grid.At(pointM))
        {
            nearestM = MathF.Min(
                nearestM, DistanceM(_pieces[_shards[shard].Piece], _shards[shard].Arc, pointM));
        }

        return nearestM;
    }

    /// <summary>
    /// A millimetre: <b>what two computations of one distance disagree by</b>. Offsetting a chain and
    /// measuring back to what it was offset from are different arithmetic, so a line laid exactly the offset
    /// out reads a hair inside it; and two wrapping lines cut where they cross are cut by two bisections of
    /// their own, so the ends that meet at a node are two points and not one point twice.
    /// </summary>
    public const float RoundingM = 0.001f;

    /// <summary>
    /// <b>How far the two ends that meet at a crossing can stand apart</b>, which is what anybody wanting
    /// them as one place has to allow.
    /// </summary>
    /// <remarks>
    /// A line is cut a rounding late, and a line meeting another <em>tangentially</em> runs √(2·R·ε) past
    /// the point they cross before it is a rounding inside it (<see cref="RoundingM"/>) — a tenth of a metre
    /// at the radius a kerb fillet is turned on, and a twentieth at the radius a car park's corner is. It is
    /// the bound and not a measurement: the ends that actually meet at a right angle stand a millimetre
    /// apart.
    /// </remarks>
    public const float OnePlaceM = 0.15f;

    /// <summary>
    /// How finely a wrapping line is walked when asking what it runs past. A quarter-metre is the road
    /// tolerance, and where the answer changes between two stations the crossing is bisected off it — so
    /// what decides where a run ends is a millimetre and not a station.
    /// </summary>
    public const float StationM = 0.25f;

    public const int BisectionRounds = 12;

    /// <summary>
    /// <b>One arc of one band</b>: the unit the index bins and a distance is measured against. A street's
    /// own box is most of a district, so asking a point about the band whole means walking every bend the
    /// street ever takes; shattered, the index names the two or three arcs that can possibly be nearest.
    /// </summary>
    readonly record struct Shard(int Piece, int Arc);

    static List<Shard> Shatter(List<Piece> pieces)
    {
        var shards = new List<Shard>();
        for (var piece = 0; piece < pieces.Count; piece++)
        {
            for (var arc = 0; arc < pieces[piece].Arcs.Length; arc++) shards.Add(new Shard(piece, arc));
        }

        return shards;
    }

    /// <summary>
    /// How near two pieces have to end and start to be one line: a centimetre, which is the same figure
    /// the walking side calls one place (<c>WalkingNetwork.SamePlaceM</c>) and well under anything a
    /// reader could see.
    /// </summary>
    public const float JoinedM = 0.01f;

    /// <summary>How far a point stands outside one arc of one band, negative within it.</summary>
    static float DistanceM(in Piece piece, int arc, Vector2 pointM)
    {
        var one = piece.Arcs.Span.Slice(arc, 1);
        var alongM = Spline.ProjectM(one, pointM, 0f, float.MaxValue);
        var on = Spline.SampleAt(one, alongM);
        var offsetM = pointM - on.PositionM;
        var asideM = MathF.Abs(Vector2.Dot(offsetM, on.Right)) - piece.HalfWidthM;

        // <b>A band ends where its line does</b> (TER-7a). Measured radially from the last station it
        // would end in a half-disc of its own half-width instead, and a line a car is turned on — which
        // begins in the middle of the lane it leaves — then carried a bulge of tarmac half a lane past that
        // point, out under the pavement corner beside it. It is also what <c>GroundShapes</c> has always
        // answered, so the two readings of one shape agree.
        var beyondM = alongM <= 0f || alongM >= one[0].LengthM
            ? MathF.Abs(Vector2.Dot(offsetM, on.Direction))
            : 0f;
        if (beyondM <= 0f) return asideM;

        var outM = MathF.Max(asideM, 0f);
        return MathF.Sqrt((outM * outM) + (beyondM * beyondM));
    }

    /// <summary>
    /// Every band the driven ground is made of. <b>Nothing here is grown by anything</b>: it is the ground
    /// a car drives on at the size it is drawn, and what stands beside it is the boundary's to strike.
    /// </summary>
    static List<Piece> Lay(GroundPieces plan, LaneLines lanes, BayLines bays, bool[] through)
    {
        var pieces = new List<Piece>();
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            var arcs = plan.Roads.SegmentsOf(road);
            if (arcs.Length == 0) continue;

            pieces.Add(new Piece(arcs.ToArray(), plan.Roads.WidthM[road] * 0.5f));
        }

        // A movement through a box the road runs through as one line stands inside the two arms' own
        // bands, so it is not a piece of the outline: offered, its wrap stood exactly on the arms' where
        // the lane fills the road and laid a second run over theirs, with a round at each end of it.
        for (var connector = 0; connector < lanes.ConnectorCount; connector++)
        {
            var arcs = lanes.ArcsOfConnector(connector);
            if (arcs.Length == 0) continue;

            var junction = lanes.JunctionOfConnector(connector);
            if (junction != CityPlan.NoRecord && through[junction]) continue;

            pieces.Add(new Piece(arcs.ToArray(), lanes.ConnectorWidthM(connector) * 0.5f));
        }

        // <b>A join folded into a lane is no movement, but the ground under it is still tarmac</b>
        // (TER-5h): the lane now runs along it, and where the two arms' kerbs do not meet there is a wedge
        // between their carriageways that no road's own band covers. Asked the same question the movements
        // are, because it is the same question.
        for (var weld = 0; weld < lanes.Welds.Count; weld++)
        {
            var arcs = lanes.Welds.ArcsOf(weld);
            if (arcs.Length == 0 || through[lanes.Welds.Junction[weld]]) continue;

            pieces.Add(new Piece(arcs.ToArray(), lanes.Welds.WidthM[weld] * 0.5f));
        }

        // <b>A car park is the movements that reach into it</b> (<see cref="BayLines"/>, GEN-4b) and has no
        // shape of its own, exactly as a junction is the movements that cross in it. Unlike those, a bay's
        // way runs <em>out</em> of the road rather than between two arms of it, so its own line is the
        // outside of the tarmac wherever nothing else stands nearer — which is the far end of every space.
        // <b>And it is driven at the width of the lane it is worked off</b> (GEN-4c), like every other
        // driven line: what a space is wide sizes the car standing in it and never the ground driven to it.
        foreach (var way in bays.GroundWays)
        {
            var arcs = bays.ArcsOf(way);
            if (arcs.Length == 0) continue;

            pieces.Add(new Piece(arcs.ToArray(), lanes.LaneWidthM[bays.Lane[way]] * 0.5f));
        }

        return pieces;
    }

    /// <summary>One band of driven ground: the line it is driven down, and half the width of that ground.</summary>
    readonly record struct Piece(ReadOnlyMemory<ArcSeg> Arcs, float HalfWidthM);

    /// <summary>
    /// Which shards reach within <see cref="ReachM"/> of which square of the town, so a distance costs a
    /// handful of functions rather than every band in the city.
    /// </summary>
    sealed class ShardGrid
    {
        const float SquareM = 16f;

        readonly List<int>[] _squares;
        readonly int _wide;
        readonly int _high;

        public ShardGrid(List<Piece> pieces, List<Shard> shards, Vector2 worldM)
        {
            _wide = Math.Max(1, (int)(worldM.X / SquareM) + 1);
            _high = Math.Max(1, (int)(worldM.Y / SquareM) + 1);
            _squares = new List<int>[_wide * _high];

            for (var shard = 0; shard < shards.Count; shard++)
            {
                var piece = pieces[shards[shard].Piece];
                var arc = piece.Arcs.Span[shards[shard].Arc];
                var reachM = new Vector2(piece.HalfWidthM + ReachM);
                var leastM = new Vector2(float.MaxValue);
                var mostM = new Vector2(float.MinValue);
                var steps = Math.Max(1, (int)MathF.Ceiling(arc.LengthM));
                for (var step = 0; step <= steps; step++)
                {
                    var onM = arc.PointAtM(arc.LengthM * step / steps);
                    leastM = Vector2.Min(leastM, onM);
                    mostM = Vector2.Max(mostM, onM);
                }

                Fill(shard, (leastM - reachM, mostM + reachM));
            }
        }

        public ReadOnlySpan<int> At(Vector2 pointM)
        {
            var square = _squares[(Square(pointM.Y, _high) * _wide) + Square(pointM.X, _wide)];
            return square is null ? default : CollectionsMarshal.AsSpan(square);
        }

        void Fill(int shard, (Vector2 LeastM, Vector2 MostM) box)
        {
            for (var y = Square(box.LeastM.Y, _high); y <= Square(box.MostM.Y, _high); y++)
            {
                for (var x = Square(box.LeastM.X, _wide); x <= Square(box.MostM.X, _wide); x++)
                {
                    var square = _squares[(y * _wide) + x] ??= [];
                    if (!square.Contains(shard)) square.Add(shard);
                }
            }
        }

        static int Square(float atM, int limit) => Math.Clamp((int)(atM / SquareM), 0, limit - 1);
    }
}
