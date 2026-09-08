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
[Trait(Priority.Key, Priority.P2)]
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
        var staged = Staged.Value;

        Assert.True(staged.WentOut, "no paramedic ever got out of an ambulance (AMB-10)");
        Assert.Null(staged.HandNotAttending);
        Assert.True(staged.Tugged, "the casualty never moved while being tugged to the vehicle (AMB-10)");
        Assert.True(staged.CameBack, "no ambulance ever drove off with its crew back aboard (SRV-3)");
        Assert.True(
            staged.Delivered > 0,
            $"the crew worked the scene on foot and nobody was delivered — {staged.Collected} collected");

        Assert.True(
            staged.StoodOffM > Config.Service.CrewReachM,
            $"an ambulance stood {staged.StoodOffM:F1} m from the body its crew was walking to, which is inside "
            + $"the {Config.Service.CrewReachM:F1} m they could have reached from the cab (AMB-10)");
    }

    /// <summary>
    /// <b>SRV-3, PER-4</b>: an ambulance standing at a scene with its crew out is a car nobody is in, and
    /// nobody in the town may take it. What keeps it out of everybody else's trip is the hospital it stands
    /// on the strength of, and never who happens to be sitting in it.
    /// </summary>
    [Fact]
    public void NobodyWalksOffWithAnUnattendedServiceVehicle() => Assert.Null(Staged.Value.WalkedOffWith);

    /// <summary>
    /// <b>One staged rescue, and every claim about it read off the one run.</b> Both facts above watch the
    /// same ten minutes of the same casualty being fetched: standing a second town to ask the second question
    /// was the whole of what this class cost, and a rescue staged twice is not two rescues.
    /// </summary>
    /// <remarks>
    /// The universal claim — that nobody walks off with a working vehicle — is what fixes the length: it must
    /// watch every tick, so the run does not end at the delivery the existential claims are answered by.
    /// </remarks>
    static readonly Lazy<Watched> Staged = new(Stage);

    /// <summary>What a staged rescue was seen to do, recorded as it went past rather than asserted inside it.</summary>
    sealed record Watched(
        bool WentOut,
        bool Tugged,
        bool CameBack,
        long Delivered,
        long Collected,
        float StoodOffM,
        string? HandNotAttending,
        string? WalkedOffWith);

    static Watched Stage()
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
        var stoodOffM = float.PositiveInfinity;
        var wasTuggedFromM = Vector2.Zero;
        string? handNotAttending = null;
        string? walkedOffWith = null;

        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance(1);
            world.RebuildProximityIndex();

            for (var car = 0; car < world.Cars.Count; car++)
            {
                var hand = world.HandOutOf(car);
                if (hand >= 0 && !world.Cars.Broken[car])
                {
                    var driver = world.Containment.DriverOf(car);
                    if (driver >= 0 && world.People.Stage[driver] != TripStage.OnDuty)
                    {
                        walkedOffWith ??=
                            $"person {driver} took the wheel of car {car} at tick {tick} while it was working";
                    }
                }

                if (!world.Cars.Ambulance[car] || !world.Duty.IsOnACall(car)) continue;

                var stage = world.Duty.Stage[car];
                if (hand >= 0)
                {
                    wentOut = true;
                    if (world.People.Stage[hand] != TripStage.Attending)
                    {
                        handNotAttending ??=
                            $"person {hand} is out of ambulance {car} doing {world.People.Stage[hand]}";
                    }

                    // <b>The vehicle is never where the crew is</b> (AMB-10): the standoff is the whole
                    // change, and a crew on foot with the ambulance already on the body would be the old
                    // rescue with a walk bolted to it.
                    if (stage == RescueStage.Fetching && world.People.Wounded[casualty])
                    {
                        stoodOffM = MathF.Min(
                            stoodOffM,
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
        }

        return new Watched(
            wentOut, tugged, cameBack, world.CasualtiesDelivered, world.CasualtiesCollected, stoodOffM,
            handNotAttending, walkedOffWith);
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
