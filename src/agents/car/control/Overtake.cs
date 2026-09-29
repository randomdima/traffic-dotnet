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
/// <b>Its length is the shortest the car can drive at <see cref="DriveMps"/></b> (<see cref="ShortestStepM"/>):
/// bent no tighter than the lock, or than the tyres hold at that speed, and its bend changing no faster than the
/// rack turns in the time the car rolls it.
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
/// <param name="BackM">Where the step back begins.</param>
/// <param name="AsideM">How far across the lane beside stands, along the driver's right — negative on its left.</param>
/// <param name="StepM">How much of the line one step takes.</param>
/// <param name="DriveMps">The speed its steps are drawn for, and the most they are driven at.</param>
/// <param name="ClearsM">Where along the line what is being passed ends, which is where the ground the car comes back into begins.</param>
/// <param name="Begun">Whether the pass has been kept past the rebuild it was laid in, and the car is moving over.</param>
internal readonly record struct Overtake(
    int Lane, float OutM, float BackM, float AsideM, float StepM, float DriveMps, float ClearsM, bool Begun)
{
    public const int NoLane = -1;

    public static Overtake None => new(NoLane, 0f, 0f, 0f, 0f, 0f, 0f, false);

    public bool Any => Lane != NoLane;

    /// <summary>Where the car is back in its own lane and the pass is over.</summary>
    public float EndsM => BackM + StepM;

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

    /// <summary>How far across its line the rear axle is aimed at this metre of it.</summary>
    public float AsideAtM(float atM)
    {
        if (atM <= OutM || atM >= EndsM) return 0f;
        if (atM < OutM + StepM) return AcrossAStep(atM - OutM);
        if (atM <= BackM) return AsideM;

        return AsideM - AcrossAStep(atM - BackM);
    }

    /// <summary>And how steeply: metres across for a metre along.</summary>
    public float SlopeAtM(float atM)
    {
        if (atM <= OutM || atM >= EndsM) return 0f;
        if (atM < OutM + StepM) return SlopeOfAStep(atM - OutM);
        if (atM <= BackM) return 0f;

        return -SlopeOfAStep(atM - BackM);
    }

    /// <summary>And how that slope is changing, a metre along.</summary>
    public float TurnAtM(float atM)
    {
        if (atM <= OutM || atM >= EndsM) return 0f;
        if (atM < OutM + StepM) return TurnOfAStep(atM - OutM);
        if (atM <= BackM) return 0f;

        return -TurnOfAStep(atM - BackM);
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

    /// <summary>How far across a step the axle is this far into it.</summary>
    float AcrossAStep(float intoM) => AsideM * Rise(intoM / StepM);

    float SlopeOfAStep(float intoM) => AsideM / StepM * RiseSlope(intoM / StepM);

    float TurnOfAStep(float intoM) => AsideM / (StepM * StepM) * Tau * MathF.Sin(Tau * intoM / StepM);

    /// <summary>How much of the way across a step is <paramref name="share"/> of the way into it.</summary>
    public static float Rise(float share) => share - (MathF.Sin(Tau * share) / Tau);

    /// <summary>And how steeply, in the step's own across for its own length.</summary>
    public static float RiseSlope(float share) => 1f - MathF.Cos(Tau * share);

    const float Tau = MathF.PI * 2f;
}
