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
        var join = index.Ways.OfRoadTurn(roads.TurnSlotAt(lane, 0));
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
    /// the claims are laid in and not about the arithmetic: the walkers go in between the cars' asks and the
    /// cars' grants, so a band is claimed before any grant is taken off it.
    /// </summary>
    /// <remarks>
    /// Laid last instead, every band was claimed where it was wiped before the next grant was taken and no
    /// driver ever read one while deciding how much road it had. Nothing in the arithmetic said so — the
    /// walkers were claimed, on the right ways, at the right metres, and one pass too late.
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
    /// <b>Every driver on a route has claimed its road.</b> A car the index has not placed is a car nobody behind
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

        // And the reading can only be that, because a live driver's own road is laid from the line it is
        // following (TER-4c.2) — so the stretch a follower is cut at on the way that driver is driving is a
        // `Reserved` one, whatever else that same car is holding elsewhere in the town.
        Span<LaneClaim> slots = stackalloc LaneClaim[64];
        foreach (var way in world.Occupancy.OccupiedWays)
        {
            var count = world.Occupancy.CopyTo(way, slots);
            for (var slot = 0; slot < count; slot++)
            {
                if (slots[slot].Of != LaneRoster.Driving || slots[slot].Occupant < 0) continue;
                if (!slots[slot].HasBody || !slots[slot].OnItsLine) continue;

                var other = slots[slot].Occupant;
                Assert.True(
                    world.Cars.Line[other].LaneCount > 0 || world.Cars.LineWayOf(other) != CarFleet.NoWay,
                    $"car {other} holds a stretch measured from a line it has not got");
            }
        }
    }

    /// <summary>
    /// <b>A body holds the ground it stands on whatever it is doing</b> (TER-4c.2) — including a car with a
    /// hand at its wheel, which is a driver by every field the fleet carries and is on no line the town laid.
    /// </summary>
    /// <remarks>
    /// <b>It is the case the gate was blind to.</b> A wreck is not driven and a parked car is not driven, so
    /// both were written where they lay; a car under a hand is driven, was refused the write on the very way
    /// its movement was held on, and the ground under it was covered by a a granted
    /// stretch instead — which a right of way takes, and which is in no question about where a body is. Every
    /// car crossing that box read it as empty.
    /// </remarks>
    [Fact]
    public void ACarUnderAHandHoldsTheGroundItIsStandingOn()
    {
        using var world = new TownWorld(Towns.Of("Odesa"), Config);
        var loop = new SimLoop<TownWorld>(world, Config);

        // A car actually inside a box, which is the ground the gate left unwritten: the join is the one way
        // it was refused, and a body standing on a lane was always written there.
        var driver = -1;
        var box = CarFleet.NoWay;
        for (var tick = 0; tick < TicksWatched && driver < 0; tick++)
        {
            loop.Advance(1);
            for (var car = 0; car < world.Cars.Count && driver < 0; car++)
            {
                if (!world.Cars.Driven[car] || world.Cars.Broken[car]) continue;
                if (world.Cars.MovementWay[car] == CarFleet.NoWay || !world.Cars.InsideTheBox[car]) continue;

                driver = car;
                box = world.Cars.MovementWay[car];
            }
        }

        Assert.True(driver >= 0, "nobody in a busy town was inside a junction on a movement of its own");

        // A hand at the wheel, on the handbrake: the car stands where the route left it — in the box — and
        // stops being a driver the road can read a line off.
        world.Select(new Selection(SelectionKind.Car, driver));
        world.Hands(new HandInput(Held: true, Throttle: 0f, Steer: 0f, Handbrake: true, WalkDirection: Vector2.Zero));
        loop.Advance(1);

        Assert.True(world.HandsOn, "the hand never reached the wheel");

        var body = LaneClaim.Nothing;
        Span<LaneClaim> slots = stackalloc LaneClaim[64];
        var count = world.Occupancy.CopyTo(box, slots);
        for (var slot = 0; slot < count; slot++)
        {
            if (slots[slot].Occupant != driver || slots[slot].Of != LaneRoster.Driving) continue;
            if (slots[slot].IsGranted) continue;

            body = slots[slot];
        }

        Assert.True(
            body.Found,
            $"car {driver} stands in the box on way {box} under a hand and no claim holds a body of it there");

        // And it is a body and not a claim, which is the whole of the difference: a rank takes a claim and
        // nothing takes this (TER-5e).
        Assert.False(LaneOccupancy.TakesAClaim(body, RightOfWay.Emergency));
    }

    /// <summary>
    /// <b>And it holds what its box covers of that way and no more</b> (TER-4c.2): a body lying across a lane
    /// takes its own width of it, where one lying along the lane takes its length.
    /// </summary>
    /// <remarks>
    /// <b>Read at a half-length whichever way it lay</b>, a car square across a lane shut two car lengths of
    /// it — and the same figure read across the band left that car off the lane it was lying in altogether.
    /// The two are one defect: a single radius is wrong on both axes at once.
    /// </remarks>
    [Fact]
    public void ABodyAcrossALaneTakesItsWidthOfItAndNotItsLength()
    {
        using var world = new TownWorld(Towns.Of("Odesa"), Config);
        var loop = new SimLoop<TownWorld>(world, Config);

        // A car the parking register has let go of, since one standing in a bay is laid from the bay's own
        // ways instead and is the one body in the town that is not laid from its pose.
        var car = -1;
        for (var tick = 0; tick < TicksWatched && car < 0; tick++)
        {
            loop.Advance(1);
            for (var body = 0; body < world.Cars.Count && car < 0; body++)
            {
                if (world.Parking.BayOf(body) < 0) car = body;
            }
        }

        Assert.True(car >= 0, "a busy town kept every one of its cars in a bay");

        var lane = 0;
        var alongM = world.Roads.LaneLengthM[lane] * 0.5f;
        var on = Spline.SampleAt(world.Roads.ArcsOf(lane), alongM);

        ref readonly var build = ref world.Cars.BuildOf(car);
        world.Cars.Driven[car] = false;
        world.Cars.Broken[car] = true;
        world.Cars.VelocityMps[car] = Vector2.Zero;
        world.Cars.PositionM[car] = on.PositionM;

        world.Cars.HeadingRad[car] = MathF.Atan2(on.Direction.Y, on.Direction.X);
        world.RebuildProximityIndex();
        var alongTheLaneM = LengthHeldOn(world, world.Ways.OfRoadLane(lane), car);

        world.Cars.HeadingRad[car] += MathF.PI * 0.5f;
        world.RebuildProximityIndex();
        var acrossTheLaneM = LengthHeldOn(world, world.Ways.OfRoadLane(lane), car);

        Assert.Equal(build.LengthM, alongTheLaneM, 1);
        Assert.Equal(build.WidthM, acrossTheLaneM, 1);
    }

    /// <summary>
    /// <b>A body over the line between two lanes claims both of them</b> (TER-4c.2): touching a
    /// way is being on it, and how much of it the body has taken is not the question the write asks.
    /// </summary>
    /// <remarks>
    /// <b>Asked as a clear width, a straddling body was written onto neither.</b> It left most of each lane
    /// beside it, so each lane in turn judged it something the traffic could get past — and a car standing
    /// square across the middle of a road held not one metre of it in either direction.
    /// </remarks>
    [Fact]
    public void ABodyOverTheLineBetweenTwoLanesIsOnBothOfThem()
    {
        using var world = new TownWorld(Towns.Of("Odesa"), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(1);

        var lane = AQuietLane(world, needsTheLaneBack: true);
        var back = world.Roads.LaneReverse[lane];
        // Half a lane over, which is the paint: the lane running back is on the offside (TER-4a), so the
        // step towards it is against the way this lane's own right hand points.
        var car = StandTheBodyOn(world, lane, -Config.LaneOffsetM * Config.RoadSideSign, out var alongM);

        Assert.True(
            LengthHeldOn(world, world.Ways.OfRoadLane(lane), car) > 0f,
            $"a body on the line between lanes {lane} and {back} holds none of lane {lane}");
        Assert.True(
            LengthHeldOn(world, world.Ways.OfRoadLane(back), car) > 0f,
            $"a body on the line between lanes {lane} and {back} holds none of lane {back}");
        Assert.Equal(car, HolderOn(world, world.Ways.OfRoadLane(back), alongM));
    }

    /// <summary>
    /// <b>And a body has to cross the line to be on the way past it</b> (TER-4c.2,
    /// <c>SimConfig.CrossesOntoAWayM</c>): a box that reaches the edge of the next lane's band without
    /// getting over it is on the lane it is standing in and on no other.
    /// </summary>
    /// <remarks>
    /// <b>What is being kept from claiming is a wing mirror over the paint.</b> A stretch has no width, so
    /// a body written onto a way is a body the traffic there has to be told about — and the two lanes of a
    /// carriageway would otherwise trade bodies on the noise in a pose.
    /// </remarks>
    [Fact]
    public void ABodyUpToTheLineAndNotOverItIsOnOnlyTheLaneItIsIn()
    {
        using var world = new TownWorld(Towns.Of("Odesa"), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(1);

        var lane = AQuietLane(world, needsTheLaneBack: true);
        var back = world.Roads.LaneReverse[lane];
        var flankM = world.Cars.BuildOf(TheBodyToStand).FlankM;

        // Its flank exactly on the paint, and then a hand's breadth over it — under what the shipped town
        // calls crossing (<c>SimConfig.CrossesOntoAWayM</c>). Both are a body touching the lane back, and
        // neither has got into it.
        foreach (var overM in new[] { 0f, 0.05f })
        {
            var acrossM = Config.LaneOffsetM - flankM + overM;
            var car = StandTheBodyOn(world, lane, -acrossM * Config.RoadSideSign, out _);

            Assert.True(
                LengthHeldOn(world, world.Ways.OfRoadLane(lane), car) > 0f,
                $"a body {overM:0.00} m over lane {lane}'s edge holds none of the lane it is standing in");
            Assert.Equal(0f, LengthHeldOn(world, world.Ways.OfRoadLane(back), car));
        }
    }

    /// <summary>
    /// <b>And it stops the traffic of the lanes whose own line it stands in, and no others</b> (TER-4c.2):
    /// what the write records is where the body is, and whether that is something to be held off is the
    /// reader's, taken against the line it is driving (<c>LaneOccupancy.StandsAside</c>).
    /// </summary>
    /// <remarks>
    /// The two halves are one rule and neither works alone. Without the reading, a body written onto every
    /// way it grazes shuts every one of them, because a stretch has no width — and a town whose every turning
    /// car closed the lane beside it is a town that stops.
    /// </remarks>
    [Fact]
    public void ABodyStandingAsideOfALanesLineIsInItsBookAndNotInItsWay()
    {
        using var world = new TownWorld(Towns.Of("Odesa"), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(1);

        var lane = AQuietLane(world, needsTheLaneBack: false);
        var way = world.Ways.OfRoadLane(lane);
        var flankM = world.Cars.BuildOf(TheBodyToStand).FlankM;

        // Its box just reaching the lane's own line, out on the kerb side so that the lane running back is
        // nothing to do with the answer.
        var car = StandTheBodyOn(world, lane, flankM * Config.RoadSideSign, out _);
        Assert.True(
            world.Occupancy.AheadBody(way, 0f, world.Roads.LaneLengthM[lane], LaneOccupancy.Nobody, out _),
            $"a body standing on lane {lane}'s own line is nothing to the traffic driving it");

        // And out at the kerb: halfway between the nearest it may stand without being in the way and the
        // furthest it can stand and still be on the lane at all, which is a crossing short of the lane's own
        // edge (<c>SimConfig.CrossesOntoAWayM</c>).
        var asideM = (Config.LanePassableAsideM + Config.LaneOffsetM - Config.CrossesOntoAWayM) * 0.5f;
        StandTheBodyOn(world, lane, (flankM + asideM) * Config.RoadSideSign, out _);

        Assert.True(
            LengthHeldOn(world, way, car) > 0f, $"a body {asideM:0.00} m aside of lane {lane} claims none of it");
        Assert.False(
            world.Occupancy.AheadBody(way, 0f, world.Roads.LaneLengthM[lane], LaneOccupancy.Nobody, out _),
            $"a body {asideM:0.00} m aside of lane {lane}'s line stops the traffic driving down the middle of it");
    }

    /// <summary>
    /// <b>And of a lane it only clips it holds the clip</b> (TER-4c.2): what a body covers of a way is the
    /// part of its box that is inside that way's band, never the shadow the whole box casts down the line.
    /// </summary>
    /// <remarks>
    /// <b>The shadow of a body standing at an angle is its own length on every way it touches</b>, however
    /// little of it is on any one of them. A car turned across its own lane reached the corner of the next
    /// one by a hand's breadth and claimed four metres of it — as much of a lane it had a wing mirror in as
    /// of the lane it was standing in.
    /// </remarks>
    [Fact]
    public void ABodyClippingALanesCornerHoldsTheCornerAndNotItsOwnShadow()
    {
        using var world = new TownWorld(Towns.Of("Odesa"), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(1);

        var lane = AQuietLane(world, needsTheLaneBack: true);
        var back = world.Ways.OfRoadLane(world.Roads.LaneReverse[lane]);

        // One pose, turned across its own line and leaning towards the lane back far enough for a corner to
        // be over the paint and not merely on it (<c>SimConfig.CrossesOntoAWayM</c>): the lane it is standing
        // in, and the one it reaches by a corner.
        var leaningM = Config.CrossesOntoAWayM * Config.RoadSideSign;
        StandTheBodyOn(world, lane, -leaningM, out _, MathF.PI / 3f);
        var clippedM = LengthHeldOn(world, back, TheBodyToStand);
        var standingInM = LengthHeldOn(world, world.Ways.OfRoadLane(lane), TheBodyToStand);

        Assert.True(clippedM > 0f, "a body reaching over the paint holds none of the lane it reaches into");
        Assert.True(
            clippedM < standingInM,
            $"a body reaching lane {world.Roads.LaneReverse[lane]} by a corner holds {clippedM:0.00} m of it, "
            + $"as much as the {standingInM:0.00} m it holds of lane {lane}, which it is standing in");
    }

    /// <summary>The car these place, moved out of whatever the town had it doing and stood where the test wants it.</summary>
    const int TheBodyToStand = 1;

    /// <summary>
    /// One body stood still on a lane, <paramref name="acrossM"/> to the right of that lane's own line and
    /// square to it, with the claims rebuilt around it.
    /// </summary>
    static int StandTheBodyOn(TownWorld world, int lane, float acrossM, out float alongM, float turnedRad = 0f)
    {
        alongM = world.Roads.LaneLengthM[lane] * 0.5f;
        var on = Spline.SampleAt(world.Roads.ArcsOf(lane), alongM);

        return StandTheBodyAt(
            world, on.PositionM + (Heading.RightOf(on.Direction) * acrossM),
            MathF.Atan2(on.Direction.Y, on.Direction.X) + turnedRad);
    }

    /// <summary>The same body stood still at a pose of its own, with the claims rebuilt around it.</summary>
    static int StandTheBodyAt(TownWorld world, Vector2 atM, float headingRad)
    {
        world.Cars.Driven[TheBodyToStand] = false;
        world.Cars.Broken[TheBodyToStand] = true;
        world.Cars.VelocityMps[TheBodyToStand] = Vector2.Zero;
        world.Cars.PositionM[TheBodyToStand] = atM;
        world.Cars.HeadingRad[TheBodyToStand] = headingRad;
        world.RebuildProximityIndex();
        return TheBodyToStand;
    }

    /// <summary>
    /// <b>A body out of its bay holds the road it is standing on</b> (TER-4c.2). The register says which bay
    /// a car was left in and never where it is: the standing is given back by the manoeuvre that drives out
    /// of one, so a body taken out by anything else — a hand at the wheel, a shunt, a recovery arm — is a car
    /// on the road that the parking register still calls parked.
    /// </summary>
    /// <remarks>
    /// <b>Laid from the register</b>, such a car held two ways of a bay it was streets from and not one metre
    /// of the lane it was standing in the middle of, so the traffic coming up behind it read clear road.
    /// </remarks>
    [Fact]
    public void ABodyOutOfItsBayHoldsTheRoadAndNotTheBayItLeft()
    {
        using var world = new TownWorld(Towns.Of("Odesa"), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(1);

        var car = -1;
        var bay = ParkingRegistry.NoBay;
        for (var body = 0; body < world.Cars.Count && car < 0; body++)
        {
            if (world.Parking.BayOf(body) < 0) continue;

            car = body;
            bay = world.Parking.BayOf(body);
        }

        Assert.True(car >= 0, "the town left none of its cars in a bay");
        Assert.True(HeldOnTheBayWaysM(world, bay, car) > 0f, $"car {car} stands in bay {bay} and holds none of it");

        // Out of the bay and into the middle of a lane, by whatever took it there — which is the point: the
        // body is somewhere the register cannot know about, and the register is not what it is laid from.
        var lane = AQuietLane(world);
        var alongM = world.Roads.LaneLengthM[lane] * 0.5f;
        var on = Spline.SampleAt(world.Roads.ArcsOf(lane), alongM);
        world.Cars.Driven[car] = false;
        world.Cars.VelocityMps[car] = Vector2.Zero;
        world.Cars.PositionM[car] = on.PositionM;
        world.Cars.HeadingRad[car] = MathF.Atan2(on.Direction.Y, on.Direction.X);
        world.RebuildProximityIndex();

        Assert.Equal(
            world.Cars.BuildOf(car).LengthM, LengthHeldOn(world, world.Ways.OfRoadLane(lane), car), 1);
        Assert.Equal(0f, HeldOnTheBayWaysM(world, bay, car));
    }

    /// <summary>
    /// <b>A car on the hook holds the ground it is dragged over, under the vehicle pulling it</b> (EVA-5,
    /// TER-4c.2): a coupled pair is one movement and so one occupant (TER-5c.2), which is what
    /// keeps a truck's own grant off the trailer behind it.
    /// </summary>
    /// <remarks>
    /// <b>Laid under its own number</b>, the trailer cut its hauler's grant and the tow stopped dead on the
    /// first metre of road it stood on; <b>laid not at all</b>, the lane a trailer swings into as the pair
    /// turns held nothing, and the traffic in it drove through a car.
    /// </remarks>
    [Fact]
    public void ACarOnTheHookHoldsItsGroundUnderTheVehiclePullingIt()
    {
        using var world = new TownWorld(Towns.Of("Odesa"), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(1);

        Assert.True(world.Cars.Count >= 2, "a tow needs two vehicles and the town stood fewer");
        const int hauler = 0;
        const int wreck = 1;

        // Square across a lane nobody is on, which is the ground the hauler's own line does not name and so
        // the ground only the trailer can answer for.
        var lane = AQuietLane(world);
        var alongM = world.Roads.LaneLengthM[lane] * 0.5f;
        var on = Spline.SampleAt(world.Roads.ArcsOf(lane), alongM);
        world.Recovery.OnTheHookOf[wreck] = hauler;
        world.Recovery.Towing[hauler] = wreck;
        world.Cars.Driven[wreck] = false;
        world.Cars.Broken[wreck] = true;
        world.Cars.VelocityMps[wreck] = Vector2.Zero;
        world.Cars.PositionM[wreck] = on.PositionM;
        world.Cars.HeadingRad[wreck] = MathF.Atan2(on.Direction.Y, on.Direction.X) + (MathF.PI * 0.5f);
        world.RebuildProximityIndex();

        var way = world.Ways.OfRoadLane(lane);
        Assert.Equal(hauler, HolderOn(world, way, alongM));
        Assert.Equal(0f, LengthHeldOn(world, way, wreck));
    }

    /// <summary>
    /// <b>A body standing over a node holds the ground either side of it</b> (TER-4c.2). A node cut into a
    /// road carries a movement of no length (<see cref="RoadGraph.IsAPlace"/>), so the two lanes meeting there
    /// butt and there is no join to hold what lies across the seam: the ground is on one lane up to the node
    /// and on the other beyond it, and a body over the node is on both.
    /// </summary>
    /// <remarks>
    /// <b>Read as the nearest lane's alone</b>, half of such a body stood on ground nothing claimed and
    /// the block drawn for it stopped dead at the node — which is a car a driver coming the other way through
    /// that seam was granted the road through.
    /// </remarks>
    [Fact]
    public void ABodyOverANodeHoldsTheGroundOnBothSidesOfIt()
    {
        using var world = new TownWorld(Towns.Of("Odesa"), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(1);

        var roads = world.Roads;
        var lane = AQuietLaneOntoANode(world, withAJoinOfItsOwn: false, out var onwards);
        Assert.True(lane >= 0, "the town has no quiet lane running into a node a join of no length is over");

        var lengthM = roads.LaneLengthM[lane];
        var node = Spline.SampleAt(roads.ArcsOf(lane), lengthM);
        var car = StandTheBodyAt(world, node.PositionM, MathF.Atan2(node.Direction.Y, node.Direction.X));

        // Half a metre either side of the seam, which for a body standing square over it is ground under the
        // body whichever lane the metre is a metre of.
        Assert.Equal(car, HolderOn(world, world.Ways.OfRoadLane(lane), lengthM - 0.5f));
        Assert.Equal(car, HolderOn(world, world.Ways.OfRoadLane(onwards), 0.5f));
    }

    /// <summary>
    /// <b>A body whose nose is over the metre its lane is left at holds the movement beyond it</b>
    /// (TER-4c.2, TER-5d): past that metre the ground stops being the lane's and starts being the box's, so a
    /// body reaching past it is standing in the junction whatever its middle is doing.
    /// </summary>
    /// <remarks>
    /// <b>Asked of where the middle projects</b>, a lane whose setback is nought put that metre at the lane's
    /// own end and the node was never asked about at all: the block on the lane ran to the mouth of the
    /// junction and stopped, and the nose over the box was ground nobody held.
    /// </remarks>
    [Fact]
    public void ABodyWithItsNoseOverTheEndOfItsLaneHoldsTheMovementBeyondIt()
    {
        using var world = new TownWorld(Towns.Of("Odesa"), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(1);

        var roads = world.Roads;
        var lane = AQuietLaneOntoANode(world, withAJoinOfItsOwn: true, out _);
        Assert.True(lane >= 0, "the town has no quiet lane running into a junction");

        // A metre short of the metre the lane is left at, so that the body is on the lane and its nose is not.
        var leavesAtM = roads.LaneLengthM[lane] - roads.LeftAtM(lane);
        var on = Spline.SampleAt(roads.ArcsOf(lane), leavesAtM - 1f);
        var car = StandTheBodyAt(world, on.PositionM, MathF.Atan2(on.Direction.Y, on.Direction.X));
        Assert.True(
            world.Cars.BuildOf(car).HalfLengthM > 1f, "a body shorter than the metre it stands back is no test");

        var way = roads.WayOfTurn(roads.TurnSlotAt(lane, 0));
        Assert.Equal(car, HolderOn(world, way, 0.25f));
    }

    /// <summary>
    /// A lane nobody is on that runs into a node, and the lane its first movement leads to — either one the
    /// movement has a line of its own over (a junction) or one where it has none and the lanes butt.
    /// </summary>
    static int AQuietLaneOntoANode(TownWorld world, bool withAJoinOfItsOwn, out int onwards)
    {
        var roads = world.Roads;
        onwards = -1;

        Span<LaneClaim> slots = stackalloc LaneClaim[1];
        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            if (roads.LaneLengthM[lane] < 60f || roads.TurnsFrom(lane).Length == 0) continue;

            var slot = roads.TurnSlotAt(lane, 0);
            if (roads.JoinArcs(slot).Length > 0 != withAJoinOfItsOwn) continue;

            var next = roads.TurnToLane(slot);
            if (world.Occupancy.CopyTo(world.Ways.OfRoadLane(lane), slots) != 0) continue;
            if (world.Occupancy.CopyTo(world.Ways.OfRoadLane(next), slots) != 0) continue;
            if (world.Occupancy.CopyTo(roads.WayOfTurn(slot), slots) != 0) continue;

            onwards = next;
            return lane;
        }

        return -1;
    }

    /// <summary>A lane of the town nobody is on, so that one body put there is the only answer it can give.</summary>
    static int AQuietLane(TownWorld world, bool needsTheLaneBack = false)
    {
        Span<LaneClaim> slots = stackalloc LaneClaim[1];
        for (var lane = 0; lane < world.Roads.LaneCount; lane++)
        {
            var back = world.Roads.LaneReverse[lane];
            if (world.Roads.LaneLengthM[lane] < 60f || (needsTheLaneBack && back < 0)) continue;
            if (world.Occupancy.CopyTo(world.Ways.OfRoadLane(lane), slots) != 0) continue;
            if (needsTheLaneBack && world.Occupancy.CopyTo(world.Ways.OfRoadLane(back), slots) != 0) continue;

            return lane;
        }

        return -1;
    }

    /// <summary>Who holds one place of one way, or <see cref="LaneOccupancy.Nobody"/>.</summary>
    static int HolderOn(TownWorld world, int way, float atM)
    {
        Span<LaneClaim> slots = stackalloc LaneClaim[64];
        var count = world.Occupancy.CopyTo(way, slots);
        for (var slot = 0; slot < count; slot++)
        {
            if (slots[slot].Of != LaneRoster.Driving) continue;
            if (slots[slot].FromM <= atM && slots[slot].StandsToM >= atM) return slots[slot].Occupant;
        }

        return LaneOccupancy.Nobody;
    }

    /// <summary>How much of a bay's own ways one car covers, over all of them.</summary>
    static float HeldOnTheBayWaysM(TownWorld world, int bay, int car)
    {
        var heldM = 0f;
        for (var slot = 0; slot < world.BayWays.WayCountOf(bay); slot++)
        {
            heldM += LengthHeldOn(world, world.BayWays.WayOf(bay, slot), car);
        }

        return heldM;
    }

    /// <summary>How much of one way an occupant's own stretches cover, which for one body is one stretch (TER-5c.2).</summary>
    static float LengthHeldOn(TownWorld world, int way, int car)
    {
        Span<LaneClaim> slots = stackalloc LaneClaim[64];
        var count = world.Occupancy.CopyTo(way, slots);
        for (var slot = 0; slot < count; slot++)
        {
            if (slots[slot].Occupant == car && slots[slot].Of == LaneRoster.Driving)
            {
                return slots[slot].StandsToM - slots[slot].FromM;
            }
        }

        return 0f;
    }

    /// <summary>
    /// <b>A body that is not driving a route still holds the ground it cannot stop short of</b> (TER-4c.1)
    /// — an obstruction is a claim that generally reaches nowhere, and not a stretch of a different
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
        // The lap, whose fleet is on the road rather than in bays.
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
        const int car = TheBodyToStand;
        world.Cars.Driven[car] = false;
        world.Cars.Broken[car] = true;
        world.Cars.PositionM[car] = on.PositionM;
        world.Cars.VelocityMps[car] = Heading.Unit(on.HeadingRad) * alongMps;
        world.RebuildProximityIndex();

        Span<LaneClaim> slots = stackalloc LaneClaim[32];
        var count = world.Occupancy.CopyTo(world.Ways.OfRoadLane(lane), slots);

        var found = false;
        for (var at = 0; at < count; at++)
        {
            if (slots[at].Occupant != car || !slots[at].IsLoose) continue;
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

        Assert.True(found, $"a body left in lane {lane} claimed none of it");
    }

    /// <summary>
    /// <b>A car driving a template of its own holds the ground that template has still to sweep</b>
    /// (TER-4c.1), and not merely the pose it is passing through. A recovery straight was walked before it
    /// was laid and then left open, so the road it was drawn through read free to everybody else: another
    /// driver came to rest in it, and the car reversed into that driver at manoeuvring pace.
    /// </summary>
    /// <remarks>
    /// <b>Asked of the ground and not of the claims' arithmetic</b> — the same walk the desk takes before it
    /// lays a template at all (<see cref="GroundAhead"/>), which is what has to answer differently. A sweep
    /// that ends off the network is nobody's ground and holds nothing, so what is watched here is the ends
    /// that stand on a lane.
    /// <para>
    /// <b>A line that is one of the town's own ways is not a template and is left out</b> (<c>CarFleet.LineWay</c>).
    /// It holds its ground as a claim on that way, and the traffic on the lane it crosses is cut by
    /// looking the table up rather than by finding a stretch of its own lane taken — which is the rule a
    /// body writes only the ways it will be on (TER-5c.1), and is exactly what the sweep could not do.
    /// </para>
    /// <para>
    /// <b>From the tick after the template is laid</b>, because the claims are rebuilt from the bodies in phase
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
        // reading is taken from is not asked about: the claims are laid in phase 2 and a manoeuvre draws its
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
                var at = Spline.SampleAt(world.Cars.LineArcsOf(car)[..line.ArcCount], line.LengthM);
                var forward = Heading.Unit(at.HeadingRad);
                var endM = at.PositionM
                           + ((world.Cars.LineIsReverse[car] ? -forward : forward) * build.CentreAheadOfAxleM);

                // The box it will be standing in and not a circle at the middle of it: what the two cover of
                // a way they meet at an angle are different pieces of road (<see cref="BodyFootprint"/>), and
                // the ground being asked about here is the body's.
                var box = new BodyFootprint(build.HalfLengthM, build.FlankM, forward);

                var wasThere = stood[car] && (endM - endedAtM[car]).Length() <= 1e-3f;
                endedAtM[car] = endM;
                stood[car] = true;
                if (!wasThere) continue;

                // And far enough off that the body where it stands cannot be what covers it.
                if (line.LengthM - world.Cars.ProgressM[car] <= build.LengthM) continue;

                var lane = world.Roads.NearestLane(endM, out var alongM);
                if (lane < 0
                    || !RoadGraph.WithinTheBand(
                        world.Roads.ArcsOf(lane), alongM, endM, world.Roads.LaneWidthM[lane], box, 0f, out _))
                {
                    continue;
                }

                swept++;
                Span<WayUnder> under = stackalloc WayUnder[world.Roads.Ways.MostWaysUnderAPlace];
                Assert.True(
                    GroundAhead.TakenAt(
                        world.Roads, world.Occupancy, endM, box, LaneOccupancy.Nobody, under, out _),
                    $"car {car} is committed to ground at the end of its template that nothing claims "
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
    /// <b>A claim ahead is held to it like anything else, and it is the case that bites.</b> It is ground its
    /// owner is not on yet — the far end of a box it has committed to, the lane it is backing onto — so it
    /// may stand <em>beside</em> a body's own committed claim on the same way and must never run back over it.
    /// Laid from the crossing table without regard to how far the car's own road had got, it did exactly
    /// that: on a join a driver was inside, the two covered the same metres.
    /// </para>
    /// <para>
    /// Every kind of way, because all of them are laid the same way from their own bodies: a walker's ask
    /// begins at its back exactly as a driver's begins at its tail.
    /// </para>
    /// </remarks>
    [Fact]
    public void NobodyHoldsTwoStretchesOfOneWay()
    {
        var world = Run("Odesa");

        NobodyIsLaidTwice(world.Occupancy, "the town");
    }

    static void NobodyIsLaidTwice(LaneOccupancy claims, string called)
    {
        Span<LaneClaim> slots = stackalloc LaneClaim[64];
        foreach (var way in claims.OccupiedWays)
        {
            var count = claims.CopyTo(way, slots);
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
                        + $"{slots[one].FromM:0.00}–{slots[one].ToM:0.00} m ({slots[one].Priority}) and "
                        + $"{slots[other].FromM:0.00}–{slots[other].ToM:0.00} m ({slots[other].Priority}) "
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
    /// <b>It is the claim and not the arithmetic that has to say so.</b> What a car is granted was worked out
    /// correctly all along and written to <c>CarFleet.AuthorityM</c>; what was claimed was the ask,
    /// which is bounded by the rules that stop the car and by nothing in front of it. Every reader of the
    /// claims after the rebuild — the junction gate above all — therefore read one car as holding road it had
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
        Span<LaneClaim> slots = stackalloc LaneClaim[64];
        foreach (var way in world.Occupancy.OccupiedWays)
        {
            var count = world.Occupancy.CopyTo(way, slots);
            for (var one = 0; one < count; one++)
            {
                if (!slots[one].HasBody || !slots[one].OnItsLine) continue;

                held++;
                for (var other = one + 1; other < count; other++)
                {
                    if (!slots[other].HasBody || !slots[other].OnItsLine) continue;
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

        // The census, without which the claim above is kept by a town with nothing in it. A map nobody
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
    /// claims hold is inside the longest one this town can write, so a car cut with nobody anywhere
    /// near it was cut by its own ask. Asked as "clear ⇒ uncut" it needed a car with nothing at all inside
    /// that claim — which on the proving ground is a car three hundred metres clear of six cars and
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
                    Assert.NotEqual(DrivingHold.Claimed, world.Cars.Hold[car]);
                    continue;
                }

                var nearestM = NearestOtherM(world, car);
                Assert.True(
                    nearestM <= ClearOfEverybodyM,
                    $"car {car} was cut to {world.Cars.AuthorityM[car]:0.0} m with the nearest body "
                    + $"{nearestM:0.0} m away, which is further than any claim this town writes "
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
    /// The longest stretch this town can ever claim: a reaction interval at the gear's own
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
    /// <b>Nobody holds road it could not have driven over.</b> A committed claim is the ground the car is
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
            var noseM = world.Cars.ClaimFromM[car] + build.TailMarginM + build.LengthM;
            var wantedM = world.Cars.ClaimToM[car] - noseM - build.BodyMarginM;
            if (wantedM <= 0f) continue;

            asked++;

            // The speed at the rebuild and not the speed now: the claims were laid at the top of this tick and
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

    /// <summary>
    /// <b>A car at rest states nothing</b> (TER-5g). A stated claim says where a body is going and a
    /// body that is not moving is going nowhere until it moves — so whatever speed such a car is planning
    /// for, the road it holds ends where the road it is committed to ends.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void ACarAtRestStatesNothing(string map)
    {
        var world = Run(map);

        for (var car = 0; car < world.Cars.Count; car++)
        {
            // A car that asked for no road is not under way, and neither edge of its ask means anything.
            if (world.Cars.ClaimToM[car] <= world.Cars.ClaimFromM[car]) continue;

            // The speed at the rebuild and not the speed now: the claims were laid at the top of this tick and
            // the body has been driven since, so a car reading back below the bar may have been over it when
            // it asked. A tick of its own braking is the whole of the difference.
            ref readonly var build = ref world.Cars.BuildOf(car);
            if (world.Cars.AlongMps[car]
                > Config.Driving.StopSpeedMps - (build.BrakingMps2 * Config.TickSeconds))
            {
                continue;
            }

            var beyondM = world.Cars.StatedToM[car] - world.Cars.ClaimToM[car];
            Assert.True(
                beyondM <= Tolerance,
                $"{map}: car {car} is at rest and states {beyondM:0.0} m of road beyond what it holds");
        }
    }

    /// <summary>
    /// <b>And a car with a road ahead of it says how far it means to get</b> (TER-5g): moving, with nothing
    /// holding it short and line still to run over, it holds ground beyond the road it is committed to.
    /// </summary>
    /// <remarks>
    /// <b>The two bounds are taken out of the question rather than asserted.</b> A stated claim is clamped
    /// by whatever stops the car — a red, a bar, a crossing, a box it has not been given — and by the line
    /// it actually has (CAR-11), so a car held short of any of those has nothing to say and is evidence of
    /// nothing either way.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void ACarWithARoadAheadOfItSaysHowFarItMeansToGet(string map)
    {
        var world = Run(map);

        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (world.Cars.ClaimToM[car] <= world.Cars.ClaimFromM[car]) continue;
            if (world.Cars.StatedToM[car] >= world.Cars.Line[car].LengthM - Tolerance) continue;

            // The speed at the rebuild and not the speed now, as above: a car reading back over the bar may
            // have been under it when it asked, by a tick of its own acceleration.
            ref readonly var build = ref world.Cars.BuildOf(car);
            if (world.Cars.AlongMps[car]
                <= Config.Driving.StopSpeedMps + (build.AccelerationMps2 * Config.TickSeconds))
            {
                continue;
            }

            ref readonly var context = ref world.Cars.Context[car];
            if (float.IsFinite(context.StopAtM) || float.IsFinite(context.CrossingStopM)) continue;

            Assert.True(
                world.Cars.StatedToM[car] > world.Cars.ClaimToM[car],
                $"{map}: car {car} is doing {world.Cars.AlongMps[car]:0.0} m/s with "
                + $"{world.Cars.Line[car].LengthM - world.Cars.StatedToM[car]:0.0} m of line left, nothing "
                + "stopping it, and states none of it");
        }
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
