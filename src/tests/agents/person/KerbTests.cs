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
/// whether it is anybody's</b>, against claims laid by hand over a real map's crossings; the signal half
/// is checked on a running town.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class KerbTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// A way of a crossing of the fixture town that runs under two lanes with road behind both of them,
    /// and no claims at all over that town's roads. A crossing sits on an arm, so the lane approaching the
    /// junction has most of a stretch behind the paint and the one leaving it has room for a car and no
    /// more.
    /// </summary>
    static Crossing ACrossing()
    {
        var plan = Towns.Of(Towns.Fixture);
        var roads = RoadGraph.Build(plan, Config);
        var furniture = LaneFurniture.Project(plan, roads);
        var walking = WalkingNetwork.Build(FootGraph.Build(plan, Config), new GroundLocator(plan, Config), Config);
        var bands = CrossingBands.Project(plan, roads, furniture, walking);

        for (var crossing = 0; crossing < plan.Crosswalks.Count; crossing++)
        {
            foreach (var edge in bands.WaysOf(crossing))
            {
                var under = bands.On(edge);
                if (under.Length < 2) continue;
                if (under[0].AlongLaneM < RoomForACarM || under[1].AlongLaneM < RoomForACarM) continue;

                var claims = new LaneOccupancy(TownWays.OfTheRoad(roads), mostSlots: 8);
                claims.Begin();
                return new Crossing(claims, bands, edge, plan.Crosswalks.DepthM[crossing] * 0.5f);
            }
        }

        throw new InvalidOperationException($"{Towns.Fixture} has no crossing under two lanes with road behind them");
    }

    readonly record struct Crossing(LaneOccupancy Claims, CrossingBands Bands, int Edge, float HalfDepthM)
    {
        /// <summary>The lanes this crossing runs under, in the order a body walking it meets them.</summary>
        public ReadOnlySpan<CrossingBands.Band> Under => Bands.On(Edge);

        /// <summary>The one it steps into, and the one after that.</summary>
        public CrossingBands.Band First => Under[0];

        public CrossingBands.Band Beyond => Under[1];

        /// <summary>A car's own stretch of one of the lanes, ending that far short of the paint's centre.</summary>
        public void PutACarOn(CrossingBands.Band band, float endingShortOfM)
        {
            var toM = band.AlongLaneM - endingShortOfM;
            Claims.ClaimUnderWay(
                Claims.Ways.OfRoadLane(band.Lane), toM - Config.Car.LengthM, toM, toM, 0f, 0);
        }

        /// <summary>The same car as a body lying there rather than driving down the lane.</summary>
        public void PutABodyOn(CrossingBands.Band band, float endingShortOfM)
        {
            var toM = band.AlongLaneM - endingShortOfM;
            Claims.ClaimWhereItStands(
                Claims.Ways.OfRoadLane(band.Lane), toM - Config.Car.LengthM, toM, toM, 0f, 0);
        }

        /// <summary>An ambulance answering a call, with its own road over one of the lanes (AMB-4).</summary>
        public void PutARescueOn(CrossingBands.Band band, float atMps)
        {
            var toM = band.AlongLaneM + Config.Car.LengthM;
            Claims.ClaimUnderWay(
                Claims.Ways.OfRoadLane(band.Lane), band.AlongLaneM - Config.Car.LengthM, toM, toM, atMps, 0,
                right: RightOfWay.Emergency);
        }

        public bool ARescueIsComingThrough => Kerb.ARescueIsOver(Config, Claims, Under, ClaimM);

        /// <summary>A car under way over the band at a pace of its own, as against one standing on it.</summary>
        public void PutACarCrossing(CrossingBands.Band band, float atMps)
        {
            var toM = band.AlongLaneM + Config.Car.LengthM;
            Claims.ClaimUnderWay(
                Claims.Ways.OfRoadLane(band.Lane), band.AlongLaneM - Config.Car.LengthM, toM, toM, atMps, 0);
        }

        public bool AStandstillIsOverIt => Kerb.AStandstillIsOver(Config, Claims, Under, ClaimM, out _);

        /// <summary>Which body that is, which is what the walker is then held by.</summary>
        public int TheStandstill
        {
            get
            {
                Kerb.AStandstillIsOver(Config, Claims, Under, ClaimM, out var standing);
                return standing;
            }
        }

        /// <summary>Somebody already over the paint on one of the lanes.</summary>
        public void PutAWalkerOn(CrossingBands.Band band)
        {
            var toM = band.AlongLaneM + 0.5f;
            Claims.ClaimUnderWay(
                Claims.Ways.OfRoadLane(band.Lane), band.AlongLaneM - 0.5f, toM, toM, 0f, 0,
                of: LaneRoster.Walking);
        }

        public bool IsClear => Kerb.TheBandItStepsIntoIsFree(Claims, Under, HalfDepthM);

        /// <summary>How much of a lane a body on this paint takes, either side of it — the town's own figure.</summary>
        public float ClaimM => (HalfDepthM + Config.PersonDiameterM) * Config.Person.RoadClaimMargin;

        /// <summary>The same question asked at that claim, which is what the town asks it at.</summary>
        public bool IsClearOfTheBand => Kerb.TheBandItStepsIntoIsFree(Claims, Under, ClaimM);
    }

    /// <summary>How far short of the paint a stretch has to end to be clear of its band, with room to spare.</summary>
    const float ClearM = 6f;

    /// <summary>And how much lane a fixture's crossing needs behind it for a car to be stood there at all.</summary>
    static float RoomForACarM => ClearM + Config.Car.LengthM;

    /// <summary>
    /// <b>A parked row must never hold a crossing shut.</b> What is asked is whether the band this body
    /// steps into is inside somebody's road, so a car that has come to rest clear of it holds none of it
    /// however near it is — and a stopped car's own claim is a body's length and no more.
    /// </summary>
    [Fact]
    public void ACarStoppedClearOfThePaintDoesNotHoldItShut()
    {
        var at = ACrossing();
        at.PutACarOn(at.First, ClearM);

        Assert.True(at.IsClear);
    }

    /// <summary>
    /// And a car whose own claim reaches over the paint is waited for, which is the other half of the
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
    /// beyond it is asked for by the same claim once it is the lane in front — which is what stops a
    /// car on the far side of a road from holding a crossing shut it is nowhere near.
    /// </summary>
    [Fact]
    public void ACarInTheLaneBeyondDoesNotHoldTheKerb()
    {
        var at = ACrossing();
        at.PutACarOn(at.Beyond, endingShortOfM: 0f);

        Assert.True(at.IsClear);
    }

    /// <summary>A body standing over the paint is not a gap, and the patience is no answer to one either.</summary>
    [Fact]
    public void ABodyStandingOnThePaintHoldsIt()
    {
        var at = ACrossing();
        at.PutABodyOn(at.First, endingShortOfM: 0f);

        Assert.False(at.IsClear);
    }

    /// <summary>
    /// <b>A car standing on the band is a standstill and not a wait</b> (PER-15). The patience takes a road
    /// a driver has taken and can give back by driving on; a body over the paint gives nothing back, so
    /// taking it past the clock is a walker walking into a car and shoving it down its own lane.
    /// </summary>
    [Fact]
    public void ACarStandingOnTheBandIsAStandstill()
    {
        var at = ACrossing();
        at.PutABodyOn(at.First, endingShortOfM: 0f);

        Assert.True(at.AStandstillIsOverIt);
    }

    /// <summary>
    /// <b>And which body it is comes back with it</b>, because a walker held at one has to name what is
    /// holding it or the clock that gives up a leg has nothing to run against.
    /// </summary>
    [Fact]
    public void TheStandstillOnTheBandIsNamed()
    {
        var at = ACrossing();
        at.PutABodyOn(at.First, endingShortOfM: 0f);

        Assert.Equal(0, at.TheStandstill);
    }

    /// <summary>
    /// <b>A car coming through the band is traffic and is waited out</b>, which is what leaves the patience
    /// the escape it was written to be: read as a standstill instead, a busy street would refuse a walker on
    /// every tick a car happened to be over the paint and the crossing would never clear at all.
    /// </summary>
    [Fact]
    public void ACarComingThroughTheBandIsNotAStandstill()
    {
        var at = ACrossing();
        at.PutACarCrossing(at.First, Config.PersonWalkSpeedMps * 2f);

        Assert.False(at.AStandstillIsOverIt);
    }

    /// <summary>
    /// <b>And a car stopped clear of the band is standing on nothing of it</b>: the question is the same
    /// strip of road the gap question is asked about, so a parked row beside a crossing holds none of it.
    /// </summary>
    [Fact]
    public void ACarStandingClearOfTheBandIsNotAStandstill()
    {
        var at = ACrossing();
        at.PutABodyOn(at.First, ClearM);

        Assert.False(at.AStandstillIsOverIt);
    }

    /// <summary>
    /// <b>Another person on the paint is not traffic.</b> The question is whether the road is anybody's to
    /// drive, and a walker halfway over is neither a reason to stay on the kerb nor something this body
    /// could be hurt by — the two of them are held apart by the pavement's own claims instead.
    /// </summary>
    [Fact]
    public void AWalkerAlreadyOnThePaintIsNotWaitedFor()
    {
        var at = ACrossing();
        at.PutAWalkerOn(at.First);

        Assert.True(at.IsClear);
    }

    /// <summary>
    /// <b>A rescue coming through is the one road the patience does not escape</b> (`AMB-4`, PER-15): past
    /// the wait a walker takes the band it was refused, and an ambulance answering a call is what it takes
    /// it from.
    /// </summary>
    [Fact]
    public void ARescueComingThroughHoldsTheCrossingPastThePatience()
    {
        var at = ACrossing();
        at.PutARescueOn(at.First, Config.PersonWalkSpeedMps * 2f);

        Assert.True(at.ARescueIsComingThrough);
    }

    /// <summary>
    /// <b>And a rescue that has stopped over the paint is not one.</b> What the exemption is worth is its
    /// own justification — a call lasts seconds, so what is being waited out is going to pass — and an
    /// ambulance standing on the band is not passing: it is a car standing on the band like any other, and
    /// what answers one of those is the clock that gives up a leg rather than a road to be waited out.
    /// </summary>
    [Fact]
    public void ARescueStandingOnThePaintDoesNotHoldItForEver()
    {
        var at = ACrossing();
        at.PutARescueOn(at.First, atMps: 0f);

        Assert.False(at.ARescueIsComingThrough);
    }

    /// <summary>
    /// <b>Stopped is a pace and never zero.</b> A car held in a queue creeps at fractions of a millimetre a
    /// second, and read against zero that is a rescue coming through for as long as it sits there — which is
    /// how one ambulance held a crossing shut for the length of a run.
    /// </summary>
    [Fact]
    public void ARescueCreepingIsNotComingThrough()
    {
        var at = ACrossing();
        at.PutARescueOn(at.First, atMps: 4e-4f);

        Assert.False(at.ARescueIsComingThrough);
    }

    /// <summary>
    /// <b>And a rescue standing on the band cannot hide one moving through it</b>: every stretch over the
    /// ground is asked rather than the first one found.
    /// </summary>
    [Fact]
    public void AStoppedRescueDoesNotHideOneComingThrough()
    {
        var at = ACrossing();
        at.PutARescueOn(at.First, atMps: 0f);
        at.PutARescueOn(at.First, Config.PersonWalkSpeedMps * 2f);

        Assert.True(at.ARescueIsComingThrough);
    }

    /// <summary>
    /// <b>No walker begins a crossing on a red</b>, over a minute of every shipped map — the
    /// own soak invariant, counted where it happens rather than sampled.
    /// </summary>
    [Theory]
    [MemberData(nameof(Towns.EveryTown), MemberType = typeof(Towns))]
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
