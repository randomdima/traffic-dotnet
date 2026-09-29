using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Car;

/// <summary>
/// <b>A pass as the line a car is aimed along</b> (CAR-46): its own lane moved across onto the lane beside and
/// back, each step the shortest the car can drive at the speed it is drawn for, and the wheel that drives it.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P2)]
public class OvertakeTests
{
    static readonly SimConfig Figures = SimConfig.Shipped();

    static readonly CarBuild Car = CarBuild.Nominal(Figures, Figures.Car.DrivenFrontShare);

    static readonly float CorneringMps2 = CarFollower.CorneringMps2(Figures, Car, 1f);

    /// <summary>The nominal lane's width, which is how far across a two-way street's other lane stands.</summary>
    static float AsideM => -Figures.LaneWidthM;

    /// <summary>A straight line along +x, long enough for any pass these tests draw.</summary>
    static readonly ArcSeg[] Straight = [new(Vector2.Zero, 0f, 400f, 0f)];

    /// <summary>How finely the drawn line is read: fine against the shortest step, coarse against a float's grain.</summary>
    const float SampleM = 0.2f;

    /// <summary>How near the wheel is to the bend read off the drawn line: what reading it through samples costs, and no more.</summary>
    const float SteeredWithinRad = 0.005f;

    static Overtake APass(float speedMps)
    {
        Assert.True(CarFollower.ShapeAPass(Car, speedMps, AsideM, CorneringMps2, 0f, out var stepM, out var driveMps));
        return new Overtake(0, 10f, 10f + stepM + 5f, AsideM, stepM, driveMps, 15f, Begun: true);
    }

    /// <summary>
    /// <b>Each step is the shortest the car can drive at the speed it is drawn for</b>: bent no tighter than its lock
    /// or than its tyres hold at that speed, its bend changing no faster than the rack turns while the car rolls —
    /// and one of the two all but spent, since a longer step would be one the car could have made shorter. All but:
    /// the bend is bounded off the step's rise, which runs a tenth over the arc's where a step runs steeply.
    /// </summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(5f)]
    [InlineData(10f)]
    [InlineData(20f)]
    public void EveryStepIsTheShortestItsLimitsAllow(float speedMps)
    {
        var pass = APass(speedMps);
        var mostBend = MathF.Min(1f / Car.TurningRadiusM, CorneringMps2 / (pass.DriveMps * pass.DriveMps));
        var mostChange = Car.SteerRateRadPerS / (pass.DriveMps * Car.WheelbaseM);

        var bentM = 0f;
        var changedM = 0f;
        var lastBend = BendAt(pass, pass.OutM + SampleM);
        for (var atM = pass.OutM + (2f * SampleM); atM < pass.OutM + pass.StepM - SampleM; atM += SampleM)
        {
            var bend = BendAt(pass, atM);
            bentM = MathF.Max(bentM, MathF.Abs(bend));
            changedM = MathF.Max(changedM, MathF.Abs(bend - lastBend) / SampleM);
            lastBend = bend;
        }

        Assert.True(bentM <= mostBend * 1.01f, $"at {speedMps} m/s the step bends {bentM:F4}/m against {mostBend:F4}/m");
        Assert.True(
            changedM <= mostChange * 1.02f,
            $"at {speedMps} m/s the step's bend changes {changedM:F4}/m² against the rack's {mostChange:F4}/m²");
        Assert.True(
            bentM >= mostBend * 0.9f || changedM >= mostChange * 0.9f,
            $"at {speedMps} m/s the step spends {bentM / mostBend:P0} of its bend and {changedM / mostChange:P0} of the rack");
    }

    /// <summary>
    /// <b>Drawn for the speed the car is doing</b>, so it is driven through without slowing — and never for less
    /// than a step bent to the lock needs, which is no shorter slower.
    /// </summary>
    [Fact]
    public void APassIsDrawnForTheSpeedTheCarIsDoing()
    {
        var fromRest = APass(0f);
        var atSpeed = APass(20f);

        Assert.Equal(20f, atSpeed.DriveMps);
        Assert.True(fromRest.DriveMps > 0f, "a pass from rest was drawn for a car that never moves");
        Assert.Equal(fromRest.StepM, APass(fromRest.DriveMps * 0.5f).StepM, 3);
    }

