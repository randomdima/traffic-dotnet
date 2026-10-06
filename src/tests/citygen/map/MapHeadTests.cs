using System.Numerics;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.CityGen.Zones;
using Xunit;

namespace TrafficSimulation.Tests.CityGen.Map;

/// <summary>
/// <b>The menu describes a map off the head of its file alone</b> (<see cref="MapHead"/>): a page holds nothing else of
/// one until it is opened, so the map has to have written the description inside it.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P6)]
public class MapHeadTests
{
    [Fact]
    public void TheDescriptionIsReadOffTheHeadAlone()
    {
        // Two thousand points put the map well past its head, so the cut below is a map cut short.
        const int Points = 2000;
        var map = TownMap.Bare("Surveyed", "a few ways laid by hand", 1, new Vector2(10, 10), ZoneKind.Town, []) with
        {
            PointM = [.. Enumerable.Range(0, Points).Select(point => new Vector2D(point * 3.7, point * 1.3))],
        };

        using var written = new MemoryStream();
        map.Write(written);
        var head = Scratch.Write("map-head.map", written.GetBuffer().AsSpan(0, MapHead.Bytes));

        Assert.Equal("a few ways laid by hand", MapHead.Description(head));
    }
}
