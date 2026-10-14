using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Person.Actions;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The physical layer</b> (TER-4c.2): every body on the ways it stands on, at p0 — a car on the ways under its
/// wheels, a walker on those under its centre, a single wheel — read off the atlas at the pose the solver left it in,
/// and nothing worked out about the ground on the way.
/// </summary>
internal sealed partial class TownWorld
{
    /// <summary>
    /// <b>A car where its wheels stand</b>: every way the atlas finds under any of its four, over the stretch its wheels
    /// hold of it — whatever the car is doing, whoever is driving it and whether it is broken. A car with no wheel on a way
    /// is on the grass, and on no way at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Its wheels and never its box</b>, as the world read them behind its last step (<see cref="WorldView.CoversOfCar"/>):
    /// each way's stretch is where its wheels on it stand, widened by <see cref="RoadFigures.HeldAroundAWheelM"/>.
    /// </para>
    /// <para>
    /// <b>Where it stands and nothing else</b>: the claims say where a body is and never what it is doing (TER-4c.5).
    /// </para>
    /// <para>
    /// <b>A car on a bar is laid under the vehicle pulling it</b> (EVA-5): the pair is one movement, so it is
    /// one occupant, and the truck is never held off its own trailer.
    /// </para>
    /// <para>
    /// <b>Which channels it is on is the world's</b> (PHY-1a): read on its own channels alone, and put on those of the ways
    /// it stands over as it reads them.
    /// </para>
    /// </remarks>
    void LayTheCarsBody(int car) =>
        _occupancy.PlaceBody(car, LaneRoster.Driving, LaidAs(car), LaneRoster.Driving, _world.View.CoversOfCar(car));

    /// <summary>
    /// Which of the town's cars this body's ground is held under — <b>itself, or the vehicle pulling it</b>
    /// (EVA-5).
    /// </summary>
    int LaidAs(int car) => _recovery.OnTheHookOf[car] is var hauler and >= 0 ? hauler : car;

    /// <summary>
    /// <b>A person where its centre stands</b> (TER-4c.2): the ground its body covers (<see cref="WalkingGround.BodyCover"/>),
    /// laid under whoever the town holds that ground as (<see cref="GroundHeldAs"/>).
    /// </summary>
    /// <param name="body">Room for one body's pieces (<see cref="WalkingGround.MostPiecesOfABody"/>), written over.</param>
    void LayTheWalkersBody(int person, Span<WayCover> body)
    {
        var occupant = HoldTheWalkersGround(person, out var roster);

        // PHY-7: inside a container there is no body in the world and nothing in anybody's way.
        if (People.Inside[person].Any)
        {
            _occupancy.LiftBody(person, LaneRoster.Walking);
            return;
        }

        var count = _walkingGround.BodyCover(person, _world.View.CoversOfWalker(person), body);
        _occupancy.PlaceBody(person, LaneRoster.Walking, occupant, roster, body[..count]);
    }

    /// <summary>Whose ground a walker's body is laid as this rebuild (<see cref="GroundHeldAs"/>), held to from here on.</summary>
    int HoldTheWalkersGround(int person, out LaneRoster roster)
    {
        var occupant = GroundHeldAs(person, out roster);
        _walkingGround.HoldTheGroundAs(person, occupant, roster);
        return occupant;
    }

    /// <summary>
    /// <b>Whose ground a walker's is</b> — its own, <b>or, for an officer out on duty, their car's</b> (SRV-11): the
    /// officer is the car's closure on foot, and the two are one occupant as a coupled pair is (EVA-5). So the
    /// officer's walk to the post is never held off the car it steps out of, which stands beside it on the same lane
    /// — a reservation has no across (TER-4c) — and the car is never held off its own officer.
    /// </summary>
    /// <remarks>
    /// <b>And an owner walking to their own car, or away from it once parked, is that car's too</b> (PER-29), for
    /// the same reason: its way in is beside it in its bay (GEN-4e), on the lane the car's own body stands on, and a
    /// walk held off that body never gets to its door or away from it.
    /// </remarks>
    internal int GroundHeldAs(int person, out LaneRoster roster)
    {
        roster = LaneRoster.Walking;
        if (People.Inside[person].Any) return person;

        if (People.Stage[person] is TripStage.WalkingToTheCar or TripStage.WalkingFromTheCar
            && People.Car[person] != PersonFleet.NoCar)
        {
            roster = LaneRoster.Driving;
            return People.Car[person];
        }

        if (People.Stage[person] != TripStage.OnDuty) return person;

        var crewOf = _crewOf[person];
        if (crewOf == NoCar || _beat.Officer[crewOf] != person) return person;

        roster = LaneRoster.Driving;
        return crewOf;
    }

    /// <summary>
    /// <b>The town's furniture stands on no ground the traffic drives</b> (TER-4c.4), and a town that puts a
    /// prop there is refused rather than driven round: a prop is not a body, holds no reservation, and a
    /// driver would be told nothing of it.
    /// </summary>
    void RefuseFurnitureOnTheRoad()
    {
        Span<WayCover> under = stackalloc WayCover[MostWaysUnderABody];
        for (var prop = 0; prop < _plan.Props.Count; prop++)
        {
            var count = Atlas.UnderDisc(_plan.Props.CentreM[prop], _plan.Props.RadiusM[prop], under);
            for (var at = 0; at < count; at++)
            {
                if (!_ways.IsDriven(under[at].Way)) continue;

                throw new InvalidOperationException(
                    $"prop {prop} at {_plan.Props.CentreM[prop]} stands on way {under[at].Way}, which the traffic drives");
            }
        }
    }
}
