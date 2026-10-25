using System.Numerics;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.CityGen;
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
    /// could walk through a tree is a walker the ground is not actually holding. <b>Its scenery is not</b>
    /// (<see cref="CityPlan.Scenery"/>): drawn, and standing nothing, on ground no walker or car is laid across.
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
            Statics.AddDisc(_plan.Props.CentreM[prop], _plan.Props.BodyRadiusM[prop]);
        }

        for (var building = 0; building < _plan.Buildings.Count; building++)
        {
            var centreM = _plan.Buildings.CentreM[building];
            var roof = BuildingRoofs.Of(_plan, BuildingCatalog.Shared, _uses, building);
            ref readonly var variant = ref BuildingCatalog.Shared.Variants[roof.Variant];

            if (variant.PartsM.Length == 0)
            {
                var fittedM = roof.FootprintM / variant.FootprintM;
                Statics.AddBox(
                    centreM, roof.FootprintM, roof.HeadingRad, variant.CornerRadiusM * MathF.Min(fittedM.X, fittedM.Y));
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
                Statics.AddBox(centreM + (forward * atM.X) + (right * atM.Y), part.SizeM * scale, roof.HeadingRad);
            }
        }
    }

    void Spawn()
    {
        var doors = new Doors(_plan.Buildings, _config.Grid.Main);
        var variants = 0;
        var fleet = 0;
        for (var spawn = 0; spawn < _plan.Spawns.Count; spawn++)
        {
            if (_plan.Spawns.Kind[spawn] == SpawnKindCar)
            {
                // The showroom's places past the last look stand nobody, so no look is shown twice.
                if (ShowroomPlan.ShowsEachLookOnce(_plan.Name) && fleet >= CarCatalog.Shared.SheetCount) continue;

                StandCar(spawn, LookOf(fleet++));
                continue;
            }

            if (_plan.Spawns.Kind[spawn] != SpawnKindPerson) continue;

            var positionM = _plan.Spawns.PositionM[spawn];
            // Round the walkers and never past them, as the fleet's own wrap does: the uniforms share
            // the sheet list with them (<see cref="PersonCatalog.Count"/>), and nobody wears one who was
            // not named to (SRV-3a).
            var person = People.Add(
                positionM, _plan.Spawns.HeadingRad[spawn], _config.Person.MassKg, _config.PersonRadiusM,
                (byte)(variants % PersonCatalog.Shared.Count), new Rng(_agentSeed, (ulong)spawn),
                PersonFleet.DrawsReckless(_agentSeed, (ulong)spawn, _config.Driving.RecklessShare));
            variants++;

            // Where on the pavement the plan stood it, found while the map is built (the root decision log, 2026-10-13).
            _placeOf[person] = _walking.PlaceNearest(positionM);
            _progress.Restart(person);
            MoveIn(person, positionM, doors);
            if (People.Inside[person].Any) HandThemTheirCar(person, spawn, positionM);
        }

        if (IdlePlan.StandsConvoys(_plan.Name)) StandTheEscort();
    }

    /// <summary>
    /// <b>The escort, as an escort</b>: its beacons up, and its pace held under the pace the car between them
    /// keeps on the ring (<see cref="IdlePlan.EscortPaceShare"/>). Nothing else on the idle map is arranged —
    /// they drive under the standing rules like any other traffic, and what makes them one convoy is that the
    /// leading car can no longer run away from what it is leading.
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
    /// <b>A map names looks when the looks are the point of the map.</b> The idle ring stands an escort and
    /// one car passing it (<see cref="IdlePlan"/>), because that is the whole of what there is to look at
    /// on it, and the showroom every look once, the service vehicles' too (<see cref="ShowroomPlan"/>) — each a
    /// plain car in its paint, since no building stood it (<see cref="IsAServiceVehicle"/>).
    /// </para>
    /// </remarks>
    byte LookOf(int car)
    {
        if (ShowroomPlan.ShowsEachLookOnce(_plan.Name)) return (byte)car;

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
    /// <b>A person the map put down at a door starts inside it</b> (GEN-7), part way through a dwell: the
    /// town's first tick is somebody's morning and not the moment they were all put on the pavement.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It closes the loop rather than adding a stage to it.</b> A trip ends by walking through a door
    /// and dwelling (PER-11), so a body that begins there begins in the state every later trip returns it
    /// to, and everything after the first dwell is the ordinary round — out of the building, a walk to the
    /// destination, in (PER-11). Started on the pavement instead, everybody's first
    /// leg was a leg no rule of theirs had drawn.
    /// </para>
    /// <para>
    /// <b>What is left of the dwell is drawn per person, a dwell and a point somewhere inside it</b>: the first tick
    /// catches everybody part way through one, so a street's worth of doors does not open on the same tick and
    /// the town does not stand empty for the shortest dwell. A door with no room behind it leaves the body
    /// standing outside it, which is the state <see cref="TripStage.StandingBy"/> already names.
    /// </para>
    /// </remarks>
    void MoveIn(int person, Vector2 positionM, Doors doors)
    {
        var building = doors.BuildingWithADoorAt(positionM, _config.WayInTouchingReachM);
        if (building < 0 || !_containers.TryAdmit(building, person)) return;

        Contain(person);
        People.Stage[person] = TripStage.Dwelling;
        People.TimerS[person] = DrawADwellS(person) * People.Draw[person].NextFloat();
    }

    /// <summary>
    /// <b>Everybody who lives here owns a car, parked in the free bay nearest their door</b> (PER-29) — free once
    /// the services' aprons are held (GEN-4k) and the neighbours stood before them have theirs. Nobody is handed
    /// one further off than anybody walks (<see cref="Core.Config.SimConfig.PersonWalkReachM"/>): a person with no bay that
    /// near owns none, and walks.
    /// </summary>
    void HandThemTheirCar(int person, int spawn, Vector2 doorM)
    {
        var bay = FreeBayNear(doorM, _config.PersonWalkReachM);
        if (bay < 0) return;

        var car = StandCarIn(bay, LookOf(Cars.Count), CarDraw(spawn));
        Cars.Owner[car] = person;
        People.Car[person] = car;
    }

    /// <summary>
    /// The owners laid at their wheel (<see cref="Core.Config.PersonFigures.StartsAtTheWheelShare"/>). <b>After the
    /// world</b>: a trip drawn at a wheel is drawn from the ways the car stands on, which the world reads.
    /// </summary>
    void SeatTheOwners()
    {
        if (_config.Person.StartsAtTheWheelShare <= 0f) return;

        for (var person = 0; person < People.Count; person++)
        {
            if (StartsAtTheWheel(person)) SeatAtTheWheel(person);
        }
    }

    /// <summary>Whether an owner is one of those laid at their wheel.</summary>
    /// <remarks>Drawn only where the share is set: a draw taken at none would move every later draw of the person's stream.</remarks>
    bool StartsAtTheWheel(int person) =>
        People.Car[person] != PersonFleet.NoCar
        && _config.Person.StartsAtTheWheelShare > 0f
        && People.Draw[person].NextFloat() < _config.Person.StartsAtTheWheelShare;

    /// <summary>
    /// An owner laid out of their dwell and in at their own wheel (<see cref="Core.Config.PersonFigures.StartsAtTheWheelShare"/>),
    /// the trip drawn from the bay as a trip begun at a wheel is (PER-29).
    /// </summary>
    void SeatAtTheWheel(int person)
    {
        var car = People.Car[person];
        if (!_containers.IsFree(car)) return;

        _containers.LeaveBuilding(_containers.WhereIs(person).Index, person);
        _containers.TryBoard(car, person);
        DrawTrip(person);
    }

    /// <summary>
    /// <b>Every building's ways in, filed by cell</b> (SIM-8) for the spawn's one question of each person the plan stood:
    /// asked of every door in the town, it was a traced city's forty thousand people times its doors.
    /// </summary>
    sealed class Doors
    {
        readonly PointCells _doors;
        readonly int[] _buildingOf;
        readonly List<int> _near = [];

        public Doors(CityPlan.BuildingArrays buildings, GridLevel level)
        {
            _doors = new PointCells(level);
            _buildingOf = new int[buildings.EntryOffsets[buildings.Count]];
            for (var building = 0; building < buildings.Count; building++)
            {
                for (var entry = buildings.EntryOffsets[building]; entry < buildings.EntryOffsets[building + 1]; entry++)
                {
                    _doors.Add(buildings.EntryPointM[entry]);
                    _buildingOf[entry] = building;
                }
            }
        }

        /// <summary>
        /// The building whose way in this body is standing at, or −1 — the nearest one within touching reach
        /// of the door, which on a street of terraces is the difference between somebody's own house and
        /// their neighbour's. <b>Read off the pose the plan left it in</b> (GEN-7): a plan says somebody lives
        /// here by standing them at the door, and nothing in the format has to name it.
        /// </summary>
        /// <remarks>Of two doors as near, the one filed later, as a scan of every door in turn would answer.</remarks>
        public int BuildingWithADoorAt(Vector2 positionM, float reachM)
        {
            _doors.Within(positionM, reachM, _near);
            var best = -1;
            var bestM = reachM * reachM;
            foreach (var entry in _near)
            {
                var farM = (_doors[entry] - positionM).LengthSquared();
                if (farM > bestM) continue;

                best = _buildingOf[entry];
                bestM = farM;
            }

            return best;
        }
    }

    /// <summary>
    /// <b>A map with no bay a car can reach drives its own cars</b> (CAR-8). A car does nothing unless
    /// something is driving it (CAR-1), and on such a map nothing but an order ever sends one anywhere — an
    /// errand needs an apron, a car's own round needs a bay, and both need a car park — so its cars would stand
    /// where they were put for ever. <b>On a map that parks, it drives the cars nobody owns that were stood on a lane</b>
    /// (<see cref="_tours"/>) — a traced map's own where it has no roadside to stand them at (GEN-7), which have no stand to
    /// run a round from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is a fact about the map and not a name in a list</b>, and the fact is <em>whether a bay can be
    /// reached</em> — which on a town that cut no car park, the fixture among them, none can. Each car takes
    /// the lane it is standing on, exactly as a car that has lost its line takes one back (CAR-9), and drives
    /// from there under the standing rules — no destination, so it is carried by the tour.
    /// </para>
    /// <para>
    /// <b>It is asked every tick and not done once.</b> A toured car's leg ends the way every leg does, by
    /// the car being stood down where its clock ran out, and nothing else would send it anywhere: the rule
    /// that put it on the road is the same rule that puts it back on it. <b>A car refused a lane asks again on
    /// its own decision clock</b> (<see cref="_refusedALane"/>) rather than every tick, as any leg's re-ask is.
    /// </para>
    /// </remarks>
    void DriveTheEmptyMap()
    {
        if (_townParks && !_anyTours) return;

        for (var car = 0; car < Cars.Count; car++)
        {
            if (_townParks && !_tours[car]) continue;

            // A car whose wheel is already held over has somebody deciding for it, which is the whole of
            // what this rule exists to supply. Handing it a lane as well would lay a line nothing drives.
            if (Cars.Driven[car] || Cars.Broken[car] || WheelIsHeldOver(car))
            {
                _refusedALane[car] = false;
                continue;
            }

            if (_refusedALane[car] && !_decisions.Turn(_clockTicks, Roster.AgentOfCar(car))) continue;

            _refusedALane[car] = !TakeTheLaneUnderIt(car);
            if (_refusedALane[car]) continue;

            Enter(car, CarAction.Follow);
            Cars.Driven[car] = true;
        }
    }

    /// <summary>Whether a car <see cref="DriveTheEmptyMap"/> stood down was refused the lane under it when it last asked.</summary>
    readonly bool[] _refusedALane;

    /// <summary>
    /// <b>Whether a car tours</b> (CAR-8): one nobody owns that the plan stood on a lane rather than in a bay — every car
    /// of a map with no bay, and a traced map's own cars beside its people where it has no roadside to stand them at (GEN-7)
    /// — which has no stand to run a round from.
    /// </summary>
    readonly bool[] _tours;

    /// <summary>Whether any car tours, which is every tick's first question of <see cref="DriveTheEmptyMap"/>.</summary>
    bool _anyTours;

    /// <summary>
    /// Whether somebody's hand is on this car's wheel rather than anything in the town deciding for it
    /// (CTL-5). <b>Public because a picture of such a car has to say so</b>: nothing is choosing for it and
    /// it is not parked either, and those are the two states everything that names a car's behaviour
    /// otherwise has.
    /// </summary>
    /// <remarks>
    /// <b>Either hand</b> (CTL-5d): the player's over the car they picked out, or a second driver's over
    /// the car it named. What is true of the car is the same for both, which is why everything asks this
    /// rather than asking whose hand it is.
    /// </remarks>
    public bool WheelIsHeldOver(int car) =>
        PlayersHandOn(car) || (_otherHand.Held && _otherCar == car);

    /// <summary>
    /// A car starts stopped where the plan put it — in the bay under it, where that bay can be reached — with
    /// nobody in it: an inert dynamic object that can be pushed and takes no action of its own, holding its
    /// handbrake.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Its bay, where it has one, is the registry's from the first tick, and its first stand begins there
    /// (CAR-8). Where it has none — a town that cut no car park (GEN-7) — it stands at the plan's own pose.
    /// </para>
    /// <para>
    /// <b>And the pose is the bay's own and not the plan's</b> (GEN-4i), for a spawn that lands in one: every
    /// manoeuvre out is shaped from the pose a car square in the middle of the space stands at, and a car
    /// standing off it is a car whose first move out of the car park is a recovery. <b>Which of the two poses
    /// that is is the driver's habit</b> (GEN-4j), so a town starts with cars standing both ways round.
    /// </para>
    /// </remarks>
    void StandCar(int spawn, byte variant)
    {
        // <b>A space that belongs to an apron is not the town's to spawn into</b> (GEN-4k), and neither is
        // one another spawn has already been put in: the car is stood in the nearest free bay instead, and
        // a car with nowhere near to stand is not stood at all rather than dropped on top of whatever has
        // the place. It is the one thing an apron costs the plan, and it costs it a car at most.
        var positionM = _plan.Spawns.PositionM[spawn];
        var bay = BayUnder(positionM);
        if (bay >= 0 && !_parking.IsFree(bay))
        {
            bay = FreeBayNear(positionM, _config.PersonWalkWorthM);
            if (bay < 0) return;
        }

        // <b>Or at the kerb</b>, where the plan stood it on a roadside (GEN-7, GEN-4n): a place opened for it there, as a
        // place is opened for any car parking at a kerb. One standing where nobody may park, or on top of another, is not
        // stood.
        if (bay < 0 && _plan.Spawns.OnARoadside(spawn))
        {
            bay = KerbPlaceUnder(positionM, Cars.BuildOfVariant(variant));
            if (bay < 0) return;
        }

        if (bay >= 0)
        {
            StandCarIn(bay, variant, CarDraw(spawn));
            return;
        }

        var draw = CarDraw(spawn);
        var backsIn = draw.NextFloat() < _config.Driving.BacksIntoBaysShare;
        var car = AddCar(positionM, _plan.Spawns.HeadingRad[spawn], variant, backsIn, draw);
        StandOnItsLevel(car);
        _tours[car] = true;
        _anyTours = true;
    }

    /// <summary>A car stood in a bay (<see cref="StandSquareIn"/>) and its first stand begun there (CAR-8).</summary>
    int StandCarIn(int bay, byte variant, Rng draw)
    {
        var car = StandSquareIn(bay, variant, draw);
        BeginTheStand(car);
        return car;
    }

    /// <summary>
    /// A car stood square in a bay at the pose its driver's habit parks in (GEN-4i, GEN-4j), the bay its own — a
    /// spawned car's and a service vehicle's alike, since every manoeuvre out of the bay is shaped from that pose.
    /// </summary>
    /// <remarks>
    /// <b>The habit before the pose</b>, because the pose a car starts standing in is what its habit would have
    /// put it in — drawn from the car's own stream, which then goes on to the fleet as it stands.
    /// </remarks>
    int StandSquareIn(int bay, byte variant, Rng draw)
    {
        var backsIn = draw.NextFloat() < _config.Driving.BacksIntoBaysShare;

        // A car at the kerb points along it whichever way it got in (GEN-4n).
        var headingRad = _parking.IsKerb(bay) ? _parking.HeadingRad(bay) : BayTemplate.StandingHeadingRad(_parking.HeadingRad(bay), !backsIn);
        var car = AddCar(_parking.CentreM(bay), headingRad, variant, backsIn, draw);
        _parking.Occupy(bay, car);
        return car;
    }

    /// <summary>
    /// <b>The body is this variant's own</b> (CAR-11): its footprint, its weight, its axles and what its tyres
    /// are worth. The town's geometry is still the nominal car's — the junctions, the lanes and the bays were
    /// sized against it and the road is the same road whoever turns up — but nothing a car decides for itself is
    /// taken from it any more.
    /// </summary>
    int AddCar(Vector2 positionM, float headingRad, byte variant, bool backsIn, Rng draw) =>
        Cars.Add(positionM, headingRad, variant, backsIn, draw);

    /// <summary>A car's own stream, keyed by the spawn it was stood for — its own, or its owner's.</summary>
    Rng CarDraw(int spawn) => new(_agentSeed, (ulong)(spawn + 1) << 8);

    /// <summary>
    /// The bay a spawned car is standing in, or −1. Read off the pose rather than assumed: a car that
    /// stands anywhere else is stood where the plan put it.
    /// </summary>
    /// <remarks>
    /// <b>And a bay no way reaches is not a bay a car is in</b> (GEN-4f). The space is still drawn on the
    /// plan, but nothing can be driven into or out of it, so a car standing on it is a car at a kerb that
    /// happens to be square — which is the state this answers and the one the rest of the town can act on.
    /// </remarks>
    int BayUnder(Vector2 positionM)
    {
        for (var bay = 0; bay < _parking.PaintedCount; bay++)
        {
            if (!_parking.CanBeReached(bay)) continue;
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
