using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Agents.Service;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Containment;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Statics;

namespace TrafficSimulation.World.Town;

/// <summary>
/// The service vehicles a town stands (SRV-1…5): an apron of ambulances at every hospital, of police cars at
/// every station and of evacuators at every depot, the crew aboard each, and the one thing every service
/// vehicle is made of.
/// </summary>
/// <remarks>
/// <b>The apron is what a special building has and an ordinary one does not</b> (GEN-4k): the bays a
/// hospital, a police station and a depot stand their vehicles on are held for those vehicles for the whole
/// run, so a car that drove out on an errand has somewhere of its own to come back to. A depot's run is
/// those bays and its yard's slots besides (EVA-2). Half of each apron's vehicles drive their district's beat
/// (<c>TownWorld.Beat.cs</c>); what each does on a call is <c>TownWorld.Patrol.cs</c>,
/// <c>TownWorld.Ambulance.cs</c> and <c>TownWorld.Recovery.cs</c>.
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>What each of this map's buildings is for (AMB-1, SRV-1).</summary>
    public BuildingUses Uses => _uses;

    /// <summary>Which buildings this map declares as police stations and as depots (SRV-1).</summary>
    public BuildingRoster PoliceStations => _uses.PoliceStations;

    public BuildingRoster Depots => _uses.Depots;

    /// <summary>
    /// How many of each the town actually stood, which is the bays a building's apron found and not the
    /// roster's own count (SRV-2) — the same real state <see cref="Ambulances"/> reports.
    /// </summary>
    public int PoliceCars { get; private set; }

    public int Evacuators { get; private set; }

    /// <summary>
    /// The bays every apron is laid on — the hospitals' runs first, then the stations', then the depots' —
    /// <see cref="_apronStride"/> entries each and <see cref="ParkingRegistry.NoBay"/> for a bay the map had
    /// nowhere to put.
    /// </summary>
    /// <remarks>
    /// <b>One stride for all three and not a run per kind.</b> A depot wants a yard slot for every wreck besides
    /// its vehicles' bays (EVA-2), which is a different figure from a station's four; laid at the larger of the
    /// two, an entry's run is <c>entry * stride</c> wherever it came from and the arithmetic that finds a slot
    /// cannot disagree with the arithmetic that laid it. What it costs is a few unused ints in an array of a few
    /// hundred.
    /// </remarks>
    int[] _apronBays = [];

    int _apronStride;

    /// <summary>Where the depots' runs begin, which is after every hospital's and every station's.</summary>
    int TheFirstYard => Hospitals.Count + PoliceStations.Count;

    /// <summary>
    /// <b>One yard slot</b> (EVA-2): the depot's own run, past the bays its evacuators stand in. Every slot a
    /// map had nowhere to put is <see cref="ParkingRegistry.NoBay"/> and is skipped rather than hidden.
    /// </summary>
    int YardSlot(int yard, int slot) =>
        _apronBays[((TheFirstYard + yard) * _apronStride) + _config.Service.ApronBays + slot];

    /// <summary>How many bays this building's apron asks for: a shift of vehicles, and a depot's yard besides.</summary>
    int ApronBaysWantedBy(int entry) =>
        _config.Service.ApronBays + (entry < TheFirstYard ? 0 : _config.Evacuator.YardSlots);

    int ApronBuildingOf(int entry) => entry < Hospitals.Count
        ? Hospitals.BuildingOf(entry)
        : entry < TheFirstYard
            ? PoliceStations.BuildingOf(entry - Hospitals.Count)
            : Depots.BuildingOf(entry - TheFirstYard);

    /// <summary>
    /// <b>Every apron claimed, before the plan's own cars are stood</b> (GEN-4k). Nothing is put in them
    /// here: what is taken is the ground, and it is taken first because a bay a spawned car is already
    /// standing in is not a bay a station can have.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A round at a time, so each special building takes its first bay before any takes its second.</b>
    /// A whole apron at a time, the first hospital drawn empties the lot it stands on and the police
    /// station across the road gets nothing — which is what the fixture town did, its four bays being the
    /// only bays it has.
    /// </para>
    /// <para>
    /// <b>Its own side of the road where the map has one, and one side either way.</b> The first bay is
    /// looked for on the building's own side and only then anywhere within a walk, because a shipped map
    /// regularly puts a building's only parking over the road from it — the fixture town's station has no
    /// bay on its own side at all, and refusing that outright stood it no cars. Every later bay is
    /// measured against <em>the first</em> and never against the one before it: chained, an apron walks
    /// round a corner a bay at a time and comes out on both kerbs of the street it started on.
    /// </para>
    /// </remarks>
    void HoldTheAprons()
    {
        var aprons = TheFirstYard + Depots.Count;
        _apronStride = _config.Service.ApronBays + _config.Evacuator.YardSlots;
        _apronBays = new int[aprons * _apronStride];
        Array.Fill(_apronBays, ParkingRegistry.NoBay);

        for (var slot = 0; slot < _apronStride; slot++)
        {
            for (var entry = 0; entry < aprons; entry++)
            {
                if (slot >= ApronBaysWantedBy(entry)) continue;

                var placeM = _plan.Buildings.CentreM[ApronBuildingOf(entry)];
                var withinM = entry < Hospitals.Count ? _config.AmbulanceHomeM : _config.ServiceHomeM;

                var first = _apronBays[entry * _apronStride];
                var bay = FreeBayNear(placeM, withinM, first >= 0 ? _parking.CentreM(first) : placeM);
                if (bay < 0 && first < 0) bay = FreeBayNear(placeM, withinM);
                if (bay < 0) continue;

                // Every slot of a depot's run past its vehicles' is a yard slot, and a yard slot is held for
                // whatever is brought to it rather than for a named vehicle that is about to be stood in it.
                if (entry >= TheFirstYard && slot >= _config.Service.ApronBays) _parking.HoldForTheYard(bay);
                else _parking.HoldTheApron(bay);

                _apronBays[(entry * _apronStride) + slot] = bay;
            }
        }
    }

    /// <summary>
    /// <b>And every apron filled</b> (AMB-2, SRV-2): one vehicle a bay, each bay then held for the vehicle
    /// standing in it for the rest of the run, its crew aboard (SRV-3) — and <b>the last of each building's
    /// fleet given its district's beat</b> (SRV-5), the first standing by its door.
    /// </summary>
    /// <remarks>
    /// A depot's vehicles take the first bays of its run and leave the rest of it empty: those are the yard's
    /// slots, and a wreck is what goes in one (EVA-2).
    /// </remarks>
    void StandTheServiceVehicles()
    {
        var apron = _config.Service.ApronBays;
        for (var entry = 0; entry < TheFirstYard + Depots.Count; entry++)
        {
            var building = ApronBuildingOf(entry);
            var run = entry * _apronStride;

            var fleet = 0;
            for (var slot = 0; slot < apron; slot++)
            {
                if (_apronBays[run + slot] >= 0) fleet++;
            }

            var standing = fleet - (int)MathF.Round(fleet * _config.Service.PatrolShare);
            var stood = 0;
            for (var slot = 0; slot < apron; slot++)
            {
                var car = entry < Hospitals.Count
                    ? StandAnAmbulance(_apronBays[run + slot], building)
                    : entry < TheFirstYard
                        ? StandAPoliceCar(_apronBays[run + slot], building)
                        : StandAnEvacuator(_apronBays[run + slot], building, entry - TheFirstYard);
                if (car < 0) continue;

                JoinTheDistrictBeat(car, building, patrols: stood++ >= standing);
            }
        }
    }

    int StandAnAmbulance(int bay, int hospital)
    {
        var car = FillAnApronBay(bay, (byte)CarCatalog.Shared.Ambulance, RescueStream);
        if (car < 0) return NoCar;

        TakeUpTheRescue(car, hospital);
        StandTheCrew(car, PersonCatalog.Shared.Paramedic);
        Ambulances++;
        return car;
    }

    int StandAPoliceCar(int bay, int station)
    {
        var car = FillAnApronBay(bay, (byte)CarCatalog.Shared.Police, PoliceStream);
        if (car < 0) return NoCar;

        BeginTheBeat(car, station, bay);
        _beat.Officer[car] = StandTheCrew(car, PersonCatalog.Shared.Police);
        PoliceCars++;
        return car;
    }

    int StandAnEvacuator(int bay, int depot, int yard)
    {
        var car = FillAnApronBay(bay, (byte)CarCatalog.Shared.Evacuator, EvacuatorStream);
        if (car < 0) return NoCar;

        TakeUpTheRecovery(car, depot, yard);
        StandTheCrew(car, PersonCatalog.Shared.Recovery);
        Evacuators++;
        return car;
    }

    /// <summary>
    /// <b>The crew a service vehicle carries</b> (SRV-3): walkers in its building's uniform (SRV-3a), stood with
    /// the car before the first tick and put straight into its crew seats. The first of them, which is the one a
    /// police car puts out at a closure (SRV-11), or <see cref="PatrolDuty.Nobody"/> for a car stood with none.
    /// </summary>
    int StandTheCrew(int car, int uniform)
    {
        var first = PatrolDuty.Nobody;
        for (var seat = 0; seat < CrewAboard(_config); seat++)
        {
            var positionM = Cars.PositionM[car];
            var body = _physics.AddPerson(positionM);
            var person = People.Add(
                body, positionM, Cars.HeadingRad[car], _physics.MassOf(body), _config.PersonDiameterM * 0.5f,
                (byte)uniform, new Rng(_agentSeed, CrewStream + (ulong)((car * Containers.CrewSeats) + seat)),
                reckless: false);
            _physics.Tag(body, new BodyTag(BodyKind.Person, person));
            _progress.Restart(person);

            People.Stage[person] = TripStage.OnDuty;
            if (_containers.TryTakeACrewSeat(car, person)) Contain(person);
            if (first < 0) first = person;
        }

        return first;
    }

    int FillAnApronBay(int bay, byte variant, ulong stream)
    {
        if (bay < 0) return NoCar;

        var car = StandAServiceVehicle(bay, variant, stream);
        _parking.HoldForTheCar(bay, car);
        return car;
    }

    const int NoCar = -1;

    /// <summary>
    /// One service vehicle standing in its bay (SRV-3). <b>The same pose a spawned car comes
    /// to rest in</b> (GEN-4i): the bay's ways meet at it, so the first thing it does when it is given
    /// something to do is drive rather than recover.
    /// </summary>
    /// <remarks>
    /// <b>What makes it a car that acts is the errand rather than a seat</b> (SRV-3), and what keeps it out of
    /// anybody else's hands is the building it stands on the strength of (<see cref="IsAServiceVehicle"/>). A
    /// crew rides in its crew seats and is stood with it (<see cref="StandTheCrew"/>).
    /// </remarks>
    int StandAServiceVehicle(int bay, byte variant, ulong stream)
    {
        // Its own stream, off the bay it stands in, so standing one cannot move what any spawn draws.
        var draw = new Rng(_agentSeed, stream + (ulong)bay);
        var backsIn = draw.NextFloat() < _config.Driving.BacksIntoBaysShare;
        var headingRad = BayTemplate.StandingHeadingRad(
            _parking.HeadingRad(bay), _bayWays.TheStandingOnOffer(bay, !backsIn));
        var positionM = _parking.CentreM(bay);

        ref readonly var build = ref _builds.Of(variant);
        var body = _physics.AddCar(
            positionM, headingRad, build.CollisionSizeM * 0.5f, build.CornerRadiusM, build.MassKg);
        var car = Cars.Add(body, positionM, headingRad, variant, backsIn, draw);
        _physics.Tag(body, new BodyTag(BodyKind.Car, car));
        _parking.Occupy(bay, car);
        return car;
    }

    /// <summary>
    /// Whether this car is one a town stood on purpose (SRV-3), which is what says the reset hands it back to
    /// its errand rather than standing it down (<see cref="ReleaseOrderOfCar"/>). <b>It is the look
    /// <em>and</em> the building it is on the strength of</b>: the fleet's wrap cannot reach a service look,
    /// so a car wearing one was named by whoever stood it — and a vehicle struck off its building (EVA-7) is
    /// an ordinary car in service paint, stood down like anybody else's.
    /// </summary>
    bool IsAServiceVehicle(int car) =>
        CarCatalog.Shared.IsService(Cars.Variant[car]) &&
        (_duty.Hospital[car] >= 0 || _beat.Station[car] >= 0 || IsAnEvacuator(car));

    /// <summary>The world seed's streams a police car, an evacuator and a crew are drawn from, each belonging to nothing else.</summary>
    const ulong PoliceStream = 0x504C4341;

    const ulong EvacuatorStream = 0x45564143;

    const ulong CrewStream = 0x4F464643;

}
