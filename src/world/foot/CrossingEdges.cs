using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Foot;

/// <summary>
/// <b>Which crossing each stretch of the foot graph is</b>, or <see cref="CityPlan.NoRecord"/> where it is
/// pavement — worked out once when the town is laid.
/// </summary>
/// <remarks>
/// <b>It is which zebra a way paints and nothing about the lanes under it.</b> What a zebra covers of each lane
/// is the marks' (TER-5c.3), worked out from the ribbons with every other piece of shared ground, so the two
/// networks are held against each other in the one measure the reservations already read.
/// </remarks>
internal sealed class CrossingEdges
{
    readonly int[] _crossingOfEdge;

    CrossingEdges(int[] crossingOfEdge) => _crossingOfEdge = crossingOfEdge;

    /// <summary>
    /// Which crossing each stretch of the foot graph <em>is</em>, or <see cref="CityPlan.NoRecord"/> where
    /// it is pavement — which is how a walker knows the way it is walking, or about to step onto, is paint.
    /// </summary>
    public ReadOnlySpan<int> CrossingOfEdge => _crossingOfEdge;

    /// <summary>The same for one stretch.</summary>
    public int CrossingOf(int edge) => _crossingOfEdge[edge];

    /// <summary>
    /// Filled by the one thing the two structures share: where the stretch stands.
    /// </summary>
    /// <remarks>
    /// The foot graph does not carry the plan's crossing index: a crossing there is a kind of edge and
    /// nothing else, which is what makes crossing at a crossing structural rather than looked-up.
    /// </remarks>
    public static CrossingEdges Of(Crossings crossings, FootGraph foot)
    {
        var of = new int[foot.EdgeCount];
        Array.Fill(of, CityPlan.NoRecord);

        for (var edge = 0; edge < foot.EdgeCount; edge++)
        {
            if (foot.KindOf(edge) != FootEdgeKind.Crossing) continue;

            var middleM = Spline.SampleAt(foot.ArcsOf(edge), foot.LengthM(edge) * 0.5f).PositionM;
            var best = CityPlan.NoRecord;
            var bestDistanceSq = float.MaxValue;
            for (var crossing = 0; crossing < crossings.Count; crossing++)
            {
                var distanceSq = Vector2.DistanceSquared(middleM, crossings.CentreM[crossing]);
                if (distanceSq >= bestDistanceSq) continue;

                (best, bestDistanceSq) = (crossing, distanceSq);
            }

            of[edge] = best;
        }

        return new CrossingEdges(of);
    }
}
