using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// <b>A car whose decision would do nothing but run its leg's clock is decided by the clock alone</b>
/// (<see cref="TownWorld.TakesOnlyTheClock"/>, <see cref="TownWorld.DecidesQuietCarsByTheClock"/>): what is asked of it
/// before the clock is the whole of what the decision would have done otherwise, so the town does nothing differently.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P2)]
public class QuietDecisionsInATownTests
{
    /// <summary>Twenty seconds: every car's decision due two hundred times, some of them a pass, a switch or a bay.</summary>
    const int Ticks = 1200;

    /// <summary>
    /// <b>Every tick, every car and walker stands, moves and faces to the bit as in the town that decides every car whole,
    /// and holds the same hold</b> — over a run in which cars were decided by their clock alone.
    /// </summary>
    [Fact]
    public void ACarDecidedByItsClockAloneLeavesTheTownAsOneDecidedWhole()
    {
        var config = SimConfig.Shipped();
        using var whole = new TownWorld(Towns.Built, config);
        using var byTheClock = new TownWorld(Towns.Built, config) { DecidesQuietCarsByTheClock = true };
        var wholeLoop = new SimLoop<TownWorld>(whole, config);
        var clockLoop = new SimLoop<TownWorld>(byTheClock, config);

        var unlike = new List<string>();
        for (var tick = 0; tick < Ticks && unlike.Count == 0; tick++)
        {
            wholeLoop.Advance();
            clockLoop.Advance();
            TownsInStep.Compare(whole, byTheClock, tick, unlike);
        }

        Assert.True(byTheClock.DecidedByTheClock > 0, "no car was decided by its clock alone");
        Assert.True(unlike.Count == 0, $"{unlike.Count} agents differ: {string.Join("; ", unlike.Take(6))}");
    }

    /// <summary>
    /// <b>Every tick, every car and walker stands, moves and faces to the bit as in the town that decides every walker
    /// whole, and holds the same hold</b> — over a run in which walkers were decided by their clock alone
    /// (<see cref="TownWorld.WalkerTakesOnlyTheClock"/>).
    /// </summary>
    [Fact]
    public void AWalkerDecidedByItsClockAloneLeavesTheTownAsOneDecidedWhole()
    {
        var config = SimConfig.Shipped();
        using var whole = new TownWorld(Towns.Built, config);
        using var byTheClock = new TownWorld(Towns.Built, config) { DecidesQuietWalkersByTheClock = true };
        var wholeLoop = new SimLoop<TownWorld>(whole, config);
        var clockLoop = new SimLoop<TownWorld>(byTheClock, config);

        var unlike = new List<string>();
        for (var tick = 0; tick < Ticks && unlike.Count == 0; tick++)
        {
            wholeLoop.Advance();
            clockLoop.Advance();
            TownsInStep.Compare(whole, byTheClock, tick, unlike);
        }

        Assert.True(byTheClock.WalkersDecidedByTheClock > 0, "no walker was decided by its clock alone");
        Assert.True(unlike.Count == 0, $"{unlike.Count} agents differ: {string.Join("; ", unlike.Take(6))}");
    }
}
