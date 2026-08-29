using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Parking;

/// <summary>
/// Every bay in the town: where it stands, where a walk to it is aimed, who is standing in it, who is on
/// their way to it and who is turning in it. <b>How each one is reached is <see cref="BayWays"/>'s</b> —
/// the ways at a bay, laid with the town like the joins through a junction.
/// </summary>
/// <remarks>
/// <para>
/// <b>The booking is a register and the movement is not</b> (GEN-4g), and the line between the two is the
/// whole of what this type is for. A bay a leg is on its way to is held here, in an index of the bays
/// themselves, because the hold begins when the trip picks the bay and the walker sets off — minutes before
/// anybody is at the wheel, over ground the car has no line to and no reservation on. Nothing else in the
/// town holds ground it is not driving towards, so nothing else in the town could answer it.
/// </para>
/// <para>
/// <b>What the booking is not is a second opinion about the road.</b> It says which bay a leg is aimed at
/// and no more: the ground the car takes getting there is its reservation on the bay's own way, laid and
/// answered like any other (TER-4c.1), and a body already standing where a booked car means to go refuses
/// it there rather than here.
/// </para>
/// <para>
/// <b>The bays are indexed by where they stand</b> (<see cref="BaysNear"/>), because the question a trip
/// asks is a question about a place: the free bays within a walk of a door, nearest first. Walked over
/// every bay in the town it was a scan per leg drawn.
/// </para>
/// <para>
/// <b>The way in is the bay's, not the car's</b> (GEN-4e): a walker is aimed at the ground off the driver's
/// door of a car standing squarely in the middle of the bay (GEN-4i), which is a point the town settled when
/// it painted the bay.
/// Worked out from where the car has actually come to rest, it moves every time something nudges the car,
/// and a body nudged out of a bay is a walk re-planned round the lot.
/// </para>
/// </remarks>
internal sealed class ParkingRegistry
{
    readonly BayWays _ways;
    readonly Vector2[] _centreM;
    readonly float[] _headingRad;
    readonly Vector2[] _doorM;
    readonly int[] _bayOfCar;
    readonly int[] _carInBay;
    readonly int[] _bookedBy;
    readonly int[] _bookingOfCar;
    readonly int[] _turningIn;
    readonly int[] _turnOfCar;
    readonly int[] _heldFor;
    readonly int[] _candidates;
    readonly BucketGrid _near;
    readonly float _spaceLengthM;
    readonly float _spaceWidthM;

    ParkingRegistry(BayWays ways, BucketGrid near, int bays, int cars, float spaceLengthM, float spaceWidthM)
    {
        _ways = ways;
        _near = near;
        _spaceLengthM = spaceLengthM;
        _spaceWidthM = spaceWidthM;
        _centreM = new Vector2[bays];
        _headingRad = new float[bays];
        _doorM = new Vector2[bays];
        _carInBay = new int[bays];
        _bookedBy = new int[bays];
        _turningIn = new int[bays];
        _heldFor = new int[bays];
        _candidates = new int[bays];
        _bayOfCar = new int[cars];
        _bookingOfCar = new int[cars];
        _turnOfCar = new int[cars];
        Array.Fill(_carInBay, Nobody);
        Array.Fill(_bookedBy, Nobody);
        Array.Fill(_turningIn, Nobody);
        Array.Fill(_heldFor, Nobody);
        Array.Fill(_bayOfCar, NoBay);
        Array.Fill(_bookingOfCar, NoBay);
        Array.Fill(_turnOfCar, NoBay);
    }

    public const int NoBay = -1;

    /// <summary>Nobody standing in a bay, nobody on their way to one and nobody holding one.</summary>
    public const int Nobody = -1;

    public int BayCount => _centreM.Length;

    public Vector2 CentreM(int bay) => _centreM[bay];

    public float HeadingRad(int bay) => _headingRad[bay];

    /// <summary>
    /// The ground beside the driver's door of a car standing in this bay: what a walk to it aims at.
    /// <b>Which flank of the space that is is the standing's</b> (GEN-4j) — a car backed in has its doors on
    /// the other side — and it is still the bay's own fact rather than the parked body's (GEN-4e).
    /// </summary>
    public Vector2 WayInM(int bay, bool noseIn) =>
        _centreM[bay] + (noseIn ? _doorM[bay] : -_doorM[bay]);

    /// <summary>Whether the town laid this bay a way at all, which is the one question there is: it is driven both ways.</summary>
    public bool CanBeReached(int bay) => _ways.CanBeReached(bay);

