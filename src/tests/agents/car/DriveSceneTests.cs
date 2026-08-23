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
        BayReserved = -1,
        OnTheFinalApproach = false,
        LaneOn = 3,
        RouteReversesHere = false,
        InManeuverS = 1f,
        BlockedS = 10f,
        HeldBackS = 0f,
        WaitedS = 0f,
        GapIsClear = true,
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
}