    /// <summary>
    /// <b>Nothing outside itself and the whole of the lane beside between its steps</b>: a car on a pass is on its
    /// own lane before it and after it, on the other lane alongside what it passes, and square to the line at
    /// both ends of each step.
    /// </summary>
    [Fact]
    public void APassIsTheLaneBesideBetweenItsStepsAndNothingOutsideThem()
    {
        var pass = APass(10f);

        Assert.Equal(0f, pass.AsideAtM(pass.OutM - 1f));
        Assert.Equal(AsideM, pass.AsideAtM(pass.OutM + pass.StepM), 3);
        Assert.Equal(AsideM, pass.AsideAtM(pass.BackM));
        Assert.Equal(0f, pass.AsideAtM(pass.EndsM + 1f));
        Assert.Equal(0f, pass.SlopeAtM(pass.OutM + pass.StepM + 0.01f));
        Assert.Equal(0f, pass.SlopeAtM(pass.EndsM - 0.001f), 3);
    }

    /// <summary>
    /// <b>A car on the pass is steered at the pass's own bend</b>: on a step its wheel holds the bend the step has
    /// there, alongside it is straight — nothing aimed a lookahead ahead that would cut the step.
    /// </summary>
    [Theory]
    [InlineData(0.25f)]
    [InlineData(0.6f)]
    [InlineData(1.5f)]
    public void ACarOnThePassIsSteeredAtItsOwnBend(float intoTheStepInSteps)
    {
        var pass = APass(0f);
        var atM = pass.OutM + (intoTheStepInSteps * pass.StepM);
        pass.PoseAtM(Straight, atM, out var axleM, out var forward);

        var steerRad = CarFollower.SteerThePass(Car, Straight, pass, atM, axleM, forward, Car.LookaheadFloorM);

        Assert.Equal(MathF.Atan(BendAt(pass, atM) * Car.WheelbaseM), steerRad, SteeredWithinRad);
    }

    /// <summary>And a car off it is turned back towards it: pushed out past the lane beside, it steers back in.</summary>
    [Fact]
    public void ACarOffThePassIsTurnedBackTowardsIt()
    {
        var pass = APass(0f);
        var atM = pass.OutM + (1.5f * pass.StepM);
        pass.PoseAtM(Straight, atM, out var axleM, out var forward);
        var outwardM = new Vector2(0f, MathF.Sign(AsideM) * 0.5f);

        var steerRad = CarFollower.SteerThePass(Car, Straight, pass, atM, axleM + outwardM, forward, Car.LookaheadFloorM);

        // Back towards the line is the way the step back will turn, which is against the step out.
        Assert.True(MathF.Sign(steerRad) == -MathF.Sign(AsideM), $"a car half a metre out steered {steerRad:F3} rad");
    }

    /// <summary>
    /// The curvature of the aimed line at a metre of it, through the places a sample either side, signed as the
    /// line's — worked in doubles, since a sample's sag on the gentlest step is a few hundredths of a millimetre.
    /// </summary>
    static float BendAt(in Overtake pass, float atM)
    {
        double oneY = pass.AsideAtM(atM - SampleM), twoY = pass.AsideAtM(atM), threeY = pass.AsideAtM(atM + SampleM);
        double firstY = twoY - oneY, secondY = threeY - twoY;
        var cross = (SampleM * secondY) - (firstY * SampleM);
        var span = Math.Sqrt((SampleM * SampleM) + (firstY * firstY)) * Math.Sqrt((SampleM * SampleM) + (secondY * secondY))
                   * Math.Sqrt((4.0 * SampleM * SampleM) + ((threeY - oneY) * (threeY - oneY)));
        return (float)(2.0 * cross / span);
    }
}
