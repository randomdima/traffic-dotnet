using System.Numerics;
using TrafficSimulation.App.Hud;
using TrafficSimulation.App.PlayerControl;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.PlayerControl;

/// <summary>
/// CTL-1b: where a click stops being a click. <b>A drag and a click begin identically</b>, so what
/// tells them apart is how far the pointer travelled before the button came up — and a threshold that
/// let the hand's own tremor through would turn every click into a box round one unit.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class GestureTests
{
    static readonly float ThresholdPx = SimConfig.Shipped().View.SelectionDragPx;

    [Fact]
    public void APointerThatBarelyMovedIsStillAClick()
    {
        var atPx = new Vector2(400f, 300f);

        Assert.False(PlayerHands.IsDrag(atPx, atPx, ThresholdPx));
        Assert.False(PlayerHands.IsDrag(atPx, atPx + new Vector2(ThresholdPx * 0.5f, 0f), ThresholdPx));
        Assert.True(PlayerHands.IsDrag(atPx, atPx + new Vector2(ThresholdPx + 1f, 0f), ThresholdPx));

        // Diagonally as well as square: the test is a distance and not a pair of them, so a gesture that
        // ran a threshold's worth each way is a box.
        Assert.True(PlayerHands.IsDrag(atPx, atPx + new Vector2(ThresholdPx, ThresholdPx), ThresholdPx));
    }

    /// <summary>The box is between the two corners whichever way round they were drawn — up-left is a box too.</summary>
    [Fact]
    public void TheBoxIsBetweenItsCornersWhicheverWayItWasDrawn()
    {
        var fromPx = new Vector2(500f, 400f);
        var toPx = new Vector2(300f, 250f);

        var box = Marquee.Between(fromPx, toPx);

        Assert.Equal(toPx, box.AtPx);
        Assert.Equal(new Vector2(200f, 150f), box.SizePx);
        Assert.Equal(box, Marquee.Between(toPx, fromPx));
    }
}
