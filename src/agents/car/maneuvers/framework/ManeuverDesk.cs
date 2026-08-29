using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Terrain;

namespace TrafficSimulation.Agents.Car.Maneuvers;

/// <summary>
/// <b>What a driver has to hand.</b> The questions an entry's <c>Sa</c> asks about the ground it is
/// standing on, and the geometry it lays to drive its own procedure — nothing more, and in particular
/// no route, no price table and no trip.
/// </summary>
/// <remarks>
/// <para>
/// It exists so the catalogue is not handed a town. An entry that could reach the composition could
/// reach anything from the signal heads to the walkers' stages, and the discipline that keeps a
/// manoeuvre a bounded procedure is that <b>it can only ask what a driver could see and only do what a
/// driver could do</b>. The three things that genuinely need the whole town go the other way, as
/// <see cref="DriveOrder"/>s the town carries.
/// </para>
/// <para>
/// <b>Every line is walked before it is written.</b> Candidates are cast from this class's own scratch
/// rather than from the car's arcs: a refusal that had already overwritten the line the car is driving
/// leaves it holding a line whose length and arcs disagree, which is a car three hundred metres from a
/// line it is sitting on.
/// </para>
/// </remarks>
internal sealed partial class ManeuverDesk
{
    readonly SimConfig _config;
    readonly CarFleet _cars;
    readonly TerrainGrid _terrain;
    readonly RoadGraph _roads;
    readonly LaneOccupancy _occupancy;
    readonly ParkingRegistry _parking;
    readonly BayWays _bayWays;

    /// <summary>Where a candidate's arcs are drawn while it is still a candidate, so a refusal writes over nothing.</summary>
    readonly ArcSeg[] _candidate = new ArcSeg[MostCandidateArcs];

    public ManeuverDesk(
        SimConfig config, CarFleet cars, TerrainGrid terrain, RoadGraph roads, LaneOccupancy occupancy,
        ParkingRegistry parking, BayWays bayWays)
    {
        _config = config;
        _cars = cars;
        _terrain = terrain;
        _roads = roads;
        _occupancy = occupancy;
        _parking = parking;
        _bayWays = bayWays;
    }

    /// <summary>The longest template in the catalogue is the swerve, at five pieces.</summary>
    const int MostCandidateArcs = RoadTemplates.MostSwerveArcs;

    /// <summary>How far along its own axis a car looks for ground it may stand on. Past this it is not off the road, it is somewhere else.</summary>
    const float SearchInCarLengths = 4f;

    /// <summary>
    /// How far a candidate template is walked between ground tests: half a car, which is the coarsest
    /// step at which the body's own nose and tail still overlap between one test and the next.
    /// </summary>
    const float GroundStepInCarLengths = 0.5f;

    public SimConfig Config => _config;

    /// <summary>The bay this car stands in, and the one its leg holds — both −1 where there is none.</summary>
    public int BayOf(int car) => _parking.BayOf(car);

    /// <summary>The bay this leg is on its way to, or <see cref="ParkingRegistry.NoBay"/>.</summary>
    public int BookingOf(int car) => _parking.BookingOf(car);

    /// <summary>The bay this leg is turning in (GEN-4l), or <see cref="ParkingRegistry.NoBay"/>.</summary>
    public int TurnOf(int car) => _parking.TurnOf(car);

    /// <summary>
    /// <b>The heading of the lane this leg comes back down</b> (`P-19`), read where the car is standing —
    /// what a car turning on the spot is working round to. False where this leg is not coming back at all.
    /// </summary>
    public bool TheWayBack(int car, out float headingRad)
    {
        headingRad = 0f;
        var back = _cars.TurnsBackOn[car];
        if (back < 0) return false;

        ref readonly var build = ref _cars.BuildOf(car);
        var axleM = CarFollower.RearAxleM(build, _cars.PositionM[car], Heading.Unit(_cars.HeadingRad[car]));
        var arcs = _roads.ArcsOf(back);
        var lengthM = _roads.LaneLengthM[back];
        var at = Spline.SampleAt(arcs, Spline.ProjectM(arcs, axleM, lengthM * 0.5f, lengthM));
        headingRad = MathF.Atan2(at.Direction.Y, at.Direction.X);
        return true;
    }

    /// <summary>
    /// And whether the body is round far enough to be driving it: within the tolerance a turn through a
    /// junction is called straight at, which is the angle the road itself is read to.
    /// </summary>
    public bool PointsTheWayBack(int car) =>
        TheWayBack(car, out var headingRad)
        && MathF.Abs(Spline.WrapRad(headingRad - _cars.HeadingRad[car]))
        <= _config.Road.TurnStraightToleranceDeg * MathF.PI / 180f;

