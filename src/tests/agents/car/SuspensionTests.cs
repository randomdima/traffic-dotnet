using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Car;

/// <summary>A body on a damped spring at every wheel (CAR-57), asked against the statics and the dynamics it has to come to.</summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P3)]
public class SuspensionTests
{
    static readonly SimConfig Figures = SimConfig.Shipped();

    static readonly CarBuild Car = CarBuild.Nominal(Figures, Figures.Car.DrivenFrontShare);

    static float[] Loads(in CarBuild car, in SprungBody body, out float acrossMps2)
    {
        var loads = new float[car.WheelCount];
        acrossMps2 = Suspension.Loads(car, body, loads);
        return loads;
    }

    static SprungBody Run(in CarBuild car, SprungBody body, Vector2 pushN, float seconds)
    {
        for (var tick = 0; tick < (int)(seconds * Figures.Sim.TickRateHz); tick++)
        {
            body = Suspension.Step(car, body, pushN, Figures.TickSeconds);
        }

        return body;
    }

    /// <summary>
    /// <b>At rest each axle stands on what the balance puts on it</b>, split evenly across it: a car carrying 0.4 of
    /// itself on the front stands 0.2 on each front wheel and 0.3 on each rear one.
    /// </summary>
    [Fact]
    public void AtRestEachAxleStandsOnWhatTheBalancePutsOnIt()
    {
        var variant = CarCatalog.Shared.Variants[CarCatalog.Shared.Plain] with { FrontWeightShare = 0.4f };
        var loads = Loads(CarBuild.Of(Figures, variant), default, out _);

        Assert.Equal(0.2f, loads[0], 1e-5f);
        Assert.Equal(0.2f, loads[1], 1e-5f);
        Assert.Equal(0.3f, loads[2], 1e-5f);
        Assert.Equal(0.3f, loads[3], 1e-5f);
    }

    /// <summary><b>A body at rest with nothing pushing it stays exactly at rest</b>: no step moves it by a rounding.</summary>
    [Fact]
    public void ABodyAtRestStaysExactlyAtRest() => Assert.Equal(default, Run(Car, default, Vector2.Zero, 1f));

    /// <summary>
    /// <b>Under a steady push the body comes to rest where its springs balance the push's moment</b>: a pitch of
    /// <c>−F·h/(k·L²)</c> and a roll of <c>−F·h/(k·t²)</c> — nose down under the brakes, the outside down in a corner,
    /// both at once braking into one.
    /// </summary>
    [Theory]
    [InlineData(-8f, 0f)]
    [InlineData(4f, 0f)]
    [InlineData(0f, 7f)]
    [InlineData(-5f, -6f)]
    public void UnderASteadyPushTheBodyTiltsAsFarAsItsSpringsBalanceIt(float alongMps2, float acrossMps2)
    {
        var pushN = new Vector2(alongMps2, acrossMps2) * Car.MassKg;
        var body = Run(Car, default, pushN, 10f);

        var trackM = 2f * Car.HalfTrackM;
        Assert.Equal(-pushN.X * Car.CgHeightM / (Car.WheelRateNPerM * Car.WheelbaseM * Car.WheelbaseM), body.PitchRad, 1e-5f);
        Assert.Equal(-pushN.Y * Car.CgHeightM / (Car.WheelRateNPerM * trackM * trackM), body.RollRad, 1e-5f);
    }

    /// <summary>
    /// <b>The acceleration across the car its springs carry is the corner itself</b> once the body has settled into it —
    /// what the driven axle has already paid for (<c>TyrePass.DriveCeilingMps2</c>).
    /// </summary>
    [Fact]
    public void AtASteadyCornerTheSpringsCarryTheCorner()
    {
        var body = Run(Car, default, new Vector2(0f, 6f * Car.MassKg), 10f);

        Loads(Car, body, out var acrossMps2);

        Assert.Equal(6f, acrossMps2, 1e-3f);
    }

    /// <summary>
    /// <b>However far the body is thrown, the wheels are the whole car and none carries less than nothing</b>: a wheel
    /// whose spring would pull the road up is off it, and carries nought.
    /// </summary>
    [Fact]
    public void AWheelThrownOffTheGroundCarriesNothingAndTheRestAreTheWholeCar()
    {
        var loads = Loads(Car, new SprungBody(0f, 0.15f, -0.2f, 0f, 0f, 0f), out _);

        Assert.All(loads, load => Assert.True(load >= 0f));
        Assert.Contains(0f, loads);
        Assert.Equal(1f, loads.Sum(), 1e-5f);
    }

    /// <summary>
    /// <b>A dive let go of rebounds past level before it settles</b>: the body is damped and not dragged, so it swings
    /// back through where it stands at rest — and is all but there again a few seconds on.
    /// </summary>
    [Fact]
    public void ADiveLetGoOfReboundsPastLevelAndSettles()
    {
        var dived = Run(Car, default, new Vector2(-8f * Car.MassKg, 0f), 10f);

        var body = dived;
        var leastRad = dived.PitchRad;
        for (var tick = 0; tick < Figures.Sim.TickRateHz; tick++)
        {
            body = Suspension.Step(Car, body, Vector2.Zero, Figures.TickSeconds);
            leastRad = MathF.Min(leastRad, body.PitchRad);
        }

        Assert.True(leastRad < 0f, $"the nose came back to {leastRad:E2} rad and no further");
        Assert.InRange(MathF.Abs(Run(Car, body, Vector2.Zero, 3f).PitchRad), 0f, dived.PitchRad * 1e-2f);
    }
}
