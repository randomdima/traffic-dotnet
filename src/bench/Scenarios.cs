using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>Which claims a town is watched against</b>: the ones its own map was laid to answer, and under them
/// the two every town owes whichever map it is.
/// </summary>
/// <remarks>
/// <b>Every map is watched and not only the laid ones.</b> A city has no staging of its own, and the
/// claims that survive without one — nothing left inside anything, nothing standing unclocked — are the
/// ones worth reading off a run somebody opened to look at. What a scenario map adds is the question it
/// was laid for.
/// </remarks>
internal static class Scenarios
{
    /// <summary>
    /// The watches this town is answered by, the map's own first. <b>They are built with the town and not
    /// once it is running</b>: a staging that ordered its cars on the tenth tick would be measuring
    /// whatever the map did with the first nine.
    /// </summary>
    public static ScenarioWatch[] For(TownWorld world, SimConfig config)
    {
        var map = world.Plan.Name;
        var town = new TownWatch(world);

        if (string.Equals(map, ExamPlan.Name, StringComparison.Ordinal))
            return [new ExamWatch(config, world), town];

        if (string.Equals(map, ZebraWatch.Map, StringComparison.Ordinal))
            return [new ZebraWatch(config, world), town];

        if (string.Equals(map, SkidpadPlan.Name, StringComparison.Ordinal))
            return [new SkidpadWatch(config, world), town];

        if (string.Equals(map, TrackPlan.FleetName, StringComparison.Ordinal))
            return [new FleetWatch(config, world), town];

        if (string.Equals(map, TrackPlan.Name, StringComparison.Ordinal))
            return [new TrackWatch(config, world, TrackLap.Pacing), town];

        if (string.Equals(map, TrackPlan.DrunkName, StringComparison.Ordinal))
            return [new TrackWatch(config, world, TrackLap.Drunk), town];

        return [town];
    }

    /// <summary>
    /// The proving ground's own figures, where one of these watches is carrying them. <b>The instrument is
    /// the watch's</b> so that the lap is measured once: the panel that draws the shape table and the
    /// claims about it are reading one <see cref="TrackMetrics"/>.
    /// </summary>
    public static TrackMetrics? FiguresIn(ReadOnlySpan<ScenarioWatch> watching)
    {
        foreach (var watch in watching)
        {
            if (watch is LapWatch lap) return lap.Metrics;
        }

        return null;
    }
}
