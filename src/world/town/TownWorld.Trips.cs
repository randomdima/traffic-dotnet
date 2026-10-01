using System.Numerics;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Containment;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Routing;

namespace TrafficSimulation.World.Town;

/// <summary>
/// The trip: why anybody in this town goes anywhere. <b>A door, a walk to it, and a dwell behind it</b>
/// (PER-9, PER-11) — and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every trip is walked</b> (PER-11). A person draws a building, walks to its way in over the
/// pavement's own network and dwells inside it before drawing the next one; there is no leg that is
/// driven, no car to be chosen and no bay to be claimed on anybody's behalf. What a car does in this town
/// is its own (`CAR-1`), and the two rosters meet only on the ground they share.
/// </para>
/// <para>
/// <b>A stage is an errand and never an action</b> (AGT-7): what a body does is a leg, and a leg that
/// fails is given up and drawn again.
/// </para>
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>How many buildings a draw may look at before the walker stands and draws again next time.</summary>
    const int DrawsPerTrip = 4;

    readonly int[] _bayCandidates = new int[BaysConsideredPerLeg];

    /// <summary>How many bays a search for one considers, nearest the place first. A bound on the work, not a preference.</summary>
    const int BaysConsideredPerLeg = 4;

    /// <summary>How many of those are worth a route search each, which is what actually costs something.</summary>
    const int BaysRoutedPerLeg = 2;

    /// <summary>How many trips have been drawn.</summary>
    public long TripsDrawn { get; private set; }

    /// <summary>How many times a car has come to rest in the bay it was aiming at.</summary>
    public long BaysParkedIn { get; private set; }

    /// <summary>How many have walked in through a door — the figure a trip is finished by.</summary>
    public long BuildingsEntered { get; private set; }

    /// <summary>And how many found it full when they got there, which is a real state and not a failure (`PER-11`).</summary>
    public long DoorsFoundFull { get; private set; }

    /// <summary>How many trips were given up on — the honest other half of the count above.</summary>
    public long TripsGivenUp { get; private set; }

    public Containers Containment => _containers;

    public ParkingRegistry Parking => _parking;

    /// <summary>
    /// A contained person: not in the town at all, so what runs for them is the trip and never the
    /// follower. <b>A building is the only container a person is in now</b>, so the whole action set is
    /// leaving it (PER-6).
    /// </summary>
    void DecideContained(int person, float sinceLastDecisionS)
    {
        if (People.Stage[person] != TripStage.Dwelling) return;

        People.TimerS[person] -= sinceLastDecisionS;
        if (People.TimerS[person] > 0f) return;

        // PHY-7a: refused means every spot outside the door is taken. That is not a stall — the
        // doorway empties as soon as whoever is standing in it walks off.
        if (!TryLeaveTheBuilding(person)) return;

        // An ordered unit idles awaiting the next order rather than drawing a trip.
        if (People.Manual[person]) People.Stage[person] = TripStage.UnderOrders;
        else DrawTrip(person);
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

            case TripStage.WaitingForAPlace:
                People.TimerS[person] -= sinceLastDecisionS;

                // Past a patience well above one dwell this is not a turnover, so the place is
                // given up rather than waited for. Dwell is bounded, so an ordinary one always ends.
                if (People.TimerS[person] > 0f) EnterTheBuilding(person);
                else DrawTrip(person);
                return;

            case TripStage.StandingBy:
                People.TimerS[person] -= sinceLastDecisionS;
                if (People.TimerS[person] <= 0f) DrawTrip(person);
                return;

            case TripStage.UnderOrders:
                // An order carried out ends in idle-awaiting-orders. One that has not is a leg like any
                // other and is laid again below.
                if (HasReached(person, People.RadiusM[person])) return;

                break;
        }

        // The leg ended short of where it was going: the line ran out, so it is laid again from where
        // the body has got to. A leg that cannot be laid at all is one this trip has no way of
        // finishing, and it is given up rather than re-asked sixty times a second.
        LayWalk(person, reachTheGoal: true);
        SetWalking(person, People.RouteCount[person] > 0);
        if (People.Walking[person])
        {
            // A fresh chain is fresh ground to be measured against: the clock run up reaching the end of
            // the last one is not time this leg spent getting nowhere (<see cref="LegProgress"/>).
            _progress.Restart(person);
            return;
        }

        // A failed order idles awaiting the next one and never draws a goal of its own.
        if (People.Manual[person]) People.Stage[person] = TripStage.UnderOrders;
        else GiveUpTheTrip(person);
    }

    /// <summary>Whether the body is at the place this leg was aimed at, which is a distance and not a state.</summary>
    bool HasReached(int person, float reachM) => (People.GoalM[person] - People.PositionM[person]).Length() <= reachM;

    /// <summary>
    /// PER-9's draw: somewhere to be. <b>A building and never anything else</b> — a map with no buildings
    /// on it has nowhere for its people to go, and they stand.
    /// </summary>
    /// <remarks>
    /// Both ends are screened when the trip is chosen, with the strict question rather than the
    /// best-effort one: a door has to be walkable-to, or this is a trip that can only end in a leg given up.
    /// A draw that finds nowhere stands and draws again on its own clock.
    /// </remarks>
    void DrawTrip(int person)
    {
        GiveUpTheClaims(person);
        People.Stage[person] = TripStage.StandingBy;
        People.TimerS[person] = People.Draw[person].NextFloat(0f, _config.Person.StandByIdleMaxS);
        People.Manual[person] = false;

        // <b>Standing by is not walking</b>, and the draw below may well find nowhere to go. Left true from
        // the leg that just failed, a body stood here holding no route at all — which is neither of PER-25's
        // two walks — and every clock in the town went on treating it as a walker under way.
        SetWalking(person, false);
        People.ClearRoute(person);

        var buildings = _plan.Buildings;
        if (buildings.Count == 0) return;

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

    /// <summary>The trip drawn: the door claimed, and the walk to it laid.</summary>
    void BeginTrip(int person, int building, Vector2 doorM)
    {
        TripsDrawn++;
        People.DestinationBuilding[person] = building;
        _containers.Claim(building);
        People.Stage[person] = TripStage.WalkingToTheDoor;
        WalkTo(person, doorM);
    }

    /// <summary>
    /// Where a walk to a car would be aimed: beside its door in the bay where it is parked in one, and the ground
    /// off the driver's door where it is not — one at a kerb, one stopped in the road (GEN-4e). Which flank
    /// of the bay that is is the standing the car came to rest in (GEN-4j). <b>Nothing calls it</b>: no
    /// walk is aimed at a car, every trip being walked (PER-11).
    /// </summary>
    Vector2 WayInOf(int car)
    {
        var bay = _parking.BayOf(car);
        if (bay >= 0)
        {
            return _parking.WayInM(bay, BayTemplate.StandsNoseIn(_parking.HeadingRad(bay), Cars.HeadingRad[car]));
        }

        return DriverDoorM(car);
    }

    /// <summary>
    /// The ground off a car's driver door — the flank away from the way the traffic runs, a body's width
    /// clear of the panels. It is where anybody aboard is thrown when the car breaks (PHY-6).
    /// </summary>
    Vector2 DriverDoorM(int car)
    {
        var forward = Heading.Unit(Cars.HeadingRad[car]);
        var door = new Vector2(-forward.Y, forward.X) * -_config.RoadSideSign;
        return Cars.PositionM[car] + door * (Cars.BuildOf(car).FlankM + _config.PersonDiameterM);
    }

    /// <summary>
    /// A bay is claimed only once a route to it exists: the search is asked before the claim, so
    /// an unroutable bay is handed back rather than held for a car that will never arrive.
    /// </summary>
    bool RouteExistsToTheBay(int fromLane, int bay)
    {
        var goals = _driveSearch.Goals;
        var goalCount = BayGoals(bay, goals);
        if (goalCount == 0) return false;

        var entry = _driving.EntryOnLane(fromLane, _roads.LaneLengthM[fromLane]);
        _driveSearch.Entries[0] = entry;
        if (entry.Link == TravelGraph.NoLink) return false;

        for (var slot = 0; slot < goalCount; slot++)
        {
            if (goals[slot].Link == entry.Link) return true;
        }

        return SearchTheDrivingNetwork(1, goalCount, _closedLinks, out var goalSlot) > 0 && goalSlot >= 0;
    }

    /// <summary>
    /// <b>Where a drive leg into a bay ends: the metre of each street lane the bay's mouth stands abeam of.</b>
    /// <b>One goal per lane the bay is worked off</b> (GEN-4f), so which side of the street the leg approaches on
    /// is priced by the search and not decided before it.
    /// </summary>
    /// <remarks>
    /// <b>The metre and not a node.</b> A destination has always been a place on a link (<see cref="RouteGoal"/>),
    /// and the bay's mouth is where the leg stops driving the road — so the search, the price, the reroute and the
    /// line all name that one place. The route's line stops short of it where the car waits for its manoeuvre
    /// (<see cref="StopForTheBayM"/>), and the manoeuvre is what covers the rest of the distance.
    /// </remarks>
    int BayGoals(int bay, Span<RouteGoal> into)
    {
        if (bay < 0) return 0;

        var written = 0;
        foreach (var lane in _bayStreets.LanesOf(bay))
        {
            if (written == into.Length) break;

            var link = _driving.LinkOfLane(lane);
            if (link == TravelGraph.NoLink) continue;

            into[written++] = new RouteGoal(link, _driving.PlaceOfM(lane, _bayStreets.AtLaneM(bay, lane)));
        }

        return written;
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

    /// <summary>One leg: the goal, the line to it, and the clocks that decide whether it is going anywhere.</summary>
    void WalkTo(int person, Vector2 goalM)
    {
        People.GoalM[person] = goalM;
        LayWalk(person, reachTheGoal: true);

        // <b>A walk with no route is not a walk</b> (PER-25). Struck out for the goal regardless, a body
        // whose search came back with nothing walked at a door on the far side of town in a straight line,
        // over whatever lay between it and the carriageway included — and nothing stopped it, the goal
        // getting nearer every tick keeping the give-up clock from ever running up.
        SetWalking(person, People.RouteCount[person] > 0);
        _progress.Restart(person);
    }
}
