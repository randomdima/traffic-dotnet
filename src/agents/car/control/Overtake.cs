using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.Agents.Car.Control;

/// <summary>
/// <b>A car getting past what stands in its lane</b> (CAR-46): the line it is driving, moved across onto the
/// lane beside for a stretch and back — measured, like everything a driver drives, at the rear axle and in the
/// line's own metres from the start of <see cref="Lane"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The line is still the town's</b> (CAR-15): what the pass adds is how far across it the rear axle is
/// aimed, and the lane beside is where that puts the car — through a junction as along a street.
/// </para>
/// <para>
/// <b>Each step across is one swing of the wheel and back</b>: u of the way into a step the axle is aimed
/// d·(u − sin 2πu ⁄ 2π) across, so the step leaves the line straight, bends hardest a quarter of the way in, the
/// other way hardest a quarter from the end, and joins the lane beside straight. Its bend never jumps, so the
/// rack is never asked to be in two places at once and the car is driven at speed along the whole of it.
/// </para>
/// <para>
/// <b>The two steps are drawn apart</b>, each no shorter than the car can drive at the pace it is drawn for
/// (<see cref="ShortestStepM"/>): the step out from the last place the body clears what it passes, fixed in the line's
/// metres when the pass is decided; the step back from as soon past what it passes as the body can come back, for the
/// pace the car has picked up by then. <b>Each is driven no faster than its own bend and the rack
/// allow</b> (<see cref="OutMps"/>, <see cref="BackMps"/>), and between them the car is on a straight, and pulls
/// away along it.
/// </para>
/// <para>
/// <b>Asked for, then begun</b>: a pass is laid a rebuild before the car moves over (TER-4c.6), so that two
/// asked for over the same ground on one tick are settled before either leaves its lane.
/// </para>
/// </remarks>
/// <param name="Lane">
/// The lane whose start its metres are measured from — the first of the car's chain, which a line re-laid from
/// anywhere else no longer is — or <see cref="NoLane"/>.
/// </param>
/// <param name="OutM">Where the step out begins.</param>
/// <param name="OutStepM">How much of the line the step out takes.</param>
/// <param name="BackM">Where the step back begins.</param>
/// <param name="BackStepM">And how much of it the step back takes.</param>
/// <param name="AsideM">How far across the lane beside stands, along the driver's right — negative on its left.</param>
/// <param name="OutMps">The most the step out may be driven at: what its bend and the rack allow.</param>
/// <param name="BackMps">And the step back.</param>
/// <param name="ClearsM">Where along the line what is being passed ends, which is where the ground the car comes back into begins.</param>
/// <param name="Begun">Whether the pass has been kept past the rebuild it was laid in, and the car is moving over.</param>
internal readonly record struct Overtake(
    int Lane, float OutM, float OutStepM, float BackM, float BackStepM, float AsideM, float OutMps, float BackMps,
    float ClearsM, bool Begun)
{
    public const int NoLane = -1;

    public static Overtake None => new(NoLane, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, false);

    public bool Any => Lane != NoLane;

    /// <summary>Where the step out is done and the car is on the lane beside.</summary>
    public float SteppedOutM => OutM + OutStepM;

    /// <summary>Where the car is back in its own lane and the pass is over.</summary>
    public float EndsM => BackM + BackStepM;

    /// <summary>
    /// <b>The shortest step <paramref name="asideM"/> across</b> whose bend is never more than
    /// <paramref name="mostBend"/> and never changes by more than <paramref name="bendPerM"/> a metre: a step of
    /// length L bends at most 2π·d ⁄ L² and changes its bend at most 4π²·d ⁄ L³ a metre, both where it leaves and
    /// joins a lane.
    /// </summary>
    /// <remarks>
    /// <b>Both are read off the step's rise and not its arc</b>, which bends a little less than that wherever it
    /// runs aslant — so a step is never bent past either, and is short of the shortest by that little.
    /// </remarks>
    public static float ShortestStepM(float asideM, float mostBend, float bendPerM)
    {
        var acrossM = MathF.Abs(asideM);
        var byTheBendM = MathF.Sqrt(Tau * acrossM / mostBend);
        var byTheRackM = MathF.Cbrt(Tau * Tau * acrossM / bendPerM);
        return MathF.Max(byTheBendM, byTheRackM);
    }

    /// <summary>The most a step of this length <paramref name="asideM"/> across bends: 2π·d ⁄ L².</summary>
    public static float MostBendOfAStep(float asideM, float stepM) => Tau * MathF.Abs(asideM) / (stepM * stepM);

    /// <summary>And the most its bend changes a metre: 4π²·d ⁄ L³.</summary>
    public static float MostBendChangeOfAStep(float asideM, float stepM) =>
        Tau * Tau * MathF.Abs(asideM) / (stepM * stepM * stepM);

    /// <summary>How far across its line the rear axle is aimed at this metre of it.</summary>
    public float AsideAtM(float atM)
    {
        if (atM <= OutM || atM >= EndsM) return 0f;
        if (atM < SteppedOutM) return AsideM * Rise((atM - OutM) / OutStepM);
        if (atM <= BackM) return AsideM;

        return AsideM * (1f - Rise((atM - BackM) / BackStepM));
    }

    /// <summary>And how steeply: metres across for a metre along.</summary>
    public float SlopeAtM(float atM)
    {
        if (atM <= OutM || atM >= EndsM) return 0f;
        if (atM < SteppedOutM) return AsideM / OutStepM * RiseSlope((atM - OutM) / OutStepM);
        if (atM <= BackM) return 0f;

        return -AsideM / BackStepM * RiseSlope((atM - BackM) / BackStepM);
    }

    /// <summary>And how that slope is changing, a metre along.</summary>
    public float TurnAtM(float atM)
    {
        if (atM <= OutM || atM >= EndsM) return 0f;
        if (atM < SteppedOutM) return TurnOfAStep(atM - OutM, OutStepM);
        if (atM <= BackM) return 0f;

        return -TurnOfAStep(atM - BackM, BackStepM);
    }

    /// <summary>
    /// <b>The most the pass may be driven at, at this metre of it</b>: its step out's figure until that step is
    /// done, its step back's from where that one begins, and nothing between — the straight alongside what it
    /// passes is the car's own to pull away along.
    /// </summary>
    public float MostMpsAtM(float atM)
    {
        if (atM < SteppedOutM) return OutMps;

        return atM >= BackM ? BackMps : float.PositiveInfinity;
    }

    /// <summary>
    /// <b>Where the rear axle stands at this metre of the line, and which way the car points</b> — the car the
    /// pass draws, which its ground is swept with and its wheel is corrected towards.
    /// </summary>
    public void PoseAtM(ReadOnlySpan<ArcSeg> line, float atM, out Vector2 axleM, out Vector2 forward)
    {
        var on = Spline.SampleAt(line, atM);
        var right = on.Right;
        var asideM = AsideAtM(atM);
        axleM = on.PositionM + (right * asideM);
        forward = Vector2.Normalize((on.Direction * (1f - (on.Curvature * asideM))) + (right * SlopeAtM(atM)));
    }

    /// <summary>
    /// <b>How the pass bends at this metre of the line</b>, the line's own bend under it included, signed as the
    /// line's: a line bending k with the axle aimed d across it, d′ and d″ its slope and turn, bends at
    /// ((1 − kd)²k + (1 − kd)d″ + 2kd′²) ⁄ ((1 − kd)² + d′²)^1.5.
    /// </summary>
    public float BendAtM(ReadOnlySpan<ArcSeg> line, float atM)
    {
        var lineBend = Spline.SampleAt(line, atM).Curvature;
        var slope = SlopeAtM(atM);
        var along = 1f - (lineBend * AsideAtM(atM));
        var alongSquared = along * along;
        var bent = (alongSquared * lineBend) + (along * TurnAtM(atM)) + (2f * lineBend * slope * slope);
        var stretch = alongSquared + (slope * slope);
        return bent / (stretch * MathF.Sqrt(stretch));
    }

    /// <summary>
    /// The same pass measured from the start of the next lane of the chain, which begins
    /// <paramref name="shiftM"/> along the line it was measured on — what a line shifted on by a lane leaves it.
    /// </summary>
    public Overtake From(int lane, float shiftM) =>
        this with { Lane = lane, OutM = OutM - shiftM, BackM = BackM - shiftM, ClearsM = ClearsM - shiftM };

    float TurnOfAStep(float intoM, float stepM) => AsideM / (stepM * stepM) * Tau * MathF.Sin(Tau * intoM / stepM);

    /// <summary>How much of the way across a step is <paramref name="share"/> of the way into it.</summary>
    public static float Rise(float share) => share - (MathF.Sin(Tau * share) / Tau);

    /// <summary>And how steeply, in the step's own across for its own length.</summary>
    public static float RiseSlope(float share) => 1f - MathF.Cos(Tau * share);

    const float Tau = MathF.PI * 2f;
}
