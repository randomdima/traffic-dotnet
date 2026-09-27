using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Foot;

/// <summary>
/// <b>The driven ground's own boundary, indexed so a place can be dropped onto it and walked along it</b>
/// (WLK-2, WLK-9, TER-7b): the merge of every ribbon the town's lines lay (<see cref="LaneShell"/>), which is
/// where the tarmac really stops.
/// </summary>
/// <remarks>
/// <b>A road's own half-width is not that line</b>, and near a junction it is not even close: the ground a
/// box's movements are driven over reaches past every arm's edge, so a kerb placed at half a carriageway
/// stands inside the tarmac at every mouth in the town. The boundary is the one shape that has already
/// merged all of it (TER-3c.8), so it is what a pedestrian node is placed off and what its connection points
/// are struck from.
/// </remarks>
internal sealed class KerbLines
{
    /// <summary>
    /// <b>One place on the boundary</b>: which of its lines, and how far along that line. It is what a step
    /// leaves behind (<see cref="Along"/>) and what a stretch of boundary is asked for between
    /// (<see cref="Between"/>).
    /// </summary>
    /// <remarks>
    /// <b>A place and not a point.</b> Two points a metre apart can stand on two different lines — the two
    /// sides of a street are two blocks' rings — and nothing laid along the boundary between them would be
    /// along anything. Reading the line back off the point would ask the question a second time and get a
    /// second answer.
    /// </remarks>
    public readonly record struct Station(int Line, float AlongM)
    {
        /// <summary>What a point that reached no boundary at all stands at.</summary>
        public static readonly Station Nowhere = new(-1, 0f);

        /// <summary>Whether the two stand on one line, which is the whole of what can be walked between.</summary>
        public bool SameLineAs(Station other) => Line >= 0 && Line == other.Line;
    }

    readonly ArcSeg[] _pieces;
    readonly ChainIndex _index;

    /// <summary>Which line each piece belongs to, and how far along that line the piece begins.</summary>
    /// <remarks>
    /// <b>It is what makes a step along the boundary a step along the boundary</b> rather than along the one
    /// arc a query happened to land in: a fillet at a mouth is a couple of metres of arc, so a setback
    /// measured inside it would run off the end of the piece and stop there.
    /// </remarks>
    readonly int[] _lineOfPiece;

    readonly float[] _startOfPieceM;
    readonly int[] _lineOffsets;
    readonly float[] _lineLengthM;

    /// <summary>Whether a line closes on itself, which is the whole of what decides a step past its end.</summary>
    readonly bool[] _lineCloses;

    KerbLines(
        ArcSeg[] pieces, ChainIndex index, int[] lineOfPiece, float[] startOfPieceM, int[] lineOffsets,
        float[] lineLengthM, bool[] lineCloses)
    {
        _pieces = pieces;
        _index = index;
        _lineOfPiece = lineOfPiece;
        _startOfPieceM = startOfPieceM;
        _lineOffsets = lineOffsets;
        _lineLengthM = lineLengthM;
        _lineCloses = lineCloses;
    }

    /// <summary>
    /// The town's boundary as the lines it is, closed rings and open runs alike — an open run is still a
    /// kerb, and a point reaching one has still reached the edge of the road.
    /// </summary>
    /// <remarks>
    /// <b>Indexed a piece at a time and not a ring at a time</b> (<see cref="ChainIndex.OfPieces"/>): a ring
    /// is the whole outside of a block, so a query that took the ring would walk a kilometre of arcs to find
    /// the two metres of it the question is about. <b>Which ring a piece came off is kept beside it</b>, so
    /// the cheap query still answers a question about the whole line.
    /// </remarks>
    public static KerbLines Of(CityPlan plan, SimConfig config) =>
        Of(plan.Paving(config).Perimeter(config), config);

    /// <summary>The same, off a merge somebody already holds.</summary>
    public static KerbLines Of(BandShell shell, SimConfig config) =>
        Of(shell.Chains, shell.Loose, config);

