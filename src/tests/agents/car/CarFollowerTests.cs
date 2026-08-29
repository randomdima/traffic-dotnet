using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Car;

/// <summary>
/// The driver, asked against a line and a pose and nothing else — no town, no solver, no body. Every
/// question here has an arithmetic answer: the speed a corner of a given radius may be taken at, the
/// steering angle a circle of a given radius needs, the distance a stop needs at a given speed.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class CarFollowerTests
{
    static readonly SimConfig Figures = SimConfig.Shipped();

    /// <summary>The nominal car: what is asked here is the controller's arithmetic, not a variant's.</summary>
    static readonly CarBuild Car = CarBuild.Nominal(Figures, Figures.Car.DrivenFrontShare);

    static readonly ArcSeg[] Straight = [new ArcSeg(Vector2.Zero, 0f, 400f, 0f)];

    static CarPose At(float alongM, float alongMps) =>
        new(new Vector2(alongM + Car.CentreAheadOfAxleM, 0f), 0f, new Vector2(alongMps, 0f), 0f,
            Figures.Car.MassKg, Vector2.Zero);

    static float Target(ReadOnlySpan<ArcSeg> line, float progressM, float alongMps, in DriveContext context, out DrivingHold hold) =>
        CarFollower.TargetSpeedMps(
            Figures, Car, line, progressM, Spline.TotalLengthM(line), 0f, alongMps,
            CarFollower.LookaheadM(Car, alongMps, Figures.Driving.LookaheadS), context, out hold, out _);

    /// <summary>Something the lane index has nothing to say about, which is the one reading the rays still hold a car off.</summary>
    static DriveContext Unnamed(float headwayM, float headwaySpeedMps = 0f) =>
        DriveContext.Clear with { HeadwayM = headwayM, HeadwaySpeedMps = headwaySpeedMps, Ahead = HeadwayKind.Unknown };

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
        var lateralMps2 = Figures.Tyre.GripMps2 * Figures.Driving.GripMargin;
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

        var behindSomething = Target(Straight, 10f, 20f, Unnamed(6f), out var queued);
        var shortOfALine = Target(Straight, 10f, 20f, DriveContext.Clear with { StopAtM = 8f }, out var waiting);
        var nearTheEnd = Target(Straight, 380f, 20f, DriveContext.Clear, out var ending);
        var shortOfGround = Target(
            Straight, 10f, 20f, DriveContext.Clear with { AuthorityM = 8f }, out var granted);

        Assert.True(behindSomething < open);
        Assert.True(shortOfALine < open);
        Assert.True(nearTheEnd < open);
        Assert.True(shortOfGround < open);
        Assert.Equal(DrivingHold.Headway, queued);
        Assert.Equal(DrivingHold.Waiting, waiting);
        Assert.Equal(DrivingHold.LineEnd, ending);
        Assert.Equal(DrivingHold.Reserved, granted);
    }

    /// <summary>
    /// A moving queue is followed at its own speed rather than stopped short of, and the arithmetic that
    /// does it is the grant's alone: the car in front reserved from where <em>it</em> will have stopped, so
    /// the ground behind that is the follower's to use.
    /// </summary>
    [Fact]
    public void AMovingQueueIsFollowedRatherThanStoppedFor()
    {
        var leaderMps = 15f;
        var gapM = Figures.CarTailMarginM + (leaderMps * Figures.Driving.FollowingHeadwayS);

        // The same pair of cars at the same gap, one at rest and one doing fifteen: the moving one's own
        // stopping distance is ground its follower is credited with, and that is the whole of the difference.
        var behindAWreck = Target(Straight, 10f, leaderMps, Granted(gapM, 0f), out _);
        var behindACar = Target(Straight, 10f, leaderMps, Granted(gapM, leaderMps), out _);

        Assert.True(behindACar > behindAWreck);
        Assert.True(behindACar >= leaderMps, "and never asked to go slower than the thing it is following");
    }

    /// <summary>
    /// <b>What a queue settles at is the standstill gap and a following time, and the braking figure is not
    /// in it.</b> A car in front is credited with its own stopping distance out of the same arithmetic the
    /// follower inverts, so the grip cancels and what is left is a distance nobody had to choose twice.
    /// </summary>
    [Theory]
    [InlineData(8f)]
    [InlineData(15f)]
    [InlineData(25f)]
    public void AQueueAtOneSpeedSettlesToTheStandstillGapAndAFollowingTime(float queueMps)
    {
        var settledM = Figures.CarTailMarginM + (queueMps * Figures.Driving.FollowingHeadwayS);

        var targetMps = Target(Straight, 10f, queueMps, Granted(settledM, queueMps), out var hold);

        Assert.Equal(queueMps, targetMps, 1e-2f);
        Assert.Equal(DrivingHold.Reserved, hold);
    }

    /// <summary>Nearer than that it slows, further back it closes up — which is what makes the gap settle at all.</summary>
    [Fact]
    public void ACarNearerThanTheFollowingTimeSlowsAndOneFurtherBackClosesUp()
    {
        var queueMps = 15f;
        var settledM = Figures.CarTailMarginM + (queueMps * Figures.Driving.FollowingHeadwayS);

        var tooNear = Target(Straight, 10f, queueMps, Granted(settledM - 5f, queueMps), out _);
        var tooFar = Target(Straight, 10f, queueMps, Granted(settledM + 5f, queueMps), out _);

        Assert.True(tooNear < queueMps);
        Assert.True(tooFar > queueMps);
    }

    /// <summary>
    /// The grant a follower gets at a given gap, as the town works one out: the ground from the nose to the
    /// tail in front, credited with what that body will have vacated once it is at rest, less the gap kept
    /// behind wherever it stops.
    /// </summary>
    /// <remarks>
    /// <b>Cut by a queue, which is what makes it a following distance</b>: a following time is kept from what
    /// is being followed and from nothing else, so what the grant was cut at is part of the grant.
    /// </remarks>
    static DriveContext Granted(float gapToTheTailM, float aheadMps) =>
        DriveContext.Clear with
        {
            AuthorityM = gapToTheTailM
                         + (aheadMps * aheadMps / (2f * CarFollower.BrakingMps2(Figures, Car, 1f)))
                         - Figures.CarTailMarginM,
            GrantCutBy = HeadwayKind.Queue,
        };

    /// <summary>
    /// <b>The grant and the rays measure different things</b>, so each holds the car off what the other
    /// cannot see: road spoken for beyond an empty corridor, and a shape in one nothing has reserved.
    /// </summary>
    [Fact]
    public void TheGrantAndTheRaysEachHoldTheCarOffWhatTheOtherCannotSee()
    {
        var spokenFor = Target(Straight, 10f, 20f, DriveContext.Clear with { AuthorityM = 6f }, out var granted);
        var inTheWay = Target(Straight, 10f, 20f, Unnamed(6f), out var seen);

        Assert.Equal(DrivingHold.Reserved, granted);
        Assert.Equal(DrivingHold.Headway, seen);
        Assert.True(spokenFor < Figures.Car.MaxSpeedMps);
        Assert.True(inTheWay < Figures.Car.MaxSpeedMps);
    }

    /// <summary>Ground worth less is planned against as ground worth less, in the corner and in the stop alike.</summary>
    [Fact]
    public void SoftGroundIsPlannedForRatherThanDiscovered()
    {
        ReadOnlySpan<ArcSeg> bend = [new ArcSeg(Vector2.Zero, 0f, 40f, 1f / 20f)];

        var onTarmac = Target(bend, 1f, 5f, DriveContext.Clear, out _);
        var onGrass = Target(bend, 1f, 5f, DriveContext.Clear with { GroundCoefficient = 0.8f }, out _);

        Assert.Equal(MathF.Sqrt(0.8f), onGrass / onTarmac, 1e-2f);
    }

    /// <summary>Below the stop speed and with nowhere to go, a car holds itself with the handbrake rather than the pedal.</summary>
    [Fact]
    public void AStoppedCarHoldsItselfOnTheHandbrake()
    {
        var command = CarFollower.Pedals(Figures, Car, 0f, 0f, 0f, Figures.TickSeconds);

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
        var command = CarFollower.Pedals(Figures, Car, 0.1f, targetMps, alongMps, Figures.TickSeconds, lastMps2);

        Assert.True(command.ThrottleMps2 == 0f || command.BrakeMps2 == 0f);
        Assert.InRange(command.ThrottleMps2, 0f, Figures.Car.AccelerationMps2);
        Assert.InRange(command.BrakeMps2, 0f, Figures.Car.BrakingMps2);
        Assert.Equal(0.1f, command.SteerRad);
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
            Figures, Car, 0f, 0f, 30f, Figures.TickSeconds, Figures.Car.AccelerationMps2);

        Assert.Equal(Figures.Car.AccelerationMps2 - travelMps2, CarFollower.PedalMps2(first), 1e-3f);

        // And the tick after that, from where it got to — so the whole travel is a pedal-travel long.
        var second = CarFollower.Pedals(
            Figures, Car, 0f, 0f, 30f, Figures.TickSeconds, CarFollower.PedalMps2(first));

        Assert.Equal(Figures.Car.AccelerationMps2 - (2f * travelMps2), CarFollower.PedalMps2(second), 1e-3f);
    }

    /// <summary>
    /// Every distance is measured a reaction lead ahead of where the car actually is, so a car doing
    /// twenty metres a second plans from where it will be a decision from now.
    /// </summary>
    [Fact]
    public void EveryDistanceIsMeasuredAReactionLeadAhead()
    {
        var stopAtM = 40f;
        var withoutLead = CarFollower.ApproachMps(0f, stopAtM, Figures.Car.BrakingMps2 * Figures.Driving.GripMargin);
        var asked = Target(Straight, 0f, 20f, DriveContext.Clear with { StopAtM = stopAtM }, out _);

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
