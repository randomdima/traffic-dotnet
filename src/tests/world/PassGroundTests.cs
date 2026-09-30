using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// <b>The ground an overtake will cover</b> (TER-4c.6), as arithmetic on the reservations: laid at p0 it cuts
/// every plan in front of it, it is never itself something to get past, and it is asked for only over ground
/// nobody holds.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P1)]
public class PassGroundTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly RoadGraph Roads = RoadGraph.Build(Towns.Of(Towns.Fixture), Config);

    static LaneOccupancy Index() =>
        new(TownWays.OfTheRoad(Roads), mostSlots: 32, mostHolds: 8, WayCrossings.None);

    /// <summary>Two ways long enough to lay these tests' stretches on, distinct and in no relation to each other.</summary>
    static (int A, int B) TwoWays()
    {
        var found = new List<int>();
        for (var lane = 0; lane < Roads.LaneCount && found.Count < 2; lane++)
        {
            if (Roads.LaneLengthM[lane] >= 60f) found.Add(TownWays.OfTheRoad(Roads).OfRoadLane(lane));
        }

        Assert.Equal(2, found.Count);
        return (found[0], found[1]);
    }

    /// <summary>
    /// <b>A pass is a body to every plan</b>: a holder that can no longer stop short of it is cut at it all the
    /// same, which is why a pass is asked for only where no plan reaches.
    /// </summary>
    [Fact]
    public void APassCutsEvenCommittedGroundAtItsNearEdge()
    {
        var (way, _) = TwoWays();
        var index = Index();

        index.Begin();
        index.LayPass(way, 30f, 45f, -8f, occupant: 2, LaneRoster.Driving);

        var hold = index.BeginHold(0f);
        var ask = new PlannedAsk(
            hold, 1, LaneRoster.Driving, ClaimPriority.Special, 5f, LineFromM: 5f, AheadM: 0f,
            CommittedToM: float.PositiveInfinity, AlongMps: 10f);

        Assert.Equal(30f, index.Reach(ask, way, 60f, 5f, out var cutBy));
        Assert.True(cutBy.Passing);
    }

    /// <summary>
    /// <b>Its own stretch</b>: laid over a way its holder's body is on, it stays apart from that body rather than
    /// growing it over whatever lies between the two.
    /// </summary>
    [Fact]
    public void APassIsNeverGrownIntoItsHoldersBody()
    {
        var (way, _) = TwoWays();
        var index = Index();

        index.Begin();
        index.LayBody(way, 10f, 14f, 0f, 1, LaneRoster.Driving, onItsLine: true);
        index.LayPass(way, 25f, 40f, 5f, 1, LaneRoster.Driving);
        index.LayBody(way, 11f, 15f, 0f, 1, LaneRoster.Driving, onItsLine: true);

        Span<LaneClaim> bodies = stackalloc LaneClaim[4];
        Assert.Equal(2, index.CopyBodiesTo(way, bodies));
        Assert.Equal((10f, 15f, false), (bodies[0].FromM, bodies[0].ToM, bodies[0].Passing));
        Assert.Equal((25f, 40f, true), (bodies[1].FromM, bodies[1].ToM, bodies[1].Passing));
    }

    /// <summary>
    /// <b>Laid a station at a time</b>, a pass is one stretch of a way where its stations meet, and two where it
    /// leaves the way and comes back to it — what lies between is what it passes.
    /// </summary>
    [Fact]
    public void APassIsOneStretchWhereItsStationsMeetAndTwoWhereItComesBack()
    {
        var (way, _) = TwoWays();
        var index = Index();

        index.Begin();
        index.LayPass(way, 10f, 16f, 0f, 1, LaneRoster.Driving);
        index.LayPass(way, 15f, 21f, 0f, 1, LaneRoster.Driving);
        index.LayPass(way, 40f, 46f, 0f, 1, LaneRoster.Driving);

        Span<LaneClaim> bodies = stackalloc LaneClaim[4];
        Assert.Equal(2, index.CopyBodiesTo(way, bodies));
        Assert.Equal((10f, 21f), (bodies[0].FromM, bodies[0].ToM));
        Assert.Equal((40f, 46f), (bodies[1].FromM, bodies[1].ToM));
    }

    /// <summary>
    /// <b>Rule 1</b>: a body at rest travelling this way on to the reader's own next way is making the reader's
    /// own movement, and is never passed — whatever it is waiting for, the reader would wait for too.
    /// </summary>
    [Fact]
    public void ABodyMakingTheReadersOwnMovementIsNeverPassed()
    {
        var waiting = new LaneClaim(10f, 14f, 0f, 3, ClaimPriority.Hard, OnItsLine: true, Onward: 7, Still: true);

        Assert.False(waiting.MayBePassedBy(onward: 7));
        Assert.True(waiting.MayBePassedBy(onward: 8));
    }

    /// <summary>
    /// <b>A line not yet laid past the way makes every movement</b>: its holder is further than it can see from the
    /// way's end, so whatever holds it there is on the way, and the reader would wait for that too — whichever of
    /// the two does not yet know where it goes next.
    /// </summary>
    [Fact]
    public void ALineNotYetLaidPastTheWayMakesEveryMovement()
    {
        var queued = new LaneClaim(
            10f, 14f, 0f, 3, ClaimPriority.Hard, OnItsLine: true, Onward: LaneOccupancy.RunsOn, Still: true);

        Assert.False(queued.MayBePassedBy(onward: 8));
        Assert.False((queued with { Onward = 7 }).MayBePassedBy(onward: LaneOccupancy.RunsOn));
    }

    /// <summary><b>A car whose line ends where it stands is making no movement</b>, and is passed by anybody going on.</summary>
    [Fact]
    public void ACarAtTheEndOfItsLineMakesNoMovement()
    {
        var arrived = new LaneClaim(
            10f, 14f, 0f, 3, ClaimPriority.Hard, OnItsLine: true, Onward: LaneOccupancy.NoWay, Still: true);

        Assert.True(arrived.MayBePassedBy(onward: 7));
        Assert.True(arrived.MayBePassedBy(onward: LaneOccupancy.RunsOn));
    }

    /// <summary>
    /// <b>Room to step out is kept only behind a body going nowhere</b> (CAR-46): at rest and not travelling the
    /// way on — never a queue, whatever it does next, and never anything moving or a pass.
    /// </summary>
    [Fact]
    public void OnlyABodyAtRestTravellingNowhereGoesNowhere()
    {
        var standing = new LaneClaim(10f, 14f, 0f, 3, ClaimPriority.Hard, Still: true);
        var queued = standing with { OnItsLine = true, Onward = 7 };

        Assert.True(standing.GoesNowhere);
        Assert.True((queued with { Onward = LaneOccupancy.NoWay }).GoesNowhere);
        Assert.False(queued.GoesNowhere);
        Assert.False((queued with { Onward = LaneOccupancy.RunsOn }).GoesNowhere);
        Assert.False((standing with { Still = false }).GoesNowhere);
        Assert.False((standing with { Passing = true }).GoesNowhere);
    }

    /// <summary><b>Rule 2</b>: a body that is moving is going somewhere, and is never passed.</summary>
    [Fact]
    public void ABodyThatIsMovingIsNeverPassed()
    {
        var crossing = new LaneClaim(10f, 11f, 0f, 3, ClaimPriority.Hard, LaneRoster.Walking, Still: false);

        Assert.False(crossing.MayBePassedBy(onward: 7));
        Assert.True((crossing with { Still = true }).MayBePassedBy(onward: 7));
    }

    /// <summary><b>A pass is never something to get past</b>: its holder is moving over it, however slowly.</summary>
    [Fact]
    public void APassIsNeverPassed()
    {
        var pass = new LaneClaim(10f, 40f, 0f, 3, ClaimPriority.Hard, Still: true, Passing: true);

        Assert.False(pass.MayBePassedBy(onward: 7));
    }

    /// <summary>
    /// <b>The lane must be free</b>: a pass is asked for only over ground no other body stands on and no other
    /// holder plans — the plans of what it passes aside, since the pass laid over them cuts them.
    /// </summary>
    [Fact]
    public void GroundIsFreeForAPassOnlyOfWhatItPasses()
    {
        var (way, _) = TwoWays();
        var index = Index();

        index.Begin();
        index.LayBody(way, 10f, 14f, 0f, 2, LaneRoster.Driving, onItsLine: true, still: true);
        Lay(index, way, holder: 2, fromM: 14f, toM: 20f);
        Span<LaneClaim> passed = stackalloc LaneClaim[1];
        passed[0] = new LaneClaim(10f, 14f, 0f, 2, ClaimPriority.Hard, Still: true);

        Assert.True(index.IsFreeForAPass(way, 14f, 30f, 1, LaneRoster.Driving, passed));
        Assert.False(index.IsFreeForAPass(way, 12f, 30f, 1, LaneRoster.Driving, passed));

        Lay(index, way, holder: 3, fromM: 25f, toM: 35f);
        Assert.False(index.IsFreeForAPass(way, 14f, 30f, 1, LaneRoster.Driving, passed));
    }

    /// <summary>
    /// <b>Two passes asked over one ground on one tick</b>: both holders read one layer, and exactly one of them
    /// keeps its pass — the lower by roster and occupant.
    /// </summary>
    [Fact]
    public void OfTwoPassesAskedOverOneGroundExactlyOneIsKept()
    {
        var (way, _) = TwoWays();
        var index = Index();

        index.Begin();
        index.LayPass(way, 20f, 40f, 5f, 4, LaneRoster.Driving);
        index.LayPass(way, 30f, 50f, -5f, 9, LaneRoster.Driving);

        Assert.True(index.KeepsItsPass(way, 20f, 40f, 4, LaneRoster.Driving));
        Assert.False(index.KeepsItsPass(way, 30f, 50f, 9, LaneRoster.Driving));
    }

    /// <summary>And a body that stepped onto the ground since the pass was asked for withdraws it.</summary>
    [Fact]
    public void APassIsWithdrawnWhereABodyHasSteppedOntoItsGround()
    {
        var (way, _) = TwoWays();
        var index = Index();

        index.Begin();
        index.LayPass(way, 20f, 40f, 5f, 1, LaneRoster.Driving);
        index.LayBody(way, 33f, 34f, 0f, 1, LaneRoster.Walking, onItsLine: false);

        Assert.False(index.KeepsItsPass(way, 20f, 40f, 1, LaneRoster.Driving));
    }

    /// <summary>
    /// <b>A movement a pass holds whole is free of a body clear of where the pass's body goes</b> — which, standing in
    /// the box itself, is what the pass is getting past — and not of one standing where it goes.
    /// </summary>
    [Fact]
    public void AMovementHeldWholeIsFreeOfABodyClearOfWhereThePassGoes()
    {
        var (way, _) = TwoWays();
        var index = Index();

        index.Begin();
        index.LayBody(way, 30f, 34f, 0f, 2, LaneRoster.Driving, onItsLine: false, still: true);

        Assert.True(index.IsFreeForAPass(way, 5f, 25f, 0f, 60f, 1, LaneRoster.Driving, [], PassTerms.Plain));
        Assert.False(index.IsFreeForAPass(way, 5f, 31f, 0f, 60f, 1, LaneRoster.Driving, [], PassTerms.Plain));
    }

    /// <summary>But not of another pass, nor of a plan, anywhere the movement is held: those are what holding it whole is for.</summary>
    [Fact]
    public void AMovementHeldWholeIsNotFreeOfAnotherPassOrAPlanAnywhereOnIt()
    {
        var (way, _) = TwoWays();
        var index = Index();

        index.Begin();
        index.LayPass(way, 40f, 50f, 5f, 3, LaneRoster.Driving);
        Assert.False(index.IsFreeForAPass(way, 5f, 25f, 0f, 60f, 1, LaneRoster.Driving, [], PassTerms.Plain));

        index.Begin();
        Lay(index, way, holder: 4, fromM: 40f, toM: 50f);
        Assert.False(index.IsFreeForAPass(way, 5f, 25f, 0f, 60f, 1, LaneRoster.Driving, [], PassTerms.Plain));
    }

    /// <summary>
    /// <b>And kept the same way</b>: a body clear of where the pass's body goes withdraws nothing, and a pass asked
    /// on the same tick by a holder numbered before it withdraws it wherever the movement is held.
    /// </summary>
    [Fact]
    public void APassOverAMovementHeldWholeIsWithdrawnByAnotherPassAnywhereOnItAndABodyOnlyWhereItGoes()
    {
        var (way, _) = TwoWays();
        var index = Index();

        index.Begin();
        index.LayPass(way, 0f, 60f, 5f, 4, LaneRoster.Driving);
        index.LayBody(way, 30f, 34f, 0f, 2, LaneRoster.Driving, onItsLine: false, still: true);
        Assert.True(index.KeepsItsPass(way, 5f, 25f, 0f, 60f, 4, LaneRoster.Driving, PassTerms.Plain));
        Assert.False(index.KeepsItsPass(way, 5f, 31f, 0f, 60f, 4, LaneRoster.Driving, PassTerms.Plain));

        index.LayPass(way, 45f, 55f, -5f, 1, LaneRoster.Driving);
        Assert.False(index.KeepsItsPass(way, 5f, 25f, 0f, 60f, 4, LaneRoster.Driving, PassTerms.Plain));
    }

    /// <summary>
    /// <b>A holder on a call gets past a queue, and past traffic that is moving</b> (AMB-4.4): never somebody on
    /// foot who is moving, who is crossing, and never a pass.
    /// </summary>
    [Fact]
    public void AQueueAndMovingTrafficArePassedOnACall()
    {
        var waiting = new LaneClaim(10f, 14f, 0f, 3, ClaimPriority.Hard, OnItsLine: true, Onward: 7, Still: true);
        var crossing = new LaneClaim(10f, 11f, 0f, 3, ClaimPriority.Hard, LaneRoster.Walking, Still: false);

        Assert.True(waiting.MayBePassedBy(onward: 7, onACall: true));
        Assert.True(waiting.MayBePassedBy(onward: LaneOccupancy.RunsOn, onACall: true));
        Assert.True((waiting with { Still = false }).MayBePassedBy(onward: 7, onACall: true));
        Assert.False((waiting with { Still = false }).MayBePassedBy(onward: 8));
        Assert.False(crossing.MayBePassedBy(onward: 7, onACall: true));
        Assert.False((waiting with { Passing = true }).MayBePassedBy(onward: 8, onACall: true));
    }

    /// <summary>
    /// <b>What a call's pass gets past holds none of its ground but what it can no longer stop short of</b>: a car
    /// it passes moving is held short of the pass, and one that cannot be refuses it. Every other pass gets past
    /// bodies at rest alone, and takes their plans whole.
    /// </summary>
    [Fact]
    public void ACallsPassIsRefusedOnlyByWhatItPassesThatCanNoLongerStop()
    {
        var (way, _) = TwoWays();
        var index = Index();
        var call = new PassTerms(ClaimPriority.Special, 0f, 0f);
        Span<LaneClaim> passed = stackalloc LaneClaim[1];
        passed[0] = new LaneClaim(10f, 14f, 4f, 2, ClaimPriority.Hard, Still: false);

        index.Begin();
        Lay(index, way, holder: 2, fromM: 14f, toM: 30f, committedToM: 16f);
        Assert.True(index.IsFreeForAPass(way, 18f, 40f, 18f, 40f, 1, LaneRoster.Driving, passed, call));
        Assert.False(index.IsFreeForAPass(way, 15f, 40f, 15f, 40f, 1, LaneRoster.Driving, passed, call));
        Assert.True(index.IsFreeForAPass(way, 15f, 40f, 15f, 40f, 1, LaneRoster.Driving, passed, PassTerms.Plain));
    }

    /// <summary>
    /// <b>A body moving down a way comes to rest where its plan there says it can no longer stop short of</b>, or
    /// where it stands, whichever is further.
    /// </summary>
    [Fact]
    public void AMovingBodyStopsByTheEndOfWhatItCanNoLongerStopShortOf()
    {
        var (way, _) = TwoWays();
        var index = Index();
        var moving = new LaneClaim(10f, 14f, 4f, 2, ClaimPriority.Hard, Still: false);

        index.Begin();
        Assert.Equal(14f, index.StopsByM(way, moving));

        Lay(index, way, holder: 2, fromM: 14f, toM: 30f, committedToM: 19f);
        Assert.Equal(19f, index.StopsByM(way, moving));
    }

    /// <summary>
    /// <b>A call's pass takes a plan its rung beats</b> (TER-4c.6, TER-5e): a movement's plan holds none of its
    /// ground, and ground a holder can no longer stop short of and another call's plan hold it as they hold any pass.
    /// </summary>
    [Fact]
    public void ACallsPassTakesAPlanItsRungBeatsAndNoOther()
    {
        var (way, _) = TwoWays();
        var index = Index();
        var call = new PassTerms(ClaimPriority.Special, 0f, 0f);

        index.Begin();
        Lay(index, way, holder: 4, fromM: 20f, toM: 40f);
        Assert.True(index.IsFreeForAPass(way, 5f, 25f, 5f, 25f, 1, LaneRoster.Driving, [], call));
        Assert.False(index.IsFreeForAPass(way, 5f, 25f, 5f, 25f, 1, LaneRoster.Driving, [], PassTerms.Plain));

        index.Begin();
        Lay(index, way, holder: 4, fromM: 20f, toM: 40f, committedToM: 30f);
        Assert.False(index.IsFreeForAPass(way, 5f, 25f, 5f, 25f, 1, LaneRoster.Driving, [], call));

        index.Begin();
        Lay(index, way, holder: 4, fromM: 20f, toM: 40f, rung: ClaimPriority.Special);
        Assert.False(index.IsFreeForAPass(way, 5f, 25f, 5f, 25f, 1, LaneRoster.Driving, [], call));
    }

    /// <summary>
    /// <b>Somebody on foot on the paint a call's pass claims is waited for</b>, and neither refuses the pass nor
    /// withdraws it; somebody off the paint, and a car on it, do both.
    /// </summary>
    [Fact]
    public void ACallsPassWaitsForSomebodyOnThePaintAndNothingElse()
    {
        var (way, _) = TwoWays();
        var index = Index();
        var overThePaint = new PassTerms(ClaimPriority.Special, 15f, 19f);

        index.Begin();
        index.LayPass(way, 5f, 25f, 5f, 1, LaneRoster.Driving);
        index.LayBody(way, 16f, 17f, 0f, 2, LaneRoster.Walking, onItsLine: false);
        Assert.True(index.IsFreeForAPass(way, 5f, 25f, 5f, 25f, 1, LaneRoster.Driving, [], overThePaint));
        Assert.True(index.KeepsItsPass(way, 5f, 25f, 5f, 25f, 1, LaneRoster.Driving, overThePaint));
        Assert.False(index.KeepsItsPass(way, 5f, 25f, 5f, 25f, 1, LaneRoster.Driving, PassTerms.Plain));

        index.LayBody(way, 21f, 22f, 0f, 3, LaneRoster.Walking, onItsLine: false);
        Assert.False(index.IsFreeForAPass(way, 5f, 25f, 5f, 25f, 1, LaneRoster.Driving, [], overThePaint));

        index.Begin();
        index.LayBody(way, 16f, 20f, 0f, 3, LaneRoster.Driving, onItsLine: false, still: true);
        Assert.False(index.IsFreeForAPass(way, 5f, 25f, 5f, 25f, 1, LaneRoster.Driving, [], overThePaint));
    }

    /// <summary>One hold of one piece, answered and laid over a stretch of a way.</summary>
    static void Lay(
        LaneOccupancy index, int way, int holder, float fromM, float toM, ClaimPriority rung = ClaimPriority.Firm,
        float committedToM = float.NegativeInfinity)
    {
        var hold = index.BeginHold(0f);
        var ask = new PlannedAsk(
            hold, holder, LaneRoster.Driving, rung, fromM, LineFromM: fromM, AheadM: 0f,
            CommittedToM: committedToM, AlongMps: 0f);
        var reachM = index.Reach(ask, way, toM, fromM, out var cutBy);
        index.Take(ask, way, reachM);
        index.EndHold(hold, reachM < toM ? reachM : float.PositiveInfinity, 0f, cutBy);
    }
}
