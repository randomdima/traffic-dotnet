using TrafficSimulation.Agents.Person.Actions;
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
    /// <para>
    /// <b>And it is put on the channels of the ways it stands over</b> (PHY-1a), every one of them: read on its own
    /// channels alone, so a car on a bridge never finds the road under it, and a car crossing a bridgehead finds the
    /// ways of both levels there and is on both until it is clear of one. Over none it keeps what it had.
    /// </para>
    /// </remarks>
    void LayTheCarsBody(int car)
    {
        var occupant = LaidAs(car);
        ref readonly var build = ref Cars.BuildOf(car);
        var halfM = build.CollisionSizeM * 0.5f;

        var under = _atlas.UnderBox(
            Cars.PositionM[car], Heading.Unit(Cars.HeadingRad[car]), halfM.X, halfM.Y, _groundUnderCars, car, Cars.Channels[car]);

        var travelling = IsUnderWay(occupant);
        var stopMps = _config.Driving.StopSpeedMps;
        var still = Cars.VelocityMps[car].LengthSquared() <= stopMps * stopMps;

        // <b>Blocked, it goes nowhere</b> (CAR-50): its line runs on, but it can neither get past what stands in
        // front of it nor back up for the room to, so to whoever comes up behind it is a body to get past.
        var blocked = still && Cars.Context[occupant].Blocked;
        byte channels = 0;
        for (var at = 0; at < under.Length; at++)
        {
            ref readonly var cover = ref under[at];
            channels |= _atlas.ChannelsOf(cover.Way);
            var onward = LaneOccupancy.NoWay;
            var onItsLine = travelling && _ground.IsOnItsLine(occupant, cover.Way, out onward);
            if (blocked) onward = LaneOccupancy.NoWay;
            _occupancy.LayBody(
                cover.Way, cover.FromM, cover.ToM, onItsLine ? Cars.AlongMps[occupant] : 0f, occupant,
                LaneRoster.Driving, onItsLine, onward, still);
        }

        if (channels != 0 && channels != Cars.Channels[car]) PutOnChannels(car, channels);
    }

    /// <summary>
    /// Which of the town's cars this body's ground is held under — <b>itself, or the vehicle pulling it</b>
    /// (EVA-5).
    /// </summary>
    int LaidAs(int car) => _recovery.OnTheHookOf[car] is var hauler and >= 0 ? hauler : car;

    /// <summary>
    /// <b>A person where its collider stands</b> (TER-4c.2): the ground its body covers (<see cref="WalkingGround.BodyCover"/>),
    /// laid under whoever the town holds that ground as (<see cref="GroundHeldAs"/>).
    /// </summary>
    /// <param name="body">Room for one body's pieces (<see cref="WalkingGround.MostPiecesOfABody"/>), written over.</param>
    void LayTheWalkersBody(int person, Span<BodyPiece> body)
    {
        // PHY-7: inside a container there is no body in the world and nothing in anybody's way.
        if (People.Inside[person].Any) return;

        var occupant = GroundHeldAs(person, out var roster);
        _walkingGround.HoldTheGroundAs(person, occupant, roster);
        var count = _walkingGround.BodyCover(person, _groundUnderWalkers, body);
        foreach (ref readonly var piece in body[..count])
        {
            _occupancy.LayBody(
                piece.Way, piece.FromM, piece.ToM, piece.AlongMps, occupant, roster, piece.OnItsLine, piece.Onward, piece.Still);
        }
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
