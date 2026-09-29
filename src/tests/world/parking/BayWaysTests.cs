using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.World.Parking;

/// <summary>
/// The ways at a bay, read off the car parks of the suite's own city (GEN-4f, GEN-4h, GEN-4j): what each bay
/// lays, where each way meets its lane and its pose, and which of them are driven in reverse.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P5)]
[Collection(nameof(TownGeometryCollection))]
public class BayWaysTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>How near two readings of one point on the town's own lines have to come.</summary>
    const float ExactM = 1e-3f;

    static (RoadGraph Roads, BayWays Bays) Laid()
    {
        var plan = Towns.Built;
        var roads = RoadGraph.Build(plan, Config);
        return (roads, BayWays.Build(plan, roads, Config));
    }

    /// <summary>
    /// <b>Every bay can be parked in and left</b> (GEN-4): it lays a way in and a way out for at least one of
    /// the two standings (GEN-4j).
    /// </summary>
    [Fact]
    public void EveryBayCanBeParkedInAndLeft()
    {
        var (_, bays) = Laid();

        Assert.True(bays.BayCount > 0, "the built city cut no car park");
        for (var bay = 0; bay < bays.BayCount; bay++)
        {
            Assert.True(
                bays.CanStand(bay, noseIn: true) || bays.CanStand(bay, noseIn: false),
                $"bay {bay} lays {bays.WayCountOf(bay)} ways and no standing has both a way in and a way out");
        }
    }

    /// <summary>
    /// <b>No way in leaves a car standing with no way out</b> (GEN-4j): whichever standing a bay is driven
    /// into, that standing is one the bay also lays a way out of.
    /// </summary>
    [Fact]
    public void EveryWayInHasAWayOutOfTheSameStanding()
    {
        var (_, bays) = Laid();

        for (var way = bays.FirstWay; way < bays.TotalWayCount; way++)
        {
            if (!bays.IsEntry(way)) continue;

            Assert.True(
                bays.CanStand(bays.BayOfWay(way), bays.IsNoseIn(way)),
                $"way {way} parks a car {(bays.IsNoseIn(way) ? "nose in" : "backed in")} in bay {bays.BayOfWay(way)}, " +
                "which lays no way out that way round");
        }
    }

    /// <summary>
    /// <b>A way meets its lane where it says it does</b>: a way in sets off from the metre of the lane it
    /// leaves, and a way out finishes on the metre of the lane it lands on.
    /// </summary>
    [Fact]
    public void AWayMeetsItsLaneAtItsOwnMetre()
    {
        var (roads, bays) = Laid();

        for (var way = bays.FirstWay; way < bays.TotalWayCount; way++)
        {
            var arcs = bays.ArcsOf(way);
            var laneM = Spline.SampleAt(roads.ArcsOf(bays.LaneOf(way)), bays.AtLaneM(way)).PositionM;
            var wayM = bays.IsEntry(way)
                ? Spline.SampleAt(arcs, 0f).PositionM
                : Spline.SampleAt(arcs, bays.DrivenLengthM(way)).PositionM;

            Assert.True(
                (wayM - laneM).Length() <= ExactM,
                $"way {way} of bay {bays.BayOfWay(way)} meets its lane {(wayM - laneM).Length():0.000} m off the metre it names");
        }
    }

    /// <summary>
    /// <b>And its bay at the pose</b> (GEN-4i): a way in is driven as far as the axle of a car standing square
    /// in the space, and a way out sets off from there.
    /// </summary>
    [Fact]
    public void AWayIsDrivenToThePose()
    {
        var (_, bays) = Laid();

        for (var way = bays.FirstWay; way < bays.TotalWayCount; way++)
        {
            var arcs = bays.ArcsOf(way);
            var poseM = bays.IsEntry(way)
                ? Spline.SampleAt(arcs, bays.DrivenLengthM(way)).PositionM
                : Spline.SampleAt(arcs, 0f).PositionM;

            Assert.True(
                (poseM - bays.AtTheBayM(way)).Length() <= ExactM,
                $"way {way} of bay {bays.BayOfWay(way)} is driven to {(poseM - bays.AtTheBayM(way)).Length():0.000} m off its pose");
        }
    }
}
