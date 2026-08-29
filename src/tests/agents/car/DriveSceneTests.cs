using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Car.Maneuvers;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Car;

/// <summary>
/// The readings the catalogue's entry conditions are arithmetic on — <b>what a car in a given state is
/// allowed to conclude about what is in front of it</b>, asked of the scene itself rather than of a town.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class DriveSceneTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>A car running an ordinary line, stopped, with an empty road in front of it.</summary>
    static DriveScene Stopped => new()
    {
        Config = Config,
        Car = 0,
        Build = CarBuild.Nominal(Config, Config.Car.DrivenFrontShare),
        AlongMps = 0f,
        ProgressM = 20f,
        Line = new DrivenLine(4, 2, 400f),
        Reverse = false,
        Hold = DrivingHold.None,
        Context = DriveContext.Clear,
        ToTheBoxM = float.PositiveInfinity,
        BoxIsOurs = false,
        InsideTheBox = false,
        LightAheadM = float.PositiveInfinity,
        BayHeld = -1,
        BayBooked = -1,
        OnTheFinalApproach = false,
        ToTheBayM = float.PositiveInfinity,
        ToTheSceneM = float.PositiveInfinity,
        Urgent = false,
        LaneOn = 3,
        TurnsBackHere = false,
        InManeuverS = 1f,
        BlockedS = 10f,
        HeldBackS = 0f,
        OnDrivableGround = true,
        BackOffsLeft = 2,
        PlannedMps = 20f,
        ReroutesLeft = 3,
        RecoveriesUsed = 0,
    };

    static DriveScene BehindA(HeadwayKind kind, DrivingHold hold = DrivingHold.Headway) =>
        Stopped with
        {
            Hold = hold,
            Context = DriveContext.Clear with
            {
                HeadwayM = Config.Car.LengthM, HeadwaySpeedMps = 0f, Ahead = kind,
            },
        };

    /// <summary>
    /// <b>A driver queueing behind another driver has nothing to back away from</b> (`E-3`). Whatever holds
    /// the car in front is going to move, and reversing out of the queue neither clears it nor changes the
    /// decision that is about to be re-taken from further back — it only puts a body into the road the
    /// traffic behind is entitled to be standing in.
    /// </summary>
    [Fact]
    public void AQueueIsNotSomethingToBackAwayFrom() =>
        Assert.False(BehindA(HeadwayKind.Queue).SomethingToBackAwayFrom);

    /// <summary>And the same for ground a crossing movement has claimed, which is another driver's road said another way.</summary>
    [Fact]
    public void GroundSomebodyElseHasClaimedIsNotSomethingToBackAwayFrom() =>
        Assert.False(BehindA(HeadwayKind.Claimed, DrivingHold.Waiting).SomethingToBackAwayFrom);

    /// <summary>
    /// Nor is a bar, a red or a crossing a car is waiting <em>on its own lane</em> for: it is waiting, and
    /// the wait is what ends it. <b>The same bar with the body standing in a box is</b> — what it is backing
    /// out of there is the ground it is itself blocking.
    /// </summary>
    [Fact]
    public void ALineTheCarIsHeldAtCountsOnlyWhereTheBodyIsItselfInTheWay()
    {
        var held = Stopped with
        {
            Hold = DrivingHold.Waiting, Context = DriveContext.Clear with { StopAtM = 0f },
        };

        Assert.False(held.SomethingToBackAwayFrom);
        Assert.True((held with { InsideTheBox = true }).SomethingToBackAwayFrom);
    }

    /// <summary>
    /// <b>What is</b>: a body that had no business being in the road, a template the car can no longer
    /// follow, and a line it has lost — the three states `E-3` is for.
    /// </summary>
    [Fact]
    public void AWreckAWalkerATemplateAndALostLineAre()
    {
        Assert.True(BehindA(HeadwayKind.Obstruction).SomethingToBackAwayFrom);
        Assert.True(BehindA(HeadwayKind.Walker).SomethingToBackAwayFrom);
        Assert.True((Stopped with { Hold = DrivingHold.LostLine }).SomethingToBackAwayFrom);
        Assert.True((Stopped with { Line = new DrivenLine(1, 0, 8f) }).SomethingToBackAwayFrom);
    }

    /// <summary>
    /// AMB-4.4: <b>a driver with a blue light on goes round a queue, and spends no patience first.</b>
    /// Ordinary traffic waits behind one however long it stands, because the car at its head is held by
    /// something that is not this driver's to drive round — and an ambulance is the case where that
    /// reasoning stops holding.
    /// </summary>
    [Fact]
    public void ARescueGoesRoundAQueueWithoutWaitingItsPatienceOut()
    {
        var behindAQueue = BehindA(HeadwayKind.Queue) with { BlockedS = 0f, HeldBackS = 0f };

        Assert.False(behindAQueue.WorthGoingRound);
        Assert.True((behindAQueue with { Urgent = true }).WorthGoingRound);
    }

    /// <summary>
    /// <b>What the blue light does not relax</b>: something the book cannot name is never driven round,
    /// and nothing is worth passing that is not slower than the road affords.
    /// </summary>
    [Fact]
    public void ARescueStillDoesNotPassWhatItCannotNameOrWhatIsNotSlower()
    {
        var urgent = BehindA(HeadwayKind.Obstruction) with { Urgent = true };

        Assert.False((urgent with { Context = urgent.Context with { Ahead = HeadwayKind.Unknown } }).WorthGoingRound);
        Assert.False(
            (urgent with { Context = urgent.Context with { HeadwaySpeedMps = urgent.PlannedMps } }).WorthGoingRound);
    }

    /// <summary>
    /// And not on the last dozen metres of a leg: past the point the line leaves the road for a bay there
    /// is nothing to gain by getting in front of anybody, and a driver with no patience to spend would
    /// swerve round the cars parked beside its own bay for as long as they were there.
    /// </summary>
    [Fact]
    public void ARescueDoesNotOvertakeOnItsFinalApproach()
    {
        var urgent = BehindA(HeadwayKind.Obstruction) with { Urgent = true };

        Assert.True(urgent.WorthGoingRound);
        Assert.False((urgent with { OnTheFinalApproach = true }).WorthGoingRound);
    }

    /// <summary>
    /// <b>Overtaking is a manoeuvre of a road segment and never of a junction</b> (`E-4`) — a car standing
    /// in a box does not go round what is in front of it, and neither does one near enough to the next box
    /// to be negotiating it.
    /// </summary>
    /// <remarks>
    /// <b>A junction has no centreline to cross</b> (CAR-6.2b) and no lane for the swerve's own claim to be
    /// laid on, so what holds the traffic behind off the ground the shape swings through is not written at
    /// all — and the movements through the box were each arbitrated on the town's own table (TER-5c), which
    /// says where a crossing car goes only for as long as it follows the join it claimed.
    /// </remarks>
    [Fact]
    public void NobodyGoesRoundAnythingAtAJunction()
    {
        var behind = BehindA(HeadwayKind.Obstruction);

        Assert.True(behind.WorthGoingRound);
        Assert.False((behind with { InsideTheBox = true }).WorthGoingRound);
        Assert.False((behind with { ToTheBoxM = Config.CarJunctionReserveM }).WorthGoingRound);

        // And the box being this car's to cross is no licence either: what it bought is the movement, which
        // is a line through the junction and not the road beside it.
        Assert.False((behind with { ToTheBoxM = 0f, BoxIsOurs = true }).WorthGoingRound);

        // Clear of it by a metre and the segment is a segment again.
        Assert.True((behind with { ToTheBoxM = Config.CarJunctionReserveM + 1f }).WorthGoingRound);
    }

    /// <summary>
    /// <b>And a blue light does not buy a junction overtake</b> (AMB-4). What a call lifts is whose turn it
    /// is; the ground beside a car in a box is other movements' and there is none of it to be given.
    /// </summary>
    [Fact]
    public void ARescueDoesNotOvertakeAtAJunctionEither()
    {
        var urgent = BehindA(HeadwayKind.Obstruction) with { Urgent = true };

        Assert.True(urgent.WorthGoingRound);
        Assert.False((urgent with { InsideTheBox = true }).WorthGoingRound);
        Assert.False((urgent with { ToTheBoxM = Config.CarJunctionReserveM }).WorthGoingRound);
    }

    /// <summary>
    /// `P-18`'s <c>Sa</c> is a place on the line, and every other car in the town has none: infinity is
    /// what says "this driver was not sent anywhere", and it must never read as a stop point.
    /// </summary>
    [Fact]
    public void ACarThatWasNotSentAnywhereHasNoSceneAheadOfIt()
    {
        Assert.True(float.IsPositiveInfinity(Stopped.ToTheSceneM));
        Assert.False(Stopped.Urgent);
    }
}
