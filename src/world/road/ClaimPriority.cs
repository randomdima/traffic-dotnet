namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>How strong a claim on ground is</b> (TER-5g) — the ladder the whole town arbitrates on, and the
/// whole of what says which of two bodies coming to one piece of the world gives it up.
/// </summary>
/// <remarks>
/// <para>
/// <b>Low is strong, and the numbers are the ladder</b> rather than whatever order the members happen to be
/// written in. The gaps are levels nothing claims yet: a level added between two that exist is a number
/// between two numbers, and neither of them moves.
/// </para>
/// <para>
/// <b>It is the one property a claim carries besides who and where</b>, and what it decides is <em>whether</em>
/// a claim can be taken and by whom. Which of two claims of one strength wins is the movement's own right of
/// way (TER-5e) — a fact about the way each of them is on rather than about either body.
/// </para>
/// </remarks>
internal enum ClaimPriority : byte
{
    /// <summary>
    /// <b>p0 — a body, and the road a body can no longer give back.</b> Nothing takes it, whatever anybody
    /// else is asking with: a right of way orders who waits and never who is driven into (TER-5e).
    /// </summary>
    /// <remarks>
    /// <b>It needs no rule of its own for a car nobody is driving.</b> A wreck, a parked car, a body shoved
    /// off its line and a car under a hand all stand on the ground they are standing on, which is this by
    /// construction — the whole of what "a hand at the wheel writes a hard claim" means.
    /// </remarks>
    Hard = 0,

    /// <summary>
    /// <b>p1 — ground somebody answering a call has been granted and not reached</b> (AMB-4): an ambulance,
    /// a police car or an evacuator with the light on. <b>Taken by <see cref="Hard"/> and by nothing else</b>,
    /// which is the whole of what "every other agent gives way" comes to.
    /// </summary>
    Special = 1,

    /// <summary>
    /// <b>p5 — ground anybody else has been granted and not reached</b>: the far end of a box a car has
    /// committed to crossing, a bay being backed out of, a swerve about to cross, a road an officer is
    /// holding (SRV-6). It is empty <em>now</em>, which is exactly why a reading taken off the bodies alone
    /// lets two cars take it at once.
    /// </summary>
    /// <remarks>
    /// <b>It is taken by a strictly stronger movement and by nothing weaker or equal.</b> Two movements of
    /// one right of way settle by whichever was granted it, and go on holding what they were given.
    /// </remarks>
    Firm = 5,

    /// <summary>
    /// <b>p9 — road a driver has stated it means to use and has not reached</b> (TER-5g): the stretch
    /// beyond the claim it is committed to that it takes to reach the planned speed, hold it, and stop from
    /// there. The weakest hold there is, and the one every stronger movement is entitled to.
    /// </summary>
    /// <remarks>
    /// <b>An equal right of way takes it, where an equal right of way does not take a <see cref="Firm"/>
    /// claim.</b> A granted claim is one movement's ground and is settled by whoever was granted it;
    /// stated claims are laid by everybody in the same rebuild, so two movements of one rank that each
    /// refused the other's would each be waiting on ground the other was merely thinking about, and neither
    /// would ever ask for it.
    /// </remarks>
    Soft = 9,

    /// <summary>
    /// <b>p10 — an ask that was refused, left standing so the traffic can see it</b> (TER-5e): the band
    /// of a lane a walker at a kerb was told it could not have. It is nobody's ground and it binds nobody.
    /// </summary>
    /// <remarks>
    /// <b>What it does is stop the traffic short of the paint</b>, which is what hands the ground back to
    /// whoever was waiting for it (TER-4c.1) — and a stop is bounded by the road a car needs to make one, so
    /// a car too close to stop keeps the paint and the wait lasts another moment. It is in no walk that cuts
    /// a grant; the one question about it is its own.
    /// </remarks>
    Rejected = 10,
}
