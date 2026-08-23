using System.Numerics;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Parking;

/// <summary>
/// Every bay in the town, who is standing in it and who is on their way to it — and, settled with the
/// town and never afterwards, <b>how each one is reached</b>: the lane a car drives in from, the lane
/// it backs out onto, and the point of ground beside it a walker is aimed at.
/// </summary>
/// <remarks>
/// <para>
/// <b>The approach is a fact about the bay, not about the car.</b> Which lane a bay can be entered from
/// falls out of the template's own arithmetic (<see cref="BayTemplate"/>) and out of nothing else, so
/// it is worked out once at load; what a leg decides is only which of the two — and that is settled by
/// the route search, since both are handed to it as goals and the cheaper one wins.
/// </para>
/// <para>
/// <b>A reservation is not an occupancy.</b> A bay is reserved when a drive leg starts and occupied when
/// a car comes to rest in it; the two are separate because the whole point of the reservation is the
/// stretch of time when the car is somewhere else. A bay with either is not free.
/// </para>
/// <para>
/// <b>The way in is the bay's, not the car's</b> (WLK-3a): a walker is aimed at the ground off the
/// driver's door of a car standing squarely in the bay, which is a point the town settled when it
/// painted the bay. Worked out from where the car has actually come to rest, it moves every time
/// something nudges the car, and a body nudged out of a bay is a walk re-planned round the lot.
/// </para>
/// </remarks>
internal sealed class ParkingRegistry
{
    readonly Vector2[] _centreM;
    readonly float[] _headingRad;
    readonly Vector2[] _wayInM;
    readonly int[] _enterLane;
    readonly float[] _enterAlongM;
    readonly int[] _leaveLane;
    readonly float[] _leaveAlongM;
    readonly int[] _occupant;
    readonly int[] _reservedBy;
    readonly int[] _bayOfCar;
    readonly int[] _reservationOfCar;

    ParkingRegistry(int bays, int cars)
    {
        _centreM = new Vector2[bays];
        _headingRad = new float[bays];
        _wayInM = new Vector2[bays];
        _enterLane = new int[bays];
        _enterAlongM = new float[bays];
        _leaveLane = new int[bays];
        _leaveAlongM = new float[bays];
        _occupant = new int[bays];
        _reservedBy = new int[bays];
        _bayOfCar = new int[cars];
        _reservationOfCar = new int[cars];
        Array.Fill(_reservationOfCar, NoBay);
        Array.Fill(_occupant, NoCar);
        Array.Fill(_reservedBy, NoCar);
        Array.Fill(_bayOfCar, NoBay);
        Array.Fill(_enterLane, NoLane);
        Array.Fill(_leaveLane, NoLane);
    }

    public const int NoBay = -1;

    public const int NoCar = -1;

    /// <summary>A bay with no lane a car could reach it from — which is a bay no trip ever claims.</summary>
    public const int NoLane = -1;

    /// <summary>
    /// How far before the bay a forward-in template is staged, which is where a drive leg's route ends.
    /// Three car lengths, which is where a car drops to manoeuvring pace.
    /// </summary>
    const float StagedInCarLengths = 3f;

    public int BayCount => _centreM.Length;

    public Vector2 CentreM(int bay) => _centreM[bay];

    public float HeadingRad(int bay) => _headingRad[bay];

    /// <summary>The ground beside the driver's door of a car standing in this bay: what a walk to it aims at.</summary>
    public Vector2 WayInM(int bay) => _wayInM[bay];

    public int EnterLane(int bay) => _enterLane[bay];

    /// <summary>Where along that lane the template is staged — the place a drive leg's route is planned to.</summary>
    public float EnterAlongM(int bay) => _enterAlongM[bay];

    public bool CanBeEntered(int bay) => _enterLane[bay] != NoLane;

    public int LeaveLane(int bay) => _leaveLane[bay];

    public float LeaveAlongM(int bay) => _leaveAlongM[bay];

    public bool CanBeLeft(int bay) => _leaveLane[bay] != NoLane;

    public int OccupantOf(int bay) => _occupant[bay];

    public int ReservedBy(int bay) => _reservedBy[bay];

    public int BayOf(int car) => _bayOfCar[car];

    /// <summary>The bay this car is on its way to, or <see cref="NoBay"/>. A leg's claim, held from its first tick.</summary>
    public int ReservationOf(int car) => _reservationOfCar[car];

    /// <summary>Nobody in it and nobody on their way to it.</summary>
    public bool IsFree(int bay) => _occupant[bay] == NoCar && _reservedBy[bay] == NoCar;

    /// <summary>A bay claimed for a drive leg. <b>Only once a route to it exists</b> — which is the caller's question, asked before this one.</summary>
    public bool TryReserve(int bay, int car)
    {
        if (!IsFree(bay)) return false;

        _reservedBy[bay] = car;
        _reservationOfCar[car] = bay;
        return true;
    }

