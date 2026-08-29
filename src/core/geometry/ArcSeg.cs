using System.Numerics;

namespace TrafficSimulation.Core.Geometry;

/// <summary>
/// One constant-curvature piece of a road's centreline. A chain of these is a road's whole shape; a
/// straight is the same record at zero curvature. Curvature is signed and positive turns to the
/// driver's right. Twenty bytes, laid out as the <c>.town</c> file lays them, so a run of them is read
/// straight out of the town's bytes rather than field by field, and the direction below.
/// </summary>
internal readonly record struct ArcSeg(Vector2 StartM, float HeadingRad, float LengthM, float Curvature)
{
    /// <summary>Below this the arc's centre is further away than any town is wide, and it is a straight.</summary>
    const float StraightCurvature = 1e-6f;

    /// <summary>
    /// <b><see cref="HeadingRad"/> as the direction it is</b>, reduced once when the piece is made rather
    /// than every time it is walked. A town's pieces are made once and projected onto by every scan of
    /// every tick, and this angle is a constant of the piece — so the reduction belongs here and the
    /// walks get it free (<see cref="Spline.ProjectM"/>, <see cref="PointAtM"/>).
    /// </summary>
    /// <remarks>
    /// <b>It is <see cref="HeadingRad"/>'s and nothing else's</b>: <c>with { StartM = … }</c> carries it
    /// across correctly because a piece moved is a piece pointing the same way, and no <c>with</c> may
    /// change the heading without it.
    /// </remarks>
    public Vector2 StartUnit { get; private init; } = Heading.Unit(HeadingRad);

    public float HeadingAtRad(float distanceM) => HeadingRad + Curvature * distanceM;

    public Vector2 EndM => PointAtM(LengthM);

    /// <summary>
    /// The circle through <see cref="StartM"/> tangent to <see cref="HeadingRad"/>, walked by arc
    /// length, as the chord <c>L·sinc(kL/2)</c> laid along the heading half way round the turn.
    /// </summary>
    /// <remarks>
    /// Not the textbook difference-of-tangent-normals over curvature: a road's bends are a huge radius
    /// and a tiny curvature, so that form differences two sines agreeing to five figures and multiplies
    /// the remainder by ten thousand — 10 cm of drift came out of a join drawn that way. The chord
    /// cancels nothing and needs no separate case for a straight.
    /// </remarks>
    /// <remarks>
    /// <b>A piece that turns through nothing is its own direction laid out</b>: the chord is the whole
    /// distance and the half turn is zero, so <see cref="StartUnit"/> is the answer and the angle is
    /// never reduced. Most of a town's pieces are straights and most of a tick's samples land on one.
    /// </remarks>
    public Vector2 PointAtM(float distanceM)
    {
        var halfTurnRad = Curvature * distanceM * 0.5f;
        if (halfTurnRad == 0f) return StartM + distanceM * StartUnit;

        var chordM = distanceM * Sinc(halfTurnRad);
        return StartM + chordM * Heading.Unit(HeadingRad + halfTurnRad);
    }

    /// <summary>
    /// sin x ⁄ x, by series over the half-turn a road arc actually subtends and by the library beyond it.
    /// </summary>
    /// <remarks>
    /// The series is the reason this is not one <c>MathF.Sin</c>: a straight subtends nothing and a
    /// road's bends subtend a fraction of a degree, so nearly every sample taken in a tick lands under
    /// <see cref="SeriesLimitRad"/> — and the call into libm it replaces costs four times what the six
    /// multiplies do. Above the limit the library answers, because the series is only accurate where it
    /// is truncated tightly.
    /// </remarks>
    static float Sinc(float x)
    {
        if (MathF.Abs(x) >= SeriesLimitRad) return MathF.Sin(x) / x;

        // 1 − x²/6 + x⁴/120 − x⁶/5040, whose first dropped term at the limit is under a float's own
        // resolution — so inside the limit this is the answer and not an approximation to it.
        var square = x * x;
        return 1f - square * (1f / 6f) + square * square * (1f / 120f) - square * square * square * (1f / 5040f);
    }

    /// <summary>Where the series stops being exact to a float and the library takes over.</summary>
    const float SeriesLimitRad = 0.6f;
}
