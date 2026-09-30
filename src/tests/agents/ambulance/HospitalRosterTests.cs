using TrafficSimulation.CityGen;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Statics;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Ambulance;

/// <summary>
/// AMB-1: <b>which buildings are hospitals is a fact about the map</b>, declared in the file, so it is
/// the same every time a map is opened.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P3)]
public class HospitalRosterTests
{
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
}
