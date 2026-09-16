using System.Numerics;
using System.Runtime.InteropServices;

namespace TrafficSimulation.Core.Geometry;

/// <summary>
/// <b>A closed shape moved off itself</b>: every ring of its boundary offset by one distance
/// (<see cref="Spline.OffsetInto"/>), every corner between two pieces made good, and <b>everything the move
/// folded through itself cut away</b> — so what comes back is the boundary of the shape the move produced
/// and not a drawing of where its lines went.
/// </summary>
/// <remarks>
/// <para>
/// <b>Outward is the walker's left</b>, which is the hand a ring keeps its ground off
/// (<see cref="BandShell.Chains"/>). A negative distance is an inset and is the same construction with the
/// corners the other way round.
/// </para>
/// <para>
/// <b>An offset is a fact about the area and not about the lines.</b> Moving each piece and joining the
/// corners is only the first half: a shape has features narrower than the distance it is moved, and those
/// pieces land on top of one another. A notch two metres across offset three metres outward has walls that
/// cross; a courtyard smaller than the distance turns inside out; two rings four metres apart offset three
/// become one ring. <b>Drawn from the moved lines alone, every one of those is a bow-tie in the picture and
/// a lie about the shape.</b>
/// </para>
/// <para>
/// <b>What settles all of them is one question asked once</b>: a place is on the offset boundary only if
/// nothing of the original stands nearer to it than the distance moved. So every moved piece is cut at every
/// crossing anything has with it — the exact closed form two arcs have
/// (<see cref="Spline.CrossingsOf(in ArcSeg, in ArcSeg, Span{float}, Span{float})"/>) — and a stretch is
/// kept where the ground at its own middle is a clear distance off every ring it was taken from. A fold's
/// two halves each lie inside that distance and both go; a collapsed ring is inside it everywhere and comes
/// back as nothing at all; two outsets that ran together keep the outside of the pair and lose the two
/// arcs inside. <b>There is no case for any of them</b>, and what is left is strung into rings by the same
/// walk a merge uses (<see cref="ArcRings"/>).
/// </para>
/// <para>
/// <b>The smoothing is a pass over the answer and never over the working.</b> Rounding a corner moves the
/// line off the distance it was moved by, so a round cut before the fold test is a round the fold test
/// deletes; and a round cut into a fold is a round put on a line that is about to go. The offset is taken
/// exactly, the folds are cut, and only then are the corners of the rings that survived rounded.
/// </para>
/// <para>
/// <b>And what it names is a curvature and not a length</b> (<see cref="Smoothed"/>): <b>one radius, asked
/// of every corner of the answer alike</b>. A corner is the one place a line's curvature is unbounded, so
/// the whole of what a smoothing can say is how tightly the answer is allowed to turn — and a figure that
/// says it corner by corner, off the pieces each one happens to stand between, rounds a corner between long
/// pieces at fifty metres and the one next to it at half a metre. The line comes back continuous and just
/// as rugged as it went in, because <b>ruggedness is a curvature and it was never touched</b>.
/// </para>
/// <para>
/// <b>The radius is a share of the distance moved, because that is the curvature the answer already has.</b>
/// Every corner of the shape comes back as the arc of that distance about it (<see cref="Corner"/>) — the
/// tightest the offset is ever obliged to turn — so rounding at the same radius is what leaves the whole
/// line turning no tighter than part of it already did, and <b>one is the smoothest line there is at this
/// distance</b>.
/// </para>
/// <para>
/// <b>And it only ever goes outwards</b> (<see cref="Inside"/>). A line laid to keep off something may keep
/// further off than it was asked to and may never come nearer, which is what a kerb off a carriageway is;
/// and it happens to be the only thing the geometry allows in any case. <b>A corner turning away from the
/// shape cannot be rounded at all</b>: it is the arc of the distance already, and every curve that rounds it
/// further is a chord of that arc, standing nearer the shape the whole way across. What can be rounded is
/// the corners turning <em>in</em> — the notches a fold cut left — and the bends the move made tighter than
/// the shape's own, since a bend of radius <c>r</c> curving inwards offsets to <c>r−d</c> and can be as
/// tight as nothing at all. Filling either of those takes the line away from the shape, and <b>every fill is
/// weighed against the shape before it is kept</b>.
/// </para>
/// <para>
/// <b>The whole set of rings is offset at once and the count may change.</b> Rings that ran together come
/// back as one, a ring that collapsed comes back as none, and a ring pinched in two by the move comes back
/// as two — so nothing here maps a ring to a ring, and a caller that wanted to know which is which has
/// asked a question the shape does not have an answer to.
/// </para>
/// </remarks>
internal static class ArcOutset
{
    /// <summary>
    /// Below this a corner does not turn: the two pieces leave and arrive along one direction, so there is
    /// no mitre to take and nothing between them but whatever gap they left.
    /// </summary>
    const float StraightTurn = 1e-3f;

    /// <summary>
    /// <b>How near the original a piece of the moved line may stand and still be on the offset.</b> The
    /// arithmetic's own millimetre (<see cref="LineTolerance.RoundingM"/>): a line offset a distance and
    /// then measured back to what it was offset from reads a hair inside it, and a test asked strictly
    /// would delete the whole answer.
    /// </summary>
    /// <remarks>
    /// <b>It is a rounding and not a licence, and widening it is not how a hole is closed.</b> Read at a
    /// centimetre instead, a fold a centimetre deep is kept — and since nothing is cut where the reading
    /// changes, what is kept is a sliver of fold with an end in mid-air at each side of it. <b>How much of
    /// a graze the answer may be short is a separate figure and is the walk's</b> (<see cref="Grazed"/>).
    /// </remarks>
    const float SlackM = LineTolerance.RoundingM;

