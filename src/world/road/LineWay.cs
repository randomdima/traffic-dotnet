namespace TrafficSimulation.World.Road;

/// <summary>
/// One of the town's own ways under a stretch of one agent's line: which way, the metres of it the stretch
/// covers, and where the near end of that falls back on the line it came from.
/// </summary>
/// <remarks>
/// <see cref="LineFromM"/> is what makes the trip back cheap. A line's metres and a way's metres share no
/// origin — the assembler trims each lane by the setbacks its joins were taken at — so a distance read off
/// the index has to be carried home through the same offset it was carried out on.
/// </remarks>
internal readonly record struct LineWay(int Way, float FromM, float ToM, float LineFromM);

/// <summary>
/// <b>What one agent's plan was answered</b>, read against the reservations and not yet laid: where on its own
/// line it ends, the ground it keeps off what ended it, what that was, and on which way.
/// </summary>
/// <param name="CutLineM">Infinity where it can have all it asked for.</param>
/// <param name="CutBy"><see cref="LaneClaim.Nothing"/> where nothing ended it.</param>
/// <param name="CutOn">The way it was refused on, or <see cref="LaneOccupancy.NoHold"/>.</param>
/// <param name="CutAt">Which of the plan's pieces that was, or −1.</param>
/// <param name="CutWayM">
/// Where on that piece's own way it was refused — <b>the metre the answer was read at, and the one the piece is
/// laid to</b>. Carried to the line and back instead, it comes home a hair past where it was refused, and laid
/// there it takes that hair off whatever refused it: a light's hold, a secondary claim met at its very start,
/// went whole to a plan it had just refused (<see cref="LaneOccupancy.Take"/>).
/// </param>
internal readonly record struct PlanAnswer(
    float CutLineM, float MarginM, LaneClaim CutBy, int CutOn, int CutAt = -1, float CutWayM = float.PositiveInfinity)
{
    public static PlanAnswer Whole => new(float.PositiveInfinity, 0f, LaneClaim.Nothing, LaneOccupancy.NoHold);
}

/// <summary>
/// <b>A line's metres and a way's, carried one to the other</b> — the trips out and home every plan makes between
/// the line its holder drives or walks and the ways under it.
/// </summary>
internal static class LineWays
{
    /// <summary>One way written into a caller's span, as the count of them it now holds.</summary>
    public static int Written(Span<LineWay> into, in LineWay way)
    {
        if (into.Length == 0) return 0;

        into[0] = way;
        return 1;
    }

    public static bool Overlaps(float fromM, float toM, float leastM, float mostM, out float fromOut, out float toOut)
    {
        fromOut = MathF.Max(fromM, leastM);
        toOut = MathF.Min(toM, mostM);
        return toOut > fromOut;
    }

    /// <summary>
    /// Where a place on a line falls in the own metres of one of the ways under it, held to the stretch of
    /// that way the caller is laying.
    /// </summary>
    public static float OnTheWayM(in LineWay way, float lineM) =>
        Math.Clamp(way.FromM + (lineM - way.LineFromM), way.FromM, way.ToM);

    /// <summary>
    /// How far along the <paramref name="index"/>th piece of a plan its answer lets it be laid: the metre it was
    /// refused at on the piece it was refused on (<see cref="PlanAnswer.CutWayM"/>), and the answer carried
    /// over from the line on every piece before that.
    /// </summary>
    public static float LaidToM(in PlanAnswer answer, int index, in LineWay way) =>
        index == answer.CutAt ? answer.CutWayM : OnTheWayM(way, answer.CutLineM);

    /// <summary>
    /// The same trip home: where a place in one way's own metres falls on the line that ran over it. <b>The
    /// pair of <see cref="OnTheWayM"/></b>, so that an answer carried out and an answer carried back cannot
    /// use two offsets.
    /// </summary>
    public static float OnTheLineM(in LineWay way, float wayM) => way.LineFromM + (wayM - way.FromM);

    /// <summary>
    /// Where a metre of one of an agent's ways falls on its line or its walk, where the way is one of those given —
    /// never behind where the stretch of it given begins, and as far past its end as the metre is.
    /// </summary>
    public static bool OnTheLine(ReadOnlySpan<LineWay> ways, int on, float wayM, out float lineM)
    {
        foreach (ref readonly var way in ways)
        {
            if (way.Way != on) continue;

            lineM = OnTheLineM(way, MathF.Max(wayM, way.FromM));
            return true;
        }

        lineM = float.NaN;
        return false;
    }
}
