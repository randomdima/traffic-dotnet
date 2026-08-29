using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.Agents.Car.Control;

/// <summary>A template that was laid: how many arcs it took, how far it runs, and the pose it ends at.</summary>
internal readonly record struct BayLine(int ArcCount, float LengthM, Vector2 EndM, float EndHeadingRad)
{
    public bool Any => ArcCount > 0;
}

/// <summary>
/// <b>The one parking shape</b>: the line between a pose on the lane and the pose in the bay, in the
/// direction the rear axle travels. One end of it is the lane and the other is the bay, and which gear
/// the car is in while it drives it is the caller's (GEN-4f).
/// </summary>
/// <remarks>
/// <para>
/// <b>There is one shape because there is one line.</b> A way in that is not the way out is two shapes to
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
    /// <summary>A straight, a swing, the turn and the run in: the most arcs the shape ever takes.</summary>
    public const int MostArcs = 4;

    /// <summary>
    /// How square to the lane a bay has to stand before this template describes it. Below it the bay is
    /// parallel to the kerb, which is a different manoeuvre and not one this engine lays.
    /// </summary>
    const float SquareEnoughRad = 30f * MathF.PI / 180f;

    /// <summary>Below these a piece is not worth writing: a millimetre of straight, and a hundredth of a degree of turn.</summary>
    const float ShortestPieceM = 1e-3f;

    const float ShortestTurnRad = 1e-4f;

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
    public static Vector2 RearAxleOfBayM(in CarBuild car, Vector2 bayCentreM, float bayHeadingRad, bool noseIn) =>
        bayCentreM + Heading.Unit(bayHeadingRad) * (noseIn ? -car.CentreAheadOfAxleM : car.CentreAheadOfAxleM);

    /// <summary>Which way the car itself points standing in a bay: into it, or back out of it.</summary>
    public static float StandingHeadingRad(float bayHeadingRad, bool noseIn) =>
        noseIn ? bayHeadingRad : bayHeadingRad + MathF.PI;

    /// <summary>And the same read off a car that is already standing there.</summary>
    public static bool StandsNoseIn(float bayHeadingRad, float carHeadingRad) =>
        Vector2.Dot(Heading.Unit(bayHeadingRad), Heading.Unit(carHeadingRad)) >= 0f;

    /// <summary>
    /// <b>The shape between two poses</b>, given in the direction the rear axle travels — which forwards is
    /// the way the car points and reversing is the way it does not.
    /// </summary>
    /// <remarks>
    /// It refuses rather than approximates. A negative run-in is a car already past the place the turn
    /// starts, and a swing past a quarter turn is a car aiming away from the road rather than lining up on
    /// the bay; both are answered by driving round and coming back, never by a line no car can hold.
    /// </remarks>
    /// <param name="fromTravelRad">The way the axle is travelling where the shape starts.</param>
    /// <param name="toTravelRad">And where it ends — for a way into a bay, the bay's own bearing.</param>
    /// <param name="runsOnBeforeTurningM">
    /// How much of the shape is the straight it opens with, which is ground it covers without leaving the
    /// line it started on. Whoever wants the shape and not the approach to it lays again from that far on.
    /// </param>
    public static BayLine TryLay(
        SimConfig config, in CarBuild car, Vector2 fromAxleM, float fromTravelRad, Vector2 toAxleM,
        float toTravelRad, Span<ArcSeg> into, out float runsOnBeforeTurningM)
    {
        runsOnBeforeTurningM = 0f;

        // <b>This car's own circle</b> (CAR-11): a van needs more street to swing into a space than a
        // hatchback does, and a shape drawn at the nominal car's radius is one the van cannot hold —
        // which is a car that ends up across the aisle rather than in the bay.
        var radiusM = car.ParkingTemplateRadiusM;
        var from = Heading.Unit(fromTravelRad);
        var turnRad = SignedTurnRad(from, Heading.Unit(toTravelRad));
        if (!SquareEnough(turnRad)) return default;

        // The basis the template is solved in: along the approach, and to the side the turn goes.
        var side = Rotate(from, turnRad >= 0f ? MathF.PI * 0.5f : -MathF.PI * 0.5f);
        var offsetM = toAxleM - fromAxleM;
        var alongM = Vector2.Dot(offsetM, from);
        var acrossM = Vector2.Dot(offsetM, side);

        // Cosine is even, so the turn's own sign is nothing to it and both come off the one reduction.
        var (sin, cos) = MathF.SinCos(MathF.Abs(turnRad));

        // <b>Every template ends on a straight</b>, because one that ends on an arc ends with the car still
        // turning and parks it out of square — so the straight is what the shape is solved around and not
        // what is left over once the arcs have had their way.
        var settlesM = car.ParkingStraightensUpM;
        var runOutM = (acrossM - (radiusM * (1f - cos))) / sin;
        var swingRad = 0f;

        if (runOutM < settlesM)
        {
            // Not enough width for one arc and the straight after it, so the swing away buys the rest:
            // R(2cos φ − 1 − cos θ) is what the pair of arcs travels sideways, and this is that read for φ.
            var cosSwing = (((acrossM - (settlesM * sin)) / radiusM) + 1f + cos) * 0.5f;
            if (cosSwing < 0f) return default;

            swingRad = MathF.Acos(MathF.Min(cosSwing, 1f));
            runOutM = settlesM;
        }

        var runInM = alongM - (radiusM * ((2f * MathF.Sin(swingRad)) + sin)) - (runOutM * cos);
        if (runInM < 0f) return default;

        runsOnBeforeTurningM = runInM;
        return Lay(fromAxleM, fromTravelRad, runInM, radiusM, swingRad, turnRad, runOutM, into);
    }

    /// <summary>The four pieces, in the order they are travelled, skipping the ones with nothing in them.</summary>
    static BayLine Lay(
        Vector2 fromM, float travelRad, float runInM, float radiusM, float swingRad, float turnRad,
        float runOutM, Span<ArcSeg> into)
    {
        var written = 0;
        var atM = fromM;
        var atRad = travelRad;
        var lengthM = 0f;

        written = Straight(runInM, into, written, ref atM, ref atRad, ref lengthM);

        var sign = turnRad >= 0f ? 1f : -1f;
        written = Turn(-sign * swingRad, radiusM, into, written, ref atM, ref atRad, ref lengthM);
        written = Turn(sign * (MathF.Abs(turnRad) + swingRad), radiusM, into, written, ref atM, ref atRad, ref lengthM);

        written = Straight(runOutM, into, written, ref atM, ref atRad, ref lengthM);

        return new BayLine(written, lengthM, atM, atRad);
    }

    static int Straight(
        float runM, Span<ArcSeg> into, int written, ref Vector2 atM, ref float atRad, ref float lengthM)
    {
        if (runM <= ShortestPieceM) return written;

        into[written] = new ArcSeg(atM, atRad, runM, 0f);
        atM = into[written].EndM;
        lengthM += runM;
        return written + 1;
    }

    static int Turn(
        float byRad, float radiusM, Span<ArcSeg> into, int written, ref Vector2 atM, ref float atRad,
        ref float lengthM)
    {
        if (MathF.Abs(byRad) <= ShortestTurnRad) return written;

        var runM = MathF.Abs(byRad) * radiusM;
        into[written] = new ArcSeg(atM, atRad, runM, (byRad >= 0f ? 1f : -1f) / radiusM);
        atM = into[written].EndM;
        atRad += byRad;
        lengthM += runM;
        return written + 1;
    }

    /// <summary>The turn from one direction to another, in (−π, π].</summary>
    public static float SignedTurnRad(Vector2 fromDirection, Vector2 toDirection) =>
        MathF.Atan2(Spline.Cross(fromDirection, toDirection), Vector2.Dot(fromDirection, toDirection));

    static Vector2 Rotate(Vector2 direction, float byRad)
    {
        var (sin, cos) = MathF.SinCos(byRad);
        return new Vector2(direction.X * cos - direction.Y * sin, direction.X * sin + direction.Y * cos);
    }
}
