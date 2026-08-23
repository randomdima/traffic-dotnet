namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>The questions the book is asked</b>: what is in front, what is behind, how much of a named piece of
/// ground is already somebody's — every one of them a walk of one way's stretches in the order they lie,
/// and none of them a decision (SIM-7).
/// </summary>
/// <remarks>
/// <b>Which stretches an answer is allowed to count is a mask and not a second loop</b>
/// (<see cref="Only"/>): a walker at a kerb asks about traffic, a driver's grant is cut by everything
/// spoken for, and `P-12` asks only about bodies on foot — three questions of one book, so that a use
/// added to <see cref="LaneUse"/> is named in one place per question rather than missed in one of them.
/// </remarks>
internal sealed partial class LaneOccupancy
{
    /// <summary>
    /// <b>The nearest body in front</b>: the stretch with the least near edge that still reaches past
    /// <paramref name="fromM"/> and begins before <paramref name="untilM"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A stretch the asking body is already overlapping answers at its own near edge rather than being
    /// skipped — a car inside somebody else is a contact and not a gap, and reading it as an empty road is
    /// the one wrong answer here.
    /// </para>
    /// <para>
    /// <b>Reaching past means the body does</b> (<see cref="LaneSlot.StandsToM"/>) and not the road it has
    /// taken: a driver behind this one has a reservation running through it, and that is a car behind and
    /// not a body in front.
    /// </para>
    /// </remarks>
    public bool AheadBody(
        int way, float fromM, float untilM, int excluding, out LaneSlot found,
        LaneRoster excludingOf = LaneRoster.Driving) =>
        Nearest(way, fromM, untilM, excluding, excludingOf, Bodies, out found);

    /// <summary>The nearest <see cref="LaneUse.Claimed"/> stretch in front, which is ground about to stop being empty.</summary>
    public bool AheadClaim(
        int way, float fromM, float untilM, int excluding, out LaneSlot found,
        LaneRoster excludingOf = LaneRoster.Driving) =>
        Nearest(way, fromM, untilM, excluding, excludingOf, Only(LaneUse.Claimed), out found);

    /// <summary>
    /// <b>Whether any traffic has this stretch of the way</b>: a road a driver has taken, ground somebody
    /// has claimed, a body with no reservation to its name. The all-or-nothing question — <em>is this piece
    /// of ground anybody's</em> — as against <see cref="NextSpokenFor"/>'s "how much of it is mine".
    /// </summary>
    /// <remarks>
    /// <b>Traffic and not everything spoken for</b>: the asker is somebody about to step into the road, and
    /// another person already standing in it is neither a reason to stay on the pavement nor something a
    /// grant is taken against.
    /// </remarks>
    public bool AnyTrafficOver(int way, float fromM, float toM) =>
        AnythingOver(way, fromM, toM, Nobody, LaneRoster.Driving, TrafficSpoken, out _);

    /// <summary>
    /// <b>Whether this stretch of ground is somebody else's</b>, whoever they are and whatever they are
    /// doing on it — what a manoeuvre asks of every place its own geometry would put a body, since a
    /// template runs over ground no way owns and the ways under it are all there is to ask; and what a
    /// driver asks of the metres another way of a junction crosses its own (TER-5c.1).
    /// </summary>
    /// <remarks>
    /// The first answer of <see cref="NextSpokenForOver"/> and not a second copy of its loop, so <em>is any
    /// of this anybody's</em> and <em>which of them are</em> cannot come apart.
    /// </remarks>
    public bool SpokenForByAnother(
        int way, float fromM, float toM, int excluding, out LaneSlot found,
        LaneRoster excludingOf = LaneRoster.Driving) =>
        AnythingOver(way, fromM, toM, excluding, excludingOf, Spoken, out found);

    bool AnythingOver(
        int way, float fromM, float toM, int excluding, LaneRoster excludingOf, int uses, out LaneSlot found)
    {
        var at = FromTheStart;
        return NextOver(way, fromM, toM, excluding, excludingOf, uses, ref at, out found);
    }

    /// <summary>
    /// <b>Every stretch of this piece of the way that is somebody else's</b>, near edge first — <b>including
    /// one that began behind it and runs through</b>, which is the whole of what tells this apart from
    /// <see cref="NextSpokenFor"/>.
    /// </summary>
    /// <remarks>
    /// The two are asking different questions of the same book. A driver on a way wants the occupants of it
    /// in the order they are actually in, and a stretch reaching back past its own tail is a car
    /// <em>behind</em> it; whoever is asking about a named piece of ground — a section two ways meet on, the
    /// mouth of a bay — wants everything lying over it, and where the near edge of that began is nothing to
    /// them.
    /// </remarks>
    public bool NextSpokenForOver(
        int way, float fromM, float toM, int excluding, ref int at, out LaneSlot found,
        LaneRoster excludingOf = LaneRoster.Driving) =>
        NextOver(way, fromM, toM, excluding, excludingOf, Spoken, ref at, out found);

