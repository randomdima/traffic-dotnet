using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// <b>The town run on to where its turns are taken</b>: they are taken on a view whose step read the covers and not on
/// every tick, so a question about what the layer holds — after something has been put down, or of a tick's bodies —
/// is asked on a tick the two are of one instant.
/// </summary>
internal static class Claims
{
    /// <summary>On at least one tick, and on until the turns have been taken on one.</summary>
    public static void UntilLaid(SimLoop<TownWorld> loop)
    {
        do loop.Advance();
        while (!loop.World.TurnsTakenThisTick);
    }
}
