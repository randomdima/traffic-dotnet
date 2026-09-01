using System.Numerics;
using TrafficSimulation.App.Camera;
using TrafficSimulation.App.PlayerControl;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.PlayerControl;

/// <summary>
/// CTL-9: what two fingers a frame apart come to. <b>All three movements are read every frame</b> —
/// the pair's middle pans, the distance zooms, the angle turns — so what is asserted here is that one
/// of them happening does not take the others with it.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class TouchGestureTests
{
    static readonly SimConfig Config = SimConfig.Shipped();
    static readonly Vector2 UiPx = new(1600f, 900f);
    static readonly Vector2 TownM = new(480f, 320f);

    static Camera2D Camera() => new(Config, TownM, UiPx);

    /// <summary>The first frame two fingers are down is what the next one is a difference from.</summary>
    static TouchGesture Holding(Camera2D camera, Vector2 onePx, Vector2 twoPx)
    {
        var gesture = new TouchGesture();
        Assert.True(gesture.Read([onePx, twoPx], camera, UiPx, Config));
        return gesture;
    }

    /// <summary>
    /// One finger is the left button and not a gesture — which is what lets a single touch drag, tap
    /// and pick through the very code a mouse does (CTL-9).
    /// </summary>
    [Fact]
    public void OneFingerIsNoGestureAtAll()
    {
        var camera = Camera();
        var before = (camera.CentreM, camera.PixelsPerMetre, camera.TurnRad);

        Assert.False(new TouchGesture().Read([new Vector2(400f, 400f)], camera, UiPx, Config));
        Assert.False(new TouchGesture().Read([], camera, UiPx, Config));

        Assert.Equal(before, (camera.CentreM, camera.PixelsPerMetre, camera.TurnRad));
    }

    /// <summary>
    /// <b>The frame two fingers land is a reading and not a movement.</b> There is nothing to be a
    /// difference from yet, and a camera that treated the first pair as one would jump by wherever the
    /// hand happened to put them.
    /// </summary>
    [Fact]
    public void TheFrameTheFingersLandMovesNothing()
    {
        var camera = Camera();
        var before = (camera.CentreM, camera.PixelsPerMetre, camera.TurnRad);

        Holding(camera, new Vector2(600f, 400f), new Vector2(1_000f, 400f));

        Assert.Equal(before, (camera.CentreM, camera.PixelsPerMetre, camera.TurnRad));
    }

    /// <summary>Two fingers spreading zoom about the point between them, and leave the town north up.</summary>
    [Fact]
    public void SpreadingZoomsAboutThePointBetweenTheFingers()
    {
        var camera = Camera();
        var onePx = new Vector2(600f, 400f);
        var twoPx = new Vector2(1_000f, 400f);
        var gesture = Holding(camera, onePx, twoPx);

        var middlePx = (onePx + twoPx) * 0.5f;
        var under = camera.WorldAt(middlePx, UiPx);
        var before = camera.PixelsPerMetre;

        gesture.Read([onePx - new Vector2(100f, 0f), twoPx + new Vector2(100f, 0f)], camera, UiPx, Config);

        Assert.Equal(before * 1.5f, camera.PixelsPerMetre, tolerance: 1e-2f);
        Assert.Equal(under.X, camera.WorldAt(middlePx, UiPx).X, tolerance: 1e-2f);
        Assert.Equal(under.Y, camera.WorldAt(middlePx, UiPx).Y, tolerance: 1e-2f);
        Assert.False(camera.IsTurned, "a pinch straight along the axis turned the town");
    }

    /// <summary>Two fingers travelling together pan and do nothing else: the distance between them did not change.</summary>
    [Fact]
    public void TwoFingersTravellingTogetherPan()
    {
        var camera = Camera();
        var onePx = new Vector2(600f, 400f);
        var twoPx = new Vector2(1_000f, 400f);
        var gesture = Holding(camera, onePx, twoPx);

        var before = camera.CentreM;
        var alongPx = new Vector2(60f, -25f);
        gesture.Read([onePx + alongPx, twoPx + alongPx], camera, UiPx, Config);

        var wantedM = before - (alongPx / camera.PixelsPerMetre);
        Assert.Equal(wantedM.X, camera.CentreM.X, tolerance: 1e-3f);
        Assert.Equal(wantedM.Y, camera.CentreM.Y, tolerance: 1e-3f);
        Assert.False(camera.IsTurned);
    }

    /// <summary>
    /// <b>A twist inside the dead zone is not a turn.</b> No two fingers spread perfectly square, so a
    /// pinch whose angle was believed leaves the town a degree or two off north every time it is zoomed.
    /// </summary>
    [Fact]
    public void ATwistInsideTheDeadZoneLeavesTheTownLevel()
    {
        var camera = Camera();
        var gesture = Holding(camera, new Vector2(600f, 400f), new Vector2(1_000f, 400f));

        gesture.Read(Spread(float.DegreesToRadians(Config.View.CameraTwistDeadZoneDeg * 0.5f)), camera, UiPx, Config);

        Assert.False(camera.IsTurned);
    }

    /// <summary>
    /// Past it the town follows the fingers, and <b>what was spent crossing the dead zone is never paid
    /// back</b>: the turn carries on from where the hand has got to rather than jumping to it.
    /// </summary>
    [Fact]
    public void PastTheDeadZoneTheTownTurnsWithTheFingers()
    {
        var camera = Camera();
        var gesture = Holding(camera, new Vector2(600f, 400f), new Vector2(1_000f, 400f));

        const float stepRad = 0.05f;
        var crossedRad = 0f;
        for (var step = 1; step <= 6; step++)
        {
            crossedRad = step * stepRad;
            gesture.Read(Spread(crossedRad), camera, UiPx, Config);
        }

        Assert.True(camera.IsTurned, $"{float.RadiansToDegrees(crossedRad):F0} degrees of twist turned nothing");
        Assert.True(
            camera.TurnRad < crossedRad,
            "the town turned by the whole twist, the dead zone it spent crossing included");

        // And from there every frame's twist is the town's, one for one.
        var turnedRad = camera.TurnRad;
        gesture.Read(Spread(crossedRad + stepRad), camera, UiPx, Config);
        Assert.Equal(turnedRad + stepRad, camera.TurnRad, tolerance: 1e-3f);
    }

    /// <summary>The same pair, held the same distance apart, turned by an angle about the same middle.</summary>
    static Vector2[] Spread(float turnRad)
    {
        var middlePx = new Vector2(800f, 400f);
        var armPx = new Vector2(MathF.Cos(turnRad), MathF.Sin(turnRad)) * 200f;
        return [middlePx - armPx, middlePx + armPx];
    }
}
