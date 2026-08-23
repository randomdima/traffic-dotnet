using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.CityGen;
using TrafficSimulation.World.Physics;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.World.Town;

/// <summary>Standing the plan up as bodies, once, before the first tick.</summary>
internal sealed partial class TownWorld
{
    /// <summary>
    /// The town's immovable geometry: every prop a static circle, every building a static box. A city's
    /// ninety-odd thousand props are real collision geometry — a walker that could walk through a tree
    /// is a walker the ground is not actually holding.
    /// </summary>
    void StandStatics()
    {
        for (var prop = 0; prop < _plan.Props.Count; prop++)
        {
            _physics.AddStaticCircle(_plan.Props.CentreM[prop], _plan.Props.RadiusM[prop]);
        }

        for (var building = 0; building < _plan.Buildings.Count; building++)
        {
            _physics.AddStaticBox(_plan.Buildings.CentreM[building], _plan.Buildings.SizeM[building], _plan.Buildings.HeadingRad[building]);
        }

        _physics.SettleStatics();
    }

    void Spawn()
    {
        var variants = 0;
        var fleet = 0;
        for (var spawn = 0; spawn < _plan.Spawns.Count; spawn++)
        {
            if (_plan.Spawns.Kind[spawn] == SpawnKindCar)
            {
                StandCar(spawn, (byte)fleet++);
                continue;
            }

            if (_plan.Spawns.Kind[spawn] != SpawnKindPerson) continue;

            var positionM = _plan.Spawns.PositionM[spawn];
            var body = _physics.AddPerson(positionM);
            var person = People.Add(
                body, positionM, _plan.Spawns.HeadingRad[spawn], _physics.MassOf(body), _config.PersonDiameterM * 0.5f,
                (byte)variants, new Rng(_agentSeed, (ulong)spawn));
            variants++;
            _physics.Tag(body, new BodyTag(BodyKind.Person, person));
            _progress.Restart(person);
        }
    }

    /// <summary>
    /// <b>A map with nowhere to be on it drives its own cars.</b> CAR-1 makes every metre of a town's
    /// traffic somebody's trip, and a car with nobody in it therefore does nothing — but a map with no
    /// building to go to and no bay to be claimed out of has no trips for that rule to be about, and its
    /// cars would stand where they were put for ever.
    /// </summary>
    /// <remarks>
    /// <b>It is a fact about the map and not a name in a list.</b> The proving ground is the only thing
    /// this fires on today (<see cref="CityGen.TrackPlan"/>), and it fires on it because there is nothing
    /// to go to there rather than because of what it is called — the people standing beside its road are
    /// not going anywhere either, which is why they are not asked about. Each car takes the lane it is
    /// standing on, exactly as `E-8` puts a recovered one back on the road, and drives from there under the
    /// standing rules — no destination, so it is carried by the tour, which on a closed circuit is a lap.
    /// </remarks>
    /// <remarks>
    /// <b>It is a standing rule and not something done once.</b> A leg ends by the car being stood down —
    /// parked, settled for where it got to, or abandoned — and on a town that is what hands the car back to
    /// whoever will draw the next trip in it. Here there is nobody to draw one, so a car the ladder stood
    /// down would stand there for the rest of the run: the rule that put it on the road is the same rule
    /// that puts it back on it.
    /// </remarks>
    void DriveTheEmptyMap()
    {
        if (_plan.Buildings.Count > 0 || _plan.ParkingLots.SpaceCount > 0) return;

        for (var car = 0; car < Cars.Count; car++)
        {
            if (Cars.Driven[car] || Cars.Broken[car]) continue;
            if (TakeTheLaneUnderIt(car)) Cars.Driven[car] = true;
        }
    }

    /// <summary>
    /// A car starts stopped in a parking space, in the pose the plan parked it in, with nobody in it —
    /// an inert dynamic object that can be pushed and takes no action of its own, holding its handbrake.
    /// </summary>
    /// <remarks>
    /// Its bay is the registry's from the first tick, so a car is claimed out of a bay rather than found
    /// on a road: every metre this town's traffic covers is somebody's trip.
    /// </remarks>
    void StandCar(int spawn, byte variant)
    {
        var positionM = _plan.Spawns.PositionM[spawn];
        var headingRad = _plan.Spawns.HeadingRad[spawn];
        var body = _physics.AddCar(positionM, headingRad);
        // The nominal footprint, wheelbase and track, and this variant's own drivetrain. Which end a car
        // drives through is the one per-variant figure that is not a dimension — the town's geometry is
        // sized against the nominal car and would have to be re-sized to take any of the others — and the
        // tyre model has always been documented as spending the variant's.
        var car = Cars.Add(
            body, positionM, headingRad, _physics.MassOf(body), variant,
            CarCatalog.Shared.DrivenFrontShareOf(variant), new Rng(_agentSeed, (ulong)(spawn + 1) << 8));
        _physics.Tag(body, new BodyTag(BodyKind.Car, car));

        var bay = BayUnder(positionM);
        if (bay >= 0) _parking.Occupy(bay, car);
    }

    /// <summary>
    /// The bay a spawned car is standing in, or −1. Read off the pose rather than assumed: a car that
    /// stands anywhere else is one this town treats as parked at a kerb.
    /// </summary>
    int BayUnder(Vector2 positionM)
    {
        for (var bay = 0; bay < _parking.BayCount; bay++)
        {
            if ((_parking.CentreM(bay) - positionM).LengthSquared() <= BayFitM * BayFitM) return bay;
        }

        return -1;
    }

    /// <summary>How near a car's own pose has to stand to a bay's for it to be the car in that bay. Half a car's width.</summary>
    float BayFitM => _config.Car.WidthM * 0.5f;

    /// <summary>
    /// How far past a lane's own half-width a car is allowed to be before it has lost its line rather
    /// than merely be crabbing across it — two of them, so a car cornering hard is not stopped for
    /// cornering hard.
    /// </summary>
    const float OffLineTolerance = 2f;
}
