using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Statics;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.World.Town;

/// <summary>Standing the plan up as bodies, once, before the first tick.</summary>
internal sealed partial class TownWorld
{
    /// <summary>
    /// The town's immovable geometry: every prop a static circle, every building the static boxes its
    /// roof is built of. A city's ninety-odd thousand props are real collision geometry — a walker that
    /// could walk through a tree is a walker the ground is not actually holding.
    /// </summary>
    /// <remarks>
    /// <b>OBJ-5a — a building is collided as the rectangles its picture is drawn of and not as the box
    /// it was drawn in.</b> An L, a courtyard and a cut corner are all the same defect otherwise: metres
    /// of empty box that stop a car in the open, and a driver who cannot see why. Nothing here is priced
    /// per tick — statics are never integrated and per-tick work is linear in the moving roster alone
    /// (SOL-22) — so the two or three boxes a building costs are paid once, when the map is opened.
    /// </remarks>
    void StandStatics()
    {
        for (var prop = 0; prop < _plan.Props.Count; prop++)
        {
            _physics.AddStaticDisc(_plan.Props.CentreM[prop], _plan.Props.RadiusM[prop]);
        }

        for (var building = 0; building < _plan.Buildings.Count; building++)
        {
            var centreM = _plan.Buildings.CentreM[building];
            var roof = BuildingRoofs.Of(_plan, BuildingCatalog.Shared, _uses, building);
            ref readonly var variant = ref BuildingCatalog.Shared.Variants[roof.Variant];

            if (variant.PartsM.Length == 0)
            {
                _physics.AddStaticBox(centreM, roof.FootprintM, roof.HeadingRad);
                continue;
            }

            // The parts are authored against the picture's own footprint, so a civic roof fitted to a
            // smaller plot carries its walls in with it rather than standing them at the size they were
            // painted.
            var scale = roof.FootprintM / variant.FootprintM;
            Heading.Frame(roof.HeadingRad, out var forward, out var right);

            foreach (var part in variant.PartsM)
            {
                var atM = part.AtM * scale;
                _physics.AddStaticBox(
                    centreM + (forward * atM.X) + (right * atM.Y), part.SizeM * scale, roof.HeadingRad);
            }
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
                StandCar(spawn, LookOf(fleet++));
                continue;
            }

            if (_plan.Spawns.Kind[spawn] != SpawnKindPerson) continue;

            var positionM = _plan.Spawns.PositionM[spawn];
            var body = _physics.AddPerson(positionM);
            // Round the walkers and never past them, as the fleet's own wrap does: the uniforms share
            // the sheet list with them (<see cref="PersonCatalog.Count"/>), and nobody wears one who was
            // not named to (SRV-3a).
            var person = People.Add(
                body, positionM, _plan.Spawns.HeadingRad[spawn], _physics.MassOf(body), _config.PersonDiameterM * 0.5f,
                (byte)(variants % PersonCatalog.Shared.Count), new Rng(_agentSeed, (ulong)spawn),
                PersonFleet.DrawsReckless(_agentSeed, (ulong)spawn, _config.Driving.RecklessShare));
            variants++;
            if (People.Reckless[person]) RecklessDrivers++;

            _physics.Tag(body, new BodyTag(BodyKind.Person, person));
            _progress.Restart(person);
            MoveIn(person, positionM);
        }