    /// <summary>
    /// <b>One closed shape moved <paramref name="outwardM"/> off its own ground</b>, and then every corner
    /// of the answer rounded at one radius, which is <paramref name="smoothing"/> of the answer's own grain
    /// — nought for the offset untouched and one for as round as the shape can be made, clamped to the two
    /// of them (<see cref="Smoothed"/>). <paramref name="rings"/> is the whole of the shape's boundary,
    /// every ring of it walked with the ground on its right.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The runs that would not close are handed back apart</b>, exactly as a merge hands its own back
    /// (<see cref="BandShell.Loose"/>): the boundary of a shape moved off itself is closed, so a run with
    /// two ends is a crossing this construction did not find rather than a shape the offset has.
    /// </para>
    /// <para>
    /// <b>A move of nothing is not smoothed either.</b> Both figures are the offset's: the radius is measured
    /// in the distance moved and the rounding is weighed against the shape it was moved off, so with no
    /// distance there is neither a radius nor anything to weigh.
    /// </para>
    /// </remarks>
    public static (ArcSeg[][] Rings, ArcSeg[][] Loose) Of(
        ReadOnlySpan<ArcSeg[]> rings, float outwardM, float smoothing)
    {
        if (rings.Length == 0) return ([], []);
        if (MathF.Abs(outwardM) <= LineTolerance.RoundingM) return (rings.ToArray(), []);

        // The pieces of a ring the stringing handed back already meet (<see cref="ArcRings.Tightened"/>);
        // this is for the caller that hands over a ring of its own, since a corner at exactly the distance
        // moved has no headroom for two vertices a centimetre apart.
        var tight = new ArcSeg[rings.Length][];
        for (var at = 0; at < rings.Length; at++) tight[at] = ArcRings.Tightened(rings[at]);

        var source = ArcRings.Flat(tight);
        var moved = ArcRings.Flat(Moved(tight, outwardM));
        if (moved.Length == 0 || source.Length == 0) return ([], []);

        // <b>The cell is the question's own radius.</b> Every query this makes asks what stands within the
        // distance moved, so a cell of that size is the one that answers it in a handful of cells — and the
        // index grows it by itself where a shape is spread too wide to bin that finely.
        var cellM = MathF.Abs(outwardM);
        var standing = ChainIndex.OfPieces(source, cellM);
        var (chains, loose) = ArcRings.Of(
            Uncovered(moved, ChainIndex.OfPieces(moved, cellM), source, standing, MathF.Abs(outwardM), outwardM),
            Grazed(outwardM));

        return (Smoothed(chains, Math.Clamp(smoothing, 0f, 1f), outwardM, source, standing), loose);
    }

    /// <summary>
    /// <b>Every finished ring with the corners that turn in on the shape rounded out</b>, at one radius —
    /// <paramref name="share"/> of the distance moved — which at no smoothing is the rings themselves.
    /// </summary>
    /// <remarks>
    /// <b>The radius is measured in the distance moved because that is the curvature the answer already
    /// has.</b> Every corner of the shape comes back as the arc of that radius about it
    /// (<see cref="Corner"/>), which is the tightest the offset itself ever turns; so a rounding at the same
    /// radius is the one that leaves the whole line turning no tighter than any part of it already did, and
    /// <b>one is the smoothest line there is at this distance</b>. A figure struck off anything else — the
    /// length of the pieces, the grain of the answer — rounds one corner at fifty metres and the next at
    /// half a metre, and the line comes back as rugged as it went in.
    /// </remarks>
    static ArcSeg[][] Smoothed(
        ArcSeg[][] chains, float share, float outwardM, ArcSeg[] source, ChainIndex standing)
    {
        var radiusM = share * MathF.Abs(outwardM);
        if (radiusM <= LineTolerance.RoundingM) return chains;

        var near = new int[source.Length];
        var alongM = new float[source.Length];
        for (var at = 0; at < chains.Length; at++)
        {
            var asked = radiusM;
            for (var pass = 0; pass < Passes && asked > LineTolerance.RoundingM; pass++, asked *= 0.5f)
            {
                chains[at] = Rounded(
                    chains[at], asked, outwardM, source, standing, MathF.Abs(outwardM), near, alongM);
            }
        }

        return chains;
    }

    /// <summary>
    /// <b>How many times a ring is offered a smaller radius for what the last one could not have</b>, each
    /// half the one before.
    /// </summary>
    /// <remarks>
    /// <b>Because a notch that will not take the radius asked for will take some radius, and refusing it
    /// outright is what leaves one corner sharp beside another that came out round.</b> The figure has to be
    /// worth more the higher it is asked, and a single pass is not: <b>a town's boundary kept eight hundred
    /// sharp notches at a radius of five metres and eighty at a radius of one</b>, because the bigger round
    /// reaches inside the shape more often and is refused for it. A pass that has already rounded a corner
    /// leaves no corner there, so the passes after it find nothing to do and cost a walk.
    /// </remarks>
    const int Passes = 3;

