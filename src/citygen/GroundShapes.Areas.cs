using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// One kind of shape, asked whether the one at an index covers a point. <b>A struct and a constraint</b>
/// so the call is the JIT's to inline: the alternative shapes are a delegate a hot path pays for, or the
/// same broad-phase walk written out once for every kind of piece the ground is cut from.
/// </summary>
internal interface IGroundShape
{
    bool Covers(int shape, Vector2 pointM);
}

/// <summary>
/// The pieces of ground that belong to no road and to no line through a box: the head a road stops at, the
/// wedge a junction's kerbs turn on, the rectangles a car park and a slab of paving are, and the rings the
/// water is cut from.
/// </summary>
internal sealed partial class GroundShapes
{
    /// <summary>
    /// How many shapes of one kind may reach a point before the broad phase's answer stops being the whole
    /// one. <see cref="AnyReaches{TShape}"/> falls back to every shape of that kind rather than truncate,
    /// so this is a working size and not a limit on what a town may hold.
    /// </summary>
    const int MostShapesNear = 32;

    BucketGrid _headIndex = null!;
    BucketGrid _kerbIndex = null!;
    BucketGrid _walkIndex = null!;
    BucketGrid _lotIndex = null!;

    TurningHeads _heads;

    Fillets _kerbs;
    Fillets _walks;

    Vector2[] _lotCentreM = [];
    Vector2[] _lotAxis = [];
    Vector2[] _lotHalfM = [];
    float _lotCornerM;

    Vector2[] _slabMinM = [];
    Vector2[] _slabSizeM = [];

    Rings _water;
    Rings _shore;

    /// <summary>
    /// Whether any shape of one kind covers the point. The broad phase names the ones that could, and a
    /// count larger than there was room for falls back to the whole set — <b>the superset is never quietly
    /// turned into a subset</b>, which is the bargain <c>BucketGrid.Query</c> asks its callers to keep.
    /// </summary>
    static bool AnyReaches<TShape>(BucketGrid index, int count, Vector2 pointM, in TShape shapes)
        where TShape : struct, IGroundShape
    {
        Span<int> near = stackalloc int[MostShapesNear];
        var found = index.Query(pointM, 0f, near);
        if (found > near.Length)
        {
            for (var shape = 0; shape < count; shape++)
            {
                if (shapes.Covers(shape, pointM)) return true;
            }

            return false;
        }

        for (var at = 0; at < found; at++)
        {
            if (shapes.Covers(near[at], pointM)) return true;
        }

        return false;
    }

