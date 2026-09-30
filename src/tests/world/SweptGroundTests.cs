using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// <b>Ground an action swept once and committed to</b> (TER-4c.6, TER-4c.8): a way under consecutive stations is one
/// run covering all of them, a run ends where its way is missing or its span is spent, and a holder with no room left
/// refuses the station.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P1)]
public class SweptGroundTests
{
    const int Holder = 1;

    const float SpanM = 4f;

    [Fact]
    public void AWayUnderConsecutiveStationsIsOneRunCoveringAllOfThem()
    {
        var ground = new SweptGround(holders: 2, mostRunsEach: 4);
        ground.Clear(Holder);

        Assert.True(ground.Station(Holder, 0f, SpanM, [new WayCover(7, 10f, 12f)]));
        Assert.True(ground.Station(Holder, 1f, SpanM, [new WayCover(7, 11f, 13f)]));
        Assert.True(ground.Station(Holder, 2f, SpanM, [new WayCover(7, 9f, 14f)]));

        Assert.Equal([new SweptRun(7, 9f, 14f, 0f, 2f)], ground.Of(Holder).ToArray());
    }

    [Fact]
    public void AWayMissingUnderAStationEndsItsRun()
    {
        var ground = new SweptGround(holders: 2, mostRunsEach: 4);
        ground.Clear(Holder);

        ground.Station(Holder, 0f, SpanM, [new WayCover(7, 10f, 12f)]);
        ground.Station(Holder, 1f, SpanM, [new WayCover(8, 0f, 2f)]);
        ground.Station(Holder, 2f, SpanM, [new WayCover(7, 20f, 22f)]);

        Assert.Equal(
            [new SweptRun(7, 10f, 12f, 0f, 0f), new SweptRun(8, 0f, 2f, 1f, 1f), new SweptRun(7, 20f, 22f, 2f, 2f)],
            ground.Of(Holder).ToArray());
    }

    [Fact]
    public void ARunSpansNoMoreOfThePathThanItsSpan()
    {
        var ground = new SweptGround(holders: 2, mostRunsEach: 4);
        ground.Clear(Holder);

        for (var station = 0; station < 3; station++)
        {
            ground.Station(Holder, station * 3f, SpanM, [new WayCover(7, station, station + 1f)]);
        }

        Assert.Equal(
            [new SweptRun(7, 0f, 2f, 0f, 3f), new SweptRun(7, 2f, 3f, 6f, 6f)],
            ground.Of(Holder).ToArray());
    }

    [Fact]
    public void AHolderWithNoRoomLeftForARunRefusesTheStation()
    {
        var ground = new SweptGround(holders: 2, mostRunsEach: 1);
        ground.Clear(Holder);

        Assert.False(ground.Station(Holder, 0f, SpanM, [new WayCover(7, 0f, 1f), new WayCover(8, 0f, 1f)]));
    }
}
