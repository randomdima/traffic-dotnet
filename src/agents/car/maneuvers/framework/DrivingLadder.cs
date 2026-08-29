namespace TrafficSimulation.Agents.Car.Maneuvers;

/// <summary>
/// What the ladder is allowed to ask of the car standing in front of it. <b>Every field is a question
/// about the pose or the holdings and none is about what the car was doing</b>: a rung that presumes an
/// obstruction has to check for one, because a clock cannot tell "cannot go forward" from "waiting to".
/// </summary>
/// <param name="ObstructionHasPriority">Somebody who is entitled to be there is in the way, so waiting is the correct answer and nothing below applies — the ground is theirs (TER-5e) and no recovery can be owed it.</param>
/// <param name="SomethingToBackAwayFrom">One of the four states that count: something in the way, a boundary it may not cross, a template it can no longer follow, a line it has lost.</param>
/// <param name="RoomBehindM">How much drivable ground the straight behind the car actually has, walked rather than assumed.</param>
/// <param name="BackOffsLeft">Attempts left on the back-off's own count — two per jam.</param>
/// <param name="InItsOwnBay">Standing in the bay it still holds, before the mouth: the one piece of road this car is entitled to occupy.</param>
/// <param name="AtItsOwnBay">Within reach of the bay this leg holds — without which a car that jams leaving one bay is sent to square itself up in another across town.</param>
/// <param name="HoldsAPlace">There is a reserved bay to give up.</param>
/// <param name="OnARoute">There is a route to re-derive, and a stretch to mark blocked.</param>
/// <param name="ReroutesLeft">Reroutes left on this leg.</param>
/// <param name="AStraightCanSaveIt">A pose exists along the car's own axis where the whole body lands on drivable ground.</param>
/// <param name="SomewhereLegalToStop">Where the car stands is not itself an obstruction — no crossing, no junction, not across a lane.</param>
internal readonly record struct LadderState(
    bool ObstructionHasPriority,
    bool SomethingToBackAwayFrom,
    float RoomBehindM,
    int BackOffsLeft,
    bool InItsOwnBay,
    bool AtItsOwnBay,
    bool HoldsAPlace,
    bool OnARoute,
    int ReroutesLeft,
    bool AStraightCanSaveIt,
    bool SomewhereLegalToStop);

/// <summary>
/// The one ladder every stuck situation walks. Each rung does something different
/// from what just failed; a rung whose entry conditions do not hold is <em>skipped</em> rather than
/// attempted, and the ladder never stops early — past its last rung there is always
/// <see cref="Maneuver.AbandonTheCar"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rung 0 hands the car back a place to wait at rather than a manoeuvre of its own.</b> What is in the
/// way has the right of way over this car (TER-5e), and the whole of what the wait needs is a name and a
/// bound — `P-6` is both, and a car that has been stopped short of a box, a bar or a crossing is already
/// standing at exactly the place that entry is about. Where there is no such place, the thing in front is
/// traffic rather than a rule, and the rungs below are what that is for.
/// </para>
/// <para>
/// <b>Rungs 2 and 3 are not a repeat of rung 1.</b> What a second back-off buys is the fuse between
/// them: a jam that has had another watchdog's worth of time to change is a different jam, and deleting
/// the rung was measured in the reference build and moved the wrong way. Rung 1′ is the unparking case
/// and is the only rung that goes <em>back</em> to a planned manoeuvre — the bay is the one piece of
/// road this car is entitled to hold, so retreating into it is a change of state rather than a retry.
/// </para>
/// <para>
/// The ladder decides <em>which</em> manoeuvre; whether the car is stuck at all is the watchdog's, and
/// the two are kept apart on purpose. A ladder that climbs for the wrong reason does more damage than
/// the jam it was for, because every rung on it is licensed to break a rule.
/// </para>
/// </remarks>
internal static class DrivingLadder
{
    /// <summary>How many rungs there are, counting 1′ as its own.</summary>
    public const int Rungs = 10;

    /// <summary>
    /// The manoeuvre this rung names, or <see cref="Maneuver.None"/> where its entry conditions do not
    /// hold and the rung is skipped.
    /// </summary>
    public static Maneuver At(int rung, in LadderState state) => rung switch
    {
        // 0 — where somebody else is entitled to the ground, waiting is correct and nothing below
        // applies: hold at whatever place the car was stopped short at, and spend another fuse there.
        0 => state.ObstructionHasPriority ? Maneuver.HoldAtALine : Maneuver.None,

        // 1′ — unparking only, and it comes before the back-off because a car still inside its bay has
        // somewhere lawful to stand and reversing further into it would leave the bay by the back.
        1 => state.InItsOwnBay ? Maneuver.LeaveTheBay : Maneuver.None,

        // 1 — the cheapest change of state: make room, and re-decide from the new distance.
        2 => CanBackOff(state) ? Maneuver.BackOff : Maneuver.None,

        // 2 — the manoeuvre is possible but was attempted badly. Skipped away from a bay.
        3 => state.AtItsOwnBay ? Maneuver.SquareUpInTheBay : Maneuver.None,

        // 3 — the second back-off, whose whole value is the fuse between it and the first.
        4 => CanBackOff(state) ? Maneuver.BackOff : Maneuver.None,

        // 4 — the destination, not the manoeuvre, is the problem.
        5 => state.HoldsAPlace ? Maneuver.GiveUpThePlace : Maneuver.None,

        // 5 — the road, not the destination, is the problem.
        6 => state is { OnARoute: true, ReroutesLeft: > 0 } ? Maneuver.Reroute : Maneuver.None,

        // 6 — the car is somewhere it should not be, and a straight exists that gets it off.
        7 => state.AStraightCanSaveIt ? Maneuver.ReturnToLegalGround : Maneuver.None,

        // 7 — get as close as the actions allow; the driver walks the rest.
        8 => state.SomewhereLegalToStop ? Maneuver.SettleForHere : Maneuver.None,

        // 8 — nothing else is available.
        _ => Maneuver.AbandonTheCar,
    };

    /// <summary>
    /// The next rung that names something, from <paramref name="rung"/> upward. <b>The ladder never
    /// stops early</b>: a skipped rung costs the walk and not the climb.
    /// </summary>
    public static Maneuver Next(in LadderState state, ref int rung)
    {
        while (rung < Rungs)
        {
            var maneuver = At(rung++, state);
            if (maneuver != Maneuver.None) return maneuver;
        }

        return Maneuver.AbandonTheCar;
    }

    /// <summary>
    /// <b>There must be something to back away from, and room to back into.</b> The fault this guard
    /// fixes was cars reversing away from empty intersections while yielding correctly, and the rung is
    /// skipped below half a car length of room rather than driven into whatever is behind.
    /// </summary>
    static bool CanBackOff(in LadderState state) =>
        state is { SomethingToBackAwayFrom: true, BackOffsLeft: > 0 } && state.RoomBehindM > 0f;
}
