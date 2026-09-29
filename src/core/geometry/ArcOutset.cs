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
/// <b>The rounding is this same move again and never a pass over its answer</b> (<see cref="Of"/>): out by
/// the distance <em>and</em> the radius, in by twice the radius, out by the radius. A corner a move closes
/// comes back as the arc of that move about it (<see cref="Corner"/>), so each move rounds the hand it
/// closes at exactly the figure it was given — <b>by the construction above and not a second one</b>, which
/// is why there is no corner it can refuse and nothing to weigh a fill against.
/// </para>
/// <para>
/// <b>And what it names is a curvature and not a length</b>: <b>one radius, asked of every corner of the
/// answer alike</b>. A corner is the one place a line's curvature is unbounded, so the whole of what a
/// smoothing can say is how tightly the answer is allowed to turn — and a figure that says it corner by
/// corner, off the pieces each one happens to stand between, rounds a corner between long pieces at fifty
/// metres and the one next to it at half a metre. The line comes back continuous and just as rugged as it
/// went in, because <b>ruggedness is a curvature and it was never touched</b>.
/// </para>
/// <para>
/// <b>The radius is a length and not a share of the distance moved</b>, because the two say different
/// things and a reader wants both. The distance is how far the line stands off the town; the radius is how
/// tightly it is allowed to turn. Tied together, a line struck a hand's breadth off a car park has a hand's
/// breadth of radius and is exactly as rugged as the park — which is the one thing a rounding was asked
/// for. <b>Untied, the distance may be nothing at all and the shape still come back round.</b>
/// </para>
/// <para>
/// <b>Which way a corner is rounded is which way it turns, and the distance rounds one hand for free.</b> A
/// corner the shape turns away at comes back as the arc of the distance about it, so at a radius inside the
/// distance there is nothing left to do to that hand and the rounding is the notches alone — <b>and then
/// the answer never comes nearer the shape than it was moved</b>, which is what a kerb off a carriageway
/// is. <b>A radius past the distance cuts those corners</b>, and there is no construction that does not: a
/// corner turning away stands at exactly the distance, and every curve that rounds it further is a chord of
/// that arc, nearer the shape the whole way across. <b>That is the trade the figure makes, and it is the
/// reader's to make</b> — asked for a rounding with no distance at all, the only honest answer is the shape
/// a ball of that radius rolls round.
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
    /// <b>One closed shape moved <paramref name="outwardM"/> off its own ground, and no corner of the answer
    /// left turning tighter than <paramref name="roundedM"/></b> — a radius in metres and not a share of the
    /// distance, so a shape can be rounded as hard as a reader likes at any distance at all, including none.
    /// <paramref name="rings"/> is the whole of the shape's boundary, every ring of it walked with the ground
    /// on its right.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The rounding is this same move run again and not a pass over its answer</b>: out by the distance
    /// <em>and</em> the radius, in by twice the radius, out by the radius — three moves that come to the
    /// distance asked for. <b>A corner a move closes comes back as the arc of that move about it</b>
    /// (<see cref="Corner"/>), so each move in the series rounds the hand it closes at exactly the figure it
    /// was given, by the one construction that already closes a fold, cuts what went inside and strings what
    /// is left. There is no second geometry to get right and no corner it can refuse.
    /// </para>
    /// <para>
    /// <b>The two hands are rounded by opposite moves, which is why there are three.</b> A corner turning in
    /// on the shape is a notch the fold cut and only a move inward rounds it; a corner the shape turns away
    /// at is sharp until a move outward rounds it, and <b>the distance itself is such a move</b> — it comes
    /// back as the arc of <c>d</c> about the corner, whatever the radius says. <b>So the last move is worth
    /// making only where the radius asks for more than the distance already gave</b> (<c>r &gt; |d|</c>) and
    /// is skipped below that, where it is the identity: the two answers agree exactly at <c>r = |d|</c>.
    /// <b>And it is the only move that can cut</b>, so a caller that wants the fill without the cut skips it
    /// at every radius (<see cref="Corners.Filled"/>).
    /// </para>
    /// <para>
    /// <b>While the radius is inside the distance the answer cannot come nearer the shape than it was
    /// moved</b> — the one thing a line to lay a kerb along may never do — because every place on it stands
    /// within <c>r</c> of a place standing <c>d+r</c> off the shape. <b>Past that it cuts the corner</b>, and
    /// necessarily: a corner turning away is a point of the answer at exactly <c>d</c>, and a radius of more
    /// than <c>d</c> laid across it is nearer the shape than the point it replaced. <b>A rounding with no
    /// distance at all is that case</b>, and what it hands back is the shape a ball of that radius rolls
    /// round rather than the shape itself.
    /// </para>
    /// <para>
    /// <b>A pocket narrower than twice the radius is closed rather than rounded, and a spit thinner than
    /// twice the radius goes</b>, since a ball of that radius fits into neither. That is the whole of the
    /// difference between this and a fillet fitted at one corner: the answer is the line the ball rolls,
    /// which is a shape rather than a corner treatment.
    /// </para>
    /// <para>
    /// <b>The runs that would not close are handed back apart</b>, exactly as a merge hands its own back
    /// (<see cref="BandShell.Loose"/>): the boundary of a shape moved off itself is closed, so a run with
    /// two ends is a crossing this construction did not find rather than a shape the offset has. <b>Every
    /// move hands its own back</b>, and what one could not close is not offered to the next.
    /// </para>
    /// </remarks>
    /// <summary>
    /// <b>What a rounding may do to a corner the shape turns away at.</b> A corner turning <em>in</em> is a
    /// notch the fold cut and is filled either way; this is the other hand.
    /// </summary>
    internal enum Corners
    {
        /// <summary>
        /// <b>Rolled on both hands</b>: a corner the shape turns away at tighter than the radius is cut, so
        /// the answer is the line a ball of that radius rolls round rather than the shape moved.
        /// </summary>
        Rolled,

        /// <summary>
        /// <b>Filled and never cut</b>, whatever the radius: a corner the shape turns away at comes back as
        /// the arc of the distance moved, as it does with no rounding at all. <b>It is what a line a body is
        /// held on wants</b> — the answer never stands nearer the shape than it was moved, so no radius can
        /// pull a walk towards the ground it was placed off (WLK-1).
        /// </summary>
        Filled,
    }

    /// <inheritdoc cref="Of(ReadOnlySpan{ArcSeg[]}, float, float, WorldGrid, Corners)"/>
    public static (ArcSeg[][] Rings, ArcSeg[][] Loose) Of(
        ReadOnlySpan<ArcSeg[]> rings, float outwardM, float roundedM, WorldGrid grid) =>
        Of(rings, outwardM, roundedM, grid, Corners.Rolled);

    /// <inheritdoc cref="Of(ReadOnlySpan{ArcSeg[]}, float, float, WorldGrid)"/>
    /// <param name="grid">The grid the pieces are indexed on while the move is worked out (SIM-8).</param>
    /// <param name="corners">
    /// Whether the corners the shape turns away at may be cut, which is the whole of the difference between
    /// the two series (<see cref="Corners"/>).
    /// </param>
    public static (ArcSeg[][] Rings, ArcSeg[][] Loose) Of(
        ReadOnlySpan<ArcSeg[]> rings, float outwardM, float roundedM, WorldGrid grid, Corners corners)
    {
        if (rings.Length == 0) return ([], []);

        var radiusM = MathF.Max(roundedM, 0f);
        var movedM = MathF.Abs(outwardM);
        if (radiusM <= LineTolerance.RoundingM)
        {
            return movedM <= LineTolerance.RoundingM ? (rings.ToArray(), []) : Struck(rings, outwardM, grid);
        }

        // Which way the series runs is the distance's, and a rounding asked for without one still runs
        // outward — the shape grows by the radius and comes back, rather than the other way about.
        var stepM = outwardM < -LineTolerance.RoundingM ? -radiusM : radiusM;

        var (wide, loose) = Struck(rings, outwardM + stepM, grid);
        if (wide.Length == 0) return ([], loose);

        // Two moves where the third would be the identity (r <= d), and where the caller will not have a
        // corner cut at any radius: out by d + r and back in by r is the fill on its own.
        if (radiusM <= movedM || corners == Corners.Filled)
        {
            var (closed, closedLoose) = Struck(wide, -stepM, grid);
            return (closed, Both(loose, closedLoose));
        }

        var (tight, tightLoose) = Struck(wide, -2f * stepM, grid);
        loose = Both(loose, tightLoose);
        if (tight.Length == 0) return ([], loose);

        var (rounded, roundedLoose) = Struck(tight, stepM, grid);
        return (rounded, Both(loose, roundedLoose));
    }

    /// <summary>What two moves of the series could not close, which is the caller's to see whichever move left it.</summary>
    static ArcSeg[][] Both(ArcSeg[][] loose, ArcSeg[][] more) =>
        loose.Length == 0 ? more : more.Length == 0 ? loose : [.. loose, .. more];

    /// <summary>
    /// <b>One closed shape moved off its own ground, once</b>: every ring offset whole, every corner joined,
    /// and every stretch of that kept where nothing of the shape stands nearer to it than the distance.
    /// </summary>
    static (ArcSeg[][] Rings, ArcSeg[][] Loose) Struck(ReadOnlySpan<ArcSeg[]> rings, float outwardM, WorldGrid grid)
    {
        // The pieces of a ring the stringing handed back already meet (<see cref="ArcRings.Tightened"/>);
        // this is for the caller that hands over a ring of its own, since a corner at exactly the distance
        // moved has no headroom for two vertices a centimetre apart.
        var tight = new ArcSeg[rings.Length][];
        for (var at = 0; at < rings.Length; at++) tight[at] = ArcRings.Tightened(rings[at]);

        var source = ArcRings.Flat(tight);
        var moved = ArcRings.Flat(Moved(tight, outwardM));
        if (moved.Length == 0 || source.Length == 0) return ([], []);

        // <b>The level is the question's own radius.</b> Every query this makes asks what stands within the
        // distance moved, so the finest level whose cell covers it answers in a handful of cells — and the
        // index steps a level coarser by itself where a shape is spread too wide to bin that finely.
        var level = grid.Covering(MathF.Abs(outwardM));
        var standing = ChainIndex.OfPieces(source, level);
        var (chains, loose) = ArcRings.Of(
            Uncovered(moved, ChainIndex.OfPieces(moved, level), source, standing, MathF.Abs(outwardM), outwardM),
            grid, Grazed(outwardM, Furthest(source)));

        return (chains, loose);
    }

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
    /// <para>
    /// <b>The fold is as deep as a float is coarse where the shape reaches</b> (<see cref="LineTolerance.At"/>):
    /// a town beyond 8 192 m folds its lines through each other by the arithmetic's own error, and on one
    /// thirty kilometres long the holes that left stood just past the centimetre's figure.
    /// </para>
    /// </remarks>
    static float Grazed(float outwardM, Vector2 furthestM) =>
        MathF.Sqrt(2f * LineTolerance.At(LineTolerance.JoinedM, furthestM) * MathF.Abs(outwardM)) + ArcRings.LeastLostM;

    /// <summary>The corner of the pieces' box furthest from the origin, where a float is at its coarsest.</summary>
    static Vector2 Furthest(ArcSeg[] pieces)
    {
        var furthestM = Vector2.Zero;
        foreach (var piece in pieces) furthestM = Vector2.Max(furthestM, Vector2.Abs(piece.StartM));

        return furthestM;
    }

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
    /// <b>One open line moved sideways with its corners made good</b> — <paramref name="offsetM"/> positive
    /// to the walker's right, as <see cref="Spline.OffsetInto"/> reads it — and the count of pieces written
    /// into <paramref name="into"/>, which needs room for twice the line's own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A line beside a line is the same construction a ring's offset is, and a piecewise move is not
    /// one.</b> Moved piece by piece, an offset chain stops being a chain at every corner its parent turns:
    /// the corner opens a gap of <c>2·d·sin(θ/2)</c> on one hand — a metre and a half on the hairpin a
    /// pavement turns round a wedge between two roads — and folds the line through itself on the other.
    /// <b>What closes both is what closes them for a shape</b>: the arc of the offset's own radius about the
    /// place the line turned at (<see cref="Corner"/>) where the corner opens, and the place the two moved
    /// pieces cross where it closes.
    /// </para>
    /// <para>
    /// <b>The fold is cut against the two pieces that made it and not against the whole line</b>, which is
    /// what parts this from <see cref="Of"/>. A shape's offset is cut by every piece of the shape because a
    /// feature anywhere may swallow an offset anywhere; a line beside a line is one hand of that, taken over
    /// a stretch of pavement a few metres long, and a fold deeper than the pieces that made it is a
    /// stretch the walking network has already cut somewhere else. Where the cut finds no crossing at all
    /// the two ends are chorded (<see cref="Bridge"/>), so what comes back is a chain either way.
    /// </para>
    /// </remarks>
    public static int Beside(ReadOnlySpan<ArcSeg> line, float offsetM, Span<ArcSeg> into)
    {
        if (line.Length == 0) return 0;
        if (MathF.Abs(offsetM) <= LineTolerance.RoundingM)
        {
            line.CopyTo(into);
            return line.Length;
        }

        var written = 0;
        var from = -1;
        Span<ArcSeg> one = stackalloc ArcSeg[1];
        for (var piece = 0; piece < line.Length; piece++)
        {
            Spline.OffsetInto(line.Slice(piece, 1), offsetM, one);
            if (one[0].LengthM <= LineTolerance.RoundingM) continue;

            var moved = one[0];
            if (written > 0) written = Joined(line, from, piece, ref moved, offsetM, into, written);
            if (moved.LengthM <= LineTolerance.RoundingM) continue;

            into[written++] = moved;
            from = piece;
        }

        return written;
    }

    /// <summary>
    /// What goes between the last moved piece written and the next one: nothing where they already meet, the
    /// round of the corner where it opens, and a cut back to where they cross where it closes — <b>which may
    /// take the piece already written with it</b>, a fold being as deep as it is.
    /// </summary>
    /// <remarks>
    /// <b>Whether there is a corner here at all is asked of the two moved ends and not of the angle between
    /// them</b>, because the two say different things about the same joint. A chain fitted to a bend turns a
    /// little at every joint of it by construction, and a joint that turns a little is one whose moved ends
    /// are already within <see cref="LineTolerance.JoinedM"/> of each other — nothing to open and nothing to
    /// fold. Read off the angle instead, every one of those is a corner to be made good, and the crossing
    /// that would make it good is two nearly tangent circles meeting tens of metres away.
    /// </remarks>
    static int Joined(
        ReadOnlySpan<ArcSeg> line, int from, int onto, ref ArcSeg moved, float offsetM, Span<ArcSeg> into,
        int written)
    {
        var apartM = Vector2.Distance(into[written - 1].EndM, moved.StartM);
        if (apartM <= LineTolerance.JoinedM) return written;

        // A piece the move turned inside out stands between these two, so the line they were corners of is
        // not there to take a corner off: what is between them is the gap it left.
        var turnRad = Turn(into[written - 1], moved);
        if (from + 1 != onto || MathF.Abs(turnRad) < StraightTurn)
        {
            return written + Bridged(into[written - 1].EndM, moved.StartM, into[written..]);
        }

        // <b>Outward is the walker's left</b> (<see cref="Of"/>), so a move to the right opens the corners a
        // move outward closes.
        if (Swung(line[from], line[onto]) * -offsetM >= 0f)
        {
            ref readonly var arriving = ref into[written - 1];
            into[written] = Round(
                arriving.EndM, arriving.HeadingAtRad(arriving.LengthM), turnRad, -offsetM);
            return written + 1;
        }

        var cut = written;
        while (cut > 0 && !Crossed(ref into[cut - 1], ref moved)) cut--;

        // Nothing it ran into over the whole of what is laid, so the fold is deeper than this line goes: the
        // line is left whole and chorded across, which is what a ring does with the same case.
        return cut > 0 ? cut : written + Bridged(into[written - 1].EndM, moved.StartM, into[written..]);
    }

    /// <summary>
    /// <b>The two moved pieces cut back to where they cross</b>, or false where they never do — in which case
    /// the arriving one is inside the fold whole and is the caller's to drop.
    /// </summary>
    /// <remarks>
    /// <b>The crossing taken is the one furthest along the piece that arrives</b>, because a fold is entered
    /// once and left once and what is wanted is where it is left. <b>And a crossing the two pieces do not
    /// read at the same place is not one</b>: two nearly tangent arcs lie on circles that meet where neither
    /// of them goes, and a cut made at that reading leaves the two ends metres apart.
    /// </remarks>
    static bool Crossed(ref ArcSeg arriving, ref ArcSeg leaving)
    {
        Span<float> alongArriving = stackalloc float[2];
        Span<float> alongLeaving = stackalloc float[2];
        var found = Spline.CrossingsOf(arriving, leaving, alongArriving, alongLeaving);

        var at = -1;
        for (var crossing = 0; crossing < found; crossing++)
        {
            if (alongArriving[crossing] <= LineTolerance.RoundingM) continue;
            if (alongLeaving[crossing] >= leaving.LengthM - LineTolerance.RoundingM) continue;

            var apartM = Vector2.Distance(
                arriving.PointAtM(alongArriving[crossing]), leaving.PointAtM(alongLeaving[crossing]));
            if (apartM > LineTolerance.JoinedM) continue;
            if (at < 0 || alongArriving[crossing] > alongArriving[at]) at = crossing;
        }

        if (at < 0) return false;

        arriving = new ArcSeg(arriving.StartM, arriving.HeadingRad, alongArriving[at], arriving.Curvature);
        leaving = new ArcSeg(
            leaving.PointAtM(alongLeaving[at]), leaving.HeadingAtRad(alongLeaving[at]),
            leaving.LengthM - alongLeaving[at], leaving.Curvature);
        return true;
    }

    /// <summary>The straight across a gap written into a span, which is nothing at all unless there is one.</summary>
    static int Bridged(Vector2 fromM, Vector2 toM, Span<ArcSeg> into)
    {
        var runM = toM - fromM;
        var lengthM = runM.Length();
        if (lengthM <= LineTolerance.At(LineTolerance.RoundingM, fromM)) return 0;

        into[0] = new ArcSeg(fromM, MathF.Atan2(runM.Y, runM.X), lengthM, 0f);
        return 1;
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
    /// <b>A piece whose own bend is tighter than the distance moved is turned inside out and kept</b>
    /// (<see cref="Inverted"/>) rather than dropped. Its offset really is a line — the arc of
    /// <c>|R−d|</c> about the same centre, walked the other way — and <b>every point of it stands exactly
    /// the distance off the piece it came from</b>, so it is a candidate like any other and the loop it
    /// makes is a fold like any other. <b>Dropped instead, it takes its two corners with it</b>: what is
    /// left between its neighbours is a chord the fold test deletes for standing inside, and a town's
    /// boundary came back with two-metre holes at every hand's breadth of tight bend that stood in one.
    /// </para>
    /// </remarks>
    static ArcSeg[] Moved(ReadOnlySpan<ArcSeg> ring, float outwardM)
    {
        var run = new List<ArcSeg>(ring.Length * 3);

        Span<ArcSeg> one = stackalloc ArcSeg[1];
        for (var piece = 0; piece < ring.Length; piece++)
        {
            Spline.OffsetInto(ring.Slice(piece, 1), -outwardM, one);
            var moved = one[0].LengthM < 0f ? Inverted(one[0]) : one[0];
            if (moved.LengthM > LineTolerance.RoundingM) run.Add(moved);

            if (Corner(ring[piece], ring[(piece + 1) % ring.Length], outwardM, out var round))
            {
                run.Add(round);
            }
        }

        return run.Count == 0 ? [] : Closed(run);
    }

    /// <summary>
    /// <b>The piece a move turned inside out, as the line it is</b>: the offset of an arc the move reached
    /// past the middle of comes back on the far side of that middle, so it is walked against its own bend
    /// and <see cref="Spline.OffsetInto"/> hands it back with a negative length to say so.
    /// </summary>
    /// <remarks>
    /// <b>It starts where the offset of the piece's own start stands and turns the way the piece did.</b>
    /// The offset point of a place on an arc is that place carried through the centre, so the two run round
    /// the centre together while the offset faces the other way — which is one arc of <c>|R−d|</c> from the
    /// offset of the start to the offset of the end, and not the piece read backwards.
    /// </remarks>
    static ArcSeg Inverted(in ArcSeg moved) =>
        new(moved.StartM, Spline.WrapRad(moved.HeadingRad + MathF.PI), -moved.LengthM, -moved.Curvature);

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
    /// anything at this distance is ever obliged to turn. What a mitre buys is a
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
    /// <para>
    /// <b>And it is read off the two pieces the corner is between, whatever the move did to them</b>: the
    /// place is the ring's own and the two headings are the ones the offset keeps, so a piece the move
    /// swallowed is still a piece its corners stand at.
    /// </para>
    /// </remarks>
    static bool Corner(in ArcSeg leaving, in ArcSeg taking, float outwardM, out ArcSeg round)
    {
        round = default;
        var turnRad = Turn(leaving, taking);
        if (MathF.Abs(turnRad) < StraightTurn) return false;
        if (Swung(leaving, taking) * outwardM < 0f) return false;

        // <b>Outward is the walker's left</b> (<see cref="Of"/>), which is where the corner's own place
        // stands whether or not the piece that arrives at it came back with a length.
        var headingRad = leaving.HeadingAtRad(leaving.LengthM);
        var atM = leaving.EndM - (Heading.RightOf(Heading.Unit(headingRad)) * outwardM);
        round = Round(atM, headingRad, turnRad, outwardM);
        return true;
    }

    /// <summary>
    /// The full round of a corner: the arc of the offset's own radius about the place the ring turned at,
    /// <b>every point of which stands exactly the distance off that place</b>. <paramref name="aboutM"/> is
    /// where that place stands, signed to the right of the heading — the move itself, since the arc starts
    /// at the corner moved off it.
    /// </summary>
    /// <remarks>
    /// <b>Which way it bends is the move's and never the turn's sign</b>, because the two are the same
    /// reading only where the corner is read the way the move sees it. A corner is offered here on the chords
    /// its two pieces subtend (<see cref="Swung"/>) and struck on the tangents they meet at
    /// (<see cref="Turn"/>); where a piece is a hand's breadth of tight bend the two disagree, and <b>an arc
    /// bent by the reading that disagreed is struck about the corner's mirror image</b> — the far side of the
    /// line, twice the distance away, and running into the shape rather than round it.
    /// </remarks>
    static ArcSeg Round(Vector2 fromM, float headingRad, float turnRad, float aboutM) =>
        new(fromM, headingRad, MathF.Abs(aboutM * turnRad), 1f / aboutM);

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
        var insideM = keepM - LineTolerance.At(SlackM, pointM);
        var found = standing.Near(pointM, keepM, near, alongM);
        if (found > near.Length) found = near.Length;

        var nearestM = float.PositiveInfinity;
        for (var at = 0; at < found; at++)
        {
            ref readonly var piece = ref source[near[at]];
            var atM = Math.Clamp(alongM[at], 0f, piece.LengthM);
            var offM = FromThePlace(piece, atM, pointM).LengthSquared();
            if (offM < insideM * insideM) return true;

            nearestM = MathF.Min(nearestM, offM);
        }

        if (found == 0) return false;

        // Two pieces meeting at a corner compute the one point they share through their own arithmetic, so
        // what is one distance comes back as two that differ in the last bits of a float.
        var tiedM = MathF.Sqrt(nearestM) + LineTolerance.At(LineTolerance.RoundingM, pointM);
        var sideM = 0f;
        for (var at = 0; at < found; at++)
        {
            ref readonly var piece = ref source[near[at]];
            var atM = Math.Clamp(alongM[at], 0f, piece.LengthM);
            var ontoM = FromThePlace(piece, atM, pointM);
            if (ontoM.Length() > tiedM) continue;

            sideM -= Vector2.Dot(ontoM, Heading.RightOf(Heading.Unit(piece.HeadingAtRad(atM))));
        }

        return sideM * outwardM > 0f;
    }

    /// <summary>
    /// <b>A place on a piece, from the place being weighed</b>: taken where it stands inside 8 192 m, and
    /// off the piece's own start beyond it, where a place read in world coordinates is already rounded by
    /// more than the test it is asked for (<see cref="LineTolerance.Coarseness"/>).
    /// </summary>
    static Vector2 FromThePlace(in ArcSeg piece, float atM, Vector2 pointM) =>
        LineTolerance.Coarseness(pointM) > 1f
            ? (piece.StartM - pointM) + piece.FromStartM(atM)
            : piece.PointAtM(atM) - pointM;

    /// <summary>The straight across a gap, which is nothing at all unless there is one.</summary>
    static void Bridge(List<ArcSeg> run, Vector2 fromM, Vector2 toM)
    {
        var runM = toM - fromM;
        var lengthM = runM.Length();
        if (lengthM <= LineTolerance.At(LineTolerance.RoundingM, fromM)) return;

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
