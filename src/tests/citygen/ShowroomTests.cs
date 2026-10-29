using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// The showroom (<see cref="ShowroomPlan"/>): the map the art is judged on parks every car look once, the service
/// vehicles' with the fleet's, and stands every building look once round them — so a look missing from it or shown
/// twice is a look the eye either never gets to judge or judges as two.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P9)]
public class ShowroomTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    [Fact]
    public void EveryLookStandsOnceAndNothingElseDoes()
    {
        using var world = new TownWorld(Towns.Of(ShowroomPlan.Name), Config);

        var looks = CarCatalog.Shared.SheetCount;
        Assert.InRange(looks, 1, ShowroomPlan.Slots);
        Assert.Equal(looks, world.Cars.Count);

        var worn = new bool[looks];
        for (var car = 0; car < world.Cars.Count; car++)
        {
            var look = world.Cars.Variant[car];
            Assert.False(worn[look], $"{CarCatalog.Shared.Variants[look].Id} stands twice");
            worn[look] = true;
        }
    }

    /// <summary><b>Every prop look stands once</b>, in the field between the yard and the buildings.</summary>
    [Fact]
    public void EveryPropLookStandsOnce()
    {
        var props = Towns.Of(ShowroomPlan.Name).Props;

        var looks = ShippedArt.Sizes.Props.Count;
        Assert.True(looks > 0, "the art holds no prop look");
        Assert.Equal(looks, props.Count);

        var worn = new bool[looks];
        foreach (var look in props.Look)
        {
            Assert.False(worn[look], $"prop look {look} stands twice");
            worn[look] = true;
        }
    }

    /// <summary><b>Every person look stands once</b> (PER-30), the uniforms with the walkers.</summary>
    [Fact]
    public void EveryPersonLookStandsOnce()
    {
        using var world = new TownWorld(Towns.Of(ShowroomPlan.Name), Config);

        var looks = PersonCatalog.Shared.SheetCount;
        Assert.Equal(looks, world.People.Count);

        var worn = new bool[looks];
        for (var person = 0; person < world.People.Count; person++)
        {
            var look = world.People.Variant[person];
            Assert.False(worn[look], $"{PersonCatalog.Shared.Variants[look].Id} stands twice");
            worn[look] = true;
        }
    }

    /// <summary>
    /// <b>The island inside the loop is walk</b> (<see cref="CityPlan.Plazas"/>): the ground answers sidewalk under every
    /// person look's place, which is the row down the island's middle.
    /// </summary>
    [Fact]
    public void ThePeopleStandOnTheIslandsWalk()
    {
        var plan = Towns.Of(ShowroomPlan.Name);
        var ground = new GroundShapes(plan.Paving(Config), Config);

        for (var spawn = 0; spawn < plan.Spawns.Count; spawn++)
        {
            if (plan.Spawns.Kind[spawn] != IdlePlan.SpawnKindPerson) continue;

            Assert.Equal(Ground.Sidewalk, ground.At(plan.Spawns.PositionM[spawn]));
        }
    }

    /// <summary><b>A car park and not a road</b>: every look starts parked in a bay of its own, nothing driving it.</summary>
    [Fact]
    public void EveryCarStartsParkedInABayOfItsOwn()
    {
        using var world = new TownWorld(Towns.Of(ShowroomPlan.Name), Config);

        var bays = new HashSet<int>();
        for (var car = 0; car < world.Cars.Count; car++)
        {
            Assert.False(world.Cars.Driven[car], $"car {car} is under way");
            Assert.True(bays.Add(world.Parking.BayOf(car)), $"car {car} has no bay of its own");
        }

        Assert.DoesNotContain(ParkingRegistry.NoBay, bays);
    }

    /// <summary>
    /// <b>Every building look stands once</b> — each ordinary roof, each civic one and each prefab — read as the town
    /// draws it (<see cref="BuildingRoofs"/>), so a footprint that matched the wrong roof shows as one look twice.
    /// </summary>
    [Fact]
    public void EveryBuildingLookStandsOnce()
    {
        using var world = new TownWorld(Towns.Of(ShowroomPlan.Name), Config);
        var plan = Towns.Of(ShowroomPlan.Name);
        var catalogue = BuildingCatalog.Shared;

        var worn = new int[catalogue.Count];
        for (var building = 0; building < plan.Buildings.Count; building++)
        {
            worn[BuildingRoofs.Of(plan, catalogue, world.Uses, building).Variant]++;
        }

        for (var look = 0; look < worn.Length; look++)
        {
            Assert.True(worn[look] == 1, $"{catalogue.Variants[look].Id} stands {worn[look]} times");
        }
    }
}
