using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Foot;

/// <summary>What joins the pavement to the one thing off it: the crossings cut through a carriageway.</summary>
internal sealed partial class FootGraph
{
    /// <summary>
    /// Every crossing, as the one edge of this network that touches a carriageway — and as a <b>T</b> in
    /// the pavement either side of it: a zebra attaches to the middle of the wrap, where the wrap's own
    /// line meets the paint's axis, and the pavement runs on through it.
    /// </summary>
    static void Crossings(CityPlan plan, Kerbs kerbs, Builder builder, float bandM)
    {
        var crossings = plan.Crosswalks;
        var outM = bandM * 0.5f;
        for (var crossing = 0; crossing < crossings.Count; crossing++)
        {
            var centreM = crossings.CentreM[crossing];
            var axis = Vector2.Normalize(crossings.Axis[crossing]);

            // The axis runs *along* the road the crossing crosses, so the way over is square to it, and
            // how far is as far as the pavement — the same distance off the tarmac the wrap itself keeps.
            var across = new Vector2(-axis.Y, axis.X);
            var mostM = plan.CrossingSpanM(crossing) * 0.5f + bandM;

            if (!Mouth(kerbs, centreM, -across, mostM, outM, out var nearM)) continue;
            if (!Mouth(kerbs, centreM, across, mostM, outM, out var farM)) continue;

            var near = builder.SplitNearest(nearM, outM);
            var far = builder.SplitNearest(farM, outM);
            if (near < 0 || far < 0 || near == far) continue;

            var fromM = builder.PositionOf(near);
            var toM = builder.PositionOf(far);
            var lineM = toM - fromM;
            builder.AddArc(
                near, far, new ArcSeg(fromM, MathF.Atan2(lineM.Y, lineM.X), lineM.Length(), 0f),
                crossings.DepthM[crossing], FootEdgeKind.Crossing);
        }
    }
}
