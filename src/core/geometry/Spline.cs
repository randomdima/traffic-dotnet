using System.Numerics;

namespace TrafficSimulation.Core.Geometry;

/// <summary>Where a chain is at one distance along it: the point, the way it is heading, and how hard it is bending there.</summary>
internal readonly record struct SplineSample(Vector2 PositionM, float HeadingRad, float Curvature)
{
    public Vector2 Direction => Heading.Unit(HeadingRad);

    /// <summary>
    /// The driver's right, which with <c>+y</c> down is the heading turned a quarter turn the way
    /// curvature counts positive — turned off <see cref="Direction"/> rather than taken from the angle
    /// again, because a caller that wants both is the common one and the pair is one reduction.
    /// </summary>
    public Vector2 Right => Heading.RightOf(Direction);
}

/// <summary>
/// One place two chains cross, as the distance along each of them (<see cref="Spline.CrossingsM"/>).
/// </summary>
internal readonly record struct SplineCrossing(float OneM, float OtherM);

/// <summary>
/// Where a walk of a chain has got to: the piece it stands in, and how far along the whole chain that
/// piece begins. <b>Only ever a hint</b> — <see cref="Spline.SampleFrom"/> restarts from the head when
/// it is handed a distance behind the one it is on, so a cursor cannot make an answer wrong, only slow.
/// </summary>
/// <remarks>
/// It exists because sampling is asked for in runs and not one at a time: a candidate's ground is walked
/// at a metre a step, a band is drawn chord by chord, and a plain <see cref="Spline.SampleAt"/> would find
/// the piece by counting from the head of the line for every one of them. Over a line of a dozen lanes
/// that is the difference between a walk and a walk squared.
/// </remarks>
internal struct SplineCursor
{
    internal int Piece;
    internal float PieceStartM;
}

/// <summary>
/// A chain of <see cref="ArcSeg"/> walked by arc length: sampled, offset sideways, projected onto, and
/// built between two poses. Everything geometric a driven line needs is here, so the assembler above it
/// is about <em>which</em> lines a route is made of and never about how an arc works.
/// </summary>
/// <remarks>
/// Nothing here allocates: every entry takes a span and returns a value, because the follower asks for
/// a sample and a projection every tick for every car.
/// </remarks>
internal static class Spline
{
    /// <summary>Below this an arc's centre is further off than any town is wide, and it is a straight.</summary>
    const float StraightCurvature = 1e-6f;

    public static float TotalLengthM(ReadOnlySpan<ArcSeg> arcs)
    {
        var lengthM = 0f;
        for (var index = 0; index < arcs.Length; index++) lengthM += arcs[index].LengthM;

        return lengthM;
    }

    /// <summary>
    /// <b>How much heading a chain spends over its whole length</b> — every bend's own turn and every kink
    /// between two pieces, all of it counted the way it is paid for, whichever way round it goes.
    /// </summary>
    /// <remarks>
    /// <b>What it is for is telling a corner from a line drawn round the houses.</b> The heading a chain's own
    /// two ends ask for is the least it could spend; what it spends over that is winding, and a body pays for
    /// it in the same coin either way — turning while it walks, or standing and pivoting at a kink.
    /// </remarks>
    public static float SweptRad(ReadOnlySpan<ArcSeg> arcs)
    {
        var sweptRad = 0f;
        for (var at = 0; at < arcs.Length; at++)
        {
            sweptRad += MathF.Abs(arcs[at].LengthM * arcs[at].Curvature);
            if (at == 0) continue;

            sweptRad += MathF.Abs(
                WrapRad(arcs[at].HeadingRad - arcs[at - 1].HeadingAtRad(arcs[at - 1].LengthM)));
        }

        return sweptRad;
    }

    /// <summary>
    /// <b>The least heading any chain between a chain's own two ends could spend</b> (<see cref="SweptRad"/>),
    /// which is the heading between them and nothing more.
    /// </summary>
    public static float AskedRad(ReadOnlySpan<ArcSeg> arcs) => MathF.Abs(TurnedRad(arcs));

    /// <summary>
    /// <b>The same heading, signed the way the chain turns through it</b> — positive to the driver's right,
    /// as a piece's own curvature is (<see cref="ArcSeg.Curvature"/>). What the chain does between its two
    /// ends is no more in this than it is in <see cref="AskedRad"/>.
    /// </summary>
    public static float TurnedRad(ReadOnlySpan<ArcSeg> arcs) =>
        arcs.Length == 0
            ? 0f
            : WrapRad(arcs[^1].HeadingAtRad(arcs[^1].LengthM) - arcs[0].HeadingRad);

    /// <summary>
    /// <b>The ground a closed chain encloses, exactly</b> — the polygon through every piece's own two ends
    /// plus the circular segment each bend cuts off its own chord, signed by which way the chain is walked.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Exact, and so the figure a cut boundary is weighed against.</b> Everything downstream of a
    /// <see cref="ShellFill"/> reads the shell as chords and loses the bow off every bend; what says how much
    /// was lost is the area nothing approximated, and there is nowhere else to get it.
    /// </para>
    /// <para>
    /// <b>A joint that is open is closed with the straight a fill would draw across it</b>, and that is why
    /// each piece contributes two terms rather than one. A merged shell's pieces do not all meet — a town's
    /// perimeter has thousands of joints open by up to a couple of handspans — and a sum that took each
    /// piece's own chord alone would silently leave out the ground under every one of them. On a chain whose
    /// pieces do meet, the second term is zero and this is the plain shoelace.
    /// </para>
    /// <para>
    /// Summed in <c>double</c> for the same reason <see cref="ShellFill"/>'s shoelace is: the terms are
    /// coordinates multiplied by coordinates, and a town two kilometres across cancels six of a float's
    /// seven figures in each of them.
    /// </para>
    /// </remarks>
    public static double EnclosedM2(ReadOnlySpan<ArcSeg> ring)
    {
        var twice = 0.0;
        var segmentsM2 = 0.0;

        for (var at = 0; at < ring.Length; at++)
        {
            var arc = ring[at];
            var fromM = arc.StartM;
            var toM = arc.EndM;
            var nextM = ring[(at + 1) % ring.Length].StartM;

            twice += ((double)fromM.X * toM.Y) - ((double)toM.X * fromM.Y);
            twice += ((double)toM.X * nextM.Y) - ((double)nextM.X * toM.Y);

            if (MathF.Abs(arc.Curvature) <= StraightCurvature) continue;

            var sweepRad = (double)arc.Curvature * arc.LengthM;
            segmentsM2 += (sweepRad - Math.Sin(sweepRad)) / (2.0 * arc.Curvature * arc.Curvature);
        }

        return (twice * 0.5) + segmentsM2;
    }

    /// <summary>
    /// How much of a chain's own end is curved, which is nil for one that ends straight. <b>What a road
    /// leaves a node with no fork on</b> (TER-5b), and so how far along that arm anything laid across a
    /// straight has to stand.
    /// </summary>
    public static float BendAtTheEndM(ReadOnlySpan<ArcSeg> arcs, bool atStart)
    {
        var bendM = 0f;
        for (var index = 0; index < arcs.Length; index++)
        {
            var arc = arcs[atStart ? index : arcs.Length - 1 - index];
            if (MathF.Abs(arc.Curvature) <= StraightCurvature) break;

            bendM += arc.LengthM;
        }

        return bendM;
    }

    /// <summary>
    /// The chain at one distance from its start, clamped to its own ends — a caller past either end
    /// gets the end pose rather than an exception, because a car shoved off the end of its line still
    /// has to be told something this tick.
    /// </summary>
    public static SplineSample SampleAt(ReadOnlySpan<ArcSeg> arcs, float distanceM)
    {
        if (arcs.Length == 0) return default;

        var remainingM = MathF.Max(0f, distanceM);
        for (var index = 0; index < arcs.Length; index++)
        {
            ref readonly var arc = ref arcs[index];
            if (remainingM > arc.LengthM && index < arcs.Length - 1)
            {
                remainingM -= arc.LengthM;
                continue;
            }

            var alongM = MathF.Min(remainingM, arc.LengthM);
            return new SplineSample(arc.PointAtM(alongM), arc.HeadingAtRad(alongM), arc.Curvature);
        }

        return default;
    }

    /// <summary>
    /// <see cref="SampleAt"/> for a caller asking in a run: the same answer, found by carrying on from
    /// where the last one left off rather than by counting from the head of the chain again.
    /// </summary>
    /// <remarks>
    /// A whole run of samples costs one walk of the pieces between the first and the last, whatever the
    /// chain is made of. Handing it a distance behind the cursor is allowed and costs the walk from the
    /// head — the cursor is a hint about where to start looking and never a claim about the caller.
    /// </remarks>
    public static SplineSample SampleFrom(ReadOnlySpan<ArcSeg> arcs, float distanceM, ref SplineCursor cursor)
    {
        if (arcs.Length == 0) return default;

        var remainingM = MathF.Max(0f, distanceM);
        if (cursor.Piece >= arcs.Length || cursor.PieceStartM > remainingM) cursor = default;

        while (cursor.Piece < arcs.Length - 1 && remainingM - cursor.PieceStartM > arcs[cursor.Piece].LengthM)
        {
            cursor.PieceStartM += arcs[cursor.Piece].LengthM;
            cursor.Piece++;
        }

        ref readonly var arc = ref arcs[cursor.Piece];
        var alongM = MathF.Min(remainingM - cursor.PieceStartM, arc.LengthM);
        return new SplineSample(arc.PointAtM(alongM), arc.HeadingAtRad(alongM), arc.Curvature);
    }

    /// <summary>
    /// The longest chord across an arc of this curvature that bows off it by no more than
    /// <paramref name="sagM"/> — <c>√(8·s·R)</c> — and <see cref="float.PositiveInfinity"/> for a
    /// straight, which no chord ever leaves.
    /// </summary>
    /// <remarks>
    /// <b>It is what turns a bend into the fewest straights that still read as the bend</b>, and it is
    /// the answer to both halves of the fixed-step question: a step chosen once is either too coarse for
    /// the tightest thing in the town or it chops a straight into a hundred pieces that one would draw.
    /// A chord falls <em>inside</em> the arc, so whatever is drawn or walked this way cuts the corner by
    /// the sag and never bows wide of it.
    /// </remarks>
    public static float ChordForSagM(float curvature, float sagM)
    {
        var bend = MathF.Abs(curvature);
        return bend <= StraightCurvature ? float.PositiveInfinity : MathF.Sqrt(8f * sagM / bend);
    }