    public void GiveUpReservation(int car)
    {
        var bay = _reservationOfCar[car];
        if (bay != NoBay && _reservedBy[bay] == car) _reservedBy[bay] = NoCar;
        _reservationOfCar[car] = NoBay;
    }

    /// <summary>A car come to rest in a bay: the reservation becomes an occupancy, and the car remembers which.</summary>
    public void Occupy(int bay, int car)
    {
        _occupant[bay] = car;
        _reservedBy[bay] = NoCar;
        _reservationOfCar[car] = NoBay;
        _bayOfCar[car] = bay;
    }

    public void Vacate(int car)
    {
        var bay = _bayOfCar[car];
        if (bay == NoBay) return;

        if (_occupant[bay] == car) _occupant[bay] = NoCar;
        _bayOfCar[car] = NoBay;
    }

    /// <summary>
    /// The free bays a car may be parked in near a place, nearest first — <b>the choice layer</b>. A bay
    /// no car can be driven into is not one of them, however near it stands.
    /// </summary>
    public int BaysNear(Vector2 toM, float withinM, Span<int> into)
    {
        var count = 0;
        Span<float> distanceM = stackalloc float[into.Length];
        for (var bay = 0; bay < BayCount; bay++)
        {
            if (!IsFree(bay) || !CanBeEntered(bay)) continue;

            var farM = Vector2.Distance(_centreM[bay], toM);
            if (farM > withinM) continue;

            var slot = count < into.Length ? count : into.Length - 1;
            if (count == into.Length && farM >= distanceM[slot]) continue;

            while (slot > 0 && distanceM[slot - 1] > farM)
            {
                distanceM[slot] = distanceM[slot - 1];
                into[slot] = into[slot - 1];
                slot--;
            }

            distanceM[slot] = farM;
            into[slot] = bay;
            if (count < into.Length) count++;
        }

        return count;
    }

    public static ParkingRegistry Build(CityPlan plan, RoadGraph roads, SimConfig config, int cars)
    {
        var lots = plan.ParkingLots;
        var registry = new ParkingRegistry(lots.SpaceCount, cars);
        Span<ArcSeg> scratch = stackalloc ArcSeg[BayTemplate.MostArcs];

        for (var bay = 0; bay < lots.SpaceCount; bay++)
        {
            var centreM = lots.SpacePositionM[bay];
            var headingRad = lots.SpaceHeadingRad[bay];
            registry._centreM[bay] = centreM;
            registry._headingRad[bay] = headingRad;

            // The driver's door, on the flank TER-4a's own side rule puts it: a clear body's width off
            // the car, because a rigid walker cannot arrive at a point inside a solid box.
            var forward = Heading.Unit(headingRad);
            var door = new Vector2(-forward.Y, forward.X) * -config.RoadSideSign;
            registry._wayInM[bay] = centreM + door * (config.Car.WidthM * 0.5f + config.PersonDiameterM);

            var axleM = BayTemplate.RearAxleOfBayM(config, centreM, headingRad);
            var nearLane = roads.NearestLane(axleM, out _);
            if (nearLane < 0) continue;

            var farLane = roads.LaneReverse[nearLane];
            registry.Settle(config, roads, bay, axleM, centreM, headingRad, nearLane, scratch);
            if (farLane >= 0) registry.Settle(config, roads, bay, axleM, centreM, headingRad, farLane, scratch);
        }

        return registry;
    }

    /// <summary>
    /// One candidate lane, asked the two questions the templates answer: can a car be driven into this
    /// bay from it, and can one back out of the bay onto it. Both are refusals more often than not —
    /// a bay is square to its kerb, so the lane it is driven in from is the far one and the lane it is
    /// backed out onto is the near one.
    /// </summary>
    void Settle(
        SimConfig config, RoadGraph roads, int bay, Vector2 axleM, Vector2 centreM, float headingRad, int lane,
        Span<ArcSeg> scratch)
    {
        var arcs = roads.ArcsOf(lane);
        var lengthM = roads.LaneLengthM[lane];
        var abeamM = Spline.ProjectM(arcs, axleM, lengthM * 0.5f, lengthM);
        var abeam = Spline.SampleAt(arcs, abeamM);

        if (_enterLane[bay] == NoLane)
        {
            var stagedM = abeamM - config.Car.LengthM * StagedInCarLengths;
            if (stagedM >= 0f)
            {
                var staged = Spline.SampleAt(arcs, stagedM);
                var line = BayTemplate.TryLayEntry(
                    config, staged.PositionM, staged.HeadingRad, centreM, headingRad, scratch);
                if (line.Any)
                {
                    _enterLane[bay] = lane;
                    _enterAlongM[bay] = stagedM;
                }
            }
        }

        if (_leaveLane[bay] != NoLane) return;

        var exit = BayTemplate.TryLayExit(
            config, axleM, headingRad, abeam.PositionM, abeam.Direction, config.CarOffPathM, scratch);
        if (!exit.Any) return;

        _leaveLane[bay] = lane;
        _leaveAlongM[bay] = abeamM;
    }
}