    /// <summary>
    /// <b>How much boundary one corner of the answer can be short</b>, which is what the walk that strings
    /// it has to be allowed to close across (<see cref="ArcRings.Of"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two moved lines that fold through each other by a hair run a long way before they part.</b> It is
    /// the tangential meeting <see cref="LineTolerance.OnePlaceM"/> is derived from, at this construction's
    /// own radius rather than a junction's: a corner turning through <c>θ</c> folds the line
    /// <c>d·(1−cos θ)</c> deep and carries its two ends <c>d·θ</c> apart, so <b>a fold no deeper than what
    /// the shape itself is drawn to</b> — a centimetre (<see cref="LineTolerance.JoinedM"/>) — <b>is
    /// <c>√(2·d·ε)</c> of boundary standing between two ends that are both exactly where they should
    /// be.</b> At five metres that is a third of a metre, and a town's boundary came back with sixteen holes
    /// of between a fifth and a third.
    /// </para>
    /// <para>
    /// <b>It is the figure the walk may close across and never one anything is moved by</b> — the hole stays
    /// exactly as wide as it is, which is what a ring already does at every piece a cut passed over.
    /// </para>
    /// </remarks>
    static float Grazed(float outwardM) =>
        MathF.Sqrt(2f * LineTolerance.JoinedM * MathF.Abs(outwardM)) + ArcRings.LeastLostM;

    /// <summary>
    /// <b>Every ring moved and its corners joined</b>, which is the offset before anything is asked about
    /// what it ran into — <b>the candidate set the cut is made over</b>.
    /// </summary>
    /// <remarks>
    /// <b>Named so that a reading can be taken of it</b> (<c>--bench outset</c>). Where the answer will not
    /// close, what a reader needs is what the cut was given rather than what it handed back: a hole is
    /// either a candidate nothing made or a candidate the cut deleted, and the two are not the same defect.
    /// </remarks>
    public static ArcSeg[][] Moved(ReadOnlySpan<ArcSeg[]> rings, float outwardM)
    {
        var moved = new ArcSeg[rings.Length][];
        for (var at = 0; at < rings.Length; at++) moved[at] = Moved(rings[at], outwardM);

        return moved;
    }

    /// <summary>
    /// One ring moved: <b>each piece offset whole</b>, the pieces the move turned inside out left behind,
    /// and a join put in at each corner that opens.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nothing is cut back here, and that is the whole of why the answer closes.</b> An offset piece
    /// stands exactly the distance off the piece it was taken from over the whole of its length, so it is
    /// both a candidate and <b>a cutter — the line every other candidate stops being on the offset at</b>.
    /// Trim one back at a corner first and the part taken off is a cutter the crossing pass
    /// (<see cref="Uncovered"/>) no longer has: a stretch of the ring a long way off ends mid-way through
    /// changing hands, is weighed at a middle that is on one side of the change, and is kept or dropped
    /// whole. <b>What that leaves is a hole a third of a metre wide with both of its ends standing at
    /// exactly the right distance</b>, which is what a town's boundary came back with — and each of those
    /// took thirty kilometres of ring open with it.
    /// </para>
    /// <para>
    /// <b>A piece whose own bend is tighter than the distance moved offsets to a length of nought or
    /// less</b> (<see cref="Spline.OffsetInto"/>) and is dropped here rather than carried as a piece that
    /// runs backwards. Every point within the distance of such a piece is within the distance of it
    /// everywhere, so there is no offset of it to draw; what it leaves is a gap between two pieces that
    /// never met, which <see cref="Closed"/> chords across for the fold test to judge.
    /// </para>
    /// </remarks>
    static ArcSeg[] Moved(ReadOnlySpan<ArcSeg> ring, float outwardM)
    {
        var run = new List<ArcSeg>(ring.Length * 3);
        var moved = new List<ArcSeg>(ring.Length);
        var at = new List<int>(ring.Length);

        Span<ArcSeg> one = stackalloc ArcSeg[1];
        for (var piece = 0; piece < ring.Length; piece++)
        {
            Spline.OffsetInto(ring.Slice(piece, 1), -outwardM, one);
            if (one[0].LengthM <= LineTolerance.RoundingM) continue;

            moved.Add(one[0]);
            at.Add(piece);
        }

        if (moved.Count == 0) return [];

        for (var piece = 0; piece < moved.Count; piece++)
        {
            run.Add(moved[piece]);
            var next = (piece + 1) % moved.Count;
            if (Corner(ring, at[piece], at[next], moved[piece], moved[next], outwardM, out var round))
            {
                run.Add(round);
            }
        }

        return Closed(run);
    }

    /// <summary>
    /// <b>What one corner puts between two moved pieces</b>, which is something only where the corner opens.
    /// The corner is read off the place the <em>original</em> ring turned at, and never off where the two
    /// moved pieces would cross if they were run on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Because the original corner is a point both ends stand the distance off, and the crossing of two
    /// tangents is not.</b> The arc about the corner stands at the distance along the whole of itself,
    /// exactly, whatever the two pieces are — while two nearly parallel pieces with a hair of gap between
    /// them cross a kilometre away, and a construction that read the corner off that crossing put a
    /// kilometre of line into the answer.
    /// </para>
    /// <para>
    /// <b>The arc and never a mitre, however slight the corner.</b> A mitre stands <c>d/cos(θ/2)</c> off the
    /// corner at its point — further than the distance asked for, and a sharper corner than anything the
    /// shape it came from had. <b>Both of those are faults in the one thing this construction is for</b>: a
    /// line to lay a kerb along has a curvature it can hold, and the arc about the corner is the tightest
    /// anything at this distance is ever obliged to turn (<see cref="Rounded"/>). What a mitre buys is a
    /// drawing convention for a stroked line, which is not what an offset is.
    /// </para>
    /// <para>
    /// <b>A corner that closes is left to close itself.</b> Its two moved pieces run past one another, so
    /// the place they meet is a crossing like every other crossing in the shape — found by the pass that
    /// finds all of them, cut once, and cut on both sides at the one solved point
    /// (<see cref="Uncovered"/>). <b>Solved here instead it is solved twice</b>: once as the corner and once
    /// as the fold, the two readings stop a hair apart, and the ring breaks between them. Where the two
    /// pieces are too short to reach each other, what stands between them is a chord
    /// (<see cref="Closed"/>) that the fold test deletes like anything else nearer the ring than the
    /// distance moved — <b>and the boundary that leaves short is the walk's to close over</b>
    /// (<see cref="Grazed"/>).
    /// </para>
    /// </remarks>
    static bool Corner(
        ReadOnlySpan<ArcSeg> ring, int from, int onto, in ArcSeg leaving, in ArcSeg taking, float outwardM,
        out ArcSeg round)
    {
        round = default;
        var turnRad = Turn(leaving, taking);

        // The two pieces met on the original only if nothing between them was dropped; otherwise there is
        // no corner to take at all, and the chord across the gap is what there is to say about it.
        var adjacent = (from + 1) % ring.Length == onto;
        if (!adjacent || MathF.Abs(turnRad) < StraightTurn) return false;
        if (Swung(ring[from], ring[onto]) * outwardM < 0f) return false;

        round = Round(leaving.EndM, leaving.HeadingAtRad(leaving.LengthM), turnRad, MathF.Abs(outwardM));
        return true;
    }

