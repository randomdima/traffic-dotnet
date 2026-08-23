using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.Agents.Car.Control;

/// <summary>
/// The two templates a car drives <em>on the road</em> rather than into a bay: the <b>swerve</b> that
/// takes it round something standing in its lane (`E-4`), and the <b>counter-swing</b> that turns it
/// round inside a junction (`P-11`). <see cref="BayTemplate"/> lays the other two.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both are drawn for the rear axle</b> and both end on a stated line rather than at a stated point:
/// a swerve ends back on the line it left, a turn-around ends on the opposite lane's line. Where along
/// that line the shape happens to finish is whatever the arcs need, and the manoeuvre hands back to
/// `P-4`, which takes the lane under the car and picks the route up from there.
/// </para>
/// <para>
/// <b>The lateral part of a swerve is a function of distance and never of time</b> (§8 rule 2). It is
/// laid as geometry for exactly that reason: a line that moves on a clock arrives whether or not the car
/// did, and steers the car into the thing it was avoiding.
/// </para>
/// </remarks>
internal static class RoadTemplates
{
    /// <summary>Out, along, and back: four arcs and the straight between the two S-bends.</summary>
    public const int MostSwerveArcs = 5;

    /// <summary>The counter-swing is two arcs and nothing else — there is no straight in a turn-around.</summary>
    public const int MostTurnAroundArcs = 2;

    /// <summary>
    /// <b>The swerve</b>: an S out to <paramref name="offsetM"/> beside the line, a run past what is
    /// standing on it, and the mirror S back onto it. The car ends where it would have been, pointing the
    /// way it was pointing, with the obstruction behind it.
    /// </summary>
    /// <remarks>
    /// <b>Every piece of it carries the road's own bend underneath the S</b>
    /// (<paramref name="alongCurvature"/>), which is what makes it a manoeuvre and not a bid to leave the
    /// carriageway: an S drawn flat is a chord across whatever the road was doing, and fifty metres of chord
    /// on a forty-metre radius is ten metres off the road. The S's own bends are what is left over once that
    /// is taken out, so the shape's <em>relation</em> to the line — out by the offset, back onto it — is the
    /// same on a bend as on a straight, and a straight is the case where the road's bend is nothing.
    /// </remarks>
    /// <param name="offsetM">
    /// How far to one side the line goes, signed: positive turns the way <see cref="ArcSeg.Curvature"/>
    /// counts positive. It is refused past twice the template radius, which is the widest an S of two
    /// quarter-turns can reach.
    /// </param>
    /// <param name="passM">The straight between the two S-bends: what has to be got past, plus the room to be clear of it.</param>
    /// <param name="radiusM">
    /// What the four bends are drawn at. <b>It is the caller's because the shape is driven at the speed it
    /// was laid for</b>: the steering lock is the right radius for a car easing out of a bay and the wrong
    /// one for a car overtaking at road speed, which the profile's own corner term would then hold to
    /// walking pace for the whole manoeuvre.
    /// </param>
    /// <param name="alongCurvature">What the road under the car is doing, which every piece of the shape is drawn on top of.</param>
    public static BayLine TryLaySwerve(
        Vector2 fromAxleM, float fromHeadingRad, float offsetM, float passM, float radiusM, float alongCurvature,
        Span<ArcSeg> into)
    {
        var reachM = MathF.Abs(offsetM);
        if (reachM < 1e-3f || reachM > 2f * radiusM || !float.IsFinite(radiusM)) return default;
        if (!float.IsFinite(passM) || passM < 0f) return default;

        // Two quarter-turns of equal angle carry a line 2R(1−cos θ) sideways and leave it parallel to
        // where it started, which is what makes the shape composable with itself in reverse.
        var turnRad = MathF.Acos(Math.Clamp(1f - reachM / (2f * radiusM), -1f, 1f));
        var side = MathF.Sign(offsetM);
        var arcM = turnRad * radiusM;
        var outward = alongCurvature + (side / radiusM);
        var back = alongCurvature - (side / radiusM);

        var written = 0;
        var atM = fromAxleM;
        var atRad = fromHeadingRad;
        var lengthM = 0f;

        Bend(ref written, ref atM, ref atRad, ref lengthM, arcM, outward, into);
        Bend(ref written, ref atM, ref atRad, ref lengthM, arcM, back, into);
        if (passM > 1e-3f) Bend(ref written, ref atM, ref atRad, ref lengthM, passM, alongCurvature, into);
        Bend(ref written, ref atM, ref atRad, ref lengthM, arcM, back, into);
        Bend(ref written, ref atM, ref atRad, ref lengthM, arcM, outward, into);

        return new BayLine(written, lengthM, atM, atRad);
    }

    /// <summary>
    /// <b>The counter-swing</b>: the car turns first <em>away</em> from the lane it is heading for, by an
    /// angle, and then sweeps back through it. A plain half-circle needs a junction as wide as twice the
    /// turning radius; the counter-swing lands the same lane separation on minimum-radius arcs, and pays
    /// for it by reaching further along the arm.
    /// </summary>
    /// <remarks>
    /// <b>The reach along the arm is the constraint to check, not the lateral one.</b> Whether the shape
    /// fits is a question about the ground it is drawn over and is asked of the terrain, arc by arc, by
    /// whoever lays it — a junction wide enough is a fact about a town and not about a car.
    /// </remarks>
    /// <param name="ontoM">A point on the lane the car is turning onto, and <paramref name="ontoDirection"/> the way it runs.</param>
    public static BayLine TryLayTurnAround(
        SimConfig config, Vector2 fromAxleM, float fromHeadingRad, Vector2 ontoM, Vector2 ontoDirection,
        Span<ArcSeg> into)
    {
        var radiusM = config.ParkingTemplateRadiusM;
        var from = Heading.Unit(fromHeadingRad);

        // A turn-around reverses the direction of travel, so a lane running any other way is not one this
        // shape ends on and the caller has picked the wrong one.
        if (Vector2.Dot(from, ontoDirection) > -0.5f) return default;

        // The separation is measured across the line being left, not between two arbitrary points: what
        // the shape has to deliver is a lateral distance, and where along the arm it delivers it is free.
        var across = new Vector2(-from.Y, from.X);
        var offsetM = Vector2.Dot(ontoM - fromAxleM, across);
        var side = MathF.Sign(offsetM);
        var separationM = MathF.Abs(offsetM);
        if (side == 0 || separationM > 2f * radiusM) return default;

        // Both arcs are at the same radius; the counter-swing's angle is what fits the separation.
        var swingRad = MathF.Acos(Math.Clamp(separationM / (2f * radiusM), -1f, 1f));

        var written = 0;
        var atM = fromAxleM;
        var atRad = fromHeadingRad;
        var lengthM = 0f;

        Bend(ref written, ref atM, ref atRad, ref lengthM, swingRad * radiusM, -side / radiusM, into);
        Bend(ref written, ref atM, ref atRad, ref lengthM, (MathF.PI + swingRad) * radiusM, side / radiusM, into);

        return new BayLine(written, lengthM, atM, atRad);
    }

    /// <summary>One piece onto the end of a chain, carrying the pose forward. A zero curvature is a straight.</summary>
    static void Bend(
        ref int written, ref Vector2 atM, ref float atRad, ref float lengthM, float pieceM, float curvature,
        Span<ArcSeg> into)
    {
        into[written] = new ArcSeg(atM, atRad, pieceM, curvature);
        atM = into[written].EndM;
        atRad += curvature * pieceM;
        lengthM += pieceM;
        written++;
    }
}
