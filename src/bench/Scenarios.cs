using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>Which claims a town is watched against</b>: the two every town owes whichever map it is — nothing
/// left inside anything, nothing standing unclocked.
/// </summary>
/// <remarks>
/// <b>A map laid to answer one question adds its own watch here</b>, and none of them is laid by this
/// build: the laboratories were laid against the layer the lane rework replaces, and the ones that come
/// back will bring their watch with them.
/// </remarks>
internal static class Scenarios
{
    /// <summary>
    /// The watches this town is answered by. <b>They are built with the town and not once it is running</b>:
    /// a staging that ordered its cars on the tenth tick would be measuring whatever the map did with the
    /// first nine.
    /// </summary>
    public static ScenarioWatch[] For(TownWorld world, SimConfig config) => [new TownWatch(world, config)];
}
