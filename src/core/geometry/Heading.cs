using System.Numerics;

namespace TrafficSimulation.Core.Geometry;

/// <summary>
/// An angle as the pair of unit vectors everything downstream actually wants. <b>The one place a
/// heading becomes a direction</b>, so that the pair costs one argument reduction rather than two.
/// </summary>
/// <remarks>
/// <c>MathF.Cos(x)</c> and <c>MathF.Sin(x)</c> are two calls into libm that reduce the same argument
/// twice; <c>MathF.SinCos</c> is one that reduces it once, and measures a third cheaper for the pair.
/// The tick asks for a direction thousands of times — every arc sampled, every wheel steered, every
/// pose read off a body — so the saving is the tick's rather than a micro-optimisation's.
/// </remarks>
internal static class Heading
{
    /// <summary>The unit vector a heading points along.</summary>
    public static Vector2 Unit(float headingRad)
    {
        var (sin, cos) = MathF.SinCos(headingRad);
        return new Vector2(cos, sin);
    }

    /// <summary>
    /// A quarter turn from a unit heading, which with <c>+y</c> down is the driver's right and the way
    /// curvature counts positive. Takes the vector and not the angle: a caller that holds the direction
    /// already holds the answer, and asking for it by angle is the second reduction this type exists to
    /// remove.
    /// </summary>
    public static Vector2 RightOf(Vector2 unit) => new(-unit.Y, unit.X);

    /// <summary>Both at once, for the callers that want the frame rather than one axis of it.</summary>
    public static void Frame(float headingRad, out Vector2 forward, out Vector2 right)
    {
        forward = Unit(headingRad);
        right = RightOf(forward);
    }
}
