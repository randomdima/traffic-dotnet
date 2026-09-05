using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>The ground one body covers, as the box it stands in</b> — half its length along its own heading, half
/// its width across that, and where that heading points. It is the asking half of <see cref="WayUnder"/>:
/// how far the body reaches <em>along</em> a way and how far <em>across</em> it are that box read against
/// the way's own line.
/// </summary>
/// <remarks>
/// <para>
/// <b>The heading is the whole of why this is a type.</b> A body askew of the way it is on reaches its own
/// length across that way's line and its own width along it, and the two swap over as it turns — so a
/// single radius is wrong on both axes at once. Read at the flank it leaves a car lying broadside off the
/// lane it is lying across; read at the half-length it writes a car's length onto a lane it covers the
/// width of.
/// </para>
/// <para>
/// <b>A box and not an outline</b>, which is the conservative way round and the only one a claim can carry:
/// a stretch is an interval of one way's arclength (<see cref="LaneClaim"/>), so what is laid is the span the
/// body's corners project onto that line, and the ground inside the box the body does not fill is ground
/// nobody else was going to get past it anyway.
/// </para>
/// <para>
/// <b>What it projects is the part of the box on the way and not the whole of it</b>
/// (<see cref="CoversOn"/>). The shadow of an angled body reaches the length of the body down every way it
/// touches, however little of it is actually on any one of them.
/// </para>
/// </remarks>
/// <param name="Forward">
/// Which way the body faces, or <see cref="Vector2.Zero"/> where it has no heading to speak of — a person is
/// a circle and reads its radius whichever way it is asked (<see cref="Round"/>).
/// </param>
internal readonly record struct BodyFootprint(float HalfLengthM, float FlankM, Vector2 Forward)
{
    /// <summary>A body with no heading: a circle, whose reach is its radius on every axis.</summary>
    public static BodyFootprint Round(float radiusM) => new(radiusM, radiusM, Vector2.Zero);

    /// <summary>
    /// <b>How far this body reaches along a way running <paramref name="alongUnit"/>, and how far across
    /// it</b> — the box projected onto the way's own line and onto the square of it.
    /// </summary>
    public void ReachOn(Vector2 alongUnit, out float alongM, out float acrossM)
    {
        if (Forward == Vector2.Zero)
        {
            alongM = HalfLengthM;
            acrossM = HalfLengthM;
            return;
        }

        var along = MathF.Abs(Vector2.Dot(Forward, alongUnit));
        var across = MathF.Abs(Vector2.Dot(Forward, Heading.RightOf(alongUnit)));

        alongM = (HalfLengthM * along) + (FlankM * across);
        acrossM = (HalfLengthM * across) + (FlankM * along);
    }

    /// <summary>
    /// <b>How much of a way's own line this body covers</b>, as the two ends of that run either side of
    /// <paramref name="offsetM"/>'s own foot on the line — and <b>false where the box is nowhere inside the
    /// band</b>.
    /// </summary>
    /// <remarks>
    /// <b>The box clipped to the band, and never the box's whole shadow</b> (TER-4c.2). What a body covers of
    /// a way is the part of it that is <em>on</em> that way, and for anything standing at an angle the two
    /// are nothing like each other: a car turned across its lane clips the corner of the next one by a hand's
    /// breadth and casts four metres of shadow down it. Taken as the shadow, that car claimed its own length
    /// of a lane it had a wing mirror in.
    /// </remarks>
    /// <param name="offsetM">Where the body's middle stands from the point on the line the reading is taken at.</param>
    /// <param name="halfBandM">Half the width of the way, which is how far either side of the line it reaches.</param>
    public bool CoversOn(
        Vector2 alongUnit, Vector2 offsetM, float halfBandM, out float backM, out float aheadM)
    {
        // The box in the way's own frame: along the line, and across it. A body with no heading is a square
        // to the line it is read against, which is the same reading <see cref="ReachOn"/> gives it.
        var acrossUnit = Heading.RightOf(alongUnit);
        var middle = new Vector2(Vector2.Dot(offsetM, alongUnit), Vector2.Dot(offsetM, acrossUnit));
        var nose = Forward == Vector2.Zero
            ? Vector2.UnitX
            : new Vector2(Vector2.Dot(Forward, alongUnit), Vector2.Dot(Forward, acrossUnit));
        var flank = new Vector2(-nose.Y, nose.X);

        var ahead = nose * HalfLengthM;
        var beside = flank * FlankM;
        var offside = middle + ahead + beside;
        var nearside = middle + ahead - beside;
        var tailNearside = middle - ahead - beside;
        var tailOffside = middle - ahead + beside;

        backM = float.PositiveInfinity;
        aheadM = float.NegativeInfinity;

        // Every edge of the box, clipped: the furthest and nearest metres of a convex shape cut by a band
        // stand on its boundary, so the four edges are the whole of where they can be.
        Cut(offside, nearside, halfBandM, ref backM, ref aheadM);
        Cut(nearside, tailNearside, halfBandM, ref backM, ref aheadM);
        Cut(tailNearside, tailOffside, halfBandM, ref backM, ref aheadM);
        Cut(tailOffside, offside, halfBandM, ref backM, ref aheadM);
        return backM <= aheadM;
    }

    /// <summary>One edge of the box against the band, widening the run by whatever length of it survives.</summary>
    static void Cut(Vector2 fromM, Vector2 toM, float halfBandM, ref float backM, ref float aheadM)
    {
        var acrossM = toM.Y - fromM.Y;
        var entersAt = 0f;
        var leavesAt = 1f;
        if (!Before(acrossM, halfBandM - fromM.Y, ref entersAt, ref leavesAt)) return;
        if (!Before(-acrossM, halfBandM + fromM.Y, ref entersAt, ref leavesAt)) return;

        var runM = toM.X - fromM.X;
        var oneM = fromM.X + (entersAt * runM);
        var otherM = fromM.X + (leavesAt * runM);
        backM = MathF.Min(backM, MathF.Min(oneM, otherM));
        aheadM = MathF.Max(aheadM, MathF.Max(oneM, otherM));
    }

    /// <summary>
    /// One edge of the band as a bound on how much of a segment is inside it — <c>rate * t &lt;= reach</c>,
    /// narrowed onto the run so far. False where nothing of the segment is left.
    /// </summary>
    static bool Before(float rate, float reachM, ref float entersAt, ref float leavesAt)
    {
        if (rate == 0f) return reachM >= 0f;

        var at = reachM / rate;
        if (rate > 0f) leavesAt = MathF.Min(leavesAt, at);
        else entersAt = MathF.Max(entersAt, at);

        return entersAt <= leavesAt;
    }
}
