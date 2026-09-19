using TrafficSimulation.App.Drive;
using Xunit;

namespace TrafficSimulation.Tests.Drive;

/// <summary>
/// DRV-2's reader: that a step is the line it was written as, that a comment is not one, and that a pedal
/// figure a key could not have produced is refused rather than quietly made to fit (DRV-1).
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P6)]
public class DriveScriptTests
{
    [Fact]
    public void ACommentAndABlankLineAreNotSteps()
    {
        var steps = DriveScript.Read("# what is near the kerb\n\nwait 1   # and then nothing\n");

        Assert.Single(steps);
        Assert.Equal(DriveVerb.Wait, steps[0].Verb);
        Assert.Equal("wait 1", steps[0].Said);
        Assert.Equal(3, steps[0].Line);
    }

    [Fact]
    public void ADriveStepHoldsThePedalsItNamesForTheSecondsItNames()
    {
        var step = DriveScript.Read("drive 1.5 throttle=1 steer=-0.4 handbrake=on")[0];

        Assert.Equal(1.5f, step.Seconds);
        Assert.Equal(1f, step.Hand.Throttle);
        Assert.Equal(-0.4f, step.Hand.Steer);
        Assert.True(step.Hand.Handbrake);
    }

    /// <summary>The brake is the throttle pushed backwards, which is the one pedal the keys have (DRV-1).</summary>
    [Fact]
    public void TheBrakeIsTheThrottleBackwards()
    {
        var step = DriveScript.Read("drive 1 brake=0.5")[0];

        Assert.Equal(-0.5f, step.Hand.Throttle);
    }

    /// <summary>A hand is held even with nothing pressed, because letting the keys go coasts (CTL-5b).</summary>
    [Fact]
    public void CoastingIsAHandThatIsHeldAndPressesNothing()
    {
        var step = DriveScript.Read("coast 2")[0];

        Assert.True(step.Hand.Held);
        Assert.Equal(0f, step.Hand.Throttle);
        Assert.Equal(0f, step.Hand.Steer);
        Assert.False(step.Hand.Handbrake);
    }

    [Fact]
    public void APedalPastItsTravelIsRefusedRatherThanClamped() =>
        Assert.Throws<ArgumentException>(() => DriveScript.Read("drive 1 throttle=2"));

    [Fact]
    public void TheThrottleAndTheBrakeCannotBothBeHeld() =>
        Assert.Throws<ArgumentException>(() => DriveScript.Read("drive 1 throttle=1 brake=1"));

    [Fact]
    public void AWheelPastItsTravelIsRefused() =>
        Assert.Throws<ArgumentException>(() => DriveScript.Read("drive 1 steer=-1.5"));

    [Fact]
    public void AVerbNobodyOffersIsAnErrorAndNeverADroppedStep() =>
        Assert.Throws<ArgumentException>(() => DriveScript.Read("teleport 12 40"));

    [Fact]
    public void AFrameIsNamedAndNeverAPath() =>
        Assert.Throws<ArgumentException>(() => DriveScript.Read("shot ../outside/the/frames"));
}
