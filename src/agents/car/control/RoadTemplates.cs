using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.Agents.Car.Control;

/// <summary>
/// The one template a car drives <em>on the road</em> rather than into a bay: the <b>swerve</b> that
/// takes it round something standing in its lane (`E-4`). <see cref="BayTemplate"/> lays the ones that go
/// into a bay, which is where a car that has to come back the other way turns (GEN-4l).
/// </summary>
/// <remarks>
/// <para>
/// <b>It is drawn for the rear axle</b> and ends on a stated line rather than at a stated point: the line
/// it left. Where along that line the shape happens to finish is whatever the arcs need, and the
/// manoeuvre hands back to `P-4`, which takes the lane under the car and picks the route up from there.
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
