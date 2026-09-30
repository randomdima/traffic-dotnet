using TrafficSimulation.CityGen;
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
[Trait(Priority.Key, Priority.P3)]
public class ServiceRosterTests
{
    public static TheoryData<string> Maps => Towns.EveryTown();

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

    /// <summary>A map with nothing on it asks for no service, rather than one of nothing (GEN-56).</summary>
    [Fact]
    public void AMapWithNoBuildingsAsksForNoService() =>
        Assert.Equal(0, ServiceBuildings.OfEachUse(Towns.WithBuildings(0)));
}
