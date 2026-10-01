using TrafficSimulation.Agents.Person.Actions;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Person.Actions;

/// <summary>
/// <b>Somebody on foot walks over no ground their action has not claimed</b> (PER-25b, TER-4c.8), asked of the suite's
/// own city.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P1)]
public class PersonActionTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>Long enough for the town's first trips to be out on the pavement.</summary>
    const int WarmUpTicks = 600;

    /// <summary>A minute, which is walkers setting off, crossing, standing aside and arriving.</summary>
    const int WatchedTicks = 3_600;

    /// <summary>
    /// <b>A walker put down in the carriageway walks back onto the pavement over a claim on the lane it stands in</b>
    /// (PER-25): it is getting back onto its way, and the straight there is ground its own plan holds before it is
    /// walked — or, refused, ground it waits for.
    /// </summary>
    [Fact]
    public void AWalkerInTheRoadClaimsTheLaneItWalksBackOver()
    {
        using var world = new TownWorld(Towns.Built, Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(WarmUpTicks);

        var (walker, lane) = PutAWalkerInTheRoad(loop);
        loop.Advance(1);

        Assert.Equal(PersonAction.Rejoin, world.People.Action[walker]);

        var hold = world.WalkHold(walker);
        Assert.True(hold != LaneOccupancy.NoHold, $"walker {walker} walking back off lane {lane} laid no hold");

        var way = world.Ways.OfRoadLane(lane);
        Span<LaneClaim> claims = stackalloc LaneClaim[MostOnAWay];
        var count = world.Occupancy.CopyPlannedTo(way, claims);
        var holdsIt = false;
        for (var at = 0; at < count; at++) holdsIt |= claims[at].Hold == hold && !claims[at].Secondary;

        Assert.True(
            holdsIt || world.Occupancy.HoldCutOn(hold) == way,
            $"walker {walker} walking back off lane {lane} neither holds it nor was refused it");
    }

    /// <summary>
    /// <b>No walker is granted ground its action has not claimed</b>: a grant is what survives of the hold its walk
    /// laid — and nothing where it laid none, standing, inside, down or under a hand.
    /// </summary>
    [Fact]
    public void NoWalkerIsGrantedGroundItsActionHasNotClaimed()
    {
        using var world = new TownWorld(Towns.Built, Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        var people = world.People;

        for (var tick = 0; tick < WatchedTicks; tick++)
        {
            loop.Advance();
            for (var person = 0; person < people.Count; person++)
            {
                if (people.GrantM[person] <= 0f) continue;

                Assert.True(
                    world.WalkHold(person) != LaneOccupancy.NoHold,
                    $"at tick {tick}, walker {person} ({people.Action[person]}) was granted "
                    + $"{people.GrantM[person]:F2} m and holds nothing");
            }
        }
    }

    /// <summary>
    /// A walker out on a stretch of its route, lifted into the middle of the street lane nearest it and left walking —
    /// and that lane.
    /// </summary>
    /// <remarks>
    /// <b>Never one whose decision comes round on the next tick</b>: a walker deciding there lays its walk again from
    /// where it stands, which is the other way back onto the network (PER-25), and walks it for that tick.
    /// </remarks>
    static (int Walker, int Lane) PutAWalkerInTheRoad(SimLoop<TownWorld> loop)
    {
        var world = loop.World;
        var people = world.People;
        for (var walker = 0; walker < people.Count; walker++)
        {
            if (people.Action[walker] != PersonAction.Walk || people.OnWay[walker] < 0) continue;
            if (loop.Decisions.Turn(loop.Tick, world.Roster.AgentOfPerson(walker))) continue;
            if (world.IsTheCrossing(people.OnWay[walker])) continue;

            var lane = world.Roads.NearestStreetLane(people.PositionM[walker], out var alongM);
            if (lane < 0) continue;

            var at = Spline.SampleAt(world.Roads.ArcsOf(lane), alongM);
            if ((at.PositionM - people.PositionM[walker]).Length() > NearALaneM) continue;

            people.PositionM[walker] = at.PositionM;
            world.PhysicsForInstruments.Release(people.Body[walker], at.PositionM, people.HeadingRad[walker]);
            return (walker, lane);
        }

        throw new InvalidOperationException("no walker out on the pavement beside a street lane to put in the road");
    }

    /// <summary>How near a walker's pavement has to be to a lane for the lane to be the street beside it.</summary>
    const float NearALaneM = 8f;

    /// <summary>How many reservations one way may hold before the reading is a bound rather than the way.</summary>
    const int MostOnAWay = 128;
}
