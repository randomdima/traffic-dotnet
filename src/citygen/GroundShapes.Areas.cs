using System.Numerics;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.CityGen;

/// <summary>
/// The pieces of ground that belong to no road and to no line through a box: the rectangles a slab of
/// paving is, and the rings the water and its shore are cut from.
/// </summary>
/// <remarks>
/// <b>Not indexed, and that is the reading.</b> A town's slabs are a few dozen boxes and its water a
/// handful of rings, each tested against its own bounding box first — a lattice over either would be a
/// rebuild and a lookup to narrow a walk that is already shorter than the narrowing.
/// </remarks>
internal sealed partial class GroundShapes
{
    Vector2[] _slabMinM = [];
    Vector2[] _slabSizeM = [];

    Rings _water;
    Rings _shore;

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
    /// itself (<see cref="LaneShell"/>) — so the shape the plan carries for it is drawn by nobody
    /// and answered by nobody, and the ground there is whichever side of that boundary it stands on.
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
