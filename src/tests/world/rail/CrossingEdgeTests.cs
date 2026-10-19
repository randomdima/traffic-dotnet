using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Rail;
using Xunit;

namespace TrafficSimulation.Tests.World.Rail;

/// <summary>
/// <b>What stands across a level crossing's road</b> (RAIL-7, RAIL-10): the suite's own city with a double track across it,
/// its roads crossing at whatever slant the generator allows.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P6)]
[Collection(TownGeometryCollection.Name)]
public class CrossingEdgeTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>How far a place read off a road's line by sampling it may stand off where it was laid.</summary>
    const float ToleranceM = 0.05f;

    /// <summary>How finely the arm's line is walked across the road, and along the road from it toward the rails, and how far at most.</summary>
    const float AcrossStepM = 0.1f;

    const float StepM = 0.01f;

    const float FurthestM = 30f;

    /// <summary>
    /// A closed crossing's arm lies square across every lane coming in — from its housing on the walk to the road's
    /// middle, or to its far kerb on a road driven one way.
    /// </summary>
    [Fact]
    public void AClosedArmLiesSquareAcrossTheLanesComingIn()
    {
        var (plan, crossings, gear) = Laid();
        for (var each = 0; each < gear.Count; each++)
        {
            var road = crossings.OfRoad[gear.Crossing[each]];
            var (pivotM, tipM) = (gear.PivotM[each], gear.PivotM[each] + (gear.DownAlong[each] * gear.ArmM[each]));
            var (pivotAlongM, pivotAcrossM) = OnTheRoad(plan, road, pivotM);
            var (tipAlongM, tipAcrossM) = OnTheRoad(plan, road, tipM);
            Assert.True(MathF.Abs(pivotAlongM - tipAlongM) < ToleranceM, $"gear {each}: the arm runs {tipAlongM - pivotAlongM:F2} m along the road end to end");

            var halfWidthM = plan.Roads.WidthM[road] * 0.5f;
            Assert.True(MathF.Abs(pivotAcrossM) > halfWidthM, $"gear {each}: the arm leaves its housing {pivotAcrossM:F2} m off the road's middle, on the road");
            var reachesM = plan.Roads.Flow[road] == RoadFlow.BothWays ? 0f : -MathF.Sign(pivotAcrossM) * halfWidthM;
            Assert.True(MathF.Abs(tipAcrossM - reachesM) < ToleranceM, $"gear {each}: the closed arm's tip is {tipAcrossM:F2} m off the road's middle, not {reachesM:F2}");
        }
    }

    /// <summary>
    /// RAIL-7: the line a barrier stands on, from its arm's tip to the walk's far edge, comes as near a rail as the
    /// clearance, measured along the road toward the track, and no nearer.
    /// </summary>
    [Fact]
    public void ABarriersLineComesAsNearARailAsTheClearanceAlongTheRoad()
    {
        var (plan, crossings, gear) = Laid();
        var railOffM = (Config.Rail.GaugeM * 0.5f) + Config.Rail.RailHeadWidthM;
        for (var each = 0; each < gear.Count; each++)
        {
            var crossing = gear.Crossing[each];
            var road = crossings.OfRoad[crossing];
            var tipM = gear.PivotM[each] + (gear.DownAlong[each] * gear.ArmM[each]);
            var (alongM, tipAcrossM) = OnTheRoad(plan, road, tipM);
            var at = Spline.SampleAt(plan.Roads.SegmentsOf(road), alongM);
            var travel = alongM < crossings.FromRoadM[crossing] ? at.Direction : -at.Direction;
            var walkAcrossM = (plan.Roads.WidthM[road] * 0.5f) + plan.PavementWidthM;
            var spanM = walkAcrossM - (tipAcrossM * MathF.Sign(Vector2.Dot(-gear.DownAlong[each], new Vector2(-at.Direction.Y, at.Direction.X))));

            var nearestM = float.MaxValue;
            var steps = (int)MathF.Ceiling(spanM / AcrossStepM);
            for (var step = 0; step <= steps; step++)
            {
                var fromM = tipM - (gear.DownAlong[each] * (spanM * step / steps));
                var runM = 0f;
                while (runM < FurthestM && !OnARail(plan, crossings, crossing, fromM + (travel * runM), railOffM)) runM += StepM;

                nearestM = MathF.Min(nearestM, runM);
            }

            Assert.True(
                MathF.Abs(nearestM - Config.Rail.BarrierRailClearanceM) < ToleranceM,
                $"gear {each}: the barrier's line comes {nearestM:F2} m along the road from a rail, not {Config.Rail.BarrierRailClearanceM}");
        }
    }

    static (CityPlan Plan, LevelCrossings Crossings, CrossingGear Gear) Laid()
    {
        var plan = Towns.LayFresh(Towns.Brief(Towns.CitySeed, buildings: 120, railTracks: 2));
        var crossings = LevelCrossings.Of(plan, Config);
        var gear = CrossingGear.Of(plan, crossings, [], Config);
        Assert.NotEqual(0, gear.Count);
        return (plan, crossings, gear);
    }

    /// <summary>Whether a place stands within a rail's outer face of any track a crossing meets.</summary>
    static bool OnARail(CityPlan plan, LevelCrossings crossings, int crossing, Vector2 atM, float railOffM)
    {
        for (var meeting = crossings.MeetingOffsets[crossing]; meeting < crossings.MeetingOffsets[crossing + 1]; meeting++)
        {
            var line = plan.Rails.TrackOf(crossings.Track[meeting]);
            for (var point = 0; point + 1 < line.Length; point++)
            {
                var runM = line[point + 1] - line[point];
                var share = Math.Clamp(Vector2.Dot(atM - line[point], runM) / runM.LengthSquared(), 0f, 1f);
                if (Vector2.Distance(atM, line[point] + (runM * share)) <= railOffM) return true;
            }
        }

        return false;
    }

    /// <summary>Where along a road a place stands, and how far off its line, read square to it where it is nearest.</summary>
    static (float AlongM, float AcrossM) OnTheRoad(CityPlan plan, int road, Vector2 atM)
    {
        var arcs = plan.Roads.SegmentsOf(road);
        var (bestM, alongM, acrossM) = (float.MaxValue, 0f, 0f);
        var lengthM = Spline.TotalLengthM(arcs);
        for (var sampleM = 0f; sampleM <= lengthM; sampleM += 0.02f)
        {
            var at = Spline.SampleAt(arcs, sampleM);
            var distanceM = Vector2.Distance(at.PositionM, atM);
            if (distanceM >= bestM) continue;

            (bestM, alongM) = (distanceM, sampleM);
            acrossM = Vector2.Dot(atM - at.PositionM, new Vector2(-at.Direction.Y, at.Direction.X));
        }

        return (alongM, acrossM);
    }
}