    /// <summary>
    /// <b>And whether it is on that lane as well as pointing along it</b> — the last thing a turn on the
    /// spot owes whoever it hands to. A car left standing off the line it is about to be given is a car
    /// the follower calls lost the moment it is handed one.
    /// </summary>
    public bool IsOnTheWayBack(int car) =>
        PointsTheWayBack(car) && OffTheWayBackM(car) <= _config.CarOffPathM;

    /// <summary>How far the axle stands off the line of the lane this leg comes back down, or infinity where there is none.</summary>
    float OffTheWayBackM(int car)
    {
        var back = _cars.TurnsBackOn[car];
        if (back < 0) return float.PositiveInfinity;

        ref readonly var build = ref _cars.BuildOf(car);
        var axleM = CarFollower.RearAxleM(build, _cars.PositionM[car], Heading.Unit(_cars.HeadingRad[car]));
        var arcs = _roads.ArcsOf(back);
        var lengthM = _roads.LaneLengthM[back];
        var at = Spline.SampleAt(arcs, Spline.ProjectM(arcs, axleM, lengthM * 0.5f, lengthM));
        return (at.PositionM - axleM).Length();
    }

    /// <summary>
    /// <b>Whether the way this leg's line finishes at is one the car reverses into</b> (GEN-4j). A route is
    /// driven forwards, so such a way is not threaded onto the line: the line stops where it begins and the
    /// shape is laid again from the pose the car came to rest in, in the gear it is drawn for.
    /// </summary>
    public bool ReversesIntoTheBay(int car) =>
        _cars.TailWayOf(car) is var way and not CarFleet.NoWay && _bayWays.IsDrivenInReverse(way);

    /// <summary>
    /// <b>The bay a manoeuvre of this leg has in hand</b>: the one the car is standing in, then the one it
    /// is turning in, then the place it is booked into. They are in the order a leg meets them, so an entry
    /// that asks gets the bay it is working on now rather than the one at the end of the leg.
    /// </summary>
    public int BayInHand(int car) =>
        BayOf(car) is var standing and >= 0 ? standing
        : TurnOf(car) is var turning and >= 0 ? turning
        : BookingOf(car);

    /// <summary>
    /// `P-2`'s success: the bay is the town's again the moment the car is out of it — <b>the standing and
    /// the turn alike</b>, since a car that has driven out of a bay it turned in (GEN-4l) is a car with no
    /// further use for it.
    /// </summary>
    public void VacateTheBay(int car)
    {
        _parking.Vacate(car);
        _parking.LeaveTheTurn(car);
    }

    /// <summary>`P-17`'s success: the bay is this car's until something else drives it away.</summary>
    public void OccupyTheBay(int car, int bay) => _parking.Occupy(bay, car);

    /// <summary>
    /// `E-9` and `E-10`: a place held by a car that has stopped driving towards it is a place removed from
    /// the town.
    /// </summary>
    public void GiveUpTheBooking(int car) => _parking.Release(car);

    /// <summary>
    /// <b>A bay booked for a leg</b> — the register's, because a booking is held over ground the car has no
    /// line to yet and is the one hold in the town that is not a piece of road (<see cref="ParkingRegistry"/>).
    /// </summary>
    public bool BookTheBay(int car, int bay) => _parking.Book(car, bay);

    /// <summary>
    /// The attempt counts, spent as an entry takes up. <b>Each is spent by the entry that has it</b> and
    /// given back by the leg or the jam it belongs to, which is what makes MAN-4's bounds a property of
    /// the catalogue rather than of the watchdog that notices them.
    /// </summary>
    public void SpendABackOff(int car) => _cars.BackOffs[car]++;


    public void SpendAReroute(int car) => _cars.Reroutes[car]++;

    public void SpendARecovery(int car) => _cars.Recoveries[car]++;

    /// <summary>
    /// <b>The stretch of lane a manoeuvre is about to put a body on</b>, taken if nobody else has taken
    /// ground it runs over. It is what makes a claim a reservation rather than a note, and it is re-laid
    /// into the index from the car's own field every tick, so nothing here has to be released on a wreck.
    /// </summary>
    bool Claim(int car, int way, float fromM, float toM)
    {
        if (_cars.ClaimWay[car] != way && _occupancy.ClaimedByAnother(way, fromM, toM, car)) return false;

        _cars.ClaimWay[car] = way;
        _cars.ClaimFromM[car] = fromM;
        _cars.ClaimToM[car] = toM;
        return true;
    }

