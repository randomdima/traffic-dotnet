using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The round a car nobody owns drives</b> (CAR-8): a stand in a bay, a drive to a free bay near a place
/// drawn from its own stream, and a stand there. <b>The driving is the leg's</b> — parking and unparking are
/// the bay's own ways (GEN-4f, GEN-4j) — and what is here is only why the car goes.
/// </summary>
/// <remarks>
/// <b>It runs only in a town a car can park in, and only for a car nobody owns</b> — one somebody owns is their
/// trips' (<see cref="RunTheOwnersCar"/>). A map with no bay a car can reach tours its cars instead
/// (<see cref="DriveTheEmptyMap"/>), which is the one thing such a map leaves them to do.
/// </remarks>
internal sealed partial class TownWorld
{
    readonly ParkedRound _round;

    /// <summary>Whether any bay of this town can be driven into, which is what says its cars park rather than tour.</summary>
    bool _townParks;

    /// <summary>How many times a car on its round has set off from where it stood for a bay somewhere else.</summary>
    public long RoundsSetOff { get; private set; }

    /// <summary>
    /// One decision of the round, taken before the leg's own. <b>A car on its way is left to the leg</b>, a car
    /// in a bay stands out its stand, and a car stood down anywhere else — its leg given up in the street — sets
    /// off for a bay at once, since a car standing in a lane is an obstruction and not a stand.
    /// </summary>
    void RunTheRound(int car, float sinceLastDecisionS)
    {
        if (Cars.Driven[car] || Cars.Broken[car] || WheelIsHeldOver(car)) return;

        var bay = _parking.BayOf(car);
        if (bay >= 0 && _parking.HoldsTheBody(bay, Cars.PositionM[car]))
        {
            _round.StoodS[car] += sinceLastDecisionS;
            if (_round.StoodS[car] < _round.StandS[car]) return;
        }

        SetOffForAnotherBay(car);
    }

    /// <summary>
    /// <b>A stand begun</b>: how long this one lasts, drawn from the car's own stream so a car park stood full
    /// in one instant does not empty in it.
    /// </summary>
    void BeginTheStand(int car)
    {
        _round.StoodS[car] = 0f;
        _round.StandS[car] = Cars.Draw[car].NextFloat(_config.Driving.ParkedMinS, _config.Driving.ParkedMaxS);
    }

    /// <summary>
    /// <b>A free bay near a place drawn within reach of where the car stands</b>, from this car's own stream
    /// the way a beat's is (SRV-5), and the leg to it. A draw that finds no free bay near it is tried again on
    /// the next decision rather than searched for: a place drawn out of a hat is worth no more than the next.
    /// </summary>
    void SetOffForAnotherBay(int car)
    {
        ref var draw = ref Cars.Draw[car];
        var reachM = _config.Driving.RoundReachM * MathF.Sqrt(draw.NextFloat());
        var placeM = Cars.PositionM[car] + (Heading.Unit(draw.NextFloat() * MathF.Tau) * reachM);

        var bay = FreeBayNear(placeM, _config.PersonWalkWorthM);
        if (bay < 0 || bay == _parking.BayOf(car)) return;

        RoundsSetOff++;
        SendTo(car, _parking.CentreM(bay), bay);
    }
}
