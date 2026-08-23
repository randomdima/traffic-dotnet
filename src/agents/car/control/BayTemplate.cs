using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.Agents.Car.Control;

/// <summary>A template that was laid: how many arcs it took, how far it runs, and the pose it ends at.</summary>
internal readonly record struct BayLine(int ArcCount, float LengthM, Vector2 EndM, float EndHeadingRad)
{
    public bool Any => ArcCount > 0;
}

/// <summary>
/// The two parking templates, as geometry and nothing else: <b>forward-in</b>, which puts a car in a
/// bay, and the <b>reverse-out</b> that is its mirror, which is how a car nose-in in a bay gets back
/// onto the lane.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both are drawn for the rear axle</b>, like every other line in this engine, and both are one
/// shape: a straight run, a fillet arc at the car's own turning circle, and a run into the bay. The
/// reverse-out is that shape with its ends swapped and its straight into the bay spent — the turn is
/// what rotates the car, and a car that backs straight out is a car standing across the lane it backed
/// into.
/// </para>
/// <para>
/// <b>Which lane a bay is entered from is decided by the arithmetic and not by preference.</b> The
/// fillet covers the car's turning radius of the depth between the lane and the bay, so a bay whose
/// near lane stands closer than that radius cannot be driven into from it at all — which is what
/// <i>forward-in and always from the far lane</i> means, and it is why this returns a
/// refusal rather than a tighter arc.
/// </para>
/// <para>
/// <b>The lane is treated as straight over the template's own length.</b> A template is a dozen metres
/// of a road whose bends are laid at a hundred and more, and the alternative is solving a pose against
/// an arc chain to place a manoeuvre that ends in a four-metre-wide bay.
/// </para>
/// </remarks>
internal static class BayTemplate
{
    /// <summary>A straight, a fillet and a run in: the most arcs either template ever takes.</summary>
    public const int MostArcs = 3;

    /// <summary>
    /// How square to the lane a bay has to stand before these templates describe it. Below it the bay
    /// is parallel to the kerb, which is a different manoeuvre and not one this engine lays.
    /// </summary>
    const float SquareEnoughRad = 30f * MathF.PI / 180f;

    /// <summary>
    /// The pose a car in a bay stands at: the plan's own space pose, read at the rear axle because
    /// that is the point every line is drawn for.
    /// </summary>
    public static Vector2 RearAxleOfBayM(SimConfig config, Vector2 bayCentreM, float bayHeadingRad) =>
        bayCentreM - Heading.Unit(bayHeadingRad) * config.Car.WheelbaseM * 0.5f;

    /// <summary>
    /// <b>Forward-in</b>: from where the car actually stands on its approach lane, into the bay's own
    /// pose. Straight run-in → fillet at the template radius → the run into the bay.
    /// </summary>
    /// <remarks>
    /// It refuses rather than approximates. A negative run-in is a car already past the turn-in point,
    /// and a negative run into the bay is a bay nearer the lane than the car can turn — both are
    /// answered by driving round and coming back, never by a line no car can hold.
    /// </remarks>
    public static BayLine TryLayEntry(
        SimConfig config, Vector2 fromAxleM, float fromHeadingRad, Vector2 bayCentreM, float bayHeadingRad,
        Span<ArcSeg> into)
    {
        var radiusM = config.ParkingTemplateRadiusM;
        var from = Heading.Unit(fromHeadingRad);
        var bay = Heading.Unit(bayHeadingRad);
        var toAxleM = RearAxleOfBayM(config, bayCentreM, bayHeadingRad);

        var turnRad = SignedTurnRad(from, bay);
        if (MathF.Abs(turnRad) < SquareEnoughRad || MathF.Abs(turnRad) > MathF.PI - SquareEnoughRad) return default;

        // The basis the template is solved in: along the approach, and to the side the turn goes.
        var side = Rotate(from, turnRad >= 0f ? MathF.PI * 0.5f : -MathF.PI * 0.5f);
        var offsetM = toAxleM - fromAxleM;
        var alongM = Vector2.Dot(offsetM, from);
        var acrossM = Vector2.Dot(offsetM, side);

        // Cosine is even, so the turn's own sign is nothing to it and both come off the one reduction.
        var (sin, cos) = MathF.SinCos(MathF.Abs(turnRad));
        var runIntoBayM = (acrossM - radiusM * (1f - cos)) / sin;
        var runInM = alongM - radiusM * sin - runIntoBayM * cos;
        if (runIntoBayM < 0f || runInM < 0f) return default;

        return Lay(fromAxleM, fromHeadingRad, runInM, radiusM, turnRad, runIntoBayM, into);
    }

