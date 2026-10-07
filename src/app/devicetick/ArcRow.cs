using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>One <see cref="ArcSeg"/> as <c>core/geometry/kernels/arc.glsl</c>'s <c>Arc</c> reads it, word for word.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct ArcRow(Vector4 A, Vector4 B)
{
    public static ArcRow Of(in ArcSeg arc) =>
        new(new Vector4(arc.StartM, arc.StartUnit.X, arc.StartUnit.Y), new Vector4(arc.HeadingRad, arc.LengthM, arc.Curvature, 0f));
}
