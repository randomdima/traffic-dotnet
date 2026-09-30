using TrafficSimulation.Agents.Ambulance;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Evacuator;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Agents.Service;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Containment;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Service;

/// <summary>
/// What a town with buildings stands (SRV-2, SRV-3, SRV-5, SRV-7, SRV-11): an apron of ambulances at each of its
/// hospitals, of police cars at each of its stations and of evacuators at each of its depots, parked, wearing a
/// service variant and with their crews aboard — and half of each apron out on its district's beat.
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
    public void EveryServiceVehicleStandsAtItsBuildingWithItsCrewAboard()
    {
        using var world = new TownWorld(Towns.Built, Config);

        var stood = 0;
        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (BuildingOf(world, car) < 0) continue;

            stood++;
            var variant = world.Cars.Variant[car];
            var police = world.Beat.Station[car] != PatrolDuty.NoBuilding;
            Assert.Equal(
                police ? CarCatalog.Shared.Police
                : world.Cars.Ambulance[car] ? CarCatalog.Shared.Ambulance
                : CarCatalog.Shared.Evacuator,
                variant);

            // SRV-3: its crew in its crew seats and nobody at the wheel — what drives a service vehicle is its
            // errand — and a police car's officer the first of them (SRV-11).
            for (var seat = 0; seat < Config.Service.CrewPerVehicle; seat++)
            {
                var crew = world.Containment.CrewOf(car, seat);
                Assert.True(crew >= 0, $"service vehicle {car} was stood with nobody in crew seat {seat}");
                Assert.Equal(TripStage.OnDuty, world.People.Stage[crew]);
            }

            if (police) Assert.Equal(world.Beat.Officer[car], world.Containment.CrewOf(car, 0));

            Assert.False(world.Cars.Driven[car], "a service vehicle is driving before it was given anything to do");
            Assert.True(world.Parking.BayOf(car) >= 0, "a service vehicle did not start in a bay");
            Assert.True(world.Containment.DriverOf(car) < 0, "a service vehicle was stood with somebody at the wheel");
        }

        Assert.True(world.PoliceCars > 0, "the built city stood no police car, which SRV-7 is about");
        Assert.Equal(world.Ambulances + world.PoliceCars + world.Evacuators, stood);
    }

    /// <summary>
    /// SRV-5: <b>half of every building's fleet drives its district's beat and the other half stands</b>, once the
    /// first stands are over — each place a patrol is sent to lying in the district its building stands in.
    /// </summary>
    [Fact]
    public void HalfOfEveryBuildingsFleetPatrolsItsDistrict()
    {
        using var world = new TownWorld(Towns.Built, Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(PastTheFirstBeatTicks);

        var fleet = new Dictionary<int, int>();
        var patrolling = new Dictionary<int, int>();
        for (var car = 0; car < world.Cars.Count; car++)
        {
            var building = BuildingOf(world, car);
            if (building < 0 || world.Cars.Broken[car]) continue;

            var district = world.ServiceBeat.District[car];
            Assert.Equal(world.Plan.Districts.At(world.Plan.Buildings.CentreM[building]), district);

            fleet[building] = fleet.GetValueOrDefault(building) + 1;
            if (!world.ServiceBeat.Patrols[car])
            {
                Assert.False(IsOnTheBeat(world, car), $"service vehicle {car} patrols though it is one that stands");
                continue;
            }

            patrolling[building] = patrolling.GetValueOrDefault(building) + 1;
            Assert.False(IsStanding(world, car), $"service vehicle {car} still stands though it is one that patrols");
            if (IsOnTheBeat(world, car))
            {
                Assert.Equal(district, world.Plan.Districts.At(world.Cars.DestinationM[car]));
            }
        }

        foreach (var (building, all) in fleet)
        {
            Assert.Equal((int)MathF.Round(all * Config.Service.PatrolShare), patrolling.GetValueOrDefault(building));
        }
    }

    /// <summary>A little past the latest a patrol first sets out, and a decision after it.</summary>
    static readonly int PastTheFirstBeatTicks =
        (int)((Config.Service.FirstBeatAfterMaxS + DecisionAfterS) / Config.TickSeconds);

    const float DecisionAfterS = 5f;

    /// <summary>The building a service vehicle stands on the strength of, or −1 for any other car.</summary>
    static int BuildingOf(TownWorld world, int car) =>
        world.Beat.Station[car] != PatrolDuty.NoBuilding ? world.Beat.Station[car]
        : world.Duty.Hospital[car] != RescueDuty.NoBuilding ? world.Duty.Hospital[car]
        : world.Recovery.Depot[car];

    static bool IsStanding(TownWorld world, int car) =>
        world.Beat.Station[car] != PatrolDuty.NoBuilding ? world.Beat.Stage[car] == PatrolStage.Standing
        : world.Cars.Ambulance[car] ? world.Duty.Stage[car] == RescueStage.Waiting
        : world.Recovery.Stage[car] == RecoveryStage.Waiting;

    static bool IsOnTheBeat(TownWorld world, int car) =>
        world.Beat.Station[car] != PatrolDuty.NoBuilding ? world.Beat.Stage[car] == PatrolStage.Patrolling
        : world.Cars.Ambulance[car] ? world.Duty.Stage[car] == RescueStage.Patrolling
        : world.Recovery.Stage[car] == RecoveryStage.Patrolling;

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
            var building = BuildingOf(world, car);
            if (building < 0) continue;

            var withinM = world.Cars.Ambulance[car] ? Config.AmbulanceHomeM : Config.ServiceHomeM;
            var standingM = world.Parking.CentreM(world.Parking.BayOf(car));
            Assert.True(
                (world.Plan.Buildings.CentreM[building] - standingM).Length() <= withinM,
                $"service vehicle {car} stands further from its own building than a walk");
        }
    }
}
