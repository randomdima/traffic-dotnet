using System.Numerics;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.Agents.Car.Maneuvers;

/// <summary>
/// The geometry a manoeuvre drives when it does not drive the route: the two bay templates, the recovery
/// straight, the swerve and the counter-swing. <b>Every one of them is walked before it is written</b>,
/// and a refusal leaves the line the car is holding exactly as it was.
/// </summary>
internal sealed partial class ManeuverDesk
{
    /// <summary>
    /// `P-2` and `P-16`'s line: the mirror of the template the car was driven in on, to be driven
    /// backwards. The geometry alone, because two entries drive it — leaving, and on the way to a second
    /// attempt at the same bay.
    /// </summary>
    public bool LayTheExitLine(int car, int bay)
    {
        if (bay < 0 || !_parking.CanBeLeft(bay)) return false;

        var lane = _parking.LeaveLane(bay);
        var at = Spline.SampleAt(_roads.ArcsOf(lane), _parking.LeaveAlongM(bay));
        var headingRad = _cars.HeadingRad[car];
        var line = BayTemplate.TryLayExit(
            _config, CarFollower.RearAxleM(_config, _cars.PositionM[car], headingRad), headingRad, at.PositionM,
            at.Direction, _config.CarOffPathM, _candidate);

        if (!line.Any) return false;

        Commit(car, line.ArcCount, line.LengthM, reverse: true);
        return true;
    }

    /// <summary>
    /// `P-14`'s line: the forward-in template, laid from where the car actually stands rather than from
    /// where the route said it would be. Refused means this bay cannot be driven into from here, which is
    /// a retarget and never a squeeze.
    /// </summary>
    public bool LayTheEntryLine(int car, int bay)
    {
        if (bay < 0 || !_parking.CanBeEntered(bay)) return false;

        var headingRad = _cars.HeadingRad[car];
        var line = BayTemplate.TryLayEntry(
            _config, CarFollower.RearAxleM(_config, _cars.PositionM[car], headingRad), headingRad,
            _parking.CentreM(bay), _parking.HeadingRad(bay), _candidate);

        if (!line.Any) return false;

        Commit(car, line.ArcCount, line.LengthM, reverse: false);
        return true;
    }

    /// <summary>
    /// `E-3` and `E-8`'s line: one straight along the car's own axis, drawn for the rear axle like every
    /// other line here and driven by the same follower in whichever gear it points.
    /// </summary>
    /// <remarks>
    /// <b>The ground is walked in the gear</b>, so the swept path is cleared before the car commits to it: a
    /// straight laid into whatever was already in the way would be a recovery that reverses into the car
    /// behind.
    /// </remarks>
    public bool LayTheStraight(int car, float reachM, bool backwards)
    {
        var headingRad = _cars.HeadingRad[car];
        var forward = Heading.Unit(headingRad);
        var travel = backwards ? -forward : forward;
        var rearAxleM = CarFollower.RearAxleM(_config, _cars.PositionM[car], forward);

        _candidate[0] = new ArcSeg(rearAxleM, MathF.Atan2(travel.Y, travel.X), reachM, 0f);

        // The body's own overhang in the direction of travel is where the looking starts: a ray from
        // inside the body ahead reports nothing.
        var tailM = backwards
            ? (_config.Car.LengthM - _config.Car.WheelbaseM) * 0.5f
            : _config.Car.WheelbaseM * 0.5f;

        var seen = Look(car, 1, tailM, MathF.Max(0f, reachM - tailM));
        if (seen < _config.Car.LengthM * 0.5f) return false;

        var lengthM = MathF.Min(reachM, tailM + seen);
        _candidate[0] = new ArcSeg(rearAxleM, MathF.Atan2(travel.Y, travel.X), lengthM, 0f);
        Commit(car, 1, lengthM, backwards);
        return true;
    }

