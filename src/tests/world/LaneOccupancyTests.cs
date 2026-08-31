using System.Collections.Concurrent;
using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// The lane index as arithmetic: stretches go in, the nearest one in front comes out, and a rebuild
/// leaves nothing of the tick before it.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class LaneOccupancyTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static LaneOccupancy Index(out RoadGraph roads, int mostSlots = 16)
    {
        roads = RoadGraph.Build(Towns.Of("Test"), Config);
        return new LaneOccupancy(roads, mostSlots);
    }

    /// <summary>The order stretches go in is not the order they are read back in — the near edge is.</summary>
    [Fact]
    public void TheNearestBodyInFrontIsTheOneWithTheLeastNearEdge()
    {
        var index = Index(out var roads);
        var way = index.WayOfLane(FirstLongLane(roads, 60f));

        index.Begin();
        index.Add(way, 40f, 44f, 3f, occupant: 7, LaneUse.Reserved);
        index.Add(way, 12f, 16f, 0f, occupant: 3, LaneUse.Reserved);
        index.Add(way, 25f, 29f, 1f, occupant: 5, LaneUse.Obstruction);

        Assert.True(index.AheadBody(way, 0f, 60f, excluding: LaneOccupancy.Nobody, out var found));
        Assert.Equal(3, found.Occupant);

        // From past the first, the next one — and from past all of them, nothing.
        Assert.True(index.AheadBody(way, 20f, 60f, LaneOccupancy.Nobody, out found));
        Assert.Equal(5, found.Occupant);
        Assert.False(index.AheadBody(way, 50f, 60f, LaneOccupancy.Nobody, out _));
    }

    /// <summary>A driver never finds itself in front of itself, which is what the exclusion is for.</summary>
    [Fact]
    public void TheAskerIsNeverWhatIsInFrontOfIt()
    {
        var index = Index(out var roads);
        var way = index.WayOfLane(FirstLongLane(roads, 60f));

        index.Begin();
        index.Add(way, 10f, 14f, 0f, occupant: 1, LaneUse.Reserved);
        index.Add(way, 30f, 34f, 0f, occupant: 2, LaneUse.Reserved);

        Assert.True(index.AheadBody(way, 0f, 60f, excluding: 1, out var found));
        Assert.Equal(2, found.Occupant);
    }

    /// <summary>
    /// A claim is not a body and a body is not a claim. The two are asked apart because they are answered
    /// apart: one is something to keep a gap behind, the other a place to be stopped short of.
    /// </summary>
    [Fact]
    public void AClaimIsNeverReturnedAsABodyNorABodyAsAClaim()
    {
        var index = Index(out var roads);
        var way = index.WayOfLane(FirstLongLane(roads, 60f));

        index.Begin();
        index.Add(way, 10f, 14f, 0f, occupant: 1, LaneUse.Claimed);
        index.Add(way, 30f, 34f, 0f, occupant: 2, LaneUse.Reserved);

        Assert.True(index.AheadBody(way, 0f, 60f, LaneOccupancy.Nobody, out var body));
        Assert.Equal(2, body.Occupant);

        Assert.True(index.AheadClaim(way, 0f, 60f, LaneOccupancy.Nobody, out var claim));
        Assert.Equal(1, claim.Occupant);
        Assert.Equal(1, index.ClaimCount);
    }

    /// <summary>
    /// <b>A claim the asker is standing on is not a cut</b> (TER-5e). A claim is ground its holder has
    /// <em>not reached</em>, so one whose near edge is behind the asker is ground the asker has — and a
    /// grant answered at it is no longer a distance in front of the nose but a body's length of negative
    /// road, which nothing can drive out of by stopping.
    /// </summary>
    /// <remarks>
    /// <b>It is a body that is answered from behind and never a claim.</b> A stretch this asker overlaps is
    /// a contact, and the grant is left free to say so — which is the whole of the difference between the
    /// two halves of this case.
    /// </remarks>
    [Fact]
    public void AClaimBehindTheAskerCutsNothingAndABodyBehindItStillDoes()
    {
        var index = Index(out var roads);
        var way = index.WayOfLane(FirstLongLane(roads, 60f));
        var asker = new LaneCredit(2f, LaneRoster.Driving, RightOfWay.Traffic);

        // The car queueing behind for the same movement, claiming the run through the body in front of it.
        index.Begin();
        index.Add(way, 10f, 30f, 0f, occupant: 1, LaneUse.Claimed);

        Assert.Equal(
            float.PositiveInfinity, index.GrantedOn(way, 20f, 60f, occupant: 2, asker, out _));

        // From behind its near edge the same claim is a place to be stopped a margin short of.
        Assert.Equal(8f, index.GrantedOn(way, 5f, 60f, occupant: 2, asker, out _), 3);

        // And a body reaching back past the asker is a contact, which the grant is left to report.
        index.Begin();
        index.Add(way, 10f, 30f, 0f, occupant: 1, LaneUse.Reserved);
        Assert.Equal(10f, index.GrantedOn(way, 20f, 60f, occupant: 2, asker, out _), 3);
    }

    /// <summary>
    /// <b>A body on foot takes the road it stands on and is not traffic.</b> It cuts the grant of anybody
    /// driving through it, exactly as a car standing there would; what it is <em>not</em> is an answer to
    /// somebody asking what is coming down the lane, and it is not an obstruction either — that reading is
    /// a walker `E-4` would cross the centreline to drive round.
    /// </summary>
    [Fact]
    public void AWalkerOnTheRoadCutsTheGrantAndIsNotTraffic()
    {
        var index = Index(out var roads);
        var way = index.WayOfLane(FirstLongLane(roads, 60f));

        index.Begin();
        index.Add(way, 20f, 26f, 0f, occupant: 4, LaneUse.OnFoot);

        // The ground it stands on is spoken for, so no driver is granted the road through it.
        var at = LaneOccupancy.FromTheStart;
        Assert.True(index.NextSpokenFor(way, 0f, 60f, LaneOccupancy.Nobody, ref at, out var taken));
        Assert.Equal(LaneUse.OnFoot, taken.Use);
        Assert.Equal(20f, taken.FromM);

        // And it is a body in front to be stopped short of.
        Assert.True(index.AheadBody(way, 0f, 60f, LaneOccupancy.Nobody, out var body));
        Assert.Equal(LaneUse.OnFoot, body.Use);

        // But never traffic, and never a claim: a walker at a kerb asking what is coming must not be
        // answered by another walker standing in the road.
        Assert.False(index.BehindBody(way, 60f, 0f, LaneOccupancy.Nobody, out _));
        Assert.False(index.AheadClaim(way, 0f, 60f, LaneOccupancy.Nobody, out _));
        Assert.False(index.ClaimedByAnother(way, 20f, 26f, LaneOccupancy.Nobody));
        Assert.Equal(0, index.ClaimCount);

        Assert.True(index.AnybodyOnFoot(way, 23f, 23f));
        Assert.True(index.AnybodyOnFoot(way, 18f, 21f));
        Assert.False(index.AnybodyOnFoot(way, 30f, 40f));
    }

    /// <summary>
    /// <b>Ground somebody is waiting for is in the book and is in nothing else</b> (TER-5e). It is the ask a
    /// walker at a kerb was refused: no grant is cut at it, nobody reads it as a body or as traffic, and the
    /// one question it answers is the one a driver approaching that paint asks.
    /// </summary>
    /// <remarks>
    /// <b>Both halves are the point.</b> Out of the book, a right of way nobody can see is not one (TER-4c);
    /// in it as a body, a car that could not stop at the kerb line brakes as hard as it can for somebody
    /// still on the pavement.
    /// </remarks>
    [Fact]
    public void GroundSomebodyIsWaitingForIsSeenAndCutsNothing()
    {
        var index = Index(out var roads);
        var way = index.WayOfLane(FirstLongLane(roads, 60f));

        index.Begin();
        index.Add(way, 20f, 26f, 0f, occupant: 4, LaneUse.Awaited, LaneRoster.Walking, RightOfWay.OnThePaint);

        Assert.True(index.AnybodyWaitingFor(way, 23f, 23f));
        Assert.False(index.AnybodyWaitingFor(way, 30f, 40f));

        var at = LaneOccupancy.FromTheStart;
        Assert.False(index.NextSpokenFor(way, 0f, 60f, LaneOccupancy.Nobody, ref at, out _));
        Assert.False(index.AheadBody(way, 0f, 60f, LaneOccupancy.Nobody, out _));
        Assert.False(index.AnybodyOnFoot(way, 23f, 23f));
        Assert.False(index.AnyTrafficOver(way, 20f, 26f));
        Assert.False(index.SpokenForByAnother(way, 20f, 26f, LaneOccupancy.Nobody, out _));
    }

    /// <summary>
    /// <b>A right of way takes a claim and never a body</b> (TER-5e): ground nobody has reached is given up
    /// to the stronger movement, and ground somebody is standing on — or committed to being able to stop in —
    /// refuses everything, whatever ranks the two of them carry.
    /// </summary>
    [Fact]
    public void ARightOfWayTakesAClaimAndNeverABody()
    {
        var claim = new LaneSlot(0f, 6f, 6f, 0f, 1, LaneUse.Claimed, LaneRoster.Driving, RightOfWay.TurningAcross);
        Assert.False(LaneOccupancy.Binds(claim, RightOfWay.StraightOn));
        Assert.True(LaneOccupancy.Binds(claim, RightOfWay.TurningAcross));

        // The same claim laid by a car that can no longer stop short of the box it is entering.
        var committed = claim with { Right = RightOfWay.Committed };
        Assert.True(LaneOccupancy.Binds(committed, RightOfWay.StraightOn));

        // And a body, which is not a rank's to take at any strength.
        Assert.True(LaneOccupancy.Binds(claim with { Use = LaneUse.Reserved }, RightOfWay.StraightOn));
        Assert.True(LaneOccupancy.Binds(claim with { Use = LaneUse.Obstruction }, RightOfWay.StraightOn));
        Assert.True(LaneOccupancy.Binds(claim with { Use = LaneUse.OnFoot }, RightOfWay.StraightOn));
    }

    /// <summary>
    /// <b>And the same comparison read from the claim's own side</b> (TER-4c.1): what takes a claim away from
    /// its holder is a rank above the one the holder is keeping it at, and nothing else — so a claim survives
    /// the traffic driving over it and the very body a swerve took it to get round, and does not survive a
    /// closed road or a rescue.
    /// </summary>
    [Fact]
    public void OnlyAStrongerRankTakesAClaimFromItsHolder()
    {
        var mine = RightOfWay.Traffic;
        var over = new LaneSlot(0f, 6f, 6f, 0f, 1, LaneUse.Reserved, LaneRoster.Driving, RightOfWay.Traffic);

        Assert.False(LaneOccupancy.TakesAClaim(over, mine));
        Assert.False(LaneOccupancy.TakesAClaim(over with { Use = LaneUse.Obstruction }, mine));
        Assert.False(LaneOccupancy.TakesAClaim(over with { Use = LaneUse.OnFoot }, mine));
        Assert.False(LaneOccupancy.TakesAClaim(over with { Use = LaneUse.Furniture }, mine));

        Assert.True(LaneOccupancy.TakesAClaim(over with { Right = RightOfWay.Closed }, mine));
        Assert.True(LaneOccupancy.TakesAClaim(over with { Right = RightOfWay.Emergency }, mine));

        // And a rank the holder itself carries takes nothing: a rescue does not give its own road back.
        Assert.False(LaneOccupancy.TakesAClaim(over with { Right = RightOfWay.Emergency }, RightOfWay.Emergency));
    }

    /// <summary>
    /// <b>Straighter is stronger</b> (TER-5e), and the order is one scale rather than a table of pairs: the
    /// stream that turns out of nobody's way, ordinary traffic, and the turn across the oncoming stream,
    /// which is the last movement a box admits (TER-5f).
    /// </summary>
    [Fact]
    public void AMovementsRightOfWayIsTheTurnItMakes()
    {
        Assert.True(RoadGraph.RightOfWayOf(LaneTurn.Straight) > RoadGraph.RightOfWayOf(LaneTurn.NearSide));
        Assert.True(RoadGraph.RightOfWayOf(LaneTurn.NearSide) > RoadGraph.RightOfWayOf(LaneTurn.FarSide));

        // Ordinary traffic is the middle of the scale and what a stretch laid without a rank is given, so
        // nothing that is not a movement through a box is either given way to or taken from.
        Assert.Equal(RightOfWay.Traffic, RoadGraph.RightOfWayOf(LaneTurn.NearSide));
        Assert.Equal(RightOfWay.Traffic, LaneSlot.Nothing.Right);
    }

    /// <summary>
    /// <b>Two cars that each found the lane clear on the same tick must not both take it.</b> It is the
    /// junction registry's argument applied to a stretch of lane, and the whole of what makes a claim a
    /// reservation rather than a note.
    /// </summary>
    [Fact]
    public void GroundSomebodyHasClaimedIsRefusedToTheNextAsker()
    {
        var index = Index(out var roads);
        var way = index.WayOfLane(FirstLongLane(roads, 60f));

        index.Begin();
        index.Add(way, 20f, 26f, 0f, occupant: 1, LaneUse.Claimed);

        Assert.True(index.ClaimedByAnother(way, 24f, 30f, excluding: 2));
        Assert.False(index.ClaimedByAnother(way, 24f, 30f, excluding: 1));
        Assert.False(index.ClaimedByAnother(way, 40f, 46f, excluding: 2));
    }

    /// <summary>
    /// <b>A driver under way is one stretch and is read to two different edges</b>: the road it has taken is
    /// what a grant behind it is cut at, and the body at the near end of that road is what anybody asking
    /// what is in front of it is answered with.
    /// </summary>
    [Fact]
    public void ADriverIsOneStretchWhoseBodyIsItsNearEnd()
    {
        var index = Index(out var roads);
        var way = index.WayOfLane(FirstLongLane(roads, 60f));

        index.Begin();
        index.AddUnderWay(way, 10f, standsToM: 14f, toM: 38f, 8f, occupant: 1);
        index.Add(way, 45f, 49f, 0f, occupant: 2, LaneUse.Obstruction);

        // One entry and not two: the car is laid once, so a walk of what is spoken for passes from its road
        // straight to the wreck beyond it.
        var at = LaneOccupancy.FromTheStart;
        Assert.True(index.NextSpokenFor(way, 0f, 60f, LaneOccupancy.Nobody, ref at, out var found));
        Assert.Equal(LaneUse.Reserved, found.Use);
        Assert.Equal(10f, found.FromM);
        Assert.Equal(38f, found.ToM);

        Assert.True(index.NextSpokenFor(way, 0f, 60f, LaneOccupancy.Nobody, ref at, out found));
        Assert.Equal(45f, found.FromM);
        Assert.False(index.NextSpokenFor(way, 0f, 60f, LaneOccupancy.Nobody, ref at, out _));

        // And a driver's own stretch is never what it is cut at.
        at = LaneOccupancy.FromTheStart;
        Assert.True(index.NextSpokenFor(way, 0f, 60f, excluding: 1, ref at, out found));
        Assert.Equal(45f, found.FromM);

        // The body is where the car stands and not where its road ends: from twenty metres up the way that
        // car is behind, and the only thing in front is the wreck.
        Assert.True(index.AheadBody(way, 0f, 60f, LaneOccupancy.Nobody, out var body));
        Assert.Equal(1, body.Occupant);
        Assert.True(index.AheadBody(way, 20f, 60f, LaneOccupancy.Nobody, out body));
        Assert.Equal(2, body.Occupant);
    }

    /// <summary>
    /// <b>A stretch that reaches back past the asker is still in front of it or still behind it, and where
    /// its body has got to is which.</b> A car doing thirty has taken road well past the car in front of it;
    /// cutting that car at it would hold up a driver on behalf of the one behind him.
    /// </summary>
    [Fact]
    public void WhatIsBehindIsNeverCutAtHoweverFarItsRoadReaches()
    {
        var index = Index(out var roads);
        var way = index.WayOfLane(FirstLongLane(roads, 60f));

        index.Begin();
        index.AddUnderWay(way, 4f, standsToM: 8f, toM: 50f, 20f, occupant: 1);

        var behind = LaneOccupancy.FromTheStart;
        Assert.False(index.NextSpokenFor(way, 20f, 60f, LaneOccupancy.Nobody, ref behind, out _));

        var ahead = LaneOccupancy.FromTheStart;
        Assert.True(index.NextSpokenFor(way, 0f, 60f, LaneOccupancy.Nobody, ref ahead, out _));

        // Nor is it a body in front, which is the same fact asked the other way round: what reaches past
        // the asker is that car's road and the car itself is well behind.
        Assert.False(index.AheadBody(way, 20f, 60f, LaneOccupancy.Nobody, out _));
        Assert.False(index.BehindBody(way, 60f, 20f, LaneOccupancy.Nobody, out _));
    }

    /// <summary>
    /// A body the asker is already overlapping is a contact and not an empty road — <b>and one reaching
    /// exactly as far as the asker's own near edge is the boundary of that and not the exception to it</b>,
    /// which is the bar a walk of what is spoken for holds a stretch to as well.
    /// </summary>
    [Fact]
    public void SomethingOverlappingTheAskerAnswersAtItsOwnNearEdge()
    {
        var index = Index(out var roads);
        var way = index.WayOfLane(FirstLongLane(roads, 60f));

        index.Begin();
        index.Add(way, 8f, 14f, 0f, occupant: 1, LaneUse.Obstruction);

        Assert.True(index.AheadBody(way, 10f, 60f, LaneOccupancy.Nobody, out var found));
        Assert.Equal(8f, found.FromM);

        Assert.True(index.AheadBody(way, 14f, 60f, LaneOccupancy.Nobody, out found));
        Assert.Equal(8f, found.FromM);

        var at = LaneOccupancy.FromTheStart;
        Assert.True(index.NextSpokenFor(way, 14f, 60f, LaneOccupancy.Nobody, ref at, out _));
    }

    /// <summary><b>Nothing survives a rebuild</b>, which is the guarantee that makes the index need no release path.</summary>
    [Fact]
    public void ARebuildLeavesNothingOfTheTickBeforeIt()
    {
        var index = Index(out var roads);
        var way = index.WayOfLane(FirstLongLane(roads, 60f));

        index.Begin();
        index.Add(way, 10f, 14f, 0f, occupant: 1, LaneUse.Reserved);
        index.Add(way, 30f, 34f, 0f, occupant: 2, LaneUse.Claimed);

        index.Begin();
        Assert.Equal(0, index.SlotCount);
        Assert.Equal(0, index.ClaimCount);
        Assert.False(index.AheadBody(way, 0f, 60f, LaneOccupancy.Nobody, out _));
        Assert.False(index.AheadClaim(way, 0f, 60f, LaneOccupancy.Nobody, out _));
    }

    /// <summary>Past the bound a stretch is not laid, and a refusal is what the caller is told.</summary>
    [Fact]
    public void PastItsBoundTheIndexRefusesRatherThanGrows()
    {
        var index = Index(out var roads, mostSlots: 2);
        var way = index.WayOfLane(FirstLongLane(roads, 60f));

        index.Begin();
        Assert.True(index.Add(way, 10f, 14f, 0f, 1, LaneUse.Reserved));
        Assert.True(index.Add(way, 20f, 24f, 0f, 2, LaneUse.Reserved));
        Assert.False(index.Add(way, 30f, 34f, 0f, 3, LaneUse.Reserved));
        Assert.Equal(2, index.SlotCount);
    }

    /// <summary>
    /// <b>A way is named once among the ways somebody is on, however often it is laid on and given back.</b>
    /// A car gives its crossing back and the car behind takes the same join in the same walk, so a way the
    /// withdrawal emptied is laid on again — and listed twice, every reader of the book counts what is on it
    /// twice.
    /// </summary>
    [Fact]
    public void AWayEmptiedAndLaidOnAgainIsNamedOnce()
    {
        var index = Index(out var roads);
        var way = index.WayOfLane(FirstLongLane(roads, 60f));

        index.Begin();
        index.Add(way, 10f, 14f, 0f, occupant: 1, LaneUse.Claimed);
        index.Withdraw(way, occupant: 1, LaneUse.Claimed);
        Assert.Equal(0, index.ClaimCount);

        index.Add(way, 30f, 34f, 0f, occupant: 2, LaneUse.Claimed);

        var named = 0;
        foreach (var listed in index.OccupiedWays)
        {
            if (listed == way) named++;
        }

        Assert.Equal(1, named);
    }

    /// <summary>A lane and the join out of it are different ways, and nothing on one is on the other.</summary>
    [Fact]
    public void AJoinIsAWayOfItsOwn()
    {
        var index = Index(out var roads);
        var lane = FirstLongLane(roads, 60f);
        var join = index.WayOfTurn(roads.TurnSlotAt(lane, 0));
        Assert.NotEqual(index.WayOfLane(lane), join);

        index.Begin();
        index.Add(join, 0f, MathF.Min(4f, index.WayLengthM(join)), 5f, occupant: 1, LaneUse.Reserved);

        Assert.True(index.AheadBody(join, 0f, index.WayLengthM(join), LaneOccupancy.Nobody, out _));
        Assert.False(index.AheadBody(index.WayOfLane(lane), 0f, 60f, LaneOccupancy.Nobody, out _));
    }

    /// <summary>
    /// A lane long enough to hold the stretches these tests lay, whose first way out is a join with metres
    /// of its own — a place cut into a road joins its two lanes at a point (GEN-4h), and a way of no length
    /// is nothing to put a body on.
    /// </summary>
    static int FirstLongLane(RoadGraph roads, float atLeastM)
    {
        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            if (roads.LaneLengthM[lane] < atLeastM || roads.TurnsFrom(lane).Length == 0) continue;
            if (roads.JoinLengthM(roads.TurnSlotAt(lane, 0)) <= 0f) continue;

            return lane;
        }

        throw new InvalidOperationException($"the fixture town has no lane {atLeastM} m long with a join out of it");
    }
}

