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
/// The pieces of ground that belong to no road and to no line through a box: the wedge a junction's kerbs
/// turn on, the rectangles a car park and a slab of paving are, and the rings the water is cut from.
/// </summary>
internal sealed partial class GroundShapes
{
    /// <summary>
    /// How many shapes of one kind may reach a point before the broad phase's answer stops being the whole
    /// one. <see cref="AnyReaches{TShape}"/> falls back to every shape of that kind rather than truncate,
    /// so this is a working size and not a limit on what a town may hold.
    /// </summary>
    const int MostShapesNear = 32;

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

    /// <remarks>
    /// <b>A kerb fillet is not among them any more.</b> The wedge between two kerbs is what the boundary
    /// has left over once every movement has taken what it sweeps, and the boundary turns that corner
    /// itself (<see cref="LaneShell.Rounded"/>) — so the shape the plan carries for it is drawn by nobody
    /// and answered by nobody, and the ground there is the one thing an intersection is: the ground its own
    /// movements did not take.
    /// </remarks>
    void LayTheShapes(Paving paving, SimConfig config)
    {
        var plan = paving.Of;

        _slabMinM = plan.PavedAreas.MinM;
        _slabSizeM = plan.PavedAreas.SizeM;

        _water = Rings.Boxed(plan.Water.Outline);
        _shore = Rings.Boxed(plan.Water.Shore);
    }
}
