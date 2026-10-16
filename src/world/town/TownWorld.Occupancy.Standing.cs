using System.Numerics;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The cars whose claims are what they were at their last turn</b> (TER-4c.2, SOL-37): standing, frozen through every
/// step since, and in no other action in between. Each keeps what it holds, its body not read again and nothing else
/// asked, and no other pass of its turn visits it — what each would do with a standing car is clear what it cleared
/// last time.
/// </summary>
internal sealed partial class TownWorld
{
    readonly StandingCars _standing;

    /// <summary>
    /// <b>What a turn kept of a car that stood</b>: which freezing it was in and how many actions it had been handed by
    /// then — the two things that change when anything that decides its claims does — and, for the passes after the
    /// bodies, the cars this turn did ask.
    /// </summary>
    internal sealed class StandingCars(int cars)
    {
        public readonly bool[] Kept = new bool[cars];

        /// <summary>
        /// Which freezing each car's body is in: the world's step count at the view it froze in (<see cref="CarView"/>),
        /// the same for as long as it stays frozen and later each time it freezes again. Meaningless while it moves.
        /// </summary>
        public readonly long[] Freezing = new long[cars];

        /// <summary>The <see cref="Freezing"/> a kept car was laid in.</summary>
        public readonly long[] KeptFreezing = new long[cars];

        public readonly uint[] Entries = new uint[cars];

        /// <summary>The pose the fleet had it at, which is what its body is laid at and what a stage may write over.</summary>
        public readonly Vector2[] PositionM = new Vector2[cars];

        public readonly float[] HeadingRad = new float[cars];

        /// <summary>The cars laid by asking this time, in index order: the only ones the later passes visit.</summary>
        public readonly int[] Asked = new int[cars];

        public int AskedCount;
    }

    /// <summary>
    /// Whether this car is laid as it was last time: kept then, still in the same freezing and the same action, and
    /// at the same pose in the fleet. A standing car's action holds nothing but its body, so its claims were its
    /// body's, and its body has not moved.
    /// </summary>
    bool StandsAsLaid(int car)
    {
        if (!_standing.Kept[car]) return false;

        ref readonly var seen = ref _world.View.Cars[car];
        return (seen.Flags & CarView.Frozen) != 0 && _standing.Freezing[car] == _standing.KeptFreezing[car]
                                        && _carActions.EntriesOf(car) == _standing.Entries[car]
                                        && Cars.PositionM[car] == _standing.PositionM[car]
                                        && Cars.HeadingRad[car] == _standing.HeadingRad[car]
                                        && Cars.VelocityMps[car] == Vector2.Zero && _recovery.OnTheHookOf[car] < 0;
    }

    /// <summary>
    /// <b>The body of every car whose turn it is, and the list of them the rest of the turn asks</b>: each laid where the
    /// world reads it (nothing done where that is where it stood), a car standing as laid not even read again and asked
    /// nothing else, and any other kept if it is standing now.
    /// </summary>
    void LayTheCarsBodies()
    {
        _standing.AskedCount = 0;
        var inTurn = CarsInTurn;
        for (var slot = 0; slot < inTurn.Length; slot++)
        {
            if (slot + PrefetchAhead < inTurn.Length) _occupancy.PrefetchTheBody(inTurn[slot + PrefetchAhead], LaneRoster.Driving, slots: false);
            if (slot + (PrefetchAhead / 2) < inTurn.Length) _occupancy.PrefetchTheBody(inTurn[slot + (PrefetchAhead / 2)], LaneRoster.Driving, slots: true);

            var car = inTurn[slot];

            // Frozen at the pose it was laid at, its body would be read off the same covers onto the same stretches.
            if (StandsAsLaid(car)) continue;

            LayTheCarsBody(car);
            KeepIfStanding(car);
            _standing.Asked[_standing.AskedCount++] = car;
        }
    }

    /// <summary>The cars whose turn it is and that are not standing as they were — the only ones whose drive, pass, manoeuvre and grant can be anything but what they were.</summary>
    internal ReadOnlySpan<int> CarsAsked => _standing.Asked.AsSpan(0, _standing.AskedCount);

    /// <summary>How many actions a car has been handed, which changes whenever anything that decides its claims does — for an instrument.</summary>
    internal uint ActionEntriesOf(int car) => _carActions.EntriesOf(car);

    void KeepIfStanding(int car)
    {
        ref readonly var seen = ref _world.View.Cars[car];
        var standing = Cars.Action[car] == CarAction.Stand && _recovery.OnTheHookOf[car] < 0
                                                          && (seen.Flags & CarView.Frozen) != 0;
        _standing.Kept[car] = standing;
        if (!standing) return;

        _standing.KeptFreezing[car] = _standing.Freezing[car];
        _standing.Entries[car] = _carActions.EntriesOf(car);
        _standing.PositionM[car] = Cars.PositionM[car];
        _standing.HeadingRad[car] = Cars.HeadingRad[car];
    }
}
