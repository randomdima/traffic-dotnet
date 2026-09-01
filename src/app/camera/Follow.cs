using System.Numerics;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.App.Camera;

/// <summary>
/// <b>OBS-1a: the camera stands on the one unit that is picked out</b>, led by that unit's own speed so
/// the ground it is about to cover is on screen. It holds no unit of its own — a frame hands it where
/// the followed unit is, and a frame that has no single unit to hand hands it nothing.
/// </summary>
/// <remarks>
/// <b>Free pan wins by arithmetic rather than by a flag</b>: what the follow left the camera at is kept,
/// and a frame that finds the camera anywhere else knows the reader moved it themselves. That is one
/// comparison for the pan, the zoom and the turn alike, and there is no way for a gesture to move the
/// camera without this noticing — including gestures that have not been written yet.
/// </remarks>
internal sealed class Follow(SimConfig config)
{
    /// <summary>Where this left the camera, while it is still there.</summary>
    (Vector2 CentreM, float PixelsPerMetre, float TurnRad)? _leftAt;

    /// <summary>Whether the camera is on a unit.</summary>
    public bool On { get; private set; }

    /// <summary>
    /// A selection was asked for on the town — a click or a box (CTL-1b). One unit is followed and
    /// anything else is not, and asking again is how a reader puts the camera back on a unit they have
    /// since panned away from.
    /// </summary>
    public void Asked(bool oneUnit)
    {
        Stop();
        On = oneUnit;
    }

    /// <summary>The camera is nobody's: there is no single unit to stand on, or the reader has taken it.</summary>
    public void Stop()
    {
        On = false;
        _leftAt = null;
    }

    /// <summary>
    /// The camera onto the unit, after the tick that moved it. <b>Called with the unit's own position and
    /// velocity rather than with the unit</b>, so what is followed can be a car, a walker, or the car a
    /// walker is riding in, and none of that is this class's to know.
    /// </summary>
    public void Step(Camera2D camera, Vector2 uiPx, Vector2 atM, Vector2 velocityMps)
    {
        if (!On) return;

        if (_leftAt is { } left && left != (camera.CentreM, camera.PixelsPerMetre, camera.TurnRad))
        {
            Stop();
            return;
        }

        camera.LookAt(atM + LeadM(camera, uiPx, velocityMps));
        _leftAt = (camera.CentreM, camera.PixelsPerMetre, camera.TurnRad);
    }

    /// <summary>
    /// How far in front of the unit the camera stands: the ground it covers in the lead time, cut back to
    /// its share of the half-view so that the unit itself stays on the picture at any speed and any zoom.
    /// The short side is what the ceiling is taken off, since the lead points wherever the unit is going.
    /// </summary>
    Vector2 LeadM(Camera2D camera, Vector2 uiPx, Vector2 velocityMps)
    {
        var leadM = velocityMps * config.View.CameraFollowLeadS;
        var spanM = camera.ViewSpanM(uiPx);
        var mostM = MathF.Min(spanM.X, spanM.Y) * 0.5f * config.View.CameraFollowLeadShareOfView;
        var lengthM = leadM.Length();
        return lengthM > mostM ? leadM * (mostM / lengthM) : leadM;
    }
}
