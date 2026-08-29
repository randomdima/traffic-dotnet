using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Terrain;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Person;

/// <summary>
/// PER-15: the question a walker asks before it steps off a kerb. <b>The band it steps into is asked
/// whether it is anybody's</b>, against a book laid by hand over a real map's crossings; the signal half
/// is checked on a running town.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class KerbTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// A way of a crossing of the fixture town that runs under two lanes with road behind both of them,
    /// and an empty book over that town's roads. A crossing sits on an arm, so the lane approaching the
    /// junction has most of a stretch behind the paint and the one leaving it has room for a car and no
    /// more.
    /// </summary>
    static Crossing ACrossing()
    {
        var plan = Towns.Of(Towns.Fixture);
        var roads = RoadGraph.Build(plan, Config);
        var furniture = LaneFurniture.Project(plan, roads);
        var walking = WalkingNetwork.Build(FootGraph.Build(plan, Config), new TerrainGrid(plan, Config), Config);
        var bands = CrossingBands.Project(plan, roads, furniture, walking);

        for (var crossing = 0; crossing < plan.Crosswalks.Count; crossing++)
        {
            foreach (var edge in bands.WaysOf(crossing))
            {
                var under = bands.On(edge);
                if (under.Length < 2) continue;
                if (under[0].AlongLaneM < RoomForACarM || under[1].AlongLaneM < RoomForACarM) continue;

                var book = new LaneOccupancy(roads, mostSlots: 8);
                book.Begin();
                return new Crossing(book, bands, edge, plan.Crosswalks.DepthM[crossing] * 0.5f);
            }
        }

        throw new InvalidOperationException($"{Towns.Fixture} has no crossing under two lanes with road behind them");
    }

    readonly record struct Crossing(LaneOccupancy Book, CrossingBands Bands, int Edge, float HalfDepthM)
    {
        /// <summary>The lanes this crossing runs under, in the order a body walking it meets them.</summary>
        public ReadOnlySpan<CrossingBands.Band> Under => Bands.On(Edge);

        /// <summary>The one it steps into, and the one after that.</summary>
        public CrossingBands.Band First => Under[0];

        public CrossingBands.Band Beyond => Under[1];

        /// <summary>A car's own stretch of one of the lanes, ending that far short of the paint's centre.</summary>
        public void PutACarOn(CrossingBands.Band band, float endingShortOfM, LaneUse use = LaneUse.Reserved) =>
            Book.Add(
                Book.WayOfLane(band.Lane), band.AlongLaneM - endingShortOfM - Config.Car.LengthM,
                band.AlongLaneM - endingShortOfM, 0f, occupant: 0, use);

        /// <summary>Somebody already over the paint on one of the lanes.</summary>
        public void PutAWalkerOn(CrossingBands.Band band) =>
            Book.Add(
                Book.WayOfLane(band.Lane), band.AlongLaneM - 0.5f, band.AlongLaneM + 0.5f, 0f, occupant: 0,
                LaneUse.OnFoot, LaneRoster.Walking);

        public bool IsClear => Kerb.TheBandItStepsIntoIsFree(Book, Under, HalfDepthM);

        /// <summary>How much of a lane a body on this paint takes, either side of it — the town's own figure.</summary>
        public float ClaimM => (HalfDepthM + Config.PersonDiameterM) * Config.Person.RoadClaimMargin;

        /// <summary>The same question asked at that claim, which is what the town asks it at.</summary>
        public bool IsClearOfTheBand => Kerb.TheBandItStepsIntoIsFree(Book, Under, ClaimM);
    }

    /// <summary>How far short of the paint a stretch has to end to be clear of its band, with room to spare.</summary>
    const float ClearM = 6f;

    /// <summary>And how much lane a fixture's crossing needs behind it for a car to be stood there at all.</summary>
    static float RoomForACarM => ClearM + Config.Car.LengthM;

    /// <summary>
    /// <b>A parked row must never hold a crossing shut.</b> What is asked is whether the band this body
    /// steps into is inside somebody's road, so a car that has come to rest clear of it holds none of it
    /// however near it is — and a stopped car's own reservation is a body's length and no more.
    /// </summary>
    [Fact]
    public void ACarStoppedClearOfThePaintDoesNotHoldItShut()
    {
        var at = ACrossing();
        at.PutACarOn(at.First, ClearM);

        Assert.True(at.IsClear);
    }

    /// <summary>
    /// And a car whose own reservation reaches over the paint is waited for, which is the other half of the
    /// same rule: it is committed to ground this body would be standing on.
    /// </summary>
    [Fact]
    public void ACarThatHasTakenTheRoadOverThePaintHoldsIt()
    {
        var at = ACrossing();
        at.PutACarOn(at.First, endingShortOfM: 0f);

        Assert.False(at.IsClear);
    }

    /// <summary>
    /// <b>The band and not the point.</b> A car committed to ground that stops just short of the paint's
    /// centre is a car that will be standing on the near half of the zebra.
    /// </summary>
    [Fact]
    public void GroundTakenAnywhereInTheBandHoldsIt()
    {
        var at = ACrossing();
        at.PutACarOn(at.First, at.HalfDepthM * 0.5f);

        Assert.False(at.IsClear);
    }

    /// <summary>
    /// <b>And a lane this body has not reached yet holds nothing</b> (`PER-15`). A zebra is carriageway and
    /// not one thing: the ground a walker needs to leave the kerb is the lane it steps into, and the one
    /// beyond it is asked for by the same reservation once it is the lane in front — which is what stops a
    /// car on the far side of a road from holding a crossing shut it is nowhere near.
    /// </summary>
    [Fact]
    public void ACarInTheLaneBeyondDoesNotHoldTheKerb()
    {
        var at = ACrossing();
        at.PutACarOn(at.Beyond, endingShortOfM: 0f);

        Assert.True(at.IsClear);
    }

    /// <summary>A body standing over the paint is not a gap: what gets a walker past one is the patience.</summary>
    [Fact]
    public void ABodyStandingOnThePaintHoldsIt()
    {
        var at = ACrossing();
        at.PutACarOn(at.First, endingShortOfM: 0f, LaneUse.Obstruction);

        Assert.False(at.IsClear);
    }

    /// <summary>
    /// <b>Another person on the paint is not traffic.</b> The question is whether the road is anybody's to
    /// drive, and a walker halfway over is neither a reason to stay on the kerb nor something this body
    /// could be hurt by — the two of them are held apart by the pavement's own book instead.
    /// </summary>
    [Fact]
    public void AWalkerAlreadyOnThePaintIsNotWaitedFor()
    {
        var at = ACrossing();
        at.PutAWalkerOn(at.First);

        Assert.True(at.IsClear);
    }

    /// <summary>
    /// <b>No walker begins a crossing on a red</b>, over a minute of every shipped map — the
    /// own soak invariant, counted where it happens rather than sampled.
    /// </summary>
    [Theory]
    [MemberData(nameof(Towns.EveryShippedMap), MemberType = typeof(Towns))]
    public void NoWalkerBeginsACrossingOnARed(string map)
    {
        using var world = new TownWorld(Towns.Of(map), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(3_600);

        Assert.Equal(0, world.CrossingsBegunOnRed);
    }

    /// <summary>
    /// <b>A car that has stopped for a crossing holds none of it</b> (TER-5e, TER-4c.1) — <b>and "it" is the
    /// band a body on the paint holds, not the paintwork</b>. This is what makes the pedestrian's right of
    /// way something the traffic can actually hand over: stopped at the paint, a car is standing on the very
    /// ground it stopped to give up, whoever it gave way to is refused by it for as long as it stands there,
    /// and every crossing in the town is back to being forced on the patience clock.
    /// </summary>
    /// <remarks>
    /// The two figures are measured off different things — the stand-off is car widths, the band is the
    /// paint's depth and a body's own margin — so where a car comes to rest is a claim about the shipped
    /// numbers, asked here rather than reasoned about.
    /// </remarks>
    [Fact]
    public void ACarStoppedForACrossingLeavesTheBandFree()
    {
        var at = ACrossing();

        // Where the crossing rule brings a car to rest, measured the way the band is: from the paint's
        // own centre on the lane.
        at.PutACarOn(at.First, at.ClaimM + Config.CarCrossingStandOffM);

        Assert.True(at.IsClearOfTheBand);
    }

    /// <summary>And the rule is running rather than merely present: walkers do stand at kerbs and ask.</summary>
    [Fact]
    public void WalkersStandAtKerbsAndAsk()
    {
        using var world = new TownWorld(Towns.Of(Towns.Fixture), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(3_600);

        Assert.True(world.KerbWaitsBegun > 0, "no walker waited at a kerb in a minute of the fixture map");
    }
}
