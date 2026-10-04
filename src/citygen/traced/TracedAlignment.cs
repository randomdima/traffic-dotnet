using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen.Traced;

/// <summary>
/// <b>A traced road's line normalised</b> (GEN-57): the fewest corners and the roundest arcs that keep every point
/// its way was surveyed through within <see cref="CityGenFigures.TracedLineToleranceM"/> — a point that near the
/// straight past it is no corner, two corners turning one way that one arc passes that near are one corner, and
/// every corner is rounded as wide as keeps its stretch of the survey that near.
/// </summary>
/// <remarks>
/// <para>
/// <b>A mapper draws a bend as a polygon</b>, a node every few metres, and rounded at its carriageway each node is a
/// facet. Normalised, a bend is the tangents either side of it meeting at one corner and the arc between them,
/// which is how a road is designed: a run of corners is merged while one arc still fits it, the closest fit first,
/// so a bend is gathered from its middle out and an S-bend stays two.
/// </para>
/// <para>
/// <b>A merged corner stands off the road</b>, where its tangents meet, so its arc keeps the survey only over a
/// range of radii: tighter pulls it out toward the corner, wider cuts it in. Each corner is given that range, and
/// a leg is shared so that each keeps at least the tightest of it (<see cref="Reaches"/>).
/// </para>
/// <para>
/// <b>No corner is rounded tighter than it was asked</b>: the least radius is the caller's, so a corner the survey
/// drew sharper than the tolerance can round at that radius sags as far as the radius takes it.
/// </para>
/// </remarks>
internal static class TracedAlignment
{
    /// <summary>
    /// One corner of the line, or one of its two ends: where it stands, the first and last surveyed point it stands
    /// for — one point, or the run of a bend whose tangents meet here — and the tightest radius that keeps those.
    /// </summary>
    readonly record struct Corner(Vector2 AtM, int First, int Last, float TightestM);

    /// <summary>
    /// The corners a surveyed line is laid round — its two ends first and last, unmoved — and the range of radii
    /// each corner between keeps its stretch of the survey within the tolerance over, one a corner and the first
    /// for the line's second point. A corner nothing keeps within it is the least radius at both.
    /// </summary>
    public static (Vector2[] PointsM, float[] TightestM, float[] WidestM) Of(
        ReadOnlySpan<Vector2> surveyedM, float leastRadiusM, float toleranceM)
    {
        var corners = Kept(surveyedM, leastRadiusM, toleranceM);
        Merge(corners, surveyedM, leastRadiusM, toleranceM);

        var pointsM = new Vector2[corners.Count];
        for (var at = 0; at < corners.Count; at++) pointsM[at] = corners[at].AtM;

        var tightestM = new float[corners.Count - 2];
        var widestM = new float[corners.Count - 2];
        for (var corner = 1; corner < corners.Count - 1; corner++)
        {
            var window = WindowOf(corners, surveyedM, corner - 1, corners[corner].AtM, corner + 1);
            (tightestM[corner - 1], widestM[corner - 1]) = Range(window, leastRadiusM, toleranceM);
        }

        return (pointsM, tightestM, widestM);
    }

    /// <summary>
    /// <b>How far back along its legs each corner of a line is rounded from</b>: as far as its widest radius takes
    /// it, a leg too short for the corners at both its ends going first to what each needs to round at its tightest
    /// — in proportion to that where it is short even of those — and what is left of it in proportion to what each
    /// wants beyond.
    /// </summary>
    /// <remarks>
    /// A sharp corner beside a gentle one takes nearly all of the leg between them rather than half, and two corners
    /// of one bend that both want more than their leg meet on it, so the bend is arcs end to end. Where the tightest
    /// and the widest are one radius everywhere, a short leg is shared in proportion to what each needs of it.
    /// </remarks>
    /// <param name="tightestM">One a corner, the first for the line's second point.</param>
    /// <param name="widestM">The same, none tighter than its <paramref name="tightestM"/>.</param>
    public static float[] Reaches(ReadOnlySpan<Vector2> pointsM, ReadOnlySpan<float> tightestM, ReadOnlySpan<float> widestM)
    {
        // A slot a point, so the line's two ends are slots that neither need nor want any of their legs.
        var needM = new float[pointsM.Length];
        var wantM = new float[pointsM.Length];
        for (var point = 1; point < pointsM.Length - 1; point++)
        {
            var halfTurnTan = Spline.HalfTurnTan(pointsM, point);
            if (!(halfTurnTan > 0f)) continue;

            needM[point] = tightestM[point - 1] * halfTurnTan;
            wantM[point] = MathF.Max(tightestM[point - 1], widestM[point - 1]) * halfTurnTan;
        }

        var reachM = new float[pointsM.Length - 2];
        for (var point = 1; point < pointsM.Length - 1; point++)
        {
            var arrivingM = Vector2.Distance(pointsM[point - 1], pointsM[point]);
            var leavingM = Vector2.Distance(pointsM[point], pointsM[point + 1]);
            var (_, arriving) = Share(arrivingM, needM[point - 1], wantM[point - 1], needM[point], wantM[point]);
            var (leaving, _) = Share(leavingM, needM[point], wantM[point], needM[point + 1], wantM[point + 1]);
            reachM[point - 1] = MathF.Min(arriving, leaving);
        }

        return reachM;
    }

