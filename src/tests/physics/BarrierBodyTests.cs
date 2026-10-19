using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Physics;
using Xunit;

namespace TrafficSimulation.Tests.Physics;

/// <summary>
/// <b>A level crossing's arm as the solver holds it</b> (RAIL-10): a box nothing it meets moves, which a car meets as it
/// would a wall.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P6)]
public class BarrierBodyTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly Vector2 ArmHalfM = new(3f, Config.Rail.BarrierWidthM * 0.5f);

    /// <summary>A car driven at an arm lying across its way is stopped short of it, and the arm is where it was put.</summary>
    [Fact]
    public void ACarDrivenAtAnArmStopsShortOfItAndTheArmStaysPut()
    {
        var world = new PhysicsWorld(Config);
        var car = world.AddNominalCar(Vector2.Zero, 0f);
        var armAtM = new Vector2(10f, 0f);
        var arm = world.AddBarrier(armAtM, MathF.PI * 0.5f, ArmHalfM);

        world.ApplyCentralImpulse(car, new Vector2(Config.Car.MassKg * 8f, 0f));
        for (var step = 0; step < 180; step++) world.Step(Config.TickSeconds);

        Assert.True(world.PositionOf(car).X + (Config.Car.LengthM * 0.5f) < armAtM.X + 0.1f, $"the car's nose is past the arm, at {world.PositionOf(car).X:F2} m");
        Assert.Equal(armAtM, world.PositionOf(arm));
    }
}
