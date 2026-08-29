using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Car.Maneuvers;
using TrafficSimulation.Agents.Ambulance;
using TrafficSimulation.Agents.Evacuator;
using TrafficSimulation.Agents.Service;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Physics;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The recovery</b> (EVA-1…8): the depots a map has, the evacuator standing at each, and the errand it
/// runs when a car is wrecked in the street. <b>The driving itself is the catalogue's</b> — an evacuator
/// runs `P-2`, `P-4`, `P-8`, `P-14`, `P-17`, `P-18` and the ladder like every other car — and what is here
/// is only the errand those manoeuvres are being run for.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the rescue's machine with a car for a casualty</b>, and deliberately built the same way: a
/// stage per observable state, one place each transition happens, and the leg itself handed to the ordinary
/// drive-leg machinery. The two differences are both rules rather than mechanism — the priority is the
/// outbound leg only (EVA-4), and what is collected is not carried but <em>towed</em>, which is
/// <see cref="TowBar"/> and <c>TownWorld.Bodies.cs</c>.
/// </para>
/// <para>
/// <b>Where a wreck ends up is a place and not a container.</b> A casualty goes inside the ambulance and
/// then inside the hospital; a wreck stays a body in the world the whole way (PHY-5), on the bar and then
/// standing in a yard slot, which is why nothing here touches the containment register.
/// </para>
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>What every evacuator is doing about its wreck (EVA-3).</summary>
    public RecoveryDuty Recovery => _recovery;

    /// <summary>How many cars have been wrecked in the town — every one of them a recovery raised.</summary>
    public long WrecksRaised { get; private set; }

    /// <summary>How many an evacuator reached and got onto the bar.</summary>
    public long WrecksHitched { get; private set; }

    /// <summary>How many were set down in a depot's yard, which is the figure the whole slice is for.</summary>
    public long WrecksYarded { get; private set; }

    /// <summary>And how many of those stood there long enough to be cars again (EVA-7).</summary>
    public long WrecksRestored { get; private set; }

    /// <summary>
    /// How many recoveries ran out of clock before the wreck was reached (EVA-8) — the honest other half of
    /// the counts above, and what says a town is jammed rather than busy.
    /// </summary>
    public long RecoveriesGivenUp { get; private set; }

    /// <summary>
    /// How many times an evacuator with a wreck to fetch had nowhere to put it (EVA-2). <b>A full yard is a
    /// depot that has stopped collecting</b>, which is a real state and is reported rather than hidden.
    /// </summary>
    public long YardsFoundFull { get; private set; }

    /// <summary>
    /// How many wrecks are lying in the town waiting to be fetched. <b>The reason the common case is
    /// free</b>: an evacuator with nothing to clear asks one integer rather than walking the fleet.
    /// </summary>
    int _wreckCount;

    /// <summary>And how many are standing in a yard being mended, which is what the repair pass is skipped on.</summary>
    int _mendingCount;

    /// <summary>And how many are on a bar this instant, which is what the coupling phase is skipped on.</summary>
    int _onTheBar;

    /// <summary>
    /// <b>The car stood in the first bay of a depot's run becomes that depot's evacuator</b> (SRV-2, EVA-2):
    /// the yard it delivers to and the bay held for it, which is where every recovery begins and ends.
    /// </summary>
    void TakeUpTheRecovery(int car, int depot, int yard)
    {
        _recovery.Depot[car] = depot;
        _recovery.Yard[car] = yard;
        _recovery.HomeBay[car] = _parking.BayOf(car);
    }

    /// <summary>Whether this car is a town's evacuator, which is the one question the decision loop asks before running the errand.</summary>
    bool IsAnEvacuator(int car) => _recovery.Depot[car] != RecoveryDuty.NoBuilding;

    /// <summary>
    /// <b>How far off its line a car may be before the road calls the line lost</b> (CAR-10a) — the nominal
    /// car's allowance, and <b>a coupled pair's own</b> (EVA-5).
    /// </summary>
    /// <remarks>
    /// <b>A line is a recommendation and a tow cannot take it as tightly.</b> The town's corners are laid
    /// for the nominal car (CAR-11a); a truck half as long again with a car on the back of it swings wide of
    /// every one of them, and measured against a car's allowance it is declared lost mid-corner — which
    /// stops it, which is what leaves a tow standing across a junction until the clock runs out. The
    /// allowance is what says a body has stopped driving its line, so it is a fact about the body, and this
    /// is the one body in the town that is two.
    /// </remarks>
    float OffTheLineAllowanceM(int car) =>
        _config.CarOffPathM * OffLineTolerance
        * (_recovery.Towing[car] >= 0 ? _config.Evacuator.TowedLineAllowance : 1f);

    /// <summary>
    /// <b>EVA-1: a car in its terminal state is a recovery waiting to be run.</b> Raised where the wreck
    /// happens, so nothing has to search the fleet for one.
    /// </summary>
    void RaiseTheRecovery(int car)
    {
        if (Cars.Broken[car]) return;

        _wreckCount++;
        WrecksRaised++;
    }

    /// <summary>
    /// One decision of one evacuator's crew, taken before the driver's own (EVA-3). <b>It decides the errand
    /// and never the driving</b>: what comes out of it is a destination, a chain and whether the priority is
    /// carried, and the catalogue does the rest.
    /// </summary>
    void RunTheRecovery(int car, float sinceLastDecisionS)
    {
        // A wrecked evacuator decides nothing (SRV-4). The roster hands terminal bodies no ticks, and what
        // this one was holding was let go on the tick it broke (<see cref="LoseTheEvacuator"/>).
        if (Cars.Broken[car]) return;

        // The elapsed the recovery's own clocks integrate over is the driver's and not the loop's nominal
        // interval, for the reason a rescue's is (<see cref="RunTheRescue"/>).
        var elapsedS = Cars.SinceDecisionS[car] > 0f ? Cars.SinceDecisionS[car] : sinceLastDecisionS;

        var stage = _recovery.Stage[car];
        if (stage != RecoveryStage.Waiting) _recovery.SinceS[car] += elapsedS;

        switch (stage)
        {
            case RecoveryStage.Waiting:
                TakeAWreck(car);
                return;

            case RecoveryStage.Running:
            case RecoveryStage.Hitching:
                RunToTheWreck(car, elapsedS);
                return;

            case RecoveryStage.BoardingAtTheScene:
                // The wreck is on the bar and the man is walking back: nothing is hauled until he is in his
                // seat, for the reason a rescue lays no delivery until its paramedic is in theirs (SRV-3).
                if (!TheHandIsAboard(car, elapsedS)) return;

                EnterTheRecoveryStage(car, RecoveryStage.Hauling);
                SendToTheYard(car);
                return;

            case RecoveryStage.Hauling:
                HaulToTheYard(car);
                return;

            case RecoveryStage.Unhitching:
                UnhitchIntoTheYard(car, elapsedS);
                return;

            case RecoveryStage.BoardingAtTheYard:
                if (!TheHandIsAboard(car, elapsedS)) return;

                GoBackToTheDepot(car);
                return;

            case RecoveryStage.GoingHome:
                if (!Cars.Driven[car]) StandTheEvacuatorDown(car);

                return;
        }
    }

    /// <summary>
    /// <b>The nearest wreck nobody is on their way to</b>, and the run to it. Asked only of an evacuator
    /// standing at its depot, only while there is anything to fetch, and <b>only while its yard has a slot
    /// free to put it in</b> (EVA-3).
    /// </summary>
    /// <remarks>
    /// <b>Somewhere to put it is part of taking the call and not a problem discovered on arrival.</b> An
    /// evacuator that set off with a full yard would arrive with a wreck on the bar and nowhere to set it
    /// down, and would then stand at the yard holding it for the rest of the run — which is the town's only
    /// evacuator taken out of service by bookkeeping. A depot whose yard is full has stopped collecting, and
    /// that is a state with a count against it (<see cref="YardsFoundFull"/>).
    /// </remarks>
    void TakeAWreck(int car)
    {
        if (_wreckCount == 0) return;

        if (AFreeYardSlot(_recovery.Yard[car]) < 0)
        {
            YardsFoundFull++;
            return;
        }

        var fromM = Cars.PositionM[car];
        var best = RecoveryDuty.Nothing;
        var bestM = float.PositiveInfinity;
        for (var wreck = 0; wreck < Cars.Count; wreck++)
        {
            if (!IsAWreckWorthFetching(wreck)) continue;

            var farM = (Cars.PositionM[wreck] - fromM).LengthSquared();
            if (farM >= bestM) continue;

            best = wreck;
            bestM = farM;
        }

        if (best < 0 || !IsTheNearestFreeEvacuatorTo(car, best, bestM)) return;

        _recovery.Wreck[car] = best;
        _recovery.SinceS[car] = 0f;
        _recovery.HitchedForS[car] = 0f;
        EnterTheRecoveryStage(car, RecoveryStage.Running);
        SendTo(car, Cars.PositionM[best], ParkingRegistry.NoBay);
    }

    /// <summary>
    /// A wreck still lying in the town: broken, on nobody's bar, not already standing in a yard, and not
    /// already somebody else's recovery. <b>One wreck to a recovery and one recovery to a wreck.</b>
    /// </summary>
    bool IsAWreckWorthFetching(int wreck) =>
        Cars.Broken[wreck] && _recovery.OnTheHookOf[wreck] == RecoveryDuty.Nothing
        && !_recovery.InTheYard[wreck] && !IsAlreadyTaken(wreck);

    /// <summary>Whether some other evacuator has already taken this wreck.</summary>
    bool IsAlreadyTaken(int wreck)
    {
        for (var car = 0; car < Cars.Count; car++)
        {
            if (_recovery.Wreck[car] == wreck) return true;
        }

        return false;
    }

    /// <summary>
    /// <b>Whether this is the evacuator EVA-3 means</b> — the nearest one with nothing else to do and a slot
    /// to put it in — asked of the wreck it was about to take. It is <see cref="IsTheNearestFreeAmbulanceTo"/>
    /// said of a depot, and it is asked for the same reason: the recovery belongs to the wreck and the choice
    /// belongs to the crew, so a town with two depots cannot send the far one because its decision ran first.
    /// </summary>
    bool IsTheNearestFreeEvacuatorTo(int car, int wreck, float farM)
    {
        for (var other = 0; other < Cars.Count; other++)
        {
            if (other == car || !IsAnEvacuator(other) || Cars.Broken[other] || _recovery.IsOnARecovery(other)) continue;
            if (AFreeYardSlot(_recovery.Yard[other]) < 0) continue;

            var otherM = (Cars.PositionM[wreck] - Cars.PositionM[other]).LengthSquared();
            if (otherM < farM || (otherM == farM && other < car)) return false;
        }

        return true;
    }

    /// <summary>
    /// The run to the wreck and the work at it: keep the destination on it, and get it onto the bar once
    /// the evacuator has come to rest within the crew's reach of it (`P-18`).
    /// </summary>
    /// <remarks>
    /// <b>Two stages and one method</b>, because the difference between them is one question asked of the
    /// pose — standing where the crew can work, or not yet — and every guard above that question is the
    /// same on either side of it. Split in two, the bound, the wreck's own validity and the re-lay would
    /// each have had to be written twice and would have drifted apart.
    /// </remarks>
    void RunToTheWreck(int car, float sinceLastDecisionS)
    {
        var wreck = _recovery.Wreck[car];

        // EVA-8's bound, and the two ways a recovery stops being one before it has begun: the clock, and
        // the wreck having stopped being one while the evacuator was on its way.
        if (wreck < 0 || !Cars.Broken[wreck] || _recovery.InTheYard[wreck])
        {
            GiveUpTheRecovery(car, counted: false);
            return;
        }

        if (_recovery.SinceS[car] >= _config.EvacuatorGiveUpS)
        {
            GiveUpTheRecovery(car, counted: true);
            return;
        }

        // A leg that ended before the wreck was reached — settled, abandoned, or a bay left and nowhere gone
        // — is begun again from where the evacuator actually stands (MAN-3). A wreck is shunted about by
        // whatever hits it, so the standing place is re-read rather than remembered.
        if (!TheHitchingPlaceM(car, wreck, out var standM))
        {
            GiveUpTheRecovery(car, counted: true);
            return;
        }

        // <b>Nothing is driven while the man is out</b> (SRV-3). A leg re-laid under a crew standing at the
        // wreck would be a truck pulling away from its own recovery man, so the approach is only ever begun
        // again from a cab everybody is in.
        if (!Cars.Driven[car] && !TheHandIsOut(car))
        {
            ShowTheStage(car, RecoveryStage.Running);
            SendTo(car, standM, ParkingRegistry.NoBay);
            return;
        }

        var atRest = Cars.VelocityMps[car].Length() <= _config.Driving.StopSpeedMps;
        var withinReach = TheHandIsOut(car)
            || (Cars.PositionM[car] - standM).Length() <= _config.EvacuatorSceneReachM;
        if (!atRest || !withinReach)
        {
            _recovery.HitchedForS[car] = 0f;
            ShowTheStage(car, RecoveryStage.Running);

            // Come to rest at the place and still out of reach: the evacuator overshot or stopped on the
            // wrong side of it. A place behind the car is not a place to hold at (MAN-6), so the leg is laid
            // again from the pose it is actually in and the route takes it round.
            if (atRest && Cars.Doing[car] == Maneuver.AttendTheScene) SendTo(car, standM, ParkingRegistry.NoBay);

            return;
        }

        ShowTheStage(car, RecoveryStage.Hitching);

        // <b>The work is done in human form</b> (SRV-3, EVA-5): the man gets out and stands at the wreck,
        // and only then does the interval the hitch takes begin to run. Nothing about the arm is reached
        // from inside the cab.
        if (!TheHandHasReached(car, TheNearestPointOnTheBodyM(wreck, Cars.PositionM[car]))) return;

        _recovery.HitchedForS[car] += sinceLastDecisionS;
        if (_recovery.HitchedForS[car] < _config.Evacuator.HitchingS) return;

        // <b>The crew works the same arm a hand works</b> (EVA-5, CTL-7) — and winches the wreck onto the
        // fork first when the truck could not be driven right onto it. See the decision log: a truck cannot
        // get its own tail onto a body lying in the lane ahead of it without a manoeuvre the catalogue does
        // not have, so the last few metres are a placement (PHY-7a) and never more than the arm's own reach.
        if (WhatTheArmIsTouching(car, out _) != wreck) WinchItOntoTheFork(car, wreck);

        WorkTheArm(car);
        _recovery.HaulsLeft[car] = _config.Evacuator.HaulsBeforeSettingItDown;
        EnterTheRecoveryStage(car, RecoveryStage.BoardingAtTheScene);
    }

    /// <summary>
    /// <b>The wreck pulled onto the fork by the winch</b>, over the last few metres and no more: it is put
    /// where the arm holds it, in line behind the truck, which is the same placement a container makes when
    /// it sets a body down (PHY-7a).
    /// </summary>
    void WinchItOntoTheFork(int car, int wreck)
    {
        var behindM = TowBar.SetDownBehindM(Cars.BuildOf(car), Cars.BuildOf(wreck));
        SetTheWreckDown(wreck, Cars.PositionM[car] - (ForwardOf(car) * behindM), Cars.HeadingRad[car]);
    }

    /// <summary>
    /// <b>Where an evacuator has to be standing for its arm to reach this wreck</b>: a whole set-down ahead
    /// of it along the road it is lying beside, so that a truck which has driven up and stopped there has
    /// the wreck behind its own tail with the fork on it. False for a wreck lying beside no road at all.
    /// </summary>
    /// <remarks>
    /// <b>The road's direction and not the wreck's own.</b> A wreck points wherever the crash left it; the
    /// truck can only arrive along the lane, so the place it can be made to stand is the one the lane leads
    /// to. What that costs is stated with the rest of what a tow cannot do (EVA-5): a car left lying across
    /// its lane is one the fork arrives at an angle to, and the leg is laid again until `EVA-8`'s bound ends
    /// the recovery.
    /// </remarks>
    bool TheHitchingPlaceM(int car, int wreck, out Vector2 placeM)
    {
        var lyingAtM = Cars.PositionM[wreck];
        var lane = _roads.NearestLane(lyingAtM, out var alongM);
        if (lane < 0)
        {
            placeM = default;
            return false;
        }

        var forward = Spline.SampleAt(_roads.ArcsOf(lane), alongM).Direction;
        placeM = lyingAtM + (forward * TowBar.SetDownBehindM(Cars.BuildOf(car), Cars.BuildOf(wreck)));
        return true;
    }

    /// <summary>
    /// The haul to the yard: the same shape as the run to the wreck, aimed at a slot instead of at a body.
    /// <b>The haul ends by the evacuator standing where the crew can reach a free slot</b> and never by the
    /// leg running out, because a leg aimed at a place does not end — `P-18` is what stops the car, and
    /// what it stops it at is the slot (<see cref="ToTheSceneM"/>).
    /// </summary>
    void HaulToTheYard(int car)
    {
        if (_recovery.Towing[car] < 0)
        {
            GoBackToTheDepot(car);
            return;
        }

        // <b>A haul that has run out of clock is drawn again</b> (EVA-8), because there is no answer to a
        // road that would not let this leg through better than laying it again from where the evacuator has
        // actually got to (MAN-3) — <b>and only so many times</b>. A rescue's delivery is never given up
        // because a casualty is aboard and there is nothing better to do with them; a wreck set down is no
        // worse off than where it fell, and what giving up buys is the town's evacuator back.
        if (_recovery.SinceS[car] >= _config.EvacuatorGiveUpS)
        {
            _recovery.SinceS[car] = 0f;
            if (_recovery.HaulsLeft[car] > 0)
            {
                _recovery.HaulsLeft[car]--;
                SendToTheYard(car);
                return;
            }

            SetItDownWhereItStands(car);
            return;
        }

        if (!Cars.Driven[car])
        {
            SendToTheYard(car);
            return;
        }

        if (Cars.VelocityMps[car].Length() > _config.Driving.StopSpeedMps) return;

        if (AFreeYardSlotWithinReach(car) < 0)
        {
            // Stopped at the yard and still short of every slot the crew could work from — the same
            // overshoot a rescue makes at a body, answered the same way.
            if (Cars.Doing[car] == Maneuver.AttendTheScene) SendToTheYard(car);

            return;
        }

        EnterTheRecoveryStage(car, RecoveryStage.Unhitching);
    }

    /// <summary>
    /// <b>The wreck off the bar and into a slot</b> (EVA-6), once the crew has spent the same interval on it
    /// they spent putting it on. A yard with no free slot within reach refuses, which is a wait and not a
    /// failure — the same wait a full hospital's door puts an ambulance in (OBJ-5).
    /// </summary>
    void UnhitchIntoTheYard(int car, float sinceLastDecisionS)
    {
        var wreck = _recovery.Towing[car];
        if (wreck < 0)
        {
            GoBackToTheDepot(car);
            return;
        }

        var slot = AFreeYardSlotWithinReach(car);
        if (slot < 0)
        {
            YardsFoundFull++;
            _recovery.HitchedForS[car] = 0f;

            // Out of clock at the yard is a leg laid again and never a wreck abandoned: the evacuator has
            // something on the bar, and standing further off the slots than the crew can work is answered by
            // driving at them again rather than by giving up (EVA-8). <b>And not while the man is out</b>
            // (SRV-3) — the recall is what brings him in first.
            if (_recovery.SinceS[car] >= _config.EvacuatorGiveUpS && !TheHandIsOut(car))
            {
                _recovery.SinceS[car] = 0f;
                EnterTheRecoveryStage(car, RecoveryStage.Hauling);
                SendToTheYard(car);
            }

            return;
        }

        // Setting a wreck down is core work too (SRV-3): the man is out at the slot before the interval
        // that gets it off the bar begins to run.
        if (!TheHandHasReached(car, _parking.CentreM(slot))) return;

        _recovery.HitchedForS[car] += sinceLastDecisionS;
        if (_recovery.HitchedForS[car] < _config.Evacuator.HitchingS) return;

        var standing = _bayWays.TheStandingOnOffer(slot, wantsNoseIn: true);
        SetTheWreckDown(wreck, _parking.CentreM(slot), BayTemplate.StandingHeadingRad(_parking.HeadingRad(slot), standing));
        _parking.Occupy(slot, wreck);

        LetGoOfIt(car, wreck);
        _recovery.InTheYard[wreck] = true;
        _recovery.RepairedForS[wreck] = 0f;
        _onTheBar--;
        _mendingCount++;
        WrecksYarded++;

        EnterTheRecoveryStage(car, RecoveryStage.BoardingAtTheYard);
    }

    /// <summary>
    /// <b>EVA-7: a wreck standing in a yard is a car again once the workshop has had it long enough.</b> It
    /// is put back together where it stands and left there, an ordinary parked car in an ordinary space,
    /// free for whoever walks past to drive away (PER-4).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The clock runs on the tick and not on a decision</b>, because a wreck is terminal and terminal
    /// bodies are handed no decisions (<see cref="IsTerminal"/>). It is skipped whole while no yard holds
    /// anything, and frozen while the town is held, so nothing is mended by having stood still through a
    /// pause.
    /// </para>
    /// <para>
    /// <b>A restored service vehicle comes back as an ordinary car.</b> Its crew got out when it broke and
    /// is not coming back, so the hospital, station or depot it belonged to has to let it go — otherwise the
    /// town would hold a bay for a vehicle nothing runs an errand for, and the next driver to park it would
    /// be a walker who could never get out of it.
    /// </para>
    /// </remarks>
    void MendTheYards(float dtS)
    {
        if (_mendingCount == 0 || HoldAgents) return;

        for (var car = 0; car < Cars.Count; car++)
        {
            if (!_recovery.InTheYard[car]) continue;

            _recovery.RepairedForS[car] += dtS;
            if (_recovery.RepairedForS[car] < _config.Evacuator.RepairS) continue;

            PutItBackOnTheRoad(car);
        }
    }

    /// <summary>One wreck mended: the terminal state left behind, and everything the wreck of it was holding dropped.</summary>
    void PutItBackOnTheRoad(int car)
    {
        _recovery.InTheYard[car] = false;
        _recovery.RepairedForS[car] = 0f;
        _mendingCount--;

        Cars.Broken[car] = false;
        Cars.Command[car] = DriveCommand.Parked;
        Cars.AccelerationMps2[car] = Vector2.Zero;
        for (var wheel = car * TyreModel.Wheels; wheel < (car + 1) * TyreModel.Wheels; wheel++)
        {
            Cars.WheelSpinMps[wheel] = 0f;
        }

        _wheels.Clear(car);
        LetTheBuildingLetItGo(car);
        WrecksRestored++;
    }

    /// <summary>
    /// A restored vehicle struck off whatever strength it was on, and the bay that was held for it given
    /// back to the town (GEN-4k). <b>An evacuator has already been struck off by the tick it broke</b>
    /// (<see cref="LoseTheEvacuator"/>), so what this releases is an ambulance's or a patrol car's.
    /// </summary>
    void LetTheBuildingLetItGo(int car)
    {
        var apron = _duty.HomeBay[car] >= 0 ? _duty.HomeBay[car] : _beat.HomeBay[car];
        if (apron >= 0 && _parking.HeldFor(apron) == car) _parking.HoldForTheCar(apron, ParkingRegistry.Reserved);

        Cars.Ambulance[car] = false;
        Cars.BlueLight[car] = false;
        _duty.Clear(car);
        _duty.Hospital[car] = RescueDuty.NoBuilding;
        _duty.HomeBay[car] = RescueDuty.NoBay;
        _beat.Station[car] = PatrolDuty.NoBuilding;
        _beat.HomeBay[car] = PatrolDuty.NoBay;
    }

    /// <summary>
    /// <b>The depot's evacuator wrecked</b> (SRV-4): the load off the bar where it stands, the errand given
    /// up, and the depot let go of a truck that is not coming back. Everything it was holding is dropped on
    /// the tick it breaks rather than left to be discovered by whatever asks next.
    /// </summary>
    /// <remarks>
    /// <b>It leaves two calls where there was one recovery</b> — the truck and, if it was hauling, the car
    /// behind it — and that is the honest state: the wreck on the bar is exactly where it would have been if
    /// nobody had come for it, which is EVA-8's own argument said of a crash instead of a clock. What the
    /// depot has until somebody else clears it is no evacuator, and its yard slots stay held for the wrecks
    /// already standing in them.
    /// </remarks>
    void LoseTheEvacuator(int car)
    {
        DropWhatIsOnTheBar(car);
        if (_recovery.IsOnARecovery(car)) RecoveriesGivenUp++;

        // Stood down rather than sent home: a wreck is handed no destinations and drives no legs.
        StandTheEvacuatorDown(car);

        var home = _recovery.HomeBay[car];
        if (home >= 0 && _parking.HeldFor(home) == car) _parking.HoldForTheCar(home, ParkingRegistry.Reserved);

        _recovery.Depot[car] = RecoveryDuty.NoBuilding;
        _recovery.Yard[car] = RecoveryDuty.NoYard;
        _recovery.HomeBay[car] = RecoveryDuty.NoBay;
    }

    /// <summary>
    /// <b>The haul given up</b> (EVA-8): the wreck off the bar where the evacuator stands, and the truck
    /// sent home free to take the next call. The wreck is a recovery again from the moment it is down, so a
    /// tow that could not get through costs the town a delay and never its evacuator.
    /// </summary>
    void SetItDownWhereItStands(int car)
    {
        DropWhatIsOnTheBar(car);
        GiveUpTheRecovery(car, counted: true);
    }

    /// <summary>
    /// The bar let go where the pair stands: a wreck becomes a call again from that moment, so nothing has
    /// to remember that somebody once started fetching it, and a car that was only ever a car goes back to
    /// driving itself from where it was put down.
    /// </summary>
    void DropWhatIsOnTheBar(int car)
    {
        var towed = _recovery.Towing[car];
        if (towed < 0) return;

        LetGoOfIt(car, towed);
        _onTheBar--;
        if (Cars.Broken[towed]) _wreckCount++;
    }

    /// <summary>
    /// <b>The arm, worked</b> (EVA-5) — the whole of a recovery vehicle's action and the only way anything in
    /// this town couples or uncouples a tow: out and under whatever the fork is touching, straight back in
    /// when it is touching nothing, and letting go of whatever is already on it. Whether it did anything.
    /// </summary>
    /// <remarks>
    /// <b>A crew reaches for it through this and so does a hand on the keys</b> (CTL-7), which is the whole
    /// point of it being one call: an evacuator's driver has to have put the truck where the fork actually
    /// reaches the car, exactly as a player does, so what the town does to fetch a wreck is a thing that can
    /// be watched being done rather than a rule the player is outside of.
    /// </remarks>
    public bool WorkTheArm(int car)
    {
        if (car < 0 || car >= Cars.Count || Cars.Broken[car] || !CarriesAnArm(car)) return false;

        if (_recovery.Towing[car] >= 0)
        {
            DropWhatIsOnTheBar(car);
            return true;
        }

        var caught = WhatTheArmIsTouching(car, out var byTheTail);
        if (caught < 0) return false;

        PutItOnTheBar(car, caught, byTheTail);
        return true;
    }

    /// <summary>Whether this car has an arm to work at all, which is the picture its variant carries (EVA-5).</summary>
    bool CarriesAnArm(int car) => Cars.BuildOf(car).TowReachM > 0f;

    /// <summary>
    /// <b>What the arm would catch, and by which end</b>: the nearest car with an end the fork can be swung
    /// under — within the arm's own reach of the hinge and behind the truck — or −1 for an arm reaching into
    /// empty road.
    /// </summary>
    /// <remarks>
    /// <b>The arm swings, so what it catches is what it can reach and not only what is straight behind it</b>
    /// (EVA-5): a car whose own bodywork comes within the arm's reach of the hinge, and by whichever of its
    /// two ends the fork is nearer. Behind the truck because the hinge is on the deck and the cab is in the
    /// way of everything in front of it. A car already on a bar, one already pulling something and one
    /// standing in a yard being mended are all passed over — the first two are somebody's tow and the third
    /// is halfway through being a car again.
    /// </remarks>
    int WhatTheArmIsTouching(int car, out bool byTheTail)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        var forward = ForwardOf(car);
        var hingeM = TowBar.HookM(build, Cars.PositionM[car], forward);

        byTheTail = false;
        var caught = -1;
        var nearestM = build.TowReachM;
        for (var other = 0; other < Cars.Count; other++)
        {
            if (other == car || _recovery.OnTheHookOf[other] >= 0 || _recovery.Towing[other] >= 0) continue;
            if (_recovery.InTheYard[other]) continue;

            var itsForward = ForwardOf(other);
            var nearestOnItM = TheNearestPointOnTheBodyM(other, hingeM);
            var farM = (nearestOnItM - hingeM).Length();
            if (farM > nearestM || Vector2.Dot(nearestOnItM - hingeM, forward) >= 0f) continue;

            caught = other;
            nearestM = farM;

            // And by whichever of its two ends the fork is nearer, which is the end it can be got under.
            var toTheNose = TowBar.ForkM(Cars.BuildOf(other), Cars.PositionM[other], itsForward, byTheTail: false);
            var toTheTail = TowBar.ForkM(Cars.BuildOf(other), Cars.PositionM[other], itsForward, byTheTail: true);
            byTheTail = (toTheTail - hingeM).LengthSquared() < (toTheNose - hingeM).LengthSquared();
        }

        return caught;
    }

    /// <summary>The point on a car's own footprint nearest a place, which is what "the arm reaches it" means.</summary>
    Vector2 TheNearestPointOnTheBodyM(int car, Vector2 fromM)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        var forward = ForwardOf(car);
        var right = Heading.RightOf(forward);
        var offset = fromM - Cars.PositionM[car];
        var alongM = Math.Clamp(Vector2.Dot(offset, forward), -build.HalfLengthM, build.HalfLengthM);
        var acrossM = Math.Clamp(Vector2.Dot(offset, right), -build.FlankM, build.FlankM);
        return Cars.PositionM[car] + (forward * alongM) + (right * acrossM);
    }

    /// <summary>
    /// <b>A car onto the bar where it stands</b> (EVA-5). Nothing is moved: the fork is already under it, so
    /// the coupling begins at whatever stretch the driver left it at and pulls the pair into line itself.
    /// </summary>
    /// <remarks>
    /// <b>What goes on the bar stops being a car for as long as it is on it</b> — no line, no bay, no
    /// junction, no decisions — which is the state a wreck is already in and the state a car has to be put
    /// into. Its wheels are straightened in the same breath: the pair still on the ground may be its steered
    /// one, and a car dragged along on a wheel wound over is a car being scrubbed sideways down the road.
    /// </remarks>
    void PutItOnTheBar(int car, int caught, bool byTheTail)
    {
        _recovery.HeldByTheTail[caught] = byTheTail;
        _parking.Vacate(caught);
        GiveUpTheBay(caught);
        Cars.Hold[caught] = DrivingHold.None;
        Cars.Line[caught] = default;
        LeaveTheCatalogue(caught);
        RestTheLadder(caught);
        DropTheMovement(caught);
        Cars.Command[caught] = Cars.Broken[caught] ? DriveCommand.Locked : DriveCommand.Idle;

        CoupleUp(car, caught);
        _onTheBar++;
        if (!Cars.Broken[caught]) return;

        _wreckCount--;
        WrecksHitched++;
    }

    /// <summary>
    /// <b>The pair joined at both ends</b>: the duty each holds of the other, and nothing else.
    /// </summary>
    /// <remarks>
    /// <b>The solver is not told about the coupling</b> (EVA-5): a truck that backs into the car on its own
    /// arm hits it, exactly as it hits anything else, and the daylight the arm holds is what keeps the two
    /// boxes apart in a straight line. What closes it is a corner taken tighter than the trailer can follow,
    /// and that contact is a real one.
    /// </remarks>
    void CoupleUp(int car, int wreck)
    {
        _recovery.Towing[car] = wreck;
        _recovery.OnTheHookOf[wreck] = car;
    }

    /// <summary>And parted, at whichever end the errand ended.</summary>
    void LetGoOfIt(int car, int wreck)
    {
        _recovery.Towing[car] = RecoveryDuty.Nothing;
        _recovery.OnTheHookOf[wreck] = RecoveryDuty.Nothing;
    }

    /// <summary>The recovery given up: the wreck released for whoever can reach it, and the evacuator sent home.</summary>
    /// <remarks>
    /// <b>A man out in the road is walked back before the truck goes anywhere</b> (SRV-3) — the whole of
    /// <see cref="GiveUpTheCall"/>'s argument said of a recovery.
    /// </remarks>
    void GiveUpTheRecovery(int car, bool counted)
    {
        if (counted) RecoveriesGivenUp++;

        _recovery.Wreck[car] = RecoveryDuty.Nothing;
        if (TheHandIsOut(car))
        {
            EnterTheRecoveryStage(car, RecoveryStage.BoardingAtTheYard);
            return;
        }

        GoBackToTheDepot(car);
    }

    /// <summary>Back to its own bay at its depot, with the priority out: an evacuator between recoveries is ordinary traffic.</summary>
    void GoBackToTheDepot(int car)
    {
        _recovery.Wreck[car] = RecoveryDuty.Nothing;
        EnterTheRecoveryStage(car, RecoveryStage.GoingHome);

        var home = _recovery.HomeBay[car];
        if (home < 0 || _parking.BayOf(car) == home || !_parking.IsFreeFor(car, home))
        {
            StandTheEvacuatorDown(car);
            return;
        }

        SendTo(car, _parking.CentreM(home), home);
    }

    /// <summary>Off duty: standing wherever the leg ended, with the recovery cleared and both bars out.</summary>
    void StandTheEvacuatorDown(int car)
    {
        _recovery.Clear(car);
        ShowWhatTheRecoveryIs(car);
    }

    /// <summary>
    /// The leg to the yard: aimed at a free slot rather than at the depot's door, because the slot is what
    /// the crew has to be standing within reach of and aiming at the building would leave the arrival a
    /// street's width from it.
    /// </summary>
    void SendToTheYard(int car)
    {
        var slot = AFreeYardSlot(_recovery.Yard[car]);
        var depot = _recovery.Depot[car];
        SendTo(
            car,
            slot >= 0 ? _parking.CentreM(slot) : depot >= 0 ? _plan.Buildings.CentreM[depot] : Cars.PositionM[car],
            ParkingRegistry.NoBay);
    }

    /// <summary>The first slot of this yard with nothing standing in it, or −1 for a yard that is full or was never laid.</summary>
    int AFreeYardSlot(int yard)
    {
        if (yard < 0) return -1;

        for (var slot = 0; slot < _config.Evacuator.YardSlots; slot++)
        {
            var bay = YardSlot(yard, slot);
            if (bay >= 0 && _parking.CarInBay(bay) == ParkingRegistry.Nobody) return bay;
        }

        return -1;
    }

    /// <summary>And the nearest one of them the crew can actually work from, which is what says a haul has arrived.</summary>
    int AFreeYardSlotWithinReach(int car)
    {
        var yard = _recovery.Yard[car];
        if (yard < 0) return -1;

        var fromM = Cars.PositionM[car];
        var best = -1;
        var bestM = _config.EvacuatorYardReachM * _config.EvacuatorYardReachM;
        for (var slot = 0; slot < _config.Evacuator.YardSlots; slot++)
        {
            var bay = YardSlot(yard, slot);
            if (bay < 0 || _parking.CarInBay(bay) != ParkingRegistry.Nobody) continue;

            var farM = (_parking.CentreM(bay) - fromM).LengthSquared();
            if (farM >= bestM) continue;

            best = bay;
            bestM = farM;
        }

        return best;
    }

    /// <summary>
    /// <b>A body put down where the crew put it</b> — the one placement in this errand, and the containment
    /// slice's own operation rather than a second one (PHY-7a): the solver is handed the pose before the body
    /// is back in the world, so nothing sees it cross the ground between.
    /// </summary>
    void SetTheWreckDown(int wreck, Vector2 atM, float headingRad)
    {
        _physics.Release(Cars.Body[wreck], atM, headingRad);
        Cars.PositionM[wreck] = atM;
        Cars.HeadingRad[wreck] = headingRad;
        Cars.VelocityMps[wreck] = Vector2.Zero;
        Cars.YawRateRadPerS[wreck] = 0f;
        Cars.AccelerationMps2[wreck] = Vector2.Zero;
        _wheels.Clear(wreck);
    }

    /// <summary>
    /// <b>The one place a stage changes</b>, so the priority is decided in exactly one place (EVA-4) and the
    /// recovery's own clock means the same thing in each of them: how long <em>this</em> leg has been
    /// running.
    /// </summary>
    void EnterTheRecoveryStage(int car, RecoveryStage stage)
    {
        _recovery.Stage[car] = stage;
        _recovery.SinceS[car] = 0f;
        _recovery.HitchedForS[car] = 0f;
        ShowWhatTheRecoveryIs(car);
    }

    /// <summary>
    /// What the truck is saying about the errand it is on: <b>the priority on the one leg that carries it</b>
    /// (EVA-4), and <b>the amber bar for the whole of the recovery</b> (CAR-14.6) — out, standing at the
    /// wreck, hauling it in and driving home alike, since what that bar says is that a truck is working here
    /// and not that anybody owes it the road.
    /// </summary>
    void ShowWhatTheRecoveryIs(int car)
    {
        Cars.BlueLight[car] = _recovery.IsHurrying(car);
        Cars.AtWork[car] = _recovery.IsOnARecovery(car);
    }

    /// <summary>
    /// The same stage change with <b>no clock touched</b> — the pair that are one leg seen from two sides:
    /// driving at the wreck and standing at it. The bound is the whole approach's and must not restart
    /// because the evacuator arrived, or a truck that keeps arriving and being nudged off could never run
    /// out of clock.
    /// </summary>
    void ShowTheStage(int car, RecoveryStage stage)
    {
        if (_recovery.Stage[car] == stage) return;

        _recovery.Stage[car] = stage;
        ShowWhatTheRecoveryIs(car);
    }

    /// <summary>
    /// <b>Whether this leg is aimed at a place in the road rather than at a bay</b> (EVA-3, EVA-6) — the run
    /// to a wreck and the haul to a yard slot the evacuator itself does not park in.
    /// </summary>
    bool IsOnItsWayToAWreck(int car) =>
        IsAnEvacuator(car) && _recovery.Stage[car] is not (RecoveryStage.Waiting or RecoveryStage.GoingHome);

    /// <summary>
    /// <b>Where `P-18` is to stop this evacuator</b>, and infinity when nothing is asking it to: the wreck
    /// while it is being fetched, and the slot the haul was aimed at while it is being brought in and
    /// unhitched. <b>The place has to outlast the arrival</b> — a stop point that went away the moment the
    /// truck reached it would set the crew to work on a car that had started driving off again.
    /// </summary>
    bool TheRecoveryStopsAt(int car, out Vector2 placeM, out float reachM)
    {
        switch (_recovery.Stage[car])
        {
            case RecoveryStage.Running:
            case RecoveryStage.Hitching:
            case RecoveryStage.BoardingAtTheScene:
                // The standing place the leg was laid to and not the wreck itself: what the truck has to be
                // stopped at is where its arm reaches the wreck from, which is a set-down further on. <b>It
                // outlasts the work</b> (SRV-3), or the truck would roll off while its man was at the arm.
                placeM = Cars.DestinationM[car];
                reachM = _config.EvacuatorSceneReachM;
                return Cars.HasDestination[car];

            case RecoveryStage.Hauling:
            case RecoveryStage.Unhitching:
            case RecoveryStage.BoardingAtTheYard:
                // The destination the leg was actually laid to, and not a slot looked up again: the two
                // could disagree the moment a slot is taken, and a stop point the route never aimed at is a
                // car braking for somewhere it is not going.
                placeM = Cars.DestinationM[car];
                reachM = _config.EvacuatorYardReachM;
                return Cars.HasDestination[car];

            default:
                placeM = default;
                reachM = 0f;
                return false;
        }
    }
}
