using System.Collections.Concurrent;
using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// The lane index as arithmetic: stretches go in, the nearest one in front comes out, and a rebuild
/// leaves nothing of the tick before it.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P1)]
public class LaneOccupancyTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static LaneOccupancy Index(out RoadGraph roads, int mostSlots = 16)
    {
        roads = RoadGraph.Build(Towns.Of("Test"), Config);
        return new LaneOccupancy(TownWays.OfTheRoad(roads), mostSlots);
    }

    /// <summary>
    /// <b>What a body covers of a way is the part of its box inside that way's band</b> (TER-4c.2), at every
    /// angle it can meet the band at and every distance it can stand off it — checked against the box itself,
    /// walked corner to corner.
    /// </summary>
    /// <remarks>
    /// <b>The shadow of the whole box is the wrong answer everywhere but square on.</b> A body at an angle
    /// casts its own length down a way it reaches by a corner, so the two agree only when the box is entirely
    /// inside the band — which is exactly the case a reading taken from the shadow was written for.
    /// </remarks>
    [Theory]
    [InlineData(0f)]
    [InlineData(15f)]
    [InlineData(30f)]
    [InlineData(45f)]
    [InlineData(70f)]
    [InlineData(90f)]
    public void ABoxCoversOfABandTheCornersOfItThatAreInside(float turnedDeg)
    {
        const float halfLengthM = 2f;
        const float flankM = 1f;
        const float halfBandM = 1.8f;
        var box = new BodyFootprint(halfLengthM, flankM, Heading.Unit(turnedDeg * MathF.PI / 180f));

        for (var acrossM = -6f; acrossM <= 6f; acrossM += 0.1f)
        {
            var offsetM = new Vector2(0f, acrossM);
            var inside = box.CoversOn(Vector2.UnitX, offsetM, halfBandM, out var backM, out var aheadM);
            Walked(box, offsetM, halfBandM, out var leastM, out var mostM, out var anyInside);

            Assert.Equal(anyInside, inside);
            if (!anyInside) continue;

            // The walk is a grid over the box, so it lands inside the true run rather than on its ends: it
            // may fall a step short of each, and never past either.
            Assert.InRange(backM, leastM - Step, leastM + Step);
            Assert.InRange(aheadM, mostM - Step, mostM + Step);
        }
    }

    /// <summary>How coarsely <see cref="Walked"/> samples the box, and so how near its answer can be trusted.</summary>
    const float Step = 0.02f;

    /// <summary>
    /// The same run arrived at by walking the box corner to corner: the least and greatest metre along the
    /// line of every point of it that is inside the band. <b>The answer this is checked against</b>, which is
    /// why it is a grid and not a second clip.
    /// </summary>
    static void Walked(
        in BodyFootprint box, Vector2 offsetM, float halfBandM, out float leastM, out float mostM,
        out bool any)
    {
        leastM = float.PositiveInfinity;
        mostM = float.NegativeInfinity;
        any = false;

        var flank = Heading.RightOf(box.Forward);
        for (var alongTheBody = -box.HalfLengthM; alongTheBody <= box.HalfLengthM; alongTheBody += Step)
        {
            for (var acrossTheBody = -box.FlankM; acrossTheBody <= box.FlankM; acrossTheBody += Step)
            {
                var pointM = offsetM + (box.Forward * alongTheBody) + (flank * acrossTheBody);
                if (MathF.Abs(pointM.Y) > halfBandM) continue;

                any = true;
                leastM = MathF.Min(leastM, pointM.X);
                mostM = MathF.Max(mostM, pointM.X);
            }
        }
    }

    /// <summary>The order stretches go in is not the order they are read back in — the near edge is.</summary>
    [Fact]
    public void TheNearestBodyInFrontIsTheOneWithTheLeastNearEdge()
    {
        var index = Index(out var roads);
        var way = index.Ways.OfRoadLane(FirstLongLane(roads, 60f));

        index.Begin();
        index.ClaimUnderWay(way, 40f, 44f, 44f, 3f, 7);
        index.ClaimUnderWay(way, 12f, 16f, 16f, 0f, 3);
        index.ClaimWhereItStands(way, 25f, 29f, 29f, 1f, 5);

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
        var way = index.Ways.OfRoadLane(FirstLongLane(roads, 60f));

        index.Begin();
        index.ClaimUnderWay(way, 10f, 14f, 14f, 0f, 1);
        index.ClaimUnderWay(way, 30f, 34f, 34f, 0f, 2);

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
        var way = index.Ways.OfRoadLane(FirstLongLane(roads, 60f));

        index.Begin();
        index.ClaimAhead(way, 10f, 14f, 0f, 1, ClaimPriority.Firm);
        index.ClaimUnderWay(way, 30f, 34f, 34f, 0f, 2);

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
        var way = index.Ways.OfRoadLane(FirstLongLane(roads, 60f));
        var asker = new LaneCredit(2f, LaneRoster.Driving, RightOfWay.Traffic);

        // The car queueing behind for the same movement, claiming the run through the body in front of it.
        index.Begin();
        index.ClaimAhead(way, 10f, 30f, 0f, 1, ClaimPriority.Firm);

        Assert.Equal(
            float.PositiveInfinity, index.GrantedOn(way, 20f, 60f, occupant: 2, asker, out _));

        // From behind its near edge the same claim is a place to be stopped a margin short of.
        Assert.Equal(8f, index.GrantedOn(way, 5f, 60f, occupant: 2, asker, out _), 3);

        // And a body reaching back past the asker is a contact, which the grant is left to report.
        index.Begin();
        index.ClaimUnderWay(way, 10f, 30f, 30f, 0f, 1);
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
        var way = index.Ways.OfRoadLane(FirstLongLane(roads, 60f));

        index.Begin();
        index.ClaimUnderWay(way, 20f, 26f, 26f, 0f, 4, of: LaneRoster.Walking);

        // The ground it stands on is spoken for, so no driver is granted the road through it.
        var at = LaneOccupancy.FromTheStart;
        Assert.True(index.NextHeld(way, 0f, 60f, LaneOccupancy.Nobody, ref at, out var taken));
        Assert.Equal(LaneRoster.Walking, taken.Of);
        Assert.Equal(20f, taken.FromM);

        // And it is a body in front to be stopped short of.
        Assert.True(index.AheadBody(way, 0f, 60f, LaneOccupancy.Nobody, out var body));
        Assert.Equal(LaneRoster.Walking, body.Of);

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
    /// <b>Ground somebody is waiting for is claimed and is in nothing else</b> (TER-5e). It is the ask a
    /// walker at a kerb was refused: no grant is cut at it, nobody reads it as a body or as traffic, and the
    /// one question it answers is the one a driver approaching that paint asks.
    /// </summary>
    /// <remarks>
    /// <b>Both halves are the point.</b> Unclaimed, a right of way nobody can see is not one (TER-4c);
    /// in it as a body, a car that could not stop at the kerb line brakes as hard as it can for somebody
    /// still on the pavement.
    /// </remarks>
    [Fact]
    public void GroundSomebodyIsWaitingForIsSeenAndCutsNothing()
    {
        var index = Index(out var roads);
        var way = index.Ways.OfRoadLane(FirstLongLane(roads, 60f));

        index.Begin();
        index.ClaimAhead(way, 20f, 26f, 0f, 4, ClaimPriority.Rejected, LaneRoster.Walking, RightOfWay.OnThePaint);

        Assert.True(index.AnybodyWaitingFor(way, 23f, 23f));
        Assert.False(index.AnybodyWaitingFor(way, 30f, 40f));

        var at = LaneOccupancy.FromTheStart;
        Assert.False(index.NextHeld(way, 0f, 60f, LaneOccupancy.Nobody, ref at, out _));
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
        var claim = new LaneClaim(
            0f, 6f, 0f, 0f, 1, ClaimPriority.Firm, Right: RightOfWay.TurningAcross);
        Assert.False(LaneOccupancy.Binds(claim, RightOfWay.StraightOn));
        Assert.True(LaneOccupancy.Binds(claim, RightOfWay.TurningAcross));

        // The same ground held by a car that can no longer stop short of the box it is entering.
        Assert.True(
            LaneOccupancy.Binds(claim with { Priority = ClaimPriority.Hard }, RightOfWay.StraightOn));

        // And a body, which is not a rank's to take at any strength.
        var body = claim with { StandsToM = 6f, Priority = ClaimPriority.Hard };
        Assert.True(LaneOccupancy.Binds(body with { OnItsLine = true }, RightOfWay.StraightOn));
        Assert.True(LaneOccupancy.Binds(body, RightOfWay.StraightOn));
        Assert.True(LaneOccupancy.Binds(body with { Of = LaneRoster.Walking }, RightOfWay.StraightOn));
    }

    /// <summary>
    /// <b>A soft claim is taken by a stronger movement and not by an equal one</b> (TER-5g) — the one
    /// place the two revocable tiers are compared differently, because a tie that refused both would leave
    /// two movements each waiting on ground the other was only thinking about.
    /// </summary>
    [Fact]
    public void ASoftClaimIsTakenByAStrongerMovementAndNotByAnEqualOne()
    {
        var stated = new LaneClaim(0f, 6f, 0f, 0f, 1, ClaimPriority.Soft);

        Assert.False(LaneOccupancy.Binds(stated, RightOfWay.StraightOn));
        Assert.False(LaneOccupancy.Binds(stated, RightOfWay.Traffic));
        Assert.True(LaneOccupancy.Binds(stated, RightOfWay.TurningAcross));

        // Where the same ground granted refuses the equal rank as well.
        Assert.True(
            LaneOccupancy.Binds(stated with { Priority = ClaimPriority.Firm }, RightOfWay.Traffic));
    }

    /// <summary>
    /// <b>What a claim is is read off its own edges</b> (TER-5g): whether a body is standing in it, whether
    /// it is the town's own furniture, and whether it is one a walker steps round — none of it a tag carried
    /// beside the numbers that decide it.
    /// </summary>
    [Fact]
    public void WhatAClaimIsIsReadOffItsOwnEdges()
    {
        var stated = new LaneClaim(0f, 6f, 0f, 0f, 1, ClaimPriority.Soft);
        Assert.False(stated.HasBody);
        Assert.True(stated.IsStated);
        Assert.False(stated.IsGranted);

        var granted = stated with { Priority = ClaimPriority.Firm };
        Assert.True(granted.IsGranted);
        Assert.False(granted.HasBody);

        var body = new LaneClaim(0f, 6f, 6f, 0f, 1, ClaimPriority.Hard);
        Assert.True(body.HasBody);
        Assert.True(body.IsLoose);
        Assert.False((body with { OnItsLine = true }).IsLoose);
        Assert.False(body.IsFurniture);
        Assert.True((body with { Occupant = LaneOccupancy.Nobody }).IsFurniture);
        Assert.False((body with { Occupant = LaneOccupancy.Nobody }).IsLoose);
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
        var over = new LaneClaim(0f, 6f, 6f, 0f, 1, ClaimPriority.Hard, OnItsLine: true);

        Assert.False(LaneOccupancy.TakesAClaim(over, mine));
        Assert.False(LaneOccupancy.TakesAClaim(over with { OnItsLine = false }, mine));
        Assert.False(LaneOccupancy.TakesAClaim(over with { Of = LaneRoster.Walking }, mine));
        Assert.False(LaneOccupancy.TakesAClaim(over with { Occupant = LaneOccupancy.Nobody }, mine));

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
        Assert.Equal(RightOfWay.Traffic, LaneClaim.Nothing.Right);
    }

    /// <summary>
    /// <b>Two cars that each found the lane clear on the same tick must not both take it.</b> It is the
    /// junction registry's argument applied to a stretch of lane, and the whole of what makes a claim
    /// binding rather than a note.
    /// </summary>
    [Fact]
    public void GroundSomebodyHasClaimedIsRefusedToTheNextAsker()
    {
        var index = Index(out var roads);
        var way = index.Ways.OfRoadLane(FirstLongLane(roads, 60f));

        index.Begin();
        index.ClaimAhead(way, 20f, 26f, 0f, 1, ClaimPriority.Firm);

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
        var way = index.Ways.OfRoadLane(FirstLongLane(roads, 60f));

        index.Begin();
        index.ClaimUnderWay(way, 10f, standsToM: 14f, toM: 38f, 8f, occupant: 1);
        index.ClaimWhereItStands(way, 45f, 49f, 49f, 0f, 2);

        // One entry and not two: the car is laid once, so a walk of what is spoken for passes from its road
        // straight to the wreck beyond it.
        var at = LaneOccupancy.FromTheStart;
        Assert.True(index.NextHeld(way, 0f, 60f, LaneOccupancy.Nobody, ref at, out var found));
        Assert.True(found.HasBody && found.OnItsLine);
        Assert.Equal(10f, found.FromM);
        Assert.Equal(38f, found.ToM);

        Assert.True(index.NextHeld(way, 0f, 60f, LaneOccupancy.Nobody, ref at, out found));
        Assert.Equal(45f, found.FromM);
        Assert.False(index.NextHeld(way, 0f, 60f, LaneOccupancy.Nobody, ref at, out _));

        // And a driver's own stretch is never what it is cut at.
        at = LaneOccupancy.FromTheStart;
        Assert.True(index.NextHeld(way, 0f, 60f, excluding: 1, ref at, out found));
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
        var way = index.Ways.OfRoadLane(FirstLongLane(roads, 60f));

        index.Begin();
        index.ClaimUnderWay(way, 4f, standsToM: 8f, toM: 50f, 20f, occupant: 1);

        var behind = LaneOccupancy.FromTheStart;
        Assert.False(index.NextHeld(way, 20f, 60f, LaneOccupancy.Nobody, ref behind, out _));

        var ahead = LaneOccupancy.FromTheStart;
        Assert.True(index.NextHeld(way, 0f, 60f, LaneOccupancy.Nobody, ref ahead, out _));

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
        var way = index.Ways.OfRoadLane(FirstLongLane(roads, 60f));

        index.Begin();
        index.ClaimWhereItStands(way, 8f, 14f, 14f, 0f, 1);

        Assert.True(index.AheadBody(way, 10f, 60f, LaneOccupancy.Nobody, out var found));
        Assert.Equal(8f, found.FromM);

        Assert.True(index.AheadBody(way, 14f, 60f, LaneOccupancy.Nobody, out found));
        Assert.Equal(8f, found.FromM);

        var at = LaneOccupancy.FromTheStart;
        Assert.True(index.NextHeld(way, 14f, 60f, LaneOccupancy.Nobody, ref at, out _));
    }

    /// <summary><b>Nothing survives a rebuild</b>, which is the guarantee that makes the index need no release path.</summary>
    [Fact]
    public void ARebuildLeavesNothingOfTheTickBeforeIt()
    {
        var index = Index(out var roads);
        var way = index.Ways.OfRoadLane(FirstLongLane(roads, 60f));

        index.Begin();
        index.ClaimUnderWay(way, 10f, 14f, 14f, 0f, 1);
        index.ClaimAhead(way, 30f, 34f, 0f, 2, ClaimPriority.Firm);

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
        var way = index.Ways.OfRoadLane(FirstLongLane(roads, 60f));

        index.Begin();
        Assert.True(index.ClaimUnderWay(way, 10f, 14f, 14f, 0f, 1));
        Assert.True(index.ClaimUnderWay(way, 20f, 24f, 24f, 0f, 2));
        Assert.False(index.ClaimUnderWay(way, 30f, 34f, 34f, 0f, 3));
        Assert.Equal(2, index.SlotCount);
    }

    /// <summary>
    /// <b>A way is named once among the ways somebody is on, however often it is laid on and given back.</b>
    /// A car gives its crossing back and the car behind takes the same join in the same walk, so a way the
    /// withdrawal emptied is laid on again — and listed twice, every reader counts what is on it
    /// twice.
    /// </summary>
    [Fact]
    public void AWayEmptiedAndLaidOnAgainIsNamedOnce()
    {
        var index = Index(out var roads);
        var way = index.Ways.OfRoadLane(FirstLongLane(roads, 60f));

        index.Begin();
        index.ClaimAhead(way, 10f, 14f, 0f, 1, ClaimPriority.Firm);
        index.Withdraw(way, occupant: 1, ClaimsAsked.Granted);
        Assert.Equal(0, index.ClaimCount);

        index.ClaimAhead(way, 30f, 34f, 0f, 2, ClaimPriority.Firm);

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
        var join = index.Ways.OfRoadConnector(roads.ConnectorsFrom(lane)[0]);
        Assert.NotEqual(index.Ways.OfRoadLane(lane), join);

        index.Begin();
        index.ClaimUnderWay(join, 0f, MathF.Min(4f, index.WayLengthM(join)), MathF.Min(4f, index.WayLengthM(join)), 5f, 1);

        Assert.True(index.AheadBody(join, 0f, index.WayLengthM(join), LaneOccupancy.Nobody, out _));
        Assert.False(index.AheadBody(index.Ways.OfRoadLane(lane), 0f, 60f, LaneOccupancy.Nobody, out _));
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
            if (roads.LaneLengthM[lane] < atLeastM || roads.LanesFrom(lane).Length == 0) continue;
            if (roads.ConnectorLengthM(roads.ConnectorsFrom(lane)[0]) <= 0f) continue;

            return lane;
        }

        throw new InvalidOperationException($"the fixture town has no lane {atLeastM} m long with a join out of it");
    }
}
