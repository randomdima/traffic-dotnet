using System.Numerics;
using System.Runtime.InteropServices;

namespace TrafficSimulation.App.Render;

/// <summary>
/// One upright quad on the ground: everything the second pipeline is told about a body, and the only
/// thing that changes between two frames of a standing town.
/// </summary>
/// <remarks>
/// <para>
/// <b>The rotation is the car's and is zero for everything else.</b> A person's sprite does not turn
/// with the body: the sheet draws all eight facings and every one of them is drawn upright, so heading
/// is shown by <em>which cell</em> is sampled. A car's art is one frame with its nose along <c>+x</c>,
/// so its heading is the quad's own rotation — one float, whose sine and cosine are taken in the shader
/// where the quad is built anyway.
/// </para>
/// <para>
/// <b>The tint's colour is three floats for the reason the ground's is</b>: a highlight is the sprite
/// drawn brighter, and an eight-bit unorm tint clamps at white — which is exactly the case a highlight
/// has to survive. <b>Its fourth is an opacity</b>, and everything the town draws but a mark leaves it
/// at one: a mark is the same quad laid at whatever strength the tyre that made it earned, and a
/// picture cannot carry a per-instance strength.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct SpriteInstance(
    Vector2 CentreM, Vector2 HalfSizeM, Vector2 UvMin, Vector2 UvSize, Vector4 Tint, uint Sheet, float HeadingRad);
