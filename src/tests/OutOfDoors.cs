using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Tests;

/// <summary>
/// Getting hold of a walker that is standing in the town. <b>On a town everybody starts indoors</b>
/// (GEN-7), so a question about a body on the pavement is asked after somebody's first dwell is up
/// rather than at the first tick — a dwell is bounded, so one always is.
/// </summary>
internal static class OutOfDoors
{
    /// <param name="besides">A walker already in hand, when what is wanted is a second one.</param>
    public static int AWalker(TownWorld world, SimLoop<TownWorld> loop, SimConfig config, int besides = -1)
    {
        var mostTicks = (int)MathF.Ceiling(config.Building.DwellMaxS / config.TickSeconds) + 1;
        for (var waited = 0; waited < mostTicks; waited++)
        {
            for (var person = 0; person < world.People.Count; person++)
            {
                if (person != besides && !world.Containment.IsContained(person)) return person;
            }

            loop.Advance(1);
        }

        throw new InvalidOperationException("no walker came out of a door within a dwell of the first tick");
    }
}
