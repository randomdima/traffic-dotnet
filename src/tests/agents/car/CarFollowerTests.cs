using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Car;

/// <summary>
/// The driver, asked against a line and a pose and nothing else — no town, no solver, no body. Every
/// question here has an arithmetic answer: the speed a corner of a given radius may be taken at, the
/// steering angle a circle of a given radius needs, the distance a stop needs at a given speed.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P2)]
public class CarFollowerTests
{
    static readonly SimConfig Figures = SimConfig.Shipped();

    /// <summary>The nominal car: what is asked here is the controller's arithmetic, not a variant's.</summary>
    static readonly CarBuild Car = CarBuild.Nominal(Figures, Figures.Car.DrivenFrontShare);

    static readonly ArcSeg[] Straight = [new ArcSeg(Vector2.Zero, 0f, 400f, 0f)];

    static CarPose At(float alongM, float alongMps) =>
        new(new Vector2(alongM + Car.CentreAheadOfAxleM, 0f), 0f, new Vector2(alongMps, 0f), 0f,
            Figures.Car.MassKg);

    static float Target(ReadOnlySpan<ArcSeg> line, float progressM, float alongMps, in DriveContext context, out DrivingHold hold) =>
        Target(line, progressM, alongMps, context, out hold, out _);

    static float Target(
        ReadOnlySpan<ArcSeg> line, float progressM, float alongMps, in DriveContext context, out DrivingHold hold,
        out float plannedMps)
    {
        var entryM = new float[line.Length];
        CornerLimits.Lay(line, entryM, Figures);

        return CarFollower.TargetSpeedMps(
            Figures, Car, line, entryM, progressM, Spline.TotalLengthM(line), 0f, alongMps,
            CarFollower.LookaheadM(Car, alongMps, Figures.Driving.LookaheadS), context, out hold, out plannedMps);
    }

    [Fact]
    public void AStraightAheadAsksForNoSteeringAtAll()
    {
        var steerRad = CarFollower.Steer(Car, Straight, 10f, new Vector2(10f, 0f), Vector2.UnitX, 8f);

        Assert.Equal(0f, steerRad, 1e-4f);
    }

    /// <summary>
    /// Pure pursuit turns the wheel for the circle through the axle and the lead point, so a car half a
    /// lane off its line steers back onto it — and the harder it is off, the more wheel it asks for.
    /// </summary>
    [Fact]
    public void ACarOffToTheLeftOfItsLineSteersBackTowardsIt()
    {
        var gentle = CarFollower.Steer(Car, Straight, 10f, new Vector2(10f, -0.5f), Vector2.UnitX, 8f);
        var harder = CarFollower.Steer(Car, Straight, 10f, new Vector2(10f, -2f), Vector2.UnitX, 8f);

        Assert.True(gentle > 0f, "a car left of its line turns right to rejoin it");
        Assert.True(harder > gentle);
        Assert.True(harder <= Figures.Car.MaxSteeringDeg * MathF.PI / 180f, "and never past the lock");
    }

    /// <summary>Nothing ever asks for more wheel than the car has, whatever the line does.</summary>
    [Theory]
    [InlineData(-30f)]
    [InlineData(30f)]
    public void TheWheelIsNeverTurnedPastItsOwnLock(float acrossM)
    {
        var steerRad = CarFollower.Steer(Car, Straight, 10f, new Vector2(10f, acrossM), Vector2.UnitX, 4f);

        Assert.InRange(
            steerRad, -Figures.Car.MaxSteeringDeg * MathF.PI / 180f, Figures.Car.MaxSteeringDeg * MathF.PI / 180f);
    }

    /// <summary>A corner is taken at √(a·R), which is the one figure that decides how hard a car can corner.</summary>
    [Theory]
    [InlineData(10f)]
    [InlineData(40f)]
    public void ACornerIsTakenAtWhatTheTyresAffordOnIt(float radiusM)
    {
        var lateralMps2 = Figures.TyreGripMps2 * Figures.Driving.GripMargin;
        ReadOnlySpan<ArcSeg> bend = [new ArcSeg(Vector2.Zero, 0f, radiusM * 2f, 1f / radiusM)];

        var targetMps = Target(bend, 1f, 5f, DriveContext.Clear, out var hold);

        Assert.Equal(MathF.Sqrt(lateralMps2 * radiusM), targetMps, 0.1f);
        Assert.Equal(DrivingHold.Corner, hold);
    }

    /// <summary>An open straight is held to the gear's own cap and to nothing else.</summary>
    [Fact]
    public void AnOpenRoadIsHeldToTheGearsOwnCap()
    {
        var targetMps = Target(Straight, 10f, 20f, DriveContext.Clear, out var hold);

        Assert.Equal(Figures.Car.MaxSpeedMps, targetMps, 0.5f);
        Assert.Equal(DrivingHold.None, hold);
    }

