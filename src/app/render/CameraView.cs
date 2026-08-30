using System.Numerics;
using System.Runtime.InteropServices;

namespace TrafficSimulation.App.Render;

/// <summary>What the vertex shader is told about the view. Written into a buffer, never pushed.</summary>
/// <remarks>
/// The screen's size is here rather than in a push constant for the same reason the camera is: a value
/// pushed per frame would be recorded per frame, and this engine records once. It is the window in
/// interface pixels and not the framebuffer — on a scaled display those differ by
/// <see cref="Runtime.AppWindow.UiScale"/>, and that division is what makes a panel the size it was
/// designed to be on a display of any density.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct CameraView(Vector2 CentreM, Vector2 ClipPerM, Vector2 UiPx);