    /// <summary>
    /// `E-4`'s claim: the stretch of the car's own lane the swerve swings out of and back into. <b>The
    /// oncoming lane is not claimed</b> — crossing the centreline is licensed for exactly this and for
    /// nothing else (CAR-6.2b), and a claim on ground the other stream is entitled to would be a car
    /// reserving the wrong side of the road.
    /// </summary>
    /// <param name="fromM">
    /// Where the car stood along its route's line when the swerve was decided on. <b>It is the caller's
    /// because laying the template has already thrown it away</b>: committing a template restarts the
    /// progress measure at its own origin, so reading the car's progress here claims a stretch at the top of
    /// the lane rather than the stretch the car is on — which leaves the traffic behind unheld and marks
    /// road nobody is going to drive.
    /// </param>
    public void ClaimTheSwerve(int car, float fromM, float passM)
    {
        var lane = _cars.LaneOf(car);
        if (lane < 0) return;

        // The line's first lane starts at the line's own origin, so the car's progress along it is already
        // that lane's own metre.
        Claim(car, _occupancy.WayOfLane(lane), fromM, fromM + passM);
    }

    /// <summary>
    /// Whether a body of this length standing here, pointing this way, is on ground a car may drive on —
    /// centre, nose and tail. <b>The length is the body's own</b> (CAR-11): a truck's nose reaches over a
    /// kerb a hatchback's stops short of.
    /// </summary>
    public bool BodyStandsOnDrivableGround(Vector2 centreM, Vector2 forward, float halfLengthM)
    {
        return _terrain.At(centreM).Drivable
               && _terrain.At(centreM + forward * halfLengthM).Drivable
               && _terrain.At(centreM - forward * halfLengthM).Drivable;
    }

    public bool StandsOnDrivableGround(int car) =>
        BodyStandsOnDrivableGround(_cars.PositionM[car], ForwardOf(car), _cars.BuildOf(car).HalfLengthM);

    /// <summary>
    /// How much ground a straight along the car's own axis actually has, <b>walked before committing and
    /// truncated where the road ends</b>. The whole body is tested at every step, because a nose that
    /// clears the kerb while the tail does not is a car parked across a verge.
    /// </summary>
    public float RoomAlongTheAxisM(int car, bool backwards)
    {
        ref readonly var build = ref _cars.BuildOf(car);
        var forward = ForwardOf(car);
        var travel = backwards ? -forward : forward;
        var stepM = build.LengthM * GroundStepInCarLengths;
        var roomM = 0f;
        while (roomM + stepM <= _config.Car.ReverseBoundM
               && BodyStandsOnDrivableGround(
                   _cars.PositionM[car] + travel * (roomM + stepM), forward, build.HalfLengthM))
        {
            roomM += stepM;
        }

        return roomM >= build.HalfLengthM ? roomM : 0f;
    }

    /// <summary>
    /// `E-8`'s question, and its <c>Sa</c>: <b>the nearest pose along the car's own axis where the whole
    /// body lands on drivable ground</b>, searched outward both ways. The path that manoeuvre can issue
    /// is a single straight, so "the nearest legal lane point" — which is generally off to one side —
    /// would have the car drive the right distance in the wrong direction.
    /// </summary>
    public bool StraightToLegalGround(int car, out float reachM, out bool backwards)
    {
        reachM = 0f;
        backwards = false;

        ref readonly var build = ref _cars.BuildOf(car);
        var forward = ForwardOf(car);
        var stepM = build.LengthM * GroundStepInCarLengths;
        for (var outM = stepM; outM <= build.LengthM * SearchInCarLengths; outM += stepM)
        {
            if (BodyStandsOnDrivableGround(_cars.PositionM[car] + forward * outM, forward, build.HalfLengthM))
            {
                reachM = outM;
                return true;
            }

            if (!BodyStandsOnDrivableGround(
                    _cars.PositionM[car] - forward * outM, forward, build.HalfLengthM))
            {
                continue;
            }

            reachM = outM;
            backwards = true;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Whether the ground under a candidate template will hold a car all the way along it. <b>The shape
    /// is the car's and the room is the town's</b>: whether a lane is wide enough for a swerve or a bay
    /// reachable from the pose a car is in is a fact about a town, and asking the terrain is the only way
    /// to have one answer to it rather than a table of street widths beside a table of car radii.
    /// </summary>
    public bool GroundAdmits(int car, ReadOnlySpan<ArcSeg> line, float lengthM)
    {
        ref readonly var build = ref _cars.BuildOf(car);
        var stepM = build.LengthM * GroundStepInCarLengths;
        for (var alongM = 0f; alongM <= lengthM; alongM += stepM)
        {
            var pose = Spline.SampleAt(line, alongM);

            // The template is the rear axle's, so the body standing on it is centred this car's own
            // axle-to-middle ahead of the line (CAR-4a).
            var centreM = pose.PositionM + pose.Direction * build.CentreAheadOfAxleM;
            if (!BodyStandsOnDrivableGround(centreM, pose.Direction, build.HalfLengthM)) return false;
        }

        return true;
    }

    Vector2 ForwardOf(int car)
    {
        var headingRad = _cars.HeadingRad[car];
        return Heading.Unit(headingRad);
    }
}
