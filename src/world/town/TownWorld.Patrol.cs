using System.Numerics;
using TrafficSimulation.Agents.Service;
using TrafficSimulation.World.Parking;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The police car's errand</b> (SRV-5): the half of a station's cars standing on its apron, the half driving
/// its district's beat (<c>TownWorld.Beat.cs</c>) — and <b>the call that interrupts both</b> (SRV-6), which is
/// what a police car is for (SRV-8). <b>The driving itself is the leg's</b> — a police car drives what every other car
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
/// <b>Where it goes is drawn and never searched for</b> (<see cref="DriveTheBeat"/>). Picking the
/// least-patrolled street would be a better beat and a worse rule — a search over the district on every
/// arrival, buying something nobody watching could tell from a draw.
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

    /// <summary>A police car stood on its station's apron (SRV-2, SRV-7), standing by for a call or its first beat.</summary>
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

        _beat.SinceS[car] += sinceLastDecisionS;

        switch (_beat.Stage[car])
        {
            case PatrolStage.Standing:
                // <b>A scene is what a stand is interrupted by</b> (SRV-6). Asked first, because a police
                // car standing on its apron is the one with least reason not to go.
                if (TakeAScene(car)) return;

                if (IsDueOnTheBeat(car)) TakeTheNextPlace(car);

                return;

            case PatrolStage.Patrolling:
                // <b>And a beat gives way to one</b>: a patrol is aimed at nothing (SRV-5), so a place drawn
                // out of a hat is never worth more than a road that has to be shut.
                if (TakeAScene(car)) return;

                if (IsDoneWithThePlace(car, _beat.SinceS[car])) TakeTheNextPlace(car);

                return;

            case PatrolStage.Attending:
                RunToTheEntrance(car);
                return;

            case PatrolStage.Closing:
                HoldTheRoadClosed(car, sinceLastDecisionS);
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

    /// <summary>Standing on its apron — where the half of a station's cars that does not patrol waits for a call.</summary>
    void StandBy(int car) => EnterThePatrolStage(car, PatrolStage.Standing);

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
    /// <b>What a police car does with nothing to do</b>: the next place of its district's beat where it is one of
    /// the station's patrols, and home to its bay where it is not — or where there is no street to be sent to.
    /// </summary>
    void TakeTheNextPlace(int car)
    {
        _beat.SinceS[car] = 0f;
        if (_serviceBeat.Patrols[car] && DriveTheBeat(car)) return;

        ReturnToTheStation(car);
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
