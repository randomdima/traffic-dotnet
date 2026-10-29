using System.Numerics;
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
    /// <b>A walker put down in the carriageway walks straight back onto its way over a claim on the stretch of it it steps
    /// onto</b> (PER-25, the root decision log's 2026-10-13 ruling): where it lands is ground its own plan holds before it
    /// is walked — or, refused, ground it waits for — and the lane it crosses is the world's to read its body on.
    /// </summary>
    [Fact]
    public void AWalkerInTheRoadClaimsTheStretchOfItsWayItStepsBackOnto()
    {
        using var world = new TownWorld(Towns.Built, Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(WarmUpTicks);

        var (walker, way) = PutAWalkerInTheRoad(loop);

        // The world reads where the body was put, and the walker learns it is in the road from that (TER-4c.2).
        world.LayTheClaims();
        Assert.Equal(PersonAction.Rejoin, world.People.Action[walker]);

        var key = LaneOccupancy.KeyOf(world.GroundHeldAs(walker, out var roster), roster);
        var claimedItsWay = false;
        for (var tick = 0; tick < WalkBackTicks && world.People.Action[walker] == PersonAction.Rejoin; tick++)
        {
            loop.Advance(1);
            var hold = world.WalkHold(walker);
            if (!world.TurnsTakenThisTick || hold == LaneOccupancy.NoHold) continue;

            foreach (var claim in world.Occupancy.Mains(way)) claimedItsWay |= world.Occupancy.HoldersOf(claim)[0] == key;
            claimedItsWay |= world.Occupancy.CutOf(hold).Way == way;
        }

        Assert.NotEqual(PersonAction.Rejoin, world.People.Action[walker]);
        Assert.True(claimedItsWay, $"walker {walker} walked back onto way {way} without ever claiming it or being refused it");
    }

    /// <summary>How long a walker a few metres out in the road is given to walk back onto its way.</summary>
    const int WalkBackTicks = 600;

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

                if (world.WalkHold(person) == LaneOccupancy.NoHold)
                {
                    Assert.Fail(
                        $"at tick {tick}, walker {person} ({people.Action[person]}) was granted "
                        + $"{people.GrantM[person]:F2} m and holds nothing");
                }
            }
        }
    }

    /// <summary>
    /// A walker out on a stretch of its route, lifted into the middle of the street lane nearest it and left walking —
    /// and the way it was walking.
    /// </summary>
    /// <remarks>
    /// <b>Never one whose decision comes round on the next tick</b>: a walker deciding there lays its walk again from
    /// where it stands, which is the other way back onto the network (PER-25), and walks it for that tick.
    /// </remarks>
    static (int Walker, int Way) PutAWalkerInTheRoad(SimLoop<TownWorld> loop)
    {
        var world = loop.World;
        var people = world.People;
        var zebras = Crossings.Lay(Towns.Built, Config, Towns.Built.Paving(Config).RoadEnds(Config).CrossedM);
        for (var walker = 0; walker < people.Count; walker++)
        {
            if (people.Action[walker] != PersonAction.Walk || people.OnWay[walker] < 0) continue;
            if (loop.Decisions.Turn(loop.Tick, world.Roster.AgentOfPerson(walker))) continue;
            if (world.IsTheCrossing(people.OnWay[walker])) continue;

            var lane = world.Roads.NearestStreetLane(people.PositionM[walker], out var alongM);
            if (lane < 0) continue;

            var at = Spline.SampleAt(world.Roads.ArcsOf(lane), alongM);
            if ((at.PositionM - people.PositionM[walker]).Length() > NearALaneM) continue;
            if (OnAZebra(zebras, at.PositionM, people.RadiusM[walker])) continue;

            var way = people.OnWay[walker];
            world.StandTheWalkerAt(walker, at.PositionM, people.HeadingRad[walker]);
            return (walker, way);
        }

        throw new InvalidOperationException("no walker out on the pavement beside a street lane to put in the road");
    }

    /// <summary>
    /// Whether a body put down at a place would stand on a zebra's band, which is walked ground and not the road
    /// (TER-6): a zebra starts at the kerb end, so the street lane beside a walk near a junction runs under one.
    /// </summary>
    static bool OnAZebra(Crossings zebras, Vector2 pointM, float radiusM)
    {
        for (var zebra = 0; zebra < zebras.Count; zebra++)
        {
            var along = Vector2.Normalize(zebras.Axis[zebra]);
            var offM = pointM - zebras.CentreM[zebra];
            if (MathF.Abs(Vector2.Dot(offM, along)) <= (zebras.DepthM[zebra] * 0.5f) + radiusM
                && MathF.Abs(Vector2.Dot(offM, new Vector2(-along.Y, along.X))) <= (zebras.SpanM[zebra] * 0.5f) + radiusM)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>How near a walker's pavement has to be to a lane for the lane to be the street beside it.</summary>
    const float NearALaneM = 8f;
}
