using System.Numerics;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.App.Camera;

/// <summary>
/// Top-down, pannable, zoomable and turnable, so that both the whole town and a single agent can be
/// watched, and a street can be read along rather than across.
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
/// <para><b>OBS-1c — the town is turned rather than the camera aimed.</b> <see cref="TurnRad"/> is how
/// far the picture is turned clockwise from north-up, which is the thing a reader is looking at; the
/// bearing anybody would have to invert it into appears nowhere. Every conversion between the town's
/// metres and the window's pixels goes through it, and so does the one pair the shaders are handed
/// (<see cref="ForShader"/>).</para>
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

    /// <summary>
    /// How far the town is turned on screen, clockwise, from north-up (OBS-1c). Zero is north up, which
    /// is where a run opens and where <see cref="FaceNorth"/> puts it back.
    /// </summary>
    public float TurnRad { get; private set; }

    /// <summary>
    /// The turn as the pair everything that applies it wants: the cosine and the sine, taken once.
    /// <b>It is what the shaders are handed too</b>, so the trigonometry is paid once a frame rather
    /// than once a vertex.
    /// </summary>
    public Vector2 Facing => new(MathF.Cos(TurnRad), MathF.Sin(TurnRad));

    /// <summary>
    /// The view rectangle in the camera's own axes — what is across the window and what is down it. It
    /// is what a scale bar and an opening framing are measured in; <b>what a cull is measured in is
    /// <see cref="CullSpanM"/></b>, which is this rectangle turned.
    /// </summary>
    public Vector2 ViewSpanM(Vector2 uiPx) => uiPx / PixelsPerMetre;

    /// <summary>
    /// The world-axis box the view covers, which is the view rectangle's own bounding box once it is
    /// turned (OBS-1c). <b>Everything that culls against the camera culls against this</b>: a town
    /// turned 45° shows a diamond, and a body just outside the upright rectangle is inside the picture.
    /// </summary>
    public Vector2 CullSpanM(Vector2 uiPx)
    {
        var spanM = ViewSpanM(uiPx);
        var facing = Vector2.Abs(Facing);
        return new Vector2(
            spanM.X * facing.X + spanM.Y * facing.Y,
            spanM.X * facing.Y + spanM.Y * facing.X);
    }

    /// <summary>Open on a named span across the short side, which is what <c>--view</c> asks for.</summary>
    public void SetSpan(float spanM, Vector2 uiPx) =>
        PixelsPerMetre = MathF.Min(uiPx.X, uiPx.Y) / spanM;

    public void LookAt(Vector2 pointM) => CentreM = pointM;

    /// <summary>What the vertex shader needs and nothing more: the middle of the view, clip units per metre, and the turn.</summary>
    public (Vector2 CentreM, Vector2 ClipPerM, Vector2 Facing) ForShader(Vector2 uiPx) =>
        (CentreM, new Vector2(2f * PixelsPerMetre / uiPx.X, 2f * PixelsPerMetre / uiPx.Y), Facing);

    public Vector2 WorldAt(Vector2 screenPx, Vector2 uiPx) =>
        CentreM + Unturned(screenPx - uiPx * 0.5f) / PixelsPerMetre;

    /// <summary>The other way round: where a place in the town lands on screen, which is what a label about it needs.</summary>
    public Vector2 ScreenAt(Vector2 pointM, Vector2 uiPx) =>
        Turned(pointM - CentreM) * PixelsPerMetre + uiPx * 0.5f;

    /// <summary>Arrow keys, in interface pixels a second, so a pan covers the same distance on screen at any zoom.</summary>
    /// <remarks>
    /// <b>The direction is the window's and not the town's</b>: the up arrow moves the picture down the
    /// screen whichever way the town is turned, which is what makes panning a turned town readable.
    /// </remarks>
    public void Pan(Vector2 direction, float seconds)
    {
        if (direction == Vector2.Zero) return;

        CentreM += Unturned(Vector2.Normalize(direction)) * (_config.View.CameraPanPxPerS * seconds / PixelsPerMetre);
    }

    public void PanByPixels(Vector2 deltaPx) => CentreM -= Unturned(deltaPx) / PixelsPerMetre;

    /// <summary>
    /// The wheel, about the pointer: the world point under the cursor is the one that does not move,
    /// which is what makes a zoom feel like leaning in rather than like being pushed.
    /// </summary>
    public void Zoom(float notches, Vector2 pointerPx, Vector2 uiPx)
    {
        if (notches == 0f) return;

        Scale(MathF.Pow(_config.View.CameraZoomPerNotch, notches), pointerPx, uiPx);
    }

    /// <summary>
    /// The same zoom asked for as a factor rather than as notches, which is what two fingers spreading
    /// have to say (CTL-9). The wheel's own step is one of these with the shipped factor in it.
    /// </summary>
    public void Scale(float factor, Vector2 aboutPx, Vector2 uiPx)
    {
        if (factor <= 0f || factor == 1f) return;

        var before = WorldAt(aboutPx, uiPx);
        PixelsPerMetre = Math.Clamp(PixelsPerMetre * factor,
            MathF.Min(uiPx.X, uiPx.Y) / WholeTownSpanM, ZoomInStopPixelsPerMetre);
        CentreM += before - WorldAt(aboutPx, uiPx);
    }

    /// <summary>
    /// <b>OBS-1c — the town turns about the point it is turned at</b>, exactly as it scales about the
    /// point it is scaled at: the world under that pixel is the one thing that does not move, so a
    /// twist between two fingers turns what is between them rather than what is in the corner.
    /// </summary>
    /// <param name="turnRad">How far the picture is to turn clockwise, which is what a hand asks for.</param>
    public void Turn(float turnRad, Vector2 aboutPx, Vector2 uiPx)
    {
        if (turnRad == 0f) return;

        var before = WorldAt(aboutPx, uiPx);
        TurnRad = Wrapped(TurnRad + turnRad);
        CentreM += before - WorldAt(aboutPx, uiPx);
    }

    /// <summary>
    /// North up again, about the middle of the window — which is the one point a turn about it leaves
    /// alone. <b>It is the only way back to level, and that is deliberate</b>: a turn that snapped to
    /// north on its own could never be nudged away from it a degree at a time, since every degree would
    /// be snapped back before the next arrived.
    /// </summary>
    public void FaceNorth() => TurnRad = 0f;

    /// <summary>Whether the town is drawn any way but north up, which is the whole of what the compass is for.</summary>
    public bool IsTurned => TurnRad != 0f;

    static float Wrapped(float radians) =>
        radians - MathF.Tau * MathF.Round(radians / MathF.Tau);

    /// <summary>A direction in the town's metres, into the window's axes.</summary>
    Vector2 Turned(Vector2 alongM)
    {
        var facing = Facing;
        return new Vector2(
            alongM.X * facing.X - alongM.Y * facing.Y,
            alongM.X * facing.Y + alongM.Y * facing.X);
    }

    /// <summary>And back: a direction in the window's axes, into the town's.</summary>
    Vector2 Unturned(Vector2 alongPx)
    {
        var facing = Facing;
        return new Vector2(
            alongPx.X * facing.X + alongPx.Y * facing.Y,
            -alongPx.X * facing.Y + alongPx.Y * facing.X);
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
