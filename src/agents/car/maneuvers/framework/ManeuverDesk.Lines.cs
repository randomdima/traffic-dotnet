using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.Agents.Car.Maneuvers;

/// <summary>
/// The geometry a manoeuvre drives when it does not drive the route: the bay's own way and the template
/// that stands in for it, the recovery straight and the swerve. <b>Every one of them is walked before it
/// is written</b>, and a refusal leaves the line the car is holding exactly as it was.
/// </summary>
/// <remarks>
/// <b>Every shape here is drawn for the car that is going to drive it</b> (CAR-11): its own circle, its own
/// axle under its own body, its own width against the ground. The town's precomputed ways are the nominal
/// car's and are offered first because a way of the book carries a reservation and a right of way with it;
/// where one does not suit the body that turned up, the same shape is laid again from the pose that body is
/// actually in (CAR-10b) rather than the car being asked to fit the drawing.
/// </remarks>
internal sealed partial class ManeuverDesk
{
    /// <summary>
    /// `P-2`'s line: <b>the town's own way at the bay, taken up as it stands</b> — the same line the car was
    /// parked on, driven in whichever gear that way is driven in (GEN-4j). Refused where the car is not
    /// standing at the bay end of it, which is what sends `P-2` to the recovery
    /// (<see cref="LayTheExitLine"/>) rather than onto a line it is not on.
    /// </summary>
    /// <remarks>
    /// <b>This is the whole of what makes leaving a bay ordinary.</b> The line is a way of the book, so the
    /// car's reservation runs along it, its grant is cut by the town's own table of what is driven over
    /// what, and the ground it takes before it moves is taken by the protocol a car crossing a junction
    /// uses — there is no gap to be looked at, no patience to be spent, and nothing here that a car turning
    /// across a junction does not also do.
    /// </remarks>
    public bool TakeTheWayOutOfTheBay(int car, int bay)
    {
        var way = TheWayOutThatSuits(car, bay);
        if (way == BayWays.NoWay) return false;

        var arcs = _bayWays.ArcsOf(way);
        var headingRad = _cars.HeadingRad[car];
        var fromAxleM = CarFollower.RearAxleM(_cars.BuildOf(car), _cars.PositionM[car], Heading.Unit(headingRad));

        // <b>The town's way is where this car's axle actually is, or it is not this car's way</b>
        // (CAR-10b, CAR-11a). A bay's ways are laid for the nominal car, and a car whose own axle sits
        // somewhere else under its body starts the way from a place it is not standing on: the recovery
        // lays the same shape from where this car really is, which is what a driver does rather than
        // shuffling onto a line.
        if ((fromAxleM - arcs[0].StartM).Length() > _config.CarOffPathM) return false;

        arcs.CopyTo(_candidate);
        Commit(car, arcs.Length, _bayWays.LengthM(way), _bayWays.IsDrivenInReverse(way), way);
        return true;
    }

    /// <summary>
    /// `P-16`'s line, and `P-2`'s only where the car is not standing on the town's own way: <b>the
    /// recovery</b> — the same shape laid from the pose the car is actually in, aimed at the place on the
    /// lane the town's own way leaves and driven in the gear that way is driven in.
    /// </summary>
    public bool LayTheExitLine(int car, int bay)
    {
        var way = TheWayOutThatSuits(car, bay);
        if (way == BayWays.NoWay) return false;

        // The axle travels the way the car points under power and against it in reverse, at both ends of
        // the shape: reversing out, the car ends up heading along the lane while its axle travels back up
        // it, so the shape's own end direction is the lane's reversed.
        var reverse = _bayWays.IsDrivenInReverse(way);
        var at = Spline.SampleAt(_roads.ArcsOf(_bayWays.LaneOf(way)), _bayWays.AtLaneM(way));
        var endsAlong = reverse ? -at.Direction : at.Direction;
        var headingRad = _cars.HeadingRad[car];
        ref readonly var build = ref _cars.BuildOf(car);
        var line = BayTemplate.TryLay(
            _config, build, CarFollower.RearAxleM(build, _cars.PositionM[car], headingRad),
            reverse ? headingRad + MathF.PI : headingRad,
            at.PositionM, MathF.Atan2(endsAlong.Y, endsAlong.X), _candidate, out _);

        if (!line.Any) return false;

        Commit(car, line.ArcCount, line.LengthM, reverse);
        return true;
    }

