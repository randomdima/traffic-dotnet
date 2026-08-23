using System.Numerics;
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

    /// <summary>A body the asker is already overlapping is a contact and not an empty road.</summary>
    [Fact]
    public void SomethingOverlappingTheAskerAnswersAtItsOwnNearEdge()
    {
        var index = Index(out var roads);
        var way = index.WayOfLane(FirstLongLane(roads, 60f));

        index.Begin();
        index.Add(way, 8f, 14f, 0f, occupant: 1, LaneUse.Obstruction);

        Assert.True(index.AheadBody(way, 10f, 60f, LaneOccupancy.Nobody, out var found));
        Assert.Equal(8f, found.FromM);
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

    static int FirstLongLane(RoadGraph roads, float atLeastM)
    {
        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            if (roads.LaneLengthM[lane] >= atLeastM && roads.TurnsFrom(lane).Length > 0) return lane;
        }

        throw new InvalidOperationException($"the fixture town has no lane {atLeastM} m long with a way out of it");
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
    /// <b>The index is never full.</b> Past its bound a stretch is dropped, and a dropped stretch is a car
    /// its followers cannot name — which reads as an obstruction and is the one thing that must not happen
    /// silently.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void TheIndexNeverReachesItsBound(string map)
    {
        var world = Run(map);
        if (world.Cars.Count == 0) return;

        Assert.True(
            world.Occupancy.SlotCount < world.Occupancy.Capacity,
            $"the index held {world.Occupancy.SlotCount} of {world.Occupancy.Capacity} stretches");
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
    /// <b>From the tick after the template is laid</b>, because the book is rebuilt from the bodies in phase
    /// 2 and a manoeuvre lays its line in phase 3. The tick a template is drawn on is the one the desk's own
    /// walk answered for, and it is the only tick in the life of the line that this does not.
    /// </para>
    /// </remarks>
    [Fact]
    public void ATemplateHoldsTheGroundItHasStillToSweep()
    {
        var world = new TownWorld(Towns.Of("Odesa"), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        var halfWidthM = Config.Car.WidthM * 0.5f;

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
                if (line.ArcCount == 0 || line.LaneCount > 0 || !world.Cars.Driven[car] || world.Cars.Broken[car])
                {
                    stood[car] = false;
                    continue;
                }

                // Where the body will be and not where the line ends: a template is drawn for the rear axle,
                // and the axle at the end of one stands a metre and a half short of the middle of the car —
                // which at the mouth of a lane is a different way of the town altogether.
                var at = Spline.SampleAt(world.Cars.LineArcsOf(car)[..line.ArcCount], line.LengthM);
                var forward = Heading.Unit(at.HeadingRad);
                var endM = at.PositionM
                           + ((world.Cars.LineIsReverse[car] ? -forward : forward) * Config.CarCentreAheadOfAxleM);

                var wasThere = stood[car] && (endM - endedAtM[car]).Length() <= 1e-3f;
                endedAtM[car] = endM;
                stood[car] = true;
                if (!wasThere) continue;

                // And far enough off that the body where it stands cannot be what covers it.
                if (line.LengthM - world.Cars.ProgressM[car] <= Config.Car.LengthM) continue;

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
    /// <b>A car nothing is in front of is held by nobody.</b> Its own ask comes back to it whole, and a
    /// grant handed back as the length of that ask would read as the car queueing behind itself — which is
    /// a car alone on an empty street driving as though there were a jam on it.
    /// </summary>
    /// <remarks>
    /// Asked of the proving ground, where a handful of cars are strung out round one long lap: a car with
    /// nothing inside the longest reservation this town can write is a car nothing could have cut.
    /// </remarks>
    [Fact]
    public void ACarWithTheRoadToItselfIsHeldByNobody()
    {
        var world = new TownWorld(Towns.Of(TrackPlan.Name), Config);
        var loop = new SimLoop<TownWorld>(world, Config);

        var alone = 0;
        for (var tick = 0; tick < TicksWatched; tick++)
        {
            loop.Advance();
            for (var car = 0; car < world.Cars.Count; car++)
            {
                if (!world.Cars.Driven[car] || world.Cars.Line[car].LaneCount == 0) continue;

                if (NearestOtherM(world, car) <= ClearOfEverybodyM) continue;

                alone++;
                Assert.True(
                    float.IsPositiveInfinity(world.Cars.AuthorityM[car]),
                    $"car {car} has the lap to itself and was cut to {world.Cars.AuthorityM[car]:0.0} m");

                Assert.NotEqual(DrivingHold.Reserved, world.Cars.Hold[car]);
            }
        }

        Assert.True(alone > 0, "not one car on the proving ground ever had the road to itself");
    }

    /// <summary>How near the nearest other car is, which is what says whether anything could have cut this one.</summary>
    static float NearestOtherM(TownWorld world, int car)
    {
        var nearestM = float.PositiveInfinity;
        for (var other = 0; other < world.Cars.Count; other++)
        {
            if (other == car) continue;

            nearestM = MathF.Min(nearestM, (world.Cars.PositionM[other] - world.Cars.PositionM[car]).Length());
        }

        return nearestM;
    }

    /// <summary>
    /// The longest stretch this town can ever write into the book: a reaction interval at the gear's own
    /// cap, a stop from there, and the body and the margin it keeps at either end of itself. Nothing
    /// further away than this can have cut anybody.
    /// </summary>
    static float ClearOfEverybodyM =>
        (Config.Car.MaxSpeedMps * Config.CarReactionS)
        + (Config.Car.MaxSpeedMps * Config.Car.MaxSpeedMps / (2f * CarFollower.BrakingMps2(Config, 1f)))
        + Config.Car.LengthM + Config.CarBodyMarginM + Config.CarTailMarginM;

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
            var noseM = world.Cars.ReserveFromM[car] + Config.CarTailMarginM + Config.Car.LengthM;
            var wantedM = world.Cars.ReserveToM[car] - noseM - Config.CarBodyMarginM;
            if (wantedM <= 0f) continue;

            asked++;

            // The speed at the rebuild and not the speed now: the book was laid at the top of this tick and
            // the body has been driven since, so a car that stood on the brakes in between reads back a tick
            // of braking slower than the ask was sized at.
            var brakingMps2 = CarFollower.BrakingMps2(Config, world.Cars.GroundCoefficient[car]);
            var reachableMps = world.Cars.AlongMps[car]
                               + (Config.Car.BrakingMps2 * Config.TickSeconds)
                               + (Config.Car.AccelerationMps2 * Config.CarReactionS);

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
    /// <b>Nobody is granted ground another car will still be standing on when it has stopped.</b> That is
    /// the whole of what holds one car off the next, and it is the one property the arithmetic of the
    /// grant exists to deliver.
    /// </summary>
    /// <remarks>
    /// Asked of the ways rather than of the cars, because that is where two grants would meet — two cars on
    /// one lane, and two cars meeting on the join between two. A grant that came out empty is skipped: a
    /// car that cannot stop in what is left is a fact about a contact, and the profile is already braking
    /// as hard as it can.
    /// </remarks>
    [Fact]
    public void NobodyIsGrantedGroundSomebodyElseWillStopOn()
    {
        var world = Run("Odesa");
        var index = world.Occupancy;

        var granted = 0;
        Span<LaneSlot> slots = stackalloc LaneSlot[64];
        foreach (var way in index.OccupiedWays)
        {
            var count = index.CopyTo(way, slots);
            for (var behind = 0; behind < count; behind++)
            {
                if (slots[behind].Use != LaneUse.Reserved) continue;

                var grantedToM = GrantedToM(world, slots[behind]);
                if (grantedToM <= slots[behind].FromM) continue;

                granted++;
                for (var ahead = behind + 1; ahead < count; ahead++)
                {
                    if (slots[ahead].Use != LaneUse.Reserved) continue;
                    if (slots[ahead].Occupant == slots[behind].Occupant) continue;

                    // <b>In front is a fact about the bodies and not about the near edges</b> (TER-5c.2):
                    // every stretch begins a margin behind its owner and a stretch clipped at a way's start
                    // begins further back still, so a slot later in the list can belong to a body this one
                    // has already passed. Nobody is granted ground in front of them on its account.
                    if (slots[ahead].StandsToM < slots[behind].StandsToM) continue;

                    // Where that car's own tail comes to rest, which is the ground it is entitled to and
                    // the first metre this one may not have. Against the grip the *asking* car has, which
                    // is what a driver can actually see: how fast the one in front is going, and not what
                    // it is standing on.
                    var brakingMps2 = CarFollower.BrakingMps2(
                        Config, world.Cars.GroundCoefficient[slots[behind].Occupant]);

                    var restingM = slots[ahead].FromM
                        + MathF.Max(0f, slots[ahead].AlongMps * slots[ahead].AlongMps / (2f * brakingMps2));

                    Assert.True(
                        grantedToM <= restingM + Tolerance,
                        $"car {slots[behind].Occupant} was granted to {grantedToM:0.00} m of way {way}, "
                        + $"where car {slots[ahead].Occupant} comes to rest at {restingM:0.00} m");
                }
            }
        }

        Assert.True(granted > 0, "no driver in a busy town was granted any ground at all");
    }

    /// <summary>
    /// Where a grant actually ends on the way it was laid on. <b>What goes into the book is what the car
    /// asked for</b> — the cut is taken off it afterwards, so a stretch drawn from the book has to be read
    /// back through the grant the driver was given.
    /// </summary>
    /// <remarks>
    /// Off the ask and never off the car's progress: the ask is what the index was laid from, and the body
    /// has driven a tick further since. A third of a metre at town speed is enough to make the arithmetic
    /// here disagree with the arithmetic that granted it.
    /// </remarks>
    static float GrantedToM(TownWorld world, in LaneSlot asked)
    {
        var car = asked.Occupant;
        var noseM = world.Cars.ReserveFromM[car] + Config.CarTailMarginM + Config.Car.LengthM;
        var overshotM = world.Cars.ReserveToM[car] - (world.Cars.AuthorityM[car] + noseM);

        return asked.ToM - MathF.Max(0f, overshotM);
    }

    /// <summary>Ground on a way is metres, and a grant is arithmetic on floats: a millimetre is not a finding.</summary>
    const float Tolerance = 1e-2f;

    /// <summary>The bar the road itself holds a car to before it calls the line lost, which is what the index places by.</summary>
    const float OnItsLineTolerance = 2f;

    public static TheoryData<string> Maps => Towns.EveryShippedMap();

    static TownWorld Run(string map)
    {
        var world = new TownWorld(Towns.Of(map), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(TicksWatched);
        return world;
    }

    /// <summary>A minute of town, which is long enough for every kind of hold to have happened on every map.</summary>
    const int TicksWatched = 3_600;
}
