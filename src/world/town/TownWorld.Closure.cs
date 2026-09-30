using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Agents.Service;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Containment;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Routing;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>A road closed round a scene</b> (SRV-8…SRV-11): the lanes a scene lies across and every lane leading
/// only to them, a police car at the mouth of each with its officer standing in front of it, and those lanes out
/// of every route and every tour for as long as the officer stands there.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three things answer one question, and none of them is a second gate on another</b> (SIM-7). Whether a car
/// <em>plans</em> to go down a closed lane is the router's and the tour's — a ban (SRV-10, SIM-6); whether one
/// that is there anyway <em>gets in</em> is the officer's body at the mouth, which every car stops short of like
/// any other body (TER-4c.1, SRV-9). No claim of the closure's own is laid: a body is already the thing the road
/// refuses a car.
/// </para>
/// <para>
/// <b>A call goes through</b> (SRV-6, AMB-4): a car carrying one is routed as if nothing were closed, since the
/// scene it is aimed at lies inside the closure (SIM-6's ban lifted for that agent on that plan), and the officer
/// steps to the kerb while one is coming down the lane.
/// </para>
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>How many lanes a scene can lie across: one, or the two of its street (SRV-9).</summary>
    const int MostLanesUnderAScene = 2;

    /// <summary>Whether a closure began or ended since the town's closures were last laid.</summary>
    bool _closuresChanged;

    /// <summary>Every lane a closure holds now, one flag a lane — what a tour may not draw (SRV-10).</summary>
    bool[] _closedLanes = [];

    /// <summary>And every run of the driving network those lanes are part of — what a route may not enter (SRV-10).</summary>
    bool[] _closedLinks = [];

    /// <summary>How many times a patrol has taken a lane of a scene to close (SRV-9).</summary>
    public long ClosuresTaken { get; private set; }

    /// <summary>And how many of those were given up on the way to the entrance — the scene gone, or out of clock.</summary>
    public long ClosuresGivenUp { get; private set; }

    /// <summary>And how many were stood: the car at the entrance and its officer on the way to the mouth.</summary>
    public long ClosuresStood { get; private set; }

    /// <summary>The lanes closed now, for whoever draws or measures them.</summary>
    public ReadOnlySpan<bool> ClosedLanes => _closedLanes;

    /// <summary>And the runs a route may not enter, for whoever plans one the way a car does (CTL-1a).</summary>
    public ReadOnlySpan<bool> ClosedLinks => _closedLinks;

    /// <summary>
    /// <b>What a search for this car may not enter</b>: every closed run, or nothing for a car carrying a call —
    /// the scene it is going to is inside the closure (SIM-6, SRV-6).
    /// </summary>
    ReadOnlySpan<bool> ClosedLinksFor(int car) => Cars.BlueLight[car] ? default : _closedLinks;

    /// <summary>
    /// <b>The town's closures laid again</b> where one began or ended: which lanes and runs are closed, and every
    /// route that ran through one dropped, so a car already on its way is sent round rather than into an officer.
    /// </summary>
    void LayTheClosures()
    {
        if (!_closuresChanged) return;

        _closuresChanged = false;
        Array.Clear(_closedLanes);
        Array.Clear(_closedLinks);
        for (var car = 0; car < Cars.Count; car++)
        {
            if (!IsAPatrolCar(car) || Cars.Broken[car] || !_beat.Closes(car)) continue;

            foreach (var lane in _beat.ClosedLanesOf(car))
            {
                _closedLanes[lane] = true;
                if (_driving.LinkOfLane(lane) is var link and not TravelGraph.NoLink) _closedLinks[link] = true;
            }
        }

        for (var car = 0; car < Cars.Count; car++) SendRoundTheClosures(car);
    }

    /// <summary>
    /// <b>A car whose route or line runs into a closed lane, sent round it</b> — the queue dropped, and the line
    /// laid again from the lane it is on, where it can still stop short of the box it would turn in at.
    /// </summary>
    /// <remarks>
    /// <b>The queue of every car, and the line only of one driving the road</b>: a car on a bay's way has that way
    /// for its line, and the queue it takes up at the end of it is the stale one.
    /// </remarks>
    void SendRoundTheClosures(int car)
    {
        if (!Cars.Driven[car] || Cars.Broken[car] || Cars.BlueLight[car]) return;

        var routeCrosses = false;
        foreach (var lane in Cars.RouteOf(car)[Cars.RouteTaken[car]..Cars.RouteCount[car]]) routeCrosses |= _closedLanes[lane];

        var onTheRoad = Cars.LineWayOf(car) == CarFleet.NoWay;
        var chain = Cars.ChainOf(car)[..(onTheRoad ? Cars.Line[car].LaneCount : 0)];
        var lineCrosses = false;
        for (var slot = 1; slot < chain.Length; slot++) lineCrosses |= _closedLanes[chain[slot]];

        if (!lineCrosses && !routeCrosses) return;

        Cars.ClearRoute(car);
        if (lineCrosses && !Cars.CommittedToTheBox[car]) LayLine(car, 1);
    }

    /// <summary>
    /// <b>The street lanes a scene lies across</b> (SRV-9), nearest first: the lane nearest it, and the other lane
    /// of its street where the scene's body stands on that one too — read off the ground the body was laid on.
    /// </summary>
    int TheScenesLanes(int casualty, int wreck, Span<int> lanes)
    {
        var lane = _roads.NearestStreetLane(TheSceneM(casualty, wreck), out _);
        if (lane < 0) return 0;

        lanes[0] = lane;
        var back = _roads.LaneReverse[lane];
        var (occupant, roster) = casualty >= 0 ? (casualty, LaneRoster.Walking) : (wreck, LaneRoster.Driving);
        if (back < 0 || lanes.Length < 2 || !_occupancy.HasTheBodyOf(_ways.OfRoadLane(back), occupant, roster)) return 1;

        lanes[1] = back;
        return 2;
    }

    /// <summary>
    /// <b>A lane of the scene no other patrol is closing, and the stretch that closes it</b> (SRV-9): the nearer
    /// entrance where both are free, so two patrols sent to one road meet it from its two ends.
    /// </summary>
    bool TakeALaneOfTheScene(int car, int casualty, int wreck)
    {
        Span<int> lanes = stackalloc int[MostLanesUnderAScene];
        var count = TheScenesLanes(casualty, wreck, lanes);
        var room = _beat.RoomForTheClosureOf(car);
        Span<int> stretch = stackalloc int[room.Length];
        var best = 0;
        var sceneLane = PatrolDuty.Nobody;
        var bestM = float.PositiveInfinity;
        for (var at = 0; at < count; at++)
        {
            if (IsClosedByAnother(car, lanes[at])) continue;

            var laid = RoadClosure.Stretch(_roads, lanes[at], stretch);
            if (laid == 0) continue;

            var entranceM = Spline.SampleAt(_roads.ArcsOf(stretch[0]), 0f).PositionM;
            var farM = (entranceM - Cars.PositionM[car]).LengthSquared();
            if (farM >= bestM) continue;

            best = laid;
            sceneLane = lanes[at];
            bestM = farM;
            stretch[..laid].CopyTo(room);
        }

        _beat.ClosedCount[car] = best;
        _beat.SceneLane[car] = sceneLane;
        if (best > 0) PlaceTheClosure(car);
        return best > 0;
    }

    /// <summary>
    /// <b>Where the officer and the car stand in the entrance</b> (SRV-9, SRV-11), found once with the closure.
    /// Where a zebra is painted over the mouth, <b>the officer stands short of it and the car past it</b>: the
    /// crossing between them stays the walkers', and the officer is still what a car turning in meets first.
    /// </summary>
    void PlaceTheClosure(int car)
    {
        var entrance = _beat.EntranceOf(car);
        var service = _config.Service;
        var postM = service.OfficerIntoTheLaneM;
        var clearFromM = postM + (_config.PersonDiameterM * 0.5f);
        if (TheZebraOverTheMouth(entrance, out var zebraFromM, out var zebraToM))
        {
            postM = MathF.Min(postM, zebraFromM - service.OfficerShortOfTheZebraM - (_config.PersonDiameterM * 0.5f));
            clearFromM = MathF.Max(clearFromM, zebraToM);
        }

        var tailBehindAxleM = (_config.Car.LengthM * 0.5f) - _config.CarCentreAheadOfAxleM;
        _beat.PostM[car] = postM;
        _beat.StandM[car] = MathF.Min(clearFromM + service.PoliceCarTailClearM + tailBehindAxleM, _roads.LaneLengthM[entrance]);
    }

    /// <summary>
    /// <b>The zebra painted over the mouth of a lane</b>, as the metres of the lane under it, or false where there is
    /// none — read off the lane's marks (TER-5c.3), as far in as a closure's officer and car would stand. Two zebras
    /// touching are one stretch of paint.
    /// </summary>
    public bool TheZebraOverTheMouth(int lane, out float fromM, out float toM)
    {
        fromM = float.PositiveInfinity;
        toM = float.NegativeInfinity;
        var reachM = _config.Service.OfficerIntoTheLaneM + _config.Service.PoliceCarTailClearM + _config.Car.LengthM;
        foreach (ref readonly var mark in _occupancy.Marks.Of(_ways.OfRoadLane(lane)))
        {
            if (mark.MineFromM >= MathF.Max(reachM, toM + _config.Service.PoliceCarTailClearM)) break;
            if (ZebraOf(mark.OnWay) == RibbonMarks.NoZebra) continue;

            fromM = MathF.Min(fromM, mark.MineFromM);
            toM = MathF.Max(toM, mark.MineToM);
        }

        return toM > fromM;
    }

    /// <summary>
    /// <b>Whether another patrol is closing the lane already</b>, for this scene or another lying on it: one closure
    /// holds a lane for everything on it, and two police cars sent to one mouth stand in each other's way.
    /// </summary>
    bool IsClosedByAnother(int car, int lane)
    {
        for (var other = 0; other < Cars.Count; other++)
        {
            if (other != car && _beat.SceneLane[other] == lane && _beat.IsOnACall(other)) return true;
        }

        return false;
    }

    /// <summary>Where on its entrance lane a police car stands (<see cref="PlaceTheClosure"/>).</summary>
    Vector2 TheStandM(int car) => OnTheEntranceM(car, _beat.StandM[car]);

    /// <summary>And where its officer stands (SRV-11), for whoever draws or measures it.</summary>
    public Vector2 TheOfficersPostM(int car) => OnTheEntranceM(car, _beat.PostM[car]);

    /// <summary>
    /// A place on the entrance lane at so many of its metres — <b>and short of its first one, on the line it leaves
    /// the box along</b>, which is where an officer standing short of a zebra over the mouth is.
    /// </summary>
    Vector2 OnTheEntranceM(int car, float alongM)
    {
        var lane = _beat.EntranceOf(car);
        if (alongM >= 0f) return Spline.SampleAt(_roads.ArcsOf(lane), MathF.Min(alongM, _roads.LaneLengthM[lane])).PositionM;

        var mouth = Spline.SampleAt(_roads.ArcsOf(lane), 0f);
        return mouth.PositionM + (mouth.Direction * alongM);
    }

    /// <summary>
    /// <b>The lane a police car on a call is sent into, and how far along it</b> — the one leg whose goal is a
    /// named lane rather than the nearer side of a street, since a car at the mouth of the other lane closes
    /// nothing (<see cref="RouteGoalsFor"/>).
    /// </summary>
    bool TheEntranceItIsSentTo(int car, out int lane, out float alongM)
    {
        lane = PatrolDuty.Nobody;
        alongM = 0f;
        if (!IsAPatrolCar(car) || _beat.Stage[car] != PatrolStage.Attending || _beat.ClosedCount[car] == 0) return false;

        lane = _beat.EntranceOf(car);
        alongM = _beat.StandM[car];
        return true;
    }

    /// <summary>
    /// <b>Where this police car is to be stopped, and how near its line has to pass to stop there</b>, and false
    /// when nothing is asking it to: the stand at its entrance, for as long as it is on its way there (SRV-9).
    /// </summary>
    /// <remarks>
    /// <b>Within half the entrance lane of the stand and no further</b>: the other lane of the street passes the
    /// stand a lane's width away, and a car stopped there is at the mouth of a lane it is not closing.
    /// </remarks>
    bool TheClosureStopsAt(int car, out Vector2 standM, out float reachM)
    {
        standM = default;
        reachM = 0f;
        if (!IsAPatrolCar(car) || _beat.Stage[car] != PatrolStage.Attending || !Cars.HasDestination[car]) return false;

        standM = Cars.DestinationM[car];
        reachM = _roads.LaneWidthM[_beat.EntranceOf(car)] * 0.5f;
        return true;
    }

    /// <summary>
    /// <b>The drive to the entrance</b>, and the closure begun once the car is standing there. The bound is the
    /// beat's own (SRV-5): a scene the traffic will not let a patrol reach costs it the leg and nothing more.
    /// </summary>
    void RunToTheEntrance(int car)
    {
        if (!TheSceneStands(_beat.Casualty[car], _beat.Wreck[car]) || _beat.SinceS[car] >= _config.PatrolGiveUpS)
        {
            ClosuresGivenUp++;
            GiveUpTheScene(car);
            return;
        }

        if (!Cars.Driven[car])
        {
            SendTo(car, TheStandM(car), ParkingRegistry.NoBay);
            return;
        }

        if (Cars.VelocityMps[car].Length() > _config.Driving.StopSpeedMps || !StoppedWhereItWasSent(car)) return;

        // Standing where it was sent: the car is parked in the lane it closes, and the officer gets out.
        ClosuresStood++;
        StandTheCarDown(car);
        EnterThePatrolStage(car, PatrolStage.Closing);
        _beat.ClosedForS[car] = 0f;
    }

    /// <summary>
    /// <b>The road held closed</b> (SRV-9, SRV-11): the officer out and standing at the mouth — at the kerb while a
    /// call is coming down the lane — for as long as the scene stands, and bounded besides (SRV-6).
    /// </summary>
    void HoldTheRoadClosed(int car, float sinceLastDecisionS)
    {
        _beat.ClosedForS[car] += sinceLastDecisionS;
        if (!TheSceneStands(_beat.Casualty[car], _beat.Wreck[car]) || _beat.ClosedForS[car] >= _config.PoliceClosureLifeS)
        {
            CallTheOfficerBack(car);
            return;
        }

        var officer = _beat.Officer[car];
        if (officer < 0) return;

        if (People.Inside[officer].Any && !LetTheOfficerOut(car, officer)) return;

        AimTheOfficer(officer, ACallIsComingThrough(car) ? TheKerbBesideThePostM(car) : TheOfficersPostM(car));
    }

    /// <summary>
    /// <b>The officer out of the car</b> (SRV-11, PHY-7a), put down on the first free ground off its kerb-side door
    /// — refused, and asked again, while there is none.
    /// </summary>
    bool LetTheOfficerOut(int car, int officer)
    {
        var doorM = TheKerbDoorM(car);
        if (!ExitSpots.TryFind(
                _config, _plan.WorldSizeM, _physics, new DoorStep(this), doorM, doorM + (doorM - Cars.PositionM[car]),
                out var spotM))
        {
            return false;
        }

        _containers.Alight(car, officer);
        Place(officer, spotM, Cars.HeadingRad[car]);
        People.Stage[officer] = TripStage.OnDuty;
        return true;
    }

    /// <summary>
    /// <b>An officer on duty walking straight to a place and standing on it</b> (SRV-11): no route and no clock,
    /// since where they stand is the closure's and the ground under it is the road's.
    /// </summary>
    void AimTheOfficer(int officer, Vector2 atM)
    {
        People.DestinationM[officer] = atM;
        People.GoalM[officer] = atM;
        People.Walking[officer] = true;
    }

    /// <summary>
    /// <b>Whether a car carrying a call is coming down the lane this car closes and has not yet reached the
    /// officer</b> (SRV-6) — its line runs into the entrance, or it is in the entrance short of the post — which is
    /// what the officer steps aside for. A call already past them is at the scene and wants nothing of the mouth.
    /// </summary>
    bool ACallIsComingThrough(int car)
    {
        var entrance = _beat.EntranceOf(car);
        var postM = _beat.PostM[car];
        for (var other = 0; other < Cars.Count; other++)
        {
            if (other == car || !Cars.BlueLight[other] || Cars.Broken[other]) continue;

            var chain = Cars.ChainOf(other)[..Cars.Line[other].LaneCount];
            if (chain.Length == 0) continue;
            if (chain[0] == entrance ? Cars.ProgressM[other] - Cars.BuildOf(other).TailBehindAxleM < postM : chain.Contains(entrance))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The kerb beside the officer's post, a body clear of the lane — where they stand while a call goes through.</summary>
    Vector2 TheKerbBesideThePostM(int car)
    {
        var lane = _beat.EntranceOf(car);
        var across = Spline.SampleAt(_roads.ArcsOf(lane), Math.Clamp(_beat.PostM[car], 0f, _roads.LaneLengthM[lane])).Right;
        return TheOfficersPostM(car) + (across * _config.RoadSideSign * ((_roads.LaneWidthM[lane] * 0.5f) + _config.PersonDiameterM));
    }

    /// <summary>The ground off a car's kerb-side door, a body's width clear of its panels — where its officer gets out and back in.</summary>
    Vector2 TheKerbDoorM(int car) => (2f * Cars.PositionM[car]) - DriverDoorM(car);

    /// <summary>
    /// <b>The scene over, or the closure out of time</b>: the officer called back to the car, which waits for them
    /// with the lanes still closed — they are still standing in one (SRV-11).
    /// </summary>
    void CallTheOfficerBack(int car)
    {
        EnterThePatrolStage(car, PatrolStage.Reopening);
        var officer = _beat.Officer[car];
        if (officer >= 0 && !People.Inside[officer].Any) AimTheOfficer(officer, TheKerbDoorM(car));
    }

    /// <summary>
    /// <b>The officer back aboard</b>, walked to the door or put in their seat once the recall runs out, and the
    /// lanes given back with them. An officer knocked down, or taken off the errand by a hand, is not waited for.
    /// </summary>
    void BringTheOfficerBack(int car)
    {
        var officer = _beat.Officer[car];
        if (officer >= 0 && !People.Inside[officer].Any)
        {
            if (People.Wounded[officer] || People.Stage[officer] != TripStage.OnDuty)
            {
                _beat.Officer[car] = PatrolDuty.Nobody;
            }
            else if ((People.PositionM[officer] - TheKerbDoorM(car)).Length() <= _config.Service.CrewReachM
                     || _beat.SinceS[car] >= _config.ServiceRecallS)
            {
                TakeTheOfficerAboard(car, officer);
            }
            else
            {
                AimTheOfficer(officer, TheKerbDoorM(car));
                return;
            }
        }

        _beat.ClearTheCall(car);
        _closuresChanged = true;
        TakeTheNextPlace(car);
    }

    void TakeTheOfficerAboard(int car, int officer)
    {
        if (!_containers.TryTakeACrewSeat(car, officer)) return;

        Contain(officer);
    }

    /// <summary>Whether this car's officer is in their seat, which is what a car has to be to take a call (SRV-9).</summary>
    bool HasItsOfficerAboard(int car) =>
        _beat.Officer[car] is var officer and >= 0 && People.Inside[officer] == new Contained(ContainerKind.Car, car);

    /// <summary>
    /// <b>An officer whose car is not coming back for them</b> — wrecked, or gone without them: an ordinary walker
    /// from then, still in uniform, who draws a trip of their own.
    /// </summary>
    void ReleaseTheOfficer(int officer)
    {
        People.Walking[officer] = false;
        People.Stage[officer] = TripStage.StandingBy;
        People.TimerS[officer] = 0f;
        People.DestinationM[officer] = People.PositionM[officer];
    }
}