    /// <summary>
    /// The wedge between two kerbs, paved back to the arc tangent to both (TER-5): the triangle they make
    /// with the chord between their tangent points, less what the arc cuts off it. The same piece
    /// <c>GroundMesh.Fillet</c> draws, and the same one at a car park's re-entrant corners (TER-3c.4).
    /// </summary>
    readonly struct Fillets(
        Vector2[] cornerM, Vector2[] tangentAM, Vector2[] tangentBM, Vector2[] arcCentreM, float[] radiusM)
        : IGroundShape
    {
        public int Count => cornerM.Length;

        public bool Covers(int shape, Vector2 pointM) =>
            (pointM - arcCentreM[shape]).LengthSquared() >= radiusM[shape] * radiusM[shape]
            && InTriangle(pointM, cornerM[shape], tangentAM[shape], tangentBM[shape]);

        /// <summary>A fillet's own bounding circle: the three points it is cut from, about their mean.</summary>
        public BucketGrid Index(Vector2 worldSizeM, float bucketM)
        {
            var centreM = new Vector2[Count];
            var radiusM = new float[Count];
            for (var corner = 0; corner < Count; corner++)
            {
                centreM[corner] = (cornerM[corner] + tangentAM[corner] + tangentBM[corner]) / 3f;
                radiusM[corner] = MathF.Max(
                    (cornerM[corner] - centreM[corner]).Length(),
                    MathF.Max(
                        (tangentAM[corner] - centreM[corner]).Length(),
                        (tangentBM[corner] - centreM[corner]).Length()));
            }

            return BucketGrid.Build(worldSizeM, bucketM, centreM, radiusM);
        }

        /// <summary>
        /// Whether any of them stands within reach of a point, <b>taken as the circle round the wedge</b>
        /// rather than as the wedge. It is deliberately generous and it is only ever asked by something
        /// keeping <em>well</em> clear of the paving, so the difference is a prop standing clear of a corner
        /// instead of clear of the arc inside it.
        /// </summary>
        public bool AnyWithin(BucketGrid index, Vector2 pointM, float reachM)
        {
            Span<int> near = stackalloc int[MostShapesNear];
            var found = index.Query(pointM, reachM, near);
            var count = found > near.Length ? Count : found;
            for (var at = 0; at < count; at++)
            {
                var corner = found > near.Length ? at : near[at];
                if (NearTheWedge(corner, pointM, reachM)) return true;
            }

            return false;
        }

        bool NearTheWedge(int corner, Vector2 pointM, float reachM)
        {
            var middleM = (cornerM[corner] + tangentAM[corner] + tangentBM[corner]) / 3f;
            var roundM = MathF.Max(
                (cornerM[corner] - middleM).Length(),
                MathF.Max((tangentAM[corner] - middleM).Length(), (tangentBM[corner] - middleM).Length()));
            var withinM = roundM + reachM;
            return (pointM - middleM).LengthSquared() <= withinM * withinM;
        }
    }

    /// <summary>The head a road stops at, or the pavement that runs round the outside of it (TER-5a).</summary>
    readonly struct Discs(Vector2[] centreM, float[] radiusM, float outM) : IGroundShape
    {
        public bool Covers(int shape, Vector2 pointM)
        {
            var reachM = radiusM[shape] + outM;
            return (pointM - centreM[shape]).LengthSquared() <= reachM * reachM;
        }
    }

    /// <summary>A car park's own tarmac, or the pavement wrap that turns the corners of it.</summary>
    readonly struct Lots(Vector2[] centreM, Vector2[] axis, Vector2[] halfM, float outM, float cornerM)
        : IGroundShape
    {
        public bool Covers(int shape, Vector2 pointM) =>
            outM > 0f
                ? InRoundedRect(pointM, centreM[shape], axis[shape], halfM[shape] + new Vector2(outM), cornerM)
                : InRect(pointM, centreM[shape], axis[shape], halfM[shape]);
    }

