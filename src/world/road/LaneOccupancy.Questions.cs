namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>The questions the reservations are asked</b>: what body is in front on one way, and everything on one
/// way for whoever draws or measures it — every one of them a walk of one way's own lists, and none of them a
/// decision (SIM-7). <b>A reader only ever reads the way it is on</b> (TER-4c.5): whatever shares ground with
/// that way has already been settled onto it by the marks.
/// </summary>
internal sealed partial class LaneOccupancy
{
    /// <summary>
    /// <b>The nearest body in front</b>: of the bodies on one way that reach past <paramref name="fromM"/>
    /// and begin before <paramref name="untilM"/>, the one with the least near edge.
    /// </summary>
    /// <remarks>
    /// A body the asker is already inside answers at its own near edge rather than being skipped — a car
    /// inside somebody else is a contact and not a gap. <b>The asker's own body is never an answer</b>, and an
    /// occupant is always named with its roster.
    /// </remarks>
    public bool AheadBody(
        int way, float fromM, float untilM, int excluding, out LaneClaim found,
        LaneRoster excludingOf = LaneRoster.Driving)
    {
        for (var at = _bodies[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var body = ref _slots[at];
            if (body.FromM > untilM) break;
            if ((body.Occupant == excluding && body.Of == excludingOf) || body.ToM <= fromM) continue;

            found = body;
            return true;
        }

        found = LaneClaim.Nothing;
        return false;
    }

    /// <summary>Whether any body other than the asker's stands over a stretch of one way.</summary>
    public bool AnyBodyOver(int way, float fromM, float toM, int excluding, LaneRoster excludingOf) =>
        AheadBody(way, fromM, toM, excluding, out _, excludingOf);

    /// <summary>
    /// <b>How far along one way a holder's own planned ground reaches</b> from <paramref name="fromM"/>, in
    /// that way's metres — <paramref name="fromM"/> where it holds none of it there.
    /// </summary>
    public float PlannedToM(int way, float fromM, int occupant, LaneRoster of)
    {
        var reachM = fromM;
        for (var at = _planned[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var piece = ref _slots[at];
            if (piece.Occupant != occupant || piece.Of != of || piece.Linked) continue;
            if (piece.ToM <= reachM || piece.FromM > reachM) continue;

            reachM = piece.ToM;
        }

        return reachM;
    }

    /// <summary>
    /// <b>Everything on one way</b>, bodies first and then planned ground, each nearest first — for a test, an
    /// overlay and an instrument, and nothing on the hot path.
    /// </summary>
    public int CopyTo(int way, Span<LaneClaim> into)
    {
        var written = CopyBodiesTo(way, into);
        return written + CopyPlannedTo(way, into[written..]);
    }

    /// <summary>The bodies on one way, nearest first.</summary>
    public int CopyBodiesTo(int way, Span<LaneClaim> into) => CopyList(_bodies, way, into);

    /// <summary>The planned ground on one way, nearest first.</summary>
    public int CopyPlannedTo(int way, Span<LaneClaim> into) => CopyList(_planned, way, into);

    int CopyList(int[] heads, int way, Span<LaneClaim> into)
    {
        var written = 0;
        for (var at = heads[way]; at != NoSlot && written < into.Length; at = _next[at]) into[written++] = _slots[at];

        return written;
    }
}