    /// <summary>
    /// And the same off any set of lines at all — <b>which is what lets the walk's own courses be read the
    /// way the boundary is</b> (<see cref="WalkLines"/>), the boundary moved off itself being a set of closed
    /// rings and whatever the move could not close, exactly as the merge that made it is.
    /// </summary>
    public static KerbLines Of(
        ReadOnlySpan<ArcSeg[]> rings, ReadOnlySpan<ArcSeg[]> runs, SimConfig config)
    {
        var pieces = new List<ArcSeg>();
        var lineOfPiece = new List<int>();
        var startOfPieceM = new List<float>();
        var lineOffsets = new List<int> { 0 };
        var lineLengthM = new List<float>();
        var lineCloses = new List<bool>();

        foreach (var ring in rings) Take(ring, closes: true);

        foreach (var run in runs) Take(run, closes: false);

        return new KerbLines(
            [.. pieces], ChainIndex.OfPieces([.. pieces], config.Grid.Main), [.. lineOfPiece],
            [.. startOfPieceM], [.. lineOffsets], [.. lineLengthM], [.. lineCloses]);

        void Take(ReadOnlySpan<ArcSeg> line, bool closes)
        {
            if (line.Length == 0) return;

            var alongM = 0f;
            foreach (var arc in line)
            {
                lineOfPiece.Add(lineLengthM.Count);
                startOfPieceM.Add(alongM);
                pieces.Add(arc);
                alongM += arc.LengthM;
            }

            lineLengthM.Add(alongM);
            lineCloses.Add(closes);
            lineOffsets.Add(pieces.Count);
        }
    }

    /// <summary>Where the boundary passes nearest a place, and which way it runs there.</summary>
    public bool NearestTo(Vector2 pointM, out SplineSample at) => Along(pointM, 0f, out at);

    /// <summary>The same, with the place it landed at (<see cref="Station"/>) as well as the line there.</summary>
    public bool NearestTo(Vector2 pointM, out SplineSample at, out Station station) =>
        Along(pointM, 0f, out at, out station);

    /// <summary>
    /// <b>Where the boundary stands a given distance along itself from the place nearest a point</b>
    /// (WLK-9), and which way it runs there — the whole line walked, and not the one arc the point landed in.
    /// </summary>
    /// <remarks>
    /// <b>A ring wraps and a run stops.</b> A closed ring is the outside of a block and has no end to walk
    /// off, so a step past its start comes round the other side; an open run really does end, and a step past
    /// the end of one is held at it rather than answered from the wrong line.
    /// </remarks>
    public bool Along(Vector2 pointM, float stepM, out SplineSample at) =>
        Along(pointM, stepM, out at, out _);

    /// <summary>The same, with the place it landed at (<see cref="Station"/>) as well as the line there.</summary>
    public bool Along(Vector2 pointM, float stepM, out SplineSample at, out Station station)
    {
        var piece = _index.Nearest(pointM, out var alongPieceM);
        if (piece < 0)
        {
            at = default;
            station = Station.Nowhere;
            return false;
        }

        var line = _lineOfPiece[piece];
        var lengthM = _lineLengthM[line];
        var stepped = _startOfPieceM[piece] + alongPieceM + stepM;
        var alongM = _lineCloses[line] ? Wrapped(stepped, lengthM) : Math.Clamp(stepped, 0f, lengthM);

        station = new Station(line, alongM);
        at = Spline.SampleAt(Of(line), alongM);
        return true;
    }

    /// <summary>
    /// <b>The stretch of one line between two of its own places</b> (WLK-11) — nothing moved and nothing
    /// fitted, just the pieces that lie between them, cut at each end.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The shorter of the two ways round a closed line</b>, and it is not a tie-break: the long way round
    /// a block is the rest of the town, so a pavement laid down it would be every street but the one asked
    /// about. What that rule picks is the stretch a walker would take — along the street between two of its
    /// corners, round the corner between two arms of a junction, round the head of a cul-de-sac between the
    /// two sides of it.
    /// </para>
    /// <para>
    /// <b>Walked forward whichever end it starts from.</b> Two places on two different lines have nothing
    /// between them and answer with nothing at all — which on a walk's own course
    /// (<see cref="WalkLines"/>) is the whole of what says the two ends cannot be joined along it.
    /// </para>
    /// </remarks>
    public ArcSeg[] Between(Station from, Station onto)
    {
        if (!from.SameLineAs(onto)) return [];

        var lengthM = _lineLengthM[from.Line];
        return _lineCloses[from.Line]
            ? Cut(from.Line, TheShortWayRound(from.AlongM, onto.AlongM, lengthM))
            : Cut(from.Line, (MathF.Min(from.AlongM, onto.AlongM), MathF.Abs(onto.AlongM - from.AlongM)));
    }