    /// <summary>A set of closed rings and the box each of them fits in.</summary>
    readonly record struct Rings(CityPlan.RingArrays Of, Vector2[] LeastM, Vector2[] MostM)
    {
        /// <summary>
        /// Whether a point stands inside any of them, by how many times a ray from it crosses the outline.
        /// The box is tested first, which is what keeps the water off the cost of a query on dry land.
        /// </summary>
        public bool Covers(Vector2 pointM)
        {
            for (var ring = 0; ring < Of.Count; ring++)
            {
                if (pointM.X < LeastM[ring].X || pointM.X > MostM[ring].X) continue;
                if (pointM.Y < LeastM[ring].Y || pointM.Y > MostM[ring].Y) continue;
                if (Inside(Of.RingOf(ring), pointM)) return true;
            }

            return false;
        }

        public static Rings Boxed(CityPlan.RingArrays rings)
        {
            var leastM = new Vector2[rings.Count];
            var mostM = new Vector2[rings.Count];
            for (var ring = 0; ring < rings.Count; ring++)
            {
                var least = new Vector2(float.MaxValue);
                var most = new Vector2(float.MinValue);
                foreach (var pointM in rings.RingOf(ring))
                {
                    least = Vector2.Min(least, pointM);
                    most = Vector2.Max(most, pointM);
                }

                leastM[ring] = least;
                mostM[ring] = most;
            }

            return new Rings(rings, leastM, mostM);
        }

        /// <summary>
        /// Whether a point stands inside any of them or within reach of one. The edge distance is measured
        /// segment by segment, which a ring of sixty-odd points makes cheap — and the box, grown by the
        /// reach, is what keeps a query on dry land off that cost.
        /// </summary>
        public bool Within(Vector2 pointM, float reachM)
        {
            for (var ring = 0; ring < Of.Count; ring++)
            {
                if (pointM.X < LeastM[ring].X - reachM || pointM.X > MostM[ring].X + reachM) continue;
                if (pointM.Y < LeastM[ring].Y - reachM || pointM.Y > MostM[ring].Y + reachM) continue;

                var edgeM = Of.RingOf(ring);
                if (Inside(edgeM, pointM)) return true;

                for (int here = 0, there = edgeM.Length - 1; here < edgeM.Length; there = here++)
                {
                    if (OffTheEdgeM(edgeM[there], edgeM[here], pointM) <= reachM) return true;
                }
            }

            return false;
        }

        static float OffTheEdgeM(Vector2 fromM, Vector2 toM, Vector2 pointM)
        {
            var runM = toM - fromM;
            var lengthSquared = runM.LengthSquared();
            var alongM = lengthSquared > 0f
                ? Math.Clamp(Vector2.Dot(pointM - fromM, runM) / lengthSquared, 0f, 1f)
                : 0f;
            return (pointM - (fromM + (runM * alongM))).Length();
        }

        static bool Inside(ReadOnlySpan<Vector2> ringM, Vector2 pointM)
        {
            var inside = false;
            for (int here = 0, there = ringM.Length - 1; here < ringM.Length; there = here++)
            {
                var a = ringM[here];
                var b = ringM[there];
                if (a.Y > pointM.Y == b.Y > pointM.Y) continue;
                if (pointM.X < ((b.X - a.X) * (pointM.Y - a.Y) / (b.Y - a.Y)) + a.X) inside = !inside;
            }

            return inside;
        }
    }

    bool SlabReaches(Vector2 pointM) => SlabWithin(pointM, 0f);

    bool SlabWithin(Vector2 pointM, float reachM)
    {
        for (var slab = 0; slab < _slabMinM.Length; slab++)
        {
            var offsetM = pointM - _slabMinM[slab];
            if (offsetM.X < -reachM || offsetM.Y < -reachM) continue;
            if (offsetM.X > _slabSizeM[slab].X + reachM || offsetM.Y > _slabSizeM[slab].Y + reachM) continue;

            return true;
        }

        return false;
    }

    /// <summary>Whether a point stands inside a triangle, by the side of each edge it falls on.</summary>
    static bool InTriangle(Vector2 pointM, Vector2 aM, Vector2 bM, Vector2 cM)
    {
        var alongAB = Side(pointM, aM, bM);
        var alongBC = Side(pointM, bM, cM);
        var alongCA = Side(pointM, cM, aM);
        return (alongAB >= 0f && alongBC >= 0f && alongCA >= 0f)
               || (alongAB <= 0f && alongBC <= 0f && alongCA <= 0f);
    }

    static float Side(Vector2 pointM, Vector2 fromM, Vector2 toM) =>
        ((toM.X - fromM.X) * (pointM.Y - fromM.Y)) - ((toM.Y - fromM.Y) * (pointM.X - fromM.X));

    static bool InRect(Vector2 pointM, Vector2 centreM, Vector2 axis, Vector2 halfM)
    {
        Offsets(pointM, centreM, axis, out var alongM, out var acrossM);
        return alongM <= halfM.X && acrossM <= halfM.Y;
    }