    /// <summary>
    /// `P-14`'s line: the same shape into the bay, laid from where the car actually stands rather than from
    /// where the route said it would be, in the standing this leg is parking in (GEN-4j). Refused means this
    /// bay cannot be driven into from here, which is a retarget and never a squeeze.
    /// </summary>
    public bool LayTheEntryLine(int car, int bay)
    {
        if (bay < 0 || !_parking.CanBeReached(bay)) return false;

        // <b>Where the leg's own line names a way into this bay, that way's standing is the one to park
        // in</b> and the habit does not get a second say (GEN-4j): the line was laid, the ground was
        // measured and the way out was settled for that standing, and a shape laid the other way round is a
        // car parked facing the wrong way for the leg that comes next (GEN-4l).
        var named = _cars.TailWayOf(car);
        var noseIn = named != CarFleet.NoWay && _bayWays.BayOfWay(named) == bay
            ? _bayWays.IsNoseIn(named)
            : _bayWays.TheStandingOnOffer(bay, !_cars.BacksIntoBays[car]);
        var bayHeadingRad = _parking.HeadingRad(bay);
        var headingRad = _cars.HeadingRad[car];
        ref readonly var build = ref _cars.BuildOf(car);

        // A bay is a piece of ground the town laid for the nominal car, and this one has to fit in it
        // (CAR-11b): a car longer than the space is a car parked across the aisle behind it.
        if (!_parking.Takes(build.LengthM, build.WidthM)) return false;

        var line = BayTemplate.TryLay(
            _config, build, CarFollower.RearAxleM(build, _cars.PositionM[car], headingRad),
            noseIn ? headingRad : headingRad + MathF.PI,
            BayTemplate.RearAxleOfBayM(build, _parking.CentreM(bay), bayHeadingRad, noseIn),
            bayHeadingRad, _candidate, out _);

        if (!line.Any) return false;

        Commit(car, line.ArcCount, line.LengthM, reverse: !noseIn);
        return true;
    }

    /// <summary>
    /// <b>Which of the ways out of a bay this car has.</b> They begin at two poses, one per standing, and
    /// <b>the car is standing in one of them</b> — so the standing picks the gear and the lane, and only
    /// then is there a choice at all: the lane already running the way the car is going (GEN-4f). Setting
    /// off into the stream running the other way is a leg that starts by driving round the block.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read off the pose and never off a booking (GEN-4j). A car nose-first in its space reverses out onto
    /// the lane beside it and has no other way out; one that backed in drives out, and may cross the
    /// carriageway doing it.
    /// </para>
    /// <para>
    /// <b>It is also the whole of what makes a turn at a lot a turn</b> (GEN-4l): a car that parked there
    /// to come back the other way is leaving for the lane its destination lies down, which is the question
    /// this already asks of every unpark.
    /// </para>
    /// </remarks>
    int TheWayOutThatSuits(int car, int bay)
    {
        if (bay < 0) return BayWays.NoWay;

        var noseIn = BayTemplate.StandsNoseIn(_parking.HeadingRad(bay), _cars.HeadingRad[car]);
        var knowsWhereItIsGoing = _cars.HasDestination[car];
        var goingM = _cars.DestinationM[car];
        var best = BayWays.NoWay;
        var bestM = float.NegativeInfinity;

        for (var slot = 0; slot < _bayWays.WayCountOf(bay); slot++)
        {
            var way = _bayWays.WayOf(bay, slot);
            if (_bayWays.IsEntry(way) || _bayWays.IsNoseIn(way) != noseIn) continue;
            if (!knowsWhereItIsGoing) return way;

            var at = Spline.SampleAt(_roads.ArcsOf(_bayWays.LaneOf(way)), _bayWays.AtLaneM(way));
            var towardsM = Vector2.Dot(goingM - at.PositionM, at.Direction);
            if (towardsM <= bestM) continue;

            best = way;
            bestM = towardsM;
        }

        return best;
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
        ref readonly var build = ref _cars.BuildOf(car);
        var rearAxleM = CarFollower.RearAxleM(build, _cars.PositionM[car], forward);

        _candidate[0] = new ArcSeg(rearAxleM, MathF.Atan2(travel.Y, travel.X), reachM, 0f);

        // The body's own overhang in the direction of travel is where the looking starts: a ray from
        // inside the body ahead reports nothing.
        var tailM = backwards ? build.TailBehindAxleM : build.CentreAheadOfAxleM;

        var seen = Look(car, 1, tailM, MathF.Max(0f, reachM - tailM));
        if (seen < build.HalfLengthM) return false;

        var lengthM = MathF.Min(reachM, tailM + seen);
        _candidate[0] = new ArcSeg(rearAxleM, MathF.Atan2(travel.Y, travel.X), lengthM, 0f);
        Commit(car, 1, lengthM, backwards);
        return true;
    }

