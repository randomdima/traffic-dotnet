using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// <b>The walk and the traffic share ground on the paint and nowhere else</b> (WLK-16), read off the marks
/// every secondary claim is placed through (TER-5c) — so a walker off a zebra is never weighed against a car.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P1)]
public class WalkOffTheRoadTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    [Theory]
    [MemberData(nameof(Towns.EveryMapWithAFootway), MemberType = typeof(Towns))]
    public void NoWayButAZebrasLanesIsMarkedAgainstTheTraffic(string map)
    {
        using var world = new TownWorld(Towns.Of(map), Config);
        var ways = world.Ways;
        var shared = new List<string>();
        for (var way = ways.FirstFootwayWay; way < ways.Count; way++)
        {
            if (world.IsTheCrossing(way)) continue;

            foreach (var mark in world.Atlas.Marks.Of(way))
            {
                if (!ways.IsDriven(mark.OnWay)) continue;

                shared.Add(
                    $"{ways.KindOf(way)} {way} {mark.MineFromM:F2}–{mark.MineToM:F2} m over " +
                    $"{ways.KindOf(mark.OnWay)} {mark.OnWay} {mark.FromM:F2}–{mark.ToM:F2} m");
            }
        }

        Assert.True(
            shared.Count == 0,
            $"{shared.Count} walked ways off the paint share the traffic's ground: {string.Join("; ", shared.Take(5))}");
    }
}
