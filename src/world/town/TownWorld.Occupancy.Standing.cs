using System.Numerics;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The cars whose claims are what they were at the last laying</b> (TER-4c.2, SOL-37): standing, frozen through every
/// step since, and in no other action in between. Each is laid again from what the atlas found under it then and asked
/// nothing else, and no other pass of the laying visits it — what each would do with a standing car is clear what it
/// cleared last time.
/// </summary>
internal sealed partial class TownWorld
{
    readonly StandingCars _standing;

    /// <summary>
    /// <b>What a laying kept of a car that stood</b>: which freezing it was in and how many actions it had been handed by
    /// then — the two things that change when anything that decides its claims does — and, for the passes after the
    /// bodies, the cars this laying did ask.
    /// </summary>
    sealed class StandingCars(int cars)
    {
        public readonly bool[] Kept = new bool[cars];

        public readonly int[] FrozeAtStep = new int[cars];

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

        var body = Cars.Body[car];
        return _physics.IsFrozen(body) && _physics.FrozeAtStep(body) == _standing.FrozeAtStep[car]
                                        && _carActions.EntriesOf(car) == _standing.Entries[car]
                                        && Cars.PositionM[car] == _standing.PositionM[car]
                                        && Cars.HeadingRad[car] == _standing.HeadingRad[car]
                                        && Cars.VelocityMps[car] == Vector2.Zero && _recovery.OnTheHookOf[car] < 0;
    }

    /// <summary>
    /// <b>Every car's body, and the list of cars the rest of the laying asks</b>: a standing car laid again from its
    /// last answer, any other laid by asking and kept if it is standing now.
    /// </summary>
    void LayTheCarsBodies()
    {
        _standing.AskedCount = 0;
        for (var car = 0; car < Cars.Count; car++)
        {
            if (StandsAsLaid(car))
            {
                LayTheStandingBody(car);
                continue;
            }

            LayTheCarsBody(car);
            KeepIfStanding(car);
            _standing.Asked[_standing.AskedCount++] = car;
        }
    }

    /// <summary>The cars this laying asked, which are the only ones whose drive, pass, manoeuvre and grant can be anything but what they were.</summary>
    ReadOnlySpan<int> CarsAsked => _standing.Asked.AsSpan(0, _standing.AskedCount);

    /// <summary>
    /// A standing car is laid as <see cref="LayTheCarsBody"/> lays it: its own body, under its own name, going nowhere
    /// down any way and still — over every way the atlas found under it at this same pose.
    /// </summary>
    void LayTheStandingBody(int car)
    {
        var covers = _groundUnderCars.CoversOf(car)[.._groundUnderCars.Last[car].Count];
        foreach (ref readonly var cover in covers)
        {
            _occupancy.LayBody(
                cover.Way, cover.FromM, cover.ToM, 0f, car, LaneRoster.Driving, onItsLine: false, LaneOccupancy.NoWay,
                still: true);
        }
    }

    void KeepIfStanding(int car)
    {
        var body = Cars.Body[car];
        var standing = Cars.Action[car] == CarAction.Stand && _recovery.OnTheHookOf[car] < 0 && _physics.IsFrozen(body);
        _standing.Kept[car] = standing;
        if (!standing) return;

        _standing.FrozeAtStep[car] = _physics.FrozeAtStep(body);
        _standing.Entries[car] = _carActions.EntriesOf(car);
        _standing.PositionM[car] = Cars.PositionM[car];
        _standing.HeadingRad[car] = Cars.HeadingRad[car];
    }
}
