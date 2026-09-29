using System.Numerics;
using TrafficSimulation.Agents.Service;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Parking;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The beat</b> (SRV-5): the police cars standing on a station's apron, and the errand that takes each
/// of them round the town and brings it back — and <b>the call that interrupts it</b> (SRV-6), which is what a
/// police car is for (SRV-8). <b>The driving itself is the leg's</b> — a police car drives what every other car
/// drives (CAR-15) — and what is here is only the reason those legs are being driven. What happens at a scene
/// once the car is there is <c>TownWorld.Closure.cs</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the rescue's machine with nothing urgent in it</b>, and deliberately built the same way: a
/// stage per observable state, one place each transition happens, and the leg itself handed to the
/// ordinary drive-leg machinery. What a beat does not have is the whole of AMB-4 — it carries no
/// priority, no blue light and no pace of its own, and holds its road like anybody else; the leg out to a
/// scene is the one that does (SRV-6). A police car on its beat is traffic that goes somewhere nobody
/// lives.
/// </para>
/// <para>
/// <b>Where it goes is drawn and never searched for.</b> Nothing in the town asks for a police car, so a
/// beat cannot be aimed at anything; what a patrol is, is a car that keeps choosing a place on a lane and
/// driving to it. Picking the least-patrolled quarter would be a better beat and a worse rule — a search over the
/// town on every arrival, buying something nobody watching could tell from a draw.
/// </para>
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>What every police car is doing about its beat (SRV-5).</summary>
    public PatrolDuty Beat => _beat;

    /// <summary>
    /// Whether this car is one of the town's patrols, which is the one question the decision loop asks
    /// before running the errand — a police car is a car with a station.
    /// </summary>
    bool IsAPatrolCar(int car) => _beat.Station[car] != PatrolDuty.NoBuilding;

    /// <summary>
    /// <b>Whether this patrol's leg ends at a place on a lane rather than in a bay</b>: a beat's place and a
    /// closure's entrance are both somewhere in the road (SRV-5, SRV-9), and only the drive home is aimed at a bay.
    /// </summary>
    bool IsOnItsBeatOrToAScene(int car) =>
        IsAPatrolCar(car) && _beat.Stage[car] is PatrolStage.Patrolling or PatrolStage.Attending or PatrolStage.Closing
            or PatrolStage.Reopening;

    /// <summary>
    /// A police car stood on its station's apron (SRV-2, SRV-7), standing by for its first beat. <b>The first
    /// stand is drawn like every later one</b>, so four cars stood in the same instant do not leave in it.
    /// </summary>
    void BeginTheBeat(int car, int station, int bay)
    {
        _beat.Station[car] = station;
        _beat.HomeBay[car] = bay;
        StandBy(car);
    }

    /// <summary>
    /// One decision of one patrol's errand, taken before the leg's own. <b>It decides the errand and never
    /// the driving</b>: what comes out of it is a destination and a chain, and the leg does the rest.
    /// </summary>
    void RunThePatrol(int car, float sinceLastDecisionS)
    {
        if (Cars.Broken[car])
        {
            LetTheCallOfAWreckGo(car);
            return;
        }

        // The elapsed the beat's own clocks integrate over is the driver's and not the loop's nominal
        // interval, for the reason a rescue's is (<see cref="RunTheRescue"/>).
        var elapsedS = Cars.SinceDecisionS[car] > 0f ? Cars.SinceDecisionS[car] : sinceLastDecisionS;
        _beat.SinceS[car] += elapsedS;

        switch (_beat.Stage[car])
        {
            case PatrolStage.Standing:
                // <b>A scene is what a stand is interrupted by</b> (SRV-6). Asked first, because a police
                // car waiting out its rest is the one with least reason not to go.
                if (TakeAScene(car)) return;

                if (_beat.SinceS[car] >= _beat.RestS[car]) SetOutOnABeat(car);

                return;

            case PatrolStage.Patrolling:
                // <b>And a beat gives way to one</b>: a patrol is aimed at nothing (SRV-5), so a place drawn
                // out of a hat is never worth more than a road that has to be shut.
                if (TakeAScene(car)) return;

                // Arrived, or the leg has run out of clock: either way this place is done with. A patrol has
                // nowhere it must be, so a road that would not let it through costs it the next street and
                // nothing else.
                if (!Cars.Driven[car] || _beat.SinceS[car] >= _config.PatrolGiveUpS) TakeTheNextPlace(car);

                return;

            case PatrolStage.Attending:
                RunToTheEntrance(car);
                return;

            case PatrolStage.Closing:
                HoldTheRoadClosed(car, elapsedS);
                return;

            case PatrolStage.Reopening:
                BringTheOfficerBack(car);
                return;

            case PatrolStage.ReturningToStation:
                // <b>A drive home is a car with nothing to do</b>, and a scene is worth more than the bay.
                if (TakeAScene(car)) return;

                // <b>Home, or out of clock, or a leg that ended short of it.</b> The last of those is laid
                // again from where the car has got to rather than given up on (CAR-15): a patrol that stood
                // down in the street the first time the traffic stopped it would leave its apron empty for
                // the rest of the run, which is the whole thing the apron is held for.
                if (_parking.BayOf(car) == _beat.HomeBay[car] || _beat.SinceS[car] >= _config.PatrolGiveUpS)
                {
                    StandBy(car);
                }
                else if (!Cars.Driven[car])
                {
                    SendHome(car);
                }

                return;
        }
    }

    /// <summary>Standing on its apron with the next beat's interval drawn — where a police car spends most of a run.</summary>
    void StandBy(int car)
    {
        EnterThePatrolStage(car, PatrolStage.Standing);
        _beat.LegsLeft[car] = 0;
        _beat.RestS[car] = Cars.Draw[car].NextFloat(
            _config.Service.RestBetweenBeatsMinS, _config.Service.RestBetweenBeatsMaxS);
    }

    /// <summary>Out on a beat of a drawn number of places, the first of them chosen here.</summary>
    void SetOutOnABeat(int car)
    {
        EnterThePatrolStage(car, PatrolStage.Patrolling);
        _beat.LegsLeft[car] = 1 + Cars.Draw[car].NextInt(_config.Service.MostPlacesOnABeat);
        TakeTheNextPlace(car);
    }

    /// <summary>
    /// <b>The one place a patrol's stage changes</b>, so the priority is decided in exactly one place
    /// (SRV-6) and the beat's own clock means the same thing in each of them — the shape a rescue's
    /// <see cref="EnterTheStage"/> and a recovery's <see cref="EnterTheRecoveryStage"/> both take. <b>And the one
    /// place a closure begins or ends</b>, which is what says the road's closures are to be laid again.
    /// </summary>
    void EnterThePatrolStage(int car, PatrolStage stage)
    {
        var closed = _beat.Closes(car);
        _beat.Stage[car] = stage;
        _beat.SinceS[car] = 0f;
        Cars.BlueLight[car] = _beat.ShowsItsLight(car);
        if (_beat.Closes(car) != closed) _closuresChanged = true;
    }

    /// <summary>
    /// <b>The nearest scene with a lane nobody is closing yet, and the run to that lane's entrance</b> (SRV-6,
    /// SRV-9) — a casualty lying in the road (AMB-5) or a wreck standing in it (EVA-1), taken on the terms a rescue
    /// and a recovery take their own calls: nearest is measured against every other free patrol and not against
    /// every other scene, and <b>it is one patrol to each lane the scene lies across</b> — two where it spans the
    /// road (SRV-9).
    /// </summary>
    /// <remarks>
    /// <b>The common case is two integers.</b> A town with nobody down and nothing broken asks
    /// <see cref="_woundedCount"/> and <see cref="_wreckCount"/> and goes back to its beat, which is what
    /// makes this affordable on every patrol's decision.
    /// </remarks>
    bool TakeAScene(int car)
    {
        if (_woundedCount == 0 && _wreckCount == 0) return false;

        // What closes a lane is somebody standing at its mouth (SRV-9), so a car with nobody aboard to put
        // there has nothing to close it with.
        if (!HasItsOfficerAboard(car)) return false;

        var fromM = Cars.PositionM[car];
        var casualty = PatrolDuty.Nobody;
        var wreck = PatrolDuty.Nobody;
        var bestM = float.PositiveInfinity;

        // Whether a scene is taken is a walk of the fleet, so only one nearer than the best is asked it.
        for (var person = 0; person < People.Count; person++)
        {
            var farM = (People.PositionM[person] - fromM).LengthSquared();
            if (farM >= bestM || !IsASceneWorthClosing(car, person, PatrolDuty.Nobody)) continue;

            casualty = person;
            wreck = PatrolDuty.Nobody;
            bestM = farM;
        }

        for (var broken = 0; broken < Cars.Count; broken++)
        {
            var farM = (Cars.PositionM[broken] - fromM).LengthSquared();
            if (farM >= bestM || !IsASceneWorthClosing(car, PatrolDuty.Nobody, broken)) continue;

            wreck = broken;
            casualty = PatrolDuty.Nobody;
            bestM = farM;
        }

        if (casualty < 0 && wreck < 0) return false;
        if (!IsTheNearestFreePatrolTo(car, TheSceneM(casualty, wreck), bestM)) return false;
        if (!TakeALaneOfTheScene(car, casualty, wreck)) return false;

        _beat.Casualty[car] = casualty;
        _beat.Wreck[car] = wreck;
        _beat.ClosedForS[car] = 0f;
        ClosuresTaken++;
        EnterThePatrolStage(car, PatrolStage.Attending);
        SendTo(car, TheStandM(car), ParkingRegistry.NoBay);
        return true;
    }

    /// <summary>
    /// <b>A scene still worth closing</b>: a body still lying in the town, or a wreck still standing in it and on
    /// nobody's hook, <b>with a lane under it no other patrol is closing yet</b> (SRV-9) — for this scene or for
    /// another lying on the same lane, since one closure holds a lane for everything on it.
    /// </summary>
    bool IsASceneWorthClosing(int car, int casualty, int wreck)
    {
        if (!TheSceneStands(casualty, wreck)) return false;

        Span<int> lanes = stackalloc int[MostLanesUnderAScene];
        var count = TheScenesLanes(casualty, wreck, lanes);
        for (var at = 0; at < count; at++)
        {
            if (!IsClosedByAnother(car, lanes[at])) return true;
        }

        return false;
    }

    /// <summary>Whether a scene is still there to be closed round — the one question the errand and the dispatch both ask of it.</summary>
    bool TheSceneStands(int casualty, int wreck) => casualty >= 0
        ? People.Wounded[casualty] && !People.Inside[casualty].Any
        : wreck >= 0 && Cars.Broken[wreck] && !_recovery.InTheYard[wreck] && _recovery.OnTheHookOf[wreck] < 0;

    /// <summary>Where the scene this call is for actually is — the one place the two rosters are read as one thing.</summary>
    Vector2 TheSceneM(int casualty, int wreck) =>
        casualty >= 0 ? People.PositionM[casualty] : Cars.PositionM[wreck];

    /// <summary>
    /// <b>Whether this is the patrol SRV-6 means</b> — the nearest one with nothing else to do — asked of
    /// the scene it was about to take. <see cref="IsTheNearestFreeAmbulanceTo"/>'s own argument said of a
    /// station: the call belongs to the scene and the choice belongs to the patrol, and asking them the other
    /// way round sends whichever car's decision happened to run first.
    /// </summary>
    /// <remarks>
    /// <b>Free is a patrol that would take the call</b> — standing, on its beat or on its way home, with its
    /// officer aboard and no hand on it. Counted as free while it would not, it held back every patrol behind it
    /// and the scene was taken by nobody.
    /// </remarks>
    bool IsTheNearestFreePatrolTo(int car, Vector2 sceneM, float farM)
    {
        for (var other = 0; other < Cars.Count; other++)
        {
            if (other == car || !TakesCalls(other)) continue;

            var otherM = (sceneM - Cars.PositionM[other]).LengthSquared();
            if (otherM < farM || (otherM == farM && other < car)) return false;
        }

        return true;
    }

    /// <summary>A patrol that would take a call now, which is what <see cref="IsTheNearestFreePatrolTo"/> counts as free.</summary>
    bool TakesCalls(int car) =>
        IsAPatrolCar(car) && !Cars.Broken[car] && !_beat.IsOnACall(car) && !IsUnderOrders(car)
        && HasItsOfficerAboard(car)
        && _beat.Stage[car] is PatrolStage.Standing or PatrolStage.Patrolling or PatrolStage.ReturningToStation;

    /// <summary>
    /// <b>The call given up on the way</b>: the priority lost with it, and the beat picked up where the call
    /// interrupted it, since a call is an interruption and not the end of a shift (SRV-5).
    /// </summary>
    void GiveUpTheScene(int car)
    {
        _beat.ClearTheCall(car);
        TakeTheNextPlace(car);
    }

    /// <summary>
    /// <b>A police car wrecked</b> (SRV-4): whatever it was closing is given back, and an officer standing out on
    /// the road is an ordinary walker from then — the car is not coming back for them.
    /// </summary>
    void LetTheCallOfAWreckGo(int car)
    {
        if (!_beat.IsOnACall(car) && _beat.Officer[car] < 0) return;

        if (_beat.Closes(car)) _closuresChanged = true;

        var officer = _beat.Officer[car];
        if (officer >= 0 && !People.Inside[officer].Any)
        {
            ReleaseTheOfficer(officer);
            _beat.Officer[car] = PatrolDuty.Nobody;
        }

        _beat.ClearTheCall(car);
    }

    /// <summary>
    /// One place of a beat done with: the next one drawn, or the station where the beat runs out.
    /// </summary>
    void TakeTheNextPlace(int car)
    {
        _beat.SinceS[car] = 0f;
        if (_beat.LegsLeft[car] > 0 && SendOnPatrol(car))
        {
            _beat.LegsLeft[car]--;
            return;
        }

        ReturnToTheStation(car);
    }

    /// <summary>
    /// <b>A place on one of the town's lanes, drawn from this car's own stream</b> — the whole of where a
    /// beat goes. False where the map has no lane to be sent to, which sends the car home instead of
    /// nowhere.
    /// </summary>
    /// <remarks>
    /// <b>Somewhere along a lane and never a junction's middle.</b> A leg ends by the car standing where it
    /// got to, so a destination is a place a patrol will be parked for a moment — and the middle of a
    /// junction is the one place in this town where standing still is being driven into. Aimed at the
    /// junction centres, the fixture town's patrol was wrecked inside the first box it reached.
    /// </remarks>
    bool SendOnPatrol(int car)
    {
        var lanes = _roads.LaneCount;
        if (lanes == 0) return false;

        // <b>A street and never a bay's arm</b> (GEN-4h): an arm is a space and a dead end, and a leg aimed
        // at a place on it is a car driven into a bay it holds no claim on.
        ref var draw = ref Cars.Draw[car];
        var lane = draw.NextInt(lanes);
        for (var redraw = 0; redraw < lanes && _roads.IsABayArm(lane); redraw++)
        {
            lane = (lane + 1) % lanes;
        }

        var alongM = draw.NextFloat() * _roads.LaneLengthM[lane];

        EnterThePatrolStage(car, PatrolStage.Patrolling);
        SendTo(car, Spline.SampleAt(_roads.ArcsOf(lane), alongM).PositionM, ParkingRegistry.NoBay);
        return true;
    }

    /// <summary>The beat over: the clock restarted on the drive home, and the first attempt at it made.</summary>
    void ReturnToTheStation(int car)
    {
        EnterThePatrolStage(car, PatrolStage.ReturningToStation);
        SendHome(car);
    }

    /// <summary>
    /// One attempt at the drive back to its own bay on the station's apron (GEN-4k). A car already standing
    /// in it, or one whose station never had an apron to give, stands by where it is: a police car in the
    /// road is still a police car, and the next beat is what moves it.
    /// </summary>
    void SendHome(int car)
    {
        var home = _beat.HomeBay[car];
        if (home < 0 || _parking.BayOf(car) == home || !_parking.IsFreeFor(car, home))
        {
            StandBy(car);
            return;
        }

        SendTo(car, _parking.CentreM(home), home);
    }
}
