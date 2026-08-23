using TrafficSimulation.Bench;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Gates;

/// <summary>
/// PHY-1 as a gate: <b>solid bodies never overlap</b>, asked of a town that is running rather than of a
/// staged pair.
/// </summary>
/// <remarks>
/// <para>
/// What is asserted is that no <em>one</em> body stays inside another, and not that nothing is ever
/// inside anything. The second is not a fact about this town and never will be: a soft-step solver
/// answers an approach by letting the pair touch and pushing them apart over the ticks that follow, so
/// in a city of a thousand bodies there is always one of them mid-recovery. The distinction is the whole
/// gate — recovery is a handful of ticks and being stuck is every tick until something moves.
/// </para>
/// <para>
/// It is worth having as a test rather than as a soak somebody remembers to run: every later milestone
/// adds bodies and reasons for them to be pressed together, and the tick a car is allowed to sink into
/// a queue and stay there is the tick this stops being a town.
/// </para>
/// </remarks>
[Trait(Tier.Key, Tier.Perf)]
[Collection(Simulation.SolverCollection.Name)]
public class OverlapGateTests
{
    /// <summary>Short of the soak's own length, which is what <c>--bench soak</c> is for; long enough for the queues to form.</summary>
    const int Ticks = 600;

    [Theory]
    [MemberData(nameof(Towns.EveryShippedMap), MemberType = typeof(Towns))]
    public void NoBodyInAStandingTownIsLeftInsideAnother(string map)
    {
        var config = SimConfig.Shipped();
        using var world = new TownWorld(Towns.Fresh(map), config);
        var loop = new SimLoop<TownWorld>(world, config);

        var bodies = world.People.Count + world.Cars.Count;
        var overlapM = new float[bodies];
        var stuckForTicks = new int[bodies];
        var longestStuckTicks = 0;

        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance();
            SoakProbe.SweepOverlaps(world, overlapM);
            for (var body = 0; body < bodies; body++)
            {
                stuckForTicks[body] = overlapM[body] > SoakProbe.OverlapAllowanceM ? stuckForTicks[body] + 1 : 0;
                longestStuckTicks = Math.Max(longestStuckTicks, stuckForTicks[body]);
            }
        }

        Assert.InRange(longestStuckTicks, 0, SoakProbe.StuckAfterTicks);
    }
}
