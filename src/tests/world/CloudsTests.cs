using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// OBJ-8's clouds (<see cref="Clouds"/>) cross the town the way its wind blows about, at their own speed, and none of the
/// wind's swings and gusts reach them.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P9)]
public class CloudsTests
{
    static readonly WindFigures Air = new();
    static readonly CloudFigures Sky = new();

    /// <summary>Where a point of the field stands over the town at <paramref name="elapsedS"/>.</summary>
    static Vector2 UnderM(Clouds clouds, double elapsedS) => -clouds.FieldAtM(elapsedS);

    [Fact]
    public void ACloudCrossesTheTownTheWayTheWindBlowsAboutAtTheCloudsSpeed()
    {
        var wind = new Wind(Air, seed: 7);
        var clouds = new Clouds(Sky, wind, seed: 7);

        var movedM = UnderM(clouds, 70.0) - UnderM(clouds, 10.0);

        Assert.Equal(Sky.SpeedMps * 60f, movedM.Length(), 0.01f);
        Assert.Equal(wind.PrevailingRad, MathF.Atan2(movedM.Y, movedM.X), 0.0001f);
    }

    [Fact]
    public void ACloudMovesAlikeWhereverTheWindHasSwungAndGusted()
    {
        var wind = new Wind(Air, seed: 7);
        var clouds = new Clouds(Sky, wind, seed: 7);
        var quarterTurnS = Air.TurnPeriodS / 4.0;

        var earlyM = UnderM(clouds, Air.GustPeriodS) - UnderM(clouds, 0.0);
        var lateM = UnderM(clouds, quarterTurnS + Air.GustPeriodS) - UnderM(clouds, quarterTurnS);

        Assert.NotEqual(wind.BearingRad(0f), wind.BearingRad((float)quarterTurnS));
        Assert.Equal(earlyM.X, lateM.X, 0.01f);
        Assert.Equal(earlyM.Y, lateM.Y, 0.01f);
    }

    [Fact]
    public void TwoTownsSeedsLayTwoSkies()
    {
        var one = new Clouds(Sky, new Wind(Air, seed: 1), seed: 1);
        var two = new Clouds(Sky, new Wind(Air, seed: 2), seed: 2);

        Assert.NotEqual(one.FieldAtM(0.0), two.FieldAtM(0.0));
    }
}
