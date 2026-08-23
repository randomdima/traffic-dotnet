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

    /// <summary>Where a candidate's arcs are drawn while it is still a candidate, so a refusal writes over nothing.</summary>
    readonly ArcSeg[] _candidate = new ArcSeg[MostCandidateArcs];

    public ManeuverDesk(
        SimConfig config, CarFleet cars, TerrainGrid terrain, RoadGraph roads, LaneOccupancy occupancy,
        ParkingRegistry parking)
    {
        _config = config;
        _cars = cars;
        _terrain = terrain;
        _roads = roads;
        _occupancy = occupancy;
        _parking = parking;
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

    public int ReservationOf(int car) => _parking.ReservationOf(car);

    /// <summary>`P-2`'s success: the bay is the town's again the moment the car is out of it.</summary>
    public void VacateTheBay(int car) => _parking.Vacate(car);

    /// <summary>`P-17`'s success: the bay is this car's until something else drives it away.</summary>
    public void OccupyTheBay(int car, int bay) => _parking.Occupy(bay, car);

    /// <summary>`E-9` and `E-10`: a place held by a car that has stopped driving is a place removed from the town.</summary>
    public void GiveUpTheReservation(int car) => _parking.GiveUpReservation(car);

    /// <summary>
    /// The attempt counts, spent as an entry takes up. <b>Each is spent by the entry that has it</b> and
    /// given back by the leg or the jam it belongs to, which is what makes MAN-4's bounds a property of
    /// the catalogue rather than of the watchdog that notices them.
    /// </summary>
    public void SpendABackOff(int car) => _cars.BackOffs[car]++;


    public void SpendAReroute(int car) => _cars.Reroutes[car]++;

    public void SpendARecovery(int car) => _cars.Recoveries[car]++;

    /// <summary>
    /// The beat before the first look at the lane. <b>Two cars in neighbouring bays that start waiting on
    /// the same tick would otherwise take the same gap</b>, so the wait starts below zero by a share of
    /// the patience drawn from the car's own stream.
    /// </summary>
    public void BeginTheWait(int car) =>
        _cars.WaitedS[car] = -_cars.Draw[car].NextFloat(0f, _config.Ladder.GiveWayPatienceLeavingBayS * BeatOfThePatience);

    /// <summary>A fifth of the patience: long enough to break a row of bays apart, short enough not to be the wait.</summary>
    const float BeatOfThePatience = 0.2f;

    public void SpendTheWait(int car, float sinceS) => _cars.WaitedS[car] += sinceS;

    /// <summary>
    /// `P-2`'s question, asked of the lane the car is about to back onto: <b>how long before anything on
    /// it reaches the pose this car will occupy</b> — a time, never a distance, exactly as a walker asks
    /// at a kerb (§8 rule 8). Anything already in the mouth of the bay answers zero.
    /// </summary>
    /// <remarks>
    /// A row of parked cars must never hold a bay shut, and a car at town speed a street away must be
    /// waited for; a distance answers both wrongly. Past the give-way patience the gap is taken anyway —
    /// a car waiting out a jam is one more car in it — and the patience is jittered by the beat, so a row
    /// of cars that began waiting together does not all give up together either.
    /// </remarks>
    public bool GapIsClear(int car)
    {
        if (_cars.WaitedS[car] < 0f) return false;

        var lengthM = _cars.Line[car].LengthM;
        var endM = Spline.SampleAt(_cars.LineOf(car), lengthM).PositionM;
        var lane = _roads.NearestLane(endM, out var mouthM);
        if (lane < 0) return true;

        // The ground the car will be standing on when it is out. It is claimed before it is looked at, so
        // that two cars in neighbouring bays which each found the lane clear on the same tick do not both
        // back onto it — the argument a car crossing a junction takes its sections on, over a stretch of lane.
        var halfM = _config.Car.LengthM * 0.5f;
        var way = _occupancy.WayOfLane(lane);
        if (!Claim(car, way, mouthM - halfM, mouthM + halfM)) return false;
        if (_cars.WaitedS[car] >= _config.Ladder.GiveWayPatienceLeavingBayS) return true;

        if (!_occupancy.BehindBody(way, mouthM - halfM, mouthM - _config.CarSightM, car, out var behind)) return true;

        // Anything already in the mouth of the bay answers zero, whatever it is doing; anything else is
        // asked as a time and never a distance (§8 rule 8), because a row of parked cars must never hold a
        // bay shut and a car at town speed a street away must be waited for.
        var gapM = mouthM - halfM - behind.ToM;
        if (gapM <= 0f) return false;
        if (behind.AlongMps <= _config.Driving.StopSpeedMps) return true;

        return gapM / behind.AlongMps >= lengthM / MathF.Max(0.1f, _config.Car.ReverseMaxMps);
    }

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

    /// <summary>Whether a body standing here, pointing this way, is on ground a car may drive on — centre, nose and tail.</summary>
    public bool BodyStandsOnDrivableGround(Vector2 centreM, Vector2 forward)
    {
        var halfM = _config.Car.LengthM * 0.5f;
        return _terrain.At(centreM).Drivable
               && _terrain.At(centreM + forward * halfM).Drivable
               && _terrain.At(centreM - forward * halfM).Drivable;
    }

    public bool StandsOnDrivableGround(int car) =>
        BodyStandsOnDrivableGround(_cars.PositionM[car], ForwardOf(car));

    /// <summary>
    /// How much ground a straight along the car's own axis actually has, <b>walked before committing and
    /// truncated where the road ends</b>. The whole body is tested at every step, because a nose that
    /// clears the kerb while the tail does not is a car parked across a verge.
    /// </summary>
    public float RoomAlongTheAxisM(int car, bool backwards)
    {
        var forward = ForwardOf(car);
        var travel = backwards ? -forward : forward;
        var stepM = _config.Car.LengthM * GroundStepInCarLengths;
        var roomM = 0f;
        while (roomM + stepM <= _config.Car.ReverseBoundM
               && BodyStandsOnDrivableGround(_cars.PositionM[car] + travel * (roomM + stepM), forward))
        {
            roomM += stepM;
        }

        return roomM >= _config.Car.LengthM * 0.5f ? roomM : 0f;
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

        var forward = ForwardOf(car);
        var stepM = _config.Car.LengthM * GroundStepInCarLengths;
        for (var outM = stepM; outM <= _config.Car.LengthM * SearchInCarLengths; outM += stepM)
        {
            if (BodyStandsOnDrivableGround(_cars.PositionM[car] + forward * outM, forward))
            {
                reachM = outM;
                return true;
            }

            if (!BodyStandsOnDrivableGround(_cars.PositionM[car] - forward * outM, forward)) continue;

            reachM = outM;
            backwards = true;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Whether the ground under a candidate template will hold a car all the way along it. <b>The shape
    /// is the car's and the room is the town's</b>: whether a junction is wide enough for a turn-around
    /// or a lane wide enough for a swerve is a fact about a town, and asking the terrain is the only way
    /// to have one answer to it rather than a table of junction sizes beside a table of car radii.
    /// </summary>
    public bool GroundAdmits(ReadOnlySpan<ArcSeg> line, float lengthM)
    {
        var stepM = _config.Car.LengthM * GroundStepInCarLengths;
        for (var alongM = 0f; alongM <= lengthM; alongM += stepM)
        {
            var pose = Spline.SampleAt(line, alongM);

            // The template is the rear axle's, so the body standing on it is centred half a wheelbase
            // ahead of the line (CAR-4a).
            var centreM = pose.PositionM + pose.Direction * _config.Car.WheelbaseM * 0.5f;
            if (!BodyStandsOnDrivableGround(centreM, pose.Direction)) return false;
        }

        return true;
    }

    Vector2 ForwardOf(int car)
    {
        var headingRad = _cars.HeadingRad[car];
        return Heading.Unit(headingRad);
    }
}