    /// <summary>
    /// `P-19`'s line: <b>one leg of a turn on the spot</b> — a single arc at this car's own lock, drawn for
    /// the rear axle and driven in whichever gear this leg of it is, as far as the ground and the book
    /// admit and never further round than one sweep.
    /// </summary>
    /// <remarks>
    /// <b>The wheel goes the same way in both gears, which is what turns a car round rather than rocking
    /// it.</b> A line is drawn in the direction the axle travels, so a leg driven in reverse starts at the
    /// body's heading turned about; the body then swings the way the arc bends whichever gear it is in, and
    /// keeping the sign is keeping the wheel over while the gear changes — which is what a driver does.
    /// </remarks>
    /// <param name="towardsRad">The heading the car is working round to, which is what says how much of a sweep is left to make.</param>
    public bool LayTheShunt(int car, float towardsRad, bool backwards)
    {
        ref readonly var build = ref _cars.BuildOf(car);
        var headingRad = _cars.HeadingRad[car];
        var forward = Heading.Unit(headingRad);
        var rearAxleM = CarFollower.RearAxleM(build, _cars.PositionM[car], forward);
        var travelRad = backwards ? headingRad + MathF.PI : headingRad;

        // <b>The way round is the way the middle of the road lies, and it is settled once.</b> A car coming
        // back the other way is half a turn from where it is going, and half a turn is as near one way round
        // as the other — read off the difference of the two headings, the answer flips sign on the first
        // degree of the first leg and the car rocks on the spot for the rest of the run.
        var side = TowardsTheCentreline(car, forward);
        var curvature = side / build.TurningRadiusM;
        var sweptM = MathF.Min(_config.CarShuntSweepRad, LeftToSweepRad(headingRad, towardsRad, side))
                     / MathF.Abs(curvature);

        // The ground first, in steps, because a leg that runs out of tarmac half way round is a car on the
        // pavement — and then the book, from the overhang the gear puts in front.
        var reachM = GroundHolds(car, new ArcSeg(rearAxleM, travelRad, sweptM, curvature), sweptM);
        var tailM = backwards ? build.TailBehindAxleM : build.CentreAheadOfAxleM;
        if (reachM <= tailM) return false;

        var seen = Look(car, 1, tailM, reachM - tailM);
        var lengthM = MathF.Min(reachM, tailM + seen);
        if (lengthM < build.HalfLengthM) return false;

        _candidate[0] = new ArcSeg(rearAxleM, travelRad, lengthM, curvature);
        Commit(car, 1, lengthM, backwards);
        return true;
    }

    /// <summary>
    /// <b>The last leg of a turn on the spot</b> (`P-19`): from the pose the shunting left the car in onto
    /// the line of the lane it is now pointing along, a run-in further down it. What it is for is that the
    /// entry hands over a car <em>on</em> its lane — one left standing two metres off the line it is about
    /// to be given is one the follower calls lost on the first tick it holds it.
    /// </summary>
    public bool LayTheLineUpOntoTheWayBack(int car)
    {
        var back = _cars.TurnsBackOn[car];
        if (back < 0) return false;

        ref readonly var build = ref _cars.BuildOf(car);
        var headingRad = _cars.HeadingRad[car];
        var axleM = CarFollower.RearAxleM(build, _cars.PositionM[car], Heading.Unit(headingRad));
        var arcs = _roads.ArcsOf(back);
        var laneLengthM = _roads.LaneLengthM[back];
        var alongM = Spline.ProjectM(arcs, axleM, laneLengthM * 0.5f, laneLengthM);

        // <b>Aimed further down the lane until the join is one this car can hold.</b> A body left a few
        // metres off the line cannot be brought onto it inside a car's length whatever the wheel does, and
        // how far it needs is a fact about how far off it ended up rather than a figure to guess at.
        for (var runInM = _config.ParkingStagedInM; alongM + runInM <= laneLengthM; runInM *= 2f)
        {
            var at = Spline.SampleAt(arcs, alongM + runInM);
            var laid = Spline.BiarcInto(axleM, headingRad, at.PositionM, at.HeadingRad, _candidate);
            if (laid == 0) continue;

            var lengthM = 0f;
            var bend = 0f;
            for (var arc = 0; arc < laid; arc++)
            {
                lengthM += _candidate[arc].LengthM;
                bend = MathF.Max(bend, MathF.Abs(_candidate[arc].Curvature));
            }

            if (bend > 1f / build.TurningRadiusM) continue;
            if (!GroundAdmits(car, _candidate.AsSpan(0, laid), lengthM)) continue;
            if (Look(car, laid, build.CentreAheadOfAxleM, lengthM) < lengthM) continue;

            Commit(car, laid, lengthM, reverse: false);
            return true;
        }

        return false;
    }