    /// <summary>One leg shared between the corners at its two ends (<see cref="Reaches"/>), neither past what it wants.</summary>
    static (float FirstM, float SecondM) Share(float legM, float firstNeedM, float firstWantM, float secondNeedM, float secondWantM)
    {
        var needM = firstNeedM + secondNeedM;
        if (needM >= legM) return needM > 0f ? (legM * firstNeedM / needM, legM * secondNeedM / needM) : (0f, 0f);

        var (firstMoreM, secondMoreM) = (firstWantM - firstNeedM, secondWantM - secondNeedM);
        var moreM = firstMoreM + secondMoreM;
        var leftM = legM - needM;
        return moreM <= leftM
            ? (firstWantM, secondWantM)
            : (firstNeedM + (leftM * firstMoreM / moreM), secondNeedM + (leftM * secondMoreM / moreM));
    }

    /// <summary>
    /// <b>The points that are corners at all</b> (Douglas–Peucker): the two ends, and every point standing further
    /// than <paramref name="toleranceM"/> off the straight between the corners either side of it.
    /// </summary>
    static List<Corner> Kept(ReadOnlySpan<Vector2> surveyedM, float leastRadiusM, float toleranceM)
    {
        var kept = new bool[surveyedM.Length];
        (kept[0], kept[^1]) = (true, true);
        var runs = new Stack<(int First, int Last)>();
        runs.Push((0, surveyedM.Length - 1));
        while (runs.Count > 0)
        {
            var (first, last) = runs.Pop();
            var (furthest, furthestM) = (-1, toleranceM);
            for (var point = first + 1; point < last; point++)
            {
                var offM = SegmentOffM(surveyedM[point], surveyedM[first], surveyedM[last]);
                if (offM > furthestM) (furthest, furthestM) = (point, offM);
            }

            if (furthest < 0) continue;

            kept[furthest] = true;
            runs.Push((first, furthest));
            runs.Push((furthest, last));
        }

        var corners = new List<Corner>();
        for (var point = 0; point < surveyedM.Length; point++)
        {
            if (kept[point]) corners.Add(new Corner(surveyedM[point], point, point, leastRadiusM));
        }

        return corners;
    }

    /// <summary>
    /// <b>Every two neighbouring corners turning one way that one arc rounds within the tolerance made one</b>,
    /// standing where the leg into the first and the leg out of the second meet — the closest fit first.
    /// </summary>
    static void Merge(List<Corner> corners, ReadOnlySpan<Vector2> surveyedM, float leastRadiusM, float toleranceM)
    {
        // offM[k]: how near one arc round corner k and k + 1 keeps their survey, and infinity at either end of the line.
        var offM = new List<float>(corners.Count);
        for (var corner = 0; corner < corners.Count; corner++)
        {
            offM.Add(corner >= 1 && corner < corners.Count - 2 ? MergedOffM(corners, surveyedM, corner, leastRadiusM) : float.PositiveInfinity);
        }

        while (true)
        {
            var best = -1;
            for (var corner = 1; corner < corners.Count - 2; corner++)
            {
                if (offM[corner] <= toleranceM && (best < 0 || offM[corner] < offM[best])) best = corner;
            }

            if (best < 0) return;

            Meeting(corners[best - 1].AtM, corners[best].AtM, corners[best + 1].AtM, corners[best + 2].AtM, out var meetM);
            var (tightestM, _) = Range(WindowOf(corners, surveyedM, best - 1, meetM, best + 2), leastRadiusM, toleranceM);
            corners[best] = new Corner(meetM, corners[best].First, corners[best + 1].Last, tightestM);
            corners.RemoveAt(best + 1);
            offM.RemoveAt(best + 1);

            // The merged corner is sharper, needs more of its legs and has longer ones, which the pairs either side read.
            for (var corner = Math.Max(1, best - 2); corner <= Math.Min(corners.Count - 3, best + 1); corner++)
            {
                offM[corner] = MergedOffM(corners, surveyedM, corner, leastRadiusM);
            }
        }
    }

