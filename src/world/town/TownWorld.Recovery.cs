using System.Numerics;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Ambulance;
using TrafficSimulation.Agents.Evacuator;
using TrafficSimulation.Agents.Service;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The recovery</b> (EVA-1…8): the depots a map has, the evacuator standing at each, and the errand it
/// runs when a car is wrecked in the street. <b>The driving itself is the leg's</b> — an evacuator
/// drives the legs every other car drives (CAR-15) — and what is here
/// is only the errand those legs are being run for.
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

    /// <summary>Every tow in the town (EVA-5), for whatever draws or reports one.</summary>
    public TowHitches Hitches => _hitches;

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

    /// <summary>
    /// <b>A car stood on a depot's apron becomes one of that depot's evacuators</b> (SRV-2, EVA-2): the yard it
    /// delivers to and the bay held for it, which is where every recovery of one that does not patrol begins and
    /// ends.
    /// </summary>
    void TakeUpTheRecovery(int car, int depot, int yard)
    {
        _recovery.Depot[car] = depot;
        _recovery.Yard[car] = yard;
        _recovery.HomeBay[car] = _parking.BayOf(car);
    }

    /// <summary>Whether this car is a town's evacuator, which is the one question the decision loop asks before running the errand.</summary>
    internal bool IsAnEvacuator(int car) => _recovery.Depot[car] != RecoveryDuty.NoBuilding;

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
    public float OffTheLineAllowanceM(int car) =>
        _config.CarOffPathM * OffLineTolerance
        * (_hitches.IsTowing(car) ? _config.Evacuator.TowedLineAllowance : 1f);

    /// <summary>
    /// <b>EVA-1: a car in its terminal state is a recovery waiting to be run.</b> Raised where the wreck
    /// happens, so nothing has to search the fleet for one — and a call from then on unless an arm already has it.
    /// </summary>
    void RaiseTheRecovery(int car)
    {
        if (Cars.Broken[car]) return;

        if (!_hitches.IsHeld(car)) _wreckCount++;
        WrecksRaised++;
    }

    /// <summary>Whether a car is a call (EVA-1): broken, on no arm and standing in no yard — what <see cref="_wreckCount"/> counts.</summary>
    bool IsACall(int car) => Cars.Broken[car] && !_hitches.IsHeld(car) && !_recovery.InTheYard[car];

    /// <summary>
    /// One decision of one evacuator's errand, taken before the leg's own (EVA-3). <b>It decides the errand
    /// and never the driving</b>: what comes out of it is a destination, a chain and whether the priority is
    /// carried, and the leg does the rest.
    /// </summary>
    void RunTheRecovery(int car, float sinceLastDecisionS)
    {
        // A wrecked evacuator decides nothing (SRV-4). The roster hands terminal bodies no ticks, and what
        // this one was holding was let go on the tick it broke (<see cref="LoseTheEvacuator"/>).
        if (Cars.Broken[car]) return;

        var stage = _recovery.Stage[car];
        if (stage != RecoveryStage.Waiting) _recovery.SinceS[car] += sinceLastDecisionS;

        switch (stage)
        {
            case RecoveryStage.Waiting:
                if (!TakeAWreck(car) && IsDueOnTheBeat(car)) DriveTheBeat(car);

                return;

            case RecoveryStage.Patrolling:
                if (!TakeAWreck(car) && IsDoneWithThePlace(car, _recovery.SinceS[car])) DriveTheBeat(car);

                return;

            case RecoveryStage.Running:
            case RecoveryStage.Hitching:
                RunToTheWreck(car, sinceLastDecisionS);
                return;

            case RecoveryStage.Hauling:
                HaulToTheYard(car);
                return;

            case RecoveryStage.Unhitching:
                UnhitchIntoTheYard(car, sinceLastDecisionS);
                return;

            case RecoveryStage.GoingHome:
                if (!Cars.Driven[car]) StandTheEvacuatorDown(car);

                return;
        }
    }

    /// <summary>
    /// <b>The nearest wreck nobody is on their way to</b>, and the run to it. Asked only of an evacuator with
    /// nothing to clear — standing or on its beat — only while there is anything to fetch, and <b>only while its
    /// yard has a slot free to put it in</b> (EVA-3).
    /// </summary>
    /// <remarks>
    /// <b>Somewhere to put it is part of taking the call and not a problem discovered on arrival.</b> An
    /// evacuator that set off with a full yard would arrive with a wreck on the bar and nowhere to set it
    /// down, and would then stand at the yard holding it for the rest of the run — which is the town's only
    /// evacuator taken out of service by bookkeeping. A depot whose yard is full has stopped collecting, and
    /// that is a state with a count against it (<see cref="YardsFoundFull"/>).
    /// </remarks>
    bool TakeAWreck(int car)
    {
        if (_wreckCount == 0) return false;

        if (!HasRoomInItsYard(car))
        {
            YardsFoundFull++;
            return false;
        }

        var fromM = Cars.PositionM[car];
        var best = RecoveryDuty.Nothing;
        var bestM = float.PositiveInfinity;
        for (var wreck = _wrecked.Next(0, Cars.Count); wreck >= 0; wreck = _wrecked.Next(wreck + 1, Cars.Count))
        {
            // Whether a wreck is taken is a walk of the evacuators, so only one nearer than the best is asked it.
            // A wreck lying beside no road has no hitching place, and taking it is a recovery given up at the
            // next decision and the same wreck taken again at the one after — a leg laid afresh every decision.
            var farM = (Cars.PositionM[wreck] - fromM).LengthSquared();
            if (farM >= bestM || !IsAWreckWorthFetching(wreck) || !TheHitchingPlace(car, wreck, out _)) continue;

            best = wreck;
            bestM = farM;
        }

        if (best < 0 || !IsTheNearestFree(Errand.Recovery, car, Cars.PositionM[best], bestM)) return false;

        _recovery.Wreck[car] = best;
        _recovery.SinceS[car] = 0f;
        _recovery.HitchedForS[car] = 0f;
        EnterTheRecoveryStage(car, RecoveryStage.Running);
        SendTo(car, RoadPlaceOfCar(best), Cars.PositionM[best]);
        return true;
    }

    /// <summary>
    /// A wreck still lying in the town: broken, on nobody's bar, not already standing in a yard, and not
    /// already somebody else's recovery. <b>One wreck to a recovery and one recovery to a wreck.</b>
    /// </summary>
    bool IsAWreckWorthFetching(int wreck) =>
        IsACall(wreck) && !IsAlreadyTaken(wreck);

    /// <summary>
    /// Whether some other evacuator has already taken this wreck. Only a car stood as one is ever given a recovery
    /// (<see cref="TakeAWreck"/>), and one struck off since stays on the list, so the list is every car that can hold one.
    /// </summary>
    bool IsAlreadyTaken(int wreck)
    {
        foreach (var car in _evacuators)
        {
            if (_recovery.Wreck[car] == wreck) return true;
        }

        return false;
    }

    /// <summary>
    /// The run to the wreck and the work at it: keep the destination on it, and get it onto the bar once
    /// the evacuator has come to rest within working reach of it.
    /// </summary>
    /// <remarks>
    /// <b>Two stages and one method</b>, because the difference between them is one question asked of the
    /// pose — standing where the arm can be worked, or not yet — and every guard above that question is the
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

        // A leg that ended before the wreck was reached — given up, or a bay left and nowhere gone
        // — is begun again from where the evacuator actually stands (CAR-15). A wreck is shunted about by
        // whatever hits it, so the standing place is re-read rather than remembered.
        if (!TheHitchingPlace(car, wreck, out var stand))
        {
            GiveUpTheRecovery(car, counted: true);
            return;
        }

        if (!Cars.Driven[car])
        {
            ShowTheStage(car, RecoveryStage.Running);
            SendTo(car, stand);
            return;
        }

        var atRest = Cars.VelocityMps[car].Length() <= _config.Driving.StopSpeedMps;
        var withinReach = (Cars.PositionM[car] - PointOf(stand)).Length() <= _config.EvacuatorSceneReachM;
        if (!atRest || !withinReach)
        {
            _recovery.HitchedForS[car] = 0f;
            ShowTheStage(car, RecoveryStage.Running);

            // Come to rest at the place and still out of reach: the evacuator overshot or stopped on the
            // wrong side of it. A place behind the car is not a place to hold at, so the leg is laid
            // again from the pose it is actually in and the route takes it round.
            if (atRest && StoppedWhereItWasSent(car)) SendTo(car, stand);

            return;
        }

        ShowTheStage(car, RecoveryStage.Hitching);

        _recovery.HitchedForS[car] += sinceLastDecisionS;
        if (_recovery.HitchedForS[car] < _config.Evacuator.HitchingS) return;

        // <b>The errand works the same arm a hand works</b> (EVA-5, CTL-7) — and winches the wreck onto the
        // fork first when the truck could not be driven right onto it. See the decision log: a truck cannot
        // get its own tail onto a body lying in the lane ahead of it without reversing onto it, which no
        // driver does but in a manoeuvre at a bay (CAR-6.5), so the last few metres are a placement (PHY-7a). And the arm
        // is worked only once its wreck is what it touches: it catches anything (EVA-5), and the errand is for one body.
        if (WhatTheArmIsTouching(car, out _) != wreck && (!WinchItOntoTheFork(car, wreck) || WhatTheArmIsTouching(car, out _) != wreck)) return;

        WorkTheArm(car);
        _recovery.HaulsLeft[car] = _config.Evacuator.HaulsBeforeSettingItDown;
        EnterTheRecoveryStage(car, RecoveryStage.Hauling);
        SendToTheYard(car);
    }

    /// <summary>
    /// <b>The wreck pulled onto the fork by the winch</b>, over the last few metres and no more: it is put
    /// where the arm holds it, in line behind the truck, which is the same placement a container makes when
    /// it sets a body down (PHY-7a) — <b>and only where nobody but the pair holds that ground</b> (TER-4c.8). False
    /// where somebody does, and the winch waits. <b>Nor while another arm has it by an end</b>: a placement under a
    /// tow would carry the other tractor's pin across the ground with it (EVA-5).
    /// </summary>
    bool WinchItOntoTheFork(int car, int wreck)
    {
        if (_hitches.IsHeld(wreck)) return false;

        var behindM = TowBar.SetDownBehindM(Cars.BuildOf(car), Cars.BuildOf(wreck));
        var atM = Cars.PositionM[car] - (ForwardOf(car) * behindM);
        if (!IsFreeToSetDown(wreck, car, atM)) return false;

        SetTheWreckDown(wreck, car, atM, Cars.HeadingRad[car]);
        return true;
    }

    /// <summary>
    /// <b>Whether a car can be put down here</b> (TER-4c.8): nobody but it and the truck putting it there stands on
    /// the ground its body would, and nobody has ground there it can no longer stop short of.
    /// </summary>
    /// <remarks>
    /// <b>A special unit's own sum on arriving</b> (the root decision log, 2026-10-13): the ground is read on the ways the
    /// world reads the truck on (TER-4c.2) — the place found on each, its body's length either side — and nothing is
    /// looked up.
    /// </remarks>
    bool IsFreeToSetDown(int car, int by, Vector2 atM)
    {
        var halfM = Cars.BuildOf(car).HalfLengthM;
        foreach (ref readonly var cover in CoversOfCar(by))
        {
            var line = _lines.LineOf(cover.Way, out _);
            var aroundM = (cover.FromM + cover.ToM) * 0.5f;
            var onM = Spline.ProjectM(line, atM, aroundM, cover.ToM - cover.FromM + (4f * halfM));
            if (_occupancy.IsTakenByOthers(cover.Way, onM - halfM, onM + halfM, car, by, LaneRoster.Driving)) return false;
        }

        return true;
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
    /// <b>Read off where the world reads the wreck</b> (<see cref="RoadPlaceOfCar"/>): nothing is looked up (the root
    /// decision log, 2026-10-13).
    /// </remarks>
    bool TheHitchingPlace(int car, int wreck, out RoadPlace place)
    {
        var lying = RoadPlaceOfCar(wreck);
        place = lying.IsSomewhere ? OnAlong(lying, TowBar.SetDownBehindM(Cars.BuildOf(car), Cars.BuildOf(wreck))) : lying;
        return lying.IsSomewhere;
    }

    /// <summary>
    /// The haul to the yard: the same shape as the run to the wreck, aimed at a slot instead of at a body.
    /// <b>The haul ends by the evacuator standing within reach of a free slot</b> and never by the
    /// leg running out, because a leg aimed at a place does not end — the stop point is what stops the car, and
    /// what it stops it at is the slot (<see cref="ToTheSceneM"/>).
    /// </summary>
    void HaulToTheYard(int car)
    {
        if (_hitches.CarOnTheBar(car) < 0)
        {
            GoBackToTheDepot(car);
            return;
        }

        // <b>A haul that has run out of clock is drawn again</b> (EVA-8), because there is no answer to a
        // road that would not let this leg through better than laying it again from where the evacuator has
        // actually got to (CAR-15) — <b>and only so many times</b>. A rescue's delivery is never given up
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
            // Stopped at the yard and still short of every slot it could set the wreck down in — the same
            // overshoot a rescue makes at a body, answered the same way.
            if (StoppedWhereItWasSent(car)) SendToTheYard(car);

            return;
        }

        EnterTheRecoveryStage(car, RecoveryStage.Unhitching);
    }

    /// <summary>
    /// <b>The wreck off the bar and into a slot</b> (EVA-6), once the same interval has been spent on it that
    /// putting it on took. A yard with no free slot within reach refuses, which is a wait and not a
    /// failure — the same wait a full hospital's door puts an ambulance in (OBJ-5).
    /// </summary>
    void UnhitchIntoTheYard(int car, float sinceLastDecisionS)
    {
        var wreck = _hitches.CarOnTheBar(car);
        if (wreck < 0)
        {
            GoBackToTheDepot(car);
            return;
        }

        // EVA-5: a wreck another arm still has by its other end is not the yard's to take. The set-down waits on it, and
        // the leg's clock ends the wait as it ends a haul.
        if (_hitches.IsHeldAt(wreck, !_hitches.LoadByTheTail(car)))
        {
            _recovery.HitchedForS[car] = 0f;
            if (_recovery.SinceS[car] >= _config.EvacuatorGiveUpS) SetItDownWhereItStands(car);

            return;
        }

        var slot = AFreeYardSlotWithinReach(car);
        if (slot < 0)
        {
            YardsFoundFull++;
            _recovery.HitchedForS[car] = 0f;

            // Out of clock at the yard is a leg laid again and never a wreck abandoned: the evacuator has
            // something on the bar, and standing further off the slots than the arm can work is answered by
            // driving at them again rather than by giving up (EVA-8).
            if (_recovery.SinceS[car] >= _config.EvacuatorGiveUpS)
            {
                _recovery.SinceS[car] = 0f;
                EnterTheRecoveryStage(car, RecoveryStage.Hauling);
                SendToTheYard(car);
            }

            return;
        }

        _recovery.HitchedForS[car] += sinceLastDecisionS;
        if (_recovery.HitchedForS[car] < _config.Evacuator.HitchingS) return;

        var headingRad = BayTemplate.StandingHeadingRad(_parking.HeadingRad(slot), noseIn: true);
        if (!IsFreeToSetDown(wreck, car, _parking.CentreM(slot))) return;

        SetTheWreckDown(wreck, car, _parking.CentreM(slot), headingRad);
        _parking.Occupy(slot, wreck);

        // In the yard before off the bar, so letting go of it raises no call.
        _recovery.InTheYard[wreck] = true;
        _recovery.RepairedForS[wreck] = 0f;
        LetGoOfTheLoad(car);
        _mendingCount++;
        WrecksYarded++;

        GoBackToTheDepot(car);
    }

    /// <summary>
    /// <b>EVA-7: a wreck standing in a yard is a car again once the workshop has had it long enough.</b> It
    /// is put back together where it stands and left there, an ordinary parked car in an ordinary space.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The clock runs on the tick and not on a decision</b>, because a wreck is terminal and terminal
    /// bodies are handed no decisions (<see cref="IsTerminal"/>). It is skipped whole while no yard holds
    /// anything, and frozen while the town is held, so nothing is mended by having stood still through a
    /// pause.
    /// </para>
    /// <para>
    /// <b>A restored service vehicle comes back as an ordinary car</b> (EVA-7): the hospital or station it
    /// belonged to strikes it off, so no errand is run for it again, and the bay held for it is held for
    /// nobody (<see cref="LetTheBuildingLetItGo"/>). A depot struck its evacuator off when it broke.
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
        _wrecked.Remove(car);
        Cars.Command[car] = DriveCommand.Parked;
        SignalTheWheel(car);
        SignalTheShow(car);
        ClearTheWheels(car);
        LetTheBuildingLetItGo(car);
        WrecksRestored++;
    }

    /// <summary>
    /// A restored vehicle struck off whatever strength it was on, and the bay that was held for it held for
    /// nobody from then on (<see cref="ParkingRegistry.Claimed"/>, GEN-4k) — not given back to the town.
    /// <b>An evacuator has already been struck off by the tick it broke</b> (<see cref="LoseTheEvacuator"/>),
    /// so what this releases is an ambulance or a patrol car.
    /// </summary>
    void LetTheBuildingLetItGo(int car)
    {
        var apron = _duty.HomeBay[car] >= 0 ? _duty.HomeBay[car] : _beat.HomeBay[car];
        if (apron >= 0 && _parking.HeldFor(apron) == car) _parking.HoldForTheCar(apron, ParkingRegistry.Claimed);

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
    /// up, and the depot let go of a truck that is not coming back — its bay held for nobody
    /// (<see cref="ParkingRegistry.Claimed"/>) rather than given back to the town. Everything it was holding
    /// is dropped on the tick it breaks rather than left to be discovered by whatever asks next.
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
        LetGoOfTheLoad(car);
        if (_recovery.IsOnARecovery(car)) RecoveriesGivenUp++;

        // Stood down rather than sent home: a wreck is handed no destinations and drives no legs.
        StandTheEvacuatorDown(car);

        var home = _recovery.HomeBay[car];
        if (home >= 0 && _parking.HeldFor(home) == car) _parking.HoldForTheCar(home, ParkingRegistry.Claimed);

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
        LetGoOfTheLoad(car);
        GiveUpTheRecovery(car, counted: true);
    }

    /// <summary>
    /// <b>The bar let go where the pair stands</b>: a wreck becomes a call again from that moment, so nothing has
    /// to remember that somebody once started fetching it, and a car that was only ever a car goes back to
    /// driving itself from where it was put down — <b>unless another arm still has it by its other end</b>, which
    /// keeps it a trailer (EVA-5).
    /// </summary>
    void LetGoOfTheLoad(int car)
    {
        var load = _hitches.Unhitch(car);
        if (load == TowHitches.Nothing) return;

        SignalTheTow(car, TowHitches.Nothing, byTheTail: false);
        if (!_hitches.IsACar(load) || _hitches.IsHeld(load)) return;

        Enter(load, CarAction.Stand);
        if (IsACall(load)) _wreckCount++;
    }

    /// <summary>
    /// <b>A rail vehicle's train taking it off the map, or putting it back on</b> (TRN-7): every arm on it lets go first,
    /// since a pin to a body taken out of the world and put down elsewhere would drag its tractor there.
    /// </summary>
    void LetGoOfTheWagon(int wagon)
    {
        var body = _hitches.OfWagon(wagon);
        if (_hitches.HeldBy(body, byTheTail: false) is var nose and not TowHitches.Nothing) LetGoOfTheLoad(nose);
        if (_hitches.HeldBy(body, byTheTail: true) is var tail and not TowHitches.Nothing) LetGoOfTheLoad(tail);
    }

    /// <summary>
    /// <b>The arm, worked</b> (EVA-5) — the whole of a recovery vehicle's action and the only way anything in
    /// this town couples or uncouples a tow: out and under whatever the fork is touching, straight back in
    /// when it is touching nothing, and letting go of whatever is already on it. Whether it did anything.
    /// </summary>
    /// <remarks>
    /// <b>The recovery reaches for it through this and so does a hand on the keys</b> (CTL-7), which is the
    /// whole point of it being one call: what the town does to couple a wreck is the lever a player pulls,
    /// and can be watched being done rather than being a rule the player is outside of. <b>The errand has one
    /// thing a press has not</b>: where the fork is not under the wreck it winches the wreck onto it first
    /// (<see cref="WinchItOntoTheFork"/>, EVA-5).
    /// </remarks>
    public bool WorkTheArm(int car)
    {
        if (car < 0 || car >= Cars.Count || Cars.Broken[car] || !CarriesAnArm(car)) return false;

        if (_hitches.IsTowing(car))
        {
            LetGoOfTheLoad(car);
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
    /// <b>What the arm would catch, and by which end</b> (<see cref="ArmReach"/>): every car but the truck itself and every
    /// rail vehicle out on the map, each by whichever of its ends no fork holds — or −1 for an arm reaching into empty
    /// road. Numbered as <see cref="TowHitches"/> numbers bodies.
    /// </summary>
    int WhatTheArmIsTouching(int car, out bool byTheTail)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        var reach = new ArmReach(TowBar.HookM(build, Cars.PositionM[car], ForwardOf(car)), ForwardOf(car), build.TowReachM);
        for (var other = 0; other < Cars.Count; other++)
        {
            if (other == car) continue;

            ref readonly var its = ref Cars.BuildOf(other);
            reach.Consider(
                other, Cars.PositionM[other], ForwardOf(other), new Vector2(its.HalfLengthM, its.FlankM),
                _hitches.IsHeldAt(other, byTheTail: false), _hitches.IsHeldAt(other, byTheTail: true));
        }

        var wagons = _world.View.Wagons;
        for (var wagon = 0; wagon < wagons.Length; wagon++)
        {
            ref readonly var view = ref wagons[wagon];
            if (!view.IsInWorld) continue;

            var body = _hitches.OfWagon(wagon);
            reach.Consider(
                body, view.PositionM, Heading.Unit(view.HeadingRad), _trains.VehicleOf(wagon).BodyM * 0.5f,
                _hitches.IsHeldAt(body, byTheTail: false), _hitches.IsHeldAt(body, byTheTail: true));
        }

        byTheTail = reach.ByTheTail;
        return reach.Caught;
    }

    /// <summary>
    /// <b>A body onto the bar where it stands</b>, by one end (EVA-5). Nothing is moved: the fork is already under it,
    /// so the coupling begins at whatever stretch the driver left it at and pulls the pair into line itself.
    /// </summary>
    /// <remarks>
    /// <b>The solver is not told about the coupling</b> beyond the link itself: a truck that backs into the car on its own
    /// arm hits it, exactly as it hits anything else, and the daylight the arm holds is what keeps the two
    /// boxes apart in a straight line. What closes it is a corner taken tighter than the trailer can follow,
    /// and that contact is a real one. <b>A rail vehicle stays on its rails</b> (TRN-8): what an arm does to one is
    /// pull it along them, and what its train does is pull the arm.
    /// </remarks>
    void PutItOnTheBar(int car, int load, bool byTheTail)
    {
        var gripM = _hitches.IsACar(load)
            ? Cars.BuildOf(load).TowGripFromTheMiddleM
            : TowBar.GripFromTheMiddleM(_trains.VehicleOf(load - _hitches.Cars).BodyM.X, _config.Evacuator.TowGripInsideTheEndM);
        if (_hitches.IsACar(load) && !_hitches.IsHeld(load)) TakeItOffItsWheels(load);

        _hitches.Hitch(car, load, byTheTail, TowBar.ForkAlongM(gripM, byTheTail));
        SignalTheTow(car, load, byTheTail);
    }

    /// <summary>
    /// <b>A car stops being one for as long as an arm has it</b> — no line, no bay, no junction, no decisions — which is
    /// the state a wreck is already in and the state a car has to be put into; a wreck stops being a call, and one
    /// standing in a yard stops being mended. Its wheels are straightened in the same breath: the pair still on the
    /// ground may be its steered one, and a car dragged along on a wheel wound over is a car being scrubbed sideways
    /// down the road.
    /// </summary>
    void TakeItOffItsWheels(int car)
    {
        if (IsACall(car))
        {
            _wreckCount--;
            WrecksHitched++;
        }

        if (_recovery.InTheYard[car])
        {
            _recovery.InTheYard[car] = false;
            _recovery.RepairedForS[car] = 0f;
            _mendingCount--;
        }

        _parking.Vacate(car);
        GiveUpTheBay(car);
        Cars.Hold[car] = DrivingHold.None;
        Cars.SetLine(car, default);
        Cars.Reroutes[car] = 0;
        _driveProgress.Restart(car);
        Cars.Command[car] = Cars.Broken[car] ? DriveCommand.Locked : DriveCommand.Idle;
        SignalTheWheel(car);
        Enter(car, CarAction.Towed);
    }

    /// <summary>The recovery given up: the wreck released for whoever can reach it, and the evacuator sent home.</summary>
    void GiveUpTheRecovery(int car, bool counted)
    {
        if (counted) RecoveriesGivenUp++;

        _recovery.Wreck[car] = RecoveryDuty.Nothing;
        GoBackToTheDepot(car);
    }

    /// <summary>
    /// Back to its own bay at its depot, with the priority out: an evacuator between recoveries is ordinary
    /// traffic. <b>One that patrols goes back to its beat instead</b> (SRV-5), from wherever the recovery left it.
    /// </summary>
    void GoBackToTheDepot(int car)
    {
        _recovery.Wreck[car] = RecoveryDuty.Nothing;
        if (_serviceBeat.Patrols[car])
        {
            StandTheEvacuatorDown(car);
            if (DriveTheBeat(car)) return;
        }

        EnterTheRecoveryStage(car, RecoveryStage.GoingHome);
        if (!SendToItsOwnBay(car, _recovery.HomeBay[car])) StandTheEvacuatorDown(car);
    }

    /// <summary>Off duty: standing wherever the leg ended, with the recovery cleared and both bars out.</summary>
    void StandTheEvacuatorDown(int car)
    {
        _recovery.Clear(car);
        ShowWhatTheRecoveryIs(car);
    }

    /// <summary>
    /// The leg to the yard: aimed at a free slot rather than at the depot's door, because the slot is what
    /// the truck has to be standing within reach of and aiming at the building would leave the arrival a
    /// street's width from it.
    /// </summary>
    void SendToTheYard(int car)
    {
        var slot = AFreeYardSlot(_recovery.Yard[car]);
        var depot = _recovery.Depot[car];
        if (slot >= 0) SendTo(car, RoadPlaceOfBay(slot), _parking.CentreM(slot));
        else if (depot >= 0) SendTo(car, _roadPlaceOfBuilding[depot], _plan.Buildings.CentreM[depot]);
        else SendTo(car, RoadPlace.Nowhere);
    }

    /// <summary>
    /// <b>Whether this evacuator's yard has a slot for one more wreck</b> (EVA-3): its empty slots, less one for every
    /// other truck of the same yard with a wreck taken and not yet set down. A yard is its depot's and not one
    /// truck's (EVA-2), and two trucks that each counted the one slot left would bring two wrecks back to it.
    /// </summary>
    bool HasRoomInItsYard(int car)
    {
        var yard = _recovery.Yard[car];
        if (yard < 0) return false;

        var room = 0;
        for (var slot = 0; slot < _config.Evacuator.YardSlots; slot++)
        {
            if (EmptyYardSlot(yard, slot) >= 0) room++;
        }

        foreach (var other in _evacuators)
        {
            if (room <= 0) break;
            if (other != car && _recovery.Yard[other] == yard && _recovery.Wreck[other] != RecoveryDuty.Nothing) room--;
        }

        return room > 0;
    }

    /// <summary>The first slot of this yard with nothing standing in it, or −1 for a yard that is full or was never laid.</summary>
    int AFreeYardSlot(int yard)
    {
        if (yard < 0) return -1;

        for (var slot = 0; slot < _config.Evacuator.YardSlots; slot++)
        {
            var bay = EmptyYardSlot(yard, slot);
            if (bay >= 0) return bay;
        }

        return -1;
    }

    /// <summary>And the nearest one of them within the truck's yard reach, which is what says a haul has arrived.</summary>
    int AFreeYardSlotWithinReach(int car)
    {
        var yard = _recovery.Yard[car];
        if (yard < 0) return -1;

        var fromM = Cars.PositionM[car];
        var best = -1;
        var bestM = _config.EvacuatorYardReachM * _config.EvacuatorYardReachM;
        for (var slot = 0; slot < _config.Evacuator.YardSlots; slot++)
        {
            var bay = EmptyYardSlot(yard, slot);
            if (bay < 0) continue;

            var farM = (_parking.CentreM(bay) - fromM).LengthSquared();
            if (farM >= bestM) continue;

            best = bay;
            bestM = farM;
        }

        return best;
    }

    /// <summary>
    /// <b>A wreck put down where the errand puts it</b> — the one placement in this errand, and the containment
    /// slice's own operation rather than a second one (PHY-7a): the solver is handed the pose before the body
    /// is back in the world, so nothing sees it cross the ground between.
    /// </summary>
    /// <remarks>
    /// <b>On the channels of the truck putting it there</b> (PHY-1a), which is what its ground was asked on
    /// (<see cref="IsFreeToSetDown"/>): a body is read on the channels it has, so one carried off a bridge and put down
    /// on the ground with the bridge's would never find the ground again.
    /// </remarks>
    void SetTheWreckDown(int wreck, int by, Vector2 atM, float headingRad)
    {
        PutOnChannels(wreck, Cars.Channels[by]);
        PlaceTheCar(wreck, atM, headingRad);
        ClearTheWheels(wreck);
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
        IsAnEvacuator(car) && _recovery.IsOnARecovery(car) && _recovery.Stage[car] != RecoveryStage.GoingHome;

    /// <summary>
    /// <b>Where this evacuator is to be stopped</b>, and false when nothing is asking it to: the wreck
    /// while it is being fetched, and the slot the haul was aimed at while it is being brought in and
    /// unhitched. <b>The place has to outlast the arrival</b> — a stop point that went away the moment the
    /// truck reached it would let it roll off part way through the hitch.
    /// </summary>
    bool TheRecoveryStopsAt(int car, out Vector2 placeM, out float reachM)
    {
        switch (_recovery.Stage[car])
        {
            case RecoveryStage.Running:
            case RecoveryStage.Hitching:
                // The standing place the leg was laid to and not the wreck itself: what the truck has to be
                // stopped at is where its arm reaches the wreck from, which is a set-down further on. <b>It
                // outlasts the work</b>, or the truck would roll off part way through the hitch.
                placeM = Cars.DestinationM[car];
                reachM = _config.EvacuatorSceneReachM;
                return Cars.HasDestination[car];

            case RecoveryStage.Hauling:
            case RecoveryStage.Unhitching:
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