    /// <summary>
    /// <b>Reverse-out</b>: from the bay's own pose, backwards, to where the turn meets the lane. The
    /// chain is laid <em>in the direction the rear axle travels</em>, which while reversing is the way
    /// the car's nose is not pointing — so the follower steers against it and the gear is reverse.
    /// </summary>
    /// <param name="laneAtM">A point on the lane being backed onto, and <paramref name="laneDirection"/> the way it runs.</param>
    /// <param name="overshootM">
    /// How far past the lane's own line the turn ends, where the bay stands nearer to it than the
    /// template radius. It is what a car backing out of a tight bay does — a foot into the other half
    /// of the road, straightened out by the first metres of driving — and it is capped, not ignored.
    /// </param>
    public static BayLine TryLayExit(
        SimConfig config, Vector2 fromAxleM, float fromHeadingRad, Vector2 laneAtM, Vector2 laneDirection,
        float overshootM, Span<ArcSeg> into)
    {
        var radiusM = config.ParkingTemplateRadiusM;
        var backwards = -Heading.Unit(fromHeadingRad);

        // Reversing, the car ends up heading along the lane, so the rear axle ends up travelling
        // against it: the template's own end direction is the lane's reversed.
        var endTravel = -laneDirection;
        var turnRad = SignedTurnRad(backwards, endTravel);
        if (MathF.Abs(turnRad) < SquareEnoughRad || MathF.Abs(turnRad) > MathF.PI - SquareEnoughRad) return default;

        // Where the turn has to end is the lane's own line, so the run back is what puts the end of the
        // arc on it — the arc's own sideways reach counted in, since a bay off square to its kerb
        // spends some of the turn going along the lane rather than across it.
        var side = Rotate(backwards, turnRad >= 0f ? MathF.PI * 0.5f : -MathF.PI * 0.5f);
        var normal = new Vector2(-laneDirection.Y, laneDirection.X);
        if (Vector2.Dot(fromAxleM - laneAtM, normal) < 0f) normal = -normal;

        var towardsLane = Vector2.Dot(backwards, normal);
        if (towardsLane > -1e-3f) return default;

        var depthM = Vector2.Dot(fromAxleM - laneAtM, normal);
        var (sin, cos) = MathF.SinCos(MathF.Abs(turnRad));
        var runOutM =
            -(depthM + radiusM * (1f - cos) * Vector2.Dot(side, normal)) / towardsLane - radiusM * sin;
        if (runOutM < -overshootM) return default;

        return Lay(fromAxleM, MathF.Atan2(backwards.Y, backwards.X), MathF.Max(0f, runOutM), radiusM, turnRad, 0f, into);
    }

    /// <summary>The three pieces, in the order they are travelled, skipping the ones with no length in them.</summary>
    static BayLine Lay(
        Vector2 fromM, float headingRad, float runInM, float radiusM, float turnRad, float runOutM, Span<ArcSeg> into)
    {
        var written = 0;
        var atM = fromM;
        var atRad = headingRad;
        var lengthM = 0f;

        if (runInM > 1e-3f)
        {
            into[written] = new ArcSeg(atM, atRad, runInM, 0f);
            atM = into[written].EndM;
            lengthM += runInM;
            written++;
        }

        var arcM = MathF.Abs(turnRad) * radiusM;
        into[written] = new ArcSeg(atM, atRad, arcM, MathF.Sign(turnRad) / radiusM);
        atM = into[written].EndM;
        atRad += turnRad;
        lengthM += arcM;
        written++;

        if (runOutM > 1e-3f)
        {
            into[written] = new ArcSeg(atM, atRad, runOutM, 0f);
            atM = into[written].EndM;
            lengthM += runOutM;
            written++;
        }

        return new BayLine(written, lengthM, atM, atRad);
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
