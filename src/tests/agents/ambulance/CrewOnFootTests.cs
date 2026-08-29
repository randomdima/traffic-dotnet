using System.Numerics;
using TrafficSimulation.Agents.Ambulance;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Containment;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Ambulance;

/// <summary>
/// <b>AMB-10 on a town</b>: the ambulance stops back from the accident, the paramedic walks the rest, tugs
/// the casualty to the vehicle and gets back in — and the casualty is aboard at the end of it.
/// </summary>
/// <remarks>
/// <b>The whole point of the change is a distance</b>, so a distance is what is asserted: the vehicle is
/// never within the crew's own reach of the body it is fetching, and somebody covers that gap on foot.
/// Asserting only that a rescue still delivers would pass unchanged if the crew had never got out.
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
public class CrewOnFootTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    const int WarmupTicks = 600;

    const int Ticks = 36_000;

    /// <summary>
    /// The fixture town's staged rescue, watched for the four things AMB-10 added: the vehicle stopping
    /// short, a paramedic out on the pavement, the body moving with them, and the seat taken back.
    /// </summary>
    [Fact]
    public void TheParamedicWalksToTheCasualtyAndTugsThemBackToTheAmbulance()
    {
        using var world = new TownWorld(Towns.Of(Towns.Fixture), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        Assert.True(world.Ambulances > 0, "the fixture map stood no ambulance, so nothing here is being asked");

        loop.Advance(WarmupTicks);
        var casualty = Towns.NearestWalkerToARoad(world);
        Assert.True(casualty >= 0, "the fixture town had nobody to knock down");
        world.Apply(new BodyTag(BodyKind.Person, casualty), DamageOutcome.Wounded);

        var wentOut = false;
        var tugged = false;
        var cameBack = false;
        var stoodOff = float.PositiveInfinity;
        var wasTuggedFromM = Vector2.Zero;
        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance(1);
            world.RebuildProximityIndex();

            for (var car = 0; car < world.Cars.Count; car++)
            {
                if (!world.Cars.Ambulance[car] || !world.Duty.IsOnACall(car)) continue;

                var stage = world.Duty.Stage[car];
                var hand = world.HandOutOf(car);
                if (hand >= 0)
                {
                    wentOut = true;
                    Assert.Equal(TripStage.Attending, world.People.Stage[hand]);

                    // <b>The vehicle is never where the crew is</b> (AMB-10): the standoff is the whole
                    // change, and a crew on foot with the ambulance already on the body would be the old
                    // rescue with a walk bolted to it.
                    if (stage == RescueStage.Fetching && world.People.Wounded[casualty])
                    {
                        stoodOff = MathF.Min(
                            stoodOff,
                            (world.Cars.PositionM[car] - world.People.PositionM[casualty]).Length());
                    }
                }

                if (stage == RescueStage.Tugging && world.People.Wounded[casualty])
                {
                    if (wasTuggedFromM != Vector2.Zero)
                    {
                        tugged |= (world.People.PositionM[casualty] - wasTuggedFromM).Length() > 1f;
                    }

                    wasTuggedFromM = world.People.PositionM[casualty];
                }

                // Back in a seat with the casualty aboard, which is the state the delivery is laid from.
                cameBack |= stage is RescueStage.Carrying or RescueStage.HandingOver && hand < 0;
            }

            if (world.CasualtiesDelivered > 0) break;
        }

        Assert.True(wentOut, "no paramedic ever got out of an ambulance (AMB-10)");
        Assert.True(tugged, "the casualty never moved while being tugged to the vehicle (AMB-10)");
        Assert.True(cameBack, "no ambulance ever drove off with its crew back aboard (SRV-3)");
        Assert.True(
            world.CasualtiesDelivered > 0,
            $"the crew worked the scene on foot and nobody was delivered — {world.CasualtiesCollected} collected");

        Assert.True(
            stoodOff > Config.Service.CrewReachM,
            $"an ambulance stood {stoodOff:F1} m from the body its crew was walking to, which is inside "
            + $"the {Config.Service.CrewReachM:F1} m they could have reached from the cab (AMB-10)");
    }

    /// <summary>
    /// <b>SRV-3, PER-4</b>: an ambulance standing at a scene with its crew out is a car nobody is in, and
    /// nobody in the town may take it. What keeps it out of everybody else's trip is the hospital it stands
    /// on the strength of, and never who happens to be sitting in it.
    /// </summary>
    [Fact]
    public void NobodyWalksOffWithAnUnattendedServiceVehicle()
    {
        using var world = new TownWorld(Towns.Of(Towns.Fixture), Config);
        var loop = new SimLoop<TownWorld>(world, Config);

        loop.Advance(WarmupTicks);
        var casualty = Towns.NearestWalkerToARoad(world);
        Assert.True(casualty >= 0, "the fixture town had nobody to knock down");
        world.Apply(new BodyTag(BodyKind.Person, casualty), DamageOutcome.Wounded);

        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance(1);
            for (var car = 0; car < world.Cars.Count; car++)
            {
                if (world.HandOutOf(car) < 0 || world.Cars.Broken[car]) continue;

                var driver = world.Containment.DriverOf(car);
                Assert.True(
                    driver < 0 || world.People.Stage[driver] == TripStage.OnDuty,
                    "somebody who is not this vehicle's crew took the wheel of it while it was working");
            }
        }
    }

    /// <summary>
    /// <b>The seats are a register and not a count</b> (SRV-3): a crew seat taken is refused to the next
    /// asker, and giving one up is what makes it free again — the same atomic question CAR-2 asks of the
    /// wheel.
    /// </summary>
    [Fact]
    public void ACrewSeatIsTakenOnceAndGivenBack()
    {
        var inside = new Contained[4];
        var containers = new Containers([1], cars: 1, inside);

        Assert.True(containers.TryTakeACrewSeat(0, 1));
        Assert.Equal(1, containers.CrewOf(0, 0));
        Assert.True(containers.AnybodyAboard(0));

        for (var seat = 1; seat < Containers.CrewSeats; seat++)
        {
            Assert.True(containers.TryTakeACrewSeat(0, 1 + seat));
        }

        Assert.False(containers.TryTakeACrewSeat(0, 3));

        containers.Alight(0, 1);
        Assert.Equal(Containers.NoDriver, containers.CrewOf(0, 0));
        Assert.True(containers.TryTakeACrewSeat(0, 3));
    }
}

