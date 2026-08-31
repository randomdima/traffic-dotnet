using TrafficSimulation.Agents.Ambulance;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Statics;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Ambulance;

/// <summary>
/// AMB-1: <b>which buildings are hospitals is a fact about the map</b>, declared in the file, so it is
/// the same every time a map is opened; the count this build would place is a share with a floor and a
/// ceiling, so a village and a city both come out with a plausible number of them.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class HospitalRosterTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    public static TheoryData<string> Maps => Towns.EveryTown();

    /// <summary>
    /// <b>The count has to be answerable before the town is stood up</b>: the fleets are laid for an
    /// ambulance and a crew apiece, so the plan alone has to say how many there will be.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryMapWithBuildingsDeclaresTheHospitalsThisBuildWouldPlace(string map)
    {
        var plan = Towns.Of(map);

        Assert.Equal(HospitalRoster.CountIn(plan, Config), BuildingRoster.Of(plan, BuildingUse.Hospital).Count);
    }

    /// <summary>Each of them once, and in ascending order, so a lookup is a walk of a handful of numbers.</summary>
    [Fact]
    public void EveryHospitalIsADistinctBuildingInOrder()
    {
        var plan = Towns.Of(Towns.Fixture);
        var roster = BuildingRoster.Of(plan, BuildingUse.Hospital);

        for (var hospital = 1; hospital < roster.Count; hospital++)
        {
            Assert.True(
                roster.BuildingOf(hospital) > roster.BuildingOf(hospital - 1),
                "the roster is not strictly ascending, so a building was read twice");
        }

        foreach (var building in roster.Buildings) Assert.True(roster.Holds(building));
    }

    /// <summary>
    /// <b>A town with a building on it has a hospital</b>, however few buildings it has: a town where
    /// nobody can be delivered is a town the whole slice does nothing on.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(40)]
    public void ATownWithBuildingsHasAtLeastOneHospitalAndNeverMoreThanTheCeiling(int buildings)
    {
        var wanted = HospitalRoster.CountIn(Towns.WithBuildings(buildings), Config);

        Assert.InRange(wanted, 1, Math.Min(Config.Ambulance.MostHospitals, buildings));
    }

    /// <summary>And a map with nothing on it places none, rather than one of nothing.</summary>
    [Fact]
    public void AMapWithNoBuildingsHasNoHospitals()
    {
        var plan = Towns.WithBuildings(0);

        Assert.Equal(0, HospitalRoster.CountIn(plan, Config));
        Assert.Equal(0, BuildingRoster.Of(plan, BuildingUse.Hospital).Count);
    }
}
