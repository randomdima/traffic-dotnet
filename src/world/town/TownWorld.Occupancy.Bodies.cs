using System.Numerics;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The physical layer</b> (TER-4c.2): every body with a collider, on every way whose ribbon that collider
/// stands over, at p0 — read off the atlas at the pose the solver left it in, and nothing worked out about
/// the ground on the way.
/// </summary>
internal sealed partial class TownWorld
{
    /// <summary>What the atlas last found under each car, so one standing still is not read again every tick.</summary>
    readonly RibbonAtlas.Recall _groundUnderCars;

    /// <summary>The same for each walker.</summary>
    readonly RibbonAtlas.Recall _groundUnderWalkers;

    /// <summary>
    /// <b>A car where its collider stands</b>: every way the atlas finds under its box, over the stretch it
    /// covers — whatever the car is doing, whoever is driving it and whether it is broken.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Its own box and never the square round it</b> (<see cref="RibbonAtlas.UnderBox"/>): a car on a
    /// street at an angle to the lattice is on the lanes its shape is over, and a square round it would be
    /// on the lane coming the other way.
    /// </para>
    /// <para>
    /// <b>Where it is travelling a way of its own line it says so</b> (<see cref="LaneClaim.OnItsLine"/>),
    /// with the speed it is doing down it and the way it takes next — which is what makes it a queue to the car
    /// behind, and one making that car's own movement (TER-4c.6). Anywhere else it is standing on the way, going
    /// nowhere down it. <b>And whether it is at rest</b>, which is what a body something may get past is.
    /// </para>
    /// <para>
    /// <b>A car on a bar is laid under the vehicle pulling it</b> (EVA-5): the pair is one movement, so it is
    /// one occupant, and the truck is never held off its own trailer.
    /// </para>
    /// </remarks>
    void LayTheCarsBody(int car)
    {
        var occupant = LaidAs(car);
        ref readonly var build = ref Cars.BuildOf(car);
        var halfM = build.CollisionSizeM * 0.5f;

        var under = _atlas.UnderBox(
            Cars.PositionM[car], Heading.Unit(Cars.HeadingRad[car]), halfM.X, halfM.Y, _groundUnderCars, car);

        var travelling = IsUnderWay(occupant);
        var stopMps = _config.Driving.StopSpeedMps;
        var still = Cars.VelocityMps[car].LengthSquared() <= stopMps * stopMps;

        // <b>Blocked, it goes nowhere</b> (CAR-50): its line runs on, but it can neither get past what stands in
        // front of it nor back up for the room to, so to whoever comes up behind it is a body to get past.
        var blocked = still && Cars.Context[occupant].Blocked;
        for (var at = 0; at < under.Length; at++)
        {
            ref readonly var cover = ref under[at];
            var onward = LaneOccupancy.NoWay;
            var onItsLine = travelling && _ground.IsOnItsLine(occupant, cover.Way, out onward);
            if (blocked) onward = LaneOccupancy.NoWay;
            _occupancy.LayBody(
                cover.Way, cover.FromM, cover.ToM, onItsLine ? Cars.AlongMps[occupant] : 0f, occupant,
                LaneRoster.Driving, onItsLine, onward, still);
        }
    }

    /// <summary>
    /// Which of the town's cars this body's ground is held under — <b>itself, or the vehicle pulling it</b>
    /// (EVA-5).
    /// </summary>
    int LaidAs(int car) => _recovery.OnTheHookOf[car] is var hauler and >= 0 ? hauler : car;

    /// <summary>
    /// <b>A person where its collider stands</b>: every way the atlas finds under its disc — and the way it is
    /// walking, over the stretch its own body takes of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A body in a lane holds the lane</b>, on the paint or off it, walking, knocked down or under a hand:
    /// which ground it stands on is a fact about the ground (TER-4c.2).
    /// </para>
    /// <para>
    /// <b>Its own way is read off its place on it</b> (<see cref="PersonFleet.OnWayM"/>), because a body is
    /// narrower than the lattice is fine and could fall between its points — and the way it walks is the one
    /// way it must never be missing from.
    /// </para>
    /// </remarks>
    void LayTheWalkersBody(int person)
    {
        // PHY-7: inside a container there is no body in the world and nothing in anybody's way.
        if (People.Inside[person].Any) return;

        var occupant = GroundHeldAs(person, out var roster);
        var radiusM = People.RadiusM[person];
        var walking = People.OnWay[person];
        var alongMps = _walkingGround.AlongItsWalkMps(person);
        var onward = walking == PersonFleet.NoWay ? LaneOccupancy.NoWay : _walkingGround.WayOf(People.PeekNextRouteWay(person));

        // A walker has no acceleration (PER-3): it is walking or it has declared nothing, and a casualty declares
        // nothing whatever it is sliding at.
        var still = People.Wounded[person] || People.DeclaredMps[person] == Vector2.Zero;

        var under = _atlas.UnderDisc(People.PositionM[person], radiusM, _groundUnderWalkers, person);
        for (var at = 0; at < under.Length; at++)
        {
            ref readonly var cover = ref under[at];
            var onItsLine = cover.Way == walking;
            _occupancy.LayBody(
                cover.Way, cover.FromM, cover.ToM, onItsLine ? alongMps : 0f, occupant, roster,
                onItsLine, onItsLine ? onward : LaneOccupancy.NoWay, still);
        }

        if (walking == PersonFleet.NoWay) return;

        var atM = People.OnWayM[person];
        _occupancy.LayBody(
            walking, MathF.Max(0f, atM - radiusM), MathF.Min(_ways.LengthM(walking), atM + radiusM), alongMps,
            occupant, roster, onItsLine: true, onward, still);
    }

    /// <summary>
    /// <b>Whose ground a walker's is</b> — its own, <b>or, for an officer out on duty, their car's</b> (SRV-11): the
    /// officer is the car's closure on foot, and the two are one occupant as a coupled pair is (EVA-5). So the
    /// officer's walk to the post is never held off the car it steps out of, which stands beside it on the same lane
    /// — a reservation has no across (TER-4c) — and the car is never held off its own officer.
    /// </summary>
    int GroundHeldAs(int person, out LaneRoster roster)
    {
        roster = LaneRoster.Walking;
        if (People.Stage[person] != TripStage.OnDuty || People.Inside[person].Any) return person;

        for (var car = 0; car < Cars.Count; car++)
        {
            if (_beat.Officer[car] != person) continue;

            roster = LaneRoster.Driving;
            return car;
        }

        return person;
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
            var count = _atlas.UnderDisc(_plan.Props.CentreM[prop], _plan.Props.RadiusM[prop], under);
            for (var at = 0; at < count; at++)
            {
                if (!_ways.IsDriven(under[at].Way)) continue;

                throw new InvalidOperationException(
                    $"prop {prop} at {_plan.Props.CentreM[prop]} stands on way {under[at].Way}, which the traffic drives");
            }
        }
    }
}
