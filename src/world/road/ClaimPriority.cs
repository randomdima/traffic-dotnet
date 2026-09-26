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
/// <b>It is the one property a claim carries besides who and where.</b> What a movement is entitled to is
/// the rung it is granted at and not a rank beside it (TER-5e): straighter is stronger, so a street going
/// through a box holds its ground at <see cref="FirmStraight"/> and the turn across the oncoming stream at
/// <see cref="FirmAcross"/>, and the comparison that settles the two of them is the one comparison there
/// is.
/// </para>
/// <para>
/// <b>It is two bands of the same order</b> (<see cref="LaneOccupancy.SaidAhead"/>): what a body has been
/// granted, and the same thing merely stated. A statement is read at the rung its holder would have been
/// granted, which is what keeps <em>an equal movement takes a statement and does not take a grant</em> to
/// one ladder — and the bands are a fixed distance apart so that reading is arithmetic rather than a table.
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
    /// <b>p2 — a road a police car at a scene is holding shut</b> (SRV-6): above every ordinary movement and
    /// below a call, which is the whole of what "the other services are let through" means. A vehicle
    /// answering one is not refused by a closure and needs to know nothing about why.
    /// </summary>
    Closed = 2,

    /// <summary>
    /// <b>p4 — ground a movement through a box that turns out of nobody's way has been granted</b>
    /// (TER-4a, TER-5e): straight on, which is the strongest movement a box admits.
    /// </summary>
    FirmStraight = 4,

    /// <summary>
    /// <b>p5 — ground anybody else has been granted and not reached</b>: the far end of a box a car has
    /// committed to crossing, <b>the road between the car and that box</b> (TER-5g.1), a bay being backed out
    /// of, a swerve about to cross, the near-side turn, and every stretch of way that is not a movement
    /// through a box at all. It is empty <em>now</em>, which is exactly why a reading taken off the bodies
    /// alone lets two cars take it at once.
    /// </summary>
    /// <remarks>
    /// <b>It is taken by a strictly stronger movement and by nothing weaker or equal.</b> Two movements of
    /// one rung settle by whichever was granted it, and go on holding what they were given.
    /// </remarks>
    Firm = 5,

    /// <summary>
    /// <b>p6 — ground the turn across the oncoming stream has been granted</b> (TER-4a): the weakest
    /// movement a box admits, because it is the last one there is (TER-5f).
    /// </summary>
    FirmAcross = 6,

    /// <summary>
    /// <b>p7 — a crossing somebody on foot has reserved to walk</b> (PER-27): the paint in front of the
    /// body on the stretch it is taking, and the band of every lane the crossing is painted across, held
    /// from a stop short of the paint until the walk is off it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It refuses nobody.</b> Ordinary traffic drives over it and takes the metres it is granted off it
    /// like any claim it outranks, which is the whole of what this rung being below the granted band means:
    /// a reservation is what somebody on foot wants of a crossing and never a right to it.
    /// <b>Making the traffic wait for one is this level's own arm of <see cref="LaneOccupancy.Binds"/></b>
    /// and nothing else — no second mechanism, and no rule of the paint's own (SIM-7).
    /// </para>
    /// <para>
    /// <b>What it buys is that it outlasts a statement.</b> A driver saying it means to use those metres does
    /// not take them off somebody waiting to cross, so the town can see who is waiting and for which paint.
    /// </para>
    /// </remarks>
    Reserved = 7,

    /// <summary>
    /// <b>p8 — road somebody answering a call has stated it means to use</b> (AMB-4), which is
    /// <see cref="Special"/> read in the stated band.
    /// </summary>
    SoftSpecial = 8,

    /// <summary>
    /// <b>p11 — road a movement straight through a box has stated it means to use</b>, which is
    /// <see cref="FirmStraight"/> read in the stated band.
    /// </summary>
    SoftStraight = 11,

    /// <summary>
    /// <b>p12 — road a driver has stated it means to use and has not reached</b> (TER-5g), which is
    /// <see cref="Firm"/> read in the stated band: the stretch beyond the claim it is committed to that it
    /// takes to reach the planned speed, hold it, and stop from there — and the pavement a walker is walking
    /// at (PER-26). Every stronger movement is entitled to it.
    /// </summary>
    /// <remarks>
    /// <b>An equal movement takes it, where an equal movement does not take a <see cref="Firm"/> claim.</b>
    /// A granted claim is one movement's ground and is settled by whoever was granted it; stated claims are
    /// laid by everybody in the same rebuild, so two movements of one rung that each refused the other's
    /// would each be waiting on ground the other was merely thinking about, and neither would ever ask for
    /// it.
    /// </remarks>
    Soft = 12,

    /// <summary>
    /// <b>p13 — road the turn across the oncoming stream has stated it means to use</b>, which is
    /// <see cref="FirmAcross"/> read in the stated band.
    /// </summary>
    SoftAcross = 13,

    /// <summary>
    /// <b>p15 — an ask that was refused, left standing so the traffic can see it</b> (TER-5g). <b>Nothing
    /// lays one</b> (PER-26): it is a level of the ladder nothing claims yet.
    /// </summary>
    /// <remarks>
    /// <b>It is nobody's ground.</b> It binds nobody (<see cref="LaneOccupancy.Binds"/>), neither takes
    /// metres from a claim nor gives any up to one as it is laid (<see cref="LaneOccupancy.MakeRoomFor"/>),
    /// and is in no walk that cuts a grant.
    /// </remarks>
    Rejected = 15,
}
