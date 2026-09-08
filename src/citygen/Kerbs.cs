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

    Kerbs(List<Piece> pieces, List<Shard> shards, ShardGrid grid, int roads)
    {
        _pieces = pieces;
        _shards = shards;
        _grid = grid;
        Roads = roads;
    }

    /// <summary>
    /// How many of the pieces are the carriageways themselves. <b>They are laid first</b>
    /// (<see cref="Lay"/>), so a piece under this is a road's own band and one at or over it is a
    /// movement, a kerb fillet, a car park or a slab — which is what tells a run of the outline that wraps
    /// a road from one that wraps anything else.
    /// </summary>
    public int Roads { get; }

    /// <param name="through">
    /// The junctions a road runs through as one line (<see cref="RoadCuts.RunsThrough"/>), whose movements
    /// are no pieces of the tarmac's outline: every one of them lies inside the two arms it joins.
    /// </param>
    public static Kerbs Of(GroundPieces plan, LaneLines lanes, bool[] through)
    {
        var pieces = Lay(plan, lanes, through, out var roads);
        var shards = Shatter(pieces);
        return new Kerbs(pieces, shards, new ShardGrid(pieces, shards, plan.WorldSizeM), roads);
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
        var (outsideM, movementM) = Nearest(pointM);
        return MathF.Min(outsideM, movementM);
    }

    /// <summary>
    /// How far a point stands off the nearest piece that is the outside of the town's tarmac
    /// (<see cref="WalkedPast.Always"/>), and off the nearest line a car is turned through a box on
    /// (<see cref="WalkedPast.WhereTheKerbIsOpen"/>) — each <see cref="ReachM"/> where none is near.
    /// </summary>
    (float OutsideM, float MovementM) Nearest(Vector2 pointM, int except = CityPlan.NoRecord)
    {
        var outsideM = ReachM;
        var movementM = ReachM;
        foreach (var shard in _grid.At(pointM))
        {
            if (_shards[shard].Piece == except) continue;

            var piece = _pieces[_shards[shard].Piece];
            var distanceM = DistanceM(piece, _shards[shard].Arc, pointM);
            if (piece.WalkedPast == WalkedPast.WhereTheKerbIsOpen) movementM = MathF.Min(movementM, distanceM);
            else outsideM = MathF.Min(outsideM, distanceM);
        }

        return (outsideM, movementM);
    }

    /// <summary>
    /// <b>Whether a point stands the offset clear of every piece of tarmac</b> — the one rule the outline
    /// is cut by, asked of one point.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Asked with a rounding's grace and no more.</b> A wrapping line stands the offset from its own
    /// piece exactly, and where two pieces are tangent it stands the offset from both of them exactly
    /// (<see cref="OffTheTarmacM"/>) — so compared without the grace, whether metres of pavement exist is
    /// settled by the last bit of a float, and the apron round every junction whose movements run edge to
    /// edge with its arms came out bare. <b>And a rounding and not a tolerance</b>, because a tolerance ε
    /// lets a line that meets another <em>tangentially</em> run √(2·R·ε) past the point they cross: at five
    /// centimetres that is better than half a metre each, which is how the pavement came apart into a piece
    /// per corner the first time round.
    /// </para>
    /// <para>
    /// <b>Except that a movement within a centimetre of the offset is at the offset</b>
    /// (<see cref="JoinedM"/>). A movement out of a lane that fills its road runs along the road's own
    /// kerb, and a movement out of a bend or into a turn may stand a few millimetres out past it: read to
    /// a rounding, that cut the road's line wherever the movement stood out by more than a millimetre, and
    /// the movement's own line — offered only where it stands the same centimetre further out than the
    /// road's (<see cref="Shell"/>) — did not take over until it did. Between the two nothing stood, and
    /// the walking network broke there. One figure decides both, so one line or the other always stands.
    /// </para>
    /// </remarks>
    public bool Clear(Vector2 pointM, float outM)
    {
        var (outsideM, movementM) = Nearest(pointM);
        return outsideM >= outM - RoundingM && movementM >= outM - JoinedM;
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
    /// <b>The town's outline at a distance out</b>: every wrapping line cut to the runs of it that are
    /// really the outside, which are the spans where <b>no tarmac stands nearer than the offset</b> and
    /// <paramref name="allowed"/> — the ground's own veto, where the caller has one — says the run may
    /// stand there (TER-3c.3).
    /// </summary>
    /// <remarks>
    /// <b>One construction, three readers.</b> The line this hands back at half a walk is the middle of the
    /// pavement: it is what the walking lanes are laid on, what the pavement is drawn as a band about, and
    /// what the ground answers <em>sidewalk</em> within half a walk of. Cut here rather than three times,
    /// the concrete, the kerb line and the lane a walker follows cannot disagree about where the pavement is
    /// (TER-7).
    /// </remarks>
    public void Shell(float outM, float weldM, Func<Vector2, bool>? allowed, List<Wrap> into)
    {
        var wraps = new List<Wrap>();
        Wrapping(outM, wraps);

        // <b>A line offered only where the kerb is open stands only where the kerb is open</b>
        // (<see cref="WalkedPast.WhereTheKerbIsOpen"/>): where no piece that is the outside of the town
        // stands nearer than the offset — nor, to the figure that makes two lines one, exactly at it. A
        // movement out of a lane that fills its road has an edge on the road's own kerb, and its line there
        // stood on the road's line to the last bit of a float: kept, it was a second run of pavement over
        // the first through every box in the town, with a round at each end of it.
        //
        // <b>And so does the line that turns round a band's end</b>: an end that runs on into the next
        // arm of a street, or stands square against one across it, has its line exactly the offset from
        // that arm along its corners' turns, and kept on the tie those turns were runs of pavement round
        // an end nothing is open at — and the end was drawn as one that reaches the outline.
        Cut(
            wraps, weldM,
            (wrap, atM) => Stands(atM, outM, allowed)
                           && (wrap.OnlyWhereTheKerbIsOpen || wrap.End != Wrap.ASide
                               ? OffTheOutsideM(atM, wrap.Piece) > outM + JoinedM
                               : true),
            into);
    }

    /// <summary>
    /// How far a point stands off the nearest piece that is the outside of the town's tarmac
    /// (<see cref="WalkedPast.Always"/>) other than <paramref name="except"/>, and <see cref="ReachM"/> where
    /// none is near.
    /// </summary>
    float OffTheOutsideM(Vector2 pointM, int except) => Nearest(pointM, except).OutsideM;

    /// <summary>Every wrapping line cut to the spans <paramref name="kept"/> says are wanted of it.</summary>
    void Cut(List<Wrap> wraps, float weldM, Func<Wrap, Vector2, bool> kept, List<Wrap> into)
    {
        var spans = new List<(float FromM, float ToM)>();
        var run = new ArcSeg[2];
        foreach (var wrap in wraps)
        {
            var (piece, line, open, end) = wrap;
            var lengthM = Spline.TotalLengthM(line);
            if (lengthM <= 0f) continue;

            Spans(line, lengthM, weldM, atM => kept(wrap, atM), spans);
            if (run.Length < line.Length + 2) run = new ArcSeg[line.Length + 2];

            foreach (var (fromM, toM) in spans)
            {
                var arcs = Spline.SubChainInto(line, fromM, toM, run);
                if (arcs > 0) into.Add(new Wrap(piece, run.AsSpan(0, arcs).ToArray(), open, end));
            }
        }
    }

    /// <summary>
    /// The spans of one wrapping line that are the outline, as distances along it. <b>A line that is clear
    /// end to end comes back cut in two</b>: a circle round a dead end and a box round a car park close on
    /// themselves, and a run whose two ends are one point is a piece nothing can be stationed along.
    /// </summary>
    static void Spans(
        ReadOnlySpan<ArcSeg> line, float lengthM, float weldM, Func<Vector2, bool> kept,
        List<(float FromM, float ToM)> into)
    {
        into.Clear();

        var stations = Math.Max(1, (int)MathF.Ceiling(lengthM / StationM));
        var was = kept(line[0].StartM);
        var openedAtM = was ? 0f : -1f;

        // Walked arc by arc rather than projected station by station: a street's offset is a chain of a
        // hundred pieces and a thousand stations, and asking the chain where each of those metres is from
        // its own start again is the square of that. It is the same walk <see cref="Spline"/> would do,
        // done once.
        var arc = 0;
        var toArcM = 0f;
        for (var station = 1; station <= stations; station++)
        {
            var alongM = lengthM * station / stations;
            while (arc + 1 < line.Length && alongM > toArcM + line[arc].LengthM)
            {
                toArcM += line[arc].LengthM;
                arc++;
            }

            var stands = kept(line[arc].PointAtM(Math.Clamp(alongM - toArcM, 0f, line[arc].LengthM)));
            if (stands == was) continue;

            var edgeM = Crossing(line, kept, lengthM * (station - 1) / stations, alongM, stands);
            if (stands) openedAtM = edgeM;
            else Keep(into, openedAtM, edgeM, weldM);

            was = stands;
        }

        if (was) Keep(into, openedAtM, lengthM, weldM);

        // Nothing gave way anywhere along it, so it is a closed line and both its ends are the same point.
        if (into.Count == 1 && into[0].FromM <= 0f && into[0].ToM >= lengthM)
        {
            into[0] = (0f, lengthM * 0.5f);
            into.Add((lengthM * 0.5f, lengthM));
        }
    }

    /// <summary>
    /// One span, kept unless it is shorter than the weld the caller welds by — in which case its two ends
    /// are one place and what it would lay is a run from a point to itself.
    /// </summary>
    static void Keep(List<(float FromM, float ToM)> into, float fromM, float toM, float weldM)
    {
        if (fromM >= 0f && toM - fromM > weldM) into.Add((fromM, toM));
    }

    /// <summary>Where along the line the answer changed, bisected between the two stations it changed between.</summary>
    static float Crossing(
        ReadOnlySpan<ArcSeg> line, Func<Vector2, bool> kept, float wasM, float isM, bool standsAtIs)
    {
        for (var halving = 0; halving < BisectionRounds; halving++)
        {
            var middleM = (wasM + isM) * 0.5f;
            var atM = Spline.SampleAt(line, middleM).PositionM;
            if (kept(atM) == standsAtIs) isM = middleM;
            else wasM = middleM;
        }

        return (wasM + isM) * 0.5f;
    }

    /// <summary>Whether one metre of a wrapping line is the outline: nothing nearer, and nothing vetoing it.</summary>
    bool Stands(Vector2 atM, float outM, Func<Vector2, bool>? allowed) =>
        Clear(atM, outM) && (allowed is null || allowed(atM));

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
    /// One wrapping line, the piece of tarmac it stands that far outside, whether that piece is the
    /// outside of the town's tarmac or only the inside of a box (<see cref="Piece.WalkedPast"/>), and which
    /// end of a band it turns round — <c>0</c> the start, <c>1</c> the end, <see cref="ASide"/> for a line
    /// along a side or round anything but a band.
    /// </summary>
    public readonly record struct Wrap(int Piece, ArcSeg[] Line, bool OnlyWhereTheKerbIsOpen, int End = Wrap.ASide)
    {
        public const int ASide = -1;
    }

    /// <summary>Which road or which movement a band piece was laid from, in the plan's own numbering.</summary>
    public int IndexOf(int piece) => _pieces[piece].Index;

    /// <summary>
    /// <b>Every line that stands <paramref name="outM"/> outside one piece of the tarmac</b>: a road's
    /// own arcs offset both ways <b>and turned round each end of it</b> (TER-3c.6), a connector's the same,
    /// the arc round a kerb fillet, and the rounded box round a car park or a slab. <b>The corner of a box
    /// is turned on the offset itself</b>, which is what keeps the line the same distance out all the way
    /// round it.
    /// </summary>
    /// <remarks>
    /// <b>The town's own kerb comes first and what only fills its gaps comes after</b>, so a caller that
    /// keeps a line where nothing else runs has already seen everything that could stand in its way by the
    /// time it is asked (TER-3c.5).
    /// </remarks>
    public void Wrapping(float outM, List<Wrap> into)
    {
        Wrapping(outM, WalkedPast.Always, into);
        Wrapping(outM, WalkedPast.WhereTheKerbIsOpen, into);
    }

    void Wrapping(float outM, WalkedPast when, List<Wrap> into)
    {
        var offset = new ArcSeg[32];
        var open = when == WalkedPast.WhereTheKerbIsOpen;
        for (var at = 0; at < _pieces.Count; at++)
        {
            var piece = _pieces[at];
            if (piece.WalkedPast != when) continue;

            switch (piece.Kind)
            {
                case Kind.Band:
                    var arcs = piece.Arcs.Span;
                    if (offset.Length < arcs.Length) offset = new ArcSeg[arcs.Length];

                    foreach (var sideM in (ReadOnlySpan<float>)[piece.HalfM.X + outM, -(piece.HalfM.X + outM)])
                    {
                        Spline.OffsetInto(arcs, sideM, offset);
                        Runs(at, open, offset.AsSpan(0, arcs.Length), into);
                    }

                    var last = arcs[^1];
                    Runs(at, open, Ending(arcs[0].StartM, arcs[0].HeadingRad + MathF.PI, piece.HalfM.X, outM), into, 0);
                    Runs(at, open, Ending(last.EndM, last.HeadingAtRad(last.LengthM), piece.HalfM.X, outM), into, 1);
                    break;

                case Kind.Fillet:
                    // A fillet is turned on the pavement's side of its own arc, so the line that wraps it
                    // is the smaller circle and not the larger one. Under the offset there is no wrapping
                    // it: the corner is tighter than the walk is wide, and what stands round it is the
                    // ring and the two roads' own lines.
                    if (piece.RadiusM <= outM) break;

                    into.Add(new Wrap(
                        at, [Around(piece.CentreM, piece.RadiusM - outM, piece.TangentAM, piece.TangentBM)], open));
                    break;

                default:
                    into.Add(new Wrap(
                        at, Box(piece.CentreM, piece.Axis, piece.HalfM + new Vector2(outM), outM), open));
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
    static void Runs(int piece, bool open, ReadOnlySpan<ArcSeg> line, List<Wrap> into, int end = Wrap.ASide)
    {
        var from = 0;
        for (var arc = 0; arc <= line.Length; arc++)
        {
            var folded = arc < line.Length && line[arc].LengthM <= 0f;
            var breaks = arc == line.Length
                || folded
                || (arc > from && Vector2.Distance(line[arc - 1].EndM, line[arc].StartM) > JoinedM);
            if (!breaks) continue;

            if (arc > from) into.Add(new Wrap(piece, line[from..arc].ToArray(), open, end));

            from = folded ? arc + 1 : arc;
        }
    }

    /// <summary>
    /// How near two pieces have to end and start to be one line: a centimetre, which is the same figure
    /// the walking side calls one place (<c>WalkingNetwork.SamePlaceM</c>) and well under anything a
    /// reader could see.
    /// </summary>
    public const float JoinedM = 0.01f;

    /// <summary>
    /// <b>The line that stands <paramref name="outM"/> outside one end of a band</b> (TER-3c.6): a quarter
    /// turn about the corner the end makes with one side, the straight across, and the quarter turn about
    /// the other corner — which is what standing that far outside a square end (TER-7a) is.
    /// </summary>
    /// <remarks>
    /// It starts and finishes where the band's two side lines do, so the three of them are one line round
    /// the end of the band and a walk laid off them has nothing to bridge.
    /// </remarks>
    static ArcSeg[] Ending(Vector2 endM, float outwardRad, float halfM, float outM)
    {
        Heading.Frame(outwardRad, out var outward, out var across);
        var rightM = endM + (across * halfM);
        var leftM = endM - (across * halfM);

        return
        [
            Around(rightM, outM, rightM + (across * outM), rightM + (outward * outM)),
            new ArcSeg(rightM + (outward * outM), Bearing(-across), halfM * 2f, 0f),
            Around(leftM, outM, leftM + (outward * outM), leftM - (across * outM)),
        ];
    }

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
    static List<Piece> Lay(GroundPieces plan, LaneLines lanes, bool[] through, out int roads)
    {
        var pieces = new List<Piece>();
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            var arcs = plan.Roads.SegmentsOf(road);
            if (arcs.Length == 0) continue;

            pieces.Add(Piece.Band(arcs.ToArray(), plan.Roads.WidthM[road] * 0.5f, road));
        }

        // A movement through a box the road runs through as one line stands inside the two arms' own
        // bands, so it is not a piece of the outline: offered, its wrap stood exactly on the arms' where
        // the lane fills the road and laid a second run over theirs, with a round at each end of it.
        roads = pieces.Count;
        for (var connector = 0; connector < lanes.ConnectorCount; connector++)
        {
            var arcs = lanes.ArcsOfConnector(connector);
            if (arcs.Length == 0) continue;

            var junction = lanes.JunctionOfConnector(connector);
            if (junction != CityPlan.NoRecord && through[junction]) continue;

            pieces.Add(Piece.Band(
                arcs.ToArray(), lanes.ConnectorWidthM(connector) * 0.5f, connector, WalkedPast.WhereTheKerbIsOpen));
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
                WalkedPast.Never));
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
    /// <b>When a piece of tarmac offers the line that stands outside it</b> — which is not the same question
    /// as whether it is tarmac at all (TER-3c.5).
    /// </summary>
    enum WalkedPast : byte
    {
        /// <summary>
        /// Never: a slab is a place to walk rather than a thing to walk past, so it holds the pavement off
        /// itself without asking for a band of its own (TER-3c).
        /// </summary>
        Never,

        /// <summary>
        /// Always: a carriageway, the fillet that rounds a wedge between two of them, and a car park are
        /// the outside of the town's tarmac, and the outside is what the pavement wraps.
        /// </summary>
        Always,

        /// <summary>
        /// <b>Only where the kerb leaves a gap</b>: a line a car is turned through a box on runs
        /// <em>inside</em> the box, and what a box is walked round is the arms that meet at it. Offered
        /// unconditionally it lays a second pavement line beside the arm's own, half a metre off it — two
        /// lanes threaded between two, and a stub of dead-ended kerb at every mouth in the town.
        /// </summary>
        WhereTheKerbIsOpen,
    }

    /// <summary>
    /// One piece of tarmac. <see cref="HalfM"/> carries what each kind is measured by — a band's
    /// half-width, a box's half-extent, and a fillet's arc as the bearing it starts at and the angle it
    /// sweeps — so one distance function serves all four.
    /// </summary>
    /// <remarks>
    /// <b><see cref="WalkedPast"/> is what the town lays a walk beside</b>, and it is not everything the
    /// town paves (TER-3c).
    /// </remarks>
    readonly record struct Piece(
        Kind Kind, Vector2 CentreM, Vector2 Axis, Vector2 HalfM, float RadiusM, float SpanM, Vector2 TangentAM,
        Vector2 TangentBM, ReadOnlyMemory<ArcSeg> Arcs, WalkedPast WalkedPast = WalkedPast.Always,
        int Index = CityPlan.NoRecord)
    {
        /// <param name="index">The road's or the movement's number in the plan, for a reader that wants to know whose band this is.</param>
        public static Piece Band(ArcSeg[] arcs, float halfWidthM, int index, WalkedPast walkedPast = WalkedPast.Always) =>
            new(Kind.Band, Vector2.Zero, Vector2.UnitX, new Vector2(halfWidthM), 0f, 0f, Vector2.Zero,
                Vector2.Zero, arcs, walkedPast, index);

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

        public static Piece Box(
            Vector2 centreM, Vector2 axis, Vector2 halfM, WalkedPast walkedPast = WalkedPast.Always) =>
            new(Kind.Box, centreM, axis.LengthSquared() > 0f ? Vector2.Normalize(axis) : Vector2.UnitX, halfM, 0f,
                0f, Vector2.Zero, Vector2.Zero, default, walkedPast);
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
