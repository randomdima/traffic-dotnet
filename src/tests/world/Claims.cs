using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// <b>The town run on to where its claims are laid</b>: they are laid once a decision interval and not every tick, so
/// a question about what the layer holds — after something has been put down, or of a tick's bodies — is asked on a
/// tick the two are of one instant.
/// </summary>
internal static class Claims
{
    /// <summary>On at least one tick, and on until the claims have been laid on one.</summary>
    public static void UntilLaid(SimLoop<TownWorld> loop)
    {
        do loop.Advance();
        while (!loop.World.ClaimsLaidThisTick);
    }
}
