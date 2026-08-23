using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Car.Maneuvers;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Car;

/// <summary>
/// <b>The catalogue's exits, asked of a state rather than of a town.</b> An entry's whole contract is its
/// <c>Sa</c>, its procedure and the successor it names for every way out, and every one of those is
/// arithmetic on <see cref="DriveScene"/> — so the cheapest tier that can answer a question about them is
/// this one, and a soak is never needed to find out what `P-4` does with a headway.
/// </summary>
/// <remarks>
/// The entries whose procedures reach for <see cref="ManeuverDesk"/> are the ones that lay geometry, and
/// they are exercised where geometry is: <see cref="RoadTemplateTests"/> for the shapes and the running
/// town for the rest.
/// </remarks>
[Trait(Tier.Key, Tier.Unit)]
public class ManeuverExitTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>A car running an ordinary line with an empty road in front of it, which every case below varies from.</summary>
    static DriveScene Running => new()
    {
        Config = Config,
        Car = 0,
        AlongMps = 10f,
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
        BlockedS = 0f,
        HeldBackS = 0f,
        WaitedS = 0f,
        GapIsClear = true,
        OnDrivableGround = true,
        BackOffsLeft = 2,
        PlannedMps = 20f,
        ReroutesLeft = 3,
        RecoveriesUsed = 0,
    };

    static ManeuverOutcome Tick(Maneuver id, in DriveScene scene)
    {
        var limits = DriveLimits.None;
        return ManeuverCatalogue.Tick(id, scene, desk: null!, sinceS: Config.Sim.AgentDecisionIntervalS, ref limits);
    }

    // `P-4` — the exits are named off the term that bound the speed profile.

    [Fact]
    public void RunningTheLineOnAnEmptyRoadStaysWhereItIs() =>
        Assert.Equal(ManeuverOutcomeKind.Running, Tick(Maneuver.RunTheLine, Running).Kind);

    /// <summary>
    /// The arguments are the enums' own numbers: <c>DrivingHold</c> and <c>Maneuver</c> are internal to
    /// the assembly under test, and a public test signature may not name them.
    /// </summary>
    [Theory]
    [InlineData((int)DrivingHold.Crossing, (int)Maneuver.PassACrossing)]
    [InlineData((int)DrivingHold.Waiting, (int)Maneuver.HoldAtALine)]
    [InlineData((int)DrivingHold.LineEnd, (int)Maneuver.HoldAtALine)]
    public void RunningTheLineNamesTheEntryTheBindingTermBelongsTo(int hold, int expected)
    {
        var outcome = Tick(Maneuver.RunTheLine, Running with { Hold = (DrivingHold)hold });
        Assert.Equal(ManeuverOutcomeKind.Succeeded, outcome.Kind);
        Assert.Equal((Maneuver)expected, outcome.Next);
    }

    [Fact]
    public void RunningTheLineTakesTheJunctionOnceTheBoxIsItsOwn()
    {
        var scene = Running with { ToTheBoxM = Config.CarJunctionReserveM * 0.5f, BoxIsOurs = true };
        Assert.Equal(Maneuver.TakeTheJunction, Tick(Maneuver.RunTheLine, scene).Next);
    }

    /// <summary>A junction is the only place the route may reverse, so the same approach names a different entry.</summary>
    [Fact]
    public void RunningTheLineTurnsAroundWhereTheRouteReverses()
    {
        var scene = Running with
        {
            ToTheBoxM = Config.CarJunctionReserveM * 0.5f, BoxIsOurs = true, RouteReversesHere = true,
        };

        Assert.Equal(Maneuver.TurnAround, Tick(Maneuver.RunTheLine, scene).Next);
    }

    /// <summary>The line stops where the bay's template is staged from, and what happens there is the plan's next step.</summary>
    [Fact]
    public void RunningTheLineAsksThePlanAtTheEndOfTheLegsLastLane()
    {
        var scene = Running with
        {
            OnTheFinalApproach = true, ProgressM = 399f, AlongMps = 0f, Hold = DrivingHold.LineEnd,
        };

        var outcome = Tick(Maneuver.RunTheLine, scene);
        Assert.Equal(ManeuverOutcomeKind.Succeeded, outcome.Kind);
        Assert.Equal(Maneuver.None, outcome.Next);
    }

    /// <summary>A car off its line keeps running: the standing rules re-acquire, and the blocked clock is what reaches the ladder.</summary>
    [Fact]
    public void RunningTheLineDoesNotHandOverWhenItHasLostTheLine() =>
        Assert.Equal(
            ManeuverOutcomeKind.Running,
            Tick(Maneuver.RunTheLine, Running with { Hold = DrivingHold.LostLine }).Kind);

    // `P-4` again — queueing is this entry with a shorter grant, and the one decision that is not.

    /// <summary>
    /// <b>The grant is a term of the profile and not a hand-over.</b> A car held by the ground it was
    /// given is running its line on a shorter road, which is this entry and needs no other.
    /// </summary>
    [Fact]
    public void RunningTheLineKeepsTheCarWhileTheGrantIsWhatBindsIt()
    {
        var scene = Running with
        {
            Hold = DrivingHold.Reserved,
            Context = DriveContext.Clear with { AuthorityM = Config.Car.LengthM, Ahead = HeadwayKind.Queue },
        };

        Assert.Equal(ManeuverOutcomeKind.Running, Tick(Maneuver.RunTheLine, scene).Kind);
    }

    /// <summary>An obstruction is what the lane index says it is: a wreck, an empty car, a body off its line.</summary>
    [Fact]
    public void RunningTheLineGoesRoundAnObstructionItHasStoodBehindLongEnough()
    {
        var scene = Running with
        {
            AlongMps = 0f,
            BlockedS = Config.Ladder.ObstructionWaitS + 1f,
            Context = DriveContext.Clear with
            {
                HeadwayM = Config.Car.LengthM, HeadwaySpeedMps = 0f, Ahead = HeadwayKind.Obstruction,
            },
        };

        Assert.Equal(Maneuver.GoRound, Tick(Maneuver.RunTheLine, scene).Next);
    }

    /// <summary>
    /// <b>The bound is the clock the watchdog keeps and not the time in the entry.</b> `P-4` is the entry a
    /// car spends its whole journey in, so a car that has been driving for a minute and has just come up
    /// behind a wreck has not been standing behind anything.
    /// </summary>
    [Fact]
    public void RunningTheLineWaitsOutTheObstructionBeforeGoingRound()
    {
        var scene = Running with
        {
            AlongMps = 0f,
            InManeuverS = Config.Ladder.ObstructionWaitS * 100f,
            BlockedS = Config.Ladder.ObstructionWaitS * 0.5f,
            Context = DriveContext.Clear with
            {
                HeadwayM = Config.Car.LengthM, HeadwaySpeedMps = 0f, Ahead = HeadwayKind.Obstruction,
            },
        };

        Assert.Equal(ManeuverOutcomeKind.Running, Tick(Maneuver.RunTheLine, scene).Kind);
    }

    /// <summary>
    /// <b>The one this whole index is for.</b> A driver stopped on the same road, going the same way, is a
    /// queue however long it has stood — whatever holds the car at its head is not this car's to drive
    /// round, and a car that swings out past a stopped queue is a head-on.
    /// </summary>
    [Theory]
    [InlineData((int)HeadwayKind.Queue)]
    [InlineData((int)HeadwayKind.Claimed)]
    [InlineData((int)HeadwayKind.Unknown)]
    public void RunningTheLineNeverGoesRoundWhatIsNotAnObstruction(int ahead)
    {
        var scene = Running with
        {
            AlongMps = 0f,
            BlockedS = Config.Ladder.ObstructionWaitS * 100f,
            Context = DriveContext.Clear with
            {
                HeadwayM = Config.Car.LengthM, HeadwaySpeedMps = 0f, Ahead = (HeadwayKind)ahead,
            },
        };

        Assert.Equal(ManeuverOutcomeKind.Running, Tick(Maneuver.RunTheLine, scene).Kind);
    }

    /// <summary>
    /// <b>Somebody who has stopped in a lane is something to get past</b> (`PER-12`). A walker is an agent
    /// like any other: paint is where a walker's priority lives, and a body standing on bare carriageway is
    /// in the way of a road it is not entitled to. What keeps the swerve off it is the ground under it, and
    /// never a rule that refuses to look.
    /// </summary>
    [Fact]
    public void RunningTheLineGoesRoundSomebodyWhoHasStoppedInTheLane()
    {
        var scene = Running with
        {
            AlongMps = 0f,
            BlockedS = Config.Ladder.ObstructionWaitS + 1f,
            Context = DriveContext.Clear with
            {
                HeadwayM = Config.Car.LengthM, HeadwaySpeedMps = 0f, Ahead = HeadwayKind.Walker,
            },
        };

        Assert.Equal(Maneuver.GoRound, Tick(Maneuver.RunTheLine, scene).Next);
    }

    /// <summary>
    /// <b>And one still walking is gone round too, which waiting cannot fix.</b> A body reeling down the
    /// middle of a carriageway holds up everything behind it precisely by moving: no clock it is measured
    /// on ever runs out, because the car never stands still to spend one.
    /// </summary>
    [Fact]
    public void RunningTheLineGoesRoundSomebodyStillWalkingWhenItIsGainingOnThem() =>
        Assert.Equal(Maneuver.GoRound, Tick(Maneuver.RunTheLine, Wanting).Next);

    /// <summary>
    /// <b>Something moving at most of what the road affords is followed and not passed.</b> The wrong side
    /// of the road has to buy something, and a swerve that gains a driver a fraction of its own pace has
    /// spent the oncoming lane for nothing.
    /// </summary>
    [Fact]
    public void RunningTheLineNeverGoesRoundSomethingBarelySlowerThanTheRoadAffords()
    {
        var scene = Running with
        {
            AlongMps = 18f,
            PlannedMps = 20f,
            Hold = DrivingHold.Headway,
            Context = DriveContext.Clear with
            {
                HeadwayM = Config.Car.LengthM * 2f,
                HeadwaySpeedMps = 20f * Config.Driving.PassWorthShare,
                Ahead = HeadwayKind.Walker,
            },
        };

        Assert.Equal(ManeuverOutcomeKind.Running, Tick(Maneuver.RunTheLine, scene).Kind);
    }

    /// <summary>
    /// And a car that is no faster than what is in front waits, however slow the pair of them are: there is
    /// no ground on which it gets past, so a swerve would be an overtake that never ends.
    /// </summary>
    [Fact]
    public void RunningTheLineNeverGoesRoundSomethingItIsNotGainingOn()
    {
        var scene = Running with
        {
            AlongMps = Config.Person.WalkSpeedMps,
            PlannedMps = 30f,
            Hold = DrivingHold.Headway,
            Context = DriveContext.Clear with
            {
                HeadwayM = Config.Car.LengthM * 2f,
                HeadwaySpeedMps = Config.Person.WalkSpeedMps,
                Ahead = HeadwayKind.Walker,
            },
        };

        Assert.Equal(ManeuverOutcomeKind.Running, Tick(Maneuver.RunTheLine, scene).Kind);
    }

    /// <summary>Somebody entitled to be there is not an obstruction, whatever the probe says about them.</summary>
    [Fact]
    public void RunningTheLineNeverGoesRoundSomethingWithPriority()
    {
        var scene = Running with
        {
            AlongMps = 0f,
            BlockedS = Config.Ladder.ObstructionWaitS + 1f,
            LightAheadM = 5f,
            Context = DriveContext.Clear with
            {
                HeadwayM = Config.Car.LengthM, HeadwaySpeedMps = 0f, Ahead = HeadwayKind.Obstruction,
            },
        };

        Assert.Equal(ManeuverOutcomeKind.Running, Tick(Maneuver.RunTheLine, scene).Kind);
    }

    // `P-6` — the exit follows the body, not the line.

    [Fact]
    public void HoldingAtALineStaysWhileTheStopPointStands()
    {
        var scene = Running with { Context = DriveContext.Clear with { StopAtM = 8f } };
        Assert.Equal(ManeuverOutcomeKind.Running, Tick(Maneuver.HoldAtALine, scene).Kind);
    }

    /// <summary>The one that removed the shuttle: a stationary car is not handed back however clear the line reads.</summary>
    [Fact]
    public void HoldingAtALineDoesNotHandBackAStationaryCar() =>
        Assert.Equal(
            ManeuverOutcomeKind.Running,
            Tick(Maneuver.HoldAtALine, Running with { AlongMps = 0f }).Kind);

    [Fact]
    public void HoldingAtALineGivesTheCarBackOnceItIsMovingAgain()
    {
        var outcome = Tick(Maneuver.HoldAtALine, Running);
        Assert.Equal(Maneuver.RunTheLine, outcome.Next);
        Assert.Equal(ManeuverReason.WayIsClear, outcome.Why);
    }

    /// <summary>A red keeps the car here even with no stop point on the line, because the light is the reason it is stopped.</summary>
    [Fact]
    public void HoldingAtALineStaysForALightWithNoStopPointOnTheLine() =>
        Assert.Equal(
            ManeuverOutcomeKind.Running,
            Tick(Maneuver.HoldAtALine, Running with { LightAheadM = 12f }).Kind);

    // `P-8` — through, or refused at the boundary.

    [Fact]
    public void TakingTheJunctionHoldsWhileTheBodyIsInTheBox()
    {
        var scene = Running with { InsideTheBox = true, BoxIsOurs = true, ToTheBoxM = 0f };
        Assert.Equal(ManeuverOutcomeKind.Running, Tick(Maneuver.TakeTheJunction, scene).Kind);
    }

    [Fact]
    public void TakingTheJunctionEndsWhenTheBoxIsBehindTheBody()
    {
        var scene = Running with { InsideTheBox = false, BoxIsOurs = false, ToTheBoxM = float.PositiveInfinity };
        Assert.Equal(Maneuver.RunTheLine, Tick(Maneuver.TakeTheJunction, scene).Next);
    }

    /// <summary>A claim lost before the car committed is a stop at the boundary, which is `P-6`'s.</summary>
    [Fact]
    public void TakingTheJunctionRefusedBeforeCommittingStopsAtTheBoundary()
    {
        var scene = Running with { InsideTheBox = false, BoxIsOurs = false, ToTheBoxM = 3f };
        var outcome = Tick(Maneuver.TakeTheJunction, scene);
        Assert.Equal(ManeuverOutcomeKind.Failed, outcome.Kind);
        Assert.Equal(Maneuver.HoldAtALine, outcome.Next);
    }

    // `P-12` — the paint, and nothing else.

    [Fact]
    public void PassingACrossingHoldsWhileThePaintIsAhead()
    {
        var scene = Running with { Context = DriveContext.Clear with { CrossingAtM = 6f } };
        Assert.Equal(ManeuverOutcomeKind.Running, Tick(Maneuver.PassACrossing, scene).Kind);
    }

    [Fact]
    public void PassingACrossingEndsWhenThePaintIsBehindTheBody()
    {
        var outcome = Tick(Maneuver.PassACrossing, Running);
        Assert.Equal(Maneuver.RunTheLine, outcome.Next);
        Assert.Equal(ManeuverReason.PaintIsBehind, outcome.Why);
    }

    // `E-1` — the name and the bound, and nothing else.

    [Fact]
    public void YieldingHoldsWhileTheObstructionHasPriority() =>
        Assert.Equal(
            ManeuverOutcomeKind.Running,
            Tick(Maneuver.Yield, Running with { LightAheadM = 10f }).Kind);

    [Fact]
    public void YieldingHandsBackOnlyOnceTheCarIsMovingAgain()
    {
        Assert.Equal(ManeuverOutcomeKind.Running, Tick(Maneuver.Yield, Running with { AlongMps = 0f }).Kind);
        Assert.Equal(ManeuverOutcomeKind.Resume, Tick(Maneuver.Yield, Running).Kind);
    }

    /// <summary>Waiting for a junction somebody else is in is correct until it has been correct for half a minute.</summary>
    [Fact]
    public void YieldingIsBoundedByTheBlockedClock()
    {
        var scene = Running with { LightAheadM = 10f, AlongMps = 0f, InManeuverS = Config.CarBlockedRoadS + 1f };
        Assert.Equal(ManeuverOutcomeKind.Escalate, Tick(Maneuver.Yield, scene).Kind);
    }

    // `E-2` — the trigger, which is the whole entry.

    [Fact]
    public void AQueueMovingAtTheSamePaceIsNeverAHazardHoweverCloseItIs()
    {
        var scene = Running with
        {
            Context = DriveContext.Clear with { HeadwayM = Config.Car.LengthM, HeadwaySpeedMps = 10f },
        };

        Assert.False(E02EmergencyStop.IsAHazard(scene));
    }

    [Fact]
    public void ClosingOnSomethingStandingStillInsideBrakingDistanceIsAHazard()
    {
        var scene = Running with
        {
            AlongMps = 20f,
            Context = DriveContext.Clear with { HeadwayM = Config.Car.LengthM, HeadwaySpeedMps = 0f },
        };

        Assert.True(E02EmergencyStop.IsAHazard(scene));
    }

    [Fact]
    public void AStoppedCarIsNeverInAnEmergency() =>
        Assert.False(E02EmergencyStop.IsAHazard(Running with { AlongMps = 0f }));

    /// <summary>
    /// <b>What the profile plans every stop at is not an emergency.</b> `E-2` is the tick the margin the
    /// profile keeps back ran out, so its threshold is that same figure read without the margin — set below
    /// it, the entry fires on ordinary planned braking and takes the pedal off the plan that was working.
    /// </summary>
    [Fact]
    public void TheProfilesOwnPlannedStopIsNeverAnEmergency()
    {
        var closingMps = 20f;

        // The ground the profile itself would choose to shed this in, offered as the gap `E-2` measures.
        var gapM = closingMps * closingMps / (2f * CarFollower.BrakingMps2(Config, 1f));
        var planned = Running with
        {
            AlongMps = closingMps,
            Context = DriveContext.Clear with
            {
                HeadwayM = gapM + (Config.Car.LengthM * 0.5f), HeadwaySpeedMps = 0f,
            },
        };

        Assert.False(E02EmergencyStop.IsAHazard(planned));

        // Half that ground is past what the tyres have at all, whatever anybody planned.
        Assert.True(
            E02EmergencyStop.IsAHazard(
                planned with
                {
                    Context = planned.Context with { HeadwayM = (gapM * 0.5f) + (Config.Car.LengthM * 0.5f) },
                }));
    }

    /// <summary>A reflex keeps its name for a beat, so one emergency stop is counted once and not twenty times.</summary>
    [Fact]
    public void TheEmergencyStopHoldsItsNameForABeatAfterTheHazardHasGone()
    {
        Assert.Equal(
            ManeuverOutcomeKind.Running,
            Tick(Maneuver.EmergencyStop, Running with { InManeuverS = 0f }).Kind);

        Assert.Equal(
            ManeuverOutcomeKind.Resume,
            Tick(Maneuver.EmergencyStop, Running with { InManeuverS = Config.Ladder.ReflexHoldS + 0.1f }).Kind);
    }

    [Fact]
    public void TheEmergencyStopSpendsWhatIsLeftOfTheTyre()
    {
        var scene = Running with
        {
            AlongMps = 20f,
            Context = DriveContext.Clear with { HeadwayM = Config.Car.LengthM, HeadwaySpeedMps = 0f },
        };

        var limits = DriveLimits.None;
        ManeuverCatalogue.Tick(Maneuver.EmergencyStop, scene, null!, 0.1f, ref limits);
        Assert.True(limits.SpendTheTyre);
        Assert.True(limits.HoldStill);
    }

    /// <summary>A car gaining on somebody reeling down its lane, which is everything `E-4` wants.</summary>
    static DriveScene Wanting => Running with
    {
        AlongMps = 20f,
        PlannedMps = 30f,
        Hold = DrivingHold.Headway,
        HeldBackS = Config.Ladder.ObstructionWaitS + 1f,
        Context = DriveContext.Clear with
        {
            HeadwayM = Config.Car.LengthM * 2f,
            HeadwaySpeedMps = Config.Person.WalkSpeedMps,
            Ahead = HeadwayKind.Walker,
        },
    };

    /// <summary>
    /// <b>And somebody crossing the road is followed and not passed.</b> A body in the lane for a second is
    /// out of it before the wrong side of the road buys anything, and the wait is what tells it from one
    /// reeling down the middle — which is the same wait a car standing behind a wreck spends.
    /// </summary>
    [Fact]
    public void RunningTheLineWaitsOutSomethingMovingBeforeGoingRoundIt() =>
        Assert.Equal(
            ManeuverOutcomeKind.Running,
            Tick(Maneuver.RunTheLine, Wanting with { HeldBackS = Config.Ladder.ObstructionWaitS * 0.5f }).Kind);

    // the recoveries that drive a template of their own, on the one exit that needs no desk

    [Theory]
    [InlineData((int)Maneuver.BackOff)]
    [InlineData((int)Maneuver.GoRound)]
    [InlineData((int)Maneuver.ReturnToLegalGround)]
    [InlineData((int)Maneuver.TurnAround)]
    public void ATemplateThatIsSpentHandsTheCarBackToTheLine(int entry)
    {
        var scene = Running with
        {
            Line = new DrivenLine(1, 0, 6f), ProgressM = 6f, AlongMps = 0f,
        };

        var outcome = Tick((Maneuver)entry, scene);
        Assert.Equal(Maneuver.RunTheLine, outcome.Next);
        Assert.Equal(ManeuverReason.LineSpent, outcome.Why);
    }

    /// <summary>And an entry that finds it is no longer on a template of its own has lost it, which is a failure and not a success.</summary>
    [Theory]
    [InlineData((int)Maneuver.BackOff)]
    [InlineData((int)Maneuver.GoRound)]
    [InlineData((int)Maneuver.ReturnToLegalGround)]
    [InlineData((int)Maneuver.ParkInTheBay)]
    [InlineData((int)Maneuver.SquareUpInTheBay)]
    public void AnEntryThatHasLostItsTemplateFails(int entry)
    {
        var outcome = Tick((Maneuver)entry, Running);
        Assert.Equal(ManeuverOutcomeKind.Failed, outcome.Kind);
        Assert.Equal(ManeuverReason.LostTheLine, outcome.Why);
    }

    // the terminal three

    [Theory]
    [InlineData((int)Maneuver.StandParked)]
    [InlineData((int)Maneuver.SettleForHere)]
    [InlineData((int)Maneuver.AbandonTheCar)]
    public void TheTerminalEntriesEndTheLegOnTheirFirstTick(int entry)
    {
        Assert.True(Maneuvers.IsTerminal((Maneuver)entry));
        Assert.Equal(ManeuverOutcomeKind.Finished, Tick((Maneuver)entry, Running).Kind);
    }

    // the traits

    /// <summary>The catalogue splits exactly where the brief does: everything from `E-1` on is reactive.</summary>
    [Fact]
    public void EveryEEntryIsReactiveAndNoPEntryIs()
    {
        foreach (var entry in Enum.GetValues<Maneuver>())
        {
            var code = Maneuvers.Code(entry);
            if (entry == Maneuver.None) continue;

            Assert.Equal(code.StartsWith('E'), Maneuvers.IsReactive(entry));
        }
    }

    /// <summary>Every entry has a code the brief would recognise, so a trace's output can be looked up.</summary>
    [Fact]
    public void EveryEntryPrintsACodeFromTheBrief()
    {
        foreach (var entry in Enum.GetValues<Maneuver>())
        {
            Assert.NotEqual("?", Maneuvers.Code(entry));
        }
    }

    /// <summary>A car standing across a lane is itself the obstruction, and patience there is the wrong answer.</summary>
    [Fact]
    public void TheFuseIsShortWhereTheBodyIsAcrossALane()
    {
        var acrossALane = Running with { InsideTheBox = true };
        Assert.Equal(Config.CarShortFuseS, ManeuverCatalogue.FuseS(Maneuver.TakeTheJunction, acrossALane));
        Assert.Equal(Config.CarBlockedRoadS, ManeuverCatalogue.FuseS(Maneuver.RunTheLine, Running));
    }

    /// <summary>`P-11` is across a lane by construction, so timing it as a fault would escalate a manoeuvre that is simply long.</summary>
    [Fact]
    public void TheTurnAroundIsNotMeasuredOnTheShortFuse()
    {
        var acrossALane = Running with { InsideTheBox = true };
        Assert.Equal(Config.CarBlockedRoadS, ManeuverCatalogue.FuseS(Maneuver.TurnAround, acrossALane));
    }

    /// <summary>The entries whose standing still <em>is</em> the procedure are the only ones the fuse does not watch.</summary>
    [Fact]
    public void OnlyTheEntriesThatStandStillOnPurposeAreUnwatched()
    {
        Assert.False(ManeuverCatalogue.Watched(Maneuver.StandParked));
        Assert.False(ManeuverCatalogue.Watched(Maneuver.SettleForHere));
        Assert.False(ManeuverCatalogue.Watched(Maneuver.AbandonTheCar));
        Assert.True(ManeuverCatalogue.Watched(Maneuver.RunTheLine));
        Assert.True(ManeuverCatalogue.Watched(Maneuver.Yield));
    }

    /// <summary>
    /// The two kinds that may not be scheduled: negotiating with something that is itself moving, and
    /// steering to a pose. Everything else is deliberation, and that is what the clock is for.
    /// </summary>
    [Theory]
    [InlineData((int)Maneuver.LeaveTheBay)]
    [InlineData((int)Maneuver.HoldAtALine)]
    [InlineData((int)Maneuver.TakeTheJunction)]
    [InlineData((int)Maneuver.TurnAround)]
    [InlineData((int)Maneuver.PassACrossing)]
    [InlineData((int)Maneuver.ParkInTheBay)]
    [InlineData((int)Maneuver.SquareUpInTheBay)]
    [InlineData((int)Maneuver.Yield)]
    [InlineData((int)Maneuver.EmergencyStop)]
    [InlineData((int)Maneuver.BackOff)]
    [InlineData((int)Maneuver.GoRound)]
    [InlineData((int)Maneuver.ReturnToLegalGround)]
    public void TheEntriesThatMayNotBeScheduledSaySo(int entry) =>
        Assert.True(ManeuverCatalogue.ThinksEveryTick((Maneuver)entry));

    [Theory]
    [InlineData((int)Maneuver.RunTheLine)]
    [InlineData((int)Maneuver.StandParked)]
    public void TheDeliberationRunsOnTheDecisionClock(int entry) =>
        Assert.False(ManeuverCatalogue.ThinksEveryTick((Maneuver)entry));
}