    bool NextOver(
        int way, float fromM, float toM, int excluding, LaneRoster excludingOf, int uses, ref int at,
        out LaneSlot found)
    {
        for (at = at == FromTheStart ? _head[way] : _next[at]; at != NoSlot; at = _next[at])
        {
            ref readonly var slot = ref _slots[at];
            if (slot.FromM > toM) break;
            if (Is(slot, excluding, excludingOf) || (Only(slot.Use) & uses) == 0 || slot.ToM < fromM) continue;

            found = slot;
            return true;
        }

        found = LaneSlot.Nothing;
        return false;
    }

    /// <summary>
    /// <b>Whether anybody is on foot over this stretch of the way</b> — the one question the road's book is
    /// asked about walkers, and the whole of `P-12`'s "is there somebody on the paint".
    /// </summary>
    /// <remarks>
    /// It excludes nobody, because the asker is a car and the occupants of these stretches are walkers:
    /// the two are indexed separately and no query but this one ever reads a
    /// <see cref="LaneUse.OnFoot"/> slot, which is what keeps the two rosters from being told apart by
    /// an integer.
    /// </remarks>
    public bool AnybodyOnFoot(int way, float fromM, float toM)
    {
        for (var at = _head[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var slot = ref _slots[at];
            if (slot.FromM > toM) break;
            if (slot.Use != LaneUse.OnFoot || slot.ToM < fromM) continue;

            return true;
        }

        return false;
    }

    /// <summary>
    /// <b>How far up the way the ground is already anybody's</b>: of everything behind
    /// <paramref name="beforeM"/> that is spoken for or standing on the way, the stretch whose far edge
    /// reaches furthest. <b>Its far edge is where whoever it belongs to is committed to being able to stop,
    /// and everything past that is ground nothing has taken</b> — which is what a body about to step into a
    /// lane has to ask, since a place inside somebody's own road is a place nothing could have stopped short
    /// of.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The furthest and not the nearest.</b> The nearest stretch behind may belong to a car that has
    /// already stopped, and the binding one is whoever is still coming through it.
    /// </para>
    /// <para>
    /// It excludes nobody, because the asker is not on the way at all: what a walker beside a road asks is
    /// whether the road is anybody's, and its own presence is not yet part of the answer.
    /// </para>
    /// </remarks>
    public bool TakenUpTo(int way, float beforeM, out LaneSlot found)
    {
        found = LaneSlot.Nothing;
        var any = false;
        for (var at = _head[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var slot = ref _slots[at];
            if (slot.FromM >= beforeM) break;
            if ((Only(slot.Use) & Spoken) == 0) continue;
            if (any && slot.ToM <= found.ToM) continue;

            found = slot;
            any = true;
        }

        return any;
    }

    /// <summary>
    /// Where a walk of one way's stretches begins. Handed to <see cref="NextSpokenFor"/> or
    /// <see cref="NextSpokenForOver"/> and carried by it after that.
    /// </summary>
    public const int FromTheStart = -2;

    /// <summary>
    /// <b>Every stretch in front that is already spoken for</b>, near edge first: a road another driver has
    /// taken, ground somebody has claimed, a body that is not going anywhere.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>In front means the body is</b> (<see cref="LaneSlot.StandsToM"/>), and never where the ground it
    /// holds begins: a stretch begins a margin behind its owner's tail (TER-4c.1), so near edges put the
    /// occupants of a way in an order that is one margin out of step with the bodies in it. What the asker
    /// wants is whoever is actually ahead of it — a car whose stretch runs past the one in front is still
    /// behind it, and one whose ground reaches back past the asker's nose is still in front of it and is
    /// exactly what the asker has to be cut at.
    /// </para>
    /// <para>
    /// <b>Walked rather than answered with the nearest one</b>, because the nearest is not the binding one:
    /// a car at speed rests further up the road than a slower car in front of it, and a driver cut at the
    /// nearer of those two would be granted road straight through the further.
    /// </para>
    /// </remarks>
    public bool NextSpokenFor(
        int way, float fromM, float untilM, int excluding, ref int at, out LaneSlot found,
        LaneRoster excludingOf = LaneRoster.Driving)
    {
        for (at = at == FromTheStart ? _head[way] : _next[at]; at != NoSlot; at = _next[at])
        {
            ref readonly var slot = ref _slots[at];
            if (slot.FromM > untilM) break;
            if (Is(slot, excluding, excludingOf) || slot.StandsToM < fromM) continue;
            if ((Only(slot.Use) & Spoken) == 0) continue;

            found = slot;
            return true;
        }

        found = LaneSlot.Nothing;
        return false;
    }

    /// <summary>The stretch a slot of this use occupies in a mask, so a query names the uses it is about.</summary>
    static int Only(LaneUse use) => 1 << (int)use;

    /// <summary>
    /// <b>Traffic</b>: a driver on its route, and anything with wheels standing on the road. What is asked
    /// about by whoever wants to know what is <em>coming</em> — a walker at a kerb, a car about to back out
    /// of a bay — and a person on the carriageway is not an answer to that question.
    /// </summary>
    const int Traffic = (1 << (int)LaneUse.Reserved) | (1 << (int)LaneUse.Obstruction);

    /// <summary>
    /// Where a body actually is, of whichever roster: traffic, and anybody on foot in the lane. <b>Read to
    /// <see cref="LaneSlot.StandsToM"/></b>, which is what tells a body apart from the road it has taken now
    /// that the two are one stretch.
    /// </summary>
    const int Bodies = Traffic | (1 << (int)LaneUse.OnFoot);

    /// <summary>
    /// Ground traffic has spoken for: reserved, claimed, or held by something with no reservation to its
    /// name. The same set as <see cref="Traffic"/> but for a claim, and read to <see cref="LaneSlot.ToM"/>.
    /// </summary>
    const int TrafficSpoken = Traffic | (1 << (int)LaneUse.Claimed);

    /// <summary>Ground anybody has spoken for, which is that and everybody on foot in the lane.</summary>
    const int Spoken = TrafficSpoken | (1 << (int)LaneUse.OnFoot);

    /// <summary>
    /// Whether a stretch is the asker's own. <b>Both halves, always</b>: an occupant is an index into one of
    /// two fleets, so a car excluding itself by number would otherwise also exclude the walker that happens
    /// to hold the same number.
    /// </summary>
    static bool Is(in LaneSlot slot, int occupant, LaneRoster of) => slot.Occupant == occupant && slot.Of == of;

    bool Nearest(int way, float fromM, float untilM, int excluding, LaneRoster excludingOf, int uses, out LaneSlot found)
    {
        for (var at = _head[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var slot = ref _slots[at];
            if (slot.FromM > untilM) break;
            if (Is(slot, excluding, excludingOf) || slot.StandsToM <= fromM) continue;
            if ((Only(slot.Use) & uses) == 0) continue;

            found = slot;
            return true;
        }

        found = LaneSlot.Nothing;
        return false;
    }

    /// <summary>
    /// <b>The nearest traffic behind</b>, which is what anything about to occupy ground it is not on yet has
    /// to ask — the mouth of a bay, the lane a swerve crosses, the paint a walker is about to step onto.
    /// </summary>
    /// <remarks>
    /// <b>Traffic and not bodies</b> (<see cref="Traffic"/>): every asker here is asking what is coming down
    /// the lane at it, and another person standing on the carriageway is not that. <b>And where the body
    /// itself has got to</b> (<see cref="LaneSlot.StandsToM"/>) — a car whose reservation reaches the mouth
    /// of a bay is a car still coming, and one whose bonnet is already past it has gone.
    /// </remarks>
    public bool BehindBody(
        int way, float beforeM, float sinceM, int excluding, out LaneSlot found,
        LaneRoster excludingOf = LaneRoster.Driving)
    {
        found = LaneSlot.Nothing;
        var any = false;
        for (var at = _head[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var slot = ref _slots[at];
            if (slot.FromM >= beforeM) break;
            if (Is(slot, excluding, excludingOf) || slot.StandsToM < sinceM || (Only(slot.Use) & Traffic) == 0) continue;

            found = slot;
            any = true;
        }

        return any;
    }

    /// <summary>
    /// Whether anybody else has already claimed ground this stretch runs over. <b>What makes a claim a
    /// reservation rather than a note</b>: two cars in neighbouring bays each looked at an empty lane and
    /// each backed onto it, and the second of them is what this refuses.
    /// </summary>
    public bool ClaimedByAnother(
        int way, float fromM, float toM, int excluding, LaneRoster excludingOf = LaneRoster.Driving)
    {
        for (var at = _head[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var slot = ref _slots[at];
            if (slot.FromM > toM) break;
            if (Is(slot, excluding, excludingOf) || slot.Use != LaneUse.Claimed || slot.ToM < fromM) continue;

            return true;
        }

        return false;
    }

    /// <summary>Everything on one way, nearest first — for a test, an overlay, and nothing on the hot path.</summary>
    public int CopyTo(int way, Span<LaneSlot> into)
    {
        var written = 0;
        for (var at = _head[way]; at != NoSlot && written < into.Length; at = _next[at]) into[written++] = _slots[at];

        return written;
    }
}
