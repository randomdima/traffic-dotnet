using System.Numerics;
using TrafficSimulation.Agents.Person.Actions;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Containment;
using TrafficSimulation.World.Foot;

namespace TrafficSimulation.World.Town;

/// <summary>The one leg of a trip that is not walking: a door gone through, and the containment that hides a body while it is behind one.</summary>
internal sealed partial class TownWorld
{
    /// <summary>
    /// OBJ-5: the door. Capacity is checked here, atomically — the claim held during the walk was
    /// advisory and only kept the crowd down.
    /// </summary>
    void EnterTheBuilding(int person)
    {
        var building = People.DestinationBuilding[person];
        if (building < 0)
        {
            // An order to a point: arriving is the end of it, and what follows is the next order or the
            // next trip.
            if (People.Manual[person]) People.Stage[person] = TripStage.UnderOrders;
            else DrawTrip(person);
            return;
        }

        if (!_containers.TryAdmit(building, person))
        {
            if (People.Stage[person] != TripStage.WaitingForAPlace)
            {
                DoorsFoundFull++;
                People.Stage[person] = TripStage.WaitingForAPlace;
                People.TimerS[person] = _config.PersonPlacePatienceS;
            }

            return;
        }

        BuildingsEntered++;
        _containers.GiveUpClaim(building);
        People.DestinationBuilding[person] = PersonFleet.NoBuilding;
        Contain(person);
        People.Stage[person] = TripStage.Dwelling;
        People.TimerS[person] = DrawADwellS(person);
    }

    /// <summary>
    /// <b>One stay inside</b> (PER-11): between the shortest dwell and the longest, leaning to the short end
    /// (<see cref="Core.Config.BuildingFigures.DwellSkew"/>), drawn from the person's own stream.
    /// </summary>
    float DrawADwellS(int person)
    {
        var building = _config.Building;
        var share = MathF.Pow(People.Draw[person].NextFloat(), building.DwellSkew);
        return building.DwellMinS + ((building.DwellMaxS - building.DwellMinS) * share);
    }

    /// <summary>PHY-7a: the building places its occupant outside, and refuses while there is nowhere to put them.</summary>
    bool TryLeaveTheBuilding(int person)
    {
        var where = _containers.WhereIs(person);
        if (where.Kind != ContainerKind.Building) return true;

        var building = where.Index;
        var centreM = _plan.Buildings.CentreM[building];
        var (door, free) = DoorExitOf(building, centreM);
        if (!TryPutDownOff(door, free, beside: -1, out var spotM, out var spot)) return false;

        _containers.LeaveBuilding(building, person);
        Place(person, spotM, MathF.Atan2(spotM.Y - centreM.Y, spotM.X - centreM.X), spot);
        return true;
    }

    /// <summary>
    /// Which of a building's ways in this trip uses. Settled when the trip is drawn and never
    /// afterwards: a door chosen again from where the body has got to re-plans at every swing.
    /// </summary>
    Vector2 DoorOf(int building, Vector2 fromM) =>
        EntryOf(building, fromM) is var entry and >= 0 ? _plan.Buildings.EntryPointM[entry] : _plan.Buildings.CentreM[building];

    /// <summary>Which of a building's ways in is nearest a place (<see cref="DoorOf"/>), or −1 for a building with none.</summary>
    int EntryOf(int building, Vector2 fromM)
    {
        var buildings = _plan.Buildings;
        var first = buildings.EntryOffsets[building];
        var last = buildings.EntryOffsets[building + 1];
        if (last <= first) return -1;

        var best = first;
        var bestDistanceSq = float.MaxValue;
        for (var entry = first; entry < last; entry++)
        {
            var distanceSq = (buildings.EntryPointM[entry] - fromM).LengthSquared();
            if (distanceSq >= bestDistanceSq) continue;

            best = entry;
            bestDistanceSq = distanceSq;
        }

        return best;
    }

    /// <summary>Into a container: the body leaves the world and everything the follower held for it is dropped.</summary>
    void Contain(int person)
    {
        People.Walking[person] = false;
        Enter(person, PersonAction.Inside);
        People.ClearRoute(person);
        ContainTheWalker(person);
    }

    /// <summary>And back out, where the container put them down — the one place a walker's pose is written by anything but the solver.</summary>
    /// <param name="place">The pavement the container stands beside, which the body's next walk is laid from.</param>
    void Place(int person, Vector2 atM, float headingRad, in FootPlace place)
    {
        People.DestinationM[person] = atM;
        People.GoalM[person] = atM;
        _placeOf[person] = place;
        _goalPlace[person] = place;
        SetWalking(person, false);
        People.ClearRoute(person);
        _progress.Restart(person);
        PlaceTheWalker(person, atM, headingRad);
    }

    /// <summary>
    /// The order pins the goal the behaviour would otherwise have picked, and nothing under it changes.
    /// What the pointer was over decides which goal that is — a building is walked to <em>and entered</em>,
    /// and ground is walked to and then stood on.
    /// </summary>
    /// <remarks>
    /// The containment check binds unchanged: the door is still asked at the door, so an ordered walker
    /// can find a building full.
    /// </remarks>
    void TakeTheOrder(int person, Vector2 toM)
    {
        GiveUpTheClaims(person);
        People.Manual[person] = true;

        // Contained when the order arrives: it is taken up the moment the container puts the body down,
        // which is what the dwell's own exit already does.
        if (People.Inside[person].Any) return;

        var building = BuildingAt(toM);
        if (building >= 0)
        {
            People.DestinationBuilding[person] = building;
            _containers.Claim(building);
            People.Stage[person] = TripStage.WalkingToTheDoor;
            WalkTo(person, DoorOf(building, People.PositionM[person]), DoorPlaceOf(building, People.PositionM[person]));
            return;
        }

        // A player's order may look the ground up (the root decision log, 2026-10-13).
        People.Stage[person] = TripStage.UnderOrders;
        WalkTo(person, toM, _walking.PlaceNearest(toM));
    }

    /// <summary>The building a point falls inside, or −1 — what a right-click on one reads as.</summary>
    public int BuildingAt(Vector2 pointM)
    {
        var buildings = _plan.Buildings;
        for (var building = 0; building < buildings.Count; building++)
        {
            var headingRad = buildings.HeadingRad[building];
            var forward = Heading.Unit(headingRad);
            var offset = pointM - buildings.CentreM[building];
            var halfM = buildings.SizeM[building] * 0.5f;
            var along = Vector2.Dot(offset, forward);
            var across = Vector2.Dot(offset, new Vector2(-forward.Y, forward.X));
            if (MathF.Abs(along) <= halfM.X && MathF.Abs(across) <= halfM.Y) return building;
        }

        return -1;
    }

    /// <summary>The trip failed. Every claim is released and a fresh one is drawn from where the body actually is.</summary>
    void GiveUpTheTrip(int person)
    {
        TripsGivenUp++;
        DrawTrip(person);
    }

    /// <summary>What a trip holds on the town's behalf, which is one thing: the building's claim.</summary>
    void GiveUpTheClaims(int person)
    {
        var building = People.DestinationBuilding[person];
        if (building >= 0) _containers.GiveUpClaim(building);

        People.DestinationBuilding[person] = PersonFleet.NoBuilding;
    }
}
