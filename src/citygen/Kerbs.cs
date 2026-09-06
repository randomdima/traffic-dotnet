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

    public static Kerbs Of(GroundPieces plan, LaneLines lanes)
    {
        var pieces = Lay(plan, lanes);
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
        var nearestM = ReachM;
        foreach (var shard in _grid.At(pointM))
        {
            nearestM = MathF.Min(
                nearestM, DistanceM(_pieces[_shards[shard].Piece], _shards[shard].Arc, pointM));
        }

        return nearestM;
    }

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
    /// <b>Every line that stands <paramref name="outM"/> outside one piece of the tarmac</b>: a road's
    /// own arcs offset both ways, a connector's offset both ways, the arc round a kerb fillet, and the
    /// rounded box round a car park or a slab. <b>The corner of a box is turned on the offset itself</b>,
    /// which is what keeps the line the same distance out all the way round it.
    /// </summary>
    /// <summary>One wrapping line, and the piece of tarmac it stands that far outside.</summary>
    public readonly record struct Wrap(int Piece, ArcSeg[] Line);

    public void Wrapping(float outM, List<Wrap> into)
    {
        var offset = new ArcSeg[32];
        for (var at = 0; at < _pieces.Count; at++)
        {
            var piece = _pieces[at];
            if (!piece.Wrapped) continue;

            switch (piece.Kind)
            {
                case Kind.Band:
                    var arcs = piece.Arcs.Span;
                    if (offset.Length < arcs.Length) offset = new ArcSeg[arcs.Length];

                    foreach (var sideM in (ReadOnlySpan<float>)[piece.HalfM.X + outM, -(piece.HalfM.X + outM)])
                    {
                        Spline.OffsetInto(arcs, sideM, offset);
                        Runs(at, offset.AsSpan(0, arcs.Length), into);
                    }

                    break;

                case Kind.Fillet:
                    // A fillet is turned on the pavement's side of its own arc, so the line that wraps it
                    // is the smaller circle and not the larger one. Under the offset there is no wrapping
                    // it: the corner is tighter than the walk is wide, and what stands round it is the
                    // ring and the two roads' own lines.
                    if (piece.RadiusM <= outM) break;

                    into.Add(new Wrap(
                        at, [Around(piece.CentreM, piece.RadiusM - outM, piece.TangentAM, piece.TangentBM)]));
                    break;

                default:
                    into.Add(new Wrap(at, Box(piece.CentreM, piece.Axis, piece.HalfM + new Vector2(outM), outM)));
                    break;
            }
        }
    }

    /// <summary>
    /// An offset chain, cut into the runs of it that are still one line. <b>Offsetting joins a chain only
    /// where the chain it came from is smooth</b>: every piece moves sideways by the same figure, so where
    /// two of them meet at an angle their offsets meet at a gap of that angle times the offset, and where a
    /// piece bends tighter than the offset is wide it comes out inside out.
    /// </summary>
    /// <remarks>
    /// <b>Walked as if it were one line, a chain with such a gap in it lies about where its own metres
    /// are</b> — a station a quarter-metre from its end stood a metre and a half away — and everything laid
    /// off those metres inherits it. Cut here, each run is a line whose distance along it is where it says.
    /// </remarks>
    static void Runs(int piece, ReadOnlySpan<ArcSeg> line, List<Wrap> into)
    {
        var from = 0;
        for (var arc = 0; arc <= line.Length; arc++)
        {
            var folded = arc < line.Length && line[arc].LengthM <= 0f;
            var breaks = arc == line.Length
                || folded
                || (arc > from && Vector2.Distance(line[arc - 1].EndM, line[arc].StartM) > JoinedM);
            if (!breaks) continue;

            if (arc > from) into.Add(new Wrap(piece, line[from..arc].ToArray()));

            from = folded ? arc + 1 : arc;
        }
    }

    /// <summary>
    /// How near two pieces have to end and start to be one line: a centimetre, which is the same figure
    /// the walking side calls one place (<c>WalkingNetwork.SamePlaceM</c>) and well under anything a
    /// reader could see.
    /// </summary>
    const float JoinedM = 0.01f;

    /// <summary>The arc about a centre between the bearings of two points, the short way round.</summary>
    static ArcSeg Around(Vector2 centreM, float radiusM, Vector2 fromM, Vector2 toM)
    {
        var fromRad = Bearing(fromM - centreM);
        var sweepRad = Spline.WrapRad(Bearing(toM - centreM) - fromRad);
        var sign = sweepRad < 0f ? -1f : 1f;
        return new ArcSeg(
            centreM + (radiusM * Heading.Unit(fromRad)), fromRad + (sign * MathF.PI * 0.5f),
            radiusM * MathF.Abs(sweepRad), sign / radiusM);
    }

    /// <summary>An oriented box with its four corners turned on one radius, as a closed chain of eight pieces.</summary>
    static ArcSeg[] Box(Vector2 centreM, Vector2 axis, Vector2 halfM, float radiusM)
    {
        // Never nought, or the corner's own arc has no radius to be struck on. A box smaller than the
        // offset it is being grown by cannot happen — it is grown by that offset on the way in.
        var cornerM = MathF.Max(1e-3f, MathF.Min(radiusM, MathF.Min(halfM.X, halfM.Y)));
        var across = Heading.RightOf(axis);
        var straightM = halfM - new Vector2(cornerM);
        var baseRad = Bearing(axis);

        var arcs = new ArcSeg[8];
        var corners = new Vector2[4];
        for (var quarter = 0; quarter < 4; quarter++)
        {
            var alongSign = quarter is 0 or 3 ? 1f : -1f;
            var acrossSign = quarter is 0 or 1 ? 1f : -1f;
            corners[quarter] =
                centreM + (axis * (straightM.X * alongSign)) + (across * (straightM.Y * acrossSign));
        }

        for (var quarter = 0; quarter < 4; quarter++)
        {
            var atRad = baseRad + (MathF.PI * 0.5f * quarter);
            arcs[quarter * 2] = new ArcSeg(
                corners[quarter] + (cornerM * Heading.Unit(atRad)), atRad + (MathF.PI * 0.5f),
                cornerM * MathF.PI * 0.5f, 1f / cornerM);

            var fromM = corners[quarter] + (cornerM * Heading.Unit(atRad + (MathF.PI * 0.5f)));
            var toM = corners[(quarter + 1) % 4] + (cornerM * Heading.Unit(atRad + (MathF.PI * 0.5f)));
            var runM = toM - fromM;
            arcs[(quarter * 2) + 1] = new ArcSeg(fromM, Bearing(runM), runM.Length(), 0f);
        }

        return arcs;
    }

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
                return (Spline.SampleAt(one, alongM).PositionM - pointM).Length() - piece.HalfM.X;
        }
    }

    /// <summary>
    /// Every piece the tarmac is made of. <b>Nothing here is grown by anything</b>: it is the ground a car
    /// drives on at the size it is drawn, and what stands beside it is the caller's offset to ask for.
    /// </summary>
    static List<Piece> Lay(GroundPieces plan, LaneLines lanes)
    {
        var pieces = new List<Piece>();
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            var arcs = plan.Roads.SegmentsOf(road);
            if (arcs.Length == 0) continue;

            pieces.Add(Piece.Band(arcs.ToArray(), plan.Roads.WidthM[road] * 0.5f));
        }

        for (var connector = 0; connector < lanes.ConnectorCount; connector++)
        {
            var arcs = lanes.ArcsOfConnector(connector);
            if (arcs.Length == 0) continue;

            pieces.Add(Piece.Band(arcs.ToArray(), lanes.LaneWidthM[lanes.ConnectorToLane[connector]] * 0.5f));
        }

        var corners = plan.JunctionCorners;
        for (var corner = 0; corner < corners.Count; corner++)
        {
            pieces.Add(Piece.Fillet(
                corners.ArcCentreM[corner], corners.RadiusM[corner], corners.CornerM[corner],
                corners.TangentAM[corner], corners.TangentBM[corner]));
        }

        for (var lot = 0; lot < plan.ParkingLots.Count; lot++)
        {
            pieces.Add(Piece.Box(
                plan.ParkingLots.CentreM[lot], plan.ParkingLots.Axis[lot], plan.ParkingLots.HalfExtentM[lot]));
        }

        var areas = plan.PavedAreas;
        for (var area = 0; area < areas.Count; area++)
        {
            pieces.Add(Piece.Box(
                areas.MinM[area] + (areas.SizeM[area] * 0.5f), Vector2.UnitX, areas.SizeM[area] * 0.5f,
                wrapped: false));
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
    /// <remarks>
    /// <b><see cref="Wrapped"/> is what the town lays a walk beside</b>, and it is not everything the town
    /// paves (TER-3c): a slab is a place to walk rather than a thing to walk past, so it holds the
    /// pavement off itself without asking for a band of its own. Every other piece asks for one.
    /// </remarks>
    readonly record struct Piece(
        Kind Kind, Vector2 CentreM, Vector2 Axis, Vector2 HalfM, float RadiusM, float SpanM, Vector2 TangentAM,
        Vector2 TangentBM, ReadOnlyMemory<ArcSeg> Arcs, bool Wrapped = true)
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

        public static Piece Box(Vector2 centreM, Vector2 axis, Vector2 halfM, bool wrapped = true) =>
            new(Kind.Box, centreM, axis.LengthSquared() > 0f ? Vector2.Normalize(axis) : Vector2.UnitX, halfM, 0f,
                0f, Vector2.Zero, Vector2.Zero, default, wrapped);
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
