using System.Numerics;
using TrafficSimulation.Agents.Ambulance;
using TrafficSimulation.Agents.Service;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Statics;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// GEN-9: <b>where a town's services stand is placed once, when the map is authored</b>, and the two
/// things that placement promises are what is asked here — a service building has somewhere for its
/// vehicles to stand, and the services are spread over the town rather than shuffled into it.
/// </summary>
/// <remarks>
/// The placement is asked of a fresh copy of the fixture map, never the shared one: it writes the uses
/// it decides into the plan it is handed.
/// </remarks>
[Trait(Tier.Key, Tier.Unit)]
public class ServicePlacementTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static ServiceApron[] Aprons(CityPlan plan) =>
    [
        HospitalRoster.Apron(plan, Config),
        PoliceStationRoster.Apron(plan, Config),
        DepotRoster.Apron(plan, Config),
    ];

    static CityPlan Placed(string map)
    {
        var plan = Towns.Fresh(map);
        ServicePlacement.Place(plan, plan.Seed, Aprons(plan));
        return plan;
    }

    /// <summary>The same seed places the same services, which is what makes a map a map and not a run.</summary>
    [Fact]
    public void TheSameSeedPlacesTheSameServices()
    {
        Assert.Equal(Placed(Towns.Fixture).Buildings.Use, Placed(Towns.Fixture).Buildings.Use);
    }

    /// <summary>
    /// <b>Placing twice over is placing once</b>: the uses are written from nothing each time, so a map
    /// authored again is not a map whose services have crept.
    /// </summary>
    [Fact]
    public void PlacingAgainPlacesTheSame()
    {
        var plan = Placed(Towns.Fixture);
        var first = plan.Buildings.Use.ToArray();

        ServicePlacement.Place(plan, plan.Seed, Aprons(plan));

        Assert.Equal(first, plan.Buildings.Use);
    }

    /// <summary>
    /// <b>A building with nowhere to park stands nothing</b> (SRV-2), so it is never given a use: an
    /// apron that finds no bay is a hospital with no ambulances at it.
    /// </summary>
    [Theory]
    [InlineData("Test")]
    [InlineData("Odesa")]
    public void EveryServiceBuildingHasSomewhereItsVehiclesCanStand(string map)
    {
        var plan = Placed(map);
        var withinM = new Dictionary<BuildingUse, float>
        {
            [BuildingUse.Hospital] = Config.AmbulanceHomeM,
            [BuildingUse.PoliceStation] = Config.ServiceHomeM,
            [BuildingUse.Depot] = Config.ServiceHomeM,
        };

        for (var building = 0; building < plan.Buildings.Count; building++)
        {
            var use = plan.Buildings.Use[building];
            if (use == BuildingUse.Ordinary) continue;

            Assert.True(
                NearestSpaceM(plan, plan.Buildings.CentreM[building]) <= withinM[use],
                $"{map}: a {use} was placed on a building with no parking within reach of it");
        }
    }

    /// <summary>
    /// <b>Spread over the town and not shuffled into it</b>, which is the whole reason the placement is
    /// an authoring step. Measured against a shuffle of the same size on the same map: the services must
    /// stand further from each other than one drawn at random does.
    /// </summary>
    [Fact]
    public void TheServicesStandFurtherApartThanAShuffleWouldPutThem()
    {
        var plan = Placed("Odesa");
        var placedM = new List<Vector2>();
        for (var building = 0; building < plan.Buildings.Count; building++)
        {
            if (plan.Buildings.Use[building] != BuildingUse.Ordinary) placedM.Add(plan.Buildings.CentreM[building]);
        }

        var shuffledM = new List<Vector2>();
        var stride = plan.Buildings.Count / placedM.Count;
        for (var slot = 0; slot < placedM.Count; slot++) shuffledM.Add(plan.Buildings.CentreM[slot * stride]);

        Assert.True(
            ClosestPairM(placedM) > ClosestPairM(shuffledM),
            "the placement put two services closer together than an even walk of the building list does");
    }

    /// <summary>How near the nearest two of them stand, which is what a clump is.</summary>
    static float ClosestPairM(List<Vector2> placedM)
    {
        var closestM = float.MaxValue;
        for (var one = 0; one < placedM.Count; one++)
        {
            for (var two = one + 1; two < placedM.Count; two++)
            {
                closestM = MathF.Min(closestM, (placedM[one] - placedM[two]).Length());
            }
        }

        return closestM;
    }

    static float NearestSpaceM(CityPlan plan, Vector2 centreM)
    {
        var nearestM = float.MaxValue;
        foreach (var spaceM in plan.ParkingLots.SpacePositionM)
        {
            nearestM = MathF.Min(nearestM, (spaceM - centreM).Length());
        }

        return nearestM;
    }
}
