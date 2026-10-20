using System.Numerics;

namespace TrafficSimulation.Core.Geometry;

/// <summary>The way between two points, the same on every head.</summary>
internal static class Blend
{
    /// <summary>
    /// <paramref name="from"/> taken <paramref name="share"/> of the way to <paramref name="to"/>, as
    /// <c>fma(from, 1 − share, to × share)</c> — which is what <see cref="Vector2.Lerp(Vector2, Vector2, float)"/> answers
    /// on the desktop, to the bit.
    /// </summary>
    /// <remarks>
    /// <see cref="Vector2.Lerp(Vector2, Vector2, float)"/> fuses only where the machine has a fused multiply-add, which a
    /// page does not; an explicit fused one is exact on every head, a page's being libm's own.
    /// </remarks>
    public static Vector2 Lerp(Vector2 from, Vector2 to, float share)
    {
        var keep = 1f - share;
        return new Vector2(
            MathF.FusedMultiplyAdd(from.X, keep, to.X * share), MathF.FusedMultiplyAdd(from.Y, keep, to.Y * share));
    }

    /// <summary>Each axis its own share of the way (<see cref="Lerp(Vector2, Vector2, float)"/>).</summary>
    public static Vector2 Lerp(Vector2 from, Vector2 to, Vector2 share) => new(
        MathF.FusedMultiplyAdd(from.X, 1f - share.X, to.X * share.X), MathF.FusedMultiplyAdd(from.Y, 1f - share.Y, to.Y * share.Y));
}
