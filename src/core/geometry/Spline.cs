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
        foreach (var arc in arcs) lengthM += arc.LengthM;

        return lengthM;
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
            var arc = arcs[index];
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

        var arc = arcs[cursor.Piece];
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
    /// A lane offset at or past the radius of the bend it is on would invert the curve, and the plan
    /// cannot produce one — a road's own lanes sit a quarter of its width from its centre.
    /// </remarks>
    public static void OffsetInto(ReadOnlySpan<ArcSeg> arcs, float offsetM, Span<ArcSeg> into)
    {
        for (var index = 0; index < arcs.Length; index++)
        {
            var arc = arcs[index];
            var right = Heading.RightOf(Heading.Unit(arc.HeadingRad));
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
        foreach (var arc in arcs)
        {
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
    /// The distance along the chain whose point is nearest the one given, searched <b>in a window</b>
    /// around where the caller last was.
    /// </summary>
    /// <remarks>
    /// The window is the whole reason this is not a search over the line: a route that doubles back
    /// past itself has two nearest points, and a car half way round a turn-around is nearer to where it
    /// started than to where it is going. What a caller wants is the nearest point to the progress it
    /// had, which is a local question.
    /// </remarks>
    public static float ProjectM(ReadOnlySpan<ArcSeg> arcs, Vector2 pointM, float aroundM, float windowM)
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

        return bestM;
    }

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

    static int Straight(Vector2 fromM, Vector2 toM, Span<ArcSeg> into)
    {
        var run = toM - fromM;
        var lengthM = run.Length();
        if (lengthM < 1e-4f) return 0;

        into[0] = new ArcSeg(fromM, MathF.Atan2(run.Y, run.X), lengthM, 0f);
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

            tangentM = (-b + MathF.Sqrt(discriminant)) / (2f * a);
        }

        if (tangentM <= 0f) return One(fromM, fromHeadingRad, toM, into);

        var jointM = ((fromM + from * tangentM) + (toM - to * tangentM)) * 0.5f;
        into[0] = ArcThrough(fromM, fromHeadingRad, jointM);
        into[1] = ArcThrough(jointM, into[0].HeadingAtRad(into[0].LengthM), toM);
        return 2;
    }

    /// <summary>
    /// The single arc a pair of poses gets when no biarc joins them — exactly the pair a turn-around
    /// is: two antiparallel tangents a lane apart, for which the equal-tangent construction has no
    /// positive root. The one arc through both points is the semicircle between them, which is the
    /// right answer and not a fallback in any sense but the arithmetic's.
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
    public static ArcSeg ArcThrough(Vector2 fromM, float headingRad, Vector2 toM)
    {
        var direction = Heading.Unit(headingRad);
        var chord = toM - fromM;
        var chordLengthSq = chord.LengthSquared();
        if (chordLengthSq < 1e-10f) return new ArcSeg(fromM, headingRad, 0f, 0f);

        var curvature = 2f * Cross(direction, chord) / chordLengthSq;
        if (MathF.Abs(curvature) < StraightCurvature)
        {
            return new ArcSeg(fromM, headingRad, MathF.Sqrt(chordLengthSq), 0f);
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

    static float NearestOnArc(ArcSeg arc, Vector2 pointM)
    {
        var along = Heading.Unit(arc.HeadingRad);
        if (MathF.Abs(arc.Curvature) < StraightCurvature)
        {
            return Math.Clamp(Vector2.Dot(pointM - arc.StartM, along), 0f, arc.LengthM);
        }

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
}
