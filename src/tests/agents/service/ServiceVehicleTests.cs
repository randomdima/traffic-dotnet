using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Containment;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Service;

/// <summary>
/// What every shipped map stands (SRV-2, SRV-3): an apron of police cars at each of its stations and an
/// evacuator at each of its depots, parked, wearing a service variant and with a crew aboard.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class ServiceVehicleTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    [Theory]
    [MemberData(nameof(Towns.EveryShippedMap), MemberType = typeof(Towns))]
    public void EveryServiceVehicleStandsAtItsBuildingWithACrewAboard(string map)
    {
        using var world = new TownWorld(Towns.Of(map), Config);

        var police = 0;
        var evacuators = 0;
        for (var car = 0; car < world.Cars.Count; car++)
        {
            var variant = world.Cars.Variant[car];
            if (variant == CarCatalog.Shared.Police) police++;
            else if (variant == CarCatalog.Shared.Evacuator) evacuators++;
            else continue;

            Assert.False(world.Cars.Ambulance[car], $"{map}: a service vehicle was stood as an ambulance");
            Assert.False(world.Cars.Driven[car], $"{map}: a service vehicle is driving before it was given anything to do");
            Assert.True(world.Parking.BayOf(car) >= 0, $"{map}: a service vehicle did not start in a bay");

            // SRV-3: a driver who keeps the wheel, and a hand whose whole job is to get out and work the
            // street (SRV-6, EVA-5). Both are aboard before the town has given the vehicle anything to do.
            var driver = world.Containment.DriverOf(car);
            var hand = world.Containment.CrewOf(car, 0);
            Assert.True(driver >= 0, $"{map}: a service vehicle was stood with nobody at the wheel");
            Assert.True(hand >= 0, $"{map}: a service vehicle was stood with no hand to work with");

            foreach (var crew in (ReadOnlySpan<int>)[driver, hand])
            {
                Assert.Equal(TripStage.OnDuty, world.People.Stage[crew]);
                Assert.Equal(ContainerKind.Car, world.People.Inside[crew].Kind);
            }
        }

        Assert.Equal(world.PoliceCars, police);
        Assert.Equal(world.Evacuators, evacuators);
        Assert.True(
            police <= world.PoliceStations.Count * Config.Service.ApronBays,
            $"{map}: more police cars than the stations have apron bays");
        Assert.True(evacuators <= world.Depots.Count, $"{map}: more evacuators than depots");
    }

    /// <summary>
    /// SRV-3a: a crew wears its own service's uniform, and <b>nobody else in the town is wearing one</b> —
    /// asserted over every walker rather than over the crews, because the fact worth checking is the one
    /// about the wrap a spawned walker's look comes off.
    /// </summary>
    [Theory]
    [MemberData(nameof(Towns.EveryShippedMap), MemberType = typeof(Towns))]
    public void EveryCrewWearsItsOwnUniformAndNobodyElseWearsOne(string map)
    {
        using var world = new TownWorld(Towns.Of(map), Config);
        var looks = PersonCatalog.Shared;

        // <b>Read off the vehicles and not off who is sitting in one</b> (SRV-3): a crew is a driver and a
        // hand now, and the hand's seat is not the wheel — asked of the wheel alone, the second body in every
        // ambulance in the town reads as a walker somebody handed a uniform to.
        var expected = new int[world.People.Count];
        Array.Fill(expected, NoUniform);
        for (var car = 0; car < world.Cars.Count; car++)
        {
            var uniform = world.Cars.Variant[car] switch
            {
                var variant when variant == CarCatalog.Shared.Ambulance => looks.Paramedic,
                var variant when variant == CarCatalog.Shared.Police => looks.Police,
                var variant when variant == CarCatalog.Shared.Evacuator => looks.Recovery,
                _ => NoUniform,
            };

            if (uniform == NoUniform) continue;

            var driver = world.Containment.DriverOf(car);
            if (driver >= 0) expected[driver] = uniform;

            for (var seat = 0; seat < Containers.CrewSeats; seat++)
            {
                var crew = world.Containment.CrewOf(car, seat);
                if (crew >= 0) expected[crew] = uniform;
            }
        }

        for (var person = 0; person < world.People.Count; person++)
        {
            var wearing = world.People.Variant[person];
            if (expected[person] == NoUniform)
            {
                Assert.True(wearing < looks.Count, $"{map}: a walker was handed a service uniform");
                continue;
            }

            Assert.Equal(looks.Variants[expected[person]].Id, looks.Variants[wearing].Id);
        }
    }

    const int NoUniform = -1;

    /// <summary>
    /// SRV-2: <b>a building with no bay near it stands nothing</b>, and what does stand is within a walk of
    /// the building it belongs to.
    /// </summary>
    [Theory]
    [MemberData(nameof(Towns.EveryShippedMap), MemberType = typeof(Towns))]
    public void NoServiceVehicleStandsFurtherFromItsBuildingThanAWalk(string map)
    {
        using var world = new TownWorld(Towns.Of(map), Config);

        for (var car = 0; car < world.Cars.Count; car++)
        {
            var variant = world.Cars.Variant[car];
            if (variant != CarCatalog.Shared.Police && variant != CarCatalog.Shared.Evacuator) continue;

            var roster = variant == CarCatalog.Shared.Police ? world.PoliceStations : world.Depots;
            var standingM = world.Parking.CentreM(world.Parking.BayOf(car));

            var near = false;
            foreach (var building in roster.Buildings)
            {
                near |= (world.Plan.Buildings.CentreM[building] - standingM).Length() <= Config.ServiceHomeM;
            }

            Assert.True(near, $"{map}: a service vehicle stands further from its building than a walk");
        }
    }
}