/// <summary>
/// <b>No vehicle is left standing with its crew in the street</b> (SRV-3). A hand out is a vehicle stopped,
/// so a hand that never comes back is a hospital, a station or a depot one vehicle short for the rest of the
/// run — which is the one way this whole errand can quietly take a town apart.
/// </summary>
/// <remarks>
/// <para>
/// <b>Watched over a busy run rather than staged.</b> What is being asked about is the tail: the scene
/// nothing clears, the pavement that will not give a body back, the call given up while somebody was out. A
/// staged casualty exercises one of those; a city knocking its own people down exercises all of them, and
/// the ceiling asserted is every bound this can legitimately spend, added up.
/// </para>
/// <para>
/// <b>A class of its own because it is ten minutes of a city and the only claim that needs them.</b> The
/// cases of one class are run one after another, so left beside <see cref="CrewOnFootTests"/>'s staged
/// rescue this one stood the whole slice's answer behind it on a machine with fifteen idle cores.
/// </para>
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
public class CrewRecallTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>Ten minutes: half again the longest a hand may honestly be out for, so a breach has room to show.</summary>
    const int Ticks = 36_000;

    [Fact]
    public void NoHandIsLeftInTheStreetLongerThanEveryBoundTogether()
    {
        using var world = new TownWorld(Towns.Of("Odesa"), Config);
        var loop = new SimLoop<TownWorld>(world, Config);

        // Every leg a hand can be out for, end to end: the longest errand bound any of the three can be
        // holding it for, the recall after that, and the one decision it takes to notice either is spent —
        // every one of these clocks is read on the vehicle's own decision and can only be found over on the
        // decision after it went over. <b>Derived and not written down</b>, so moving a figure moves the
        // ceiling with it rather than turning this into a test of what the figures used to be.
        var ceilingS = MathF.Max(
                           Config.AmbulanceGiveUpS,
                           MathF.Max(Config.EvacuatorGiveUpS, MathF.Max(Config.PatrolGiveUpS, Config.PoliceClosureLifeS)))
                       + Config.ServiceRecallS
                       + Config.Sim.AgentDecisionIntervalS;
        var outS = new float[world.Cars.Count];
        var everOut = false;
        var longestS = 0f;

        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance(1);
            for (var car = 0; car < world.Cars.Count; car++)
            {
                if (world.HandOutOf(car) < 0)
                {
                    outS[car] = 0f;
                    continue;
                }

                everOut = true;
                outS[car] += Config.TickSeconds;
                longestS = MathF.Max(longestS, outS[car]);
            }
        }

        Assert.True(everOut, "no crew was ever out on this run, so it says nothing about getting one back");
        Assert.True(
            longestS <= ceilingS,
            $"a vehicle stood {longestS:F1} s with its crew in the street, against {ceilingS:F1} s of bounds "
            + "— the errand's and the recall's together (SRV-3, AMB-9)");
    }
}
