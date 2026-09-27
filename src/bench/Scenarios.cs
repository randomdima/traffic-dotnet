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
        return string.Equals(world.Plan.Name, ExamPlan.Name, StringComparison.Ordinal)
            ? [ExamWatch.Over(config, world), town]
            : [town];
    }
}