    /// <summary>
    /// The full round of a corner: the arc of the offset's own radius about the place the ring turned at,
    /// <b>every point of which stands exactly the distance off that place</b>.
    /// </summary>
    static ArcSeg Round(Vector2 fromM, float headingRad, float turnRad, float radiusM) =>
        new(fromM, headingRad, radiusM * MathF.Abs(turnRad), turnRad > 0f ? 1f / radiusM : -1f / radiusM);

    /// <summary>
    /// <b>Every moved piece cut at every crossing, and each stretch kept where nothing of the original
    /// stands nearer to it than the distance moved.</b> It is the whole of the fold removal and it has no
    /// case in it: a fold, a collapse and two rings running together are one answer to one question.
    /// </summary>
    /// <remarks>
    /// <b>Asked at the stretch's own middle, which is what the cutting is for.</b> Whether a place is inside
    /// the distance changes exactly where the moved lines cross, so a stretch between two crossings is
    /// inside along all of it or outside along all of it — and one station settles it. Asked of a piece that
    /// was not cut, the same station answers for a stretch that is half of each.
    /// </remarks>
    static List<ArcSeg> Uncovered(
        ArcSeg[] moved, ChainIndex crossing, ArcSeg[] source, ChainIndex standing, float keepM,
        float outwardM)
    {
        var cutAtM = new List<float>?[moved.Length];
        var candidate = new int[moved.Length];
        Span<float> here = stackalloc float[2];
        Span<float> there = stackalloc float[2];

        for (var at = 0; at < moved.Length; at++)
        {
            if (moved[at].LengthM <= ArcRings.LeastPieceM) continue;

            var offered = crossing.Crossing(moved.AsSpan(at, 1), 0f, candidate);
            for (var n = 0; n < offered && n < candidate.Length; n++)
            {
                // Each pair solved once and both sides cut by it: solved twice, the two readings of one
                // crossing stop a hair apart and the ring breaks there.
                var other = candidate[n];
                if (other <= at || moved[other].LengthM <= ArcRings.LeastPieceM) continue;

                var found = Spline.CrossingsOf(moved[at], moved[other], here, there);
                for (var cut = 0; cut < found; cut++)
                {
                    (cutAtM[at] ??= []).Add(here[cut]);
                    (cutAtM[other] ??= []).Add(there[cut]);
                }
            }
        }

        var kept = new List<ArcSeg>(moved.Length);
        var near = new int[source.Length];
        var alongM = new float[source.Length];
        var edges = new List<float>();
        for (var at = 0; at < moved.Length; at++)
        {
            if (moved[at].LengthM <= ArcRings.LeastPieceM) continue;

            cutAtM[at]?.Sort();
            Stretches(kept, moved[at], cutAtM[at], source, standing, keepM, outwardM, near, alongM, edges);
        }

        return kept;
    }

    /// <summary>
    /// <b>One moved piece split at its cuts, and each stretch of it weighed on its own</b> — the whole of
    /// the piece and not a part of it: <b>two cuts nearer to each other than the shortest walkable stretch
    /// are one cut</b>, and never a stretch to drop.
    /// </summary>
    /// <remarks>
    /// <b>Because dropping it is a hole, and a hole is what a ring cannot be walked across.</b> Two
    /// boundaries running along one another cross wherever the last bits of a float say they do, which is a
    /// handful of crossings a few centimetres apart rather than the one place they share
    /// (<see cref="ArcRings.LeastPieceM"/>); dropping the slivers between them leaves a gap as wide as the
    /// whole cluster, and a shipped city's boundary came back with one of those wider than what the walk
    /// closes across. <b>Coalesced instead, the cut is moved by a hair and no boundary is lost at all.</b>
    /// </remarks>
    static void Stretches(
        List<ArcSeg> kept, in ArcSeg piece, List<float>? cutAtM, ArcSeg[] source, ChainIndex standing,
        float keepM, float outwardM, int[] near, float[] alongM, List<float> edges)
    {
        edges.Clear();
        edges.Add(0f);
        var cuts = cutAtM?.Count ?? 0;
        for (var cut = 0; cut < cuts; cut++)
        {
            var atM = Math.Clamp(cutAtM![cut], 0f, piece.LengthM);
            if (atM - edges[^1] > ArcRings.LeastPieceM) edges.Add(atM);
        }

        // The piece's far end is an edge like any other, and a last stretch too short to be walked is the
        // cut before it that has to give way rather than the stretch itself.
        if (edges.Count > 1 && piece.LengthM - edges[^1] <= ArcRings.LeastPieceM) edges.RemoveAt(edges.Count - 1);

        edges.Add(piece.LengthM);
        for (var at = 1; at < edges.Count; at++)
        {
            var fromM = edges[at - 1];
            var toM = edges[at];
            if (Nearer(source, standing, piece.PointAtM((fromM + toM) * 0.5f), keepM, outwardM, near, alongM))
            {
                continue;
            }

            kept.Add(
                new ArcSeg(piece.PointAtM(fromM), piece.HeadingAtRad(fromM), toM - fromM, piece.Curvature));
        }
    }