/// <summary>
/// The same index asked of a running town: that it actually describes one, and that a driver reads what
/// is in front of it off the fleet rather than off a ray.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class LaneOccupancyInATownTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// <b>Somebody in a lane cuts the road a driver is granted</b> (TER-4c), which is a fact about the order
    /// the book is laid in and not about the arithmetic: the walkers go in between the cars' asks and the
    /// cars' grants, so a band is in the book before any grant is taken off it.
    /// </summary>
    /// <remarks>
    /// Laid last instead, every band went into a book that was wiped before the next grant was taken and no
    /// driver ever read one while deciding how much road it had. Nothing in the arithmetic said so — the
    /// walkers were in the book, on the right ways, at the right metres, and one pass too late.
    /// </remarks>
    [Fact]
    public void ADriverIsCutByTheWalkersInItsLane()
    {
        var world = new TownWorld(Towns.Of("Odesa"), Config);
        var loop = new SimLoop<TownWorld>(world, Config);

        var cutByAWalker = 0;
        for (var tick = 0; tick < TicksWatched; tick++)
        {
            loop.Advance();
            for (var car = 0; car < world.Cars.Count; car++)
            {
                if (world.Cars.GrantCutBy[car] == HeadwayKind.Walker) cutByAWalker++;
            }
        }

        Assert.True(cutByAWalker > 0, "no driver in a minute of a busy town was ever cut by somebody on foot");
    }

    /// <summary>
    /// <b>Every driver on a route is in the book.</b> A car the index has not placed is a car nobody behind
    /// it can tell from a wreck, which is the one misreading this whole index exists to remove.
    /// </summary>
    [Fact]
    public void EveryDriverOnItsOwnRouteIsInTheIndex()
    {
        var world = Run("Odesa");

        var onARoute = 0;
        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (world.Cars.Driven[car] && !world.Cars.Broken[car] && world.Cars.Line[car].LaneCount > 0) onARoute++;
        }

        Assert.True(onARoute > 0, "no car in a busy town was driving a route");
        Assert.True(
            world.Occupancy.SlotCount >= onARoute,
            $"{onARoute} cars were on a route and the index held {world.Occupancy.SlotCount} stretches");
    }

    /// <summary>
    /// <b>A driver stopped behind another driver reads it as a queue and not as an obstruction.</b> This is
    /// the whole scenario: whatever holds the car at the head of a queue is not this car's to drive round,
    /// and a town where it were would be a town of cars overtaking each other at every red.
    /// </summary>
    [Fact]
    public void NobodyReadsAQueueAsSomethingToGetPast()
    {
        var world = Run("Odesa");

        var queued = 0;
        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (world.Cars.Driven[car] && world.Cars.Context[car].Ahead == HeadwayKind.Queue) queued++;
        }

        Assert.True(queued > 0, "no car in a busy town was queueing behind another");

        // And the reading can only be that, because it is the book's own placement: what a driver is told
        // is `KindOf` the stretch in front of it, so an obstruction is a car the book laid as one. An
        // obstruction is allowed and is what `E-4` acts on — it may never be a live driver on its own line.
        Span<LaneSlot> slots = stackalloc LaneSlot[64];
        foreach (var way in world.Occupancy.OccupiedWays)
        {
            var count = world.Occupancy.CopyTo(way, slots);
            for (var slot = 0; slot < count; slot++)
            {
                if (slots[slot].Use != LaneUse.Obstruction) continue;
                if (slots[slot].Of != LaneRoster.Driving || slots[slot].Occupant < 0) continue;

                var other = slots[slot].Occupant;
                Assert.False(
                    world.Cars.Driven[other]
                    && !world.Cars.Broken[other]
                    && world.Cars.Line[other].LaneCount > 0
                    && world.Cars.OffLineM[other] <= Config.CarOffPathM * OnItsLineTolerance,
                    $"live driver {other} was laid in the book as an obstruction");
            }
        }
    }

    /// <summary>
    /// <b>A body that is not driving a route still holds the ground it cannot stop short of</b> (TER-4c.1)
    /// — an obstruction is a reservation that generally reaches nowhere, and not a stretch of a different
    /// kind. Standing still it is the body and no more; shoved down a lane at speed it is the body and the
    /// road that speed takes to shed, which is the ground the traffic behind must not be granted.
    /// </summary>
    /// <remarks>
    /// <b>The two readings are one arithmetic and that is the point.</b> Held to its footprint whatever it
    /// was doing, a car knocked down a lane by a collision handed the driver behind it the metres it was
    /// about to be standing on — and the faster it was travelling, the more of them.
    /// </remarks>
    [Theory]
    [InlineData(0f)]
    [InlineData(6f)]
    [InlineData(14f)]
    public void ABodyOffItsRouteHoldsTheRoadItsSpeedStillNeeds(float alongMps)
    {
        // The lap, whose fleet is on the road rather than in bays: a car a bay holds is laid at that bay's
        // own extent instead (<c>LieInTheBay</c>), which is the one body this arithmetic is not asked of.
        var world = new TownWorld(Towns.Of("Fleet"), Config);
        new SimLoop<TownWorld>(world, Config).Advance(600);

        // A lane long enough that the whole stretch lands inside it, so nothing under test is clipped at
        // either end of the way (<see cref="LaneOccupancy.Add"/>).
        var lane = 0;
        for (var at = 0; at < world.Roads.LaneCount; at++)
        {
            if (world.Roads.LaneLengthM[at] > world.Roads.LaneLengthM[lane]) lane = at;
        }

        var arcs = world.Roads.ArcsOf(lane);
        var midM = world.Roads.LaneLengthM[lane] * 0.5f;
        var on = Spline.SampleAt(arcs, midM);

        // A body the road is not driving: nobody in it and broken, which is also what keeps it off a
        // template — a sweep is committed ground already laid, and taking both would count it twice.
        // <b>And one no bay holds</b>: a car a bay has is laid at the bay's own exact extent instead
        // (<c>LieInTheBay</c>), which is the one body in the town this arithmetic is not asked of.
        var car = -1;
        for (var at = 0; at < world.Cars.Count && car < 0; at++)
        {
            if (world.Parking.BayOf(at) < 0) car = at;
        }

        Assert.True(car >= 0, "every car in the town was held by a bay");

        world.Cars.Driven[car] = false;
        world.Cars.Broken[car] = true;
        world.Cars.PositionM[car] = on.PositionM;
        world.Cars.VelocityMps[car] = Heading.Unit(on.HeadingRad) * alongMps;
        world.RebuildProximityIndex();

        Span<LaneSlot> slots = stackalloc LaneSlot[32];
        var count = world.Occupancy.CopyTo(world.Occupancy.WayOfLane(lane), slots);

        var found = false;
        for (var at = 0; at < count; at++)
        {
            if (slots[at].Occupant != car || slots[at].Use != LaneUse.Obstruction) continue;
            if (slots[at].Of != LaneRoster.Driving) continue;

            found = true;
            var wantedM = MathF.Max(
                0f,
                alongMps * alongMps
                / (2f * CarFollower.BrakingMps2(
                    Config, world.Cars.BuildOf(car), world.Cars.GroundCoefficient[car])));

            Assert.Equal(wantedM, slots[at].ToM - slots[at].StandsToM, 2);

            // And the body itself is where it always was: what the speed buys is ground past the body and
            // never a longer body (TER-5c.2).
            Assert.Equal(
                world.Cars.BuildOf(car).LengthM, slots[at].StandsToM - slots[at].FromM, 2);
        }

        Assert.True(found, $"a body left in lane {lane} was in nobody's book");
    }

    /// <summary>
    /// <b>A car driving a template of its own holds the ground that template has still to sweep</b>
    /// (TER-4c.1), and not merely the pose it is passing through. A recovery straight was walked before it
    /// was laid and then left open, so the road it was drawn through read free to everybody else: another
    /// driver came to rest in it, and the car reversed into that driver at manoeuvring pace.
    /// </summary>
    /// <remarks>
    /// <b>Asked of the ground and not of the book's arithmetic</b> — the same walk the desk takes before it
    /// lays a template at all (<see cref="GroundAhead"/>), which is what has to answer differently. A sweep
    /// that ends off the network is nobody's ground and holds nothing, so what is watched here is the ends
    /// that stand on a lane.
    /// <para>
    /// <b>A line that is one of the town's own ways is not a template and is left out</b> (<c>CarFleet.LineWay</c>).
    /// It holds its ground as a reservation on that way, and the traffic on the lane it crosses is cut by
    /// looking the table up rather than by finding a stretch of its own lane taken — which is the rule a
    /// body writes only the ways it will be on (TER-5c.1), and is exactly what the sweep could not do.
    /// </para>
    /// <para>
    /// <b>From the tick after the template is laid</b>, because the book is rebuilt from the bodies in phase
    /// 2 and a manoeuvre lays its line in phase 3. The tick a template is drawn on is the one the desk's own
    /// walk answered for, and it is the only tick in the life of the line that this does not.
    /// </para>
    /// <para>
    /// <b>Asked of the drunks' lap and not of a city</b>, because the entries that lay a template <em>over a
    /// lane</em> are the two reactive ones — the swerve and the back-off — and a city goes minutes at a time
    /// without either. The drunks' lap exists to produce them and produces a dozen a minute; a city produces
    /// them when it happens to jam, which is not a thing to hang a claim on.
    /// </para>
    /// </remarks>
    [Fact]
    public void ATemplateHoldsTheGroundItHasStillToSweep()
    {
        var world = new TownWorld(Towns.Of(TrackPlan.DrunkName), Config);
        var loop = new SimLoop<TownWorld>(world, Config);

        // Where each car's template ended at the tick before, so that a line drawn since the rebuild this
        // reading is taken from is not asked about: the book is laid in phase 2 and a manoeuvre draws its
        // line in phase 3, and the tick a template is drawn on is the one the desk's own walk answered for.
        var endedAtM = new Vector2[world.Cars.Count];
        var stood = new bool[world.Cars.Count];

        var swept = 0;
        for (var tick = 0; tick < TicksWatched; tick++)
        {
            loop.Advance();
            for (var car = 0; car < world.Cars.Count; car++)
            {
                var line = world.Cars.Line[car];
                if (line.ArcCount == 0 || line.LaneCount > 0 || !world.Cars.Driven[car] || world.Cars.Broken[car]
                    || world.Cars.LineWayOf(car) != CarFleet.NoWay)
                {
                    stood[car] = false;
                    continue;
                }

                // Where the body will be and not where the line ends: a template is drawn for the rear axle,
                // and the axle at the end of one stands a metre and a half short of the middle of the car —
                // which at the mouth of a lane is a different way of the town altogether.
                ref readonly var build = ref world.Cars.BuildOf(car);
                var halfWidthM = build.FlankM;
                var at = Spline.SampleAt(world.Cars.LineArcsOf(car)[..line.ArcCount], line.LengthM);
                var forward = Heading.Unit(at.HeadingRad);
                var endM = at.PositionM
                           + ((world.Cars.LineIsReverse[car] ? -forward : forward) * build.CentreAheadOfAxleM);

                var wasThere = stood[car] && (endM - endedAtM[car]).Length() <= 1e-3f;
                endedAtM[car] = endM;
                stood[car] = true;
                if (!wasThere) continue;

                // And far enough off that the body where it stands cannot be what covers it.
                if (line.LengthM - world.Cars.ProgressM[car] <= build.LengthM) continue;

                var lane = world.Roads.NearestLane(endM, out var alongM);
                if (lane < 0
                    || !RoadGraph.WithinTheBand(
                        world.Roads.ArcsOf(lane), alongM, endM, world.Roads.LaneWidthM[lane], halfWidthM,
                        halfWidthM, out _))
                {
                    continue;
                }

                swept++;
                Assert.True(
                    GroundAhead.TakenAt(
                        world.Roads, world.Occupancy, endM, halfWidthM, LaneOccupancy.Nobody, out _),
                    $"car {car} is committed to ground at the end of its template that the book calls free "
                    + $"(tick {tick}, {world.Cars.Doing[car]}, progress {world.Cars.ProgressM[car]:0.00} of "
                    + $"{line.LengthM:0.00} m, ending on lane {lane} at {alongM:0.00} m)");
            }
        }

        Assert.True(swept > 0, "no car in a minute of a busy town was driving a template over a lane");
    }

    /// <summary>
    /// <b>Nobody holds one metre of one way twice.</b> A body and the road it has taken are one stretch read
    /// to two edges, so an occupant lying over itself is a thing every walk of a way counts as two
    /// occupants and the overlay draws as two washes over one piece of ground.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A claim is held to it like anything else, and it is the case that bites.</b> A claim is ground its
    /// owner is not on yet — the far end of a box it has committed to, the lane it is backing onto — so it
    /// may stand <em>beside</em> a body's own reservation on the same way and must never run back over it.
    /// Laid from the crossing table without regard to how far the car's own road had got, it did exactly
    /// that: on a join a driver was inside, the reservation and the claim covered the same metres.
    /// </para>
    /// <para>
    /// Both books, because both are laid the same way from their own bodies: a walker's ask begins at its
    /// back exactly as a driver's begins at its tail.
    /// </para>
    /// </remarks>
    [Fact]
    public void NobodyHoldsTwoStretchesOfOneWay()
    {
        var world = Run("Odesa");

        NobodyIsLaidTwice(world.Occupancy, "the road");
        NobodyIsLaidTwice(world.Footfall, "the pavement");
    }

    static void NobodyIsLaidTwice(LaneOccupancy book, string called)
    {
        Span<LaneSlot> slots = stackalloc LaneSlot[64];
        foreach (var way in book.OccupiedWays)
        {
            var count = book.CopyTo(way, slots);
            for (var one = 0; one < count; one++)
            {
                for (var other = one + 1; other < count; other++)
                {
                    if (slots[one].Occupant != slots[other].Occupant
                        || slots[one].Of != slots[other].Of
                        || slots[one].Occupant == LaneOccupancy.Nobody)
                    {
                        continue;
                    }

                    Assert.False(
                        slots[one].ToM > slots[other].FromM && slots[one].FromM < slots[other].ToM,
                        $"{called}: {slots[one].Of} {slots[one].Occupant} holds both "
                        + $"{slots[one].FromM:0.00}–{slots[one].ToM:0.00} m ({slots[one].Use}) and "
                        + $"{slots[other].FromM:0.00}–{slots[other].ToM:0.00} m ({slots[other].Use}) "
                        + $"of way {way}");
                }
            }
        }
    }

    /// <summary>
    /// <b>Two bodies are never granted one metre</b> (TER-4c.1). Ground is asked for, answered and then it is
    /// the asker's, so the ground one body holds ends where the next body's begins and never inside it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the book and not the arithmetic that has to say so.</b> What a car is granted was worked out
    /// correctly all along and written to <c>CarFleet.AuthorityM</c>; what went into the book was the ask,
    /// which is bounded by the rules that stop the car and by nothing in front of it. Every reader of the
    /// book after the rebuild — the junction gate above all — therefore read one car as holding road it had
    /// been refused, and refused the crossing traffic by it
    /// (<see cref="TownWorld.CutTheGroundToTheGrant"/>).
    /// </para>
    /// <para>
    /// <b>Told at the widest overlap and not at the first</b>: a millimetre of float is not a finding, and
    /// what says whether a mechanism is wrong or a number is loose is how far in the worst of them reaches.
    /// </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NoTwoBodiesAreGrantedOneMetre(string map)
    {
        var world = Run(map);

        var held = 0;
        var worstM = 0f;
        var told = string.Empty;
        Span<LaneSlot> slots = stackalloc LaneSlot[64];
        foreach (var way in world.Occupancy.OccupiedWays)
        {
            var count = world.Occupancy.CopyTo(way, slots);
            for (var one = 0; one < count; one++)
            {
                if (slots[one].Use != LaneUse.Reserved) continue;

                held++;
                for (var other = one + 1; other < count; other++)
                {
                    if (slots[other].Use != LaneUse.Reserved) continue;
                    if (slots[one].Occupant == slots[other].Occupant && slots[one].Of == slots[other].Of)
                    {
                        continue;
                    }

                    var overlapM = MathF.Min(slots[one].ToM, slots[other].ToM)
                                   - MathF.Max(slots[one].FromM, slots[other].FromM);
                    if (overlapM <= worstM) continue;

                    worstM = overlapM;
                    told = $"{slots[one].Of} {slots[one].Occupant} holds "
                           + $"{slots[one].FromM:0.00}–{slots[one].ToM:0.00} m of way {way} and "
                           + $"{slots[other].Of} {slots[other].Occupant} holds "
                           + $"{slots[other].FromM:0.00}–{slots[other].ToM:0.00} m of it";
                }
            }
        }

        Assert.True(
            worstM <= Tolerance, $"{map}: two bodies were granted {worstM:0.00} m of one way — {told}");

        // The census, without which the claim above is kept by a book with nothing in it. A map nobody
        // drives on has nothing to hold: a scenario laid to watch pedestrians is one.
        var driving = 0;
        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (world.Cars.Driven[car]) driving++;
        }

        Assert.True(held > 0 || driving == 0, $"{map}: {driving} cars are driving and not one holds any road");
    }

    /// <summary>
    /// <b>A car nothing is in front of is held by nobody.</b> Its own ask comes back to it whole, and a
    /// grant handed back as the length of that ask would read as the car queueing behind itself — which is
    /// a car alone on an empty street driving as though there were a jam on it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Asked the other way round: a cut grant must have had something to be cut at.</b> Every body the
    /// book holds is inside the longest reservation this town can write, so a car cut with nobody anywhere
    /// near it was cut by its own ask. Asked as "clear ⇒ uncut" it needed a car with nothing at all inside
    /// that reservation — which on the proving ground is a car three hundred metres clear of six cars and
    /// fifteen people on one lap, and never happens.
    /// </para>
    /// <para>
    /// <b>The people count</b> (TER-4c): somebody standing in a lane cuts the road a driver is granted
    /// exactly as a car standing there would, so they are as much an answer to <em>what could have cut this</em>
    /// as the traffic is.
    /// </para>
    /// </remarks>
    [Fact]
    public void ACarWithTheRoadToItselfIsHeldByNobody()
    {
        var world = new TownWorld(Towns.Of(TrackPlan.Name), Config);
        var loop = new SimLoop<TownWorld>(world, Config);

        var uncut = 0;
        for (var tick = 0; tick < TicksWatched; tick++)
        {
            loop.Advance();
            for (var car = 0; car < world.Cars.Count; car++)
            {
                if (!world.Cars.Driven[car] || world.Cars.Line[car].LaneCount == 0) continue;

                if (float.IsPositiveInfinity(world.Cars.AuthorityM[car]))
                {
                    uncut++;
                    Assert.NotEqual(DrivingHold.Reserved, world.Cars.Hold[car]);
                    continue;
                }

                var nearestM = NearestOtherM(world, car);
                Assert.True(
                    nearestM <= ClearOfEverybodyM,
                    $"car {car} was cut to {world.Cars.AuthorityM[car]:0.0} m with the nearest body "
                    + $"{nearestM:0.0} m away, which is further than any reservation this town writes "
                    + $"({ClearOfEverybodyM:0.0} m)");
            }
        }

        Assert.True(uncut > 0, "not one car on the proving ground was ever granted its whole ask");
    }

    /// <summary>
    /// How near the nearest other body is, which is what says whether anything could have cut this one.
    /// <b>The people as well as the cars</b>: somebody standing in a lane cuts the road a driver is granted
    /// exactly as a car standing there would (TER-4c), and the proving ground has fifteen of them pacing
    /// across it.
    /// </summary>
    static float NearestOtherM(TownWorld world, int car)
    {
        var atM = world.Cars.PositionM[car];
        var nearestM = float.PositiveInfinity;
        for (var other = 0; other < world.Cars.Count; other++)
        {
            if (other == car) continue;

            nearestM = MathF.Min(nearestM, (world.Cars.PositionM[other] - atM).Length());
        }

        for (var person = 0; person < world.People.Count; person++)
        {
            nearestM = MathF.Min(nearestM, (world.People.PositionM[person] - atM).Length());
        }

        return nearestM;
    }

    /// <summary>
    /// The longest stretch this town can ever write into the book: a reaction interval at the gear's own
    /// cap, a stop from there, and the body and the margin it keeps at either end of itself. Nothing
    /// further away than this can have cut anybody.
    /// </summary>
    /// <remarks>
    /// <b>Taken over the whole fleet and not off the nominal car</b> (CAR-11): the cars in a town are the
    /// ones it is drawn with, and the bound has to hold for the fastest and the longest of them.
    /// </remarks>
    static float ClearOfEverybodyM
    {
        get
        {
            var builds = CarBuilds.OfTheFleet(Config, CarCatalog.Shared);
            var mostM = 0f;
            for (var variant = 0; variant < CarCatalog.Shared.SheetCount; variant++)
            {
                ref readonly var build = ref builds.Of(variant);
                mostM = MathF.Max(
                    mostM,
                    (build.MaxSpeedMps * Config.CarReactionS)
                    + (build.MaxSpeedMps * build.MaxSpeedMps / (2f * CarFollower.BrakingMps2(Config, build, 1f)))
                    + build.LengthM + build.BodyMarginM + build.TailMarginM);
            }

            return mostM;
        }
    }

    /// <summary>
    /// <b>Nobody holds road it could not have driven over.</b> A reservation is the ground the car is
    /// committed to — one reaction interval at the fastest that interval can leave it doing, and a stop
    /// from there — and never the ground the speed it is driving towards would eventually need. A car
    /// holding what its top speed would take is a street shut to everybody behind it at a third of that
    /// speed.
    /// </summary>
    /// <remarks>
    /// The ceiling is the car's own figures and takes no notice of what the profile planned, because the
    /// plan can only lower the ask: whatever the driver is aiming at, full throttle for a reaction interval
    /// is the whole of what it can commit itself to in one.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NobodyHoldsRoadItCouldNotHaveDrivenOver(string map)
    {
        var world = Run(map);

        var asked = 0;
        var driving = 0;
        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (world.Cars.Driven[car]) driving++;

            // The road in front of the nose, less the margin the car keeps at either end of itself: what is
            // being asked about is the road it committed to and not the ground it stands in.
            ref readonly var build = ref world.Cars.BuildOf(car);
            var noseM = world.Cars.ReserveFromM[car] + build.TailMarginM + build.LengthM;
            var wantedM = world.Cars.ReserveToM[car] - noseM - build.BodyMarginM;
            if (wantedM <= 0f) continue;

            asked++;

            // The speed at the rebuild and not the speed now: the book was laid at the top of this tick and
            // the body has been driven since, so a car that stood on the brakes in between reads back a tick
            // of braking slower than the ask was sized at.
            var brakingMps2 = CarFollower.BrakingMps2(Config, build, world.Cars.GroundCoefficient[car]);
            var reachableMps = world.Cars.AlongMps[car]
                               + (build.BrakingMps2 * Config.TickSeconds)
                               + (build.AccelerationMps2 * Config.CarReactionS);

            var committedM = (reachableMps * Config.CarReactionS)
                             + (reachableMps * reachableMps / (2f * brakingMps2));

            Assert.True(
                wantedM <= committedM + Tolerance,
                $"{map}: car {car} holds {wantedM:0.0} m of road at {world.Cars.AlongMps[car]:0.0} m/s, "
                + $"where all it is committed to is {committedM:0.0} m");
        }

        // A map nobody is driving on has nothing to hold: a scenario laid to watch pedestrians is one.
        Assert.True(asked > 0 || driving == 0, $"{map}: {driving} cars are driving and not one asked for any road");
    }

    /// <summary>Ground on a way is metres, and a grant is arithmetic on floats: a millimetre is not a finding.</summary>
    const float Tolerance = 1e-2f;

    /// <summary>The bar the road itself holds a car to before it calls the line lost, which is what the index places by.</summary>
    const float OnItsLineTolerance = 2f;

    public static TheoryData<string> Maps => Towns.EveryTown();

    static readonly ConcurrentDictionary<string, TownWorld> Ran = new();

    /// <summary>
    /// <b>The town a minute in, taken once per map and read by every claim that asks about the same
    /// moment.</b> Nothing here writes to the world it is handed — what these ask of is a finished state,
    /// which is one run of the town however many questions are put to it.
    /// </summary>
    /// <remarks>
    /// A claim that has to watch the ticks go by stands its own world (<see
    /// cref="ATemplateHoldsTheGroundItHasStillToSweep"/>), because what it is about is the ticks and not
    /// the state they arrive at.
    /// </remarks>
    static TownWorld Run(string map) => Ran.GetOrAdd(map, opened =>
    {
        var world = new TownWorld(Towns.Of(opened), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(TicksWatched);
        return world;
    });

    /// <summary>A minute of town, which is long enough for every kind of hold to have happened on every map.</summary>
    const int TicksWatched = 3_600;
}
