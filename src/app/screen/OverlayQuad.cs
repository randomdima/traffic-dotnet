using System.Numerics;
using System.Runtime.InteropServices;

namespace TrafficSimulation.App.Screen;

/// <summary>
/// One quad of the third pipeline: a panel, a glyph, a tape, a ring or a debug line. The interface
/// and the debug layers are nothing else.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="Screen"/> is the whole difference between the interface and the layers.</b> Zero
/// puts the quad in the town's own metres, where a line drawn where it happens belongs; one puts it
/// in interface pixels from the top-left, where a panel laid out once stays put whatever the
/// camera does. One buffer, one draw, and the vertex shader picks the transform.
/// </para>
/// <para>
/// <b>The colour is four floats, not four bytes</b>: a panel is drawn over the town at partial alpha
/// and a layer's wash at a good deal less, and both are laid out in the same units the theme states
/// them in.
/// </para>
/// <para>
/// <b><see cref="Taper"/> is the one thing the quad is not a rectangle for</b>, and it is what lets a
/// band follow a bend. It moves the two ends in opposite directions along the quad's own axis, by
/// <see cref="Taper"/> at one edge and <c>−Taper</c> at the other, so an end is cut square to the line
/// the band is a band of rather than to the chord across it — and the next piece is cut on the same
/// line. Zero on everything else, which is every panel, glyph, tape and straight line drawn.
/// </para>
/// </remarks>
/// <param name="Taper">
/// How far the ends slant, in the quad's own units. A corner stands at
/// <c>±(HalfSize.X − Taper)</c> along the axis on the <c>+</c> side across it and at
/// <c>±(HalfSize.X + Taper)</c> on the <c>−</c> side.
/// </param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct OverlayQuad(
    Vector2 Centre, Vector2 HalfSize, Vector2 UvMin, Vector2 UvSize, Vector4 Colour, float Rotation, uint Screen,
    float Taper = 0f);