    /// <summary>
    /// <b>Speed is the minimum of everything</b>, and which term won is which constraint was smallest.
    /// One line, one pose, four different worlds.
    /// </summary>
    [Fact]
    public void SpeedIsTheLeastOfEverythingThatLimitsTheCar()
    {
        var open = Target(Straight, 10f, 20f, DriveContext.Clear, out _);

        var shortOfALight = Target(
            Straight, 10f, 20f, DriveContext.Clear with { AuthorityM = 8f, GrantCutBy = HeadwayKind.Light },
            out var waiting);
        var nearTheEnd = Target(Straight, 380f, 20f, DriveContext.Clear, out var ending);
        var shortOfGround = Target(
            Straight, 10f, 20f, DriveContext.Clear with { AuthorityM = 8f }, out var granted);

        Assert.True(shortOfALight < open);
        Assert.True(nearTheEnd < open);
        Assert.True(shortOfGround < open);
        Assert.Equal(DrivingHold.Waiting, waiting);
        Assert.Equal(DrivingHold.LineEnd, ending);
        Assert.Equal(DrivingHold.Claimed, granted);
    }

    /// <summary>
    /// <b>A car keeps to the section it was granted</b> (S-2a): a grant of exactly its lead and its stop at the
    /// speed it is doing is what holds that speed — less road slows it and more lets it gather pace.
    /// </summary>
    [Theory]
    [InlineData(8f)]
    [InlineData(15f)]
    [InlineData(25f)]
    public void AGrantOfItsLeadAndItsStopHoldsTheSpeedItIsDoing(float alongMps)
    {
        var brakingMps2 = Car.PlannedBrakingMps2;
        var neededM = (alongMps * CarFollower.LeadS(Car)) + (alongMps * alongMps / (2f * brakingMps2));

        var held = Target(Straight, 10f, alongMps, DriveContext.Clear with { AuthorityM = neededM }, out var hold);
        var shorter = Target(Straight, 10f, alongMps, DriveContext.Clear with { AuthorityM = neededM - 5f }, out _);
        var longer = Target(Straight, 10f, alongMps, DriveContext.Clear with { AuthorityM = neededM + 5f }, out _);

        Assert.Equal(alongMps, held, 1e-2f);
        Assert.Equal(DrivingHold.Claimed, hold);
        Assert.True(shorter < alongMps);
        Assert.True(longer > alongMps);
    }

    /// <summary>
    /// <b>What cut the grant is no term of the speed</b>: a body, a light and ground somebody claimed are all the same
    /// distance to a driver, which is the whole of following.
    /// </summary>
    /// <remarks>The kinds are passed as their bytes, the enum being the engine's own and not the suite's.</remarks>
    [Theory]
    [InlineData((byte)HeadwayKind.Body)]
    [InlineData((byte)HeadwayKind.Light)]
    public void WhateverCutTheGrantTheSpeedIsTheSame(byte cutBy)
    {
        var claimed = Target(
            Straight, 10f, 15f, DriveContext.Clear with { AuthorityM = 30f, GrantCutBy = HeadwayKind.Claimed }, out _);
        var cut = Target(
            Straight, 10f, 15f, DriveContext.Clear with { AuthorityM = 30f, GrantCutBy = (HeadwayKind)cutBy }, out _);

        Assert.Equal(claimed, cut);
    }

    /// <summary>
    /// <b>What is left of the tyre is spent where the grant is cut shorter than the car can stop in</b> (S-2):
    /// short of what even the tyres' utmost stops it in, and never on road it can.
    /// </summary>
    [Fact]
    public void AGrantCutShortUnderTheCarIsAHazardAndOneItCanStopInIsNot()
    {
        const float AlongMps = 15f;
        var utmostM = AlongMps * AlongMps / (2f * Car.UtmostBrakingMps2);

        Assert.True(CarFollower.IsAHazard(Figures, Car, AlongMps, DriveContext.Clear with { AuthorityM = utmostM * 0.9f }));
        Assert.False(CarFollower.IsAHazard(Figures, Car, AlongMps, DriveContext.Clear with { AuthorityM = utmostM * 1.1f }));
        Assert.False(CarFollower.IsAHazard(Figures, Car, AlongMps, DriveContext.Clear));
    }

    /// <summary>
    /// <b>A bend ahead is braked for off the arc the car is coming to</b> (S-2): nearer it the car is held to less,
    /// and what holds it is the corner.
    /// </summary>
    [Fact]
    public void ABendAheadIsBrakedForBeforeTheCarReachesIt()
    {
        ReadOnlySpan<ArcSeg> line = [new ArcSeg(Vector2.Zero, 0f, 200f, 0f), new ArcSeg(new Vector2(200f, 0f), 0f, 30f, 1f / 15f)];

        var far = Target(line, 20f, 20f, DriveContext.Clear, out _);
        var near = Target(line, 150f, 20f, DriveContext.Clear, out var hold);

        Assert.True(near < far);
        Assert.Equal(DrivingHold.Corner, hold);
    }

