using System.Numerics;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.App.Camera;

/// <summary>
/// Top-down, pannable and zoomable, so that both the whole town and a single agent can be watched
/// It is the only thing that decides how much world is on screen: the viewport is the window, one to
/// one, with no letterbox bars.
/// </summary>
/// <remarks>
/// <para>The shipped feel: arrows pan at 300 screen pixels a second, the wheel zooms
/// about the pointer by 1.15 a notch, and <b>the view opens on a 70 m span at the middle of the
/// town</b> — not on the whole-town fit, which on a small map is unreadably small.</para>
/// <para><b>Every pixel here is an interface pixel</b>, the space the panels are laid out in, so a
/// scaled desktop changes none of the framing: a span in metres over twice the display's pixels is
/// the same picture at twice the resolution. <see cref="DevicePxPerUiPx"/> is the single exception,
/// and it is named where it is used.</para>
/// </remarks>
internal sealed class Camera2D
{
    readonly SimConfig _config;
    readonly Vector2 _worldSizeM;

    public Camera2D(SimConfig config, Vector2 worldSizeM, Vector2 uiPx)
    {
        _config = config;
        _worldSizeM = worldSizeM;
        CentreM = worldSizeM * 0.5f;
        PixelsPerMetre = MathF.Min(uiPx.X, uiPx.Y) / config.View.CameraDefaultViewM;
    }

    /// <summary>
    /// The display's own pixels per interface pixel — the desktop's scale factor, and the one thing
    /// about the display this class is told. It sets <em>only</em> the zoom-in stop, which is a claim
    /// about the art's texels against the display's pixels and not about the interface.
    /// </summary>
    public float DevicePxPerUiPx { get; set; } = 1f;

    /// <summary>Where the middle of the window is, in the world.</summary>
    public Vector2 CentreM { get; private set; }

    public float PixelsPerMetre { get; private set; }

    public Vector2 ViewSpanM(Vector2 uiPx) => uiPx / PixelsPerMetre;

    /// <summary>Open on a named span across the short side, which is what <c>--view</c> asks for.</summary>
    public void SetSpan(float spanM, Vector2 uiPx) =>
        PixelsPerMetre = MathF.Min(uiPx.X, uiPx.Y) / spanM;

    public void LookAt(Vector2 pointM) => CentreM = pointM;

    /// <summary>What the vertex shader needs and nothing more: the middle of the view, and clip units per metre.</summary>
    public (Vector2 CentreM, Vector2 ClipPerM) ForShader(Vector2 uiPx) =>
        (CentreM, new Vector2(2f * PixelsPerMetre / uiPx.X, 2f * PixelsPerMetre / uiPx.Y));

    public Vector2 WorldAt(Vector2 screenPx, Vector2 uiPx) =>
        CentreM + (screenPx - uiPx * 0.5f) / PixelsPerMetre;

    /// <summary>The other way round: where a place in the town lands on screen, which is what a label about it needs.</summary>
    public Vector2 ScreenAt(Vector2 pointM, Vector2 uiPx) =>
        (pointM - CentreM) * PixelsPerMetre + uiPx * 0.5f;

    /// <summary>Arrow keys, in interface pixels a second, so a pan covers the same distance on screen at any zoom.</summary>
    public void Pan(Vector2 direction, float seconds)
    {
        if (direction == Vector2.Zero) return;

        CentreM += Vector2.Normalize(direction) * (_config.View.CameraPanPxPerS * seconds / PixelsPerMetre);
    }

    public void PanByPixels(Vector2 deltaPx) => CentreM -= deltaPx / PixelsPerMetre;

    /// <summary>
    /// The wheel, about the pointer: the world point under the cursor is the one that does not move,
    /// which is what makes a zoom feel like leaning in rather than like being pushed.
    /// </summary>
    public void Zoom(float notches, Vector2 pointerPx, Vector2 uiPx)
    {
        if (notches == 0f) return;

        var before = WorldAt(pointerPx, uiPx);
        PixelsPerMetre = Math.Clamp(PixelsPerMetre * MathF.Pow(_config.View.CameraZoomPerNotch, notches),
            MathF.Min(uiPx.X, uiPx.Y) / WholeTownSpanM, ZoomInStopPixelsPerMetre);
        CentreM += before - WorldAt(pointerPx, uiPx);
    }

    /// <summary>
    /// The zoom-out end, and not a figure of its own: out until the whole town is on screen — the
    /// town's own longer side.
    /// </summary>
    float WholeTownSpanM => MathF.Max(_worldSizeM.X, _worldSizeM.Y);

    /// <summary>
    /// The zoom-in end: the art's own grid against the display's pixels, times how far past 1:1 the
    /// zoom is allowed to magnify it. The scale factor divides it because the claim is about the
    /// display's pixels and not the interface's — on a 2× desktop an interface pixel is two of the
    /// display's, so stopping at the art's own 96 would let the zoom run to twice the resolution the
    /// sprites carry.
    /// </summary>
    float ZoomInStopPixelsPerMetre =>
        _config.View.CarSpritePixelsPerMetre * _config.View.CameraMaxSpriteMagnification / DevicePxPerUiPx;
}