    /// <summary>
    /// How near the closest arc round corner <paramref name="first"/> and the next made one keeps their survey — or
    /// infinity where they turn opposite ways, their outer legs meet nowhere between them, or those legs leave the
    /// one corner no room to round at the least radius.
    /// </summary>
    static float MergedOffM(List<Corner> corners, ReadOnlySpan<Vector2> surveyedM, int first, float leastRadiusM)
    {
        var (beforeM, oneM, otherM, afterM) = (corners[first - 1].AtM, corners[first].AtM, corners[first + 1].AtM, corners[first + 2].AtM);
        var oneTurn = Spline.Cross(oneM - beforeM, otherM - oneM);
        var otherTurn = Spline.Cross(otherM - oneM, afterM - otherM);
        if (oneTurn == 0f || otherTurn == 0f || (oneTurn > 0f) != (otherTurn > 0f)) return float.PositiveInfinity;
        if (!Meeting(beforeM, oneM, otherM, afterM, out var meetM)) return float.PositiveInfinity;

        var window = WindowOf(corners, surveyedM, first - 1, meetM, first + 2);
        var roomM = window.RoomM;
        return roomM >= leastRadiusM ? window.OffM(Closest(window, leastRadiusM, roomM)) : float.PositiveInfinity;
    }

    /// <summary>
    /// Where the leg into <paramref name="oneM"/> carried on meets the leg out of <paramref name="otherM"/> carried
    /// back: past the first and short of the second, or not at all.
    /// </summary>
    static bool Meeting(Vector2 beforeM, Vector2 oneM, Vector2 otherM, Vector2 afterM, out Vector2 meetM)
    {
        var arriving = oneM - beforeM;
        var leaving = afterM - otherM;
        var across = Spline.Cross(arriving, leaving);
        meetM = default;
        if (across == 0f) return false;

        var along = Spline.Cross(otherM - oneM, leaving) / across;
        meetM = oneM + (arriving * along);
        return along >= 0f && Vector2.Dot(otherM - meetM, leaving) >= 0f;
    }

    /// <summary>
    /// A corner standing at <paramref name="cornerM"/> between corners <paramref name="before"/> and
    /// <paramref name="after"/>, standing for every surveyed point between them past half way to each, and read
    /// against the whole of the surveyed line from the one to the other.
    /// </summary>
    static Window WindowOf(List<Corner> corners, ReadOnlySpan<Vector2> surveyedM, int before, Vector2 cornerM, int after) =>
        new(
            surveyedM, corners[before].AtM, cornerM, corners[after].AtM,
            (corners[before].Last + corners[before + 1].First) / 2, (corners[after - 1].Last + corners[after].First + 1) / 2,
            corners[before].Last, corners[after].First, NeedM(corners, before), NeedM(corners, after));

    /// <summary>How far back along its legs a corner needs to round at its tightest, and nothing at an end.</summary>
    static float NeedM(List<Corner> corners, int corner) =>
        corner <= 0 || corner >= corners.Count - 1
            ? 0f
            : corners[corner].TightestM * Spline.HalfTurnTan([corners[corner - 1].AtM, corners[corner].AtM, corners[corner + 1].AtM], 1);

