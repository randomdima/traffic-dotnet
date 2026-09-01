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
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct CameraView(Vector2 CentreM, Vector2 ClipPerM, Vector2 UiPx, Vector2 Facing);
