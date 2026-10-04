using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen.Traced;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.Routing;

/// <summary>
/// <b>A route may move across onto the lane beside</b> (CAR-53), asked of a street of two lanes into a tee whose arm is
/// turned onto from the inner lane alone (<see cref="TracedPlanTests.TwoLanesIntoATee"/>).
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P2)]
public class LaneSwitchRoutingTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// <b>The kerb lane's run joins the run a turn off the lane beside it leads onto</b>, at what that turn costs from the
    /// lane beside and one move across more.
    /// </summary>
    [Fact]
    public void AKerbLaneJoinsTheTurnOffTheLaneBesideItForOneMoveAcrossMore()
    {
        var plan = TracedPlanTests.TwoLanesIntoATee();
        var roads = RoadGraph.Build(plan, Config);
        var network = DrivingNetwork.Build(roads, new bool[roads.LaneCount], plan, Config);

        var kerb = roads.NearestStreetLane(KerbLaneM, out _);
        var inner = roads.LaneBeside(kerb, inward: true);
        var arm = TheLaneNorthFrom(roads, inner);

        Assert.Equal(
            PriceM(network, inner, arm) + DrivingNetwork.SwitchM(roads, Config, kerb, inner),
            PriceM(network, kerb, arm));
    }

    /// <summary>What the network charges for leaving one lane's run for another's, or infinity where it may not.</summary>
    static float PriceM(DrivingNetwork network, int from, int onto)
    {
        var turns = network.Graph.TurnsFrom(network.LinkOfLane(from));
        var at = turns.IndexOf(network.LinkOfLane(onto));
        return at < 0 ? float.PositiveInfinity : network.Graph.TurnPricesFrom(network.LinkOfLane(from))[at];
    }

    /// <summary>The lane a lane joins that runs north.</summary>
    static int TheLaneNorthFrom(RoadGraph roads, int lane)
    {
        foreach (var onward in roads.LanesFrom(lane))
        {
            if (roads.EndOf(onward).PositionM.Y < roads.StartOf(onward).PositionM.Y) return onward;
        }

        throw new InvalidOperationException($"lane {lane} turns onto no lane running north");
    }

    /// <summary>A place on the street's kerb lane, short of the tee.</summary>
    static readonly Vector2 KerbLaneM = new(500f, 502f);
}