    /// <summary>
    /// The same chain moved sideways by a signed offset, <b>positive to the driver's right</b> — which
    /// is what turns a road's centreline into the line a lane is driven on.
    /// </summary>
    /// <remarks>
    /// An offset arc subtends the angle its parent does, so it is shorter on the inside of a bend and
    /// longer on the outside: the parent's <c>k·L</c> is preserved and the radius moves by the offset.
    /// An offset past the radius of the bend it is on inverts the piece, which comes back with a negative
    /// length (and at the radius exactly, with none): what that is is the caller's to say, and a move of a
    /// shell's boundary keeps it as the line it is (<see cref="ArcOutset"/>, <c>ArcOutset.Inverted</c>).
    /// </remarks>
    public static void OffsetInto(ReadOnlySpan<ArcSeg> arcs, float offsetM, Span<ArcSeg> into)
    {
        for (var index = 0; index < arcs.Length; index++)
        {
            var arc = arcs[index];
            var right = Heading.RightOf(arc.StartUnit);
            var shrink = 1f - arc.Curvature * offsetM;
            into[index] = new ArcSeg(
                arc.StartM + right * offsetM,
                arc.HeadingRad,
                arc.LengthM * shrink,
                MathF.Abs(shrink) < 1e-6f ? 0f : arc.Curvature / shrink);
        }
    }

    /// <summary>
    /// The same line walked the other way: the pieces come out back to front, each starting where it used
    /// to end and pointing the way it used to come from, and <b>bending the other way</b> — a bend that
    /// was a right-hander is a left-hander to whoever meets it.
    /// </summary>
    public static void ReverseInto(ReadOnlySpan<ArcSeg> arcs, Span<ArcSeg> into)
    {
        for (var index = 0; index < arcs.Length; index++)
        {
            var arc = arcs[arcs.Length - 1 - index];
            into[index] = new ArcSeg(arc.EndM, WrapRad(arc.HeadingAtRad(arc.LengthM) + MathF.PI), arc.LengthM, -arc.Curvature);
        }
    }

    /// <summary>The stretch of a chain between two distances, written into a span of its own as a chain in its own right.</summary>
    public static int SubChainInto(ReadOnlySpan<ArcSeg> arcs, float fromM, float toM, Span<ArcSeg> into)
    {
        if (toM - fromM <= 0f) return 0;

        var written = 0;
        var startM = 0f;
        for (var index = 0; index < arcs.Length; index++)
        {
            ref readonly var arc = ref arcs[index];
            var endM = startM + arc.LengthM;
            var takeFromM = MathF.Max(fromM, startM);
            var takeToM = MathF.Min(toM, endM);
            if (takeToM > takeFromM && written < into.Length)
            {
                var intoArcM = takeFromM - startM;
                into[written++] = new ArcSeg(
                    arc.PointAtM(intoArcM), arc.HeadingAtRad(intoArcM), takeToM - takeFromM, arc.Curvature);
            }

            startM = endM;
        }

        return written;
    }

    /// <summary>
    /// <b>The same chain with the joins it does not really turn at rubbed out</b>: every run of pieces that
    /// carries on the same circle written as the one piece it is, and how many pieces that left.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Carrying on is measured and not compared field by field</b> (<see cref="CarriesOn"/>): a kink, a
    /// different radius and a piece doubling back are one question — how far the shape would move — asked
    /// at the scale the caller names in <paramref name="withinM"/> rather than at the scale two floats
    /// agree to.
    /// </para>
    /// <para>
    /// <b>It is not a simplification of the curve</b>: a piece that turns at all, however slightly, is a
    /// piece the chain keeps, and a chain of one curve cut in a hundred places is that curve.
    /// </para>
    /// <para>
    /// <b>Joined as it goes, so a run of pieces is one piece and not a pair of them</b>: each is weighed
    /// against what has already been joined rather than against its own neighbour.
    /// </para>
    /// </remarks>
    public static int JoinedInto(ReadOnlySpan<ArcSeg> arcs, float withinM, Span<ArcSeg> into)
    {
        var written = 0;
        for (var index = 0; index < arcs.Length; index++)
        {
            if (written > 0 && CarriesOn(into[written - 1], arcs[index], withinM, out var joined))
            {
                into[written - 1] = joined;
                continue;
            }

            into[written++] = arcs[index];
        }

        return written;
    }

    /// <summary>
    /// <b>Whether one piece carries on into the next as a single piece would</b>, and that piece — the
    /// first at the two of them end to end, which stands within <paramref name="withinM"/> of the joint
    /// and of the second's own end.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The second is weighed against the first laid on from where the second starts</b>, so the joint's
    /// own gap is measured once rather than again at the far end of the piece. Laid on from the first's
    /// own start instead, a straight cut in two halves at a town's coordinates comes back as a corner: the
    /// hair between two computations of the joint is carried the whole length of the join and lands
    /// outside whatever figure the joint itself was inside.
    /// </para>
    /// <para>
    /// <b>It is a walk and not a radius</b> (<see cref="ArcSeg.PointAtM"/>). A road's bends are a tiny
    /// curvature and a radius of kilometres, so anything asking how far a place stands off a circle
    /// subtracts two huge numbers and answers in whatever the last bits of them left.
    /// </para>
    /// <para>
    /// <b>A piece that runs backwards joins nothing.</b> An offset tighter than the bend it is taken off
    /// hands back a piece of negative length (<see cref="OffsetInto"/>), which is a fold in the shape and
    /// not a length to be added to the one before it.
    /// </para>
    /// </remarks>
    public static bool CarriesOn(in ArcSeg one, in ArcSeg other, float withinM, out ArcSeg joined)
    {
        joined = new ArcSeg(one.StartM, one.HeadingRad, one.LengthM + other.LengthM, one.Curvature);
        if (one.LengthM <= 0f || other.LengthM <= 0f) return false;

        var nearM = LineTolerance.At(withinM, other.StartM);
        if (Vector2.DistanceSquared(one.EndM, other.StartM) > nearM * nearM) return false;

        var carried = new ArcSeg(other.StartM, one.HeadingAtRad(one.LengthM), other.LengthM, one.Curvature);
        return Vector2.DistanceSquared(carried.EndM, other.EndM) <= nearM * nearM;
    }

    /// <summary>
    /// The distance along the chain whose point is nearest the one given, searched <b>in a window</b>
    /// around where the caller last was.
    /// </summary>
    /// <remarks>
    /// The window is the whole reason this is not a search over the line: a route that doubles back
    /// past itself has two nearest points, and a car half way round its manoeuvre into a bay is nearer to where
    /// it started than to where it is going. What a caller wants is the nearest point to the progress it
    /// had, which is a local question.
    /// </remarks>
    public static float ProjectM(ReadOnlySpan<ArcSeg> arcs, Vector2 pointM, float aroundM, float windowM) =>
        ProjectM(arcs, pointM, aroundM, windowM, out _);

    /// <summary>
    /// <b>The same projection, with how far off it landed</b> — the squared distance from the point to the
    /// place returned, which this loop has in hand and a caller would otherwise sample the chain again to
    /// find out.
    /// </summary>
    /// <remarks>
    /// <b>It is the loop's own figure and not a second reading of the answer.</b> A caller measuring back
    /// from <see cref="SampleAt"/> walks the chain a second time and can land on the other piece of a joint
    /// the projection stood exactly on, so the two readings are not merely the same cost twice.
    /// </remarks>
    public static float ProjectM(
        ReadOnlySpan<ArcSeg> arcs, Vector2 pointM, float aroundM, float windowM, out float offSq)
    {
        // The window's far end is not clamped to the chain's length, and does not need to be: nothing
        // this loop can offer stands past the last piece's end, so a ceiling above that never bites. The
        // clamp is what used to make this cost a whole extra walk of the chain to measure it — which the
        // nearest-edge scans pay once per edge in the town.
        var fromM = MathF.Max(0f, aroundM - windowM);
        var toM = aroundM + windowM;

        var bestM = fromM;
        var bestDistanceSq = float.MaxValue;
        var startM = 0f;
        foreach (var arc in arcs)
        {
            var endM = startM + arc.LengthM;
            if (endM >= fromM && startM <= toM)
            {
                var alongM = NearestOnArc(arc, pointM);
                alongM = Math.Clamp(startM + alongM, fromM, toM) - startM;
                var distanceSq = (arc.PointAtM(alongM) - pointM).LengthSquared();
                if (distanceSq < bestDistanceSq)
                {
                    bestDistanceSq = distanceSq;
                    bestM = startM + alongM;
                }
            }

            startM = endM;
        }

        offSq = bestDistanceSq;
        return bestM;
    }

    /// <summary>
    /// <b>Where two chains cross, as the distance along each</b> — solved piece against piece and not
    /// searched for, so an answer is the crossing point itself and not the nearest station to it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every crossing the two have and not one of them</b>, as many as <paramref name="into"/> has room
    /// for and nearest first to the two places asked about. Two chains cross wherever they happen to, and
    /// <em>which</em> of those a caller means is the caller's own question to answer off its own ground —
    /// there is no distance that says it, since two lines a right angle apart cross a stride from where
    /// their edges do and two that are all but parallel cross a street away.
    /// </para>
    /// <para>
    /// <b>And <paramref name="beyondM"/> past either chain's own ends</b>, which is where two lines that
    /// stop short of each other cross: the two arms of a junction end at their own mouths and the corner
    /// between them stands on neither. A crossing found out there comes back as a distance past the chain's
    /// length or short of nothing, on the circle or the line the end piece lies on — the piece run on, never
    /// a tangent laid off it.
    /// </para>
    /// <para>
    /// <b>Every case is closed form</b>: two straights are one determinant, a straight and an arc are a
    /// quadratic, and two arcs are the radical line between two circles. Nothing here bisects, so a
    /// crossing is exact to a float rather than to whatever a walk was stationed at.
    /// </para>
    /// </remarks>
    public static int CrossingsM(
        ReadOnlySpan<ArcSeg> one, ReadOnlySpan<ArcSeg> other, float nearOneM, float nearOtherM,
        Span<SplineCrossing> into, float beyondM = 0f)
    {
        var kept = 0;
        Span<float> alongOneM = stackalloc float[2];
        Span<float> alongOtherM = stackalloc float[2];
        var onePieceM = 0f;
        for (var onePiece = 0; onePiece < one.Length; onePiece++)
        {
            var otherPieceM = 0f;
            for (var otherPiece = 0; otherPiece < other.Length; otherPiece++)
            {
                var found = AlongBoth(
                    one[onePiece], other[otherPiece], Behind(onePiece, one.Length, beyondM),
                    Past(onePiece, one.Length, beyondM), Behind(otherPiece, other.Length, beyondM),
                    Past(otherPiece, other.Length, beyondM), alongOneM, alongOtherM);
                for (var at = 0; at < found; at++)
                {
                    kept = Ranked(
                        into, kept, new SplineCrossing(onePieceM + alongOneM[at], otherPieceM + alongOtherM[at]),
                        nearOneM, nearOtherM);
                }

                otherPieceM += other[otherPiece].LengthM;
            }

            onePieceM += one[onePiece].LengthM;
        }

        return kept;
    }