    /// <summary>
    /// <b>The end of its own plan is a stop point and not a limit on the plan</b> (TER-4c.1): it slows the car, and
    /// the speed the next plan is sized by is what the car would do without it — or the plan would shrink to fit it.
    /// </summary>
    [Fact]
    public void TheEndOfItsOwnPlanSlowsTheCarAndNotItsNextPlan()
    {
        var open = Target(Straight, 10f, 20f, DriveContext.Clear, out _, out var openPlannedMps);
        var held = Target(Straight, 10f, 20f, DriveContext.Clear with { HorizonM = 20f }, out var hold, out var heldPlannedMps);

        Assert.True(held < open);
        Assert.Equal(DrivingHold.Reach, hold);
        Assert.Equal(openPlannedMps, heldPlannedMps);
    }

    /// <summary>
    /// <b>The speed a stop allows does not move with the speed the car is doing</b> (S-2): read at that speed, the lead
    /// it covers first grows with it, and close to the stop a car a little quick was told to be a lot slower — the
    /// pedal overshooting each way, throttle and brake in turn.
    /// </summary>
    [Theory]
    [InlineData(0.5f)]
    [InlineData(2f)]
    public void AStopAsksTheSameSpeedOfACarALittleQuickAsOfOneALittleSlow(float stopInM)
    {
        var context = DriveContext.Clear with { AuthorityM = stopInM };
        var slow = Target(Straight, 10f, 0.3f, context, out var hold);
        var quick = Target(Straight, 10f, 1.5f, context, out _);

        Assert.Equal(DrivingHold.Claimed, hold);
        Assert.Equal(slow, quick);
    }

    /// <summary>
    /// <b>The end of its own plan is kept as a hold found standing there would cut it</b> (S-2, TER-4c.1): a stand-off
    /// short, and a lead later than the end itself is read — so a car whose plan reaches that and its stop holds the
    /// speed it is doing, and one whose plan reaches less slows.
    /// </summary>
    [Theory]
    [InlineData(8f)]
    [InlineData(15f)]
    [InlineData(25f)]
    public void AHoldFoundAtTheEndOfItsPlanIsOneTheCarCanStillStopFor(float alongMps)
    {
        var leadM = alongMps * CarFollower.LeadS(Car);
        var neededM = Figures.Driving.StandOffM + (2f * leadM) + (alongMps * alongMps / (2f * Car.PlannedBrakingMps2));

        var held = Target(Straight, 10f, alongMps, DriveContext.Clear with { HorizonM = neededM }, out var hold);
        var shorter = Target(Straight, 10f, alongMps, DriveContext.Clear with { HorizonM = neededM - leadM }, out _);

        Assert.Equal(alongMps, held, 1e-2f);
        Assert.Equal(DrivingHold.Reach, hold);
        Assert.True(shorter < alongMps);
    }

    /// <summary>Below the stop speed and with nowhere to go, a car holds itself with the handbrake rather than the pedal.</summary>
    [Fact]
    public void AStoppedCarHoldsItselfOnTheHandbrake()
    {
        var command = CarFollower.Pedals(Figures, Car, 0f, 0f, 0f,Figures.TickSeconds);

        Assert.True(command.Handbrake);
        Assert.Equal(0f, command.ThrottleMps2);
        Assert.Equal(0f, command.BrakeMps2);
    }

    /// <summary>One pedal or the other, never both, and never more than the pedal itself can ask for.</summary>
    [Theory]
    [InlineData(0f, 30f, 30f)]
    [InlineData(30f, 0f, -30f)]
    public void OnePedalOrTheOtherAndNeverBoth(float alongMps, float targetMps, float lastMps2)
    {
        var command = CarFollower.Pedals(Figures, Car, 0.1f, targetMps, alongMps,Figures.TickSeconds, lastMps2);

        Assert.True(command.ThrottleMps2 == 0f || command.BrakeMps2 == 0f);
        Assert.InRange(command.ThrottleMps2, 0f, Figures.CarAccelerationMps2);
        Assert.InRange(command.BrakeMps2, 0f, Figures.CarBrakingMps2);
        Assert.Equal(0.1f, command.SteerRad);
    }

