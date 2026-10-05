using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen.Traced;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// <b>A car is on the channels of the ways under it</b> (PHY-1a), asked of one street carried over another
/// (<see cref="TracedPlanTests.OverAStreet"/>) as the cars it stands drive off: each one stood on the bridge drives off
/// it, and each one stood on the street under it drives along under the deck.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P3)]
public class ChannelsInATownTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>Long enough for a car stood on the bridge to have driven off either end of it.</summary>
    const float WatchedS = 15f;

    static readonly byte Ground = CityPlan.RoadArrays.GroundChannel;

    static readonly byte Over = CityPlan.RoadArrays.ChannelOf(CityPlan.RoadArrays.Over);

    /// <summary>
    /// <b>A car driving off a bridge is on both channels at its bridgehead and on the ground's past it</b>: the bridge's
    /// alone, then the bridge's and the ground's, then the ground's alone.
    /// </summary>
    [Fact]
    public void ACarDrivingOffABridgeIsOnBothChannelsAtItsBridgeheadAndTheGroundsPastIt()
    {
        var passed = ChannelsEachCarPassedThrough(out var stoodOver);

        foreach (var car in stoodOver) Assert.Equal([Over, (byte)(Over | Ground), Ground], passed[car]);
    }

    /// <summary><b>A car driving under a bridge is on the ground's channel alone</b>, the deck over it included.</summary>
    [Fact]
    public void ACarDrivingUnderABridgeIsOnTheGroundsChannelAlone()
    {
        var passed = ChannelsEachCarPassedThrough(out var stoodOver);

        for (var car = 0; car < passed.Length; car++)
        {
            if (!stoodOver.Contains(car)) Assert.Equal([Ground], passed[car]);
        }
    }

    /// <summary>Every channel mask each car was on, in the order it went onto them, and which cars were stood on the bridge.</summary>
    static List<byte>[] ChannelsEachCarPassedThrough(out HashSet<int> stoodOver)
    {
        using var world = new TownWorld(TracedPlanTests.OverAStreet(), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        var cars = world.Cars;
        var passed = new List<byte>[cars.Count];
        stoodOver = [];
        for (var car = 0; car < cars.Count; car++)
        {
            passed[car] = [cars.Channels[car]];
            if (cars.LevelOf(car) == CityPlan.RoadArrays.Over) stoodOver.Add(car);
        }

        Assert.NotEmpty(stoodOver);
        Assert.NotEqual(cars.Count, stoodOver.Count);

        for (var tick = 0; tick < WatchedS / Config.TickSeconds; tick++)
        {
            loop.Advance(1);
            for (var car = 0; car < cars.Count; car++)
            {
                if (cars.Channels[car] != passed[car][^1]) passed[car].Add(cars.Channels[car]);
            }
        }

        return passed;
    }
}