    /// <summary>
    /// <b>Where two pieces cross, as the distance along each of them</b> — the same closed form
    /// <see cref="CrossingsM"/> is built out of (<see cref="CrossingsOf(in ArcSeg, in ArcSeg, Span{Vector2})"/>,
    /// <see cref="AlongOf"/>), asked of one piece against one piece and ranked by nothing.
    /// </summary>
    /// <remarks>
    /// <b>For the caller that is arranging pieces rather than following a chain.</b> <c>CrossingsM</c> walks
    /// two whole chains and keeps the crossings nearest a place the caller already has; a caller cutting
    /// every piece at every crossing has no such place and wants all of them, piece by piece, with the
    /// distances measured along the pieces themselves.
    /// </remarks>
    public static int CrossingsOf(
        in ArcSeg one, in ArcSeg other, Span<float> alongOneM, Span<float> alongOtherM) =>
        CrossingsOf(one, other, 0f, alongOneM, alongOtherM);

    /// <summary>
    /// <b>The same, with both pieces run on past their own two ends by <paramref name="beyondM"/></b> — for
    /// the caller that is asking where two pieces <em>would</em> meet rather than where they do.
    /// </summary>
    /// <remarks>
    /// <b>It is what makes a corner between two bends exact.</b> Two pieces that meet at a corner have
    /// offsets that cross somewhere neither of them reaches, and the straight-line mitre that stands in for
    /// it — the distance against the half turn — is the answer only where both pieces are straight. On a
    /// bend it is out by centimetres, which is a corner that does not close.
    /// </remarks>
    public static int CrossingsOf(
        in ArcSeg one, in ArcSeg other, float beyondM, Span<float> alongOneM, Span<float> alongOtherM) =>
        AlongBoth(one, other, beyondM, beyondM, beyondM, beyondM, alongOneM, alongOtherM);

    /// <summary>
    /// <b>Where two pieces' circles or lines meet, as the distance along each</b> — every meeting both of
    /// them reach, each run on behind and past itself by the figures given.
    /// </summary>
    /// <remarks>
    /// <b>Solved about the first piece's own start where a float is coarser than a millimetre</b>
    /// (<see cref="LineTolerance.Coarseness"/>). A distance along a piece is the same number in any frame,
    /// but the point the two meet at is not: read at a town's far edge it is rounded to two millimetres
    /// before either piece is asked where it stands, and two pieces cut there stop that far apart. Moved to
    /// the piece's start, the two starts differ exactly and everything after is arithmetic on metres.
    /// </remarks>
    static int AlongBoth(
        in ArcSeg one, in ArcSeg other, float oneBehindM, float onePastM, float otherBehindM, float otherPastM,
        Span<float> alongOneM, Span<float> alongOtherM)
    {
        if (LineTolerance.Coarseness(one.StartM) > 1f)
        {
            var originM = one.StartM;
            return AlongBoth(
                one with { StartM = Vector2.Zero }, other with { StartM = other.StartM - originM }, oneBehindM,
                onePastM, otherBehindM, otherPastM, alongOneM, alongOtherM);
        }

        Span<Vector2> atM = stackalloc Vector2[2];
        var found = CrossingsOf(one, other, atM);
        var kept = 0;
        for (var at = 0; at < found && kept < alongOneM.Length && kept < alongOtherM.Length; at++)
        {
            if (!AlongOf(one, atM[at], oneBehindM, onePastM, out var alongOne)) continue;
            if (!AlongOf(other, atM[at], otherBehindM, otherPastM, out var alongOther)) continue;

            alongOneM[kept] = alongOne;
            alongOtherM[kept] = alongOther;
            kept++;
        }

        return kept;
    }

    /// <summary>
    /// How far a piece may be run on behind and past itself: only the chain's own two ends run on, since a
    /// crossing beyond the end of a piece in the middle of one is the next piece's to answer for.
    /// </summary>
    static float Behind(int piece, int pieces, float beyondM) => piece == 0 ? beyondM : 0f;

    static float Past(int piece, int pieces, float beyondM) => piece == pieces - 1 ? beyondM : 0f;

    /// <summary>
    /// One crossing put in its place among those already found, and the furthest dropped where there is no
    /// room for it.
    /// </summary>
    static int Ranked(
        Span<SplineCrossing> into, int kept, SplineCrossing crossing, float nearOneM, float nearOtherM)
    {
        var offM = OffM(crossing, nearOneM, nearOtherM);
        var at = kept;
        while (at > 0 && OffM(into[at - 1], nearOneM, nearOtherM) > offM)
        {
            if (at < into.Length) into[at] = into[at - 1];
            at--;
        }

        if (at >= into.Length) return kept;

        into[at] = crossing;
        return Math.Min(kept + 1, into.Length);
    }

    /// <summary>How far off the two places asked about one crossing stands, along the two chains together.</summary>
    static float OffM(SplineCrossing crossing, float oneM, float otherM) =>
        MathF.Abs(crossing.OneM - oneM) + MathF.Abs(crossing.OtherM - otherM);

    /// <summary>
    /// Where the circles or lines two pieces lie on meet, as points and without regard to whether either
    /// piece reaches them — which is <see cref="AlongOf"/>'s question and is asked of each in turn.
    /// </summary>
    static int CrossingsOf(in ArcSeg one, in ArcSeg other, Span<Vector2> into)
    {
        var oneStraight = MathF.Abs(one.Curvature) < StraightCurvature;
        var otherStraight = MathF.Abs(other.Curvature) < StraightCurvature;

        if (oneStraight && otherStraight) return StraightsCross(one, other, into);
        if (MathF.Abs(one.Curvature) < FlatCurvature && MathF.Abs(other.Curvature) < FlatCurvature)
        {
            return FlatsCross(one, other, into);
        }

        if (oneStraight) return StraightCrossesArc(one, other, into);
        if (otherStraight) return StraightCrossesArc(other, one, into);

        return ArcsCross(one, other, into);
    }

    /// <summary>
    /// <b>Below this a bend is a straight to the circle pair's arithmetic</b>: a radius of ten kilometres,
    /// past which two pieces' circles cross on a radical line their two curvatures all but cancel out of.
    /// </summary>
    /// <remarks>
    /// <b>It is the curvature a straight comes back with and not a bend anything lays</b>: a ring's straight
    /// re-struck between two ends a millimetre off its own (<c>ArcRings.Tightened</c>) curves by a few millionths,
    /// and two of those meeting at five degrees read as circles five hundred kilometres across whose radical
    /// line is nought — and the crossing the whole of a wedge's offset turns on was not found.
    /// </remarks>
    const float FlatCurvature = 1e-4f;

    /// <summary>
    /// <b>Where two all but straight pieces cross</b>: where their chords do, run on to the pieces themselves
    /// by Newton's steps along both at once — exact to the arithmetic in a couple of them, the chords being a
    /// sagitta off at most.
    /// </summary>
    static int FlatsCross(in ArcSeg one, in ArcSeg other, Span<Vector2> into)
    {
        var oneChord = new ArcSeg(one.StartM, Facing(one.EndM - one.StartM), one.LengthM, 0f);
        var otherChord = new ArcSeg(other.StartM, Facing(other.EndM - other.StartM), other.LengthM, 0f);
        if (StraightsCross(oneChord, otherChord, into) == 0) return 0;

        var oneM = Vector2.Dot(into[0] - one.StartM, oneChord.StartUnit);
        var otherM = Vector2.Dot(into[0] - other.StartM, otherChord.StartUnit);
        for (var step = 0; step < FlatSteps; step++)
        {
            var oneAt = one.PointAtM(oneM);
            var oneUnit = Heading.Unit(one.HeadingAtRad(oneM));
            var otherUnit = Heading.Unit(other.HeadingAtRad(otherM));
            var across = Cross(oneUnit, otherUnit);
            if (MathF.Abs(across) < ApartToCross) return 0;

            var gapM = other.PointAtM(otherM) - oneAt;
            oneM += Cross(gapM, otherUnit) / across;
            otherM += Cross(gapM, oneUnit) / across;
        }

        into[0] = one.PointAtM(oneM);
        return 1;

        static float Facing(Vector2 runM) => MathF.Atan2(runM.Y, runM.X);
    }

    /// <summary>How many Newton's steps two flat pieces' crossing is run on by: the first lands within the arithmetic.</summary>
    const int FlatSteps = 3;

    /// <summary>The one point two straights meet at, which two parallels have none of.</summary>
    static int StraightsCross(in ArcSeg one, in ArcSeg other, Span<Vector2> into)
    {
        var across = Cross(one.StartUnit, other.StartUnit);
        if (MathF.Abs(across) < ApartToCross) return 0;

        var atM = Cross(other.StartM - one.StartM, other.StartUnit) / across;
        into[0] = one.StartM + (atM * one.StartUnit);
        return 1;
    }

    /// <summary>
    /// <b>A piece's circle written from its own start</b>: <c>k·|p|² = 2·(p·n)</c> for <c>p</c> measured off
    /// <see cref="ArcSeg.StartM"/>, with <c>n</c> the way the piece turns.
    /// </summary>
    /// <remarks>
    /// <b>Everything here is solved in this form and never against the centre</b>, which is why it is worth
    /// a note. A road's bend is a radius of kilometres, so <c>|start − centre|² − r²</c> is the difference
    /// of two numbers agreeing to six figures and a float carries seven: the crossing that came back was a
    /// metre and a half from the point both lines actually stand on, and a corner drawn on it landed near
    /// the junction rather than in it. Written this way the curvature is a factor and never a reciprocal, a
    /// straight is the same equation at <c>k = 0</c>, and nothing large is ever cancelled.
    /// </remarks>
    static Vector2 TurnOf(in ArcSeg arc) => Heading.RightOf(arc.StartUnit);

