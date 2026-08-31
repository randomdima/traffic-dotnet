using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Terrain;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// The carriageway under a crossing, projected onto the crossing's own ways: that a band is one lane's
/// width of a zebra rather than the zebra, that it lies on the way it is measured along, and that the
/// bands of one way are the lanes in the order a body walking it meets them.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class CrossingBandsTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// <b>A band is one lane wide.</b> The whole reason it exists: a car takes the paint it drives over
    /// and a body holds the lane it stands in, and either of them taking the way's whole length is a
    /// crossing held shut by somebody nowhere near it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void ABandIsOneLanesWidthOfTheWay(string map)
    {
        var (bands, roads, walking, crossings) = Project(map);

        var found = 0;
        for (var crossing = 0; crossing < crossings; crossing++)
        {
            foreach (var edge in bands.WaysOf(crossing))
            {
                foreach (var band in bands.On(edge))
                {
                    found++;
                    Assert.True(
                        band.ToM - band.FromM <= (roads.LaneWidthM[band.Lane] * WidestSkew) + (StationM * 2f),
                        $"{map}: lane {band.Lane} covers {band.ToM - band.FromM:0.00} m of crossing way {edge}, "
                        + $"which is {roads.LaneWidthM[band.Lane]:0.00} m wide");

                    // And never less, or a car takes paint it drives over and hands the rest to a body.
                    // The exception is a way that ends inside the lane, where the ground is all there is.
                    var whole = band.FromM > StationM && band.ToM < walking.LaneLengthM(edge) - StationM;
                    Assert.True(
                        !whole || band.ToM - band.FromM >= roads.LaneWidthM[band.Lane] - Rounding,
                        $"{map}: lane {band.Lane} covers only {band.ToM - band.FromM:0.00} m of crossing way "
                        + $"{edge}, and is {roads.LaneWidthM[band.Lane]:0.00} m wide");
                }
            }
        }

        Assert.True(found > 0 || crossings == 0, $"{map} paints crossings and no lane runs under one");
        AssertEveryBandIsOnItsWay(map, bands, walking, crossings);
    }

    /// <summary>
    /// And <b>in the order the way meets them</b>, which is what makes the first band the one a body
    /// stepping off the kerb enters — the whole of what PER-15 asks about. Two bands may touch where two
    /// lanes do and may not overlap by more than the slack their edges were rounded out by.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void TheBandsOfAWayAreTheLanesInTheOrderItMeetsThem(string map)
    {
        var (bands, _, _, crossings) = Project(map);

        for (var crossing = 0; crossing < crossings; crossing++)
        {
            foreach (var edge in bands.WaysOf(crossing))
            {
                var under = bands.On(edge);
                for (var at = 1; at < under.Length; at++)
                {
                    Assert.True(
                        under[at].FromM >= under[at - 1].ToM - (StationM * 2f),
                        $"{map}: crossing way {edge} has lane {under[at].Lane} beginning at "
                        + $"{under[at].FromM:0.00} m under lane {under[at - 1].Lane}, which runs to "
                        + $"{under[at - 1].ToM:0.00} m");
                }
            }
        }
    }

    /// <summary>
    /// <b>A crossing of more than one lane is not one band.</b> Stated apart from the width, because it is
    /// the reading a driver and a walker are actually held against: on a two-lane road there is ground on
    /// the paint that belongs to one of them and not the other.
    /// </summary>
    [Fact]
    public void AZebraOverTwoLanesIsTwoBands()
    {
        var (bands, _, walking, crossings) = Project("Odesa");

        var many = 0;
        for (var crossing = 0; crossing < crossings; crossing++)
        {
            foreach (var edge in bands.WaysOf(crossing))
            {
                var under = bands.On(edge);
                if (under.Length < 2) continue;

                many++;
                var covered = 0f;
                foreach (var band in under) covered += band.ToM - band.FromM;

                Assert.True(
                    covered < walking.LaneLengthM(edge),
                    $"the lanes under crossing way {edge} cover {covered:0.00} m of its "
                    + $"{walking.LaneLengthM(edge):0.00} m");
            }
        }

        Assert.True(many > 0, "no crossing in Odesa runs under more than one lane");
    }

    static void AssertEveryBandIsOnItsWay(string map, CrossingBands bands, WalkingNetwork walking, int crossings)
    {
        for (var crossing = 0; crossing < crossings; crossing++)
        {
            foreach (var edge in bands.WaysOf(crossing))
            {
                var lengthM = walking.LaneLengthM(edge);
                foreach (var band in bands.On(edge))
                {
                    Assert.True(
                        band.FromM >= 0f && band.ToM <= lengthM && band.ToM > band.FromM,
                        $"{map}: lane {band.Lane} covers {band.FromM:0.00}–{band.ToM:0.00} m of crossing way "
                        + $"{edge}, which is {lengthM:0.00} m long");
                }
            }
        }
    }

    /// <summary>
    /// How far past a lane's own width a band may run. A lane crosses the paint no more than a right angle
    /// off square (<c>LaneFurniture</c> takes nothing further off than that), so the widest a band gets is
    /// the width over the sine of it.
    /// </summary>
    const float WidestSkew = 1.45f;

    /// <summary>The step the projection walks a way at, which is the slack every edge of a band carries.</summary>
    const float StationM = 0.25f;

    /// <summary>A band is metres of arithmetic on floats: a millimetre is not a finding.</summary>
    const float Rounding = 1e-3f;

    public static TheoryData<string> Maps => Towns.EveryTown();

    static (CrossingBands Bands, RoadGraph Roads, WalkingNetwork Walking, int Crossings) Project(string map)
    {
        var plan = Towns.Of(map);
        var roads = RoadGraph.Build(plan, Config);
        var furniture = LaneFurniture.Project(plan, roads);
        var walking = WalkingNetwork.Build(FootGraph.Build(plan, Config), new TerrainGrid(plan, Config), Config);

        return (CrossingBands.Project(plan, roads, furniture, walking), roads, walking, plan.Crosswalks.Count);
    }
}
