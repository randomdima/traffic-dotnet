using System.Numerics;
using System.Runtime.InteropServices;

namespace TrafficSimulation.App.Render;

/// <summary>What the vertex shader is told about the view. Written into a buffer, never pushed.</summary>
/// <remarks>
/// <para>
/// The screen's size is here rather than in a push constant for the same reason the camera is: a value
/// pushed per frame would be recorded per frame, and this engine records once. It is the window in
/// interface pixels and not the framebuffer — on a scaled display those differ by
/// <see cref="Runtime.AppWindow.UiScale"/>, and that division is what makes a panel the size it was
/// designed to be on a display of any density.
/// </para>
/// <para>
/// <b><see cref="Facing"/> is the turn as its cosine and its sine</b> (OBS-1c), so a vertex stage
/// applies it with two multiplies and no trigonometry. It is applied in the one place a world position
/// becomes a clip position, which is why nothing that carries a rotation of its own — a sprite's
/// heading, a band's direction — has to know the town is turned at all.
/// </para>
/// <para>
/// <b>The ground's periods are the renderer's and not the caller's</b> (<see cref="GroundMesh.SurfacePeriodsM"/>): a
/// corner carries no texture coordinate, so the vertex stage divides its place by its surface's period, and the
/// renderer writes the ones its mesh was laid with over whatever it is handed. Four floats a vector, because a
/// uniform block lays a vector of four at sixteen bytes and the five surfaces need two.
/// </para>
/// <para>
/// <b><see cref="PropCull"/> is the renderer's too</b>: the least half-size a prop is drawn at this frame, and the prop
/// looks' sheets from and past (<see cref="TownRenderer.LayTown"/>) — a prop smaller on screen than
/// <c>ViewFigures.PropLeastPx</c> is put out of the picture by the vertex stage, which is where its size is known.
/// </para>
/// <para>
/// <b><see cref="Turning"/> is how far every fan has turned at this tick (OBJ-6, TRN-13)</b>, in turns
/// (<see cref="FanTable.TurnsAt"/>), each body adding its own share in the fragment stage — then the least a picture's
/// widest fan is across for it to draw any this frame, in metres (<c>ViewFigures.FanLeastPx</c>): below it no one reads
/// the turn, and the picture is drawn as it is. A vector of four for the block's sake; the other two are nought.
/// </para>
/// <para>
/// <b><see cref="Clouds"/> is the clouds' shadows at this tick (OBJ-8)</b>, <see cref="CloudShadows.At"/>; left out, a
/// frame draws none.
/// </para>
/// <para>
/// <b><see cref="Patches"/> is the renderer's, as the periods are</b> (<see cref="GroundMesh.Patches"/>): the tint every
/// surface takes from where it lies. Left out, the ground is drawn untinted.
/// </para>
/// <para>
/// <b><see cref="WaterDriftM"/> is how far the water's two looks have drifted at this tick</b>,
/// <see cref="GroundPatches.DriftAt"/>; left out, the water stands.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct CameraView(
    Vector2 CentreM, Vector2 ClipPerM, Vector2 UiPx, Vector2 Facing, Vector4 SurfacePeriodsM = default,
    Vector4 WaterPeriodM = default, Vector4 PropCull = default, Vector4 Turning = default, CloudShadow Clouds = default,
    GroundPatches Patches = default, Vector4 WaterDriftM = default)
{
    /// <summary>How far the fans have turned and the least one is cut across, as <see cref="Turning"/> carries them.</summary>
    public static Vector4 Turned(float turns, float leastAcrossM) => new(turns, leastAcrossM, 0f, 0f);
}
