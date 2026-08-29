using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Car;

/// <summary>
/// CAR-3a: <b>neither control arrives in the tick it is asked for</b>. What a driver sets is what it is
/// asking for; what the body carries out is as far towards it as the rack got. The pedal's own travel is
/// already <see cref="CarBuild.PedalRateMps3"/>'s and tested through the follower — what is asked here is
/// the wheel's.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class CarControlsTests
{
    static readonly SimConfig Figures = SimConfig.Shipped();

    static readonly CarBuild Car = CarBuild.Nominal(Figures, Figures.Car.DrivenFrontShare);

    /// <summary>
    /// <b>A press is a wheel being wound on and not a lock being selected.</b> This is the whole of what
    /// makes a car with digital controls drivable: full lock asked for at a standstill arrives over the
    /// rack's own travel, so an angle short of it can be held by holding the key for less time.
    /// </summary>
    [Fact]
    public void TheWheelDoesNotArriveInTheTickItIsAskedFor()
    {
        var atRad = Car.WheelWoundTo(0f, Car.MaxSteerRad, Figures.TickSeconds);

        Assert.True(
            atRad < Car.MaxSteerRad * 0.5f,
            $"a tick of winding took the wheel {atRad:F3} of the {Car.MaxSteerRad:F3} rad asked for");
        Assert.True(atRad > 0f, "the wheel did not move at all");
    }

    /// <summary>
    /// <b>And it takes the rack's own time to go from one lock to the other</b>, which is the figure
    /// <see cref="DrivingFigures.WheelTravelS"/> states and the only thing that decides how a key press
    /// feels.
    /// </summary>
    [Fact]
    public void LockToLockTakesTheRacksOwnTime()
    {
        var atRad = -Car.MaxSteerRad;
        var ticks = 0;

        // What the last tick leaves is a rounding rather than travel, so the lock counts as reached
        // once what is left of it is smaller than the arithmetic that got there.
        while (atRad < Car.MaxSteerRad - Car.MaxSteerRad * 1e-5f && ticks < 10_000)
        {
            atRad = Car.WheelWoundTo(atRad, Car.MaxSteerRad, Figures.TickSeconds);
            ticks++;
        }

        Assert.Equal(Figures.Driving.WheelTravelS, ticks * Figures.TickSeconds, Figures.TickSeconds);
    }

    /// <summary>
    /// <b>An angle already reached is held rather than travelled to</b>, so a driver holding a corner is
    /// not asking the rack for anything and a wheel is never carried past what was asked for.
    /// </summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(0.2f)]
    [InlineData(-0.2f)]
    public void AWheelAlreadyWhereItWasAskedForStaysThere(float atRad)
    {
        Assert.Equal(atRad, Car.WheelWoundTo(atRad, atRad, Figures.TickSeconds), 1e-6f);
    }

    /// <summary>
    /// <b>A shorter lock is reached in the same time</b>: the rate is the car's own lock over the rack's
    /// travel, so a car with less steering to give does not take longer to give all of it.
    /// </summary>
    [Fact]
    public void EveryCarTakesTheSameTimeToUseTheLockItHas()
    {
        var half = Car with { MaxSteerRad = Car.MaxSteerRad * 0.5f, SteerRateRadPerS = Car.SteerRateRadPerS * 0.5f };

        Assert.Equal(
            Car.WheelWoundTo(0f, Car.MaxSteerRad, Figures.TickSeconds) * 0.5f,
            half.WheelWoundTo(0f, half.MaxSteerRad, Figures.TickSeconds),
            1e-6f);
    }

    /// <summary>
    /// CAR-3b: <b>the throttle is bounded by what the patch has left</b>. A car going straight may be asked
    /// for the whole of what the rubber puts down along the roll; one already spending that rubber on a
    /// corner may be asked for the remainder, and one at the lateral limit for nothing at all.
    /// </summary>
    [Fact]
    public void ACornerTakesTheThrottleTheTyresNoLongerHave()
    {
        var acrossMps2 = Car.GripMps2;

        Assert.Equal(Car.LongGripMps2, TyreModel.DriveLeftMps2(Car.LongGripMps2, acrossMps2, 0f), 1e-3f);
        Assert.Equal(0f, TyreModel.DriveLeftMps2(Car.LongGripMps2, acrossMps2, acrossMps2), 1e-3f);

        // The ellipse and not a straight line: half the side grip spent leaves √(3)/2 of the roll and not a
        // half of it, which is the difference between a car that lifts for a bend and one that gives up.
        Assert.Equal(
            Car.LongGripMps2 * MathF.Sqrt(0.75f),
            TyreModel.DriveLeftMps2(Car.LongGripMps2, acrossMps2, acrossMps2 * 0.5f),
            1e-3f);

        // Which way round the corner goes is not a thing the tyres have an opinion about.
        Assert.Equal(
            TyreModel.DriveLeftMps2(Car.LongGripMps2, acrossMps2, acrossMps2 * 0.5f),
            TyreModel.DriveLeftMps2(Car.LongGripMps2, acrossMps2, acrossMps2 * -0.5f),
            1e-6f);

        // And a corner past what the tyres hold — a car being shoved sideways — leaves nothing rather than
        // the root of a negative.
        Assert.Equal(0f, TyreModel.DriveLeftMps2(Car.LongGripMps2, acrossMps2, acrossMps2 * 3f));
    }
}