    /// <summary>
    /// The two points a straight meets a piece's circle at, as the roots of that circle
    /// (<see cref="TurnOf"/>) walked along the straight — one where it is tangent and none where it misses.
    /// </summary>
    static int StraightCrossesArc(in ArcSeg straight, in ArcSeg arc, Span<Vector2> into)
    {
        var turn = arc.Curvature;
        var offM = straight.StartM - arc.StartM;
        var facing = TurnOf(arc);

        var a = turn;
        var b = 2f * ((turn * Vector2.Dot(offM, straight.StartUnit)) - Vector2.Dot(straight.StartUnit, facing));
        var c = (turn * offM.LengthSquared()) - (2f * Vector2.Dot(offM, facing));

        var found = Roots(a, b, c, out var oneM, out var otherM);
        if (found > 0) into[0] = straight.StartM + (oneM * straight.StartUnit);
        if (found > 1) into[1] = straight.StartM + (otherM * straight.StartUnit);

        return found;
    }

    /// <summary>
    /// The two points two pieces' circles meet at: their two equations (<see cref="TurnOf"/>) cross-scaled
    /// and subtracted, which cancels the square terms and leaves the radical line, then that line walked
    /// against the first circle.
    /// </summary>
    static int ArcsCross(in ArcSeg one, in ArcSeg other, Span<Vector2> into)
    {
        var oneTurn = one.Curvature;
        var otherTurn = other.Curvature;
        var oneFacing = TurnOf(one);
        var otherFacing = TurnOf(other);
        var offM = other.StartM - one.StartM;

        // The radical line, as p·across + acrossM = 0 for p measured off the first piece's start.
        var across = 2f * ((otherTurn * oneFacing) - (oneTurn * otherFacing) - (oneTurn * otherTurn * offM));
        var acrossM = (oneTurn * otherTurn * offM.LengthSquared()) + (2f * oneTurn * Vector2.Dot(offM, otherFacing));
        var apart = across.LengthSquared();
        if (apart < ApartToCross * ApartToCross) return 0;

        var footM = across * (-acrossM / apart);
        var along = Heading.RightOf(across) / MathF.Sqrt(apart);

        var found = Roots(
            oneTurn,
            -2f * Vector2.Dot(along, oneFacing),
            (oneTurn * footM.LengthSquared()) - (2f * Vector2.Dot(footM, oneFacing)),
            out var oneAtM,
            out var otherAtM);

        if (found > 0) into[0] = one.StartM + footM + (oneAtM * along);
        if (found > 1) into[1] = one.StartM + footM + (otherAtM * along);

        return found;
    }

    /// <summary>
    /// The real roots of <c>a·t² + b·t + c</c>, <b>taken the way that does not cancel</b>: the root whose
    /// sign matches the linear term is solved for and the other read off the product of the two, so a
    /// quadratic that is all but linear — a bend a kilometre across, which is most of a town's — answers
    /// with the same precision as a straight.
    /// </summary>
    static int Roots(float a, float b, float c, out float oneM, out float otherM)
    {
        oneM = 0f;
        otherM = 0f;
        if (MathF.Abs(a) < StraightCurvature)
        {
            if (MathF.Abs(b) < ApartToCross) return 0;

            oneM = -c / b;
            return 1;
        }

        var under = (b * b) - (4f * a * c);
        if (under < 0f) return 0;

        var rootM = MathF.Sqrt(under);
        var halved = -0.5f * (b + (b < 0f ? -rootM : rootM));
        oneM = halved / a;
        otherM = MathF.Abs(halved) > ApartToCross ? c / halved : oneM;

        if (oneM > otherM) (oneM, otherM) = (otherM, oneM);

        return rootM > 0f ? 2 : 1;
    }

    /// <summary>
    /// How far along a piece a point on its own circle or line stands, and whether the piece reaches it at
    /// all — a hair's breadth past either end being reached, since a crossing that lands on the join
    /// between two pieces belongs to both, and <paramref name="behindM"/> or <paramref name="pastM"/>
    /// further where the caller asked for the piece run on.
    /// </summary>
    static bool AlongOf(in ArcSeg arc, Vector2 pointM, float behindM, float pastM, out float atM)
    {
        var offM = pointM - arc.StartM;
        if (MathF.Abs(arc.Curvature) < StraightCurvature)
        {
            atM = Vector2.Dot(offM, arc.StartUnit);
        }
        else
        {
            // <b>Read off the chord and not off the centre.</b> The chord to a point on the piece's own
            // circle stands half the turn off the start heading. Read as the angle between two vectors out
            // of the centre, a road's bend puts that centre kilometres away and both vectors are that long,
            // so the angle between them is what is left of two floats agreeing to six figures.
            var halfTurnRad = MathF.Atan2(Cross(arc.StartUnit, offM), Vector2.Dot(arc.StartUnit, offM));
            atM = ChordAlongM(arc, offM, halfTurnRad);

            // <b>The chord names one turn of the circle and the caller may mean the turn before it.</b> A
            // point a stride behind the start is most of a turn ahead of it as readily as a stride behind,
            // and a piece that turns past a half circle reaches one past its own end the long way round.
            var roundM = MathF.Tau / MathF.Abs(arc.Curvature);
            if (atM > arc.LengthM + pastM + OnThePieceM) atM -= roundM;
            if (atM < -behindM - OnThePieceM) atM += roundM;
        }

        if (atM < -behindM - OnThePieceM || atM > arc.LengthM + pastM + OnThePieceM) return false;

        atM = Math.Clamp(atM, -behindM, arc.LengthM + pastM);
        return true;
    }

    /// <summary>
    /// <b>How far along a piece a point on its circle stands, measured as the chord and not as the turn</b>:
    /// the chord's own length divided by the <c>sinc</c> of the half turn it subtends, which is
    /// <see cref="ArcSeg.PointAtM"/> read backwards and the whole of why it is written this way.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The turn over the curvature multiplies the error by the radius.</b> A road's bend subtends a
    /// fraction of a degree over a piece, so the half turn is a small angle read off two town-sized
    /// coordinates — and a town coordinate carries its last bit at a quarter of a millimetre, which over a
    /// chord of a few metres is tens of microradians. Divided by a curvature of a hundred-thousandth that
    /// is <em>metres</em>: at a kilometre and a half from the origin, a crossing on a bend of a hundred
    /// kilometres' radius came back half a metre from where the two lines actually meet, one on a bend of a
    /// thousand came back four metres out, and the shallowest bends missed it altogether — the distance
    /// landing off the piece, and a plain crossing reading as no crossing at all.
    /// </para>
    /// <para>
    /// <b>The chord carries no such factor.</b> Its length is a metre-scale quantity measured to a metre
    /// scale's precision, and the <c>sinc</c> it is divided by is within a part in ten thousand of one over
    /// everything a road bends through — so the answer is as accurate as the point handed in. Past a
    /// radian of half turn the <c>sinc</c> is small enough to cancel and the turn is exact enough to use,
    /// the curvature there being large: that is the one case this hands back.
    /// </para>
    /// <para>
    /// <b>A chord standing within a radian of the start heading is a point ahead of the start, whatever the
    /// curvature is doing</b>, so the distance handed back is never negative. A point <em>behind</em> the
    /// start stands a half turn off that heading however shallow the bend — the chord to it points backwards
    /// — so it is the turn's case, and comes back as the far side of a whole circle for the caller to wrap
    /// (<see cref="AlongOf"/>). <b>Taking the sign off the half turn's own sign instead decides it on
    /// noise</b>: over a piece that barely bends the true half turn is a fraction of a microradian while the
    /// point handed in carries tens of them, so a crossing plainly in front reads as one behind and is
    /// wrapped a whole circle off the piece.
    /// </para>
    /// </remarks>
    static float ChordAlongM(in ArcSeg arc, Vector2 offM, float halfTurnRad) =>
        MathF.Abs(halfTurnRad) >= ChordHalfTurnRad
            ? 2f * halfTurnRad / arc.Curvature
            : offM.Length() / ArcSeg.Sinc(halfTurnRad);

    /// <summary>
    /// Where the chord stops being the better reading of a distance along and the turn takes over: a radian
    /// of half turn, at which the <c>sinc</c> is still 0.84 and nothing it divides is cancelled.
    /// </summary>
    const float ChordHalfTurnRad = 1f;

    /// <summary>Below this two lines or two centres are one and there is no one point to answer with.</summary>
    const float ApartToCross = 1e-6f;

    /// <summary>How far past a piece's own ends a crossing may stand and still be that piece's: a tenth of a millimetre.</summary>
    const float OnThePieceM = 1e-4f;

    /// <summary>
    /// A polyline laid as a chain, with every corner it turns at <b>rounded over a margin either side of
    /// the point</b> rather than laid as the point itself. A right angle laid as a point is a standstill:
    /// whatever follows the line has to stop turning before it can go on.
    /// </summary>
    /// <remarks>
    /// The margin is bounded by half of each of the two segments the corner stands between, so two corners
    /// a stride apart share the ground rather than overrunning one another, and a corner that has no room
    /// for its full margin gets the room it has.
    /// </remarks>
    public static int FilletedInto(ReadOnlySpan<Vector2> pointsM, float marginM, Span<ArcSeg> into)
    {
        var written = 0;
        var cursorM = pointsM[0];

        for (var corner = 1; corner < pointsM.Length - 1; corner++)
        {
            var arriving = pointsM[corner] - pointsM[corner - 1];
            var leaving = pointsM[corner + 1] - pointsM[corner];
            var arrivingM = arriving.Length();
            var leavingM = leaving.Length();
            if (arrivingM < 1e-4f || leavingM < 1e-4f) continue;

            arriving /= arrivingM;
            leaving /= leavingM;
            var turnRad = MathF.Atan2(Cross(arriving, leaving), Vector2.Dot(arriving, leaving));
            if (MathF.Abs(turnRad) < 1e-4f) continue;

            var reachM = MathF.Min(marginM, MathF.Min(arrivingM, leavingM) * 0.5f);
            var radiusM = reachM / MathF.Tan(MathF.Abs(turnRad) * 0.5f);
            var enterM = pointsM[corner] - arriving * reachM;

            written += Straight(cursorM, enterM, into[written..]);
            var headingRad = MathF.Atan2(arriving.Y, arriving.X);
            var sign = turnRad < 0f ? -1f : 1f;
            into[written++] = new ArcSeg(enterM, headingRad, radiusM * MathF.Abs(turnRad), sign / radiusM);
            cursorM = pointsM[corner] + leaving * reachM;
        }

        return written + Straight(cursorM, pointsM[^1], into[written..]);
    }