    /// <summary>
    /// <b>A car braking in a bend keeps the bend</b> (CAR-47): at its share of grip round a corner and asked to stop
    /// with the pedal already down, what it brakes at and what the corner takes stay inside what the tyres hold.
    /// </summary>
    [Fact]
    public void ACarBrakingInABendKeepsTheBend()
    {
        const float radiusM = 40f;
        var alongMps = MathF.Sqrt(Car.PlannedCorneringMps2 * radiusM);
        var steerRad = MathF.Atan(Car.WheelbaseM / radiusM);

        var command = CarFollower.Pedals(
            Figures, Car, steerRad, 0f, alongMps,Figures.TickSeconds, -Figures.CarBrakingMps2);

        var acrossMps2 = alongMps * alongMps / radiusM;
        var gripMps2 = Car.GripMps2;
        Assert.True(command.BrakeMps2 > 0f);
        Assert.True(
            (command.BrakeMps2 * command.BrakeMps2) + (acrossMps2 * acrossMps2) < gripMps2 * gripMps2,
            $"braking at {command.BrakeMps2:0.00} m/s² round a {acrossMps2:0.00} m/s² corner is past the {gripMps2:0.00} the tyres hold");
    }

    /// <summary>
    /// <b>And on a straight it brakes at what every stop is planned against</b> (CAR-47, S-2), however far down the
    /// pedal is — the pedal stands well clear of the tyres, and the stop it plans is the one it makes.
    /// </summary>
    [Fact]
    public void OnAStraightItBrakesAtWhatEveryStopIsPlannedAgainst()
    {
        var command = CarFollower.Pedals(Figures, Car, 0f, 0f, 20f,Figures.TickSeconds, -Figures.CarBrakingMps2);

        Assert.Equal(Car.PlannedBrakingMps2, command.BrakeMps2, 1e-4f);
    }

    /// <summary>
    /// <b>The pedal travels rather than snapping.</b> What closes the speed error in one tick is sixty
    /// times that error, so an error of a fifth of a metre a second would otherwise saturate it and a car
    /// merely holding a speed would flick between the two stops several times a second.
    /// </summary>
    [Fact]
    public void ThePedalMovesAtItsOwnRateAndNotInOneTick()
    {
        var travelMps2 = Car.PedalRateMps3 * Figures.TickSeconds;

        // Flat out, then asked for a standstill: what arrives this tick is one tick of pedal travel.
        var first = CarFollower.Pedals(
            Figures, Car, 0f, 0f, 30f,Figures.TickSeconds, Figures.CarAccelerationMps2);

        Assert.Equal(Figures.CarAccelerationMps2 - travelMps2, CarFollower.PedalMps2(first), 1e-3f);

        // And the tick after that, from where it got to — so the whole travel is a pedal-travel long.
        var second = CarFollower.Pedals(
            Figures, Car, 0f, 0f, 30f,Figures.TickSeconds, CarFollower.PedalMps2(first));

        Assert.Equal(Figures.CarAccelerationMps2 - (2f * travelMps2), CarFollower.PedalMps2(second), 1e-3f);
    }

    /// <summary>
    /// <b>The pedal closes on the speed the car will be doing once the pedal it left has acted</b> (S-2): a pedal that
    /// will bring it up to its target over the step it cannot see yet comes off rather than asking again.
    /// </summary>
    [Fact]
    public void ThePedalClosesOnTheSpeedThePedalItLeftWillBring()
    {
        const float alongMps = 10f;
        const float leftMps2 = 1f;
        var targetMps = alongMps + (leftMps2 * Figures.TickSeconds * SimFigures.WorldStepsAhead);

        var command = CarFollower.Pedals(Figures, Car, 0f, targetMps, alongMps, Figures.TickSeconds, leftMps2);

        Assert.Equal(0f, CarFollower.PedalMps2(command), 1e-3f);
    }

    /// <summary>
    /// Every distance is measured a reaction lead ahead of where the car actually is, so a car doing
    /// twenty metres a second plans from where it will be a decision from now.
    /// </summary>
    [Fact]
    public void EveryDistanceIsMeasuredAReactionLeadAhead()
    {
        var stopAtM = 40f;
        var withoutLead = CarFollower.ApproachMps(0f, stopAtM, Figures.CarBrakingMps2 * Figures.Driving.GripMargin);
        var asked = Target(Straight, 0f, 20f, DriveContext.Clear with { PlaceStopM = stopAtM }, out _);

        Assert.True(asked < withoutLead);
    }

    /// <summary>The rear axle is half a wheelbase behind the middle of the body, which is the point every line is drawn for.</summary>
    [Fact]
    public void TheLineIsTheRearAxlesAndNotTheBodys()
    {
        var pose = At(10f, 0f);
        var rearAxleM = CarFollower.RearAxleM(Car, pose.PositionM, pose.HeadingRad);

        Assert.Equal(10f, rearAxleM.X, 1e-4f);
        Assert.Equal(0f, CarFollower.OffLineM(Straight, rearAxleM, 10f), 1e-3f);
    }
}
