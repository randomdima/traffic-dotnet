using TrafficSimulation.Agents.Ambulance;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Agents.Service;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Service;

/// <summary>
/// <b>SRV-6 on a town</b>: somebody is knocked down, a police car is called to the scene, its officer gets
/// out and the road round the body is closed — to ordinary traffic and not to the rescue.
/// </summary>
/// <remarks>
/// <b>Watched over a whole run rather than asserted of one tick.</b> Which patrol takes the call and how
/// long it takes to arrive are facts about that town's traffic; what is being asked is that the machine
/// runs at all — a call is taken, an officer stands in the street, and a stretch of lane comes out of the
/// book at the rank SRV-6 says it should.
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
public class ClosureTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>Long enough for a patrol to cross the fixture town twice over.</summary>
    const int Ticks = 24_000;

    /// <summary>
    /// And long enough for a city to knock its own people down, close roads over them and deliver them —
    /// which the rescue probe puts at a couple of minutes a casualty on the maps this is asked of.
    /// </summary>
    const int CityTicks = 30_000;

    const int WarmupTicks = 600;

    /// <summary>
    /// <b>A casualty raises a police call, and the officer works it on foot</b> (SRV-6, SRV-3): the car is
    /// stopped short of the scene, the officer is out of it and standing in the street, and the lane the
    /// body lies on is held closed by him.
    /// </summary>
    [Fact]
    public void ACasualtyBringsAPoliceCarWhoseOfficerClosesTheRoad()
    {
        using var world = new TownWorld(Towns.Of(Towns.Fixture), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        Assert.True(world.PoliceCars > 0, "the fixture map stood no police car, so nothing here is being asked");

        loop.Advance(WarmupTicks);
        var casualty = Towns.NearestWalkerToARoad(world);
        Assert.True(casualty >= 0, "the fixture town had nobody to knock down");
        world.Apply(new BodyTag(BodyKind.Person, casualty), DamageOutcome.Wounded);

        var called = false;
        var hurried = false;
        var closed = false;
        var onFoot = false;
        var furthest = PatrolStage.Standing;
        for (var tick = 0; tick < Ticks && !closed; tick++)
        {
            loop.Advance(1);
            world.RebuildProximityIndex();

            for (var car = 0; car < world.Cars.Count; car++)
            {
                if (!world.Beat.IsOnACall(car)) continue;

                called = true;
                if (world.Beat.Stage[car] > furthest) furthest = world.Beat.Stage[car];

                hurried |= world.Beat.Stage[car] == PatrolStage.Attending && world.Cars.BlueLight[car];

                // <b>Out of the vehicle, so out of its crew register</b> (SRV-3): a hand at work is in
                // neither the seat nor the wheel, and the town's own note of who it is is the only way back
                // to the body.
                var officer = world.HandOutOf(car);
                if (officer < 0) continue;

                onFoot |= world.People.Stage[officer] == TripStage.Attending;
                closed |= world.People.ClosesTheRoadM[officer] > 0f && TheRoadIsShutAround(world, officer);
            }
        }

        var got = $"the furthest any call got was {furthest}";
        Assert.True(called, "nobody was ever sent to the scene (SRV-6)");
        Assert.True(hurried, $"a police car drove to a scene without the priority that leg carries — {got}");
        Assert.True(onFoot, $"an officer answered a call without getting out of the car (SRV-3) — {got}");
        Assert.True(closed, $"an officer reached a scene and never closed the road (SRV-6) — {got}");
    }

    /// <summary>
    /// <b>A town that is closing roads is still a town that collects and delivers</b> (SRV-6, AMB-4). The
    /// rank order is what stops a closure refusing the call it was raised for, and the whole point of
    /// putting a closure below a call is that the two errands answering one scene cannot deadlock.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Asked of a city and not of one staged body.</b> Staged, the two halves race: the fixture town's
    /// ambulance reaches a casualty in about two minutes and the scene stops being one the moment it does,
    /// so whether an officer got there first is a fact about that town's traffic rather than about SRV-6. A
    /// city raises scenes of its own all run and closes roads all over itself, which is the state the claim
    /// is actually about.
    /// </para>
    /// <para>
    /// <b>Both halves in one run.</b> Closures are counted as well as the deliveries, because "the rescue
    /// still works" is not worth asserting on a run where nobody ever shut a road.
    /// </para>
    /// </remarks>
    [Fact]
    public void ATownClosingRoadsStillCollectsAndDelivers()
    {
        using var world = new TownWorld(Towns.Of("Odesa"), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        Assert.True(world.PoliceCars > 0 && world.Ambulances > 0, "the map stood no service to ask this of");

        var closures = 0;
        var closing = new bool[world.Cars.Count];

        // <b>Every claim below is that something happened, so the run ends when all three have.</b> The
        // bound is how long a city can take to raise, close and deliver, and not how long the watch is
        // worth keeping up: past the tick that answers the last of them there is nothing further to see.
        for (var tick = 0; tick < CityTicks; tick++)
        {
            loop.Advance(1);
            for (var car = 0; car < world.Cars.Count; car++)
            {
                var officer = world.HandOutOf(car);
                var holds = officer >= 0 && world.People.ClosesTheRoadM[officer] > 0f;
                if (holds && !closing[car]) closures++;

                closing[car] = holds;
            }

            if (closures > 0 && world.CasualtiesCollected > 0 && world.CasualtiesDelivered > 0) break;
        }

        Assert.True(closures > 0, "no road was ever closed, so this run says nothing about a closure (SRV-6)");
        Assert.True(
            world.CasualtiesCollected > 0,
            $"{closures} roads were closed and no casualty was ever got aboard — a closure that refuses the "
            + "call it was raised for is the one deadlock the rank order exists to prevent (SRV-6, AMB-4)");
        Assert.True(
            world.CasualtiesDelivered > 0,
            $"{closures} roads were closed, {world.CasualtiesCollected} casualties were collected and none "
            + "reached a door (AMB-8)");
    }

    /// <summary>
    /// <b>And every closure ends</b> (SRV-6): its scene stops being one, or its own bound does it. A lane
    /// held out of the town for the rest of a run is the single failure a soft reservation can cause, so the
    /// bound is watched rather than trusted.
    /// </summary>
    [Fact]
    public void NoClosureOutlivesItsOwnBound()
    {
        using var world = new TownWorld(Towns.Of(Towns.Fixture), Config);
        var loop = new SimLoop<TownWorld>(world, Config);

        loop.Advance(WarmupTicks);
        var casualty = Towns.NearestWalkerToARoad(world);
        Assert.True(casualty >= 0, "the fixture town had nobody to knock down");
        world.Apply(new BodyTag(BodyKind.Person, casualty), DamageOutcome.Wounded);

        var longest = 0f;
        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance(1);
            for (var car = 0; car < world.Cars.Count; car++)
            {
                if (!world.Beat.IsOnACall(car)) continue;

                longest = MathF.Max(longest, world.Beat.ClosedForS[car]);
            }
        }

        // The bound plus the interval one decision spans: the clock is read on the patrol's own decision and
        // can only be found spent one decision after it was.
        Assert.True(
            longest <= Config.PoliceClosureLifeS + Config.Ladder.ObstructionWaitS,
            $"a closure stood for {longest:F1} s against a bound of {Config.PoliceClosureLifeS:F1} s (SRV-6)");
    }

    /// <summary>
    /// <b>And what he closes it to is ordinary traffic and not a call</b> (SRV-6) — the two halves read off
    /// the same stretch, so the claim cannot pass by the closure not being there.
    /// </summary>
    static bool TheRoadIsShutAround(TownWorld world, int officer)
    {
        var book = world.Occupancy;
        var slots = new LaneSlot[book.Capacity];
        foreach (var way in book.OccupiedWays)
        {
            var found = 0;
            var count = book.CopyTo(way, slots);
            for (var slot = 0; slot < count; slot++)
            {
                ref readonly var taken = ref slots[slot];
                if (taken.Of != LaneRoster.Walking || taken.Occupant != officer) continue;
                if (taken.Use != LaneUse.Claimed) continue;

                Assert.Equal(RightOfWay.Closed, taken.Right);

                // Ordinary traffic is refused it and a rescue is not, which is the whole of the errand.
                Assert.True(LaneOccupancy.Binds(taken, RightOfWay.Traffic));
                Assert.True(LaneOccupancy.Binds(taken, RightOfWay.OnThePaint));
                Assert.False(LaneOccupancy.Binds(taken, RightOfWay.Emergency));

                // <b>TER-5c.2 said of an officer</b>: a body holds one metre of one way once, so the
                // stretch he closes is the only one of that way he is on.
                found++;
            }

            if (found > 0) return true;
        }

        return false;
    }
}