    /// <summary>
    /// <b>The same, and of the two arcs of a closed line the one running nearer a third place</b> — which is
    /// how a way that is read on more than one line says the same thing on all of them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Which side of a block a way runs down cannot be settled twice.</b> Each lane of a pavement is read
    /// on a course of its own, cut against the whole town at its own distance (<see cref="WalkLines"/>), so
    /// two lanes of one way stand at two arc lengths round two differently cut rings — and the shorter of the
    /// two arcs is not always the same side on both. Asked which of them runs nearer a place the way is known
    /// to pass, both lanes come back down the same street.
    /// </para>
    /// <para>
    /// <b>Weighed on where the two arcs run and not on which of them contains the place.</b> Containment asks
    /// the place which arc it stands on, which is a question about the line nearest <em>it</em> — and where a
    /// corner has been swallowed the nearest line is across the road, so the answer is the whole ring but the
    /// corner. The two arcs' own middles are a stride apart and a block apart from the place, so nothing
    /// hangs on a reading either of them could get wrong.
    /// </para>
    /// </remarks>
    public ArcSeg[] Between(Station from, Station onto, Vector2 nearM)
    {
        if (!from.SameLineAs(onto)) return [];
        if (!_lineCloses[from.Line]) return Between(from, onto);

        var lengthM = _lineLengthM[from.Line];
        var forwardM = Wrapped(onto.AlongM - from.AlongM, lengthM);
        var one = (StartM: from.AlongM, SpanM: forwardM);
        var other = (StartM: onto.AlongM, SpanM: lengthM - forwardM);

        return Cut(from.Line, Nearer(from.Line, one, nearM) <= Nearer(from.Line, other, nearM) ? one : other);
    }

    /// <summary>How far one arc's own middle stands off a place, which is what picks between two of them.</summary>
    float Nearer(int at, (float StartM, float SpanM) arc, Vector2 nearM) =>
        Vector2.DistanceSquared(
            Spline.SampleAt(Of(at), Wrapped(arc.StartM + (arc.SpanM * 0.5f), _lineLengthM[at])).PositionM,
            nearM);

    /// <summary>One arc of one line as the chain it is, wrapped across the line's own start where it runs past it.</summary>
    ArcSeg[] Cut(int at, (float StartM, float SpanM) arc)
    {
        if (arc.SpanM <= LineTolerance.RoundingM) return [];

        var line = Of(at);
        var lengthM = _lineLengthM[at];
        var cut = new ArcSeg[line.Length * 2];
        var taken = Spline.SubChainInto(line, arc.StartM, MathF.Min(arc.StartM + arc.SpanM, lengthM), cut);
        if (arc.StartM + arc.SpanM > lengthM)
        {
            taken += Spline.SubChainInto(line, 0f, arc.StartM + arc.SpanM - lengthM, cut.AsSpan(taken));
        }

        return cut[..taken];
    }

    /// <summary>Which end of a closed line to set off from, and how far, to walk the shorter of its two arcs.</summary>
    static (float StartM, float SpanM) TheShortWayRound(float oneM, float otherM, float lengthM)
    {
        var forwardM = Wrapped(otherM - oneM, lengthM);
        return forwardM * 2f <= lengthM ? (oneM, forwardM) : (otherM, lengthM - forwardM);
    }

    ReadOnlySpan<ArcSeg> Of(int line) =>
        _pieces.AsSpan(_lineOffsets[line], _lineOffsets[line + 1] - _lineOffsets[line]);

    static float Wrapped(float alongM, float lengthM)
    {
        if (lengthM <= 0f) return 0f;

        var atM = alongM % lengthM;
        return atM < 0f ? atM + lengthM : atM;
    }
}
