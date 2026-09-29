namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>How strong a reservation is</b> (TER-5g) — the ladder the planned layer is settled on, and the whole of
/// what says which of two holds meeting on one piece of the world gives it up.
/// </summary>
/// <remarks>
/// <para>
/// <b>Low is strong, and the numbers are the ladder</b> rather than whatever order the members happen to be
/// written in. Every rung but the first is a <em>planned</em> reservation; the first is the physical layer,
/// which is never compared at all.
/// </para>
/// <para>
/// <b>It is the one property a planned reservation carries besides who, where and how far off</b>. What a
/// movement is entitled to is the rung it holds its ground at (TER-5e): straighter is stronger, so a street
/// going through a box holds at <see cref="FirmStraight"/> and the turn across the oncoming stream at
/// <see cref="FirmAcross"/>, and the comparison that settles the two of them is the one comparison there is
/// (<see cref="LaneOccupancy.Beats"/>).
/// </para>
/// </remarks>
internal enum ClaimPriority : byte
{
    /// <summary>
    /// <b>p0 — a body</b>: the ground its collider stands on (TER-4c.2), and the ground a pass will cover
    /// (TER-4c.6). Never compared, never cut and never taken, and free to overlap another body's, because it is a
    /// record of where real things are — or will be, past the point of going back — rather than of what anybody
    /// plans.
    /// </summary>
    Hard = 0,

    /// <summary>
    /// <b>p1 — ground its holder can no longer stop short of</b>: the road a moving body will cover before it
    /// could be at rest. Taken by nothing, since a right of way orders who waits and never who is driven into
    /// (TER-5e).
    /// </summary>
    Committed = 1,

    /// <summary>
    /// <b>p2 — the planned ground of somebody answering a call</b> (AMB-4): an ambulance, a police car or an
    /// evacuator with the light on. Given up to a committed body and to nothing else.
    /// </summary>
    Special = 2,

    /// <summary>
    /// <b>p4 — a light's hold</b> (TLT-1): the stretch past a bar on an approach that is not showing green,
    /// and the paint of a crossing that is showing red. Above a walker and every movement, below a call.
    /// </summary>
    Signal = 4,

    /// <summary>
    /// <b>p5 — somebody on foot</b> (PER-27): every way a walker plans, one rung above the strongest movement
    /// the traffic makes and below a light's hold.
    /// </summary>
    /// <remarks>
    /// <b>Nothing here is about a zebra</b>: that the traffic gives way on the paint is this rung meeting a
    /// car's plan through the marks the zebra is laid with (TER-5c.3), and the pavement a car's turn sweeps at a
    /// corner is given up the same way. Ground a car can no longer stop short of is above it all the same
    /// (TER-5e).
    /// </remarks>
    Afoot = 5,

    /// <summary><b>p6 — a movement through a box that turns out of nobody's way</b> (TER-4a, TER-5e): straight on.</summary>
    FirmStraight = 6,

    /// <summary>
    /// <b>p7 — ordinary traffic</b>: the near-side turn, and every stretch of way a driver plans that is not a
    /// movement through a box at all.
    /// </summary>
    Firm = 7,

    /// <summary><b>p8 — the turn across the oncoming stream</b> (TER-4a): the weakest movement a box admits, because it is the last one there is (TER-5f).</summary>
    FirmAcross = 8,

    /// <summary>
    /// <b>p9 — a car backing up for the room to step out round what stands in its lane</b> (TER-4c.7, CAR-50):
    /// below every movement, so whatever else wants the ground behind it has it.
    /// </summary>
    /// <remarks>
    /// <b>Strictly below and never equal to <see cref="FirmAcross"/></b>: the approach to a turn across is held at
    /// that rung too (TER-5g.1), and a tie goes to whoever is nearer — which a car backing onto the ground behind
    /// its own tail always is.
    /// </remarks>
    Backing = 9,
}
