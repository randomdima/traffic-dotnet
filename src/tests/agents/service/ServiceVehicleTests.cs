using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Evacuator;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Agents.Service;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Containment;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Service;

/// <summary>
/// What a town with buildings stands (SRV-2, SRV-3, SRV-7, SRV-11): an apron of police cars at each of its
/// stations, each with its officer aboard, and an evacuator at each of its depots, parked and wearing a service
/// variant.
/// </summary>
/// <remarks>
/// <para>
/// <b>Asked of <see cref="Towns.Built"/></b>, the one town the suite lays with buildings on it and therefore the
/// one with stations, depots and the yards cut for them (GEN-55).
/// </para>
/// <para>
/// <b>A service vehicle is one with a building</b> and never one recognised by its paint. What makes a car
/// a patrol is its station (<c>TownWorld.Beat</c>) and what makes one a recovery is its depot
/// (<c>TownWorld.Recovery</c>); the paint is what that car then wears, and is asserted here rather than
/// used to find it — a map may dress its own cars in a look (<see cref="CityGen.IdlePlan"/>), and a look
/// is not a duty.
/// </para>
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P2)]
public class ServiceVehicleTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    [Fact]
    public void EveryServiceVehicleStandsAtItsBuildingAndEveryPoliceCarCarriesItsOfficer()
    {
        using var world = new TownWorld(Towns.Built, Config);

        var police = 0;
        var evacuators = 0;
        for (var car = 0; car < world.Cars.Count; car++)
        {
            var variant = world.Cars.Variant[car];
            var officer = world.Beat.Officer[car];
            if (world.Beat.Station[car] != PatrolDuty.NoBuilding)
            {
                police++;
                Assert.Equal(CarCatalog.Shared.Police, variant);

                // SRV-11: its officer, in a crew seat and not at the wheel — what drives a service vehicle is its
                // errand (SRV-3).
                Assert.True(officer >= 0, "a police car was stood with no officer");
                Assert.Equal(officer, world.Containment.CrewOf(car, 0));
                Assert.Equal(TripStage.OnDuty, world.People.Stage[officer]);
            }
            else if (world.Recovery.Depot[car] != RecoveryDuty.NoBuilding)
            {
                evacuators++;
                Assert.Equal(CarCatalog.Shared.Evacuator, variant);
                Assert.True(world.Containment.CrewOf(car, 0) < 0, "an evacuator was stood with a crew aboard");
            }
            else
            {
                continue;
            }

            Assert.False(world.Cars.Ambulance[car], "a service vehicle was stood as an ambulance");
            Assert.False(world.Cars.Driven[car], "a service vehicle is driving before it was given anything to do");
            Assert.True(world.Parking.BayOf(car) >= 0, "a service vehicle did not start in a bay");
            Assert.True(world.Containment.DriverOf(car) < 0, "a service vehicle was stood with somebody at the wheel");
        }

        Assert.True(police > 0, "the built city stood no police car, which SRV-7 is about");
        Assert.Equal(world.PoliceCars, police);
        Assert.Equal(world.Evacuators, evacuators);
        Assert.True(
            police <= world.PoliceStations.Count * Config.Service.ApronBays,
            "more police cars than the stations have apron bays");
        Assert.True(evacuators <= world.Depots.Count, "more evacuators than depots");
    }

    /// <summary>
    /// SRV-3a: a crew wears its own service's uniform, and <b>nobody else in the town is wearing one</b> —
    /// asserted over every walker rather than over the crews, because the fact worth checking is the one
    /// about the wrap a spawned walker's look comes off.
    /// </summary>
    [Fact]
    public void EveryCrewWearsItsOwnUniformAndNobodyElseWearsOne()
    {
        using var world = new TownWorld(Towns.Built, Config);
        var looks = PersonCatalog.Shared;

        // <b>Read off the vehicles and not off who is sitting in one</b> (SRV-3): a crew rides in a crew seat, and
        // asked of the wheel alone every officer in the town reads as a walker somebody handed a uniform to.
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
                Assert.True(wearing < looks.Count, "a walker was handed a service uniform");
                continue;
            }

            Assert.Equal(looks.Variants[expected[person]].Id, looks.Variants[wearing].Id);
        }
    }

    const int NoUniform = -1;

    /// <summary>
    /// SRV-2: <b>a building with no bay near it stands nothing</b>, and what does stand is within a walk of
    /// the building it belongs to — for a police car, its station's yard (SRV-7).
    /// </summary>
    [Fact]
    public void NoServiceVehicleStandsFurtherFromItsBuildingThanAWalk()
    {
        using var world = new TownWorld(Towns.Built, Config);

        for (var car = 0; car < world.Cars.Count; car++)
        {
            var patrol = world.Beat.Station[car] != PatrolDuty.NoBuilding;
            if (!patrol && world.Recovery.Depot[car] == RecoveryDuty.NoBuilding) continue;

            var building = patrol ? world.Beat.Station[car] : world.Recovery.Depot[car];
            var standingM = world.Parking.CentreM(world.Parking.BayOf(car));
            Assert.True(
                (world.Plan.Buildings.CentreM[building] - standingM).Length() <= Config.ServiceHomeM,
                $"service vehicle {car} stands further from its own building than a walk");
        }
    }
}