    /// <summary>
    /// <b>Whether a place is inside the shape's offset rather than on it</b>: either something of the
    /// original stands nearer to it than the distance moved, <b>or the nearest thing to it has it on the
    /// wrong side</b> — the side the ground is, where a place may stand any distance off the boundary and
    /// still be in the middle of the shape.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The distance alone answers for a place outside the shape and says nothing about one inside
    /// it.</b> A slot four metres across and twenty deep, moved five, is filled in — and the arc the move
    /// puts round the far corner of that slot sweeps down into the solid ground behind it, where it is five
    /// metres off the slot's own floor and further than that from everything else. Read for distance alone
    /// that is on the offset; read for which side of the floor it stands, it is under it. <b>A mitre never
    /// showed this either</b>, because two straights leaving a corner stay in the wedge the corner opens and
    /// an arc about it does not.
    /// </para>
    /// <para>
    /// <b>And where the nearest thing is a corner, both pieces that meet there are asked and the readings
    /// are added</b> — the same reading a subtraction settles its own side with
    /// (<see cref="ArcSubtract"/>). A place off a sharp corner stands on the ground side of one of the two
    /// pieces and on the far side of the other, so either alone is a coin toss and the sum is the corner's
    /// own bisector. <b>It is the whole of what a corner round is weighed by</b>: the round the move puts
    /// about a corner the shape turns the other way stands exactly the distance off the one point it turns
    /// about, so every place on it is that tie and nothing else decides it.
    /// </para>
    /// </remarks>
    static bool Nearer(
        ArcSeg[] source, ChainIndex standing, Vector2 pointM, float keepM, float outwardM, int[] near,
        float[] alongM)
    {
        var insideM = keepM - SlackM;
        var found = standing.Near(pointM, keepM, near, alongM);
        if (found > near.Length) found = near.Length;

        var nearestM = float.PositiveInfinity;
        for (var at = 0; at < found; at++)
        {
            ref readonly var piece = ref source[near[at]];
            var atM = Math.Clamp(alongM[at], 0f, piece.LengthM);
            var offM = Vector2.DistanceSquared(piece.PointAtM(atM), pointM);
            if (offM < insideM * insideM) return true;

            nearestM = MathF.Min(nearestM, offM);
        }

        if (found == 0) return false;

        // Two pieces meeting at a corner compute the one point they share through their own arithmetic, so
        // what is one distance comes back as two that differ in the last bits of a float.
        var tiedM = MathF.Sqrt(nearestM) + LineTolerance.RoundingM;
        var sideM = 0f;
        for (var at = 0; at < found; at++)
        {
            ref readonly var piece = ref source[near[at]];
            var atM = Math.Clamp(alongM[at], 0f, piece.LengthM);
            var ontoM = piece.PointAtM(atM);
            if (Vector2.Distance(ontoM, pointM) > tiedM) continue;

            sideM += Vector2.Dot(pointM - ontoM, Heading.RightOf(Heading.Unit(piece.HeadingAtRad(atM))));
        }

        return sideM * outwardM > 0f;
    }

