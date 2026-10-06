using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.CityGen.Zones;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.CityGen.Zones;

/// <summary>
/// <b>Which zone a place stands in, and what it is laid by</b> (GEN-58, <see cref="ZoneTree"/>): the deepest zone
/// holding it, the smaller of two as deep and the whole map where none does; the ground it holds; a zone's settings its
/// own over its kind's; and a zone that says any look saying its whole mix.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P6)]
public class ZoneTreeTests
{
    static readonly ZoneFigures Figures = SimConfig.Shipped().Zones;

    /// <summary><b>The deepest zone holding a place is its zone</b>: a park inside a suburb is the park's ground, the rest the suburb's.</summary>
    [Fact]
    public void TheDeepestZoneHoldingAPlaceIsItsZone()
    {
        var tree = Tree(new(ZoneKind.Suburb, Square(100, 100, 400), []), new(ZoneKind.Park, Square(200, 200, 100), [], Parent: 1));

        Assert.Equal((2, 1, TownMap.ZoneArrays.Root), (tree.At(new(250, 250)), tree.At(new(450, 450)), tree.At(new(50, 50))));
    }

    /// <summary><b>Of two zones as deep, the smaller is the one</b>: a school's grounds overlapping a quarter's edge are the school's where they overlap.</summary>
    [Fact]
    public void OfTwoAsDeepTheSmallerIsTheOne()
    {
        var tree = Tree(new(ZoneKind.Residential, Square(100, 100, 400), []), new(ZoneKind.Civic, Square(450, 100, 100), []));

        Assert.Equal(2, tree.At(new(475, 150)));
    }

    /// <summary><b>A hole is no part of its zone</b>: a place in a courtyard cut out of a quarter is the whole map's.</summary>
    [Fact]
    public void AHoleIsNoPartOfItsZone()
    {
        var zones = new TownMap.ZoneArrays.Builder();
        zones.Add(-1, ZoneKind.Town, [], [TownMap.WholeOutline(new Vector2(1000, 1000))]);
        zones.Add(0, ZoneKind.OldTown, [], [Square(100, 100, 400), Square(200, 200, 100)]);
        var tree = new ZoneTree(zones.Arrays(), Figures);

        Assert.Equal((TownMap.ZoneArrays.Root, 1), (tree.At(new(250, 250)), tree.At(new(150, 150))));
    }

    /// <summary><b>A zone is laid by its own settings over its kind's</b>: a suburb saying its frontage keeps every other of its kind's settings.</summary>
    [Fact]
    public void AZoneIsLaidByItsOwnOverItsKinds()
    {
        var tree = Tree(new Zoned.Zone(ZoneKind.Suburb, Square(100, 100, 400), [(ZoneParam.Frontage, 0.25f)]));

        Assert.Equal((0.25f, Figures.Suburb.FrontM, Figures.Suburb.SkewDeg), (tree[1].Frontage, tree[1].FrontM, tree[1].SkewDeg));
    }

    /// <summary>
    /// <b>A zone holds its outline's ground less the zones inside it</b>, which hold their own: a park inside a suburb is
    /// none of the suburb's ground, and the suburb none of the whole map's.
    /// </summary>
    [Fact]
    public void AZoneHoldsItsOutlineLessTheZonesInsideIt()
    {
        var tree = Tree(new(ZoneKind.Suburb, Square(100, 100, 400), []), new(ZoneKind.Park, Square(200, 200, 100), [], Parent: 1));

        Assert.Equal((840_000f, 150_000f, 10_000f), (tree.AreaM2Of(TownMap.ZoneArrays.Root), tree.AreaM2Of(1), tree.AreaM2Of(2)));
    }

    /// <summary><b>A zone that says any look says its whole mix</b>: a residential quarter saying it builds towers builds no flats or houses its kind would.</summary>
    [Fact]
    public void AZoneThatSaysAnyLookSaysItsWholeMix()
    {
        var tree = Tree(new Zoned.Zone(ZoneKind.Residential, Square(100, 100, 400), [(ZoneParams.Of(BuildingLook.Tower), 2f)]));

        Assert.Equal(1f, tree[1].LookShares[(int)BuildingLook.Tower]);
        Assert.Equal(1f, tree[1].LookShares.Sum());
    }

    static ZoneTree Tree(params Zoned.Zone[] zones) => new(Zoned.Town(1000f, 1000f, [], zones), Figures);

    static Vector2[] Square(float xM, float yM, float sideM) => [new(xM, yM), new(xM + sideM, yM), new(xM + sideM, yM + sideM), new(xM, yM + sideM)];
}