        if (IdlePlan.StandsConvoys(_plan.Name)) StandTheEscort();
    }

    /// <summary>
    /// <b>The escort, as an escort</b>: its beacons up, its pace held under the pace the car between them
    /// keeps on the ring (<see cref="IdlePlan.EscortPaceShare"/>), and the three of them following at half
    /// the interval traffic keeps (<see cref="IdlePlan.ConvoyFollowingShare"/>). Nothing else on the idle
    /// map is arranged — they drive under the standing rules like any other traffic, and what makes them
    /// one convoy is that the leading car can no longer run away from what it is leading.
    /// </summary>
    /// <remarks>
    /// <b>The pace is the escorted car's own and not a figure of the map's.</b> It is what that build's
    /// grip affords on the ring's tightest corner, or its gear's cap where the corner is wide enough for
    /// that to bind first — so a heavier charge, a different look or a rounder loop all move the convoy
    /// together rather than leaving a number behind that used to be true.
    /// </remarks>
    void StandTheEscort()
    {
        if (Cars.Count < IdlePlan.ConvoyCars) return;

        ref readonly var escorted = ref Cars.BuildOf(IdlePlan.Escorted);
        var paceMps = IdlePlan.EscortPaceShare * MathF.Min(
            escorted.MaxSpeedMps,
            CarFollower.CornerMps(1f / IdlePlan.CornerRadiusM(_config), escorted.GripMps2 * _config.Driving.GripMargin));

        for (var car = 0; car < IdlePlan.ConvoyCars; car++)
        {
            // The gap is kept by whoever is behind, so it is the whole convoy that keeps a short one —
            // including the car being escorted, which is following the police in front of it.
            Cars.FollowingShare[car] = IdlePlan.ConvoyFollowingShare;
            if (car == IdlePlan.Escorted) continue;

            Cars.BlueLight[car] = true;
            Cars.PaceMps[car] = paceMps;
        }
    }

    /// <summary>
    /// Which look a car the map put down wears: <b>the map's own where the map names one, and otherwise
    /// the fleet's wrap.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The wrap goes round the fleet and never past it</b> (<see cref="CarCatalog.Count"/>): the service
    /// vehicles share the sheet list with it, and the traffic a town draws for itself is drawn from the
    /// fleet alone.
    /// </para>
    /// <para>
    /// <b>A map names looks when the looks are the point of the map.</b> The exam stands one of them
    /// (<see cref="ExamPlan.StandsOneLook"/>), because every card is a crossing read against another card
    /// and a fleet of different weights would be a second variable inside every comparison; the idle ring
    /// stands an escort and one car passing it (<see cref="IdlePlan"/>), because that is the whole of what
    /// there is to look at on it.
    /// </para>
    /// </remarks>
    byte LookOf(int car)
    {
        if (ExamPlan.StandsOneLook(_plan.Name)) return (byte)CarCatalog.Shared.Plain;

        if (IdlePlan.StandsConvoys(_plan.Name))
        {
            return (byte)(IdlePlan.PartOf(car) switch
            {
                IdlePart.Armoured => CarCatalog.Shared.Armoured,
                IdlePart.Sports => CarCatalog.Shared.Sports,
                _ => CarCatalog.Shared.Police,
            });
        }

        return (byte)(car % CarCatalog.Shared.Count);
    }

    /// <summary>
    /// <b>A person the map put down at a door starts inside it</b> (GEN-7), dwelling out the interval an
    /// arrival dwells: the town's first tick is somebody's morning and not the moment they were all put
    /// on the pavement.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It closes the loop rather than adding a stage to it.</b> A trip ends by walking through a door
    /// and dwelling (PER-11), so a body that begins there begins in the state every later trip returns it
    /// to, and everything after the first dwell is the ordinary round — out of the building, to a car if
    /// the trip is worth one, to the destination, in. Started on the pavement instead, everybody's first
    /// leg was a leg no rule of theirs had drawn.
    /// </para>
    /// <para>
    /// <b>The dwell is drawn per person</b>, so a street's worth of doors does not open on the same tick;
    /// and a door with no room behind it leaves the body standing outside it, which is the state
    /// <see cref="TripStage.StandingBy"/> already names.
    /// </para>
    /// </remarks>
    void MoveIn(int person, Vector2 positionM)
    {
        var building = BuildingWithADoorAt(positionM);
        if (building < 0 || !_containers.TryAdmit(building, person)) return;

        Contain(person);
        People.Stage[person] = TripStage.Dwelling;
        People.TimerS[person] = People.Draw[person].NextFloat(_config.Building.DwellMinS, _config.Building.DwellMaxS);
    }

    /// <summary>
    /// The building whose way in this body is standing at, or −1 — the nearest one within touching reach
    /// of the door, which on a street of terraces is the difference between somebody's own house and
    /// their neighbour's. <b>Read off the pose the map left it in</b>, as the reeling and the pacing
    /// walkers are (PER-16): a map says somebody lives here by standing them at the door, and nothing in
    /// the format has to name it.
    /// </summary>
    int BuildingWithADoorAt(Vector2 positionM)
    {
        var buildings = _plan.Buildings;
        var best = -1;
        var bestM = _config.WayInTouchingReachM * _config.WayInTouchingReachM;
        for (var building = 0; building < buildings.Count; building++)
        {
            var first = buildings.EntryOffsets[building];
            var last = buildings.EntryOffsets[building + 1];
            for (var entry = first; entry < last; entry++)
            {
                var farM = (buildings.EntryPointM[entry] - positionM).LengthSquared();
                if (farM > bestM) continue;

                best = building;
                bestM = farM;
            }
        }

        return best;
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
            // A car whose wheel is already held over has somebody deciding for it, which is the whole of
            // what this rule exists to supply. Handing it a lane as well would lay a line nothing drives.
            if (Cars.Driven[car] || Cars.Broken[car] || WheelIsHeldOver(car)) continue;
            if (TakeTheLaneUnderIt(car)) Cars.Driven[car] = true;
        }
    }

    /// <summary>
    /// <b>A map that drives its own cars by holding their wheels over</b>: every car on the skidpad is put
    /// on the lock its column stands and the pedal its row asks for, and holds both for the whole run
    /// (<see cref="CityGen.SkidpadPlan"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the same substitution a hand at the wheel makes</b> (CTL-5) and is read through the same
    /// seam: no manoeuvre is selected, no soft rule is consulted, and the hard envelope — the gear's cap,
    /// the rack's travel, the friction ellipse — binds exactly as it does under anybody's hand. What the
    /// pad measures is worth nothing if the car it measures is not the car the town drives.
    /// </para>
    /// <para>
    /// <b>Laid once, because a held wheel is not a decision.</b> The command never changes, so there is
    /// nothing here for the tick to do: the seam reads the same figures every frame and the pad costs the
    /// loop an array lookup.
    /// </para>
    /// </remarks>
    void HoldTheWheels()
    {
        if (!SkidpadPlan.HoldsItsCarsWheels(_plan.Name)) return;

        // The pad's own cars and no others: a car this map never laid a square for is not one it has a
        // pedal to hold, and it is left to the rule that drives an empty map.
        _wheelHeld = new HandInput[Cars.Capacity];
        for (var car = 0; car < Math.Min(Cars.Count, SkidpadPlan.Cars); car++)
        {
            _wheelHeld[car] = new HandInput(
                Held: true, Throttle: SkidpadPlan.PedalOf(SkidpadPlan.RunOf(car)), Steer: SkidpadPlan.LockedLeft,
                Handbrake: false, WalkDirection: Vector2.Zero);
        }
    }

    /// <summary>
    /// Whether the map is holding this car's wheel over rather than anybody in it deciding. <b>Public
    /// because a picture of such a car has to say so</b>: nothing is choosing for it and it is not parked
    /// either, and those are the two states everything that names a car's behaviour otherwise has.
    /// </summary>
    public bool WheelIsHeldOver(int car) => _wheelHeld is not null && _wheelHeld[car].Held;

    /// <summary>
    /// A car starts stopped in a parking space, in the pose that space stands its cars at, with nobody in
    /// it — an inert dynamic object that can be pushed and takes no action of its own, holding its
    /// handbrake.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Its bay is the registry's from the first tick, so a car is claimed out of a bay rather than found
    /// on a road: every metre this town's traffic covers is somebody's trip.
    /// </para>
    /// <para>
    /// <b>And the pose is the bay's own and not the plan's</b> (GEN-4i), for a spawn that lands in one: the
    /// bay's ways meet at the pose a car square in the middle of the space stands at, and a car standing off
    /// the way it is about to drive is a car whose first move out of the car park is a recovery. <b>Which of
    /// the two poses that is is the driver's habit</b> (GEN-4j), so a town starts with cars standing both
    /// ways round wherever its bays lay both.
    /// </para>
    /// </remarks>
    void StandCar(int spawn, byte variant)
    {
        var positionM = _plan.Spawns.PositionM[spawn];
        var headingRad = _plan.Spawns.HeadingRad[spawn];

        // The habit before the pose, because the pose a car starts standing in is what its habit would have
        // put it in — drawn from the car's own stream, which then goes on to the fleet as it stands.
        var draw = new Rng(_agentSeed, (ulong)(spawn + 1) << 8);
        var backsIn = draw.NextFloat() < _config.Driving.BacksIntoBaysShare;

        // <b>A space that belongs to an apron is not the town's to spawn into</b> (GEN-4k), and neither is
        // one another spawn has already been put in: the car is stood in the nearest free bay instead, and
        // a car with nowhere near to stand is not stood at all rather than dropped on top of whatever has
        // the place. It is the one thing an apron costs the plan, and it costs it a car at most.
        var bay = BayUnder(positionM);
        if (bay >= 0 && !_parking.IsFree(bay))
        {
            bay = FreeBayNear(positionM, _config.PersonWalkWorthM);
            if (bay < 0) return;
        }

        if (bay >= 0)
        {
            headingRad = BayTemplate.StandingHeadingRad(
                _parking.HeadingRad(bay), _bayWays.TheStandingOnOffer(bay, !backsIn));

            positionM = _parking.CentreM(bay);
        }

        // <b>The body is this variant's own</b> (CAR-11): its footprint, its weight, its axles and what its
        // tyres are worth. The town's geometry is still the nominal car's — the junctions, the lanes and
        // the bays were sized against it and the road is the same road whoever turns up — but nothing a
        // car decides for itself is taken from it any more.
        ref readonly var build = ref _builds.Of(variant);
        var body = _physics.AddCar(
            positionM, headingRad, build.CollisionSizeM * 0.5f, build.CornerRadiusM, build.MassKg);
        var car = Cars.Add(body, positionM, headingRad, variant, backsIn, draw);
        _physics.Tag(body, new BodyTag(BodyKind.Car, car));

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
