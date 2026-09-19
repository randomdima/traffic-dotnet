using TrafficSimulation.App.Drive;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using Xunit;

namespace TrafficSimulation.Tests.Drive;

/// <summary>
/// A script at the wheel of a town that is standing: that a step is as long as it says, that the hand it
/// holds reaches the car through the seam the keys go through (DRV-1), and that letting the keys go does
/// not hand the car back (CTL-5b).
/// </summary>
/// <remarks>
/// <b>No frame is asked for</b>, so no device is opened: these are questions about the driving and not
/// about the picture.
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P6)]
public class DriveRunTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>A drive of the fixture that asks for no picture, so nothing here needs a Vulkan driver.</summary>
    static DriveAsk Ask => new(
        Map: Towns.Fixture, Script: "the tier's own", FramesDir: ".tmp/drive", Out: null, WidthPx: 640,
        HeightPx: 360, ViewM: 45f, Ui: [], Validate: false);

    static DriveLog Drive(string script) => DriveRun.Drive(DriveScript.Read(script), Ask, Config);

    [Fact]
    public void AStepRunsTheTownForAsLongAsItSays()
    {
        var log = Drive("select nearest 0 0\ndrive 1.5 throttle=1\n");

        Assert.Equal(0L, log.Readings[0].Tick);
        Assert.Equal((long)(1.5f * Config.Sim.TickRateHz), log.Readings[1].Tick);
    }

    /// <summary>
    /// The row only a hand on the wheel writes (OBS-2m): it is there while the script holds the keys and
    /// gone once the wheel is given back, which is the whole of what says the hand reached the car.
    /// </summary>
    [Fact]
    public void TheWheelRowSaysTheScriptIsDrivingAndTheResetTakesItBack()
    {
        var log = Drive("select nearest 0 0\ndrive 1 throttle=1\nrelease\n");

        Assert.Contains(log.Readings[1].Rows, row => row.StartsWith("wheel"));
        Assert.DoesNotContain(log.Readings[2].Rows, row => row.StartsWith("wheel"));
    }

    [Fact]
    public void LettingGoOfTheKeysDoesNotHandTheCarBack() =>
        Assert.Throws<ArgumentException>(() => Drive("select nearest 0 0\ndrive 1 throttle=1\nwait 1\n"));

    [Fact]
    public void AHandWithNothingPickedOutReachesNothing() =>
        Assert.Throws<ArgumentException>(() => Drive("drive 1 throttle=1\n"));
}
