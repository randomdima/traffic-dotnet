using TrafficSimulation.Agents.Ambulance;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Bench;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Containment;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Ambulance;

/// <summary>
/// The rescue asked of a town: the ambulances a map stands (AMB-2, AMB-3), what a knocked-down walker
/// becomes (PER-18), and that a call is taken and driven at (AMB-4, AMB-5).
/// </summary>
/// <remarks>
/// <b>Whether a rescue completes is a reading and not a bound</b>, and it is <c>--bench rescue</c>'s
/// (<see cref="RescueProbe"/>): a body an ambulance cannot get to through the traffic is a fact about how
/// congested that map is, and asserting it here would be tuning the towns until the instrument could no
/// longer report the thing it was written to find. What is asserted below is the machinery either side of
/// it.
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
public class RescueTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// AMB-2 and AMB-3: every ambulance the town stood is parked on its own hospital's apron, wearing the
    /// service variant, with a crew inside it — and no ordinary car is any of those things.
    /// </summary>
    [Theory]
    [MemberData(nameof(Towns.EveryShippedMap), MemberType = typeof(Towns))]
    public void EveryAmbulanceStandsAtItsHospitalWithACrewAboard(string map)
    {
        using var world = new TownWorld(Towns.Of(map), Config);

        var stood = 0;
        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (!world.Cars.Ambulance[car]) continue;

            stood++;
            var hospital = world.Duty.Hospital[car];
            Assert.True(world.Hospitals.Holds(hospital), $"{map}: an ambulance belongs to no hospital");
            Assert.True(world.Parking.BayOf(car) >= 0, $"{map}: an ambulance did not start in a bay");
            Assert.False(world.Cars.BlueLight[car], $"{map}: an ambulance started with its light on");
            Assert.Equal(RescueStage.Waiting, world.Duty.Stage[car]);

            var crew = world.Containment.DriverOf(car);
            Assert.True(crew >= 0, $"{map}: an ambulance has nobody in it (CAR-1)");
            Assert.Equal(TripStage.OnDuty, world.People.Stage[crew]);
            Assert.Equal(ContainerKind.Car, world.People.Inside[crew].Kind);

            var homeM = world.Plan.Buildings.CentreM[hospital];
            Assert.True(
                (world.Parking.CentreM(world.Parking.BayOf(car)) - homeM).Length() <= Config.AmbulanceHomeM,
                $"{map}: an ambulance stands further from its hospital than a walk");
        }

        Assert.Equal(world.Ambulances, stood);
        Assert.True(
            stood <= world.Hospitals.Count * Config.Service.ApronBays,
            $"{map}: more ambulances than the hospitals have apron bays");
    }

    /// <summary>
    /// PER-18: a contact with a car leaves a casualty — off their feet, taking no actions, holding none of
    /// the trip's claims — and it is <b>not</b> a terminal state, because something is coming for them.
    /// </summary>
    [Fact]
    public void AStruckWalkerBecomesACasualtyRatherThanGettingUp()
    {
        using var world = new TownWorld(Towns.Of(Towns.Fixture), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(600);

        var walker = FirstWalkerInTheTown(world);
        world.Apply(new BodyTag(BodyKind.Person, walker), DamageOutcome.Wounded);

        Assert.True(world.People.Wounded[walker]);
        Assert.False(world.People.IsOnItsFeet(walker));
        Assert.False(world.People.Acts(walker));
        Assert.False(world.People.Walking[walker]);
        Assert.False(world.IsTerminal(world.Roster.AgentOfPerson(walker)));
        Assert.Equal(1, world.CasualtiesRaised);

        // And still down two seconds later: nothing about going down wears off on a clock — only a
        // hospital puts somebody back on their feet.
        loop.Advance(120);
        Assert.True(world.People.Wounded[walker]);
    }

    /// <summary>
    /// AMB-5 and AMB-4: the nearest idle ambulance takes the call, sets off with its light on, and holds
    /// its road at the rank that makes everybody else give way. <b>One casualty to a call</b>: no second
    /// ambulance is sent to the same body.
    /// </summary>
    [Fact]
    public void ACasualtyIsAnsweredByOneAmbulanceWithItsLightOn()
    {
        using var world = new TownWorld(Towns.Of(Towns.Fixture), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(600);
        Assert.True(world.Ambulances > 0, "the fixture map stood no ambulance, so nothing here is being asked");

        var walker = FirstWalkerInTheTown(world);
        world.Apply(new BodyTag(BodyKind.Person, walker), DamageOutcome.Wounded);
        loop.Advance(120);

        var answering = 0;
        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (world.Duty.Casualty[car] != walker) continue;

            answering++;
            Assert.True(world.Cars.Ambulance[car]);
            Assert.Equal(RescueStage.Running, world.Duty.Stage[car]);
            Assert.True(world.Cars.BlueLight[car], "an ambulance on a call is not carrying its priority");
            Assert.True(world.Cars.Driven[car], "an ambulance took a call and did not set off");
        }

        Assert.Equal(1, answering);
    }

    /// <summary>
    /// AMB-3, the other half: an ambulance is never anybody's trip car, because PER-4 asks for a car nobody
    /// is in and this one always has its crew in it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Towns.EveryShippedMap), MemberType = typeof(Towns))]
    public void NobodyEverBoardsAnAmbulance(string map)
    {
        using var world = new TownWorld(Towns.Of(map), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(1_200);

        for (var person = 0; person < world.People.Count; person++)
        {
            var car = world.People.TripCar[person];
            if (car < 0) continue;

            Assert.False(world.Cars.Ambulance[car], $"{map}: somebody's trip claimed an ambulance");
        }
    }

    /// <summary>A walker out in the town on its feet, which is what a staged contact needs.</summary>
    static int FirstWalkerInTheTown(TownWorld world)
    {
        for (var person = 0; person < world.People.Count; person++)
        {
            if (world.People.Inside[person].Any || world.People.Wounded[person]) continue;

            return person;
        }

        return -1;
    }
}
