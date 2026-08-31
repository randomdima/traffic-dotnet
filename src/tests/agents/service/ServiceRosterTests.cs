using TrafficSimulation.Agents.Service;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Statics;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Service;

/// <summary>
/// SRV-1: <b>which buildings are police stations and which are depots is a fact about the map</b>,
/// declared the way a hospital is — and the three uses share one set of buildings, so what the file must
/// never do is give the same one two uses.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class ServiceRosterTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    public static TheoryData<string> Maps => Towns.EveryTown();

    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryMapDeclaresTheStationsAndDepotsThisBuildWouldPlace(string map)
    {
        var plan = Towns.Of(map);

        Assert.Equal(
            PoliceStationRoster.CountIn(plan, Config), BuildingRoster.Of(plan, BuildingUse.PoliceStation).Count);
        Assert.Equal(DepotRoster.CountIn(plan, Config), BuildingRoster.Of(plan, BuildingUse.Depot).Count);
    }

    /// <summary>
    /// <b>A building serves one use at most</b> (SRV-1) — which the file settles rather than the order
    /// anything is read in, one byte a building being unable to say two things. What is checked is that
    /// the rosters read back off it agree.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NoBuildingServesTwoUses(string map)
    {
        var plan = Towns.Of(map);
        var uses = BuildingUses.Of(plan);

        foreach (var building in uses.PoliceStations.Buildings) Assert.False(uses.Hospitals.Holds(building));
        foreach (var building in uses.Depots.Buildings)
        {
            Assert.False(uses.Hospitals.Holds(building));
            Assert.False(uses.PoliceStations.Holds(building));
        }
    }

    /// <summary>
    /// <b>A town with a building on it gets one of each</b>, and never more of either than its ceiling —
    /// which is what makes a service vehicle something every shipped map can be looked at for.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(40)]
    public void ATownWithBuildingsHasAStationAndADepot(int buildings)
    {
        var few = Towns.WithBuildings(buildings);

        Assert.InRange(PoliceStationRoster.CountIn(few, Config), 1, Math.Min(Config.Service.MostStations, buildings));
        Assert.InRange(DepotRoster.CountIn(few, Config), 1, Math.Min(Config.Service.MostDepots, buildings));
    }

    /// <summary>And a map with nothing on it stands none, rather than one of nothing.</summary>
    [Fact]
    public void AMapWithNoBuildingsStandsNothing()
    {
        var plan = Towns.WithBuildings(0);

        Assert.Equal(0, PoliceStationRoster.CountIn(plan, Config));
        Assert.Equal(0, DepotRoster.CountIn(plan, Config));
        Assert.Equal(0, BuildingRoster.Of(plan, BuildingUse.PoliceStation).Count);
    }
}