    /// <summary>
    /// <b>A polyline laid as straights, with every corner it turns at rounded at one radius</b> — or, where the
    /// two legs either side have no room for it, at the widest the shorter leg's half affords. A leg is one
    /// straight however many places along it the line passes, so a point a leg merely runs through is no
    /// corner and costs nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Whether a cramped corner came out at a radius anything can hold is the caller's to ask; this lays
    /// what the legs have room for. At most <c>2·points − 3</c> pieces.
    /// </para>
    /// <para>
    /// <b>A straight takes its leg's bearing and never the bearing between its own two ends.</b> Two corners
    /// that each take half of the leg between them leave a straight of nothing, whose two ends are one point
    /// read twice — and far enough from the origin that point's two readings differ by a float's own step in
    /// any direction at all, so a heading read off them is noise and the line turns through it.
    /// </para>
    /// </remarks>
    public static int RoundedInto(ReadOnlySpan<Vector2> pointsM, float radiusM, Span<ArcSeg> into)
    {
        var reachM = pointsM.Length > 2 ? new float[pointsM.Length - 2] : [];
        for (var corner = 1; corner < pointsM.Length - 1; corner++)
        {
            var arrivingM = Vector2.Distance(pointsM[corner - 1], pointsM[corner]);
            var leavingM = Vector2.Distance(pointsM[corner], pointsM[corner + 1]);
            reachM[corner - 1] = MathF.Min(radiusM * HalfTurnTan(pointsM, corner), MathF.Min(arrivingM, leavingM) * 0.5f);
        }

        return RoundedInto(pointsM, reachM, into);
    }

    /// <summary>
    /// <b>A polyline laid as straights, with each corner rounded from its own reach</b> — how far back along
    /// each of its two legs the round starts, which sets its radius from the turn. A caller sharing a leg
    /// between the corners at its ends keeps the two reaches inside it; nothing here asks.
    /// </summary>
    /// <param name="reachM">One a corner, the first for the polyline's second point.</param>
    public static int RoundedInto(ReadOnlySpan<Vector2> pointsM, ReadOnlySpan<float> reachM, Span<ArcSeg> into)
    {
        var written = 0;
        var cursorM = pointsM[0];
        var leg = Vector2.Zero;

        for (var corner = 1; corner < pointsM.Length - 1; corner++)
        {
            var arriving = pointsM[corner] - pointsM[corner - 1];
            var leaving = pointsM[corner + 1] - pointsM[corner];
            var arrivingM = arriving.Length();
            var leavingM = leaving.Length();
            if (arrivingM < 1e-4f || leavingM < 1e-4f) continue;

            arriving /= arrivingM;
            leaving /= leavingM;
            var turnRad = MathF.Atan2(Cross(arriving, leaving), Vector2.Dot(arriving, leaving));
            if (MathF.Abs(turnRad) < 1e-4f) continue;

            var cornerReachM = reachM[corner - 1];
            var cornerRadiusM = cornerReachM / MathF.Tan(MathF.Abs(turnRad) * 0.5f);
            var enterM = pointsM[corner] - arriving * cornerReachM;

            written += Straight(cursorM, enterM, arriving, into[written..]);
            into[written++] = new ArcSeg(
                enterM, MathF.Atan2(arriving.Y, arriving.X), cornerRadiusM * MathF.Abs(turnRad),
                MathF.Sign(turnRad) / cornerRadiusM);
            cursorM = pointsM[corner] + leaving * cornerReachM;
            leg = leaving;
        }

        return written + Straight(cursorM, pointsM[^1], leg, into[written..]);
    }

    /// <summary>
    /// The tangent of half the turn a polyline makes at one of its points, which is a corner's reach over its
    /// radius — read the way <see cref="RoundedInto(ReadOnlySpan{Vector2}, ReadOnlySpan{float}, Span{ArcSeg})"/>
    /// reads the turn, so a reach set from it rounds at the radius it was set for.
    /// </summary>
    public static float HalfTurnTan(ReadOnlySpan<Vector2> pointsM, int corner)
    {
        var arriving = pointsM[corner] - pointsM[corner - 1];
        var leaving = pointsM[corner + 1] - pointsM[corner];
        arriving /= arriving.Length();
        leaving /= leaving.Length();
        return MathF.Tan(MathF.Abs(MathF.Atan2(Cross(arriving, leaving), Vector2.Dot(arriving, leaving))) * 0.5f);
    }

    static int Straight(Vector2 fromM, Vector2 toM, Span<ArcSeg> into) => Straight(fromM, toM, Vector2.Zero, into);

    /// <param name="along">
    /// The bearing of the leg the straight lies on, or zero to read it off the two ends. Given, the length is
    /// the run's along it, so a sliver the floats leave pointing back down the leg is no straight at all.
    /// </param>
    static int Straight(Vector2 fromM, Vector2 toM, Vector2 along, Span<ArcSeg> into)
    {
        var run = toM - fromM;
        var heading = along == Vector2.Zero ? run : along;
        var lengthM = along == Vector2.Zero ? run.Length() : Vector2.Dot(run, along);
        if (lengthM < 1e-4f) return 0;

        into[0] = new ArcSeg(fromM, MathF.Atan2(heading.Y, heading.X), lengthM, 0f);
        return 1;
    }

    /// <summary>
    /// The two arcs that leave one pose and arrive at another, tangent to both and to each other — the
    /// join a route makes through a junction, where the lane in and the lane out are two fixed poses
    /// and everything between them is the assembler's to draw.
    /// </summary>
    /// <remarks>
    /// A biarc with equal tangent lengths: it exists for every pair of poses that are not the same
    /// point, it is <c>G¹</c> at the join by construction, and it is two <see cref="ArcSeg"/>s — so a
    /// connector is sampled, offset and projected onto by the code that already does those to a road.
    /// It is not curvature-bounded: whether a car can hold the line is the follower's problem, and a
    /// connector tighter than the steering lock is a line the car visibly rides wide of rather than a
    /// refusal in the middle of a tick.
    /// </remarks>
    public static int BiarcInto(Vector2 fromM, float fromHeadingRad, Vector2 toM, float toHeadingRad, Span<ArcSeg> into)
    {
        var from = Heading.Unit(fromHeadingRad);
        var to = Heading.Unit(toHeadingRad);
        var chord = toM - fromM;
        if (chord.LengthSquared() < 1e-8f) return 0;

        // The equal-tangent biarc: 2(1 − T₁·T₂)·δ² + 2(chord·(T₁+T₂))·δ − |chord|² = 0. The leading
        // term is |T₁−T₂|² and is therefore never negative, which is what makes the root positive and
        // unique — writing it the other way up leaves two positive roots and picking the larger draws a
        // connector that loops for hundreds of metres, which is what it did.
        var tangents = from + to;
        var a = 2f * (1f - Vector2.Dot(from, to));
        var b = 2f * Vector2.Dot(chord, tangents);
        var c = -Vector2.Dot(chord, chord);

        float tangentM;
        if (a < 1e-6f)
        {
            // Parallel tangents: the quadratic degenerates to a linear one.
            if (MathF.Abs(b) < 1e-6f) return One(fromM, fromHeadingRad, toM, into);

            tangentM = -c / b;
        }
        else
        {
            var discriminant = b * b - 4f * a * c;
            if (discriminant < 0f) return One(fromM, fromHeadingRad, toM, into);

            // The same root either way up, taken the way that subtracts nothing alike: for two poses a few degrees
            // apart a is a hair and the root's square is b's own, and −b plus it is a difference of two near-equal
            // floats that came out metres wrong — a connector between two lanes carrying on with a kink a few
            // centimetres long at its start, and its neighbour, a lane over, without one.
            var rootM = MathF.Sqrt(discriminant);
            tangentM = b > 0f ? -2f * c / (b + rootM) : (-b + rootM) / (2f * a);
        }

        if (tangentM <= 0f) return One(fromM, fromHeadingRad, toM, into);

        var jointM = ((fromM + from * tangentM) + (toM - to * tangentM)) * 0.5f;
        into[0] = ArcThrough(fromM, fromHeadingRad, jointM);
        into[1] = ArcThrough(jointM, into[0].HeadingAtRad(into[0].LengthM), toM);
        return 2;
    }

    /// <summary>
    /// The one arc of a given radius that joins two poses, and the straight either side of it — <b>the whole
    /// turn made at once on a circle the caller names</b>, with the lines the two poses are already on left
    /// straight for everything the turn does not spend.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The radius is given rather than solved for, which is the whole of the difference from</b>
    /// <see cref="BiarcInto"/>: a biarc spends the ground between the poses on the turn and so turns the
    /// whole way along it, while this spends the turn's own and nothing else. Where a car is at a standstill
    /// the circle is the car's, not the room's.
    /// </para>
    /// <para>
    /// <b>The turn sits at the corner the two lines make.</b> An arc of this radius through a turn stands off
    /// the point those lines cross by the tangent length either side, so the place it begins is arithmetic
    /// and not a search, and what is left over at each end is straight.
    /// </para>
    /// <para>
    /// <b>Nought where there is no such line</b>: poses that are parallel or facing, or either of them
    /// standing nearer the corner than the tangent length — a turn this wide does not fit in the room there
    /// is. A caller that wants a line whatever the poses falls back on <see cref="BiarcInto"/>, which joins
    /// any two. <b>A run of nothing is still a run</b>: a pose at exactly the tangent length is the tightest
    /// place this line exists at, and which side of nought the arithmetic lands on there is the rounding
    /// rather than the geometry.
    /// </para>
    /// </remarks>
    public static int StraightArcStraightInto(
        Vector2 fromM, float fromHeadingRad, Vector2 toM, float toHeadingRad, float radiusM,
        Span<ArcSeg> into)
    {
        if (!ToTheCorner(fromM, fromHeadingRad, toM, toHeadingRad, out var turnRad, out var beforeM, out var afterM))
        {
            return 0;
        }

        var from = Heading.Unit(fromHeadingRad);
        var tangentM = radiusM * MathF.Abs(MathF.Tan(turnRad * 0.5f));
        beforeM -= tangentM;
        afterM -= tangentM;
        if (beforeM < -LineTolerance.RoundingM || afterM < -LineTolerance.RoundingM) return 0;

        var laid = 0;
        beforeM = MathF.Max(beforeM, 0f);
        if (beforeM > LineTolerance.RoundingM) into[laid++] = new ArcSeg(fromM, fromHeadingRad, beforeM, 0f);

        var arc = new ArcSeg(
            fromM + (from * beforeM), fromHeadingRad, MathF.Abs(turnRad) * radiusM,
            MathF.CopySign(1f / radiusM, turnRad));
        into[laid++] = arc;

        afterM = MathF.Max(afterM, 0f);
        if (afterM > LineTolerance.RoundingM) into[laid++] = new ArcSeg(arc.EndM, toHeadingRad, afterM, 0f);

        return laid;
    }

