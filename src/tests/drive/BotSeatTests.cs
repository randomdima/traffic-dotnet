using System.Numerics;
using TrafficSimulation.App.Drive;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Drive;

/// <summary>
/// DRV-8 and CTL-5d: a driver who is not the one watching. That it holds the car it names without
/// touching the selection, that the player wins a car both are holding, and that a word belonging to the
/// reader is refused rather than carried out on their town.
/// </summary>
/// <remarks>
/// <b>No eye is opened</b>, so nothing here needs a Vulkan driver: these are questions about the hand and
/// not about the picture.
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P6)]
public class BotSeatTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static TownWorld Open() => new(Towns.Of(Towns.Fixture), Config);

    /// <summary>The hand a seat holds after one step, pushed through the seam the way a run pushes it.</summary>
    static BotHands Seated(TownWorld world, int car, string step)
    {
        var hands = new BotHands(world, Config, car, ".tmp/bot", viewM: 45f);
        hands.Take(DriveScript.Read(step)[0]);
        world.HandOnCar(car, hands.Hand);
        return hands;
    }

    /// <summary>
    /// CTL-5d: the named car is driven and <b>the reader's selection is not touched</b> — which is the
    /// whole of what makes two drivers in one town possible.
    /// </summary>
    [Fact]
    public void ASeatDrivesTheCarItNamedAndLeavesTheSelectionAlone()
    {
        using var world = Open();
        var loop = new SimLoop<TownWorld>(world, Config);

        world.Select(new Selection(SelectionKind.Car, 1));
        Seated(world, 0, "drive 1 throttle=1");
        loop.Advance(60);

        Assert.True(world.Cars.Command[0].ThrottleMps2 > 0f, "the seat's pedal should reach its own car");
        Assert.True(world.WheelIsHeldOver(0));
        Assert.Equal(0, world.HandDrivenCar);

        // The reader still has the car they picked out, and nothing is driving it.
        Assert.Equal(new Selection(SelectionKind.Car, 1), world.Lead);
        Assert.False(world.HandsOn);
        Assert.False(world.WheelIsHeldOver(1));
    }

    /// <summary>
    /// CTL-5d: <b>a car they are both holding is the player's.</b> The selection is a reader saying which
    /// car is theirs, so a second hand on it is the one that gives way.
    /// </summary>
    [Fact]
    public void ThePlayerWinsACarBothHandsAreOn()
    {
        using var world = Open();
        var loop = new SimLoop<TownWorld>(world, Config);

        world.Select(new Selection(SelectionKind.Car, 0));
        world.Hands(new HandInput(Held: true, Throttle: 0f, Steer: 0f, Handbrake: true, WalkDirection: Vector2.Zero));
        Seated(world, 0, "drive 1 throttle=1");
        loop.Advance(60);

        Assert.True(world.Cars.Command[0].Handbrake, "the player's own hand should be the one on the car");
        Assert.Equal(0f, world.Cars.Command[0].ThrottleMps2);
    }

    /// <summary>
    /// DRV-8: the seat takes the wheel when it sits down. A car left to drive its own route until the
    /// first pedal arrives is a car the seat inherits at speed — which is how the first run wrecked one.
    /// </summary>
    [Fact]
    public void TheWheelIsTakenBeforeAnyPedalIsPressed()
    {
        using var world = Open();
        var hands = new BotHands(world, Config, 0, ".tmp/bot", viewM: 45f);

        Assert.True(hands.Hand.Held);
        Assert.Equal(0f, hands.Hand.Throttle);

        world.HandOnCar(0, hands.Hand);
        Assert.True(world.WheelIsHeldOver(0));
    }

    /// <summary>
    /// DRV-8: everything that reaches the reader's own interface is refused. A seat that could pick units
    /// out or give orders would be driving the town somebody else is looking at.
    /// </summary>
    [Theory]
    [InlineData("select car 1")]
    [InlineData("order 10 10")]
    [InlineData("release")]
    [InlineData("action")]
    [InlineData("look 10 10")]
    public void AWordThatBelongsToTheReaderIsRefused(string step)
    {
        using var world = Open();
        var hands = new BotHands(world, Config, 0, ".tmp/bot", viewM: 45f);

        var refused = Assert.Throws<ArgumentException>(() => hands.Take(DriveScript.Read(step)[0]));
        Assert.Contains(BotHands.Allowed, refused.Message);
    }

    /// <summary>
    /// DRV-8: <b>the wheel is where the driver left it until a line names it.</b> A step is a burst of
    /// pedals, so one that says nothing about the wheel leaves it wound where it was — and <c>steer=0</c> is
    /// an angle asked for, which is why the two are told apart by the line and not by the figure.
    /// </summary>
    [Fact]
    public void AWheelStandsWhereItWasPutUntilAStepMovesIt()
    {
        using var world = Open();
        var hands = new BotHands(world, Config, 0, ".tmp/bot", viewM: 45f);

        hands.Take(DriveScript.Read("drive 1 throttle=0.5 steer=-0.4")[0]);
        Assert.Equal(-0.4f, hands.Hand.Steer);

        hands.Take(DriveScript.Read("drive 1 throttle=0.3")[0]);
        Assert.Equal(-0.4f, hands.Hand.Steer);
        Assert.Equal(0.3f, hands.Hand.Throttle);

        hands.Take(DriveScript.Read("drive 1 throttle=0.3 steer=0")[0]);
        Assert.Equal(0f, hands.Hand.Steer);
    }

    /// <summary>
    /// DRV-8: <b>a seat that has been told nothing stops the town, and a step lets it go again.</b> That is
    /// the whole of what makes a driver's thinking free: the clock is not running while it looks at the frame
    /// it was given, so what it reads is still true when its answer arrives.
    /// </summary>
    [Fact]
    public void ATownAskedToWaitWaitsUntilTheNextStepArrives()
    {
        using var world = Open();
        var steps = Path.Combine(".tmp", "tests", "bot-waits", "steps.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(steps)!);
        File.WriteAllText(steps, string.Empty);

        using var seat = new BotSeat(
            new BotAsk(
                Car: 0, Steps: steps, FramesDir: Path.GetDirectoryName(steps)!, Out: null, EyeWidthPx: 768,
                EyeHeightPx: 432, ViewM: 45f, Waits: true),
            Config, world, eye: null, hold: null, pace: null);

        seat.Follow(0);
        Assert.True(seat.WaitingToBeTold, "a seat with an empty tape should be standing the town still");

        File.AppendAllText(steps, "drive 1 throttle=0.5\n");
        seat.Follow(0);
        Assert.False(seat.WaitingToBeTold, "a step in hand is a town that should be running");

        // And the second it runs out again — the step it was given has been driven to the end of its while.
        seat.Follow(Config.Sim.TickRateHz);
        Assert.True(seat.WaitingToBeTold);
    }

    /// <summary>
    /// DRV-8: <b>the handbook is this car's own figures</b> and not a briefing somebody typed. The two that
    /// decide everything a driver does with the controls — what a pedal is worth and how far the wheel
    /// turns — are checked against the build the solver drives, which is the only place they come from.
    /// </summary>
    [Fact]
    public void TheHandbookQuotesTheCarTheSolverDrives()
    {
        using var world = Open();
        var hands = new BotHands(world, Config, 0, ".tmp/bot", viewM: 45f);

        ref readonly var build = ref world.Cars.BuildOf(0);
        var handbook = hands.Handbook(eyeWidthPx: 768, eyeHeightPx: 432);

        Assert.Contains($"{build.AccelerationMps2:F1} m/s2", handbook);
        Assert.Contains($"{float.RadiansToDegrees(build.MaxSteerRad):F0} degrees", handbook);
        Assert.Contains($"{build.TurningRadiusM:F1} m of radius at full lock", handbook);

        // And the eye it describes, whose scale is the span across the frame's short side: 432 px of 45 m.
        Assert.Contains("9.6 px to the metre", handbook);
    }
}
