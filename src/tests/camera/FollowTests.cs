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

    [Fact]
    public void AUnitAtRestIsStoodExactlyOn()
    {
        var (camera, follow) = Watching();

        follow.Step(camera, UiPx, UnitM, Vector2.Zero);

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

        follow.Step(camera, UiPx, UnitM, velocityMps);

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

        follow.Step(camera, UiPx, UnitM, new Vector2(0f, -60f));

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
            follow.Step(camera, UiPx, UnitM, Vector2.Zero);

            gesture(camera);
            var movedToM = camera.CentreM;
            follow.Step(camera, UiPx, UnitM + new Vector2(10f, 0f), Vector2.Zero);

            Assert.False(follow.On);
            Assert.Equal(movedToM, camera.CentreM);
        }
    }

    /// <summary>Which is what asking again is for: a click on the unit puts the camera back on it.</summary>
    [Fact]
    public void AskingAgainPutsItBackOnTheUnit()
    {
        var (camera, follow) = Watching();
        follow.Step(camera, UiPx, UnitM, Vector2.Zero);
        camera.PanByPixels(new Vector2(200f, 120f));
        follow.Step(camera, UiPx, UnitM, Vector2.Zero);

        follow.Asked(oneUnit: true);
        follow.Step(camera, UiPx, UnitM, Vector2.Zero);

        Assert.Equal(UnitM, camera.CentreM);
    }

    /// <summary>A group has no one place to stand, so the camera stays where the reader left it.</summary>
    [Fact]
    public void SeveralUnitsAreNotFollowedAtAll()
    {
        var (camera, follow) = Watching();
        var stoodAtM = camera.CentreM;

        follow.Asked(oneUnit: false);
        follow.Step(camera, UiPx, UnitM, Vector2.Zero);

        Assert.False(follow.On);
        Assert.Equal(stoodAtM, camera.CentreM);
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