    /// <summary>
    /// <b>The corners of one finished ring rounded at <paramref name="radiusM"/></b>: every corner cut back
    /// by the tangent its own turn asks for at that radius, and <b>the two places the cut left</b> joined by
    /// the arcs that leave and arrive along them (<see cref="Spline.BiarcInto"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The radius is the figure and the trim is worked out from it</b>, which is what makes the answer
    /// one curvature all the way round rather than one length: a corner of ten degrees is cut back a tenth
    /// of what a right angle is, and both come back turning at the same radius. The other way about — a trim
    /// the caller names — is a different radius at every corner, and the sharper the corner the tighter it
    /// comes out, which is ruggedness rewritten rather than rounded.
    /// </para>
    /// <para>
    /// <b>The same amount either side, because a corner cut back further on one side than the other is not
    /// a corner being rounded</b> — the biarc across an uneven pair turns out of the corner and back into
    /// it, so a right angle between a long piece and a short one comes out as a swing of a hundred and
    /// thirty degrees, which is then refused for turning further than what it replaces and leaves the corner
    /// untouched. Struck evenly, a corner between two straights is exactly one arc through the turn and
    /// nothing else.
    /// </para>
    /// <para>
    /// <b>And where two corners want the same piece, they are one corner</b> (<see cref="Swallowed"/>). A
    /// piece its two ends have claimed away is taken whole, and what fills the run is a single pair of arcs
    /// from the last place the ring was left standing to the next — <b>which is the whole of how a radius
    /// larger than the shape's own detail gets to mean anything</b>. Capped at what each corner's own two
    /// pieces can spare instead, a stretch of half-metre steps rounds at a quarter of a metre however round
    /// it is asked to be, and the figure stops doing anything at a fifth of its range: the answer is
    /// continuous and exactly as rugged as it went in. Swallowed, the same stretch comes back as one curve.
    /// </para>
    /// <para>
    /// <b>A ring that is claimed away entirely shares itself out instead</b> (<see cref="Shared"/>), which
    /// is what keeps the far end of the figure a shape rather than a collapse: a square asked for more
    /// radius than it has sides is <em>the circle through the middle of its four sides</em>, not a single
    /// arc from one pose back to itself.
    /// </para>
    /// <para>
    /// <b>Outwards only, and nothing is ever rounded off a corner that turns away from the shape.</b> A
    /// corner turning away is the offset of a corner of the shape: it is the arc of the distance moved
    /// already, and <b>the only curve that rounds it further is one that cuts inside it</b> — a chord of it,
    /// nearer the shape than the distance the whole way across, which is not an offset of anything. So the
    /// claims are laid at the corners that turn <em>in</em> — the notches a fold cut left — where filling
    /// takes the line away from the shape, and <b>every fill is then weighed against the shape itself</b>
    /// (<see cref="Nearer"/>) rather than trusted to the sign. That one test is what makes the figure
    /// one-sided in fact and not just in intent: a fill spanning a run of corners can reach inside where
    /// each of them alone would not, and it is refused for it.
    /// </para>
    /// <para>
    /// <b>The trim and the join, rather than a fillet solved against two curves.</b> A corner between two
    /// straights rounded this way is exactly the circle tangent to both — the biarc between two poses
    /// symmetric about a corner is one arc — and a corner between two bends comes back tangent-continuous at
    /// both ends without a circle having to be solved against two arcs.
    /// </para>
    /// <para>
    /// <b>Every fill is struck between two places the ring itself has, and the ring is never scanned for
    /// gaps to fill.</b> That is the whole of the difference between a smoothing and a loop generator. A
    /// ring is walked by ends that stand within a weld of one another (<see cref="ArcRings.WeldM"/>), not on
    /// top of one another, and it doubles back by a hand's breadth wherever a cut passed over a piece too
    /// short to keep — so a pass that looks for two ends apart and puts an arc between them finds those, and
    /// <b>a biarc between two poses on one heading with the far one a few centimetres behind is a full
    /// circle</b>. A shipped city's boundary came back with twenty-five metres of arc at a radius of four in
    /// the middle of a straight, drawn as a ring hanging off the line with nothing under it.
    /// </para>
    /// <para>
    /// <b>A fill that is refused gives back everything it claimed</b> (<see cref="Fills"/>): a claim is a
    /// claim on the pieces and not yet a cut, so the pass that would leave a hole leaves corners instead.
    /// </para>
    /// </remarks>
    static ArcSeg[] Rounded(
        ArcSeg[] ring, float radiusM, float outwardM, ArcSeg[] source, ChainIndex standing, float keepM,
        int[] near, float[] alongM)
    {
        if (ring.Length < 2) return ring;

        var wantM = new float[ring.Length];
        var turnsRad = new float[ring.Length];
        for (var at = 0; at < ring.Length; at++)
        {
            var onto = (at + 1) % ring.Length;
            turnsRad[at] = Turn(ring[at], ring[onto]);
            if (MathF.Abs(turnsRad[at]) < StraightTurn || turnsRad[at] * outwardM > 0f) continue;

            wantM[at] = radiusM * MathF.Tan(MathF.Abs(turnsRad[at]) * 0.5f);
        }

        var tight = new bool[ring.Length];
        for (var at = 0; at < ring.Length; at++)
        {
            if (MathF.Abs(ring[at].Curvature) * radiusM <= 1f) continue;

            tight[at] = true;
            var claimM = radiusM
                * MathF.Tan(MathF.Min(MathF.Abs(ring[at].LengthM * ring[at].Curvature), MostBend) * 0.5f);

            var back = (at + ring.Length - 1) % ring.Length;
            wantM[back] = MathF.Max(wantM[back], claimM);
            wantM[at] = MathF.Max(wantM[at], claimM);
        }

        var swallowed = new bool[ring.Length];
        if (Swallowed(ring, wantM, tight, swallowed) < 2)
        {
            Shared(ring, wantM);
            Array.Clear(swallowed);
        }

        var fills = new int[ring.Length];
        var filled = new ArcSeg[ring.Length * 2];
        Span<ArcSeg> biarc = stackalloc ArcSeg[2];
        for (var at = 0; at < ring.Length; at++)
        {
            if (swallowed[at]) continue;

            var onto = (at + 1) % ring.Length;
            while (swallowed[onto]) onto = (onto + 1) % ring.Length;

            fills[at] = Fills(ring, wantM, turnsRad, at, onto, biarc);
            if (fills[at] > 0 && Inside(biarc, fills[at], source, standing, keepM, outwardM, near, alongM))
            {
                fills[at] = 0;
            }

            if (fills[at] == 0)
            {
                Given(wantM, swallowed, ring.Length, at, onto);
                continue;
            }

            for (var piece = 0; piece < fills[at]; piece++) filled[(at * 2) + piece] = biarc[piece];
        }

        var run = new List<ArcSeg>(ring.Length * 3);
        for (var at = 0; at < ring.Length; at++)
        {
            if (swallowed[at]) continue;

            Kept(run, ring[at], wantM[(at + ring.Length - 1) % ring.Length], wantM[at]);
            for (var piece = 0; piece < fills[at]; piece++) run.Add(filled[(at * 2) + piece]);
        }

        return run.Count == 0 ? [] : ArcRings.Joined(CollectionsMarshal.AsSpan(run), shut: true);
    }

