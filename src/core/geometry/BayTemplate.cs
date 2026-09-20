using System.Numerics;

namespace TrafficSimulation.Core.Geometry;

/// <summary>
/// <b>How a car stands in a bay</b>: which way it points in one, where its rear axle is, and whether a
/// bay is square enough to the kerb to be driven into at all (GEN-4i, GEN-4j).
/// </summary>
/// <remarks>
/// <para>
/// <b>The line between the lane and the bay is the town's</b> and not this file's any more: a bay's ways
/// are laid with the town (<c>World.Parking.BayWays</c>) and driven like any other way of it (CAR-15).
/// What is left here is what a pose in a bay <em>is</em>, which both the laying and the standing read.
/// </para>
/// <para>
/// <b>There was one shape because there is one line.</b> A way in that is not the way out is two shapes to
/// solve, two landings to check against the lane and two answers that can disagree about whether a bay is
/// usable at all; the same line travelled the other way is a shape that lands on the lane by construction
/// — it started there — and a bay that can be driven into can by definition be driven out of.
/// </para>
/// <para>
/// <b>Which way round the car ends up standing is a different shape, not a different traversal</b>
/// (GEN-4j). Nose-first, the axle comes up the lane and turns in; backed in, the car has driven past the
/// bay first and the axle travels back down the lane before it turns. Both are this shape — the second is
/// asked for with the lane's direction reversed and the bay's axle at the deep end of the space — and each
/// of the two is driven forwards one way round and in reverse the other.
/// </para>
/// <para>
/// <b>It is drawn for the rear axle</b>, like every other line in this engine, and it is four pieces: a
/// straight along the lane, a swing away from the bay, the turn into it, and the straight that ends in the
/// bay. The swing is the piece a driver makes without thinking and the arithmetic cannot do without: a
/// quarter turn of radius <c>R</c> moves the axle <c>R</c> sideways, so a bay standing nearer its lane than
/// that is one no single arc reaches — swinging <c>φ</c> the other way first brings the sideways travel down
/// to <c>R(2cos φ − 1)</c>, which is what lets a car turn into a bay off the lane beside it rather than only
/// off the far one.
/// </para>
/// <para>
/// <b>The lane is treated as straight over the template's own length.</b> A template is a dozen metres of a
/// road whose bends are laid at a hundred and more, and the alternative is solving a pose against an arc
/// chain to place a manoeuvre that ends in a four-metre-wide bay.
/// </para>
/// </remarks>
internal static class BayTemplate
{
    /// <summary>
    /// How square to the lane a bay has to stand before this template describes it. Below it the bay is
    /// parallel to the kerb, which is a different manoeuvre and not one this engine lays.
    /// </summary>
    const float SquareEnoughRad = 30f * MathF.PI / 180f;

    /// <summary>
    /// Whether a turn of this size is the shape here rather than a slide along a kerb — asked by whoever
    /// lays a bay's own ways as well, so the bar is stated once.
    /// </summary>
    public static bool SquareEnough(float turnRad) =>
        MathF.Abs(turnRad) >= SquareEnoughRad && MathF.Abs(turnRad) <= MathF.PI - SquareEnoughRad;

    /// <summary>
    /// <b>Where the rear axle of a car standing in a bay is</b>: square in it and in the middle of it
    /// (GEN-4i), read back from the middle of the body, because the axle is the point every line is drawn
    /// for.
    /// </summary>
    /// <remarks>
    /// <b>The body stands in the same place either way round and the axle does not</b> (GEN-4j). Nose in,
    /// the axle is the wheelbase's half behind the middle of the space; backed in, it is that far past it,
    /// at the deep end — which is why a way to a backed-in car runs a metre further into the bay than a way
    /// to one that drove in.
    /// </remarks>
    public static Vector2 RearAxleOfBayM(
        float centreAheadOfAxleM, Vector2 bayCentreM, float bayHeadingRad, bool noseIn) =>
        bayCentreM + Heading.Unit(bayHeadingRad) * (noseIn ? -centreAheadOfAxleM : centreAheadOfAxleM);

    /// <summary>Which way the car itself points standing in a bay: into it, or back out of it.</summary>
    public static float StandingHeadingRad(float bayHeadingRad, bool noseIn) =>
        noseIn ? bayHeadingRad : bayHeadingRad + MathF.PI;

    /// <summary>And the same read off a car that is already standing there.</summary>
    public static bool StandsNoseIn(float bayHeadingRad, float carHeadingRad) =>
        Vector2.Dot(Heading.Unit(bayHeadingRad), Heading.Unit(carHeadingRad)) >= 0f;
}