    /// <summary>
    /// An oriented rectangle whose four corners are turned on an arc of <paramref name="radiusM"/> — the
    /// piece <c>GroundMesh.RoundedRect</c> draws a car park's pavement wrap as, so the ground the wrap
    /// covers and the ground it is drawn over are one shape (TER-7).
    /// </summary>
    static bool InRoundedRect(Vector2 pointM, Vector2 centreM, Vector2 axis, Vector2 halfM, float radiusM)
    {
        Offsets(pointM, centreM, axis, out var alongM, out var acrossM);
        if (alongM > halfM.X || acrossM > halfM.Y) return false;

        var turnM = MathF.Min(radiusM, MathF.Min(halfM.X, halfM.Y));
        var pastM = new Vector2(alongM - (halfM.X - turnM), acrossM - (halfM.Y - turnM));
        return pastM.X <= 0f || pastM.Y <= 0f || pastM.LengthSquared() <= turnM * turnM;
    }

    static void Offsets(Vector2 pointM, Vector2 centreM, Vector2 axis, out float alongM, out float acrossM)
    {
        var offsetM = pointM - centreM;
        alongM = MathF.Abs(Vector2.Dot(offsetM, axis));
        acrossM = MathF.Abs(Vector2.Dot(offsetM, Heading.RightOf(axis)));
    }

    /// <summary>
    /// The shapes that belong to no road, laid over the broad phases that answer which of them reach a
    /// point. <b>Every one is read off the plan the town is drawn from</b>, and none is re-derived here.
    /// </summary>
    void LayTheShapes(Paving paving, SimConfig config)
    {
        var plan = paving.Of;
        var walkM = paving.WalkM;
        var bucketM = config.Terrain.GroundBucketM;

        _heads = paving.Heads;
        _headIndex = BucketGrid.Build(
            plan.WorldSizeM, bucketM, _heads.CentreM, Grown(_heads.RadiusM, walkM));

        _kerbs = new Fillets(
            plan.JunctionCorners.CornerM, plan.JunctionCorners.TangentAM, plan.JunctionCorners.TangentBM,
            plan.JunctionCorners.ArcCentreM, plan.JunctionCorners.RadiusM);
        _kerbIndex = _kerbs.Index(plan.WorldSizeM, bucketM);

        var corners = paving.Corners;
        var cornerM = new Vector2[corners.Count];
        var tangentAM = new Vector2[corners.Count];
        var tangentBM = new Vector2[corners.Count];
        var arcCentreM = new Vector2[corners.Count];
        var radiusM = new float[corners.Count];
        for (var corner = 0; corner < corners.Count; corner++)
        {
            cornerM[corner] = corners[corner].CornerM;
            tangentAM[corner] = corners[corner].TangentAM;
            tangentBM[corner] = corners[corner].TangentBM;
            arcCentreM[corner] = corners[corner].ArcCentreM;
            radiusM[corner] = corners[corner].RadiusM;
        }

        _walks = new Fillets(cornerM, tangentAM, tangentBM, arcCentreM, radiusM);
        _walkIndex = _walks.Index(plan.WorldSizeM, bucketM);

        _lotCentreM = plan.ParkingLots.CentreM;
        _lotAxis = plan.ParkingLots.Axis;
        _lotHalfM = plan.ParkingLots.HalfExtentM;
        _lotCornerM = paving.WrapCornerM;
        var lotReachM = new float[_lotCentreM.Length];
        for (var lot = 0; lot < lotReachM.Length; lot++)
        {
            lotReachM[lot] = (_lotHalfM[lot] + new Vector2(walkM)).Length();
        }

        _lotIndex = BucketGrid.Build(plan.WorldSizeM, bucketM, _lotCentreM, lotReachM);

        _slabMinM = plan.PavedAreas.MinM;
        _slabSizeM = plan.PavedAreas.SizeM;

        _water = Rings.Boxed(plan.Water.Outline);
        _shore = Rings.Boxed(plan.Water.Shore);
    }

    static float[] Grown(float[] radiusM, float byM)
    {
        var grown = new float[radiusM.Length];
        for (var index = 0; index < grown.Length; index++) grown[index] = radiusM[index] + byM;

        return grown;
    }
}
