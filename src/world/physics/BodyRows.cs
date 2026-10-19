using System.Numerics;

namespace TrafficSimulation.World.Physics;

/// <summary>
/// <b>One body's row as the kernels read it</b>, field by field (<c>kernels/step.glsl</c>, <c>kernels/filter.glsl</c>): what a
/// device lays the table into, and what a reader of the table here lays it into to read it as a kernel reads the device's.
/// </summary>
internal static class BodyRows
{
    /// <summary>Where it stands and how it is turned: position, then rotation's cosine and sine.</summary>
    public static Vector4 Pose(in BodyTable table, int body) =>
        new(table.PositionM[body], table.Rotation[body].X, table.Rotation[body].Y);

    /// <summary>Its shape: the core's half-size, the corner radius rolled round it, and how far its furthest point reaches.</summary>
    public static Vector4 Shape(in BodyTable table, int body) =>
        new(table.ExtentM[body], table.CornerRadiusM[body], table.ReachM[body]);

    /// <summary>Velocity, then yaw rate.</summary>
    public static Vector4 Motion(in BodyTable table, int body) => new(table.VelocityMps[body], table.YawRateRadPerS[body], 0f);

    /// <summary>Its filter in one word: its flags, then its category, its mask and its channels, a byte each.</summary>
    public static uint Filter(in BodyTable table, int body) =>
        (uint)table.Flags[body] | (LayerByte(table.Category[body]) << 8) | (LayerByte(table.Mask[body]) << 16)
        | ((uint)table.Channels[body] << 24);

    /// <summary>A layer mask in the byte the kernels read it in: every layer this town has is in its low bits.</summary>
    public static uint LayerByte(ulong mask) =>
        mask >> 8 == 0 ? (uint)mask : throw new InvalidOperationException("A collision layer past the 8th is not laid on the device.");
}
