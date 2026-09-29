namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>A hold backing down a way</b> (TER-4c.7): the ground behind a car that has to back up for the room to step
/// out round what stands in its lane, asked for from its tail down against the way's own metres.
/// </summary>
/// <remarks>
/// <para>
/// <b>Answered and laid by the same comparison as every other hold</b> (<see cref="Beats"/>), read the other way
/// down the way: what it meets first is the highest metre of anything behind its holder, and how near the holder
/// is to that metre is how far below its tail the two meet.
/// </para>
/// <para>
/// <b>The one plan it takes that the comparison would keep is that of somebody queued behind it</b>
/// (<see cref="IsQueuedBehind"/>), over the ground that one could still stop short of: it runs up to the holder only
/// because the holder stands there, and weighed against it every car of a queue held the ground the car in front of
/// it would back into.
/// </para>
/// </remarks>
internal sealed partial class LaneOccupancy
{
    /// <summary>
    /// <b>How far back down one way a hold backing along it can be had</b> — read and never written: from
    /// <paramref name="tailM"/>, where its holder's body ends, down towards <paramref name="fromM"/>, stopped
    /// <paramref name="bodyKeptOffM"/> short of the far edge of the first body behind the holder and at the nearest
    /// metre another hold's claim keeps against it. <paramref name="fromM"/> where nothing stops it.
    /// </summary>
    /// <param name="ask">
    /// Who asks and at what rung. Where it stands, how near it is and what it can no longer stop short of are read
    /// off <paramref name="tailM"/> and <paramref name="committedFromM"/> and not off the ask.
    /// </param>
    /// <param name="committedFromM">
    /// Where the ground its holder can no longer stop short of ends, at or below <paramref name="tailM"/>.
    /// </param>
    /// <param name="cutBy">What stopped it, or <see cref="LaneClaim.Nothing"/>.</param>
    public float ReachBack(
        in PlannedAsk ask, int way, float fromM, float tailM, float committedFromM, float bodyKeptOffM,
        out LaneClaim cutBy)
    {
        var limitM = fromM;
        cutBy = LaneClaim.Nothing;
        for (var at = _bodies[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var body = ref _slots[at];
            if (body.FromM >= tailM) break;
            if (Owns(ask, body) || body.ToM + bodyKeptOffM <= limitM) continue;

            limitM = MathF.Min(tailM, body.ToM + bodyKeptOffM);
            cutBy = body;
        }

        for (var at = _planned[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var other = ref _slots[at];
            if (other.FromM >= tailM) break;

            var nearM = MathF.Min(tailM, other.ToM);
            if (nearM <= limitM || Owns(ask, other)) continue;

            if (IsQueuedBehind(ask, other))
            {
                nearM = MathF.Min(nearM, other.CommittedToM);
                if (nearM <= MathF.Max(other.FromM, limitM)) continue;
            }

            // Kept where the holder can no longer stop short of it, the rest of the meeting lies below that, where
            // the backing rung is weighed on its own.
            if (KeepsBacking(ask, way, other, MathF.Max(other.FromM, limitM), nearM, tailM, committedFromM))
            {
                var belowM = MathF.Min(nearM, committedFromM);
                if (belowM <= MathF.Max(other.FromM, limitM)
                    || KeepsBacking(ask, way, other, MathF.Max(other.FromM, limitM), belowM, tailM, committedFromM))
                {
                    continue;
                }

                nearM = belowM;
            }

            limitM = nearM;
            cutBy = other;
        }

        return limitM;
    }

    /// <summary>
    /// <b>A hold backing down one way laid</b> over <c>[fromM, tailM)</c>, as far as <see cref="ReachBack"/> said it
    /// could be had: the ground its holder can no longer stop short of from <paramref name="committedFromM"/> up, and
    /// the rest below that at the ask's own rung.
    /// </summary>
    /// <remarks>
    /// <b>A piece's arrival is read from its own near edge</b>, as every piece's is (<see cref="PlannedAsk.ArrivalAt"/>),
    /// which for a hold travelling down the way is the wrong end of it. It decides nothing: the backing rung is below
    /// every other, and of two holders that can no longer stop, the committed piece is shorter than either could
    /// stop in.
    /// </remarks>
    public void TakeBack(in PlannedAsk ask, int way, float fromM, float tailM, float committedFromM)
    {
        var splitM = Math.Clamp(committedFromM, fromM, tailM);
        Take(ask with { FromM = fromM, AheadM = tailM - splitM, CommittedToM = float.NegativeInfinity }, way, splitM);
        Take(ask with { FromM = splitM, AheadM = 0f, CommittedToM = float.PositiveInfinity }, way, tailM);
    }

    /// <summary>
    /// <b>Whether another's main claim is the plan of somebody queued behind the asker</b>: its hold was cut by the
    /// asker's own body, so it runs up to the asker because the asker stands there.
    /// </summary>
    bool IsQueuedBehind(in PlannedAsk ask, in LaneClaim other)
    {
        if (other.Secondary || other.Hold == NoHold) return false;

        ref readonly var cutBy = ref _holdCutBy[other.Hold];
        return cutBy.HasBody && !cutBy.Passing && Owns(ask, cutBy);
    }

    /// <summary>
    /// Whether a hold backing down a way keeps the metre <paramref name="atM"/> against another's claim over
    /// <c>[overFromM, atM)</c> — committed there where <paramref name="atM"/> is above
    /// <paramref name="committedFromM"/>, and reached after as much of the way as lies between it and the tail.
    /// </summary>
    bool KeepsBacking(
        in PlannedAsk ask, int way, in LaneClaim other, float overFromM, float atM, float tailM, float committedFromM)
    {
        var here = ask with
        {
            FromM = atM,
            AheadM = tailM - atM,
            CommittedToM = atM > committedFromM ? float.PositiveInfinity : float.NegativeInfinity,
        };

        return Beats(
            here, atM, StandsOn(way, ask.Occupant, ask.Of, overFromM, atM), other,
            StandsOn(way, other.Occupant, other.Of, overFromM, atM));
    }
}
