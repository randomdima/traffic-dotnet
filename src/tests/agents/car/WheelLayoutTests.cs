using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Car;

/// <summary>
/// <b>A body on other than two axles of two</b> (CAR-57): the three-axle layout of <see cref="Layouts.SixWheels"/> asked
/// what the model owes any layout — standing, driving, steering and holding still by what its wheels are, and not by
/// where in a table of four they sit.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P3)]
public class WheelLayoutTests
{
    static readonly SimConfig Figures = SimConfig.Shipped();

    static readonly CarBuild Truck = Layouts.SixWheels(Figures);

    /// <summary>
    /// <b>At rest its wheels hold up the whole of it and turn it about nothing</b>: the shares sum to one, and taken about
    /// its centre of mass they make no moment along the car or across it.
    /// </summary>
    [Fact]
    public void AtRestItsWheelsHoldUpTheWholeOfItAndTurnItAboutNothing()
    {
        var loads = new float[Truck.WheelCount];
        Suspension.Loads(Truck, default, loads);

        var pitch = 0f;
        var roll = 0f;
        for (var wheel = 0; wheel < Truck.WheelCount; wheel++)
        {
            var atM = Suspension.FromCentreOfMassM(Truck, wheel);
            pitch += loads[wheel] * atM.X;
            roll += loads[wheel] * atM.Y;
        }

        Assert.Equal(1f, loads.Sum(), 1e-5f);
        Assert.Equal(0f, pitch, 1e-5f);
        Assert.Equal(0f, roll, 1e-5f);
    }

    /// <summary>
    /// <b>Its drive is placed on its driven axles by what each stands on</b>, which is what driving them is for: the two
    /// rear axles share it as they share the weight between them, and the front axle, undriven, gets none.
    /// </summary>
    [Fact]
    public void ItsDriveIsPlacedOnItsDrivenAxlesByWhatEachStandsOn()
    {
        var loads = new float[Truck.WheelCount];
        Suspension.Loads(Truck, default, loads);
        var (middle, rear) = (loads[2] + loads[3], loads[4] + loads[5]);

        Assert.Equal(0f, Truck.DriveOf(0));
        Assert.Equal(middle / (middle + rear), Truck.DriveOf(1), 1e-5f);
        Assert.Equal(rear / (middle + rear), Truck.DriveOf(2), 1e-5f);
    }

    /// <summary>
    /// <b>A wheel steered behind the axle the car turns about turns the other way</b> to the ones ahead of it: they all
    /// point round one centre, standing on that axle's line, and an unsteered wheel is not turned at all.
    /// </summary>
    [Fact]
    public void AWheelSteeredBehindTheAxleItTurnsAboutTurnsTheOtherWay()
    {
        var angles = new float[Truck.WheelCount];
        TyreModel.Ackermann(Figures, Truck, 0.3f, angles);

        Assert.True(angles[0] > 0f && angles[1] > 0f, "the front pair turned the way the rack did");
        Assert.Equal(0f, angles[2]);
        Assert.Equal(0f, angles[3]);
        Assert.True(angles[4] < 0f && angles[5] < 0f, "the rear pair turned the same way as the front");
    }

    /// <summary>
    /// <b>A car its locks hold is brought to rest whole at every wheel its handbrake holds</b> (CAR-52) — four of them on
    /// two axles here — and nothing is spent at the wheels it does not.
    /// </summary>
    [Fact]
    public void ACarItsLocksHoldIsBroughtToRestAtEveryWheelItsHandbrakeHolds()
    {
        var pose = new CarPose(Vector2.Zero, 0f, new Vector2(9e-4f, -1.7e-4f), 4.1e-3f, Truck.MassKg);
        var inertiaKgM2 = Truck.MassKg * 2f;
        var atM = new Vector2[Truck.WheelCount];
        TyreModel.WheelPointsM(Truck, pose, atM);
        var loads = new float[Truck.WheelCount];
        Suspension.Loads(Truck, pose.Sprung, loads);
        var ground = Enumerable.Repeat(
            new SurfaceUnderWheel(Figures.Terrain.PavedCoefficient, Figures.PavedDragMps2, 0f, Ploughs: false),
            Truck.WheelCount).ToArray();
        var wheels = new WheelImpulse[Truck.WheelCount];
        var spinMps = new float[Truck.WheelCount];

        Assert.True(TyreModel.HoldStill(Truck, pose, inertiaKgM2, loads, atM, ground, Figures.TickSeconds, wheels, spinMps));

        var momentumNs = Vector2.Zero;
        var spinNms = 0f;
        for (var wheel = 0; wheel < Truck.WheelCount; wheel++)
        {
            var armM = wheels[wheel].AtM - pose.PositionM;
            momentumNs += wheels[wheel].ImpulseNs;
            spinNms += (armM.X * wheels[wheel].ImpulseNs.Y) - (armM.Y * wheels[wheel].ImpulseNs.X);
            if (!Truck.WheelAt(wheel).IsHandbraked) Assert.Equal(Vector2.Zero, wheels[wheel].ImpulseNs);
        }

        var wantNs = -pose.VelocityMps * pose.MassKg;
        Assert.Equal(wantNs.X, momentumNs.X, MathF.Abs(wantNs.X) * 1e-4f);
        Assert.Equal(wantNs.Y, momentumNs.Y, MathF.Abs(wantNs.Y) * 1e-4f);
        Assert.Equal(-pose.YawRateRadPerS * inertiaKgM2, spinNms, MathF.Abs(pose.YawRateRadPerS * inertiaKgM2) * 1e-4f);
    }

    /// <summary>
    /// <b>Under the brakes it dips onto its front axle</b>, and its wheels are still the whole of it: the springs of any
    /// layout come to rest under a push at the ground as they do under two axles of two.
    /// </summary>
    [Fact]
    public void UnderTheBrakesItDipsOntoItsFrontAxle()
    {
        var body = default(SprungBody);
        for (var tick = 0; tick < 10 * Figures.Sim.TickRateHz; tick++)
        {
            body = Suspension.Step(Truck, body, new Vector2(-7f * Truck.MassKg, 0f), Figures.TickSeconds);
        }

        var standing = new float[Truck.WheelCount];
        var braking = new float[Truck.WheelCount];
        Suspension.Loads(Truck, default, standing);
        Suspension.Loads(Truck, body, braking);

        Assert.True(body.PitchRad > 0f, $"the nose came up {body.PitchRad} rad under the brakes");
        Assert.True(braking[0] + braking[1] > standing[0] + standing[1]);
        Assert.Equal(1f, braking.Sum(), 1e-5f);
    }
}