    /// <summary>
    /// <b>The widest circle <see cref="StraightArcStraightInto"/> fits between two poses</b>: the one whose
    /// tangent length is the nearer pose's own distance from the corner their two lines make — nought where
    /// the lines are parallel or facing, or either pose stands past that corner.
    /// </summary>
    public static float WidestTurnM(Vector2 fromM, float fromHeadingRad, Vector2 toM, float toHeadingRad)
    {
        if (!ToTheCorner(fromM, fromHeadingRad, toM, toHeadingRad, out var turnRad, out var beforeM, out var afterM))
        {
            return 0f;
        }

        return MathF.Max(0f, MathF.Min(beforeM, afterM)) / MathF.Abs(MathF.Tan(turnRad * 0.5f));
    }

    /// <summary>
    /// The turn between two poses and where the point their two lines cross stands — how far ahead of the
    /// first, and how far behind the second — or false where the lines are parallel or facing.
    /// </summary>
    public static bool ToTheCorner(
        Vector2 fromM, float fromHeadingRad, Vector2 toM, float toHeadingRad, out float turnRad,
        out float fromCornerM, out float cornerToM)
    {
        turnRad = WrapRad(toHeadingRad - fromHeadingRad);
        fromCornerM = cornerToM = 0f;
        var apart = MathF.Sin(turnRad);
        if (MathF.Abs(apart) < 1e-4f) return false;

        var chord = toM - fromM;
        fromCornerM = Cross(chord, Heading.Unit(toHeadingRad)) / apart;
        cornerToM = Cross(Heading.Unit(fromHeadingRad), chord) / apart;
        return true;
    }

    /// <summary>
    /// <b>The corner between two poses that something has to be able to hold</b> — the biarc between them
    /// (<see cref="BiarcInto"/>), <b>refused where it turns tighter than <paramref name="tightestM"/> or
    /// spends more heading than a corner has to spend</b> (<see cref="SweptRad"/>), in which case nothing is
    /// written and the caller lays whatever it falls back on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>An equal-tangent biarc gives two arcs of its own choosing, and two of the answers it gives are no
    /// use to anything that has to walk or drive the line.</b> It <b>runs away</b> where the two poses are
    /// nearly parallel and offset sideways: a six-centimetre step between two stretches of one straight
    /// pavement drew a 26 m loop. And it <b>hairpins</b> where they are not: a third of the corners in a town
    /// came out tighter than the tightest circle a walker's feet can hold, some of them a centimetre across,
    /// and a body handed one of those orbits it rather than reaching the point on the far side — measured, on
    /// the day the mitre was wired in, as Odesa's given-up walks going from 37 a minute to 211.
    /// </para>
    /// <para>
    /// <b>Both failures are one failure read in heading</b>, and that is the bound: <b>a corner spends at most
    /// half a turn</b>. The heading between two poses is never more than that, so a corner that turns one way
    /// through it never needs more — and everything past it is a corner turning twice where it needed to turn
    /// once. The runaway spends a whole turn to arrive facing the way it set off; the hairpin at two
    /// near-opposite poses spends a turn and a half. <b>What is left where the bound refuses is the pivot</b>,
    /// and a pivot is possible: a body stands, turns and walks the straight, spending the heading its two ends
    /// ask for and no more. It is not free, which is why the curve is preferred wherever the curve is a
    /// corner.
    /// </para>
    /// <para>
    /// So a corner has to be all three: <b>no tighter than the radius it is asked to hold</b> — which for a
    /// walk is the circle the feet can hold at pace (<c>SimConfig.WalkerTightestTurnM</c>) — <b>no more wound
    /// than half a turn</b>, and <b>no longer than a half turn's own share of the straight it bridges</b>
    /// (<see cref="HalfATurnOfItsChord"/>). The heading catches what turns twice over; the length catches what
    /// bows out past the ground between its two ends, which spends no extra heading at all and so is
    /// invisible to the other two.
    /// </para>
    /// </remarks>
    public static int CorneredInto(
        Vector2 fromM, float fromHeadingRad, Vector2 toM, float toHeadingRad, float tightestM,
        Span<ArcSeg> into)
    {
        var laid = BiarcInto(fromM, fromHeadingRad, toM, toHeadingRad, into);
        if (laid == 0) return 0;

        var lengthM = 0f;
        var bend = 0f;
        for (var arc = 0; arc < laid; arc++)
        {
            lengthM += into[arc].LengthM;
            bend = MathF.Max(bend, MathF.Abs(into[arc].Curvature));
        }

        if (bend > 1e-6f && 1f / bend < tightestM) return 0;
        if (SweptRad(into[..laid]) > HalfATurnRad + LineTolerance.StraightOnRad) return 0;

        return lengthM <= (HalfATurnOfItsChord * (toM - fromM).Length()) + LineTolerance.JoinedM ? laid : 0;
    }

    /// <summary>
    /// <b>The line a car is driven on from one pose to another across a junction</b>: <b>on along the lane it leaves for as
    /// long as it can, turned or shifted across as late and as short as the circle it is given allows, and on along the
    /// lane it joins</b> — so the stretch it spends across other lanes' ground is the least the two ask for, and no stretch
    /// of it runs on a line of its own between theirs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A turn at a corner</b> — two poses further round than <paramref name="straightRad"/> whose lines cross ahead of
    /// the first and behind the second, no further off either than the two stand apart — is the first line, one arc of
    /// <paramref name="turnRadiusM"/> at the corner, and the second line (<see cref="StraightArcStraightInto"/>); with too
    /// little room before or after the corner for that circle, the widest one the room holds.
    /// </para>
    /// <para>
    /// <b>A shift</b> — two poses within <paramref name="straightRad"/> of straight on whose lines do not cross between
    /// them, the second ahead and at least <paramref name="carriedOnM"/> across — runs on along the first line as far as it
    /// can and crosses onto the second on two arcs of the circle
    /// turning opposite ways, each no more than a quarter turn, with the straight their inner tangent leaves between
    /// (<see cref="ShiftedLateInto"/>): a lane carried on to one offset across a box, and the far arm of a staggered
    /// crossing, turned off and back. Where the box is too short for that circle, the biarc.
    /// </para>
    /// <para>
    /// <b>A U-turn</b> — two poses within <paramref name="straightRad"/> of facing opposite ways, the second across on the
    /// side the turn goes — runs on to the further of the two and turns across on half a circle as wide as the gap.
    /// </para>
    /// <para>
    /// <b>Anything else keeps the biarc</b> (<see cref="BiarcInto"/>). A lane carried on through a bend does — within
    /// <paramref name="straightRad"/> of straight on, or <paramref name="carriedThrough"/> a place its road only bends
    /// at — and one carried on less than <paramref name="carriedOnM"/> across: lanes side by side are carried on side by
    /// side, and the paint between them with them. So does a hairpin, two poses the first of which already stands past where their
    /// lines cross, which has no line a driver takes but round, and what mends that is the lane ending sooner. <b>Nothing
    /// is swung out of onto the lanes beside</b>, however tight.
    /// </para>
    /// </remarks>
    /// <param name="turnRadiusM">The circle a turn or a shift is made on.</param>
    /// <param name="straightRad">
    /// How near straight on two poses are for the line between them to be a bend carried on, and how near facing opposite
    /// ways for it to be a U-turn.
    /// </param>
    /// <param name="carriedOnM">
    /// How far across a lane may shift and still be carried on rather than shifted: less than that, the biarc, which is
    /// what keeps the lines either side of a painted line across a box the same distance apart all the way.
    /// </param>
    /// <param name="carriedThrough">
    /// Whether the two lanes are one road carried on through a place it only bends at, nothing turning off it: then a bend
    /// however sharp is the biarc, as the lanes beside it and the paint between them are.
    /// </param>
    /// <returns>How many arcs were written, at most <see cref="MostMovementArcs"/>; nought where the poses are one point.</returns>
    public static int MovementInto(
        Vector2 fromM, float fromHeadingRad, Vector2 toM, float toHeadingRad, float turnRadiusM, float straightRad,
        float carriedOnM, bool carriedThrough, Span<ArcSeg> into)
    {
        if (Vector2.DistanceSquared(fromM, toM) < LineTolerance.RoundingM * LineTolerance.RoundingM) return 0;

        var from = Heading.Unit(fromHeadingRad);
        var to = Heading.Unit(toHeadingRad);
        var askedRad = MathF.Abs(WrapRad(toHeadingRad - fromHeadingRad));
        var cornerAhead = ToTheCorner(fromM, fromHeadingRad, toM, toHeadingRad, out var turnRad, out var beforeM, out var afterM)
            && beforeM >= LineTolerance.RoundingM && afterM >= LineTolerance.RoundingM
            && MathF.Max(beforeM, afterM) <= Vector2.Distance(fromM, toM);
        if (cornerAhead && (carriedThrough || askedRad <= straightRad)) return BiarcInto(fromM, fromHeadingRad, toM, toHeadingRad, into);

        if (cornerAhead)
        {
            var cornered = TurnedAtTheCornerInto(fromM, fromHeadingRad, toM, toHeadingRad, turnRadiusM, into);
            return cornered > 0 ? cornered : BiarcInto(fromM, fromHeadingRad, toM, toHeadingRad, into);
        }

        var aheadM = Vector2.Dot(toM - fromM, from);
        var behindM = Vector2.Dot(toM - fromM, to);
        var acrossM = Cross(from, toM - fromM);
        if ((askedRad <= straightRad || carriedThrough) && aheadM > 0f && behindM > 0f && MathF.Abs(acrossM) >= carriedOnM
            && ShiftedLateInto(fromM, fromHeadingRad, toM, toHeadingRad, turnRadiusM, into) is var shifted and > 0)
        {
            return shifted;
        }

        if (askedRad >= MathF.PI - straightRad && MathF.Abs(acrossM) >= LineTolerance.RoundingM
            && (askedRad >= MathF.PI - LineTolerance.StraightOnRad || MathF.Sign(acrossM) == MathF.Sign(turnRad)))
        {
            // Each pose driven on as far as the other stands past it, and half the gap further, which leaves the two corners
            // the gap between the lines apart: a half circle across it — and no run on after it too short to be told from
            // more of the half circle.
            var halfM = MathF.Abs(acrossM) * 0.5f;
            var backM = MathF.Max(behindM, 0f) < CarriedOnByM(halfM, toM) ? 0f : MathF.Max(behindM, 0f);
            return TwoCornersInto(fromM, fromM + (from * (MathF.Max(aheadM, 0f) + halfM)), toM - (to * (backM + halfM)), toM, halfM, into);
        }

        return BiarcInto(fromM, fromHeadingRad, toM, toHeadingRad, into);
    }

