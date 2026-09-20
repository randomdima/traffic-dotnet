using System.Numerics;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Routing;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Person;

/// <summary>
/// <b>What a walker does with a town</b>: the line it follows (PER-25) and the two claims it lays
/// (PER-26).
/// </summary>
/// <remarks>
/// <b>Asked of <see cref="Towns.Built"/></b>, the one town the suite lays that stands anybody up — a
/// walker begins inside a building (GEN-7) and the fixture has no buildings to begin in.
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P4)]
public class WalkingTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// <b>GEN-7: everybody the plan stands is inside a building before the first tick.</b> A body stood at
    /// a way in walks through it as the town is stood up, so the first thing any walker does is a dwell and
    /// not a leg nothing drew.
    /// </summary>
    [Fact]
    public void EverybodyTheTownStandsBeginsInsideABuilding()
    {
        using var world = Stood(out var plan);

        Assert.Equal(PeopleStood(plan), world.People.Count);
        for (var person = 0; person < world.People.Count; person++)
        {
            Assert.Equal(TripStage.Dwelling, world.People.Stage[person]);
            Assert.True(world.People.Inside[person].Any, $"walker {person} was stood outside every door");
        }
    }

    /// <summary>
    /// <b>PER-26's first claim: a walker out of doors holds the ground it is standing on.</b> Every way its
    /// own box is over carries a stretch of that way under this body's own number, at the one rank nothing
    /// takes (TER-5g, p0) — which is the whole of what stops a driver reaching it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Asked of whether the claim is there and not of which way carries it.</b> Two answers decide
    /// that: the route says which way a body is <em>walking</em> (<see cref="PersonFleet.OnWay"/>) and the
    /// ground walk says which ways its box is <em>over</em>, and where two of the town's lines run within
    /// a body's width of each other — courses meeting, a crossing's mouth — the two name different ways
    /// for the same patch of ground and both are right. Asserting they agree asserted a coincidence.
    /// </para>
    /// <para>
    /// <b>What the claim covering an exact metre is worth is asked where the metre is known</b>, which is
    /// <see cref="AWalkerInALaneHoldsTheGroundItIsStandingOn"/> — a body stood on a lane's own centreline
    /// by construction, so there is nothing for a second reading to differ about.
    /// </para>
    /// </remarks>
    [Fact]
    public void AWalkerOutOfDoorsHoldsTheGroundItStandsOn()
    {
        using var world = Walking(out var afoot);

        foreach (var person in afoot)
        {
            Assert.True(
                HoldsAnyPavement(world, person),
                $"walker {person} is out of doors at {world.People.OnWayM[person]:F1} m of way " +
                $"{world.People.OnWay[person]} and holds no ground at all");
        }
    }

    /// <summary>
    /// <b>PER-26's second claim: what a walker states is ground in front of it.</b> A statement runs from
    /// the body's own front to where it is aiming, so no stretch of it reaches back over the ground the
    /// body is standing on — which is the one thing that would make it two answers about one piece of the
    /// world.
    /// </summary>
    /// <remarks>
    /// <b>How far it reaches is not asked</b>, because it is data and asserting it would be the arithmetic
    /// written out twice (VER-12). <b>And a statement cut to nothing is the ordinary answer</b> — stronger
    /// ground in front takes it (TER-5g) — so what is asked is where the ones that survive begin.
    /// </remarks>
    [Fact]
    public void WhatAWalkerStatesBeginsInFrontOfIt()
    {
        using var world = Walking(out var afoot);

        var stated = 0;
        foreach (var person in afoot)
        {
            var way = world.People.OnWay[person];
            var alongM = world.People.OnWayM[person];

            foreach (var claim in ClaimsOf(world, way, person, ClaimPriority.Soft))
            {
                Assert.True(
                    claim.FromM >= alongM,
                    $"walker {person} stands at {alongM:F1} m of way {way} and states from {claim.FromM:F1} m");
                stated++;
            }
        }

        Assert.True(stated > 0, "nobody in the town was walking a way of its own, so nothing was stated");
    }

    /// <summary>
    /// <b>PER-26: a body on no way of the network states nothing</b>, there being no way to state it on. It
    /// is the other half of the walk off the network (PER-25) — such a body is walking straight at the
    /// pavement over ground the town does not number, and what it holds while it does is the box it is
    /// standing in.
    /// </summary>
    [Fact]
    public void ABodyOnNoWayOfTheNetworkStatesNothing()
    {
        using var world = Walking(out _);

        Span<LaneClaim> claims = stackalloc LaneClaim[MostClaimsOnAWay];
        foreach (var way in world.Occupancy.OccupiedWays)
        {
            var count = world.Occupancy.CopyTo(way, claims);
            for (var at = 0; at < count; at++)
            {
                ref readonly var claim = ref claims[at];
                if (claim.Of != LaneRoster.Walking || claim.Priority != ClaimPriority.Soft) continue;

                Assert.True(
                    world.People.OnWay[claim.Occupant] != PersonFleet.NoWay,
                    $"walker {claim.Occupant} is on no way of the network and states {way}");
            }
        }
    }

    /// <summary>
    /// <b>PER-25: a body that is walking is walking a route.</b> A search that comes back with nothing is
    /// a leg that cannot be walked at all — and a body sent off at its goal regardless walks the straight
    /// line to it over whatever lies between, a carriageway included, which is the one thing the pavement
    /// network exists to stop.
    /// </summary>
    /// <remarks>
    /// <b>Asked of every walker and not only the ones on a way</b>, because what it refuses is precisely a
    /// body walking while it holds nothing to walk: the goal getting nearer every tick keeps the give-up
    /// clock from ever running up, so nothing else in the town would ever notice.
    /// </remarks>
    [Fact]
    public void ABodyThatIsWalkingIsWalkingARoute()
    {
        using var world = Walking(out _);

        var walking = 0;
        for (var person = 0; person < world.People.Count; person++)
        {
            if (!world.People.Walking[person]) continue;

            Assert.True(
                world.People.RouteCount[person] > 0,
                $"walker {person} is walking with no route to walk");
            walking++;
        }

        Assert.True(walking > 0, "nobody in the town was walking, so nothing was asked");
    }

    /// <summary>
    /// <b>And the way it is on is a way of that route</b>: a walker is seated on the chain it holds and
    /// carried along it, so where it stands on the network is read and never searched for.
    /// </summary>
    [Fact]
    public void AWalkerStandsOnTheWayOfItsRouteItHasTaken()
    {
        using var world = Walking(out var afoot);

        foreach (var person in afoot)
        {
            var route = world.People.RouteOf(person);
            var at = world.People.RouteAt(person);

            Assert.InRange(at, 0, world.People.RouteCount[person] - 1);
            Assert.Equal(route[at], world.People.CurrentRouteWay(person));
        }
    }

    /// <summary>
    /// <b>CTL-1a: what is drawn for a walker is the walk it is holding.</b> The picture sets off the way
    /// the body is going — the first point of it lies on the side the follower is aiming at and never back
    /// past the body.
    /// </summary>
    /// <remarks>
    /// <b>It is the one thing a re-plan drawn beside the body gets wrong, and it gets it wrong often.</b>
    /// The pavement offers a walk both ways along the stretch a body stands on
    /// (<see cref="WalkingNetwork.EntriesNear"/>), so a search run again from under a walker is free to
    /// answer with the lane running back — and then the line drawn is a U-turn across the pavement while
    /// the body walks on down the chain it is actually holding.
    /// </remarks>
    [Fact]
    public void TheWalkDrawnForAWalkerSetsOffTheWayTheBodyIsWalking()
    {
        using var world = Walking(out var afoot);

        var drawn = 0;
        foreach (var person in afoot)
        {
            var atM = world.People.PositionM[person];
            var aim = world.People.DestinationM[person] - atM;
            var points = world.WalkHeld(slot: 0, person);
            if (points.Length == 0 || aim.LengthSquared() < 1e-6f) continue;

            Assert.True(
                Vector2.Dot(Vector2.Normalize(points[0] - atM), Vector2.Normalize(aim)) > 0f,
                $"the walk drawn for walker {person} sets off at {points[0] - atM} while the body walks at {aim}");
            drawn++;
        }

        Assert.True(drawn > 0, "no walker of the town had a walk to draw, so nothing was asked");
    }

    /// <summary>
    /// <b>A walker standing on the pavement is offered both ways along it</b> (WLK-8): the lane it stands
    /// nearest and the lane beside it running back, so a search may send it either way. A walker has no
    /// lane of its own to be pointing along, and offered one it could only ever go the way that lane
    /// happened to run — putting anything behind it a lap of the ring away.
    /// </summary>
    /// <remarks>
    /// <b>Two links and not two readings of one.</b> A pavement is two one-way lines a lane's width apart
    /// (<see cref="FootGraph.Reverse"/> is none), so the second entry is a stretch of its own — and where
    /// the two offered were the same link, nothing would have been offered at all.
    /// </remarks>
    [Fact]
    public void AWalkerIsOfferedBothWaysAlongThePavement()
    {
        using var world = Walking(out var afoot);

        var entries = new RouteEntry[2];
        foreach (var person in afoot)
        {
            var count = world.Walking.EntriesNear(world.People.PositionM[person], entries);

            Assert.True(count == 2, $"walker {person} was offered {count} ways onto the network rather than two");
            Assert.True(
                entries[0].Link != entries[1].Link,
                $"walker {person} was offered link {entries[0].Link} twice");
        }
    }

    /// <summary>
    /// <b>A walker standing on a carriageway holds the lane under it</b> (PER-26, TER-4c.2) — which since
    /// the paint stopped carrying a rank of its own is the whole of what stops a driver reaching somebody
    /// crossing the road. <b>On a zebra or on bare tarmac alike</b>: the claim is laid from the body's own
    /// box off the ways beneath it, and what is painted there is not a question it asks.
    /// </summary>
    /// <remarks>
    /// <b>Stood on the lane's own line rather than found there</b>, because whether a town's walk happens
    /// to take somebody into a road within the ticks a town-tier case affords is not what this is asking —
    /// and a body sampled off the centreline is on that lane by construction, so the case fails for the
    /// claim being missing and for nothing else.
    /// </remarks>
    [Fact]
    public void AWalkerInALaneHoldsTheGroundItIsStandingOn()
    {
        using var world = Walking(out var afoot);

        var person = afoot[0];
        var lane = LongestLane(world.Roads);
        var alongM = world.Roads.LaneLengthM[lane] * 0.5f;

        world.People.PositionM[person] = Spline.SampleAt(world.Roads.ArcsOf(lane), alongM).PositionM;
        world.People.VelocityMps[person] = Vector2.Zero;
        world.RebuildProximityIndex();

        var way = world.Ways.OfRoadLane(lane);
        Assert.True(
            Holds(world, way, person, ClaimPriority.Hard, alongM),
            $"walker {person} stands in lane {lane} at {alongM:F1} m and holds none of it");
    }

    /// <summary>The lane with the most room to stand a body in the middle of, so no case is a question about length.</summary>
    static int LongestLane(RoadGraph roads)
    {
        var best = 0;
        for (var lane = 1; lane < roads.LaneCount; lane++)
        {
            if (roads.LaneLengthM[lane] > roads.LaneLengthM[best]) best = lane;
        }

        return best;
    }

    /// <summary>Whether this body holds a stretch of any way of the pavement at p0, which is PER-26's first claim.</summary>
    static bool HoldsAnyPavement(TownWorld world, int person)
    {
        var claims = new LaneClaim[MostClaimsOnAWay];
        foreach (var way in world.Occupancy.OccupiedWays)
        {
            if (world.Ways.KindOf(way) is not (WayKind.Footway or WayKind.Mitre)) continue;

            var count = world.Occupancy.CopyTo(way, claims);
            for (var at = 0; at < count; at++)
            {
                ref readonly var claim = ref claims[at];
                if (claim.Occupant != person || claim.Of != LaneRoster.Walking) continue;
                if (claim.Priority == ClaimPriority.Hard) return true;
            }
        }

        return false;
    }

    /// <summary>Whether this body holds a stretch of that way covering that metre, at that rank.</summary>
    static bool Holds(TownWorld world, int way, int person, ClaimPriority priority, float alongM)
    {
        foreach (var claim in ClaimsOf(world, way, person, priority))
        {
            if (claim.FromM <= alongM && alongM <= claim.ToM) return true;
        }

        return false;
    }

    /// <summary>Every stretch of one way this walker holds at one rank.</summary>
    static List<LaneClaim> ClaimsOf(TownWorld world, int way, int person, ClaimPriority priority)
    {
        var found = new List<LaneClaim>();
        if (way == PersonFleet.NoWay) return found;

        var claims = new LaneClaim[MostClaimsOnAWay];
        var count = world.Occupancy.CopyTo(way, claims);
        for (var at = 0; at < count; at++)
        {
            ref readonly var claim = ref claims[at];
            if (claim.Occupant != person || claim.Of != LaneRoster.Walking) continue;
            if (claim.Priority != priority) continue;

            found.Add(claim);
        }

        return found;
    }

    /// <summary>More stretches than any way of a pavement carries, which is a handful of bodies at a corner.</summary>
    const int MostClaimsOnAWay = 64;

    /// <summary>The town stood up and not yet ticked, which is where GEN-7 is asked.</summary>
    static TownWorld Stood(out CityPlan plan)
    {
        plan = Towns.Built;
        return new TownWorld(plan, Config);
    }

    /// <summary>
    /// And the same town run on until its people are out of their first dwell and walking — with the
    /// walkers that are out of doors and standing on a way of the network, which is who PER-26 is asked of.
    /// </summary>
    static TownWorld Walking(out List<int> afoot)
    {
        var world = Stood(out _);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(SettleTicks);

        afoot = [];
        for (var person = 0; person < world.People.Count; person++)
        {
            if (world.People.Inside[person].Any || world.People.Wounded[person]) continue;
            if (world.People.OnWay[person] == PersonFleet.NoWay) continue;

            afoot.Add(person);
        }

        Assert.True(afoot.Count > 0, "no walker of the town got out of a door and onto the pavement");
        return world;
    }

    /// <summary>
    /// Long enough that the longest dwell a building hands out has run and the bodies are on the pavement,
    /// and short enough to stay a town-tier case.
    /// </summary>
    static int SettleTicks => (int)(Config.Building.DwellMaxS * Config.Sim.TickRateHz) + 120;

    /// <summary>How many the plan actually stood, which is the brief's count bounded by the doors there were.</summary>
    static int PeopleStood(CityPlan plan)
    {
        var stood = 0;
        for (var spawn = 0; spawn < plan.Spawns.Count; spawn++)
        {
            if (plan.Spawns.Kind[spawn] == SpawnKindPerson) stood++;
        }

        return stood;
    }

    const byte SpawnKindPerson = 0;
}