    /// <summary>
    /// `E-4`'s line: out beside the lane, past what is standing in it, and back onto the line it left.
    /// <b>Neither the side nor how far out is assumed</b> — a whole lane over first and on the centreline
    /// side, because that is the side CAR-6.2b licenses and the width an overtake actually takes, then the
    /// verge and the narrow shift as the ground refuses each in turn.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A lane over and not a body's width.</b> The book carries what is in the way as a stretch of
    /// arclength and never as a place across the road, so where in its lane the thing actually stands is a
    /// fact nothing here can read — and a shift of the car's own width leaves a car spanning both halves of
    /// its own lane, which passes a wreck on the line and nothing that is anywhere else on it. Moving over
    /// by the lane's own width clears everything the lane can hold, and the ground walk is what says whether
    /// there is room for it.
    /// </para>
    /// <para>
    /// <b>The bends are drawn for the speed the car is doing</b>, which is what makes this an overtake
    /// rather than a shuffle: laid at the steering lock, a swerve is a sequence of 4 m corners and the
    /// profile's own corner term holds the car to 6 m/s for the whole of it — so a car cannot get past
    /// anything moving, because it has slowed below it to try. The floor is the lock, for a car at rest.
    /// </para>
    /// </remarks>
    /// <param name="passM">What has to be got past, measured from the nose.</param>
    /// <param name="atMps">What the car is doing, and therefore how wide the shape has to be drawn to hold it.</param>
    public bool LayTheSwerve(int car, float passM, float atMps)
    {
        var headingRad = _cars.HeadingRad[car];
        var forward = Heading.Unit(headingRad);
        var rearAxleM = CarFollower.RearAxleM(_config, _cars.PositionM[car], forward);

        // The road's own bend where the car stands, read off the line it is driving rather than searched
        // for: the route's line is the road, and the car's progress along it is already in hand.
        var alongCurvature = Spline.SampleAt(_cars.LineOf(car), _cars.ProgressM[car]).Curvature;

        // Wide enough that the S laid on top of that bend is still a shape the wheel can hold — a swerve
        // into a hairpin adds its own curvature to the hairpin's — and wide enough to be driven at the
        // speed the car is doing.
        var radiusM = MathF.Max(
            MathF.Max(_config.ParkingTemplateRadiusM, InsideTheLockM(alongCurvature)),
            _config.CarCorneringRadiusM(MathF.Abs(atMps), _cars.GroundCoefficient[car]));

        var lane = _cars.LaneOf(car);
        var aLaneOverM = lane >= 0 ? _roads.LaneWidthM[lane] : _config.Car.WidthM;
        var towardsTheCentre = TowardsTheCentreline(car, forward);

        for (var reach = 0; reach < 2; reach++)
        {
            // A lane over first, and the body's own width as the last thing tried: it does not clear a lane
            // but it clears a wreck sitting on the line, which is the case a narrow road still has room for.
            var offsetM = reach == 0 ? aLaneOverM : MathF.Min(_config.Car.WidthM, aLaneOverM);
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var side = attempt == 0 ? towardsTheCentre : -towardsTheCentre;
                var line = RoadTemplates.TryLaySwerve(
                    rearAxleM, headingRad, offsetM * side, passM, radiusM, alongCurvature, _candidate);

                if (!line.Any) continue;
                if (!GroundAdmits(_candidate.AsSpan(0, line.ArcCount), line.LengthM)) continue;
                if (Look(car, line.ArcCount, _config.Car.WheelbaseM * 0.5f, line.LengthM) < line.LengthM) continue;

                Commit(car, line.ArcCount, line.LengthM, reverse: false);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The tightest a swerve's own bends may be drawn where the road is already bending: what is left of
    /// the steering lock once the road has had its share. <b>A curvature is what adds, not a radius.</b>
    /// </summary>
    float InsideTheLockM(float alongCurvature)
    {
        var spare = (1f / _config.CarTurningRadiusM) - MathF.Abs(alongCurvature);
        return spare > 0f ? 1f / spare : float.PositiveInfinity;
    }

    /// <summary>
    /// `P-11`'s line: the counter-swing onto the reverse of the lane the car is on. Refused where the
    /// town has no reverse lane there, or where the ground the shape needs is not a junction's.
    /// </summary>
    public bool LayTheTurnAround(int car, int lane)
    {
        if (lane < 0) return false;

        var onto = _roads.LaneReverse[lane];
        if (onto < 0) return false;

        var headingRad = _cars.HeadingRad[car];
        var forward = Heading.Unit(headingRad);
        var rearAxleM = CarFollower.RearAxleM(_config, _cars.PositionM[car], forward);

        var ontoArcs = _roads.ArcsOf(onto);
        var ontoLengthM = _roads.LaneLengthM[onto];
        var at = Spline.SampleAt(ontoArcs, Spline.ProjectM(ontoArcs, rearAxleM, ontoLengthM * 0.5f, ontoLengthM));

        var line = RoadTemplates.TryLayTurnAround(_config, rearAxleM, headingRad, at.PositionM, at.Direction, _candidate);
        if (!line.Any) return false;
        if (!GroundAdmits(_candidate.AsSpan(0, line.ArcCount), line.LengthM)) return false;
        if (Look(car, line.ArcCount, _config.Car.WheelbaseM * 0.5f, line.LengthM) < line.LengthM) return false;

        Commit(car, line.ArcCount, line.LengthM, reverse: false);
        return true;
    }

    /// <summary>
    /// Which way the middle of the road lies, as the sign a curvature turns that way. Read off the
    /// reverse lane where the town has one, and off the side traffic keeps otherwise — a one-way street
    /// has a centreline all the same, and it is the side the parked cars are not on.
    /// </summary>
    float TowardsTheCentreline(int car, Vector2 forward)
    {
        var lane = _cars.LaneOf(car);
        var opposite = lane >= 0 ? _roads.LaneReverse[lane] : -1;
        if (opposite < 0) return -_config.RoadSideSign;

        var arcs = _roads.ArcsOf(opposite);
        var lengthM = _roads.LaneLengthM[opposite];
        var at = Spline.SampleAt(arcs, Spline.ProjectM(arcs, _cars.PositionM[car], lengthM * 0.5f, lengthM));
        var across = new Vector2(-forward.Y, forward.X);
        return MathF.Sign(Vector2.Dot(at.PositionM - _cars.PositionM[car], across)) is var side and not 0
            ? side
            : -_config.RoadSideSign;
    }

    /// <summary>
    /// How far down a candidate the ground is still nobody else's. <b>The book and not a ray</b>
    /// (<see cref="GroundAhead"/>): a cast found a shape and could not say whether the ground beyond it was
    /// already inside somebody's road, so a swerve was laid into the stretch a car three seconds away was
    /// committed to.
    /// </summary>
    float Look(int car, int arcCount, float fromM, float reachM) =>
        GroundAhead.ClearM(
            _roads, _occupancy, _candidate.AsSpan(0, arcCount), fromM, reachM, _config.Car.WidthM * 0.5f, car);

    /// <summary>
    /// The candidate becomes the line. <b>The one place a template is written</b>: the arcs are copied
    /// over and the progress starts again from its beginning, because a new line is a new origin for every
    /// distance measured along one.
    /// </summary>
    void Commit(int car, int arcCount, float lengthM, bool reverse)
    {
        _candidate.AsSpan(0, arcCount).CopyTo(_cars.LineArcsOf(car));
        _cars.Line[car] = new DrivenLine(arcCount, 0, lengthM);
        _cars.ProgressM[car] = 0f;
        _cars.LineIsReverse[car] = reverse;
    }
}