    /// <summary>
    /// <b>A turn at the corner two poses' lines make</b>: straight along the first, one arc of <paramref name="radiusM"/>
    /// — or the widest the room either side of the corner holds, where that is less — and straight along the second
    /// (<see cref="StraightArcStraightInto"/>).
    /// </summary>
    /// <remarks>
    /// <b>No straight after the arc too short to be told from more of it</b> (<see cref="CarriedOnByM"/>): one is turned
    /// into instead, on a circle that much wider; where the corner is so even that its other side is then left as short,
    /// nought. <b>The straight after the arc is laid back off the second pose</b>, on that pose's own line.
    /// </remarks>
    /// <returns>How many pieces were written, at most three; nought where the poses make no such corner.</returns>
    public static int TurnedAtTheCornerInto(
        Vector2 fromM, float fromHeadingRad, Vector2 toM, float toHeadingRad, float radiusM, Span<ArcSeg> into)
    {
        if (!ToTheCorner(fromM, fromHeadingRad, toM, toHeadingRad, out var turnRad, out _, out var afterM)) return 0;

        var widestM = WidestTurnM(fromM, fromHeadingRad, toM, toHeadingRad);
        radiusM = MathF.Min(radiusM, widestM);
        var halfTan = MathF.Abs(MathF.Tan(turnRad * 0.5f));
        var leftM = afterM - (radiusM * halfTan);
        if (leftM > LineTolerance.RoundingM && leftM < CarriedOnByM(radiusM, toM))
        {
            radiusM = MathF.Min(widestM, afterM / halfTan);
            leftM = afterM - (radiusM * halfTan);
        }

        if (leftM > LineTolerance.RoundingM && leftM < CarriedOnByM(radiusM, toM)) return 0;

        // The run onto the second pose laid back off the pose itself, so it lies on the lane's own line to the float and
        // its band's edge on the edge of whatever runs beside that lane — laid on from the arc's end, it stands the
        // arc's rounding off it, a few millimetres at a city's far edge, and two edges that near are neither one edge
        // nor two.
        var laid = StraightArcStraightInto(fromM, fromHeadingRad, toM, toHeadingRad, radiusM, into);
        if (laid > 1 && into[laid - 1].Curvature == 0f)
        {
            var runM = into[laid - 1].LengthM;
            into[laid - 1] = new ArcSeg(toM - (Heading.Unit(toHeadingRad) * runM), toHeadingRad, runM, 0f);
        }

        return laid;
    }

    /// <summary>
    /// <b>The line from one pose to another nearly the same way, shifted across as late as it can be</b>
    /// (<see cref="MovementInto"/>): on along the first pose's line as far as two arcs of <paramref name="radiusM"/>
    /// turning opposite ways — off it toward the second line and back onto it — still reach the second pose, each no
    /// more than a quarter turn, with the straight between them their inner tangent.
    /// </summary>
    /// <returns>How many pieces were written, at most four; nought where even from the first pose itself no such shift fits.</returns>
    public static int ShiftedLateInto(Vector2 fromM, float fromHeadingRad, Vector2 toM, float toHeadingRad, float radiusM, Span<ArcSeg> into)
    {
        var from = Heading.Unit(fromHeadingRad);
        var acrossM = Cross(from, toM - fromM);

        // Shifted less than a join's width, the two arcs would turn by less than a joined line keeps.
        if (MathF.Abs(acrossM) < LineTolerance.JoinedM) return 0;

        var firstSign = MathF.Sign(acrossM);
        Span<ArcSeg> shift = stackalloc ArcSeg[3];
        if (OppositeTurnsInto(fromM, fromHeadingRad, toM, toHeadingRad, radiusM, firstSign, shift) == 0) return 0;

        // As far on as the shift still fits: it fits from the first pose and not from abreast of the second, and it fits
        // from every place between short of where it stops fitting.
        var (fitsM, failsM) = (0f, Vector2.Dot(toM - fromM, from));
        while (failsM - fitsM > LineTolerance.RoundingM)
        {
            var midM = (fitsM + failsM) * 0.5f;
            if (OppositeTurnsInto(fromM + (from * midM), fromHeadingRad, toM, toHeadingRad, radiusM, firstSign, shift) > 0) fitsM = midM;
            else failsM = midM;
        }

        var laid = 0;
        if (fitsM > LineTolerance.RoundingM) into[laid++] = new ArcSeg(fromM, fromHeadingRad, fitsM, 0f);

        var turned = OppositeTurnsInto(fromM + (from * fitsM), fromHeadingRad, toM, toHeadingRad, radiusM, firstSign, shift);
        shift[..turned].CopyTo(into[laid..]);
        return laid + turned;
    }

    /// <summary>
    /// Two arcs of <paramref name="radiusM"/> turning opposite ways — the first the way <paramref name="firstSign"/> says,
    /// positive to the right — and the straight their inner tangent leaves between them, from one pose to another; or
    /// nought where the two circles overlap or either arc would turn more than a quarter turn.
    /// </summary>
    static int OppositeTurnsInto(Vector2 fromM, float fromHeadingRad, Vector2 toM, float toHeadingRad, float radiusM, float firstSign, Span<ArcSeg> into)
    {
        var firstM = fromM + (Heading.RightOf(Heading.Unit(fromHeadingRad)) * (radiusM * firstSign));
        var secondM = toM - (Heading.RightOf(Heading.Unit(toHeadingRad)) * (radiusM * firstSign));
        var acrossM = secondM - firstM;
        var apartM = acrossM.Length();
        if (apartM < 2f * radiusM) return 0;

        var offRad = MathF.Asin(MathF.Min(1f, 2f * radiusM / apartM));
        var straightRad = MathF.Atan2(acrossM.Y, acrossM.X) + (firstSign * offRad);
        var firstRad = WrapRad(straightRad - fromHeadingRad) * firstSign;
        var secondRad = WrapRad(toHeadingRad - straightRad) * -firstSign;
        // A turn the other way by less than a rounding along the circle is no turn; by more, it is a kink in the line.
        var roundingRad = LineTolerance.RoundingM / radiusM;
        if (firstRad < -roundingRad || secondRad < -roundingRad || firstRad > QuarterTurnRad || secondRad > QuarterTurnRad) return 0;

        var straight = Heading.Unit(straightRad);
        var leaveM = firstM - (Heading.RightOf(straight) * (radiusM * firstSign));
        var straightM = apartM * MathF.Cos(offRad);
        if (straightM > LineTolerance.RoundingM && straightM < CarriedOnByM(radiusM, leaveM)) return 0;

        var laid = 0;
        if (firstRad * radiusM > LineTolerance.RoundingM) into[laid++] = new ArcSeg(fromM, fromHeadingRad, firstRad * radiusM, firstSign / radiusM);
        if (straightM > LineTolerance.RoundingM) into[laid++] = new ArcSeg(leaveM, straightRad, straightM, 0f);
        if (secondRad * radiusM > LineTolerance.RoundingM)
        {
            into[laid++] = new ArcSeg(leaveM + (straight * straightM), straightRad, secondRad * radiusM, -firstSign / radiusM);
        }

        return laid;
    }

    /// <summary>
    /// The line from one point through two corners to another, each corner rounded as wide as half the legs either side of
    /// it allow, and no wider than <paramref name="reachM"/>.
    /// </summary>
    static int TwoCornersInto(Vector2 fromM, Vector2 firstM, Vector2 secondM, Vector2 toM, float reachM, Span<ArcSeg> into)
    {
        Span<Vector2> cornersM = [fromM, firstM, secondM, toM];
        var acrossM = Vector2.Distance(firstM, secondM) * 0.5f;
        Span<float> reachesM =
        [
            MathF.Min(reachM, MathF.Min(Vector2.Distance(fromM, firstM), acrossM)),
            MathF.Min(reachM, MathF.Min(acrossM, Vector2.Distance(secondM, toM))),
        ];
        return RoundedInto(cornersM, reachesM, into);
    }

    /// <summary>
    /// <b>The line from one pose to another turned at once and turned last</b>: a circle of <paramref name="radiusM"/>
    /// off the first pose, the straight tangent to it and to the same circle off the second, and that circle into the
    /// second — both turning the one way <paramref name="sideSign"/> says, positive to the right. It keeps nearer the
    /// inside of the turn than any other line holding that circle, which is what a turn across another made at the same
    /// time needs.
    /// </summary>
    /// <returns>How many pieces were written, at most three; nought where it would turn further than once round to the second pose.</returns>
    public static int TurnedAtOnceInto(
        Vector2 fromM, float fromHeadingRad, Vector2 toM, float toHeadingRad, float radiusM, float sideSign, Span<ArcSeg> into)
    {
        var firstM = fromM + (Heading.RightOf(Heading.Unit(fromHeadingRad)) * (radiusM * sideSign));
        var secondM = toM + (Heading.RightOf(Heading.Unit(toHeadingRad)) * (radiusM * sideSign));
        var acrossM = secondM - firstM;
        var straightM = acrossM.Length();
        var straightRad = straightM > LineTolerance.RoundingM ? MathF.Atan2(acrossM.Y, acrossM.X) : toHeadingRad;
        var roundingRad = LineTolerance.RoundingM / radiusM;
        var firstRad = Round(sideSign * (straightRad - fromHeadingRad), roundingRad);
        var secondRad = Round(sideSign * (toHeadingRad - straightRad), roundingRad);
        if (firstRad + secondRad > Round(sideSign * (toHeadingRad - fromHeadingRad), roundingRad) + LineTolerance.StraightOnRad) return 0;
        if (firstRad * radiusM > LineTolerance.RoundingM && straightM > LineTolerance.RoundingM && straightM < CarriedOnByM(radiusM, fromM)) return 0;

        var laid = 0;
        if (firstRad * radiusM > LineTolerance.RoundingM) into[laid++] = new ArcSeg(fromM, fromHeadingRad, firstRad * radiusM, sideSign / radiusM);

        var leaveM = firstM - (Heading.RightOf(Heading.Unit(straightRad)) * (radiusM * sideSign));
        if (straightM > LineTolerance.RoundingM) into[laid++] = new ArcSeg(leaveM, straightRad, straightM, 0f);
        if (secondRad * radiusM > LineTolerance.RoundingM)
        {
            into[laid++] = new ArcSeg(leaveM + (Heading.Unit(straightRad) * straightM), straightRad, secondRad * radiusM, sideSign / radiusM);
        }

        return laid;

        // A turn one way, from nought up to once round; one the other way by more than a rounding along the circle is
        // most of a turn round, and not a kink in the line.
        static float Round(float turnRad, float roundingRad)
        {
            var rad = turnRad % (2f * MathF.PI);
            return rad < -roundingRad ? rad + (2f * MathF.PI) : MathF.Max(rad, 0f);
        }
    }

