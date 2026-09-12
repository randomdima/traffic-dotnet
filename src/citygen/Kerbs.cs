using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>The town's tarmac as one shape</b> — every carriageway, every line a car is turned through a box on,
/// every kerb fillet, every car park and every slab — answered as a single distance from a point, and
/// offered as the <b>lines that stand a given distance outside all of it</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>It exists so that what runs beside the road can be laid off the road</b> rather than off the
/// records the road was drawn from. A pavement offset from a road's centreline and pieced back together
/// at the junctions is a second description of the same edge, and the two disagree wherever the first one
/// was not the whole story — at a corner, where a car park merges into a street, at a dead end's head.
/// Offset from <em>this</em>, there is one description and the junctions are not a case (TER-3c.3).
/// </para>
/// <para>
/// <b>A junction is not a piece of it.</b> What a box is made of is the lines cars are driven through it on
/// (<see cref="LaneLines"/>) and the fillets that round the wedges between its arms — so a box that is
/// skewed, one-way, five-armed or barely a bend is wrapped by following those lines, and nothing here has
/// to know what shape they made (TER-5). <b>A dead end has none either</b>: what is there is the road that
/// stops, and a band ends where its own line does (TER-5a).
/// </para>
/// <para>
/// <b>A wrapping line is a candidate and not an answer.</b> Each is the outward offset of one piece, so
/// it stands the asked-for distance from <em>that</em> piece and says nothing about the rest — where two
/// pieces merge, each one's line runs on into the other's tarmac. What makes the set an outline is
/// keeping only the stations no piece stands nearer to than the offset (<see cref="OffTheTarmacM"/>):
/// two lines then give way to one another at the point they cross, which is the point both are the
/// offset distance from both pieces.
/// </para>
/// <para>
/// <b>And a piece is not always the outside of the tarmac.</b> A line a car is turned through a box on is
/// tarmac that the arms enclose, so what stands the offset outside <em>it</em> can stand the offset outside
/// everything else as well and still be a line up the middle of the pavement. Such a piece offers its line
/// only where the kerb is open (<see cref="Piece.WalkedPast"/>), and the caller is handed the town's own
/// kerb first so that it knows (TER-3c.5).
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
    public static Kerbs Of(GroundPieces plan, LaneLines lanes, BayLines bays, float bayWidthM, bool[] through)
    {
        var pieces = Lay(plan, lanes, bays, bayWidthM, through);
        var shards = Shatter(pieces);
        return new Kerbs(pieces, shards, new ShardGrid(pieces, shards, plan.WorldSizeM));
    }

    /// <summary>
    /// <b>How far a point stands off the nearest tarmac</b>, negative within it, and <see cref="ReachM"/>
    /// where nothing is near enough to have an opinion.
    /// </summary>
    /// <remarks>
    /// <b>A wrapping line stands the offset from its own piece exactly, and often from a second piece as
    /// well</b> — a line through a box leaves a lane at that lane's own width, so its band and the road's run
    /// edge to edge, and the line that wraps one runs half a walk from <em>both</em> of them for as far as
    /// they touch. Which side of the offset a tie like that falls on is the last bit of a float, so whoever
    /// compares this against the offset has to allow a rounding either way (<c>FootGraph.Clear</c>);
    /// compared exactly, six metres of the apron round such a junction were read as carriageway and no
    /// pavement was laid on them.
    /// </remarks>
    public float OffTheTarmacM(Vector2 pointM)
    {
        return Nearest(pointM);
    }

    /// <summary>
    /// <b>How far a point stands off the ground the town is driven <em>along</em></b>, negative within it:
    /// the band every road, every movement and every bay way lays, and <b>nothing that was paved around
    /// them</b> — not a junction's corner apron and not a car park's slab.
    /// </summary>
    /// <remarks>
    /// It is the shape a perimeter said in the lines is the outside of (<see cref="LaneShell"/>), and the
    /// two things left out are why there are two readings of one tarmac. A <b>fillet</b> stands outside the
    /// corner where two roads' kerbs cross, so counted in, the outside leaves the lanes at every mouth and
    /// no lane section is left to carry it round; left out, the two arms' own bands meet at that crossing
    /// point and the boundary goes round the junction on the lanes themselves. A <b>slab</b> is paved under
    /// a whole car park, so counted in, its bays are buried and the outside of one is the slab's own edge,
    /// which no car is driven along.
    /// </remarks>
    public float OffTheDrivenM(Vector2 pointM)
    {
        var nearestM = ReachM;
        foreach (var shard in _grid.At(pointM))
        {
            var piece = _pieces[_shards[shard].Piece];
            if (piece.Kind != Kind.Band) continue;

            nearestM = MathF.Min(nearestM, DistanceM(piece, _shards[shard].Arc, pointM));
        }

        return nearestM;
    }

    /// <summary>
    /// How far a point stands off the nearest piece of the town's tarmac, and <see cref="ReachM"/> where
    /// none is near.
    /// </summary>
    float Nearest(Vector2 pointM)
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
    /// <b>One piece of one piece</b>: the unit the index bins and a distance is measured against. Every
    /// kind but a road is one of these whole; <b>a road is one per arc</b>, because a street's own box is
    /// most of a district and asking a point about it means walking every bend the street ever takes.
    /// Shattered, the index names the two or three arcs that can possibly be nearest.
    /// </summary>
    readonly record struct Shard(int Piece, int Arc);

    static List<Shard> Shatter(List<Piece> pieces)
    {
        var shards = new List<Shard>();
        for (var piece = 0; piece < pieces.Count; piece++)
        {
            if (pieces[piece].Kind != Kind.Band)
            {
                shards.Add(new Shard(piece, -1));
                continue;
            }

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

    static float Bearing(Vector2 alongM) => MathF.Atan2(alongM.Y, alongM.X);

    /// <summary>
    /// How far a point stands outside one piece of tarmac, negative within it.
    /// </summary>
    /// <remarks>
    /// <b>A fillet is answered as its own arc</b> and not as the wedge behind it: the wedge's other two
    /// edges are the two kerbs the arc is tangent to, and those are the roads' own to answer for. What it
    /// still has to know is <em>where the fillet stops</em>, because the wedge outside the arc runs on for
    /// ever and the tarmac does not — it ends where the two kerbs cross, which is the corner
    /// (<see cref="Piece.SpanM"/>). Left unbounded, a point twenty metres past a junction read as twenty
    /// metres inside it and every wrapping line near one was cut away.
    /// </remarks>
    static float DistanceM(in Piece piece, int arc, Vector2 pointM)
    {
        switch (piece.Kind)
        {
            case Kind.Fillet:
                var offM = pointM - piece.CentreM;
                var radialM = offM.Length();
                var sweptRad = Spline.WrapRad(Bearing(offM) - piece.HalfM.X);
                if (MathF.Abs(sweptRad) > MathF.Abs(piece.HalfM.Y) || sweptRad * piece.HalfM.Y < 0f)
                {
                    return MathF.Min((pointM - piece.TangentAM).Length(), (pointM - piece.TangentBM).Length());
                }

                // Within the wedge the arc is the whole of the near edge, so the distance to the fillet is
                // the distance to the arc — inward as far as the centre, outward as far as the corner.
                return radialM > piece.SpanM ? radialM - piece.SpanM : piece.RadiusM - radialM;

            case Kind.Box:
                var local = new Vector2(
                    Vector2.Dot(pointM - piece.CentreM, piece.Axis),
                    Vector2.Dot(pointM - piece.CentreM, Heading.RightOf(piece.Axis)));
                var outsideM = Vector2.Abs(local) - piece.HalfM;
                return Vector2.Max(outsideM, Vector2.Zero).Length() + MathF.Min(MathF.Max(outsideM.X, outsideM.Y), 0f);

            default:
                var one = piece.Arcs.Span.Slice(arc, 1);
                var alongM = Spline.ProjectM(one, pointM, 0f, float.MaxValue);
                var on = Spline.SampleAt(one, alongM);
                var offsetM = pointM - on.PositionM;
                var asideM = MathF.Abs(Vector2.Dot(offsetM, on.Right)) - piece.HalfM.X;

                // <b>A band ends where its line does</b> (TER-7a). Measured radially from the last
                // station it would end in a half-disc of its own half-width instead, and a line a car is
                // turned on — which begins in the middle of the lane it leaves — then carried a bulge of
                // tarmac half a lane past that point, out under the pavement corner beside it. What that
                // ate was the corner: the fillet's own wrapping line was cut where the bulge reached it and
                // the pavement came apart at the mouth. It is also what <c>GroundShapes</c> has always
                // answered, so the two readings of one shape now agree.
                var beyondM = alongM <= 0f || alongM >= one[0].LengthM
                    ? MathF.Abs(Vector2.Dot(offsetM, on.Direction))
                    : 0f;
                if (beyondM <= 0f) return asideM;

                var outM = MathF.Max(asideM, 0f);
                return MathF.Sqrt((outM * outM) + (beyondM * beyondM));
        }
    }

    /// <summary>
    /// Every piece the tarmac is made of. <b>Nothing here is grown by anything</b>: it is the ground a car
    /// drives on at the size it is drawn, and what stands beside it is the caller's offset to ask for.
    /// </summary>
    static List<Piece> Lay(GroundPieces plan, LaneLines lanes, BayLines bays, float bayWidthM, bool[] through)
    {
        var pieces = new List<Piece>();
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            var arcs = plan.Roads.SegmentsOf(road);
            if (arcs.Length == 0) continue;

            pieces.Add(Piece.Band(arcs.ToArray(), plan.Roads.WidthM[road] * 0.5f));
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

            pieces.Add(Piece.Band(
                arcs.ToArray(), lanes.ConnectorWidthM(connector) * 0.5f));
        }

        var corners = plan.JunctionCorners;
        for (var corner = 0; corner < corners.Count; corner++)
        {
            pieces.Add(Piece.Fillet(
                corners.ArcCentreM[corner], corners.RadiusM[corner], corners.CornerM[corner],
                corners.TangentAM[corner], corners.TangentBM[corner]));
        }

        // <b>A car park is the movements that reach into it</b> (<see cref="BayLines"/>, GEN-4b) and has no
        // shape of its own, exactly as a junction is the movements that cross in it. Unlike those, a bay's
        // way runs <em>out</em> of the road rather than between two arms of it, so its own line is the
        // outside of the tarmac wherever nothing else stands nearer — which is the far end of every space.
        foreach (var way in bays.GroundWays)
        {
            var arcs = bays.ArcsOf(way);
            if (arcs.Length == 0) continue;

            pieces.Add(Piece.Band(arcs.ToArray(), bayWidthM * 0.5f));
        }

        var areas = plan.PavedAreas;
        for (var area = 0; area < areas.Count; area++)
        {
            pieces.Add(Piece.Box(
                areas.MinM[area] + (areas.SizeM[area] * 0.5f), Vector2.UnitX, areas.SizeM[area] * 0.5f));
        }

        return pieces;
    }

    enum Kind : byte
    {
        Band,
        Fillet,
        Box,
    }

    /// <summary>
    /// One piece of tarmac. <see cref="HalfM"/> carries what each kind is measured by — a band's
    /// half-width, a box's half-extent, and a fillet's arc as the bearing it starts at and the angle it
    /// sweeps — so one distance function serves all four.
    /// </summary>
    readonly record struct Piece(
        Kind Kind, Vector2 CentreM, Vector2 Axis, Vector2 HalfM, float RadiusM, float SpanM, Vector2 TangentAM,
        Vector2 TangentBM, ReadOnlyMemory<ArcSeg> Arcs)
    {
        public static Piece Band(ArcSeg[] arcs, float halfWidthM) =>
            new(Kind.Band, Vector2.Zero, Vector2.UnitX, new Vector2(halfWidthM), 0f, 0f, Vector2.Zero,
                Vector2.Zero, arcs);

        /// <summary><see cref="SpanM"/> is how far the corner stands from the arc's centre, which is how far out the wedge is tarmac.</summary>
        public static Piece Fillet(
            Vector2 arcCentreM, float radiusM, Vector2 cornerM, Vector2 tangentAM, Vector2 tangentBM)
        {
            var fromRad = Bearing(tangentAM - arcCentreM);
            var sweepRad = Spline.WrapRad(Bearing(tangentBM - arcCentreM) - fromRad);
            return new(
                Kind.Fillet, arcCentreM, Vector2.UnitX, new Vector2(fromRad, sweepRad), radiusM,
                (cornerM - arcCentreM).Length(), tangentAM, tangentBM, default);
        }

        public static Piece Box(Vector2 centreM, Vector2 axis, Vector2 halfM) =>
            new(Kind.Box, centreM, axis.LengthSquared() > 0f ? Vector2.Normalize(axis) : Vector2.UnitX, halfM, 0f,
                0f, Vector2.Zero, Vector2.Zero, default);
    }

    /// <summary>
    /// Which shards reach within <see cref="ReachM"/> of which square of the town, so a distance costs a
    /// handful of functions rather than every piece of tarmac in the city.
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
                if (piece.Kind != Kind.Band)
                {
                    Fill(shard, Box(piece));
                    continue;
                }

                var arc = piece.Arcs.Span[shards[shard].Arc];
                var reachM = new Vector2(piece.HalfM.X + ReachM);
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

        static (Vector2 LeastM, Vector2 MostM) Box(in Piece piece)
        {
            var reachM = piece.Kind switch
            {
                // A fillet reaches from its arc out to the corner the two kerbs cross at, and it is that
                // and not the arc's own radius that says how far from the centre it can be met.
                Kind.Fillet => new Vector2(MathF.Max(piece.RadiusM, piece.SpanM) + ReachM),
                _ => new Vector2(
                        (MathF.Abs(piece.Axis.X) * piece.HalfM.X) + (MathF.Abs(piece.Axis.Y) * piece.HalfM.Y),
                        (MathF.Abs(piece.Axis.Y) * piece.HalfM.X) + (MathF.Abs(piece.Axis.X) * piece.HalfM.Y))
                    + new Vector2(ReachM),
            };

            return (piece.CentreM - reachM, piece.CentreM + reachM);
        }

        static int Square(float atM, int limit) => Math.Clamp((int)(atM / SquareM), 0, limit - 1);
    }
}