    /// <summary>
    /// <b>Whether a body this size stands inside a bay at all</b> (CAR-11b). The spaces are one size,
    /// painted for the nominal car with a margin either side, and a car longer than that is one parked
    /// across the aisle behind it — so it is asked before a line into a bay is laid rather than discovered
    /// once the car is in it.
    /// </summary>
    public bool Takes(float lengthM, float widthM) => lengthM <= _spaceLengthM && widthM <= _spaceWidthM;

    /// <summary>The bay this car has been left in, or <see cref="NoBay"/>.</summary>
    public int BayOf(int car) => _bayOfCar[car];

    /// <summary>And the car standing in this bay, or <see cref="Nobody"/> — the same fact from the other end.</summary>
    public int CarInBay(int bay) => _carInBay[bay];

    /// <summary>And the bay its leg is on its way to.</summary>
    public int BookingOf(int car) => _bookingOfCar[car];

    /// <summary>And the bay it is turning in on the way there (GEN-4l), which is the other one it can hold.</summary>
    public int TurnOf(int car) => _turnOfCar[car];

    /// <summary>Nobody standing in it, nobody on their way to it, nobody holding it, and a way in.</summary>
    public bool IsFree(int bay) => IsFreeFor(Nobody, bay);

    /// <summary>
    /// The same question asked by a car that may have a bay of its own (GEN-4k). <b>A held bay is free to
    /// its holder and to nobody else</b>, whether or not the holder is standing in it: an apron whose
    /// vehicle is out on an errand is still that vehicle's apron.
    /// </summary>
    public bool IsFreeFor(int car, int bay) =>
        _carInBay[bay] == Nobody && _bookedBy[bay] == Nobody && _ways.CanBeReached(bay)
        && (_turningIn[bay] == Nobody || _turningIn[bay] == car)
        && (_heldFor[bay] == Nobody || _heldFor[bay] == car);

    /// <summary>
    /// <b>A bay taken out of the town before there is a vehicle to put in it</b> (GEN-4k) — the first half
    /// of an apron's hold. Held this way it is free to nobody at all, which is what lets an apron be
    /// claimed before the plan's own cars are stood and still be there when they have been.
    /// </summary>
    public void HoldTheApron(int bay) => _heldFor[bay] = Reserved;

    /// <summary>
    /// And the second half: <b>the bay kept for one named vehicle for the whole run</b>. It is a hold on a
    /// place and not a booking — a booking is what a leg under way has, and this outlives every leg the
    /// vehicle drives.
    /// </summary>
    public void HoldForTheCar(int bay, int car) => _heldFor[bay] = car;

    /// <summary>Which vehicle this bay is held for, <see cref="Reserved"/>, or <see cref="Nobody"/>.</summary>
    public int HeldFor(int bay) => _heldFor[bay];

    /// <summary>An apron bay whose vehicle has not been stood in it yet. Free to nobody, which is the point.</summary>
    public const int Reserved = -2;

    /// <summary>
    /// <b>A slot of a depot's wreck yard</b> (EVA-2), held for whatever the evacuator brings to it and for
    /// nobody else — the one hold in the town that never names a vehicle.
    /// </summary>
    /// <remarks>
    /// It is not <see cref="Reserved"/>, which is a bay waiting for the vehicle that will hold it for the
    /// rest of the run. A yard slot is empty most of the time on purpose: what stands in it is whichever
    /// wreck was fetched last, and that car is an ordinary one again half a minute later.
    /// </remarks>
    public const int TheYard = -3;

    /// <summary>A slot of the yard taken out of the town for the whole run, whatever happens to be standing in it.</summary>
    public void HoldForTheYard(int bay) => _heldFor[bay] = TheYard;

    /// <summary>
    /// <b>A bay booked for a leg</b>, and the one that leg held before it given back — a car is aimed at one
    /// place at a time.
    /// </summary>
    public bool Book(int car, int bay)
    {
        if (!IsFreeFor(car, bay)) return false;

        Release(car);
        _bookingOfCar[car] = bay;
        _bookedBy[bay] = car;
        return true;
    }

    /// <summary>
    /// The booking given back: <b>a place held by a car that has stopped driving towards it is a place
    /// removed from the town</b>, so every way a leg can end gives it up.
    /// </summary>
    public void Release(int car)
    {
        var bay = _bookingOfCar[car];
        if (bay == NoBay) return;

        _bookingOfCar[car] = NoBay;
        _bookedBy[bay] = Nobody;
    }

    /// <summary>
    /// <b>A bay a leg is turning in</b> (GEN-4l), and the second hold a car may have: it is coming back the
    /// other way from here, so the place it is going to is still booked and still its. Refused, like every
    /// other hold, where the bay is not free to this car.
    /// </summary>
    /// <remarks>
    /// It is a booking and lives inside one leg, so every way that leg can stop wanting it gives it back —
    /// the car driving out of the bay, the leg taking another place, the car being stood down. What it is
    /// not is an occupancy: nobody comes to rest here, and a car turning is one nobody may walk to.
    /// </remarks>
    public bool TakeTheTurn(int car, int bay)
    {
        if (!IsFreeFor(car, bay)) return false;

        LeaveTheTurn(car);
        _turnOfCar[car] = bay;
        _turningIn[bay] = car;
        return true;
    }

