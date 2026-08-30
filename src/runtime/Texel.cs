using System.Runtime.InteropServices;

namespace TrafficSimulation.Runtime;

/// <summary>
/// One texel, as both machines' textures are laid out: red, green, blue, alpha, one byte each.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is this project's own so that the browser's head need not carry a decoder.</b> The type a
/// picture library hands back is that library's, and naming it in the atlas, the mip chain and the
/// blitter is what put an image codec into a page that has a perfectly good one of its own
/// (<see cref="TrafficSimulation.App.Render.Texels"/>). Four bytes in this order is not a thing worth
/// a dependency.
/// </para>
/// <para>
/// <b>Sequential and blittable, and both are load-bearing.</b> A page of the atlas is cast whole to
/// bytes on its way to the driver, so the layout here is the layout the sampler reads; anything that
/// reordered or padded it would come out as a colour swap rather than as an error.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct Texel(byte R, byte G, byte B, byte A);
