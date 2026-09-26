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

    /// <summary>
    /// <b>PER-27: a walker walking a crossing reserves it to the far kerb.</b> The paint in front of the
    /// body on the stretch it is taking, and the band of every lane that paint is laid across, are
    /// stretches of this walker's at p7 — the far kerb spoken for from the moment the walk is on the zebra,
    /// and not only the metre under the feet.
    /// </summary>
    /// <remarks>
    /// <b>Stood on the paint by hand</b>, because no walk a town lays takes a zebra yet
    /// ([the known gaps](../../../../docs/index.md#known-gaps)): what this asks is what a body walking one
    /// claims, and a case that waited for the router to offer a crossing would be asking about the router.
    /// </remarks>
    [Fact]
    public void AWalkerWalkingACrossingReservesIt()
    {
        using var world = Walking(out var afoot);

        var edge = ACrossingOfTheTown(world);
        Assert.True(edge >= 0, "the town painted no zebra with a lane under it");

        var person = afoot[0];
        var way = world.Ways.OfFootway(edge);
        var lengthM = world.Ways.LengthM(way);
        WalkTheCrossing(world, person, edge, lengthM * 0.5f);

        Assert.True(
            Holds(world, way, person, ClaimPriority.Reserved, lengthM * 0.75f),
            $"walker {person} is half way over crossing way {way} and has not reserved the rest of it");

        // And the carriageway under it, which is where the traffic meets the reservation at all.
        var lane = world.Ways.OfRoadLane(world.Bands.On(edge)[0].Lane);
        Assert.True(
            ClaimsOf(world, lane, person, ClaimPriority.Reserved).Count > 0,
            $"walker {person} walks crossing way {way} and has reserved no band of lane way {lane} under it");
    }

    /// <summary>
    /// <b>PER-27: what a walker reserves is the stretch of paint it is taking and not the crossing's other
    /// one.</b> A zebra is two walking lanes over one carriageway (WLK-15) and the second of them is the
    /// walk back, which this body is not on — so nothing of it is this walker's at p7.
    /// </summary>
    /// <remarks>
    /// <b>It is what keeps the cut at the body's near edge cut.</b> The two lanes run the same paint
    /// opposite ways, so a reservation laid on both covered from the other one's far end exactly the half
    /// the walker had already crossed.
    /// </remarks>
    [Fact]
    public void AWalkerWalkingACrossingLeavesItsOtherStretchAlone()
    {
        using var world = Walking(out var afoot);

        var edge = ACrossingOfTheTown(world);
        Assert.True(edge >= 0, "the town painted no zebra with a lane under it");

        var back = TheWalkBackOver(world, edge);
        Assert.True(back >= 0, $"the town walks crossing {world.Bands.CrossingOf(edge)} one way only");

        var person = afoot[0];
        var way = world.Ways.OfFootway(edge);
        WalkTheCrossing(world, person, edge, world.Ways.LengthM(way) * 0.5f);

        var twin = world.Ways.OfFootway(back);
        Assert.True(
            ClaimsOf(world, twin, person, ClaimPriority.Reserved).Count == 0,
            $"walker {person} walks crossing way {way} and has reserved the walk back over it,"
            + $" way {twin}, which carries {WhatIsOn(world, twin)}");
    }

    /// <summary>
    /// <b>PER-27: a walker at a kerb stands off a crossing the traffic has.</b> The reservation is asked of
    /// the claims, and a wheeled body on the paint is the answer that a walker still on the corner may not
    /// step out — which is the whole of what the reservation buys, since it refuses the traffic nothing.
    /// </summary>
    /// <remarks>
    /// <b>Asked of a crossing nothing is on</b>, so that the clear answer is the case's own construction
    /// and the taken one is the car it stands there. Both are asserted, because an answer that is always
    /// "wait" would pass a case that only asked the second.
    /// </remarks>
    [Fact]
    public void AWalkerAtAKerbStandsOffACrossingTheTrafficHas()
    {
        using var world = Walking(out var afoot);
        Assert.True(world.Cars.Count > 0, "the town stood no car to put on a zebra");
        Assert.True(
            AQuietCrossing(world, out var edge, out var from),
            "the town painted no zebra nobody was on that a walk arrives at");

        var person = afoot[0];
        StandAtTheKerbOf(world, person, from, edge);
        Assert.Equal(world.Bands.CrossingOf(edge), world.People.OnCrossing[person]);
        Assert.False(
            world.People.WaitsToCross[person],
            $"walker {person} waits at the kerb of crossing {edge} with nothing on it");

        // Square across the paint, which is how a car comes to be standing on one.
        var way = world.Ways.OfFootway(edge);
        var on = Spline.SampleAt(world.LineOfWay(way, out _), world.Ways.LengthM(way) * 0.5f);
        var across = Heading.RightOf(on.Direction);
        StandTheBodyAt(world, on.PositionM, MathF.Atan2(across.Y, across.X));

        Assert.True(
            world.People.WaitsToCross[person],
            $"walker {person} is at the kerb of crossing {edge} with a car standing on it and does not wait"
            + $" — paint way {way} carries {WhatIsOn(world, way)}");

        // And the wait is the walk standing still: the body aims at its own feet, so nothing steps onto the
        // paint while somebody else is on it.
        world.RebuildProximityIndex();
        Assert.Equal(world.People.PositionM[person], world.People.DestinationM[person]);
    }

    /// <summary>
    /// A stretch of paint whose first metres are kerb rather than carriageway, with nothing of the driving
    /// roster on it or on a lane beneath it — so that what a walker is told about it is the case's own
    /// doing — and the pavement a walk arrives at it from.
    /// </summary>
    static bool AQuietCrossing(TownWorld world, out int edge, out int from)
    {
        for (edge = 0; edge < world.Foot.EdgeCount; edge++)
        {
            if (world.Foot.KindOf(edge) != FootEdgeKind.Crossing) continue;
            if (world.Bands.On(edge).Length == 0 || !IsQuiet(world, world.Ways.OfFootway(edge))) continue;

            var quiet = true;
            foreach (var band in world.Bands.On(edge)) quiet &= IsQuiet(world, world.Ways.OfRoadLane(band.Lane));
            if (!quiet) continue;

            from = TheWalkOnto(world, edge);
            if (from != PersonFleet.NoWay) return true;
        }

        edge = -1;
        from = PersonFleet.NoWay;
        return false;
    }

    /// <summary>Everything on one way, for a message that has to say why an answer was not the one wanted.</summary>
    static string WhatIsOn(TownWorld world, int way)
    {
        var claims = new LaneClaim[MostClaimsOnAWay];
        var count = world.Occupancy.CopyTo(way, claims);
        var said = new List<string>();
        for (var at = 0; at < count; at++)
        {
            said.Add($"{claims[at].Of} {claims[at].Occupant} p{(byte)claims[at].Priority} "
                + $"{claims[at].FromM:0.0}-{claims[at].ToM:0.0}");
        }

        return said.Count == 0 ? "nothing" : string.Join(", ", said);
    }

    /// <summary>Whether no wheeled body and no driver's road is anywhere on this way.</summary>
    static bool IsQuiet(TownWorld world, int way)
    {
        var claims = new LaneClaim[MostClaimsOnAWay];
        var count = world.Occupancy.CopyTo(way, claims);
        for (var at = 0; at < count; at++)
        {
            if (claims[at].Of == LaneRoster.Driving) return false;
        }

        return true;
    }

    /// <summary>The pavement a walk reaches this stretch of paint from, or <see cref="PersonFleet.NoWay"/>.</summary>
    static int TheWalkOnto(TownWorld world, int edge)
    {
        for (var from = 0; from < world.Foot.EdgeCount; from++)
        {
            foreach (var turn in world.Walking.TurnsFrom(from))
            {
                if (turn == edge) return from;
            }
        }

        return PersonFleet.NoWay;
    }

    /// <summary>
    /// This walker put at the far end of the pavement that arrives at one stretch of paint, walking a chain
    /// of the two — a body at a kerb, which is where a crossing is asked for and not yet stepped onto.
    /// </summary>
    static void StandAtTheKerbOf(TownWorld world, int person, int from, int edge)
    {
        var people = world.People;
        var route = people.RouteOf(person);
        route[0] = from;
        route[1] = edge;
        people.RouteCount[person] = 2;
        people.RouteTaken[person] = 1;
        people.RouteToM[person] = world.Walking.WayLengthM(edge);

        var atM = MathF.Max(0f, world.Walking.WayLengthM(from) - AtTheKerbM);
        people.OnWayM[person] = atM;
        people.VelocityMps[person] = Vector2.Zero;
        people.PositionM[person] = Spline.SampleAt(world.Walking.WayArcs(from), atM).PositionM;

        world.RebuildProximityIndex();
    }

    /// <summary>
    /// How far short of the kerb the body is stood: inside any stopping distance a walking pace produces,
    /// so that the case is about the crossing being asked for and not about how far off one is asked from.
    /// </summary>
    const float AtTheKerbM = 0.1f;

    /// <summary>A car of the fleet stood where it is asked for, going nowhere — a body and nothing else.</summary>
    static void StandTheBodyAt(TownWorld world, Vector2 atM, float headingRad)
    {
        world.Cars.Driven[0] = false;
        world.Cars.Broken[0] = true;
        world.Cars.VelocityMps[0] = Vector2.Zero;
        world.Cars.PositionM[0] = atM;
        world.Cars.HeadingRad[0] = headingRad;
        world.RebuildProximityIndex();
    }

    /// <summary>
    /// One of the ways the town's zebras are walked, with a lane actually running under it, or <c>-1</c>
    /// where it painted none.
    /// </summary>
    static int ACrossingOfTheTown(TownWorld world)
    {
        for (var edge = 0; edge < world.Foot.EdgeCount; edge++)
        {
            if (world.Foot.KindOf(edge) != FootEdgeKind.Crossing) continue;
            if (world.Bands.On(edge).Length == 0) continue;

            return edge;
        }

        return -1;
    }

    /// <summary>
    /// The other stretch of the same zebra — the walk back over it — or <c>-1</c> where there is none.
    /// </summary>
    static int TheWalkBackOver(TownWorld world, int edge)
    {
        var crossing = world.Bands.CrossingOf(edge);
        for (var other = 0; other < world.Foot.EdgeCount; other++)
        {
            if (other != edge && world.Bands.CrossingOf(other) == crossing) return other;
        }

        return -1;
    }

    /// <summary>
    /// This walker put onto one stretch of paint by hand, as far along it as asked, with the claims rebuilt
    /// around it — the route it is walking and the body on it, which is what
    /// <see cref="PersonFleet.OnCrossing"/> is read off.
    /// </summary>
    static void WalkTheCrossing(TownWorld world, int person, int edge, float alongM)
    {
        var people = world.People;
        var way = world.Ways.OfFootway(edge);
        var route = people.RouteOf(person);
        route[0] = edge;
        people.RouteCount[person] = 1;
        people.RouteTaken[person] = 1;
        people.RouteToM[person] = world.Ways.LengthM(way);
        people.OnWayM[person] = alongM;
        people.PositionM[person] = Spline.SampleAt(world.LineOfWay(way, out _), alongM).PositionM;

        world.RebuildProximityIndex();
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