    /// <summary>
    /// The tightest and the widest radius a corner is rounded at keeping its stretch of the survey within the
    /// tolerance, none below the least or wider than its legs have room for — or the least at both, where even the
    /// closest fit does not keep it or the legs have no room past the least.
    /// </summary>
    /// <remarks>
    /// How far the arc stands off falls to its closest fit and rises past it, so where both ends of the range keep
    /// the survey everything between does, and where one does the edge is searched for from it.
    /// </remarks>
    static (float TightestM, float WidestM) Range(Window window, float leastRadiusM, float toleranceM)
    {
        var roomM = window.RoomM;
        if (!(roomM > leastRadiusM)) return (leastRadiusM, leastRadiusM);

        var (leastKeeps, roomKeeps) = (window.OffM(leastRadiusM) <= toleranceM, window.OffM(roomM) <= toleranceM);
        if (leastKeeps) return (leastRadiusM, roomKeeps ? roomM : Edge(window, leastRadiusM, roomM, toleranceM));
        if (roomKeeps) return (Edge(window, roomM, leastRadiusM, toleranceM), roomM);

        var closestM = Closest(window, leastRadiusM, roomM);
        if (window.OffM(closestM) > toleranceM) return (leastRadiusM, leastRadiusM);

        return (Edge(window, closestM, leastRadiusM, toleranceM), Edge(window, closestM, roomM, toleranceM));
    }

    /// <summary>
    /// The radius past which a corner's arc leaves its survey further than the tolerance, between one that keeps it
    /// and one that does not — returned on the keeping side, halved on its logarithm.
    /// </summary>
    static float Edge(Window window, float keptM, float leftM, float toleranceM)
    {
        while (MathF.Abs(MathF.Log(leftM / keptM)) > RadiusPrecision)
        {
            var middleM = MathF.Sqrt(keptM * leftM);
            if (window.OffM(middleM) <= toleranceM) keptM = middleM;
            else leftM = middleM;
        }

        return keptM;
    }

    /// <summary>The radius whose arc keeps a corner's stretch of the survey nearest, by golden section on its logarithm.</summary>
    static float Closest(Window window, float leastRadiusM, float roomM)
    {
        var (low, high) = (MathF.Log(leastRadiusM), MathF.Log(roomM));
        var (inner, outer) = (high - (Golden * (high - low)), low + (Golden * (high - low)));
        var (innerOffM, outerOffM) = (window.OffM(MathF.Exp(inner)), window.OffM(MathF.Exp(outer)));
        while (high - low > RadiusPrecision)
        {
            if (innerOffM <= outerOffM)
            {
                (high, outer, outerOffM) = (outer, inner, innerOffM);
                inner = high - (Golden * (high - low));
                innerOffM = window.OffM(MathF.Exp(inner));
            }
            else
            {
                (low, inner, innerOffM) = (inner, outer, outerOffM);
                outer = low + (Golden * (high - low));
                outerOffM = window.OffM(MathF.Exp(outer));
            }
        }

        return MathF.Exp((low + high) * 0.5f);
    }

    /// <summary>(√5 − 1) ⁄ 2, the share of its interval a golden section keeps each step.</summary>
    const float Golden = 0.618034f;

    /// <summary>
    /// How near a radius is searched for, as a share of itself: a thousandth, which on any corner a road has moves its
    /// arc by far less than a millimetre.
    /// </summary>
    const float RadiusPrecision = 1e-3f;

