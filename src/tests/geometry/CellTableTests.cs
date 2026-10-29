using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using Xunit;

namespace TrafficSimulation.Tests.Geometry;

/// <summary><b>A cell reads back the last number filed against it, and a cell never filed reads −1</b>, however full the table grew.</summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P3)]
public class CellTableTests
{
    [Fact]
    public void ACellReadsTheLastNumberFiledAgainstItAndNoneReadsMinusOne()
    {
        var draw = new Rng(5, 1);
        var table = new CellTable();
        var filed = new Dictionary<long, int>();
        for (var set = 0; set < 20000; set++)
        {
            var key = GridLevel.KeyOf(draw.NextInt(300) - 150, draw.NextInt(300) - 150);
            var value = draw.NextInt(1 << 20);
            table.Set(key, value);
            filed[key] = value;
        }

        Assert.Equal(filed.Count, table.Count);
        for (var x = -160; x < 160; x++)
        {
            for (var y = -160; y < 160; y++)
            {
                var key = GridLevel.KeyOf(x, y);
                Assert.Equal(filed.TryGetValue(key, out var value) ? value : -1, table[key]);
            }
        }
    }
}
