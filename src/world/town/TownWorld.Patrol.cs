using System.Numerics;
using TrafficSimulation.Agents.Car.Maneuvers;
using TrafficSimulation.Agents.Service;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Parking;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The beat</b> (SRV-5): the police cars standing on a station's apron, and the errand that takes each
/// of them round the town and brings it back. <b>The driving itself is the catalogue's</b> — a police car
/// runs the same entries and the same ladder every other car runs — and what is here is only the reason
/// those manoeuvres are being run.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the rescue's machine with nothing urgent in it</b>, and deliberately built the same way: a
/// stage per observable state, one place each transition happens, and the leg itself handed to the
/// ordinary drive-leg machinery. What it does not have is the whole of AMB-4 — a patrol carries no
/// priority, no blue light and no pace of its own, and holds its road like anybody else. A police car in
/// this town is traffic that goes somewhere nobody lives.
/// </para>
/// <para>
/// <b>Where it goes is drawn and never searched for.</b> Nothing in the town asks for a police car, so a
/// beat cannot be aimed at anything; what a patrol is, is a car that keeps choosing a junction and driving
/// to it. Picking the least-patrolled quarter would be a better beat and a worse rule — a search over the
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
    /// A police car stood on its station's apron (SRV-2), standing by for its first beat. <b>The first
    /// stand is drawn like every later one</b>, so four cars stood in the same instant do not leave in it.
    /// </summary>
    void BeginTheBeat(int car, int station, int bay)
    {
        _beat.Station[car] = station;
        _beat.HomeBay[car] = bay;
        StandBy(car);
    }

    /// <summary>
    /// One decision of one patrol's crew, taken before the driver's own. <b>It decides the errand and never
    /// the driving</b>: what comes out of it is a destination and a chain, and the catalogue does the rest.
    /// </summary>
    void RunThePatrol(int car, float sinceLastDecisionS)
    {
        if (Cars.Broken[car]) return;

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
                RunToTheSceneToCloseIt(car);
                return;

            case PatrolStage.Closing:
                HoldTheRoadClosed(car, elapsedS);
                return;

            case PatrolStage.BoardingAtTheScene:
                // The officer walking back to his seat. Nothing is driven until he is in it (SRV-3), and
                // the lane he was holding is already the town's again — the closure goes with the body.
                if (!TheHandIsAboard(car, elapsedS)) return;

                _beat.ClearTheCall(car);
                TakeTheNextPlace(car);
                return;

            case PatrolStage.ReturningToStation:
                // <b>Home, or out of clock, or a leg that ended short of it.</b> The last of those is laid
                // again from where the car has got to rather than given up on (MAN-3): a patrol that stood
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
    /// <see cref="EnterTheStage"/> and a recovery's <see cref="EnterTheRecoveryStage"/> both take.
    /// </summary>
    void EnterThePatrolStage(int car, PatrolStage stage)
    {
        _beat.Stage[car] = stage;
        _beat.SinceS[car] = 0f;
        Cars.BlueLight[car] = _beat.IsHurrying(car);
    }

    /// <summary>
    /// <b>The nearest scene nobody is on their way to, and the run to it</b> (SRV-6) — a casualty lying in
    /// the road (AMB-5) or a wreck standing in one (EVA-1), on the terms a rescue and a recovery already
    /// take their own calls: nearest is measured against every other free patrol and not against every other
    /// scene, and it is one call to a scene and one scene to a call.
    /// </summary>
    /// <remarks>
    /// <b>The common case is two integers.</b> A town with nobody down and nothing broken asks
    /// <see cref="_woundedCount"/> and <see cref="_wreckCount"/> and goes back to its beat, which is what
    /// makes this affordable on every patrol's decision.
    /// </remarks>
    bool TakeAScene(int car)
    {
        if (_woundedCount == 0 && _wreckCount == 0) return false;

        var fromM = Cars.PositionM[car];
        var casualty = PatrolDuty.Nobody;
        var wreck = PatrolDuty.Nobody;
        var bestM = float.PositiveInfinity;

        for (var person = 0; person < People.Count; person++)
        {
            if (!IsASceneWorthClosing(person, aCar: false)) continue;

            var farM = (People.PositionM[person] - fromM).LengthSquared();
            if (farM >= bestM) continue;

            casualty = person;
            wreck = PatrolDuty.Nobody;
            bestM = farM;
        }

        for (var broken = 0; broken < Cars.Count; broken++)
        {
            if (!IsASceneWorthClosing(broken, aCar: true)) continue;

            var farM = (Cars.PositionM[broken] - fromM).LengthSquared();
            if (farM >= bestM) continue;

            wreck = broken;
            casualty = PatrolDuty.Nobody;
            bestM = farM;
        }

        if (casualty < 0 && wreck < 0) return false;
        if (!IsTheNearestFreePatrolTo(car, TheSceneM(casualty, wreck), bestM)) return false;

        _beat.Casualty[car] = casualty;
        _beat.Wreck[car] = wreck;
        _beat.ClosedForS[car] = 0f;
        EnterThePatrolStage(car, PatrolStage.Attending);
        SendTo(car, ThePoliceStandoffM(TheSceneM(casualty, wreck)), ParkingRegistry.NoBay);
        return true;
    }

    /// <summary>
    /// A scene still worth putting a road closed round: a body still lying in the town, or a wreck still
    /// standing in it, and neither already somebody's.
    /// </summary>
    bool IsASceneWorthClosing(int index, bool aCar)
    {
        if (aCar)
        {
            if (!Cars.Broken[index] || _recovery.InTheYard[index]) return false;
        }
        else if (!People.Wounded[index] || People.Inside[index].Any)
        {
            return false;
        }

        for (var car = 0; car < Cars.Count; car++)
        {
            if ((aCar ? _beat.Wreck[car] : _beat.Casualty[car]) == index) return false;
        }

        return true;
    }

    /// <summary>Where the scene this call is for actually is — the one place the two rosters are read as one thing.</summary>
    Vector2 TheSceneM(int casualty, int wreck) =>
        casualty >= 0 ? People.PositionM[casualty] : Cars.PositionM[wreck];

    /// <summary>
    /// <b>Whether this is the patrol SRV-6 means</b> — the nearest one with nothing else to do — asked of
    /// the scene it was about to take. <see cref="IsTheNearestFreeAmbulanceTo"/>'s own argument said of a
    /// station: the call belongs to the scene and the choice belongs to the crew, and asking them the other
    /// way round sends whichever car's decision happened to run first.
    /// </summary>
    bool IsTheNearestFreePatrolTo(int car, Vector2 sceneM, float farM)
    {
        for (var other = 0; other < Cars.Count; other++)
        {
            if (other == car || !IsAPatrolCar(other) || Cars.Broken[other] || _beat.IsOnACall(other)) continue;

            var otherM = (sceneM - Cars.PositionM[other]).LengthSquared();
            if (otherM < farM || (otherM == farM && other < car)) return false;
        }

        return true;
    }

    /// <summary>
    /// <b>Where a police car is stopped for a scene</b> (SRV-6): further back along the lane than the
    /// ambulance's own standoff (AMB-10), because the vehicle whose errand is to keep the ground clear is
    /// the one that has no business standing on it.
    /// </summary>
    Vector2 ThePoliceStandoffM(Vector2 sceneM)
    {
        var lane = _roads.NearestLane(sceneM, out var alongM);
        if (lane < 0) return sceneM;

        var forward = Spline.SampleAt(_roads.ArcsOf(lane), alongM).Direction;
        return sceneM - (forward * _config.PoliceStandoffM);
    }

    /// <summary>
    /// <b>The drive out to a scene</b>, and the officer out of the car once it has stopped short of one.
    /// The bound is the beat's own (SRV-5): a scene the traffic will not let a patrol reach costs it the
    /// leg and nothing more, because the closure is a courtesy to whoever is working there and never the
    /// thing that saves anybody.
    /// </summary>
    void RunToTheSceneToCloseIt(int car)
    {
        if (!TheSceneStillStands(car, out var sceneM)) return;

        if (_beat.SinceS[car] >= _config.PatrolGiveUpS)
        {
            GiveUpTheScene(car);
            return;
        }

        var standoffM = ThePoliceStandoffM(sceneM);
        if (!Cars.Driven[car])
        {
            SendTo(car, standoffM, ParkingRegistry.NoBay);
            return;
        }

        // <b>Near the scene rather than on its own mark</b>, and that is the standoff paying for itself: a
        // police car queues behind whatever else has been called here — the ambulance is aimed at a mark
        // half as far back (AMB-10) — and what closes the road is a man walking, so where the traffic let
        // the car stop matters only up to how far he is willing to walk.
        var atRest = Cars.VelocityMps[car].Length() <= _config.Driving.StopSpeedMps;
        if (!atRest || (Cars.PositionM[car] - sceneM).Length() > _config.PoliceClosureM)
        {
            if (atRest && Cars.Doing[car] == Maneuver.AttendTheScene) SendTo(car, standoffM, ParkingRegistry.NoBay);

            return;
        }

        EnterThePatrolStage(car, PatrolStage.Closing);
    }

    /// <summary>
    /// <b>The road held closed</b> (SRV-6): the officer standing beside the carriageway the scene lies on,
    /// holding a stretch of it at a rank ordinary traffic does not outrank and a rescue does.
    /// </summary>
    /// <remarks>
    /// <b>Beside the road and not in it.</b> What a closure is, is ground spoken for; a body standing in the
    /// lane would be a thing the rescue itself has to be held off (AMB-4a), which is the closure working
    /// backwards. So the officer stands on the far side of the kerb line and the claim does the refusing.
    /// </remarks>
    void HoldTheRoadClosed(int car, float sinceLastDecisionS)
    {
        if (!TheSceneStillStands(car, out var sceneM)) return;

        // <b>A closure is bounded</b> (SRV-6). A scene nothing ever clears would otherwise hold a street out
        // of the town for the rest of the run, which is the one failure a soft reservation can cause.
        _beat.ClosedForS[car] += sinceLastDecisionS;
        if (_beat.ClosedForS[car] >= _config.PoliceClosureLifeS)
        {
            GiveUpTheScene(car);
            return;
        }

        if (!TheHandHasReached(car, TheClosingPlaceM(car, sceneM))) return;

        People.ClosesTheRoadM[TheHandOf(car)] = _config.PoliceClosureM;
    }

    /// <summary>
    /// <b>Where the officer stands to close this scene's road</b>: beside the lane it lies on, a body's
    /// width the far side of the kerb line, <b>on the side his own car came up</b> — which is the side of
    /// that carriageway there is a pavement on, without anything here having to know which way the town
    /// drives.
    /// </summary>
    Vector2 TheClosingPlaceM(int car, Vector2 sceneM)
    {
        var lane = _roads.NearestLane(sceneM, out var alongM);
        if (lane < 0) return sceneM;

        var at = Spline.SampleAt(_roads.ArcsOf(lane), alongM);
        var across = new Vector2(-at.Direction.Y, at.Direction.X);
        var offM = Vector2.Dot(Cars.PositionM[car] - at.PositionM, across);
        var side = offM != 0f ? MathF.Sign(offM) : _config.RoadSideSign;
        return at.PositionM + (across * side * ((_roads.LaneWidthM[lane] * 0.5f) + _config.PersonDiameterM));
    }

    /// <summary>
    /// <b>Whether there is still a scene here to be closed round</b>: the casualty collected or the wreck
    /// hitched is the errand over, which is the same question the rescue and the recovery ask of their own
    /// calls before giving them up.
    /// </summary>
    bool TheSceneStillStands(int car, out Vector2 sceneM)
    {
        var casualty = _beat.Casualty[car];
        var wreck = _beat.Wreck[car];
        sceneM = default;

        var stands = casualty >= 0
            ? People.Wounded[casualty] && !People.Inside[casualty].Any
            : wreck >= 0 && Cars.Broken[wreck] && !_recovery.InTheYard[wreck]
              && _recovery.OnTheHookOf[wreck] < 0;

        if (!stands)
        {
            GiveUpTheScene(car);
            return false;
        }

        sceneM = TheSceneM(casualty, wreck);
        return true;
    }

    /// <summary>
    /// <b>Where `P-18` is to stop this police car</b>, and false when nothing is asking it to (SRV-6). The
    /// place outlasts the arrival for the reason a recovery's does: a stop point that went away the moment
    /// the car reached it would let the vehicle roll off with its officer standing at the kerb.
    /// </summary>
    bool TheClosureStopsAt(int car, out Vector2 standoffM)
    {
        standoffM = default;
        if (_beat.Stage[car] is not (PatrolStage.Attending or PatrolStage.Closing
            or PatrolStage.BoardingAtTheScene))
        {
            return false;
        }

        if (!Cars.HasDestination[car]) return false;

        standoffM = Cars.DestinationM[car];
        return true;
    }

    /// <summary>
    /// The scene let go of: the lane given back to the town, the officer walked in before the car drives
    /// anywhere (SRV-3), and <b>the beat picked up where the call interrupted it</b> — a patrol with places
    /// left goes to the next one rather than home, since a call is an interruption and not the end of a
    /// shift (SRV-5).
    /// </summary>
    void GiveUpTheScene(int car)
    {
        var hand = TheHandOf(car);
        if (hand >= 0) People.ClosesTheRoadM[hand] = 0f;

        if (TheHandIsOut(car))
        {
            EnterThePatrolStage(car, PatrolStage.BoardingAtTheScene);
            return;
        }

        _beat.ClearTheCall(car);
        TakeTheNextPlace(car);
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

        ref var draw = ref Cars.Draw[car];
        var lane = draw.NextInt(lanes);
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
