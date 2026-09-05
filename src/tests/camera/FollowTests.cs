using System.Numerics;
using TrafficSimulation.App.Camera;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.Camera;

/// <summary>
/// OBS-1a: the camera stands on the one unit picked out, leads it by its own speed, and lets go of it
/// the moment the reader moves the camera themselves.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class FollowTests
{
    static readonly Vector2 UiPx = new(1600f, 900f);
    static readonly Vector2 TownM = new(480f, 320f);
    static readonly Vector2 UnitM = new(120f, 80f);

    /// <summary>A frame at the rate the window is drawn at, which is what the eases are measured over.</summary>
    const float FrameS = 1f / 60f;

    [Fact]
    public void AUnitAtRestIsStoodExactlyOn()
    {
        var (camera, follow) = Watching();

        follow.Step(camera, UiPx, UnitM, Vector2.Zero, FrameS);

        Assert.Equal(UnitM, camera.CentreM);
    }

    /// <summary>
    /// The lead is what makes a followed car readable: the ground it is about to cover is on screen,
    /// which is the half of the picture a camera centred on the car itself spends on the road behind.
    /// </summary>
    [Fact]
    public void AMovingUnitIsLedAlongTheWayItIsGoing()
    {
        var (camera, follow) = Watching();
        var velocityMps = new Vector2(8f, -6f);

        follow.Step(camera, UiPx, UnitM, velocityMps, FrameS);

        var leadM = camera.CentreM - UnitM;
        Assert.True(leadM.Length() > 0f);
        Assert.Equal(0f, Cross(leadM, velocityMps), tolerance: 1e-3f);
        Assert.True(Vector2.Dot(leadM, velocityMps) > 0f);
    }

    /// <summary>
    /// What the ceiling on the lead is for: at speed, and at a framing close enough to read a car by,
    /// the lead would otherwise put the unit off the very picture it is the subject of.
    /// </summary>
    [Fact]
    public void TheLeadNeverTakesTheUnitOffThePicture()
    {
        var (camera, follow) = Watching();

        follow.Step(camera, UiPx, UnitM, new Vector2(0f, -60f), FrameS);

        var onScreenPx = camera.ScreenAt(UnitM, UiPx);
        Assert.InRange(onScreenPx.X, 0f, UiPx.X);
        Assert.InRange(onScreenPx.Y, 0f, UiPx.Y);
    }

    /// <summary>
    /// Free pan wins, whichever gesture it was: the camera is left where the hand put it and the unit
    /// goes on without it.
    /// </summary>
    [Fact]
    public void MovingTheCameraTakesItOffTheUnit()
    {
        foreach (var gesture in Gestures)
        {
            var (camera, follow) = Watching();
            follow.Step(camera, UiPx, UnitM, Vector2.Zero, FrameS);

            gesture(camera);
            var movedToM = camera.CentreM;
            follow.Step(camera, UiPx, UnitM + new Vector2(10f, 0f), Vector2.Zero, FrameS);

            Assert.False(follow.On);
            Assert.Equal(movedToM, camera.CentreM);
        }
    }

    /// <summary>Which is what asking again is for: a click on the unit puts the camera back on it.</summary>
    [Fact]
    public void AskingAgainPutsItBackOnTheUnit()
    {
        var (camera, follow) = Watching();
        follow.Step(camera, UiPx, UnitM, Vector2.Zero, FrameS);
        camera.PanByPixels(new Vector2(200f, 120f));
        follow.Step(camera, UiPx, UnitM, Vector2.Zero, FrameS);

        follow.Asked(oneUnit: true);
        follow.Step(camera, UiPx, UnitM, Vector2.Zero, FrameS);

        Assert.Equal(UnitM, camera.CentreM);
    }

    /// <summary>A group has no one place to stand, so the camera stays where the reader left it.</summary>
    [Fact]
    public void SeveralUnitsAreNotFollowedAtAll()
    {
        var (camera, follow) = Watching();
        var stoodAtM = camera.CentreM;

        follow.Asked(oneUnit: false);
        follow.Step(camera, UiPx, UnitM, Vector2.Zero, FrameS);

        Assert.False(follow.On);
        Assert.Equal(stoodAtM, camera.CentreM);
    }

    /// <summary>
    /// The town steps at a fixed rate and is drawn at the window's, so a camera nailed to the unit shows
    /// every tick boundary. What it does instead is close on the unit over a span of real time.
    /// </summary>
    [Fact]
    public void TheCameraClosesOnTheUnitRatherThanBeingNailedToIt()
    {
        var (camera, follow) = Watching();
        follow.Step(camera, UiPx, UnitM, Vector2.Zero, FrameS);
        var steppedToM = UnitM + new Vector2(2f, 0f);

        follow.Step(camera, UiPx, steppedToM, Vector2.Zero, FrameS);

        Assert.InRange(camera.CentreM.X, UnitM.X + 1e-3f, steppedToM.X - 1e-3f);
        Assert.Equal(UnitM.Y, camera.CentreM.Y, tolerance: 1e-3f);
    }

    /// <summary>
    /// And the same distance of it whatever the frame rate is: an ease written as a fraction of the frame
    /// would close twice as fast on a machine drawing twice as often.
    /// </summary>
    [Fact]
    public void TheEaseCoversTheSameGroundAtAnyFrameRate()
    {
        var overTenFrames = ClosedOn(new Vector2(6f, 0f), frames: 10, FrameS);
        var inOne = ClosedOn(new Vector2(6f, 0f), frames: 1, 10f * FrameS);

        Assert.Equal(overTenFrames.X, inOne.X, tolerance: 1e-3f);
    }

    /// <summary>
    /// The lead swings round as the unit turns rather than being thrown across the picture: a walker who
    /// stops at a kerb and a car that turns a corner both change heading faster than a reader can follow
    /// the offset doing it. <b>What the ease decides is when the lead arrives and not what it is.</b>
    /// </summary>
    [Fact]
    public void TheLeadSwingsRoundRatherThanJumping()
    {
        var velocityMps = new Vector2(8f, 0f);
        var (snapped, straight) = Watching();
        straight.Step(snapped, UiPx, UnitM, velocityMps, FrameS);
        var leadM = snapped.CentreM - UnitM;

        var (camera, follow) = Watching();
        follow.Step(camera, UiPx, UnitM, Vector2.Zero, FrameS);
        follow.Step(camera, UiPx, UnitM, velocityMps, FrameS);
        var afterOneFrameM = camera.CentreM - UnitM;
        for (var frame = 0; frame < 300; frame++) follow.Step(camera, UiPx, UnitM, velocityMps, FrameS);

        Assert.True(afterOneFrameM.Length() < leadM.Length() * 0.25f,
            $"the lead jumped {afterOneFrameM.Length():F2} m of {leadM.Length():F2} m in one frame");
        Assert.Equal(leadM.X, (camera.CentreM - UnitM).X, tolerance: 0.05f);
        Assert.Equal(leadM.Y, (camera.CentreM - UnitM).Y, tolerance: 0.05f);
    }

    /// <summary>
    /// A unit that jumped is stood on outright: somebody who got into a car, or a selection asked for
    /// across the town. Easing over a screen's length is a camera that has lost the unit until it lands.
    /// </summary>
    [Fact]
    public void AUnitThatJumpedIsStoodOnAtOnce()
    {
        var (camera, follow) = Watching();
        follow.Step(camera, UiPx, UnitM, Vector2.Zero, FrameS);
        var acrossTheTownM = UnitM + new Vector2(200f, 0f);

        follow.Step(camera, UiPx, acrossTheTownM, Vector2.Zero, FrameS);

        Assert.Equal(acrossTheTownM, camera.CentreM);
    }

    /// <summary>Where a follow of a unit standing still at the given place ends up after so many frames.</summary>
    static Vector2 ClosedOn(Vector2 offsetM, int frames, float seconds)
    {
        var (camera, follow) = Watching();
        follow.Step(camera, UiPx, UnitM, Vector2.Zero, FrameS);
        for (var frame = 0; frame < frames; frame++)
            follow.Step(camera, UiPx, UnitM + offsetM, Vector2.Zero, seconds);

        return camera.CentreM;
    }

    /// <summary>The three ways a reader moves the camera themselves, each of which ends a follow.</summary>
    static readonly Action<Camera2D>[] Gestures =
    [
        camera => camera.PanByPixels(new Vector2(200f, 120f)),
        camera => camera.Zoom(2f, UiPx * 0.25f, UiPx),
        camera => camera.Turn(0.4f, UiPx * 0.25f, UiPx),
    ];

    static (Camera2D Camera, Follow Follow) Watching()
    {
        var config = SimConfig.Shipped();
        var follow = new Follow(config);
        follow.Asked(oneUnit: true);
        return (new Camera2D(config, TownM, UiPx), follow);
    }

    static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
}