    /// <summary>The turn given back — the bay is the town's again the moment the leg has stopped wanting it.</summary>
    public void LeaveTheTurn(int car)
    {
        var bay = _turnOfCar[car];
        if (bay == NoBay) return;

        _turnOfCar[car] = NoBay;
        _turningIn[bay] = Nobody;
    }

    /// <summary>
    /// A car come to rest in a bay. <b>The booking becomes an occupancy</b>: the leg that was aimed here has
    /// arrived, and what holds the bay from now on is the body standing in it.
    /// </summary>
    public void Occupy(int bay, int car)
    {
        Vacate(car);
        Release(car);
        LeaveTheTurn(car);
        _bayOfCar[car] = bay;
        _carInBay[bay] = car;
    }

    public void Vacate(int car)
    {
        var bay = _bayOfCar[car];
        if (bay == NoBay) return;

        _bayOfCar[car] = NoBay;
        _carInBay[bay] = Nobody;
    }

    /// <summary>
    /// The free bays a car may be parked in near a place, nearest first — <b>the choice layer</b>. A bay no
    /// car can be driven into is not one of them, however near it stands.
    /// </summary>
    /// <remarks>
    /// The index hands back the bays whose bucket the walk reaches, which is a superset; the distance and
    /// the register throw the rest out. <see cref="_candidates"/> is the town's own bay count, so the query
    /// can never come back truncated.
    /// </remarks>
    public int BaysNear(Vector2 toM, float withinM, Span<int> into)
    {
        var count = 0;
        Span<float> distanceM = stackalloc float[into.Length];
        var candidates = _near.Query(toM, withinM, _candidates);
        for (var slot = 0; slot < candidates; slot++)
        {
            var bay = _candidates[slot];
            var farM = Vector2.Distance(_centreM[bay], toM);
            if (farM > withinM) continue;

            var at = count < into.Length ? count : into.Length - 1;
            if (count == into.Length && farM >= distanceM[at]) continue;
            if (!IsFree(bay)) continue;

            while (at > 0 && distanceM[at - 1] > farM)
            {
                distanceM[at] = distanceM[at - 1];
                into[at] = into[at - 1];
                at--;
            }

            distanceM[at] = farM;
            into[at] = bay;
            if (count < into.Length) count++;
        }

        return count;
    }

    public static ParkingRegistry Build(CityPlan plan, BayWays ways, SimConfig config, int cars)
    {
        var lots = plan.ParkingLots;
        var registry = new ParkingRegistry(
            ways, Index(plan), lots.SpaceCount, cars, config.ParkingSpaceLengthM, config.ParkingSpaceWidthM);

        for (var bay = 0; bay < lots.SpaceCount; bay++)
        {
            var centreM = lots.SpacePositionM[bay];
            var headingRad = lots.SpaceHeadingRad[bay];
            registry._centreM[bay] = centreM;
            registry._headingRad[bay] = headingRad;

            // The driver's door, on the flank TER-4a's own side rule puts it: a clear body's width off
            // the car, because a rigid walker cannot arrive at a point inside a solid box. The car stands
            // square in the middle of its bay (GEN-4i), so the body's middle is the bay's own centre — and
            // the offset is carried rather than the point, because a car backed in has its doors on the
            // other flank and turning the body about its middle turns this with it.
            var forward = Heading.Unit(headingRad);
            var door = new Vector2(-forward.Y, forward.X) * -config.RoadSideSign;
            registry._doorM[bay] = door * ((config.Car.WidthM * 0.5f) + config.PersonDiameterM);
        }

        // A bay is a place in the index and not a circle: what is measured against the query is the
        // distance to its centre, and the lot it belongs to is already the bucket.
        registry._near.Rebuild(registry._centreM, new float[lots.SpaceCount], lots.SpaceCount);
        return registry;
    }

    /// <summary>
    /// The index the bays are binned into, <b>bucketed at the lot</b> — the cluster they actually arrive in.
    /// A handful of spaces along one chord share a bucket, so the ring a walk searches is lots and not bays.
    /// </summary>
    static BucketGrid Index(CityPlan plan)
    {
        var lots = plan.ParkingLots;
        var bucketM = plan.CellSizeM;
        foreach (var halfExtentM in lots.HalfExtentM) bucketM = MathF.Max(bucketM, halfExtentM.Length() * 2f);

        return new BucketGrid(plan.WorldSizeM, bucketM);
    }
}
