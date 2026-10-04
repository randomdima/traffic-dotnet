using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.CityGen.Traced;

/// <summary>
/// <b>A traced town stands cars at its bridges over its roads</b> (GEN-57, PHY-1a): a car each way on the bridge over
/// the road it crosses, and one each way on that road short of the deck — asked of one street carried over another
/// (<see cref="TracedPlanTests.OverAStreet"/>).
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P3)]
public class TracedBridgeCarsTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// <b>One car each way stands on the bridge right over the street</b>, on the bridge's carriageway and pointing
    /// along it.
    /// </summary>
    [Fact]
    public void ACarEachWayStandsOnTheBridgeOverTheStreet()
    {
        var plan = TracedPlanTests.OverAStreet();
        var crossingM = TracedPlanTests.OverAStreetCrossingM;
        var halfM = plan.Roads.WidthM[plan.Bridges.Road[0]] * 0.5f;

        var over = Standing(plan).Where(car => MathF.Abs(car.AtM.X - crossingM.X) < LineTolerance.RoundingM).ToArray();

        Assert.Equal(2, over.Length);
        Assert.All(over, car => Assert.InRange(MathF.Abs(car.AtM.Y - crossingM.Y), 0f, halfM));
        Assert.Equal([-1f, 1f], over.Select(car => MathF.Round(car.Facing.X)).Order());
    }

    /// <summary>
    /// <b>One car each way stands on the street short of the deck</b>, clear of it and pointing at the bridge it is
    /// about to pass under.
    /// </summary>
    [Fact]
    public void ACarEachWayStandsOnTheStreetShortOfTheDeck()
    {
        var plan = TracedPlanTests.OverAStreet();
        var crossingM = TracedPlanTests.OverAStreetCrossingM;
        var deckHalfM = plan.Bridges.DeckWidthM[0] * 0.5f;

        var under = Standing(plan).Where(car => MathF.Abs(car.AtM.X - crossingM.X) >= LineTolerance.RoundingM).ToArray();

        Assert.Equal(2, under.Length);
        Assert.All(under, car => Assert.True(MathF.Abs(car.AtM.Y - crossingM.Y) > deckHalfM, $"{car.AtM} stands under the deck"));
        Assert.All(under, car => Assert.True(Vector2.Dot(crossingM - car.AtM, car.Facing) > 0f, $"{car.AtM} faces away from the bridge"));
    }

    static IEnumerable<(Vector2 AtM, Vector2 Facing)> Standing(CityPlan plan) =>
        Enumerable.Range(0, plan.Spawns.Count).Select(spawn =>
            (plan.Spawns.PositionM[spawn], Heading.Unit(plan.Spawns.HeadingRad[spawn])));
}
