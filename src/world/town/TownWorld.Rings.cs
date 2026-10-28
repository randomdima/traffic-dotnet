using System.Numerics;
using TrafficSimulation.Agents.Person.Actions;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The showroom's walkers</b> (PER-30): every walker look once, each round a ring of its own down the middle of the
/// car park (<see cref="ShowroomPlan"/>), so its walk is seen in every facing its sheet draws. A ring is walked on no way
/// of a network and claims nothing; the follower walks the body there as it walks a hand's (CTL-6).
/// </summary>
internal sealed partial class TownWorld
{
    /// <summary>The centre of each walker's ring, on the showroom; empty on any other map.</summary>
    Vector2[] _ringCentreM = [];

    /// <summary>Where a walker on show is stood: on its ring, east of the centre the plan put.</summary>
    Vector2 RingStartM(Vector2 centreM) => centreM + new Vector2(_config.Person.ShowroomRingM, 0f);

    /// <summary>The ring a walker on show walks, read at each of its decisions (<see cref="WalkTheRing"/>).</summary>
    void GiveItItsRing(int person, Vector2 centreM)
    {
        if (_ringCentreM.Length == 0) _ringCentreM = new Vector2[People.Capacity];

        _ringCentreM[person] = centreM;
    }

    /// <summary>
    /// <b>A look on show walks its ring</b>, aimed a lead round it from where it stands — each decision, so it neither
    /// arrives nor leaves the ring. A walker let go by a hand takes its ring up again from wherever the hand left it.
    /// </summary>
    void WalkTheRing(int person)
    {
        Enter(person, PersonAction.Show);
        People.Walking[person] = true;
        People.DestinationM[person] = AimRoundTheRing(
            _ringCentreM[person], People.PositionM[person], People.HeadingRad[person], _config.Person.ShowroomRingM,
            float.DegreesToRadians(_config.Person.ShowroomRingLeadDeg));
    }

    /// <summary>
    /// <b>Where a walker on show aims</b> (PER-30): the point of its ring <paramref name="leadRad"/> round from the
    /// walker's own bearing off the centre, the way the map's angles grow — so a body on the ring walks round it and a
    /// body off it is drawn back on. At the very centre, where no bearing is nearer than another, it is straight ahead.
    /// </summary>
    internal static Vector2 AimRoundTheRing(
        Vector2 centreM, Vector2 positionM, float headingRad, float radiusM, float leadRad)
    {
        var outM = positionM - centreM;
        var (sin, cos) = outM.LengthSquared() > 0f
            ? Trig.SinCos(Trig.Atan2(outM.Y, outM.X) + leadRad)
            : Trig.SinCos(headingRad);
        return centreM + (new Vector2(cos, sin) * radiusM);
    }

    /// <summary>Whether this walker is a look on show, walking its ring rather than a trip, and under no hand (PER-30).</summary>
    bool IsOnShow(int person) => _ringCentreM.Length > 0 && People.Action[person] != PersonAction.Hand;
}
