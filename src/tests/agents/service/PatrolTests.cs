using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Agents.Service;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Containment;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Service;

/// <summary>
/// The beat asked of a town (SRV-5): that a police car stands on its own station's apron, leaves it, drives
/// somewhere that is not its station, and comes back — with its crew aboard throughout and no priority on
/// the road.
/// </summary>
/// <remarks>
/// <b>What is asserted is the machine and never how far a patrol got</b>, on <see cref="Ambulance"/>'s own
/// argument: how much of a city a beat covers in ten minutes is a reading about that city's traffic and
/// not a claim about this engine (<see cref="Bench.Scenario"/>).
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
public class PatrolTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>Ten minutes: the longest drawn stand, the longest drawn beat, and the drive home after it.</summary>
    const int Ticks = 36_000;

    /// <summary>
    /// Two minutes, which is what the hold below is watched over. <b>A bay is chosen from the first tick a
    /// car looks for one</b>, so a hold that does not bind is broken in the first minute of any town with
    /// traffic in it; the ten minutes above are the beat's own cycle and buy this claim nothing but time.
    /// </summary>
    const int SoakTicks = 7_200;

    /// <summary>SRV-5 and SRV-2: every police car belongs to a station and starts on that station's apron.</summary>
    [Theory]
    [MemberData(nameof(Towns.EveryShippedMap), MemberType = typeof(Towns))]
    public void EveryPoliceCarStandsByOnItsOwnStationsApron(string map)
    {
        using var world = new TownWorld(Towns.Of(map), Config);

        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (world.Cars.Variant[car] != CarCatalog.Shared.Police) continue;

            var station = world.Beat.Station[car];
            Assert.True(world.PoliceStations.Holds(station), $"{map}: a police car belongs to no station");
            Assert.Equal(PatrolStage.Standing, world.Beat.Stage[car]);

            var bay = world.Beat.HomeBay[car];
            Assert.Equal(bay, world.Parking.BayOf(car));
            Assert.Equal(car, world.Parking.HeldFor(bay));
            Assert.InRange(
                world.Beat.RestS[car], Config.Service.RestBetweenBeatsMinS, Config.Service.RestBetweenBeatsMaxS);
        }
    }

    /// <summary>
    /// GEN-4k: <b>every bay of an apron is filled with its own vehicle before the first tick</b>, and the
    /// town holds exactly as many as it stood service vehicles.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A depot's yard slots are held and hold nobody</b> (EVA-2), which is the one exception and is a
    /// hold of a different kind: it names no vehicle, because what stands in it is whichever wreck was
    /// fetched last. They are counted apart rather than skipped, so a yard that quietly stopped being held
    /// would still fail this.
    /// </para>
    /// <para>
    /// <b>Asked of every shipped map because it is answered off a town at rest</b>, which costs the reading
    /// and no driving at all. What the hold comes to once the town is moving is the soak below, and that one
    /// is asked of the maps that have traffic to break it with.
    /// </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(Towns.EveryShippedMap), MemberType = typeof(Towns))]
    public void EveryHeldBayStartsWithTheVehicleItIsHeldFor(string map)
    {
        using var world = new TownWorld(Towns.Of(map), Config);
        var held = 0;
        var yarded = 0;

        for (var bay = 0; bay < world.Parking.BayCount; bay++)
        {
            var holder = world.Parking.HeldFor(bay);
            if (holder == ParkingRegistry.Nobody) continue;

            if (holder == ParkingRegistry.TheYard)
            {
                yarded++;
                Assert.Equal(ParkingRegistry.Nobody, world.Parking.CarInBay(bay));
                continue;
            }

            held++;
            Assert.Equal(holder, world.Parking.CarInBay(bay));
            Assert.True(
                CarCatalog.Shared.IsService(world.Cars.Variant[holder]),
                $"{map}: an apron bay is held for a car that is not a service vehicle");
        }

        // A map with no building has no hospital and no station, so it holds nothing and is vacuously fine.
        Assert.Equal(world.Ambulances + world.PoliceCars + world.Evacuators, held);
        Assert.True(yarded <= world.Depots.Count * Config.Evacuator.YardSlots, $"{map}: more yard slots than depots ask for");
    }

    /// <summary>
    /// GEN-4k driven: <b>an apron is held for its own vehicles and for nobody else</b>. Nothing but the car
    /// a bay is held for may be standing in it or on its way to it, however long the town runs.
    /// </summary>
    [Theory]
    [MemberData(nameof(Towns.EveryMapWorthASoak), MemberType = typeof(Towns))]
    public void NoOtherCarEverTakesABayHeldForAServiceVehicle(string map)
    {
        using var world = new TownWorld(Towns.Of(map), Config);
        var loop = new SimLoop<TownWorld>(world, Config);

        for (var tick = 0; tick < SoakTicks; tick++)
        {
            loop.Advance(1);
            for (var bay = 0; bay < world.Parking.BayCount; bay++)
            {
                var holder = world.Parking.HeldFor(bay);
                if (holder == ParkingRegistry.Nobody) continue;

                Assert.False(world.Parking.IsFree(bay), $"{map}: a held bay reads free to the town");
                if (holder == ParkingRegistry.TheYard) continue;

                var standing = world.Parking.CarInBay(bay);
                Assert.True(
                    standing < 0 || standing == holder,
                    $"{map}: at tick {tick} car {standing} is standing in a bay held for car {holder}");
            }
        }
    }

    /// <summary>
    /// GEN-4k: <b>an apron stands along one kerb</b>. Every bay of one building's apron is on the same side
    /// of the road as every other, so a station's cars are a station's yard rather than two halves of a
    /// street.
    /// </summary>
    /// <remarks>
    /// <b>What this guards is the loop and not the arithmetic.</b> It asks the town the same question the
    /// seam asked when it laid the apron, so it cannot catch a wrong answer to that question — what it
    /// catches is an apron laid without asking it, which is the way this has already been got wrong once:
    /// the bays were taken nearest-first and landed on both kerbs.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Towns.EveryShippedMap), MemberType = typeof(Towns))]
    public void EveryApronStandsAlongOneKerb(string map)
    {
        using var world = new TownWorld(Towns.Of(map), Config);
        var aprons = new Dictionary<int, List<Vector2>>();

        for (var car = 0; car < world.Cars.Count; car++)
        {
            var home = world.Cars.Ambulance[car] ? world.Duty.HomeBay[car] : world.Beat.HomeBay[car];
            var building = world.Cars.Ambulance[car] ? world.Duty.Hospital[car] : world.Beat.Station[car];
            if (home < 0 || building < 0) continue;

            if (!aprons.TryGetValue(building, out var bays)) aprons[building] = bays = [];
            bays.Add(world.Parking.CentreM(home));
        }

        foreach (var (building, bays) in aprons)
        {
            foreach (var bayM in bays)
            {
                Assert.True(
                    world.StandOnTheSameSideOfTheRoad(bayM, bays[0]),
                    $"{map}: building {building} has apron bays on both sides of its road");
            }
        }
    }

    /// <summary>
    /// SRV-5: a beat is stood out of, driven, and come back from — <b>with no priority anywhere in it</b>,
    /// and the driver at the wheel throughout (SRV-3).
    /// </summary>
    /// <remarks>
    /// <b>Asked of a beat and never of a call.</b> A police car sent to a scene carries the priority for
    /// that one leg and its officer gets out to hold the road (SRV-6), so both claims here are made of the
    /// three stages a beat is made of and of nothing else — asked of every stage alike, this would be
    /// asserting that SRV-6 does not happen.
    /// </remarks>
    [Fact]
    public void APoliceCarLeavesItsStationOnABeatAndKeepsItsCrew()
    {
        using var world = new TownWorld(Towns.Of(Towns.Fixture), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        Assert.True(world.PoliceCars > 0, "the fixture map stood no police car, so nothing here is being asked");

        var patrolled = new bool[world.Cars.Count];
        var setOut = false;
        var cameBack = false;
        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance(1);
            for (var car = 0; car < world.Cars.Count; car++)
            {
                if (world.Cars.Variant[car] != CarCatalog.Shared.Police) continue;

                // <b>Either half of finishing counts</b>: the drive home, and standing by at the end of it.
                // A car whose station has no apron to come back to is home the moment its places run out,
                // so the returning stage is one the town can pass through inside a single call.
                patrolled[car] |= world.Beat.Stage[car] == PatrolStage.Patrolling;
                setOut |= patrolled[car];
                cameBack |= world.Beat.Stage[car] == PatrolStage.ReturningToStation
                            || (patrolled[car] && world.Beat.Stage[car] == PatrolStage.Standing);

                // A wrecked car is not a service vehicle any more: PHY-6 puts its driver out like anybody
                // else's, and SRV-3's crew is about a vehicle that still works.
                if (world.Cars.Broken[car]) continue;

                // On a call and not on a beat: SRV-6's own leg, and none of what is asked below is asked
                // of it — the light is on and the officer is out in the street on purpose.
                if (world.Beat.IsOnACall(car)) continue;

                // A patrol is ordinary traffic (SRV-5): the light is the errand's, and a beat is not one.
                Assert.False(world.Cars.BlueLight[car], "a police car drove a beat with a blue light on");

                var crew = world.Containment.DriverOf(car);
                Assert.True(crew >= 0, "a police car drove a beat with nobody at the wheel (SRV-3)");
                Assert.Equal(TripStage.OnDuty, world.People.Stage[crew]);
                Assert.Equal(ContainerKind.Car, world.People.Inside[crew].Kind);
            }
        }

        Assert.True(setOut, "no police car ever set out on a beat");
        Assert.True(cameBack, "no police car ever finished a beat and stood by again");
    }
}
