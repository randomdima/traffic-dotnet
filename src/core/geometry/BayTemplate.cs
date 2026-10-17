using System.Numerics;

namespace TrafficSimulation.Core.Geometry;

/// <summary>
/// <b>How a car stands in a bay</b>: which way it points in one, and whether a bay is square enough to the
/// kerb to be driven into at all (GEN-4j).
/// </summary>
/// <remarks>
/// <b>The line between the lane and the bay is the car's</b> and not this file's: it is shaped when the car
/// makes the manoeuvre (<see cref="BayManoeuvre"/>, GEN-4f). What is here is which way round a pose in a bay
/// stands (GEN-4j), read by whatever shapes a way to one or stands a car in one.
/// </remarks>
internal static class BayTemplate
{
    /// <summary>
    /// How square to the lane a bay has to stand to be one a car turns into. Below it the place is alongside
    /// the kerb, and a car slides into it rather than turning (GEN-4n).
    /// </summary>
    const float SquareEnoughRad = 30f * MathF.PI / 180f;

    /// <summary>
    /// Whether a turn of this size is a turn into a bay rather than a slide along a kerb, stated once for
    /// whoever asks.
    /// </summary>
    public static bool SquareEnough(float turnRad) =>
        MathF.Abs(turnRad) >= SquareEnoughRad && MathF.Abs(turnRad) <= MathF.PI - SquareEnoughRad;

    /// <summary>And whether it is a slide along the kerb, the same way the car is going (GEN-4n).</summary>
    public static bool Alongside(float turnRad) => MathF.Abs(turnRad) < SquareEnoughRad;

    /// <summary>Which way the car itself points standing in a bay: into it, or back out of it.</summary>
    public static float StandingHeadingRad(float bayHeadingRad, bool noseIn) =>
        noseIn ? bayHeadingRad : bayHeadingRad + MathF.PI;

    /// <summary>And the same read off a car that is already standing there.</summary>
    public static bool StandsNoseIn(float bayHeadingRad, float carHeadingRad) =>
        Vector2.Dot(Heading.Unit(bayHeadingRad), Heading.Unit(carHeadingRad)) >= 0f;
}
