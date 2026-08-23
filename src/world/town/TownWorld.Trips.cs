using System.Numerics;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Car.Maneuvers;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Containment;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Routing;

namespace TrafficSimulation.World.Town;

/// <summary>
/// The trip: why anybody in this town goes anywhere, and everything it needs — the containment
/// contract, the parking registry, and the stretch of a drive leg that is a template rather than a
/// route.
/// </summary>
/// <remarks>
/// <para>
/// A trip is a person's and never a car's: a person draws a building, decides whether the trip is worth
/// a car, walks to one if it is, and the car's destination is theirs for as long as they are sitting in
/// it. Everything a car does here is a stretch of somebody's trip.
/// </para>
/// <para>
/// <b>What is not here is the catalogue.</b> Each stage covers the ground a named manoeuvre will own,
/// and what is missing between them is the arbitration table and the escalation ladder — so a leg that
/// fails is given up and drawn again rather than walked down a ladder.
/// </para>
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>How many buildings a draw may look at before the walker stands and draws again next time.</summary>
    const int DrawsPerTrip = 4;

    /// <summary>How many bays a drive leg considers, nearest the destination first. A bound on the work, not a preference.</summary>
    const int BaysConsideredPerLeg = 4;

    /// <summary>How many of those are worth a route search each, which is what actually costs something.</summary>
    const int BaysRoutedPerLeg = 2;

    /// <summary>
    /// The short random beat before looking at the lane at all, as a share of the give-way patience. A
    /// stagger and not a wait — its whole job is that two neighbouring bays do not take one gap.
    /// </summary>
    const float BeatOfThePatience = 0.05f;

    /// <summary>`P-11`'s patience, in dwells: well above one, so an ordinary turnover is always waited out.</summary>
    const float PlacePatienceInDwells = 3f;

    /// <summary>Room for the proximity index's answer at an exit search. More than a doorway ever holds.</summary>
    const int SpotsNearAWayOut = 64;

    readonly int[] _bayCandidates = new int[BaysConsideredPerLeg];
    readonly int[] _spotNearby = new int[SpotsNearAWayOut];

    /// <summary>How many trips have been drawn, and how many of them were worth a car.</summary>
    public long TripsDrawn { get; private set; }

    public long TripsWorthACar { get; private set; }

    /// <summary>How many people have got into a car, come to rest in a bay, and got out again.</summary>
    public long Boardings { get; private set; }

    public long BaysParkedIn { get; private set; }

    public long Alightings { get; private set; }

    /// <summary>How many have walked in through a door — the figure a trip is finished by.</summary>
    public long BuildingsEntered { get; private set; }

    /// <summary>And how many found it full when they got there, which is a real state and not a failure (`P-11`).</summary>
    public long DoorsFoundFull { get; private set; }

    /// <summary>How many trips were given up on — the honest other half of the count above.</summary>
    public long TripsGivenUp { get; private set; }

    public Containers Containment => _containers;

    public ParkingRegistry Parking => _parking;

    /// <summary>
    /// A contained person: not in the town at all, so what runs for them is the trip and never the
    /// follower. Only the container acts — a passenger's whole action set is exit, and a driver's is
    /// exit plus the car's.
    /// </summary>
    void DecideContained(int person, float sinceLastDecisionS)
    {
        switch (People.Stage[person])
        {
            case TripStage.Dwelling:
                People.TimerS[person] -= sinceLastDecisionS;
                if (People.TimerS[person] > 0f) return;

                // `P-1`: refused means every spot outside the door is taken. That is not a stall — the
                // doorway empties as soon as whoever is standing in it walks off.
                if (!TryLeaveTheBuilding(person)) return;

                // An ordered unit idles awaiting the next order rather than drawing a trip.
                if (People.Manual[person]) People.Stage[person] = TripStage.UnderOrders;
                else DrawTrip(person);

                return;

            case TripStage.Alighting:
                TryAlight(person);
                return;

            case TripStage.Driving:
                // A car that has stopped driving with somebody still in it — its leg ended somewhere
                // this stage did not hear about — is got out of rather than sat in.
                var inside = _containers.WhereIs(person);
                if (inside.Kind != ContainerKind.Car || Cars.Doing[inside.Index] == Maneuver.None)
                {
                    People.Stage[person] = TripStage.Alighting;
                }

                return;
        }
    }

    /// <summary>
    /// A walker with nothing to walk: it has arrived, or it never had anywhere to go. Which of those it
    /// is, is the stage's own question, and every stage answers it — there is no state a body can stand
    /// still in that nothing is running for.
    /// </summary>
    void StandingStill(int person, float sinceLastDecisionS)
    {
        switch (People.Stage[person])
        {
            case TripStage.WalkingToTheDoor:
                if (!HasReached(person, _config.WayInTouchingReachM)) break;

                WalkArrivals++;
                EnterTheBuilding(person);
                return;

            case TripStage.WalkingToTheCar:
                if (!HasReached(person, _config.WayInTouchingReachM)) break;

                WalkArrivals++;
                BoardTheCar(person);
                return;

            case TripStage.WaitingForAPlace:
                People.TimerS[person] -= sinceLastDecisionS;

                // `E-6`: past a patience well above one dwell this is not a turnover, so the place is
                // given up rather than waited for. Dwell is bounded, so an ordinary one always ends.
                if (People.TimerS[person] > 0f) EnterTheBuilding(person);
                else DrawTrip(person);
                return;

            case TripStage.StandingBy:
                People.TimerS[person] -= sinceLastDecisionS;
                if (DoneStandingAbout(person)) DrawTrip(person);
                return;

            case TripStage.UnderOrders:
                // An order carried out ends in idle-awaiting-orders. One that has not is a leg like any
                // other and is laid again below.
                if (HasReached(person, People.RadiusM[person])) return;

                break;

            case TripStage.Driving:
            case TripStage.Alighting:
                return;
        }

        // The leg ended short of where it was going: the line ran out, so it is laid again from where
        // the body has got to. A leg that cannot be laid at all is one this trip has no way of
        // finishing, and it is given up rather than re-asked sixty times a second.
        LayWalk(person, reachTheGoal: true);
        People.Walking[person] = People.WalkedCount[person] > 0;
        if (People.Walking[person]) return;

        // A failed order idles awaiting the next one and never draws a goal of its own.
        if (People.Manual[person]) People.Stage[person] = TripStage.UnderOrders;
        else GiveUpTheTrip(person);
    }

    /// <summary>Whether the body is at the place this leg was aimed at, which is a distance and not a state.</summary>
    bool HasReached(int person, float reachM) => (People.GoalM[person] - People.PositionM[person]).Length() <= reachM;

    /// <summary>
    /// `P-10`'s draw: somewhere to be, and whether it is worth a car.
    /// </summary>
    /// <remarks>
    /// Both ends are screened when the trip is chosen, with the strict question rather than the
    /// best-effort one: a door has to be walkable-to, or this is a trip that can only end in the ladder.
    /// A draw that finds nowhere stands and draws again on its own clock.
    /// </remarks>
    void DrawTrip(int person)
    {
        GiveUpTheClaims(person);
        People.Stage[person] = TripStage.StandingBy;
        People.TimerS[person] = People.Draw[person].NextFloat(0f, _config.Person.StandByIdleMaxS);
        People.Manual[person] = false;

        var buildings = _plan.Buildings;
        if (buildings.Count == 0)
        {
            // A map with nothing to go to — a scenario map — is walked rather than stood still on. Where a
            // walker was put down decides which: in a carriageway it reels down it, beside one it paces
            // across it, and anywhere else it wanders.
            if (!ReelDownTheRoad(person) && !PaceTheRoad(person)) WanderInstead(person);
            return;
        }

        var fromM = People.PositionM[person];
        for (var attempt = 0; attempt < DrawsPerTrip; attempt++)
        {
            var building = People.Draw[person].NextInt(buildings.Count);

            // Preferring one with room, once the people already walking there are counted — and taking
            // one anyway on the last look, because every building being spoken for is not a reason to
            // stand still.
            if (attempt < DrawsPerTrip - 1 && !_containers.LooksLikelyToHaveRoom(building)) continue;

            var doorM = DoorOf(building, fromM);
            if ((doorM - fromM).Length() <= _config.WayInTouchingReachM) continue;
            if (!IsWalkableTo(doorM)) continue;

            BeginTrip(person, building, doorM);
            return;
        }
    }

    /// <summary>
    /// The trip drawn: the door claimed, the walk to it laid, and then the one structural question —
    /// is this trip worth a car.
    /// </summary>
    /// <remarks>
    /// The walk is laid <em>first</em> because "the same block" is a fact about the route and not about
    /// the distance: a destination fifty metres away across a dual carriageway is two crossings, and one
    /// three hundred metres away round a corner is none.
    /// </remarks>
    void BeginTrip(int person, int building, Vector2 doorM)
    {
        TripsDrawn++;
        People.DestinationBuilding[person] = building;
        _containers.Claim(building);
        People.Stage[person] = TripStage.WalkingToTheDoor;
        WalkTo(person, doorM);

        var farM = (doorM - People.PositionM[person]).Length();
        if (Trip.IsWorthWalking(_config, farM, WalkCrossesACarriageway(person))) return;

        TripsWorthACar++;
        if (TryDrive(person, doorM)) return;

        // Only walks the whole way when no free intact car can be reached. The walk is already laid, so
        // this is the same leg it would have taken anyway.
        People.Stage[person] = TripStage.WalkingToTheDoor;
    }

    /// <summary>
    /// The drive leg, arranged before a step is taken: a car within a walk, and a bay within a walk of
    /// the destination — the bay claimed only once a route to it exists.
    /// </summary>
    bool TryDrive(int person, Vector2 doorM)
    {
        var car = NearestCarWorthWalkingTo(person);
        if (car < 0) return false;

        var fromLane = LaneACarWouldSetOffOn(car);
        if (fromLane < 0) return false;

        var bays = _parking.BaysNear(doorM, _config.PersonWalkWorthM, _bayCandidates);
        var searched = 0;
        for (var slot = 0; slot < bays && searched < BaysRoutedPerLeg; slot++)
        {
            var bay = _bayCandidates[slot];
            if (bay == _parking.BayOf(car)) continue;

            searched++;
            if (!RouteExistsToTheBay(fromLane, bay)) continue;
            if (!_parking.TryReserve(bay, car)) continue;

            People.TripCar[person] = car;
            People.Stage[person] = TripStage.WalkingToTheCar;
            WalkTo(person, WayInOf(car));
            return true;
        }

        return false;
    }

    /// <summary>
    /// The nearest car this trip may use: free, stopped and intact, inside a walk, reachable on foot,
    /// and — where it is standing in a bay — one it can be got out of.
    /// </summary>
    /// <remarks>
    /// Nobody owns a car here: the shipped maps carry no owner, so every car is driven by whoever is
    /// nearest and eligible. A map that named owners would be read here and nowhere else.
    /// </remarks>
    int NearestCarWorthWalkingTo(int person)
    {
        var fromM = People.PositionM[person];
        var best = -1;
        var bestM = _config.PersonWalkWorthM;
        for (var car = 0; car < Cars.Count; car++)
        {
            if (!CanBeBoarded(car)) continue;

            var farM = (Cars.PositionM[car] - fromM).Length();
            if (farM >= bestM) continue;

            var bay = _parking.BayOf(car);
            if (bay >= 0 && !_parking.CanBeLeft(bay)) continue;
            if (!IsWalkableTo(WayInOf(car))) continue;

            best = car;
            bestM = farM;
        }

        return best;
    }

    /// <summary>Asked of the car's own state: nobody in it, not moving, not broken.</summary>
    bool CanBeBoarded(int car) =>
        !Cars.Broken[car] && _containers.IsFree(car) &&
        Cars.VelocityMps[car].LengthSquared() <= _config.Driving.StopSpeedMps * _config.Driving.StopSpeedMps;

    /// <summary>
    /// Where a walk to a car is aimed: the bay's own way in where it is parked in one, and the ground
    /// off the driver's door where it is not — one at a kerb, one stopped in the road (`P-5`).
    /// </summary>
    Vector2 WayInOf(int car)
    {
        var bay = _parking.BayOf(car);
        if (bay >= 0) return _parking.WayInM(bay);

        var headingRad = Cars.HeadingRad[car];
        var forward = Heading.Unit(headingRad);
        var door = new Vector2(-forward.Y, forward.X) * -_config.RoadSideSign;
        return Cars.PositionM[car] + door * (_config.Car.WidthM * 0.5f + _config.PersonDiameterM);
    }

    /// <summary>The lane a car would take when it sets off: its bay's own, or the one it is standing on.</summary>
    int LaneACarWouldSetOffOn(int car)
    {
        var bay = _parking.BayOf(car);
        if (bay >= 0) return _parking.LeaveLane(bay);

        var rearAxleM = CarFollower.RearAxleM(_config, Cars.PositionM[car], Cars.HeadingRad[car]);
        return _roads.NearestLane(rearAxleM, out _);
    }

    /// <summary>
    /// A bay is claimed only once a route to it exists: the search is asked before the reservation, so
    /// an unroutable bay is handed back rather than held for a car that will never arrive.
    /// </summary>
    bool RouteExistsToTheBay(int fromLane, int bay)
    {
        var goals = _driveSearch.Goals;
        var goalCount = BayGoals(bay, goals, out var goalPointM);
        if (goalCount == 0) return false;

        var entry = _driving.EntryOnLane(fromLane, _roads.LaneLengthM[fromLane]);
        _driveSearch.Entries[0] = entry;
        if (entry.Link == TravelGraph.NoLink) return false;

        for (var slot = 0; slot < goalCount; slot++)
        {
            if (goals[slot].Link == entry.Link) return true;
        }

        return SearchTheDrivingNetwork(goalCount, goalPointM, out var goalSlot) > 0 && goalSlot >= 0;
    }

    /// <summary>Where a drive leg ends, as the search takes it: the place on the lane the bay's own template is staged from.</summary>
    int BayGoals(int bay, Span<RouteGoal> into, out Vector2 goalPointM)
    {
        goalPointM = Vector2.Zero;
        if (bay < 0 || !_parking.CanBeEntered(bay)) return 0;

        var lane = _parking.EnterLane(bay);
        var alongM = _parking.EnterAlongM(bay);
        var link = _driving.LinkOfLane(lane);
        if (link == TravelGraph.NoLink) return 0;

        goalPointM = Spline.SampleAt(_roads.ArcsOf(lane), alongM).PositionM;
        into[0] = new RouteGoal(link, _driving.PlaceOfM(lane, alongM));
        return 1;
    }

    /// <summary>
    /// Whether a place can be walked to at all — the strict question, asked of both ends when a trip is
    /// drawn: the pavement's network has to come within the one short straight hop allowed off it.
    /// </summary>
    bool IsWalkableTo(Vector2 pointM)
    {
        if (!_terrain.Contains(pointM)) return false;

        var goals = _walkSearch.Goals;
        if (_walking.GoalsAt(pointM, goals) == 0) return false;

        return (NetworkPointM(goals[0]) - pointM).Length() <= _config.PersonOffNetworkHopM;
    }

    /// <summary>Where a place on the walking network actually stands, which is what the hop off it is measured against.</summary>
    Vector2 NetworkPointM(RouteGoal goal)
    {
        var runs = _walking.Runs;
        var slot = runs.PieceAt(goal.Link, goal.AlongM, out var alongPieceM);
        var edge = runs.PiecesOf(goal.Link)[slot];
        return Spline.SampleAt(_foot.ArcsOf(edge), alongPieceM).PositionM;
    }

    /// <summary>Whether the walk just laid steps onto a road — which, the network being what it is, is whether it uses a crossing.</summary>
    bool WalkCrossesACarriageway(int person)
    {
        var crossings = People.WalkedCrossingOf(person);
        for (var point = 0; point < People.WalkedCount[person]; point++)
        {
            if (crossings[point] != CityPlan.NoRecord) return true;
        }

        return false;
    }

    /// <summary>One leg: the goal, the line to it, and the clocks that decide whether it is going anywhere.</summary>
    void WalkTo(int person, Vector2 goalM)
    {
        People.GoalM[person] = goalM;
        LayWalk(person, reachTheGoal: true);
        People.Walking[person] = People.WalkedCount[person] > 0 || !HasReached(person, People.RadiusM[person]);
        _progress.Restart(person);
    }

    /// <summary>
    /// A walkable place drawn out of the town, for a map with no buildings on it — a scenario map is a
    /// rig and its walkers have nowhere to be.
    /// </summary>
    void WanderInstead(int person)
    {
        if (!Wander.DrawDestination(_plan, _terrain, ref People.Draw[person], out var goalM)) return;

        People.Stage[person] = TripStage.WalkingToTheDoor;
        People.DestinationBuilding[person] = PersonFleet.NoBuilding;
        WalkTo(person, goalM);
    }

    /// <summary>
    /// <b>A walker with nowhere to be and a road beside it paces across it</b>: out into the lane, a stand
    /// in it, back to where it was put down, and out again. False where there is no road at hand, which is
    /// every walker on a map that has somewhere for them to be.
    /// </summary>
    /// <remarks>
    /// <b>Stepping out is refused while the ground is anybody's</b> and going back never is
    /// (<see cref="StepOut"/>). A refusal leaves the walker standing by on the short idle
    /// <see cref="DrawTrip"/> has already set, so the road is asked again a moment later — which is the
    /// whole of the wait on the pavement, and why the body is back in the lane within a second of the last
    /// car clearing it.
    /// </remarks>
    /// <summary>
    /// <b>A walker with nowhere to be that was put down in a carriageway reels along it</b>: a lurch down
    /// the road it is on, thrown anywhere across the width of it, and every few lurches a stand where it
    /// stopped. False for everybody else, which is every walker on a map that has somewhere to be.
    /// </summary>
    /// <remarks>
    /// <b>The stand is the whole reason the traffic ever gets past.</b> A body reeling down a lane is
    /// something to follow — it is going somewhere, so `E-4`'s bar on going round something moving holds —
    /// and one that has stopped is something in the way, which is what a driver waits its obstruction wait
    /// behind and then goes round.
    /// </remarks>
    bool ReelDownTheRoad(int person)
    {
        if (!ReelsDownARoad(person)) return false;

        if (People.Draw[person].NextInt(_config.Person.LurchesPerStand) == 0)
        {
            StandABeat(person, _config.Person.LurchStandS);
            return true;
        }

        var lurch = Reel.NextLurch(
            _config, _roads, _occupancy, person, People.PositionM[person],
            Heading.Unit(People.HeadingRad[person]), People.RadiusM[person], ref People.Draw[person], out var goalM);

        // Off the road altogether — thrown there by a contact — is a body the ordinary wander carries, and
        // a lurch that found no road is how this says so.
        if (lurch == Lurch.NoRoad) return false;

        // Refused leaves it standing on the short idle `DrawTrip` has already set, so it asks the road again
        // a moment later. It is standing in a lane while it does, which is the whole of what it is.
        if (lurch == Lurch.NoRoom) return true;

        People.Stage[person] = TripStage.WalkingToTheDoor;
        People.DestinationBuilding[person] = PersonFleet.NoBuilding;
        WalkTo(person, goalM);
        return true;
    }

    /// <summary>
    /// Whether this walker is one of the reeling ones. <b>A fact about the map and the pose the map put the
    /// body in</b>: nowhere to be, no pavement to walk along, and put down on the carriageway itself.
    /// </summary>
    /// <remarks>
    /// <b>Where the body was put down and never where it has got to</b> — a pacer standing out in the lane
    /// is in a carriageway too, and one that changed rule as it walked would pace out and reel away.
    /// </remarks>
    bool ReelsDownARoad(int person) =>
        _plan.Buildings.Count == 0 && _foot.EdgeCount == 0
        && Reel.InTheCarriageway(_roads, People.StoodAtM[person]);

    bool PaceTheRoad(int person)
    {
        if (!PacesARoad(person, out var step, out var inTheRoad)) return false;
        if (!inTheRoad && !StepOut.RoomToStepOut(_config, _occupancy, step, People.StoodAtM[person])) return true;

        People.Stage[person] = TripStage.WalkingToTheDoor;
        People.DestinationBuilding[person] = PersonFleet.NoBuilding;
        WalkTo(person, inTheRoad ? People.StoodAtM[person] : step.RoadM);
        return true;
    }

    /// <summary>
    /// Whether this walker is one of the paced ones, and which of its two places it is standing at.
    /// </summary>
    /// <remarks>
    /// <b>It is a fact about the map: nowhere to be, and no pavement to walk along.</b> A town with a
    /// walking network has somewhere for a walker with no trip to go — that is what a wander is — and its
    /// walkers cross a carriageway on the paint (`P-12`). A map with no pavement at all has laid nothing
    /// for them but the road beside them.
    /// </remarks>
    bool PacesARoad(int person, out PacedStep step, out bool inTheRoad)
    {
        inTheRoad = false;
        step = default;
        if (_plan.Buildings.Count > 0 || _foot.EdgeCount > 0) return false;

        // A body put down in the carriageway is reeling down it and not pacing across it — the two rules
        // are told apart by the pose the map left the body in, and by nothing else.
        if (Reel.InTheCarriageway(_roads, People.StoodAtM[person])) return false;
        if (!StepOut.BesideARoad(_config, _roads, People.StoodAtM[person], out step)) return false;

        var positionM = People.PositionM[person];
        inTheRoad = (positionM - step.RoadM).LengthSquared() < (positionM - People.StoodAtM[person]).LengthSquared();
        return true;
    }

    /// <summary>
    /// Whether this stand is over. <b>A body that has stepped into a lane stands there until something has
    /// come to rest for it</b>, which is the whole of what it stepped out for — a clock that ended the stand
    /// early would let a driver that was already braking pick the throttle up again, and the shape would go
    /// unmeasured. It walks off the moment there is a car standing in front of it and not a moment after,
    /// because a body still in the way of a driver who has already answered is measuring that driver's
    /// patience.
    /// </summary>
    /// <remarks>
    /// <b>The beat is the bound on it and never the reason it ends.</b> A body steps out into a clear lane
    /// and waits there, so on a road anything drives the car always arrives; what the bound covers is the
    /// road nothing comes down at all, which would otherwise be a body standing in a lane for good.
    /// </remarks>
    bool DoneStandingAbout(int person) =>
        People.TimerS[person] <= 0f
        || (PacesARoad(person, out var step, out var inTheRoad)
            && inTheRoad
            && StepOut.StoodStillFor(_config, _occupancy, step));

    /// <summary>
    /// The beat: standing out in the lane for as long as it takes something to arrive and come to rest, and
    /// drawn afresh so that no two walkers keep the same time. <b>A stand on purpose is not a stall</b>, so
    /// the clock that gives a leg up does not run while it lasts — the same thing that is true of a walker
    /// held at a kerb.
    /// </summary>
    void StandABeat(int person) => StandABeat(person, _config.Person.StandAboutS);

    void StandABeat(int person, float beatS)
    {
        People.Walking[person] = false;
        People.Stage[person] = TripStage.StandingBy;
        People.TimerS[person] = beatS + People.Draw[person].NextFloat(0f, beatS);
    }
}
