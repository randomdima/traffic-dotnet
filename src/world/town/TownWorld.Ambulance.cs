using System.Numerics;
using TrafficSimulation.Agents.Ambulance;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Car.Maneuvers;
using TrafficSimulation.Agents.Evacuator;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Containment;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Statics;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The rescue</b> (AMB-1…9): the hospitals a map has, the ambulances standing at them, and the errand
/// each one runs when somebody is knocked down. <b>The driving itself is the catalogue's</b> — an
/// ambulance runs `P-2`, `P-4`, `P-8`, `P-14`, `P-17` and the ladder like every other car — and what is
/// here is only the errand those manoeuvres are being run for.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the walker's trip machine with a different reason behind it</b>, and deliberately built the
/// same way: a stage per observable state, one place each transition happens, and the leg itself handed
/// to the ordinary drive-leg machinery. An ambulance that drove by rules of its own would be a second
/// driver in the town, and the two would disagree about the road within a week.
/// </para>
/// <para>
/// <b>The whole of what a blue light does to the road is elsewhere</b>, because it belongs to the road:
/// the rank a stretch is laid with (<see cref="RightOfWayOf"/>), the red that stops applying
/// (<see cref="SignalStopM"/>), the kerb that is not given way to (<see cref="GivingWayAtTheKerb"/>) and
/// the patience an overtake no longer waits out (<see cref="DriveScene.WorthGettingPastOnACall"/>). What
/// this file decides is only whether the light is on.
/// </para>
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>Which of this map's buildings are hospitals — declared by the map itself (AMB-1).</summary>
    public BuildingRoster Hospitals => _uses.Hospitals;

    /// <summary>What every ambulance is doing about its call.</summary>
    public RescueDuty Duty => _duty;

    /// <summary>How many ambulances the town actually stood, which is a hospital with a bay near it and not a hospital.</summary>
    public int Ambulances { get; private set; }

    /// <summary>How many people have been knocked down and survived it — every one of them a call raised.</summary>
    public long CasualtiesRaised { get; private set; }

    /// <summary>How many an ambulance reached and got aboard.</summary>
    public long CasualtiesCollected { get; private set; }

    /// <summary>And how many were delivered through a hospital's door, which is the figure the whole slice is for.</summary>
    public long CasualtiesDelivered { get; private set; }

    /// <summary>
    /// How many calls ran out of clock before the casualty was reached (AMB-9) — the honest other half of
    /// the count above, and what says a town is jammed rather than busy.
    /// </summary>
    public long CallsGivenUp { get; private set; }

    /// <summary>
    /// How many casualties are lying in the town waiting to be collected. <b>The reason the common case is
    /// free</b>: an ambulance with nothing to fetch asks one integer rather than walking the roster.
    /// </summary>
    int _woundedCount;

    /// <summary>
    /// <b>A car stood on a hospital's apron becomes that hospital's ambulance</b> (AMB-2, AMB-3): the
    /// hospital it delivers to and the bay held for it, which is where every leg of every call ends.
    /// </summary>
    /// <remarks>
    /// <b>A hospital with no bay near it gets no ambulance</b>, and that is a real state rather than a
    /// failure — a map with no parking anywhere near its hospitals is a map where nobody can be collected,
    /// which <see cref="Ambulances"/> reports rather than hides. Standing the apron itself is
    /// <c>TownWorld.Service.cs</c>, because a hospital's and a station's are the same apron.
    /// </remarks>
    void TakeUpTheRescue(int car, int hospital)
    {
        Cars.Ambulance[car] = true;
        _duty.Hospital[car] = hospital;
        _duty.HomeBay[car] = _parking.BayOf(car);
    }

    /// <summary>
    /// <b>PER-18: a person struck by a car and left alive is a casualty</b> — down where they fell, taking
    /// no actions, and waiting for somebody to come and get them.
    /// </summary>
    /// <remarks>
    /// <b>Everything the trip was holding is given back here.</b> A casualty is not going to walk to the
    /// building it had claimed or drive the car it had booked, and a claim held by a body lying in the road
    /// is a place removed from the town for as long as the rescue takes.
    /// </remarks>
    /// <remarks>
    /// <b>And the body itself stops being something to collide with</b> (PHY-5b), from the tick after the
    /// contact that put it there — the impulse of the impact has already been spent by the step the arbiter
    /// is judging. The road's book is untouched: a driver is still held off a body in the lane by the
    /// stretch it holds, which is what stops the traffic reaching it in the first place.
    /// </remarks>
    void RaiseTheCall(int person)
    {
        if (People.Wounded[person]) return;

        _physics.PutOnLayer(People.Body[person], CollisionLayer.Downed);
        People.Wounded[person] = true;
        People.Walking[person] = false;
        People.ClearWalkedLine(person);
        People.HeldAtTheKerb[person] = false;
        GiveUpTheClaims(person);
        _woundedCount++;
        CasualtiesRaised++;
    }

    /// <summary>
    /// <b>PHY-6: the driver of a car that breaks goes down beside it</b> — out through their own door, onto
    /// the road next to the wreck, and a casualty on the same terms as anybody a car has hit.
    /// </summary>
    /// <remarks>
    /// <b>The body is put down rather than found a place</b>, which is what separates this from alighting
    /// (PHY-7a): an ordinary exit searches for a clear spot beside the car and refuses while there is
    /// none, and a refusal here would leave a casualty inside a wreck — a person nothing will ever come
    /// for (AMB-7). A crash is not an arrangement anybody was making, so the body lands where the door is
    /// and the solver sorts out what it landed on (PHY-9).
    /// </remarks>
    void ThrowTheDriverClear(int car)
    {
        // A hand already out in the road when the vehicle broke under them is nobody's crew any more, on
        // EVA-7's terms — it is standing where it stood and the errand it was on is over.
        LetGoOfTheHand(car);

        ThrowClear(car, _containers.DriverOf(car));
        for (var seat = 0; seat < Containers.CrewSeats; seat++)
        {
            ThrowClear(car, _containers.CrewOf(car, seat));
        }
    }

    /// <summary>One occupant of a broken car, put down beside it and a casualty on the same terms as anybody a car has hit.</summary>
    void ThrowClear(int car, int person)
    {
        if (person < 0) return;

        var doorM = DriverDoorM(car);
        _containers.Alight(car, person);
        People.TripCar[person] = PersonFleet.NoCar;
        People.Stage[person] = TripStage.StandingBy;
        People.ClosesTheRoadM[person] = 0f;
        Place(person, doorM, MathF.Atan2(doorM.Y - Cars.PositionM[car].Y, doorM.X - Cars.PositionM[car].X));
        RaiseTheCall(person);
    }

    /// <summary>
    /// One decision of one ambulance's crew, taken before the driver's own (AMB-5). <b>It decides the
    /// errand and never the driving</b>: what comes out of it is a destination, a chain and whether the
    /// blue light is on, and the catalogue does the rest.
    /// </summary>
    void RunTheRescue(int car, float sinceLastDecisionS)
    {
        if (Cars.Broken[car]) return;

        // The elapsed the call's own clocks integrate over is the driver's and not the loop's nominal
        // interval, exactly as the catalogue's is (<see cref="DecideDriver"/>): an ambulance in an entry
        // that thinks every tick would otherwise run its clocks at six times real time and give up on a
        // call twenty seconds into a two-minute bound.
        var elapsedS = Cars.SinceDecisionS[car] > 0f ? Cars.SinceDecisionS[car] : sinceLastDecisionS;

        var stage = _duty.Stage[car];
        if (stage != RescueStage.Waiting) _duty.SinceS[car] += elapsedS;

        switch (stage)
        {
            case RescueStage.Waiting:
                TakeACall(car);
                return;

            case RescueStage.Running:
                RunToTheScene(car);
                return;

            case RescueStage.Fetching:
                FetchTheCasualty(car);
                return;

            case RescueStage.Tugging:
                TugTheCasualtyToTheVehicle(car);
                return;

            case RescueStage.Loading:
                LoadTheCasualty(car, elapsedS);
                return;

            case RescueStage.Boarding:
                // The paramedic walking back to their own seat: nothing is driven until they are in it —
                // an ambulance that drove off with its crew standing in the road is a station one paramedic
                // short for the rest of the run. What it drives to is whether there is anybody on the
                // stretcher, which is also how a call given up mid-scene gets its crew back.
                if (!TheHandIsAboard(car, elapsedS)) return;

                if (_containers.PassengerOf(car) < 0)
                {
                    GoHome(car);
                    return;
                }

                EnterTheStage(car, RescueStage.Carrying);
                SendToTheHospital(car);
                return;

            case RescueStage.Carrying:
                // The leg ends by the car being stood down at the hospital's bay, which is where the
                // hand-over happens. Anywhere else it stood down — settled short, abandoned — is the same
                // door asked from a little further off, and the call's own clock is what bounds the asking.
                if (!Cars.Driven[car])
                {
                    EnterTheStage(car, RescueStage.HandingOver);
                    return;
                }

                // <b>A delivery that has run out of clock is drawn again and never given up</b> (AMB-9):
                // the casualty is aboard and alive, and there is no answer to a road that would not let
                // this leg through better than laying the leg again from where the car has actually got to
                // (MAN-3). The bay it books may well be a different one by now.
                if (_duty.SinceS[car] >= _config.AmbulanceGiveUpS)
                {
                    _duty.SinceS[car] = 0f;
                    SendToTheHospital(car);
                }

                return;

            case RescueStage.HandingOver:
                HandOverTheCasualty(car);
                return;

            case RescueStage.GoingHome:
                if (!Cars.Driven[car]) StandDown(car);

                return;
        }
    }

    /// <summary>
    /// <b>The nearest casualty nobody is on their way to</b>, and the run to them. Asked only of an
    /// ambulance standing at its station, and only while there is anybody to fetch.
    /// </summary>
    /// <remarks>
    /// <b>One casualty to a call and one call to a casualty.</b> Two ambulances sent to one body is one of
    /// them driving across the town to find the place already attended, which is a call it was not
    /// available for; the claim is the <see cref="RescueDuty.Casualty"/> field and there is no second
    /// register of it.
    /// </remarks>
    void TakeACall(int car)
    {
        if (_woundedCount == 0) return;

        var fromM = Cars.PositionM[car];
        var best = RescueDuty.Nobody;
        var bestM = float.PositiveInfinity;
        for (var person = 0; person < People.Count; person++)
        {
            if (!People.Wounded[person] || People.Inside[person].Any) continue;
            if (IsSpokenFor(person)) continue;

            var farM = (People.PositionM[person] - fromM).LengthSquared();
            if (farM >= bestM) continue;

            best = person;
            bestM = farM;
        }

        if (best < 0 || !IsTheNearestFreeAmbulanceTo(car, best, bestM)) return;

        _duty.Casualty[car] = best;
        _duty.SinceS[car] = 0f;
        _duty.LoadedForS[car] = 0f;
        EnterTheStage(car, RescueStage.Running);
        SendTo(car, TheStandoffM(car, best), ParkingRegistry.NoBay);
    }

    /// <summary>
    /// <b>Where an ambulance is stopped for this casualty</b> (AMB-10): a standoff back along the lane the
    /// body is lying beside, so the vehicle stands clear of the accident and the crew walk the rest.
    /// </summary>
    /// <remarks>
    /// <b>Back along the lane and not simply away from the body.</b> The truck's own hitching place
    /// (<see cref="TheHitchingPlaceM"/>) is measured the same way and for the same reason: a vehicle can only
    /// arrive along the road, so the one place it can be made to stand is a place on the road — and a
    /// standoff measured as a radius would regularly land on a pavement, a wall, or the far carriageway.
    /// <b>Behind the body</b>, because that is the side the ambulance is coming from and the side the
    /// traffic behind it is stopped on.
    /// </remarks>
    Vector2 TheStandoffM(int car, int casualty)
    {
        var lyingAtM = People.PositionM[casualty];
        var lane = _roads.NearestLane(lyingAtM, out var alongM);
        if (lane < 0) return lyingAtM;

        var forward = Spline.SampleAt(_roads.ArcsOf(lane), alongM).Direction;
        return lyingAtM - (forward * _config.AmbulanceStandoffM);
    }

    /// <summary>
    /// <b>Whether this is the ambulance AMB-5 means</b> — the nearest one with nothing else to do — asked
    /// of the casualty it was about to take.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The call belongs to the casualty and the choice above belongs to the crew</b>, and the two have to
    /// be asked in that order. <see cref="TakeACall"/> alone answers "the nearest body to <em>me</em>", which
    /// is not AMB-5: the ambulance that gets the call is then whichever one's decision happened to run
    /// first, and on a city with four ambulances at each of six hospitals that is regularly one two
    /// kilometres away while an idle crew stands a street from the body — which is the whole of a shipped
    /// city's deliveries.
    /// </para>
    /// <para>
    /// <b>Deferring is not a deadlock</b>: a crew that is not the nearest takes nothing and asks again on
    /// its next decision, by which time the nearest has either taken this call or taken a nearer one of its
    /// own. The tie is broken on the car's index so two ambulances equidistant from one body cannot each
    /// wait for the other.
    /// </para>
    /// </remarks>
    bool IsTheNearestFreeAmbulanceTo(int car, int person, float farM)
    {
        for (var other = 0; other < Cars.Count; other++)
        {
            if (other == car || !Cars.Ambulance[other] || Cars.Broken[other] || _duty.IsOnACall(other)) continue;

            var otherM = (People.PositionM[person] - Cars.PositionM[other]).LengthSquared();
            if (otherM < farM || (otherM == farM && other < car)) return false;
        }

        return true;
    }

    /// <summary>Whether some other ambulance has already taken this casualty's call.</summary>
    bool IsSpokenFor(int person)
    {
        for (var car = 0; car < Cars.Count; car++)
        {
            if (_duty.Casualty[car] == person) return true;
        }

        return false;
    }

    /// <summary>
    /// <b>The drive to the standoff</b> (AMB-10): keep the destination the standoff short of the body, and
    /// put the crew out once the car has come to rest at it (`P-18`).
    /// </summary>
    void RunToTheScene(int car)
    {
        if (!TheCallStillStands(car, out var casualty)) return;

        var standoffM = TheStandoffM(car, casualty);

        // A leg that ended before the standoff was reached — settled, abandoned, or a bay left and nowhere
        // gone — is begun again from where the car actually stands (MAN-3). The body is shunted by whatever
        // hits it, so the mark is re-read rather than remembered.
        if (!Cars.Driven[car])
        {
            SendTo(car, standoffM, ParkingRegistry.NoBay);
            return;
        }

        var atRest = Cars.VelocityMps[car].Length() <= _config.Driving.StopSpeedMps;
        var withinReach = (Cars.PositionM[car] - standoffM).Length() <= _config.AmbulanceSceneReachM;
        if (!atRest || !withinReach)
        {
            _duty.LoadedForS[car] = 0f;

            // <b>Come to rest at the place and still short of the mark</b>: the car overshot it, or stopped
            // on the wrong side of it. A place behind the car is not a place to hold at (MAN-6), so the leg
            // is laid again from the pose it is actually in and the route takes it round.
            if (atRest && Cars.Doing[car] == Maneuver.AttendTheScene) SendTo(car, standoffM, ParkingRegistry.NoBay);

            return;
        }

        EnterTheStage(car, RescueStage.Fetching);
    }

    /// <summary>
    /// <b>The paramedic out and over to the body</b> (AMB-10) — the leg the standoff bought, and an ordinary
    /// walk on the ordinary pavement while it lasts.
    /// </summary>
    void FetchTheCasualty(int car)
    {
        if (!TheCallStillStands(car, out var casualty)) return;
        if (!TheHandHasReached(car, People.PositionM[casualty])) return;

        EnterTheStage(car, RescueStage.Tugging);
    }

    /// <summary>
    /// <b>And the body back to the vehicle</b> (AMB-10): the crew walks to their own door and the casualty
    /// comes along behind them, which is the winch (EVA-5) said of a person.
    /// </summary>
    void TugTheCasualtyToTheVehicle(int car)
    {
        if (!TheCallStillStands(car, out var casualty)) return;

        var hand = TheHandOf(car);
        if (hand < 0 || !TheHandIsOut(car))
        {
            GiveUpTheCall(car, counted: true);
            return;
        }

        // Reached the vehicle with the body in tow: the tug is over and the loading begins. The door is the
        // one on the side the crew is walking back from, which is the side they went out of. <b>The body is
        // brought along whether or not this was the step that arrived</b>, or a casualty would be left a
        // stride short of the ambulance on the tick the crew reached it.
        var arrived = TheHandHasReached(car, TheWorkingDoorM(car, People.PositionM[hand]));
        TugAlong(hand, casualty);
        if (arrived) EnterTheStage(car, RescueStage.Loading);
    }

    /// <summary>
    /// <b>The casualty onto the stretcher</b> (AMB-6): the crew's bounded interval, spent standing at the
    /// vehicle rather than out in the road where the body was found.
    /// </summary>
    void LoadTheCasualty(int car, float sinceLastDecisionS)
    {
        if (!TheCallStillStands(car, out var casualty)) return;

        _duty.LoadedForS[car] += sinceLastDecisionS;
        if (_duty.LoadedForS[car] < _config.Ambulance.LoadingS) return;
        if (!_containers.TryLoad(car, casualty)) return;

        Contain(casualty);
        CasualtiesCollected++;
        _woundedCount--;

        EnterTheStage(car, RescueStage.Boarding);
    }

    /// <summary>
    /// <b>Whether there is still a call here to be run</b>, and AMB-9's bound over it: the casualty gone —
    /// healed, or somebody else's — and the clock spent are the three ways one ends before it is finished.
    /// <b>A crew that is out is walked back before the vehicle drives anywhere</b>, which is what
    /// <see cref="GiveUpTheCall"/> does with them.
    /// </summary>
    bool TheCallStillStands(int car, out int casualty)
    {
        casualty = _duty.Casualty[car];
        if (casualty < 0 || !People.Wounded[casualty] || People.Inside[casualty].Any)
        {
            GiveUpTheCall(car, counted: false);
            return false;
        }

        if (_duty.SinceS[car] >= _config.AmbulanceGiveUpS)
        {
            GiveUpTheCall(car, counted: true);
            return false;
        }

        return true;
    }

    /// <summary>
    /// The casualty through the door (OBJ-5), healed, and dwelling inside like anybody else who walked in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Treatment is the dwell and not a clock of its own.</b> A body inside a building for a bounded
    /// interval and then put back out on the pavement is exactly what PER-11 already is; a second timer
    /// beside it would be a second answer to when this person is next seen.
    /// </para>
    /// <para>
    /// <b>A full hospital refuses, and that is a real state</b> (OBJ-5). The ambulance stands at the door
    /// with the casualty aboard and asks again on its next decision — the same wait a walker who found a
    /// building full is put in, and the door empties as the people inside it finish their own dwells.
    /// </para>
    /// </remarks>
    void HandOverTheCasualty(int car)
    {
        var casualty = _duty.Casualty[car];
        var hospital = _duty.Hospital[car];
        if (casualty < 0 || hospital < 0)
        {
            GoHome(car);
            return;
        }

        if (!_containers.TryTransfer(car, casualty, hospital))
        {
            DoorsFoundFull++;
            return;
        }

        // A full participant again the moment the hospital has them (PHY-5b), written while the body is
        // still inside: what walks back out of the door is somebody the town can bump into.
        _physics.PutOnLayer(People.Body[casualty], CollisionLayer.Person);
        People.Wounded[casualty] = false;
        People.Stage[casualty] = TripStage.Dwelling;
        People.TimerS[casualty] = _config.Ambulance.TreatmentS;
        People.DestinationBuilding[casualty] = hospital;
        BuildingsEntered++;
        CasualtiesDelivered++;

        GoHome(car);
    }

    /// <summary>The call given up: the casualty released for whoever can reach them, and the ambulance sent home.</summary>
    /// <remarks>
    /// <b>A crew out in the road is walked back before the vehicle goes anywhere</b> (SRV-3). An ambulance
    /// that drove off the moment its call ran out of clock would leave a paramedic standing at a scene it
    /// had given up on — a body somebody would then have to send another ambulance for, and a hospital one
    /// crew short for the rest of the run.
    /// </remarks>
    void GiveUpTheCall(int car, bool counted)
    {
        if (counted) CallsGivenUp++;

        _duty.Casualty[car] = RescueDuty.Nobody;
        if (TheHandIsOut(car))
        {
            EnterTheStage(car, RescueStage.Boarding);
            return;
        }

        GoHome(car);
    }

    /// <summary>Back to its own bay on the hospital's apron, with the light out: an ambulance between calls is ordinary traffic.</summary>
    void GoHome(int car)
    {
        _duty.Casualty[car] = RescueDuty.Nobody;
        EnterTheStage(car, RescueStage.GoingHome);

        // Already standing in its own bay, or nowhere at its hospital to stand: either way there is no leg
        // to drive.
        var alreadyHome = _parking.BayOf(car) == _duty.HomeBay[car];
        var bay = alreadyHome ? -1 : TheBayAtTheHospital(car);
        if (bay < 0)
        {
            // It waits where it is — an ambulance in the road is still an ambulance, and the next call is
            // what moves it.
            StandDown(car);
            return;
        }

        SendTo(car, _parking.CentreM(bay), bay);
    }

    /// <summary>Off duty: standing wherever the leg ended, with the call cleared and the light out.</summary>
    void StandDown(int car)
    {
        _duty.Clear(car);
        Cars.BlueLight[car] = false;
    }

    /// <summary>The hospital's own bay, which is the leg a delivery is.</summary>
    void SendToTheHospital(int car)
    {
        var hospital = _duty.Hospital[car];
        var bay = TheBayAtTheHospital(car);
        if (bay < 0)
        {
            // No bay at the hospital: drive to its door and hand over from wherever the leg ends, which is
            // what <see cref="RescueStage.HandingOver"/> is asked from anyway.
            SendTo(car, hospital >= 0 ? _plan.Buildings.CentreM[hospital] : Cars.PositionM[car], ParkingRegistry.NoBay);
            return;
        }

        SendTo(car, _parking.CentreM(bay), bay);
    }

    /// <summary>
    /// <b>Its own bay on its own hospital's apron</b> (GEN-4k), which is the place every leg of a call ends
    /// at — held for this ambulance for the whole run, so a rescue never comes back to find its station
    /// taken.
    /// </summary>
    /// <remarks>
    /// The search near the hospital is the fallback for a hospital that never had an apron to give — the
    /// map with no parking near it AMB-2 already reports — and not the ordinary answer.
    /// </remarks>
    int TheBayAtTheHospital(int car)
    {
        var home = _duty.HomeBay[car];
        if (home >= 0 && _parking.IsFreeFor(car, home)) return home;

        var hospital = _duty.Hospital[car];
        return hospital >= 0 ? FreeBayNear(_plan.Buildings.CentreM[hospital], _config.AmbulanceHomeM) : -1;
    }

    /// <summary>
    /// <b>One leg of a call</b>: the place, the bay it books if it has one, and the chain re-derived from
    /// the pose the car is actually in (MAN-3).
    /// </summary>
    /// <remarks>
    /// It is <see cref="SetOff"/> with the destination named rather than looked up. A drive leg's
    /// destination is normally the bay the trip claimed, and a rescue has one leg that is not a bay at all
    /// — a body in the road — so the place is handed in and the rest is the same machinery.
    /// </remarks>
    void SendTo(int car, Vector2 toM, int bay)
    {
        GiveUpTheBay(car);
        if (bay >= 0) TakeTheBay(car, bay);

        Cars.Driven[car] = true;
        Cars.ClearRoute(car);
        GiveUpTheTurn(car);
        RestTheLadder(car);
        Cars.Suspended[car] = Maneuver.None;
        Cars.HasDestination[car] = true;
        Cars.DestinationM[car] = toM;

        PlanTheLeg(car);
        TakeTheNextStep(car);
    }

    /// <summary>
    /// <b>The one place a stage changes</b>, so the blue light is decided in exactly one place (AMB-4) and
    /// the call's own clock means the same thing in each of them: how long <em>this</em> leg of the rescue
    /// has been running. Carried across a stage, a collection that took two minutes would arrive at the
    /// hospital with its delivery already out of time.
    /// </summary>
    void EnterTheStage(int car, RescueStage stage)
    {
        _duty.Stage[car] = stage;
        _duty.SinceS[car] = 0f;
        Cars.BlueLight[car] = _duty.IsHurrying(car);
    }

    /// <summary>
    /// <b>How far ahead of the rear axle the place this car was sent to stands</b>, along the line it is
    /// driving — `P-18`'s whole <c>Sa</c>, and infinity for every car that is not on its way to one.
    /// </summary>
    /// <remarks>
    /// <b>It is the casualty projected onto the line and not the distance to them.</b> A line bends, and a
    /// body ten metres away across a kerb is not ten metres along the road; what a stop point has to be is
    /// a distance the profile can brake against, which is a place on the line the car is holding.
    /// </remarks>
    float ToTheSceneM(int car)
    {
        // <b>And asked of a hand's order first of all</b> (CTL-8a, CTL-8c). A car sent to a place on the
        // road is stopped at it by `P-18` exactly as a rescue is stopped at its casualty, and one sent
        // after another car is stopped a gap short of it — one entry, three errands and a hand.
        if (TheOrderStopsAt(car, out var orderedM))
        {
            return ToThePlaceOnTheLineM(car, orderedM, _config.OrderedPlaceReachM);
        }

        // <b>The place has to outlast the arrival</b>, as the recovery's does: a stop point that went away
        // the moment the ambulance reached it would let the vehicle roll off while the crew were out
        // walking to the body.
        if (Cars.Ambulance[car] && IsAtOrOnItsWayToAScene(car))
        {
            var casualty = _duty.Casualty[car];
            return casualty < 0 || People.Inside[casualty].Any
                ? float.PositiveInfinity
                : ToThePlaceOnTheLineM(car, TheStandoffM(car, casualty), _config.AmbulanceSceneReachM);
        }

        // <b>And asked of an officer's scene</b> (SRV-6), which is the same place further back along the
        // same lane: a police car standing at one is held there for as long as the road is closed.
        if (TheClosureStopsAt(car, out var standoffM))
        {
            return ToThePlaceOnTheLineM(car, standoffM, _config.AmbulanceSceneReachM);
        }

        // <b>The same question asked of a wreck</b> (EVA-3, EVA-6). A recovery stops beside what it has come
        // for and again beside where it is putting it, and `P-18` is what stops it every time — what differs
        // is the place and how near the crew has to be to work.
        return TheRecoveryStopsAt(car, out var placeM, out var reachM)
            ? ToThePlaceOnTheLineM(car, placeM, reachM)
            : float.PositiveInfinity;
    }

    /// <summary>
    /// <b>How far ahead of the rear axle a place stands along the line this car is driving</b>, or infinity
    /// where the line does not come past it — `P-18`'s whole <c>Sa</c>.
    /// </summary>
    float ToThePlaceOnTheLineM(int car, Vector2 placeM, float workingReachM)
    {
        var line = Cars.LineOf(car);
        if (line.Length == 0) return float.PositiveInfinity;

        // <b>Forward along the line and never the nearest point on it.</b> A route out of a bay regularly
        // begins beside the very body it is going to fetch and then runs the other way round the block, so
        // the globally nearest point is behind the car — and a stop point behind a car is a car holding
        // still for ever a street away from the casualty. What the profile can brake against is the next
        // time this line comes past the body, which is what is searched for here.
        var progressM = Cars.ProgressM[car];
        var windowM = SightM(car);
        var atM = Spline.ProjectM(line, placeM, progressM + windowM, windowM);
        var offM = (Spline.SampleAt(line, atM).PositionM - placeM).Length();

        // A casualty the line does not actually run past is not a place to stop at: the ambulance is still
        // routing, and `P-4` goes on driving until the line does reach them.
        if (offM > workingReachM) return float.PositiveInfinity;

        // <b>A place behind the axle is one of two things, and the crew's reach is what tells them apart.</b>
        // Fifteen metres back is the body this line comes round to later, and holding for it is the car
        // standing still a street away for ever; half a car length back is the crew's own working position,
        // and giving that up is an ambulance that must come to a dead stop with its axle short of the body
        // or drive away and try again — which is what an overshoot of centimetres would cost.
        if (atM < progressM)
        {
            return (Cars.PositionM[car] - placeM).Length() <= workingReachM
                ? atM - progressM
                : float.PositiveInfinity;
        }

        return atM - progressM;
    }

    /// <summary>
    /// <b>An ambulance that has been wrecked</b>: the casualty aboard is put back in the road as a casualty
    /// again, so somebody else's call can reach them, and the crew goes down beside it like any other
    /// driver (PHY-6).
    /// </summary>
    /// <remarks>
    /// <b>Out of the back and not out of the door</b>, which is where a stretcher comes out and — the half
    /// that matters here — is not where the crew lands, so a wrecked ambulance leaves two bodies on the
    /// road rather than two bodies in one place.
    /// </remarks>
    void SpillTheAmbulance(int car)
    {
        var casualty = _containers.PassengerOf(car);
        _duty.Clear(car);
        Cars.BlueLight[car] = false;
        if (casualty < 0) return;

        var headingRad = Cars.HeadingRad[car];
        var behind = -Heading.Unit(headingRad);
        var tailM = Cars.PositionM[car] + (behind * (Cars.BuildOf(car).HalfLengthM + _config.PersonDiameterM));

        _containers.Unload(car, casualty);
        Place(casualty, tailM, headingRad);
        _woundedCount++;
    }

    /// <summary>
    /// <b>Whether this leg is a run to a body in the road rather than to a bay</b> (AMB-5) — the one thing
    /// about a rescue the ordinary drive-leg machinery has to know, and the whole of what it changes is
    /// where the route search is aimed.
    /// </summary>
    bool IsOnItsWayToAScene(int car) => Cars.Ambulance[car] && _duty.Stage[car] == RescueStage.Running;

    /// <summary>
    /// <b>And whether it is at one</b> — every stage between arriving at the standoff and having the crew
    /// back aboard (AMB-10). It is what keeps `P-18` holding the vehicle still while the work is done on
    /// foot, which the arrival on its own would not.
    /// </summary>
    bool IsAtOrOnItsWayToAScene(int car) =>
        _duty.Stage[car] is RescueStage.Running or RescueStage.Fetching or RescueStage.Tugging
            or RescueStage.Loading or RescueStage.Boarding;

    /// <summary>
    /// <b>Whether this leg finishes at a place on a lane rather than in a bay</b> — the one thing about an
    /// errand the ordinary drive-leg machinery has to know, and the whole of what it changes is where the
    /// route search is aimed and whether a bay is claimed for the arrival.
    /// </summary>
    /// <remarks>
    /// The two errands that have one are a rescue's run to a body in the road (AMB-5) and a recovery's run
    /// to a wreck and haul to a yard slot (EVA-3, EVA-6) — a leg that claimed a bay would be a car that
    /// parked instead of arriving. <b>And two of the player's four orders</b> (CTL-8a, CTL-8c), which is
    /// the whole of what makes "drive to that spot" different from "park near it".
    /// </remarks>
    bool IsAimedAtAPlaceInTheRoad(int car) =>
        IsOnItsWayToAScene(car) || IsOnItsWayToAWreck(car) || IsOrderedToAPlaceInTheRoad(car);

    /// <summary>The world seed's stream an ambulance and its crew are drawn from, which belongs to nothing else.</summary>
    const ulong RescueStream = 0x414D4255;
}