    /// <summary>
    /// How far a body still has to swing to be pointing where it is going, <b>measured the way it is
    /// actually turning</b> — so it falls to nothing over the legs of a turn instead of jumping from half a
    /// turn one way to half a turn the other as the body passes head-on.
    /// </summary>
    static float LeftToSweepRad(float headingRad, float towardsRad, float side)
    {
        var turn = (side >= 0f ? towardsRad - headingRad : headingRad - towardsRad) % MathF.Tau;
        return turn < 0f ? turn + MathF.Tau : turn;
    }

    /// <summary>How much of a candidate arc the ground will hold a body over, walked from its start in steps.</summary>
    float GroundHolds(int car, in ArcSeg arc, float lengthM)
    {
        var stepM = _cars.BuildOf(car).LengthM * GroundStepInCarLengths;
        var heldM = 0f;
        while (heldM + stepM <= lengthM && GroundAdmits(car, [arc], heldM + stepM)) heldM += stepM;

        return heldM;
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
        ref readonly var build = ref _cars.BuildOf(car);
        var rearAxleM = CarFollower.RearAxleM(build, _cars.PositionM[car], forward);

        // The road's own bend where the car stands, read off the line it is driving rather than searched
        // for: the route's line is the road, and the car's progress along it is already in hand.
        var alongCurvature = Spline.SampleAt(_cars.LineOf(car), _cars.ProgressM[car]).Curvature;

        // Wide enough that the S laid on top of that bend is still a shape the wheel can hold — a swerve
        // into a hairpin adds its own curvature to the hairpin's — and wide enough to be driven at the
        // speed the car is doing. Both are this car's own: a long car cannot be drawn the shape a short
        // one gets away with, and it is the tyres of the car that is swerving that have to hold it.
        var radiusM = MathF.Max(
            MathF.Max(build.ParkingTemplateRadiusM, InsideTheLockM(build, alongCurvature)),
            build.CorneringRadiusM(MathF.Abs(atMps), _cars.GroundCoefficient[car], _config.Driving.GripMargin));

        var lane = _cars.LaneOf(car);
        var aLaneOverM = lane >= 0 ? _roads.LaneWidthM[lane] : build.WidthM;
        var towardsTheCentre = TowardsTheCentreline(car, forward);

        for (var reach = 0; reach < 2; reach++)
        {
            // A lane over first, and the body's own width as the last thing tried: it does not clear a lane
            // but it clears a wreck sitting on the line, which is the case a narrow road still has room for.
            var offsetM = reach == 0 ? aLaneOverM : MathF.Min(build.WidthM, aLaneOverM);
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var side = attempt == 0 ? towardsTheCentre : -towardsTheCentre;
                var line = RoadTemplates.TryLaySwerve(
                    rearAxleM, headingRad, offsetM * side, passM, radiusM, alongCurvature, _candidate);

                if (!line.Any) continue;
                if (!GroundAdmits(car, _candidate.AsSpan(0, line.ArcCount), line.LengthM)) continue;
                if (Look(car, line.ArcCount, build.CentreAheadOfAxleM, line.LengthM) < line.LengthM) continue;

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
    static float InsideTheLockM(in CarBuild build, float alongCurvature)
    {
        var spare = (1f / build.TurningRadiusM) - MathF.Abs(alongCurvature);
        return spare > 0f ? 1f / spare : float.PositiveInfinity;
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
            _roads, _occupancy, _candidate.AsSpan(0, arcCount), fromM, reachM, _cars.BuildOf(car).FlankM, car);

    /// <summary>
    /// The candidate becomes the line. <b>The one place a line off the route is written</b>: the arcs are
    /// copied over and the progress starts again from its beginning, because a new line is a new origin for
    /// every distance measured along one.
    /// </summary>
    /// <param name="way">
    /// The way of the book this line <em>is</em>, where it is one of them, and <see cref="CarFleet.NoWay"/>
    /// for geometry the car laid itself. It is written here because this is where the line is written, so
    /// the two can never describe different things.
    /// </param>
    void Commit(int car, int arcCount, float lengthM, bool reverse, int way = CarFleet.NoWay)
    {
        _candidate.AsSpan(0, arcCount).CopyTo(_cars.LineArcsOf(car));
        _cars.Line[car] = new DrivenLine(arcCount, 0, lengthM);
        _cars.ProgressM[car] = 0f;
        _cars.LineIsReverse[car] = reverse;
        _cars.LineWay[car] = way;
    }
}
