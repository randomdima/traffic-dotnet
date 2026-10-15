using TrafficSimulation.Core.Simulation;
using Xunit;

namespace TrafficSimulation.Tests.Simulation;

/// <summary>
/// <b>A keyed sort leaves the keys in order and every row beside its own key</b> — what the atlas is filed by, so a row
/// parted from its key is a way filed under a point it does not cover.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P2)]
public class KeyedSortTests
{
    public static TheoryData<string> Shapes => ["random", "sorted", "reversed", "equal", "few", "organ pipe"];

    [Theory]
    [MemberData(nameof(Shapes))]
    public void TheKeysComeOutInOrderEachWithItsOwnRow(string shape)
    {
        foreach (var count in (ReadOnlySpan<int>)[0, 1, 2, 3, 15, 16, 17, 100, 4099])
        {
            var keys = Keys(shape, count);
            var given = (long[])keys.Clone();
            var rows = Enumerable.Range(0, count).ToArray();

            KeyedSort.Sort(keys.AsSpan(), rows.AsSpan());

            for (var at = 1; at < count; at++) Assert.True(keys[at - 1] <= keys[at], $"{shape}, {count}: out of order at {at}");
            for (var at = 0; at < count; at++) Assert.Equal(given[rows[at]], keys[at]);
            Assert.Equal(Enumerable.Range(0, count), rows.Order());
        }
    }

    static long[] Keys(string shape, int count)
    {
        var draw = new Random(count);
        return shape switch
        {
            "random" => Enumerable.Range(0, count).Select(_ => draw.NextInt64(long.MinValue, long.MaxValue)).ToArray(),
            "sorted" => Enumerable.Range(0, count).Select(at => (long)at).ToArray(),
            "reversed" => Enumerable.Range(0, count).Select(at => (long)(count - at)).ToArray(),
            "equal" => new long[count],
            "few" => Enumerable.Range(0, count).Select(_ => (long)draw.Next(3)).ToArray(),
            _ => Enumerable.Range(0, count).Select(at => (long)Math.Min(at, count - at)).ToArray(),
        };
    }
}
