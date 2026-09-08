using System.Collections.Concurrent;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Person;

/// <summary>
/// PER-24 asked of a running town rather than of the geometry: that walkers do step round the bodies in
/// their way, and that the side they step is the rule and not the leftover.
/// </summary>
/// <remarks>
/// <b>It is the claim the shipped figures answer and the arithmetic cannot.</b> Whether the right is
/// available is a fact about pavement widths, kerbs and the room a body needs — a clearance a hair wider,
/// or a graze a hair meaner, and every walker in the town turns left at every obstruction while every unit
/// test still passes.
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P2)]
public class StepRoundTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>A minute of a busy town, which is long enough for its walkers to meet each other.</summary>
    const int Ticks = 3_600;

    [Fact]
    public void WalkersStepRoundWhatIsInTheirWay()
    {
        var world = Run(Towns.City);

        Assert.True(world.StepsRound > 0, "nobody in a minute of a busy town stepped round anything");
    }

    /// <summary>
    /// <b>And the right is the rule.</b> A step to the left is what the ground leaves when the right is
    /// refused (PER-7.2), so a town where most of them go left is one where the rule has become the
    /// exception — the symptom of a graze too mean to get a body past the kerb line.
    /// </summary>
    [Fact]
    public void MostOfThemGoToTheRight()
    {
        var world = Run(Towns.City);

        Assert.True(
            world.StepsRoundToTheLeft * 2 < world.StepsRound,
            $"{world.StepsRoundToTheLeft} of {world.StepsRound} steps round a body went to the left");
    }

    /// <summary>
    /// The minute, run once however many claims are asked of it — both of these are read off the same
    /// walkers meeting the same bodies, and standing a second town to ask the second question was the whole
    /// of what the class cost.
    /// </summary>
    static TownWorld Run(string map) => Runs.GetOrAdd(map, at =>
    {
        var world = new TownWorld(Towns.Of(at), Config);
        new SimLoop<TownWorld>(world, Config).Advance(Ticks);

        return world;
    });

    static readonly ConcurrentDictionary<string, TownWorld> Runs = new();
}
