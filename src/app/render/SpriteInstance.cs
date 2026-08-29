using System.Numerics;
using System.Runtime.InteropServices;

namespace TrafficSimulation.App.Render;

/// <summary>
/// One upright quad on the ground: everything the second pipeline is told about a body, and the only
/// thing that changes between two frames of a standing town.
/// </summary>
/// <remarks>
/// <para>
/// <b>The rotation belongs to whatever the art draws one frame of</b>, and is zero for everything else.
/// A walker on its feet does not turn with the body: the sheet draws all eight facings and every one of
/// them is drawn upright, so heading is shown by <em>which cell</em> is sampled. A car's art is one frame
/// with its nose along <c>+x</c>, and so is a body lying in the road (PER-18), so for those the heading
/// is the quad's own rotation — one float, whose sine and cosine are taken in the shader where the quad
/// is built anyway.
/// </para>
/// <para>
/// <b>The tint is a colour and an opacity</b>, and everything the town draws but a mark leaves both
/// alone: a mark is the same quad laid at whatever strength the tyre that made it earned, and a picture
/// cannot carry a per-instance strength. <b>Nothing here says "selected"</b> — that is a shape drawn
/// over the picture rather than a colour laid into it (<see cref="Hud.SelectionMark"/>).
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct SpriteInstance(
    Vector2 CentreM, Vector2 HalfSizeM, Vector2 UvMin, Vector2 UvSize, Vector4 Tint, uint Sheet, float HeadingRad);