    /// <summary>
    /// <b>One corner and the stretch of survey it stands for</b>: its legs from the corner before and to the one
    /// after, each of those keeping what it needs of them; the surveyed points from <c>lo</c> to <c>hi</c> its arc
    /// is read against; and the surveyed line between the two corners, which the arc is read against in turn.
    /// </summary>
    /// <remarks>
    /// <b>Read both ways</b>: an arc can pass near every surveyed point and still bulge between two far apart — a
    /// merged corner whose short outer leg sends its tangents' meeting a kilometre off swings an arc tens of metres
    /// wide of the line between them.
    /// </remarks>
    readonly ref struct Window(
        ReadOnlySpan<Vector2> surveyedM, Vector2 beforeM, Vector2 cornerM, Vector2 afterM, int lo, int hi, int spanLo, int spanHi,
        float beforeNeedM, float afterNeedM)
    {
        readonly ReadOnlySpan<Vector2> _pointsM = surveyedM[lo..(hi + 1)];
        readonly ReadOnlySpan<Vector2> _lineM = surveyedM[spanLo..(spanHi + 1)];
        readonly Vector2 _cornerM = cornerM;
        readonly Vector2 _beforeM = beforeM;
        readonly Vector2 _afterM = afterM;
        readonly Vector2 _arriving = Vector2.Normalize(cornerM - beforeM);
        readonly Vector2 _leaving = Vector2.Normalize(afterM - cornerM);
        readonly float _halfTurnTan = Spline.HalfTurnTan([beforeM, cornerM, afterM], 1);

        /// <summary>
        /// The widest radius the legs have room for, the corners either side keeping what they need — nought where
        /// they have none, or the corner turns through nothing.
        /// </summary>
        public float RoomM { get; } = Room(
            MathF.Min(Vector2.Distance(beforeM, cornerM) - beforeNeedM, Vector2.Distance(cornerM, afterM) - afterNeedM),
            Spline.HalfTurnTan([beforeM, cornerM, afterM], 1));

        static float Room(float reachM, float halfTurnTan) => reachM > 0f && halfTurnTan > 0f ? reachM / halfTurnTan : 0f;

        /// <summary>
        /// How far the corner's legs rounded at this radius and its stretch of the survey stand apart: the furthest
        /// any of its surveyed points, or the middle of any leg between two of them, stands off the rounded legs, and
        /// any of the arc's ends, quarters and middle off the surveyed line.
        /// </summary>
        public float OffM(float radiusM)
        {
            var reachM = radiusM * _halfTurnTan;
            var enterM = _cornerM - (_arriving * reachM);
            var leaveM = _cornerM + (_leaving * reachM);

            // Curvature to the right is positive, and the right of a heading is (−y, x).
            var inward = Spline.Cross(_arriving, _leaving) > 0f ? new Vector2(-_arriving.Y, _arriving.X) : new Vector2(_arriving.Y, -_arriving.X);
            var centreM = enterM + (inward * radiusM);

            var furthestM = 0f;
            for (var point = 0; point < _pointsM.Length; point++)
            {
                furthestM = MathF.Max(furthestM, RoundedOffM(_pointsM[point], enterM, leaveM, centreM, radiusM));
                if (point > 0) furthestM = MathF.Max(furthestM, RoundedOffM((_pointsM[point - 1] + _pointsM[point]) * 0.5f, enterM, leaveM, centreM, radiusM));
            }

            // Toward the arc's middle and its quarters from either end: never opposite, the arc turning through less
            // than a half turn, so their sum is a direction and these five run along it in order.
            var (enterFrom, leaveFrom) = (Vector2.Normalize(enterM - centreM), Vector2.Normalize(leaveM - centreM));
            var middleFrom = Vector2.Normalize(enterFrom + leaveFrom);
            ReadOnlySpan<Vector2> alongFrom = [enterFrom, Vector2.Normalize(enterFrom + middleFrom), middleFrom, Vector2.Normalize(middleFrom + leaveFrom), leaveFrom];
            foreach (var from in alongFrom) furthestM = MathF.Max(furthestM, LineOffM(centreM + (from * radiusM), _lineM));

            return furthestM;
        }

        /// <summary>How far a place stands off the corner's legs rounded from these two places on them.</summary>
        float RoundedOffM(Vector2 pointM, Vector2 enterM, Vector2 leaveM, Vector2 centreM, float radiusM)
        {
            var fromCentreM = pointM - centreM;
            return Vector2.Dot(fromCentreM, _arriving) >= 0f && Vector2.Dot(fromCentreM, _leaving) <= 0f
                ? MathF.Abs(fromCentreM.Length() - radiusM)
                : MathF.Min(SegmentOffM(pointM, _beforeM, enterM), SegmentOffM(pointM, leaveM, _afterM));
        }
    }

    /// <summary>How far a place stands off the nearest place on a line.</summary>
    static float LineOffM(Vector2 pointM, ReadOnlySpan<Vector2> lineM)
    {
        var offM = lineM.Length == 1 ? Vector2.Distance(pointM, lineM[0]) : float.PositiveInfinity;
        for (var at = 1; at < lineM.Length; at++) offM = MathF.Min(offM, SegmentOffM(pointM, lineM[at - 1], lineM[at]));
        return offM;
    }

    static float SegmentOffM(Vector2 pointM, Vector2 fromM, Vector2 toM)
    {
        var runM = toM - fromM;
        var lengthSquared = runM.LengthSquared();
        var along = lengthSquared > 0f ? Math.Clamp(Vector2.Dot(pointM - fromM, runM) / lengthSquared, 0f, 1f) : 0f;
        return Vector2.Distance(pointM, fromM + (runM * along));
    }
}
