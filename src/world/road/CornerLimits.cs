using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>The speed each arc of a line may be entered at</b> (S-2): the most a car may be doing where the arc begins
/// and still hold every corner from there to the end of the line, braking into each tighter one. Laid once with
/// the line, so a driver reads the corners ahead off the arc it is coming to rather than walking them every tick.
/// </summary>
/// <remarks>
/// <para>
/// <b>Carried as a length, so one line serves every car</b>: the entry speed squared over the grip the car plans
/// against. A corner and a stop both scale with that grip, so a car's own entry speed squared is its grip times
/// the figure here — exactly while its brakes can out-pull its tyres, and short of it where they cannot.
/// </para>
/// <para>
/// <b>A straight arc bounds nothing of its own</b>: what it carries is the braking into the next bend. The end of
/// the line bounds nothing here either, being a stop point of its own (S-2).
/// </para>
/// </remarks>
internal static class CornerLimits
{
    /// <summary>
    /// Every arc's entry figure, from the last arc back: its own corner, or the next arc's entry and a stop's worth
    /// of its own length, whichever is less.
    /// </summary>
    public static void Lay(ReadOnlySpan<ArcSeg> arcs, Span<float> entryM, SimConfig config)
    {
        var cornerShare = config.Driving.GripMargin;
        var brakingShare = config.Driving.BrakingMargin;
        var nextM = float.PositiveInfinity;
        for (var arc = arcs.Length - 1; arc >= 0; arc--)
        {
            nextM = MathF.Min(CornerM(arcs[arc].Curvature, cornerShare), nextM + (2f * brakingShare * arcs[arc].LengthM));
            entryM[arc] = nextM;
        }
    }

    /// <summary>
    /// <b>How far along the line the corners let anything past <paramref name="fromM"/> come to rest</b> (TER-4c.1):
    /// the first arc begun past it, entered at its entry figure, and a stop from there at the share of grip a stop is
    /// planned at. A car that will take a bend slowly can be at rest no further than the bend lets it, whatever it is
    /// doing on the approach.
    /// </summary>
    /// <remarks>
    /// <b>It only moves on as <paramref name="fromM"/> does</b>: an arc's entry figure holds every corner past it, so
    /// the rest one arc allows is never further than the next one's. And like the figures it is every car's alike,
    /// since the grip a car corners on and the grip it stops on cancel.
    /// </remarks>
    public static float RestToM(ReadOnlySpan<ArcSeg> arcs, ReadOnlySpan<float> entryM, float fromM, SimConfig config)
    {
        var startM = 0f;
        for (var arc = 0; arc < arcs.Length; arc++)
        {
            if (startM > fromM) return startM + (entryM[arc] / (2f * config.Driving.BrakingMargin));

            startM += arcs[arc].LengthM;
        }

        return float.PositiveInfinity;
    }

    /// <summary>What one arc bounds on its own: its corner speed squared over the grip, which is the share of grip a corner is planned at over its curvature.</summary>
    static float CornerM(float curvature, float cornerShare)
    {
        var bend = MathF.Abs(curvature);
        return bend < StraightPerM ? float.PositiveInfinity : cornerShare / bend;
    }

    /// <summary>A curvature below which an arc is a straight, the same one the follower's own corner term reads (<c>CarFollower.CornerMps</c>).</summary>
    const float StraightPerM = 1e-4f;
}