    /// <summary>
    /// <b>Whether any part of a fill stands nearer the shape than the distance it was moved off it</b> —
    /// the one thing a smoothing may not do, and the reason it is asked here rather than argued from the
    /// direction the corner turned.
    /// </summary>
    /// <remarks>
    /// <b>A line to lay a kerb along may stand further off the carriageway than it was asked to and may
    /// never stand nearer</b>, so the rounding is one-sided in the same terms the offset itself is: the test
    /// is <see cref="Nearer"/>, the same one every stretch of the answer already passed. <b>Sampled along
    /// the fill and not at its ends</b>, which are on the ring and so exactly at the distance by
    /// construction — what a fill does is bow away from them, and the whole question is which way.
    /// </remarks>
    static bool Inside(
        ReadOnlySpan<ArcSeg> biarc, int made, ArcSeg[] source, ChainIndex standing, float keepM,
        float outwardM, int[] near, float[] alongM)
    {
        for (var piece = 0; piece < made; piece++)
        {
            var steps = Math.Clamp(
                (int)MathF.Ceiling(biarc[piece].LengthM / (keepM * StationShare)), 1, MostStations);

            for (var step = 0; step <= steps; step++)
            {
                var atM = biarc[piece].LengthM * step / steps;
                if (Nearer(source, standing, biarc[piece].PointAtM(atM), keepM, outwardM, near, alongM))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// How finely a fill is read for whether it has gone inside, <b>as a share of the distance moved</b>: a
    /// quarter of it, so a fill that dips in at all dips in over several stations rather than between two.
    /// </summary>
    const float StationShare = 0.25f;

    /// <summary>And how many stations one arc of a fill is read at however long it is, which bounds the cost of the pass.</summary>
    const int MostStations = 64;

    /// <summary>
    /// <b>Which pieces of a ring both of their own ends have claimed away</b>, and how many are left
    /// standing. A piece with nothing left between the two claims on it is taken whole by the run of corners
    /// around it; <b>a claim is never trimmed to make it fit</b>, because a corner given less than it asked
    /// for is a corner at a radius nobody named.
    /// </summary>
    static int Swallowed(ArcSeg[] ring, float[] wantM, bool[] tight, bool[] swallowed)
    {
        var standing = 0;
        for (var at = 0; at < ring.Length; at++)
        {
            var back = (at + ring.Length - 1) % ring.Length;
            swallowed[at] =
                tight[at] || ring[at].LengthM - wantM[back] - wantM[at] <= ArcRings.LeastPieceM;

            if (!swallowed[at]) standing++;
        }

        return standing;
    }

    /// <summary>
    /// <b>How far a piece the rounding takes out is credited with turning</b>, for the tangent it claims off
    /// its neighbours: most of a half circle, because the tangent of a half turn is unbounded and a bend
    /// that doubles back does not get to claim a kilometre of what stands either side of it.
    /// </summary>
    const float MostBend = MathF.PI * 0.9f;

    /// <summary>
    /// <b>A ring with nothing left standing, shared out</b>: every claim on it cut by the one share that
    /// makes the tightest piece exactly meet, so each piece is divided between the two corners at its ends
    /// and none is asked for more than it has.
    /// </summary>
    /// <remarks>
    /// <b>It is the only place a claim is cut, and the reason is that the alternative is not a shape.</b>
    /// With every piece swallowed there is one place left standing in the whole ring and a single fill from
    /// it back round to itself, which is a circle struck through one pose — the loop this construction spent
    /// a city's boundary learning to refuse. Shared out, the ring is as round as its own material allows and
    /// still its own shape: a square is the circle through the middle of its sides.
    /// </remarks>
    static void Shared(ArcSeg[] ring, float[] wantM)
    {
        var share = 1f;
        for (var at = 0; at < ring.Length; at++)
        {
            var back = (at + ring.Length - 1) % ring.Length;
            var claimedM = wantM[back] + wantM[at];
            if (claimedM > 0f) share = MathF.Min(share, ring[at].LengthM / claimedM);
        }

        for (var at = 0; at < ring.Length; at++) wantM[at] *= share;
    }

    /// <summary>
    /// <b>What a refused fill gives back</b>: the claims it was struck between, and the pieces it would have
    /// taken whole — so the run comes back the corners it was rather than the hole the claims cut.
    /// </summary>
    static void Given(float[] wantM, bool[] swallowed, int pieces, int from, int onto)
    {
        for (var at = from; at != onto; at = (at + 1) % pieces)
        {
            wantM[at] = 0f;
            if (at != from) swallowed[at] = false;
        }
    }

    /// <summary>
    /// <b>What fills the run of corners between two pieces still standing</b>: the arcs from where the claim
    /// cut the piece arriving to where it cut the piece leaving, along the two headings there — and
    /// <b>nothing at all where the fill is refused</b>, in which case the caller takes the claims back and
    /// the run stays the corners it was.
    /// </summary>
    /// <remarks>
    /// <b>What fills a run may not turn further than everything it replaces</b> (<see cref="Replaced"/>).
    /// That one test is what separates a fillet from a loop, and it is read off the ring rather than off a
    /// tolerance. <b>A ring doubles back</b>: two moved pieces folding through each other less deeply than
    /// the arithmetic can tell (<see cref="SlackM"/>) are both kept whole, so the second starts a few
    /// centimetres behind where the first stopped, and <b>the only arc from one pose to the other goes all
    /// the way round</b>. A shipped city's boundary came back with twenty-five metres of it at a radius of
    /// four, drawn as a ring hanging off a straight with nothing under it. Weighed against the degree the
    /// ring actually turns there, that is a full circle where one degree was asked for, and it goes.
    /// <b>The needle's own U-turn passes the same test</b>, which a rule about which way the far end lies
    /// would not: where the offset doubles round the tip of a spike the corner really does turn half a
    /// circle, and half a circle is what fills it.
    /// </remarks>
    static int Fills(
        ArcSeg[] ring, float[] wantM, float[] turnsRad, int from, int onto, Span<ArcSeg> biarc)
    {
        var backM = wantM[from];
        var headM = wantM[(onto + ring.Length - 1) % ring.Length];
        if (backM <= 0f && headM <= 0f) return 0;

        var awayM = ring[from].LengthM - backM;
        var leavingRad = ring[from].HeadingAtRad(awayM);
        var fromM = ring[from].PointAtM(awayM);
        var toM = ring[onto].PointAtM(headM);
        if (Vector2.DistanceSquared(fromM, toM) <= LineTolerance.RoundingM * LineTolerance.RoundingM)
        {
            return 0;
        }

        var made = Spline.BiarcInto(fromM, leavingRad, toM, ring[onto].HeadingAtRad(headM), biarc);
        var turnedRad = 0f;
        for (var piece = 0; piece < made; piece++)
        {
            turnedRad += MathF.Abs(biarc[piece].LengthM * biarc[piece].Curvature);
        }

        if (turnedRad > Replaced(ring, wantM, turnsRad, from, onto) + StraightTurn) return 0;

        var kept = 0;
        for (var piece = 0; piece < made; piece++)
        {
            if (biarc[piece].LengthM > LineTolerance.RoundingM) biarc[kept++] = biarc[piece];
        }

        return kept;
    }

    /// <summary>
    /// <b>How far everything one fill takes away turns</b>: the tail of the piece it leaves, every corner
    /// and every whole piece between, and the head of the piece it arrives at — <b>added up without sign</b>,
    /// so a run that zig-zags is allowed the turning it really does rather than the little it nets out at.
    /// </summary>
    static float Replaced(ArcSeg[] ring, float[] wantM, float[] turnsRad, int from, int onto)
    {
        var turnedRad = MathF.Abs(wantM[from] * ring[from].Curvature)
            + MathF.Abs(wantM[(onto + ring.Length - 1) % ring.Length] * ring[onto].Curvature);

        for (var at = from; at != onto; at = (at + 1) % ring.Length)
        {
            turnedRad += MathF.Abs(turnsRad[at]);
            if (at != from) turnedRad += MathF.Abs(ring[at].LengthM * ring[at].Curvature);
        }

        return turnedRad;
    }

    /// <summary>What is left of one piece once the corners at either end have taken their share.</summary>
    static void Kept(List<ArcSeg> run, in ArcSeg piece, float ontoM, float backM)
    {
        var toM = piece.LengthM - backM;
        if (toM - ontoM <= LineTolerance.RoundingM) return;

        run.Add(
            new ArcSeg(piece.PointAtM(ontoM), piece.HeadingAtRad(ontoM), toM - ontoM, piece.Curvature));
    }

    /// <summary>The straight across a gap, which is nothing at all unless there is one.</summary>
    static void Bridge(List<ArcSeg> run, Vector2 fromM, Vector2 toM)
    {
        var runM = toM - fromM;
        var lengthM = runM.Length();
        if (lengthM <= LineTolerance.RoundingM) return;

        run.Add(new ArcSeg(fromM, MathF.Atan2(runM.Y, runM.X), lengthM, 0f));
    }

    /// <summary>How far one piece turns to take up the next, negative to the walker's left.</summary>
    static float Turn(in ArcSeg from, in ArcSeg onto)
    {
        var leaving = Heading.Unit(from.HeadingAtRad(from.LengthM));
        var taking = onto.StartUnit;
        return MathF.Atan2(
            (leaving.X * taking.Y) - (leaving.Y * taking.X), Vector2.Dot(leaving, taking));
    }

    /// <summary>
    /// <b>Which way the ring went round a corner</b>, read off the two chords its pieces subtend rather than
    /// off the tangents they meet at — so it is where the pieces <em>went</em> and not how they left.
    /// </summary>
    /// <remarks>
    /// <b>It is the one reading a cusp does not destroy, and a boundary is full of cusps.</b> Two arcs that
    /// meet with their tangents exactly opposed turn a half circle whichever side the ground is on, so the
    /// cross product that would say which is nought and its sign is the last bits of a float — and a corner
    /// read the wrong way round puts a half circle of offset through solid ground, which the fold test then
    /// keeps because every point of it stands exactly the distance off the one place it turns about. <b>A
    /// chord cannot be opposed to the tangent that leaves it by more than half the piece's own sweep</b>, so
    /// this differs from <see cref="Turn"/> nowhere but at those cusps.
    /// </remarks>
    static float Swung(in ArcSeg from, in ArcSeg onto)
    {
        var leaving = from.EndM - from.StartM;
        var taking = onto.EndM - onto.StartM;
        return MathF.Atan2(
            (leaving.X * taking.Y) - (leaving.Y * taking.X), Vector2.Dot(leaving, taking));
    }

    /// <summary>
    /// <b>The moved ring shut into the chain it is</b>: the chord across every gap a swallowed piece left,
    /// and then the whole of it joined.
    /// </summary>
    /// <remarks>
    /// <b>A chord and not a repair.</b> What it closes is a feature the move was wide enough to take whole,
    /// so the ring has to carry on across where that feature was — and whether the chord itself is on the
    /// offset at all is the fold test's to say (<see cref="Uncovered"/>), like every other piece here.
    /// </remarks>
    static ArcSeg[] Closed(List<ArcSeg> run)
    {
        var chain = new List<ArcSeg>(run.Count * 2);
        for (var at = 0; at < run.Count; at++)
        {
            chain.Add(run[at]);
            Bridge(chain, run[at].EndM, run[(at + 1) % run.Count].StartM);
        }

        return ArcRings.Joined(CollectionsMarshal.AsSpan(chain), shut: true);
    }

}
