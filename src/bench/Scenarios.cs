using TrafficSimulation.CityGen.Exam;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>Which claims a town is watched against</b>: the ones its own map was laid to answer, and under them
/// the two every town owes whichever map it is — nothing left inside anything, nothing standing unclocked.
/// </summary>
/// <remarks>
/// <b>A map laid to answer one question adds its own watch here.</b> The scenario map is the one there is
/// (<see cref="ExamWatch"/>); the laboratories the lane rework replaced come back with a watch of their own.
/// </remarks>
internal static class Scenarios
{
    /// <summary>
    /// The watches this town is answered by, the map's own first. <b>They are built with the town and not
    /// once it is running</b>: a staging that ordered its cars on the tenth tick would be measuring whatever
    /// the map did with the first nine.
    /// </summary>
    public static ScenarioWatch[] For(TownWorld world, SimConfig config)
    {
        var town = new TownWatch(world, config);
        ScenarioWatch[] every = world.Trains.Count > 0 ? [town, new RailWatch(world, config)] : [town];
        return Stages(world.Plan.Name) ? [ExamWatch.Over(config, world), .. every] : every;
    }

    /// <summary>
    /// Whether a map's watches stage the town before its first tick — asking the world off the clock, which a world that
    /// cannot wait cannot answer (a page's device).
    /// </summary>
    public static bool Stages(string map) => string.Equals(map, ExamPlan.Name, StringComparison.Ordinal);
}
