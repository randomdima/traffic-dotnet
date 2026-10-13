using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// <b>The managed world stepped beside the actors is the managed world stepped at once</b> (<see cref="ManagedWorld"/>):
/// two towns of one plan, one stepping its bodies on a thread of their own, ticked side by side.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P2)]
public class ManagedWorldTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>Long enough for the town's people to reach their cars and its cars to drive, park and meet each other.</summary>
    const int Ticks = 600;

    /// <summary>Every car and walker stands, moves and faces where the world stepped at once has it, to the bit, every tick.</summary>
    [Fact]
    public void ATownSteppedBesideTheActorsIsTheTownSteppedAtOnce()
    {
        using var atOnce = new TownWorld(Towns.Built, Config);
        using var aside = new TownWorld(Towns.Built, Config);
        atOnce.HandTheWorldTo(laying => new ManagedWorld(laying, stepsAside: false));
        aside.HandTheWorldTo(laying => new ManagedWorld(laying, stepsAside: true));
        var atOnceLoop = new SimLoop<TownWorld>(atOnce, Config);
        var asideLoop = new SimLoop<TownWorld>(aside, Config);

        var unlike = new List<string>();
        for (var tick = 1; tick <= Ticks && unlike.Count == 0; tick++)
        {
            atOnceLoop.Advance();
            asideLoop.Advance();
            TownsInStep.Compare(atOnce, aside, tick, unlike);
        }

        Assert.True(unlike.Count == 0, string.Join('\n', unlike.Take(8)));
        Assert.Equal(atOnce.World.Marks.Count, aside.World.Marks.Count);
    }
}
