using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// What the traffic writes on the ground, in a running town rather than in arithmetic: that a town
/// standing still writes nothing, and that one being driven writes something.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class GroundMarkTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// <b>A parked car with its handbrake on does not scrub the road.</b> Every car in the town starts
    /// standing in a bay, so a town nobody has driven yet is a town with nothing written on it — and a
    /// mark model that gets this wrong paints the whole roster's outline into the car park.
    /// </summary>
    [Fact]
    public void ATownNobodyHasDrivenYetHasNothingWrittenOnIt()
    {
        using var world = new TownWorld(Towns.Of(Towns.Fixture), Config);
        new SimLoop<TownWorld>(world, Config).Advance(60);

        Assert.Equal(0, world.Marks.Count);
    }

    /// <summary>
    /// A minute of a city is a minute of hard stops, and every one of them drags rubber over the road.
    /// The count is a floor and not a figure: what is being held is that the marks reach the ground at
    /// all, which nothing else in the suite would notice going missing.
    /// </summary>
    [Fact]
    public void ADrivenTownWritesOnTheGround()
    {
        using var world = new TownWorld(Towns.Of("Odesa"), Config);
        var loop = new SimLoop<TownWorld>(world, Config);

        // The minute is how long a city may take to write its first mark, not how long this watches for:
        // the claim is that something reaches the ground, so the tick it does is the end of the question.
        for (var tick = 0; tick < 3_600 && world.Marks.Count == 0; tick++) loop.Advance(1);

        Assert.True(world.Marks.Count > 0, "a minute of a city left the road as it found it");
    }
}
