using System.Numerics;

namespace TrafficSimulation.Core.Geometry;

/// <summary>
/// <b>How a car stands in a bay</b>: which way it points in one, where its rear axle is, and whether a
/// bay is square enough to the kerb to be driven into at all (GEN-4i, GEN-4j).
/// </summary>
/// <remarks>
/// <para>
/// <b>The line between the lane and the bay is the car's</b> and not this file's: it is shaped when the car
/// makes the manoeuvre (<see cref="BayManoeuvre"/>, GEN-4f). What is here is what a pose in a bay <em>is</em>
/// — which way round the car stands (GEN-4j) and where its rear axle is (GEN-4i) — read by whatever shapes a
/// way to one or stands a car in one.
/// </para>
/// </remarks>
internal static class BayTemplate
{
    /// <summary>
    /// How square to the lane a bay has to stand to be one a car turns into. Below it the bay is parallel
    /// to the kerb, which this engine does not lay.
    /// </summary>
    const float SquareEnoughRad = 30f * MathF.PI / 180f;

    /// <summary>
    /// Whether a turn of this size is a turn into a bay rather than a slide along a kerb, stated once for
    /// whoever asks.
    /// </summary>
    public static bool SquareEnough(float turnRad) =>
        MathF.Abs(turnRad) >= SquareEnoughRad && MathF.Abs(turnRad) <= MathF.PI - SquareEnoughRad;

    /// <summary>
    /// <b>Where the rear axle of a car standing in a bay is</b>, as metres into the space from its mouth: square
    /// in it and in the middle of it (GEN-4i), read back from the middle of the body, because the axle is the
    /// point every line is drawn for.
    /// </summary>
    /// <remarks>
    /// <b>The body stands in the same place either way round and the axle does not</b> (GEN-4j). Nose in,
    /// the axle is the wheelbase's half short of the middle of the space; backed in, it is that far past it,
    /// at the deep end.
    /// </remarks>
    public static float RearAxleIntoTheBayM(float centreAheadOfAxleM, float bayLengthM, bool noseIn) =>
        MathF.Max(0f, (bayLengthM * 0.5f) + (noseIn ? -centreAheadOfAxleM : centreAheadOfAxleM));

    /// <summary>Which way the car itself points standing in a bay: into it, or back out of it.</summary>
    public static float StandingHeadingRad(float bayHeadingRad, bool noseIn) =>
        noseIn ? bayHeadingRad : bayHeadingRad + MathF.PI;

    /// <summary>And the same read off a car that is already standing there.</summary>
    public static bool StandsNoseIn(float bayHeadingRad, float carHeadingRad) =>
        Vector2.Dot(Heading.Unit(bayHeadingRad), Heading.Unit(carHeadingRad)) >= 0f;
}
