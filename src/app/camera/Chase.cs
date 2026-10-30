using System.Numerics;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.App.Camera;

/// <summary>
/// <b>A player's camera</b> (MP-9): it chases the player's own car, a lead in front of it, and closes on that place through
/// a critically damped spring, so the steps a car drawn off 20 snapshots a second still has, and a lead that swings round
/// with the car, never reach the picture as a jolt. The player may zoom it in as far as a reader's camera goes
/// (<see cref="Camera2D.ZoomInStopPixelsPerMetre"/>) and out to <see cref="MultiplayerFigures.WidestViewM"/>, and drag it
/// up to <see cref="MultiplayerFigures.MostPanM"/> off the car; let go, it comes back. It is never turned.
/// </summary>
/// <remarks>
/// <para>
/// <b>Unlike <see cref="Follow"/>, the camera itself eases</b>, and the car moves a little on the glass as it speeds up
/// and slows down. A follow carries its unit exactly because the town under it is stepped at the tick, so the unit is
/// steady and the residue goes into the lead; a player's car is drawn off snapshots, so carrying it exactly would carry
/// every residue of its interpolation onto the whole town around it.
/// </para>
/// <para>
/// <b>The drag is laid over the spring and not through it</b>, so the town follows the hand exactly while it holds, and
/// only the car's own motion is smoothed. A car further off than the view is wide — a new seat, a first frame — is
/// jumped to rather than chased across the town.
/// </para>
/// </remarks>
internal sealed class Chase(SimConfig config)
{
    Vector2 _atM;
    Vector2 _speedMps;
    Vector2 _leadM;
    Vector2 _panM;
    bool _dragging;
    float _spanM = config.View.CameraDefaultViewM;
    bool _on;

    /// <summary>Nothing chased: the next car handed is stood on outright, and the drag is let go of.</summary>
    public void Stop()
    {
        _on = false;
        _panM = Vector2.Zero;
    }

    /// <summary>The wheel: in by a notch's factor, out by its inverse, held between the closest and the widest view.</summary>
    public void Zoom(float notches) => _spanM /= MathF.Pow(config.View.CameraZoomPerNotch, notches);

    /// <summary>
    /// A drag this frame, in interface pixels at the camera's scale: the town moves with the hand, and stops where the
    /// view is <see cref="MultiplayerFigures.MostPanM"/> off the car.
    /// </summary>
    public void Drag(Vector2 travelledPx, float pixelsPerMetre)
    {
        _dragging = true;
        _panM -= travelledPx / pixelsPerMetre;
        var mostM = config.Multiplayer.MostPanM;
        if (_panM.Length() > mostM) _panM = Vector2.Normalize(_panM) * mostM;
    }

    /// <summary>The hand let go: the view goes back to its car over <see cref="MultiplayerFigures.PanReturnS"/>.</summary>
    public void Release() => _dragging = false;

    /// <summary>
    /// The camera onto the car for the frame about to be drawn: framed to the player's span and closed on the car's lead
    /// over <paramref name="seconds"/> of real time, with the drag laid over it.
    /// </summary>
    public void Step(Camera2D camera, Vector2 uiPx, Vector2 carM, Vector2 carMps, float seconds)
    {
        Frame(camera, uiPx);

        var spanM = camera.ViewSpanM(uiPx);
        var wantedLeadM = Lead(carMps, spanM);
        if (!_on || Vector2.Distance(_atM, carM + _leadM) > MathF.Max(spanM.X, spanM.Y))
        {
            _on = true;
            _leadM = wantedLeadM;
            _atM = carM + _leadM;
            _speedMps = carMps;
        }
        else
        {
            _leadM = Vector2.Lerp(_leadM, wantedLeadM, Closed(seconds, config.View.CameraFollowLeadEaseS));
            (_atM, _speedMps) = Settle(_atM, _speedMps, carM + _leadM, seconds, config.Multiplayer.CameraSettleS);
        }

        if (!_dragging) _panM *= 1f - Closed(seconds, config.Multiplayer.PanReturnS);
        camera.LookAt(_atM + _panM);
    }

    /// <summary>
    /// The player's span, every frame — what the wheel asked for across the short side, no closer than the camera's
    /// zoom-in stop and no wider than the widest along the long side — so a canvas resized is framed again.
    /// </summary>
    void Frame(Camera2D camera, Vector2 uiPx)
    {
        var shortPx = MathF.Min(uiPx.X, uiPx.Y);
        var widestShortM = config.Multiplayer.WidestViewM * shortPx / MathF.Max(uiPx.X, uiPx.Y);
        var closestShortM = shortPx / camera.ZoomInStopPixelsPerMetre;
        _spanM = Math.Clamp(_spanM, MathF.Min(closestShortM, widestShortM), widestShortM);
        camera.SetSpan(_spanM, uiPx);
    }

    /// <summary>The ground the car covers in the lead time, cut to its share of the half-view (OBS-1a's lead).</summary>
    Vector2 Lead(Vector2 carMps, Vector2 spanM)
    {
        var leadM = carMps * config.View.CameraFollowLeadS;
        var mostM = MathF.Min(spanM.X, spanM.Y) * 0.5f * config.View.CameraFollowLeadShareOfView;
        var lengthM = leadM.Length();
        return lengthM > mostM ? leadM * (mostM / lengthM) : leadM;
    }

    /// <summary>How much of what is left to close this frame, at any frame rate: an exponential approach.</summary>
    static float Closed(float seconds, float easeS) =>
        easeS <= 0f ? 1f : seconds <= 0f ? 0f : 1f - MathF.Exp(-seconds / easeS);

    /// <summary>
    /// <b>One frame of a critically damped spring</b> from where the camera is, moving as it is, towards a place: it
    /// arrives without overshooting, and a target that moves at a steady speed is trailed at a steady distance rather than
    /// jerked after. Exact for any frame length, by the usual polynomial for the exponential.
    /// </summary>
    internal static (Vector2 AtM, Vector2 SpeedMps) Settle(Vector2 atM, Vector2 speedMps, Vector2 toM, float seconds, float settleS)
    {
        if (settleS <= 0f) return (toM, Vector2.Zero);

        var omega = 2f / settleS;
        var x = omega * seconds;
        var decay = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
        var offM = atM - toM;
        var pull = (speedMps + omega * offM) * seconds;
        return (toM + (offM + pull) * decay, (speedMps - omega * pull) * decay);
    }
}