    /// <summary>
    /// <b>The longest straight an arc of this radius carries on into</b> (<see cref="CarriesOn"/>): one so short the arc
    /// laid on over it stays within a rounding of its end, so a joined line takes it for more of the arc, and a turn drawn
    /// with one after its arc turns further than it was asked by the length of it.
    /// </summary>
    public static float CarriedOnByM(float radiusM, Vector2 atM) =>
        MathF.Sqrt(2f * LineTolerance.At(LineTolerance.RoundingM, atM) * radiusM);

    /// <summary>The tightest circle anywhere in a chain, and none on a straight.</summary>
    static float TightestM(ReadOnlySpan<ArcSeg> arcs)
    {
        var bend = 0f;
        foreach (var arc in arcs) bend = MathF.Max(bend, MathF.Abs(arc.Curvature));

        return bend <= StraightCurvature ? float.PositiveInfinity : 1f / bend;
    }

    /// <summary>The most arcs <see cref="MovementInto"/> writes: a jog's straight, arc, straight, arc and straight.</summary>
    public const int MostMovementArcs = 5;

    const float QuarterTurnRad = MathF.PI * 0.5f;

    /// <summary>
    /// <b>The most heading one corner spends</b> (<see cref="CorneredInto"/>): half a turn, which is the most
    /// the heading between any two poses can ask for.
    /// </summary>
    public const float HalfATurnRad = MathF.PI;

    /// <summary>
    /// <b>The most ground one corner covers, as a share of the straight between its own two ends</b>
    /// (<see cref="CorneredInto"/>): what a half turn on one circle covers of its own chord, which is
    /// <c>(θ/2)/sin(θ/2)</c> at θ of half a turn — π/2, or a little over one and a half.
    /// </summary>
    /// <remarks>
    /// <b>It is the shape's own figure and not a tolerance.</b> An arc of any turn covers that much of its
    /// chord and no more, the ratio rising with the turn and the half turn being the most a corner asks for
    /// (<see cref="HalfATurnRad"/>) — so a line over it is not an arc between its two ends at all but two
    /// arcs bowing out past the ground between them.
    /// </remarks>
    public static readonly float HalfATurnOfItsChord = HalfATurnRad * 0.5f / MathF.Sin(HalfATurnRad * 0.5f);

    /// <summary>
    /// <b>What turning on the spot costs between two poses, in metres of the ground a curve would cover</b> —
    /// the straight between them plus the heading a body standing still has to spend to walk it and arrive
    /// facing the right way.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A pivot is priced in metres and the exchange rate is exact.</b> The tightest circle anything holds
    /// is its pace over its turn rate, so a radian turned on the spot takes exactly as long as
    /// <paramref name="tightestM"/> of walking — which makes the tightest arc and the pivot cost the same for
    /// the same change of heading, and every wider arc a trade of heading for ground. The comparison needs no
    /// clock and no speed: at one pace, time and distance are the same figure.
    /// </para>
    /// <para>
    /// <b>The heading is what the two poses ask for and not what facing the straight would take.</b> A body
    /// that turns to face a two-metre step, walks it and turns again spends more than that, and it is free not
    /// to: it may cover a short step without committing to a heading. Priced at the least any plan could
    /// spend, this is <b>the figure a curve has to beat to be worth curving</b>, which is the only honest
    /// bar to hold a curve to.
    /// </para>
    /// </remarks>
    public static float PivotedM(
        Vector2 fromM, float fromHeadingRad, Vector2 toM, float toHeadingRad, float tightestM) =>
        (toM - fromM).Length() + (MathF.Abs(WrapRad(toHeadingRad - fromHeadingRad)) * tightestM);

    /// <summary>
    /// The single arc a pair of poses gets when no biarc joins them — two antiparallel tangents a lane
    /// apart, for which the equal-tangent construction has no positive root. The one arc through both
    /// points is the semicircle between them, which is the right answer and not a fallback in any sense
    /// but the arithmetic's.
    /// </summary>
    /// <remarks>
    /// At a lane's own spacing that circle is far tighter than the steering lock affords. Turning a car
    /// round is a manoeuvre with a reverse in it, not a line to be followed; until that entry exists,
    /// what a car meets at a dead end is a line it cannot hold.
    /// </remarks>
    static int One(Vector2 fromM, float headingRad, Vector2 toM, Span<ArcSeg> into)
    {
        into[0] = ArcThrough(fromM, headingRad, toM);
        return 1;
    }

    /// <summary>The one arc that leaves a pose and reaches a point: its curvature is the chord's, and its length is the turn it makes.</summary>
    /// <remarks>
    /// <b>A bend too slight to be one is the straight to the point</b>, leaving a hair off the pose's own
    /// bearing rather than on it. Laid on the bearing instead, it misses the point by the whole of the bend it
    /// was not given — a millimetre and a half over 180 m — and a ring tightened that way
    /// (<see cref="ArcRings.Tightened"/>) has a joint that does not meet, so a corner struck about one side of it
    /// stands nearer the other than the distance it was struck at.
    /// </remarks>
    public static ArcSeg ArcThrough(Vector2 fromM, float headingRad, Vector2 toM)
    {
        var direction = Heading.Unit(headingRad);
        var chord = toM - fromM;
        var chordLengthSq = chord.LengthSquared();
        if (chordLengthSq < 1e-10f) return new ArcSeg(fromM, headingRad, 0f, 0f);

        var curvature = 2f * Cross(direction, chord) / chordLengthSq;
        if (MathF.Abs(curvature) < StraightCurvature)
        {
            return new ArcSeg(fromM, MathF.Atan2(chord.Y, chord.X), MathF.Sqrt(chordLengthSq), 0f);
        }

        var turnRad = 2f * MathF.Atan2(Cross(direction, chord), Vector2.Dot(direction, chord));
        return new ArcSeg(fromM, headingRad, turnRad / curvature, curvature);
    }

    public static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    /// <summary>Into (−π, π].</summary>
    public static float WrapRad(float angleRad)
    {
        angleRad %= MathF.Tau;
        if (angleRad > MathF.PI) angleRad -= MathF.Tau;
        else if (angleRad <= -MathF.PI) angleRad += MathF.Tau;

        return angleRad;
    }

    /// <summary>
    /// How far along one piece the point nearest another stands, clamped to the piece's own ends.
    /// </summary>
    /// <remarks>
    /// <b>This one is still measured against the centre, and that is a decision rather than an oversight</b>
    /// (<see cref="ChordAlongM"/> is the other way of asking, and is what a crossing uses). Written from the
    /// start instead it is a millimetre or two better on a shallow bend far from the origin — and it moves
    /// which lane a body a hair from two of them snaps to, which moves the routes the town drives. Measured
    /// both ways, the merge closed <em>more</em> of its boundary with this form and a rescue that arrived
    /// inside its bound stopped arriving with the other, so the accuracy on offer is not worth what it
    /// costs. It is a nearest and not a cut: nothing downstream of it is cut to the millimetre.
    /// <para>
    /// <b>Except on a bend flatter than <see cref="FlatCurvature"/></b>, whose centre stands tens of kilometres
    /// off: read about it, a place on the arc's own end came back two millimetres short of it on a traced
    /// road's 64 km bend, so a band's square end there read as alongside the band and the merge dropped it as
    /// covered. Such a bend is read from its start instead (<see cref="NearestOnFlat"/>).
    /// </para>
    /// </remarks>
    internal static float NearestOnArc(in ArcSeg arc, Vector2 pointM)
    {
        var along = arc.StartUnit;
        if (MathF.Abs(arc.Curvature) < StraightCurvature)
        {
            return Math.Clamp(Vector2.Dot(pointM - arc.StartM, along), 0f, arc.LengthM);
        }

        if (MathF.Abs(arc.Curvature) < FlatCurvature) return NearestOnFlat(arc, pointM);

        var radius = 1f / arc.Curvature;
        var centreM = arc.StartM + radius * Heading.RightOf(along);
        var fromCentre = arc.StartM - centreM;
        var toPoint = pointM - centreM;
        if (toPoint.LengthSquared() < 1e-10f) return 0f;

        var turnRad = MathF.Atan2(Cross(fromCentre, toPoint), Vector2.Dot(fromCentre, toPoint));
        var alongM = turnRad / arc.Curvature;

        // The far half of the circle is behind the start as easily as past the end; whichever end the
        // point is beyond, the nearest point on the arc itself is that end.
        if (alongM < 0f) alongM = alongM + MathF.Tau / MathF.Abs(arc.Curvature) <= arc.LengthM ? alongM + MathF.Tau / MathF.Abs(arc.Curvature) : 0f;

        return Math.Clamp(alongM, 0f, arc.LengthM);
    }

    /// <summary>
    /// <b>How far along an all but straight arc the point nearest a place stands</b>, read from the arc's start: along
    /// its first heading, then put right by Newton's step against the heading where that lands, clamped to the arc's
    /// own ends. Nothing is measured from the centre, which on such an arc is further off than a float holds a
    /// millimetre.
    /// </summary>
    /// <remarks>
    /// The first reading is short by the bend's own κ²s³⁄6 — a tenth of a metre over 400 m at the bend read this way
    /// that is nearest round — and each step leaves what was left squared times the curvature.
    /// </remarks>
    static float NearestOnFlat(in ArcSeg arc, Vector2 pointM)
    {
        var offM = pointM - arc.StartM;
        var alongM = Math.Clamp(Vector2.Dot(offM, arc.StartUnit), 0f, arc.LengthM);
        for (var step = 0; step < FlatSteps; step++)
        {
            var heading = Heading.Unit(arc.HeadingAtRad(alongM));
            alongM = Math.Clamp(alongM + Vector2.Dot(offM - arc.FromStartM(alongM), heading), 0f, arc.LengthM);
        }

        return alongM;
    }
}
