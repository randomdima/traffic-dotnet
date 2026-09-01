using System.Numerics;
using TrafficSimulation.App.Camera;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.App.PlayerControl;

/// <summary>
/// <b>CTL-9 — two fingers on the glass, read as one movement of the camera.</b> What the page reports
/// is where the contacts are; what a hand meant by them is the change between one frame and the next,
/// and it is three things at once: the pair's middle pans, the distance between them zooms, and the
/// angle between them turns (OBS-1c).
/// </summary>
/// <remarks>
/// <para>
/// <b>All three are read every frame and none of them is a mode.</b> A gesture that had to be
/// classified as a pinch or a twist before it was applied is one that picks wrong on the frame the
/// hand had not decided yet, and then holds the wrong answer for the rest of the movement.
/// </para>
/// <para>
/// <b>The twist alone has a dead zone</b>, and it is the one place a raw reading would be wrong rather
/// than merely small: no two fingers spread perfectly square, so a pinch that turned by whatever the
/// hand happened to do leaves the town a few degrees off north every time it is zoomed. Past the dead
/// zone the turn follows every frame, and what was spent crossing it is never applied — so the town
/// does not jump on the frame the twist is believed.
/// </para>
/// </remarks>
internal sealed class TouchGesture
{
    Vector2 _onePx;
    Vector2 _twoPx;

    /// <summary>Whether two fingers were down last frame, which is what makes this frame's reading a difference.</summary>
    bool _held;

    /// <summary>How far the pair has twisted since it went down, until the dead zone is crossed.</summary>
    float _twistedRad;

    /// <summary>And whether it has been crossed, after which every frame's twist is the town's turn.</summary>
    bool _turning;

    /// <summary>
    /// One frame of the fingers on the glass, and whether two of them are holding the camera. <b>The
    /// answer is what stops the one-finger gestures</b>: a second finger landing mid-drag ends that drag
    /// rather than panning and boxing at once (CTL-9).
    /// </summary>
    public bool Read(ReadOnlySpan<Vector2> touchesPx, Camera2D camera, Vector2 uiPx, SimConfig config)
    {
        if (touchesPx.Length < 2)
        {
            _held = false;
            return false;
        }

        var onePx = touchesPx[0];
        var twoPx = touchesPx[1];
        if (!_held)
        {
            _held = true;
            _turning = false;
            _twistedRad = 0f;
            (_onePx, _twoPx) = (onePx, twoPx);
            return true;
        }

        // The middle first, so the zoom and the turn are taken about where the fingers are now rather
        // than about where they were before the pan moved the ground under them.
        var middlePx = (onePx + twoPx) * 0.5f;
        camera.PanByPixels(middlePx - ((_onePx + _twoPx) * 0.5f));

        var was = _twoPx - _onePx;
        var now = twoPx - onePx;
        var wasPx = was.Length();
        var nowPx = now.Length();
        (_onePx, _twoPx) = (onePx, twoPx);

        if (wasPx < config.View.TouchLeastSpreadPx || nowPx < config.View.TouchLeastSpreadPx) return true;

        camera.Scale(nowPx / wasPx, middlePx, uiPx);

        var twistedRad = Turned(MathF.Atan2(now.Y, now.X) - MathF.Atan2(was.Y, was.X));
        _twistedRad += twistedRad;
        _turning |= MathF.Abs(_twistedRad) > float.DegreesToRadians(config.View.CameraTwistDeadZoneDeg);
        if (_turning) camera.Turn(twistedRad, middlePx, uiPx);

        return true;
    }

    /// <summary>
    /// A frame's worth of twist, which is a small angle: the difference of two headings crosses the cut
    /// in <c>atan2</c> whenever the pair happens to lie along it, and a half turn read there would spin
    /// the town on a frame nothing moved.
    /// </summary>
    static float Turned(float radians) => radians - MathF.Tau * MathF.Round(radians / MathF.Tau);
}
