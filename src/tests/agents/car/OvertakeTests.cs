using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;
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

    /// <summary>A step drawn for a speed: the shortest the car can drive at it, and the most it may be driven at.</summary>
    static (float StepM, float DriveMps) AStep(float speedMps)
    {
        Assert.True(CarFollower.ShapeAPass(Car, speedMps, AsideM, CorneringMps2, 0f, out var stepM, out var driveMps));
        return (stepM, driveMps);
    }

    /// <summary>A pass stepping out at one speed and back at another, with a straight between.</summary>
    static Overtake APass(float outMps, float backMps)
    {
        var (outStepM, outDriveMps) = AStep(outMps);
        var (backStepM, backDriveMps) = AStep(backMps);
        return new Overtake(
            0, 10f, outStepM, 10f + outStepM + 5f, backStepM, AsideM, outDriveMps, backDriveMps, 15f, Begun: true);
    }

    static Overtake APass(float speedMps) => APass(speedMps, speedMps);

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
        var (bentShare, changedShare) = SpentAt(pass, pass.OutMps);

        Assert.True(bentShare <= 1.01f, $"at {speedMps} m/s the step bends {bentShare:P0} of what it may");
        Assert.True(changedShare <= 1.02f, $"at {speedMps} m/s the step's bend changes {changedShare:P0} of the rack");
        Assert.True(
            bentShare >= 0.9f || changedShare >= 0.9f,
            $"at {speedMps} m/s the step spends {bentShare:P0} of its bend and {changedShare:P0} of the rack");
    }

    /// <summary>
    /// <b>And a step of any length is driven no faster than its bend and the rack allow</b>
    /// (<see cref="CarFollower.StepMps"/>) — a step out stretched to reach the lane beside no sooner than it must is
    /// driven at what its own length allows, and at that pace one of the two is all but spent.
    /// </summary>
    [Theory]
    [InlineData(16f)]
    [InlineData(25f)]
    [InlineData(60f)]
    public void AStepIsDrivenAtTheMostItsBendAndTheRackAllow(float stepM)
    {
        var driveMps = CarFollower.StepMps(Car, stepM, AsideM, CorneringMps2, 0f);
        var pass = new Overtake(0, 10f, stepM, 10f + stepM, stepM, AsideM, driveMps, driveMps, 15f, Begun: true);
        var (bentShare, changedShare) = SpentAt(pass, driveMps);

        Assert.True(bentShare <= 1.01f, $"a {stepM} m step at {driveMps:F2} m/s bends {bentShare:P0} of what it may");
        Assert.True(changedShare <= 1.02f, $"a {stepM} m step at {driveMps:F2} m/s changes its bend {changedShare:P0} of the rack");
        Assert.True(
            bentShare >= 0.9f || changedShare >= 0.9f,
            $"a {stepM} m step at {driveMps:F2} m/s spends {bentShare:P0} of its bend and {changedShare:P0} of the rack");
    }

    /// <summary>
    /// <b>Drawn for the speed the car is doing</b>, so it is driven through without slowing — and never for less
    /// than a step bent to the lock needs, which is no shorter slower.
    /// </summary>
    [Fact]
    public void APassIsDrawnForTheSpeedTheCarIsDoing()
    {
        var fromRest = AStep(0f);
        var atSpeed = AStep(20f);

        Assert.Equal(20f, atSpeed.DriveMps);
        Assert.True(fromRest.DriveMps > 0f, "a pass from rest was drawn for a car that never moves");
        Assert.Equal(fromRest.StepM, AStep(fromRest.DriveMps * 0.5f).StepM, 3);
    }

    /// <summary>
    /// <b>Nothing outside itself and the whole of the lane beside between its steps</b>: a car on a pass is on its
    /// own lane before it and after it, on the other lane alongside what it passes, and square to the line at
    /// both ends of each step — however much longer the step back is than the step out.
    /// </summary>
    [Fact]
    public void APassIsTheLaneBesideBetweenItsStepsAndNothingOutsideThem()
    {
        var pass = APass(0f, 15f);

        Assert.Equal(0f, pass.AsideAtM(pass.OutM - 1f));
        Assert.Equal(AsideM, pass.AsideAtM(pass.SteppedOutM), 3);
        Assert.Equal(AsideM, pass.AsideAtM(pass.BackM));
        Assert.Equal(0f, pass.AsideAtM(pass.EndsM + 1f));
        Assert.Equal(0f, pass.SlopeAtM(pass.SteppedOutM + 0.01f));
        Assert.Equal(0f, pass.SlopeAtM(pass.EndsM - 0.001f), 3);
    }

    /// <summary>
    /// <b>Each step no faster than it was drawn for, and the straight between them the car's own</b>: a car that
    /// stepped out from rest is let pull away alongside what it passes, up to what it can still come down from to
    /// the pace of its step back by where that begins.
    /// </summary>
    [Fact]
    public void ACarOnAPassPullsAwayAlongsideAndStepsBackAtItsStepsPace()
    {
        var pass = APass(0f, 15f) with { BackM = 200f };
        var alongsideM = pass.SteppedOutM + 1f;

        var stepOutMps = Target(pass, pass.OutM + 1f, pass.OutMps);
        var alongsideMps = Target(pass, alongsideM, pass.OutMps);
        var stepBackMps = Target(pass, pass.BackM + 1f, pass.BackMps * 2f);

        Assert.Equal(pass.OutMps, stepOutMps, 3);
        Assert.True(alongsideMps > pass.OutMps, $"alongside, a car that stepped out at {pass.OutMps:F2} m/s was held to {alongsideMps:F2}");
        Assert.Equal(pass.BackMps, stepBackMps, 3);
    }

    /// <summary>What a car on a straight is let do at a metre of its pass, with nothing else on the road.</summary>
    static float Target(in Overtake pass, float progressM, float alongMps)
    {
        var entryM = new float[Straight.Length];
        CornerLimits.Lay(Straight, entryM, Figures);

        return CarFollower.TargetSpeedMps(
            Figures, Car, Straight, entryM, progressM, Spline.TotalLengthM(Straight), 0f, alongMps,
            CarFollower.LookaheadM(Car, alongMps, Figures.Driving.LookaheadS), DriveContext.Clear, out _, out _, pass);
    }

    /// <summary>
    /// How much of what it may do the step out of a pass spends at a speed: the most it bends against its lock and
    /// what the tyres hold there, and the most its bend changes against what the rack turns while the car rolls.
    /// </summary>
    static (float Bent, float Changed) SpentAt(in Overtake pass, float driveMps)
    {
        var mostBend = MathF.Min(1f / Car.TurningRadiusM, CorneringMps2 / (driveMps * driveMps));
        var mostChange = Car.SteerRateRadPerS / (driveMps * Car.WheelbaseM);

        var bentM = 0f;
        var changedM = 0f;
        var lastBend = BendAt(pass, pass.OutM + SampleM);
        for (var atM = pass.OutM + (2f * SampleM); atM < pass.SteppedOutM - SampleM; atM += SampleM)
        {
            var bend = BendAt(pass, atM);
            bentM = MathF.Max(bentM, MathF.Abs(bend));
            changedM = MathF.Max(changedM, MathF.Abs(bend - lastBend) / SampleM);
            lastBend = bend;
        }

        return (bentM / mostBend, changedM / mostChange);
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
        var atM = pass.OutM + (intoTheStepInSteps * pass.OutStepM);
        pass.PoseAtM(Straight, atM, out var axleM, out var forward);

        var steerRad = CarFollower.SteerThePass(Car, Straight, pass, atM, axleM, forward, Car.LookaheadFloorM);

        Assert.Equal(MathF.Atan(BendAt(pass, atM) * Car.WheelbaseM), steerRad, SteeredWithinRad);
    }

    /// <summary>And a car off it is turned back towards it: pushed out past the lane beside, it steers back in.</summary>
    [Fact]
    public void ACarOffThePassIsTurnedBackTowardsIt()
    {
        var pass = APass(0f);
        var atM = pass.OutM + (1.5f * pass.OutStepM);
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
