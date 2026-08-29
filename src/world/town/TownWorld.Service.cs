using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Statics;

namespace TrafficSimulation.World.Town;

/// <summary>
/// The service vehicles a town stands (SRV-1…5): the police cars on a station's apron, the evacuator at a
/// depot, and the one thing every service vehicle — an ambulance included — is made of.
/// </summary>
/// <remarks>
/// <b>The apron is what a special building has and an ordinary one does not</b> (GEN-4k): the bays a
/// hospital, a police station and a depot stand their vehicles on are held for those vehicles for the whole
/// run, so a car that drove out on an errand has somewhere of its own to come back to. A depot's run is
/// that bay and its yard's slots besides (EVA-2). What a police car does between standing and coming back
/// is <c>TownWorld.Patrol.cs</c>; an ambulance's is <c>TownWorld.Ambulance.cs</c> and an evacuator's is
/// <c>TownWorld.Recovery.cs</c>.
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
    /// <b>One stride for all three and not a run per kind.</b> A depot wants its evacuator's own bay and a
    /// yard slot for every wreck (EVA-2), which is a different figure from a station's four; laid at the
    /// larger of the two, an entry's run is <c>entry * stride</c> wherever it came from and the arithmetic
    /// that finds a slot cannot disagree with the arithmetic that laid it. What it costs is a few unused
    /// ints in an array of a few hundred.
    /// </remarks>
    int[] _apronBays = [];

    int _apronStride;

    /// <summary>Where the depots' runs begin, which is after every hospital's and every station's.</summary>
    int TheFirstYard => Hospitals.Count + PoliceStations.Count;

    /// <summary>
    /// <b>One yard slot</b> (EVA-2): the depot's own run, past the bay its evacuator stands in. Every slot a
    /// map had nowhere to put is <see cref="ParkingRegistry.NoBay"/> and is skipped rather than hidden.
    /// </summary>
    int YardSlot(int yard, int slot) => _apronBays[((TheFirstYard + yard) * _apronStride) + 1 + slot];

    /// <summary>How many bays this building's apron asks for: a hospital's and a station's shift, or a depot's one vehicle and its yard.</summary>
    int ApronBaysWantedBy(int entry) =>
        entry < TheFirstYard ? _config.Service.ApronBays : 1 + _config.Evacuator.YardSlots;

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
        _apronStride = Math.Max(_config.Service.ApronBays, 1 + _config.Evacuator.YardSlots);
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

                // Every slot of a depot's run past the first is a yard slot, and a yard slot is held for
                // whatever is brought to it rather than for a named vehicle that is about to be stood in it.
                if (entry >= TheFirstYard && slot > 0) _parking.HoldForTheYard(bay);
                else _parking.HoldTheApron(bay);

                _apronBays[(entry * _apronStride) + slot] = bay;
            }
        }
    }

    /// <summary>
    /// <b>And every apron filled</b> (AMB-2, SRV-2): one vehicle and one crew a bay, each bay then held for
    /// the vehicle standing in it for the rest of the run.
    /// </summary>
    void StandTheServiceVehicles()
    {
        var apron = _config.Service.ApronBays;
        for (var entry = 0; entry < Hospitals.Count; entry++)
        {
            var hospital = Hospitals.BuildingOf(entry);
            for (var slot = 0; slot < apron; slot++)
            {
                var car = FillAnApronBay(
                    _apronBays[(entry * _apronStride) + slot], (byte)CarCatalog.Shared.Ambulance,
                    (byte)PersonCatalog.Shared.Paramedic, RescueStream);
                if (car < 0) continue;

                TakeUpTheRescue(car, hospital);
                Ambulances++;
            }
        }

        for (var entry = 0; entry < PoliceStations.Count; entry++)
        {
            var station = PoliceStations.BuildingOf(entry);
            var run = (Hospitals.Count + entry) * _apronStride;
            for (var slot = 0; slot < apron; slot++)
            {
                var car = FillAnApronBay(
                    _apronBays[run + slot], (byte)CarCatalog.Shared.Police, (byte)PersonCatalog.Shared.Police,
                    PoliceStream);
                if (car < 0) continue;

                BeginTheBeat(car, station, _parking.BayOf(car));
                PoliceCars++;
            }
        }

        // A depot stands its one evacuator in the first bay of its own run and keeps the rest of that run
        // empty: those are the yard's slots, and a wreck is what goes in one (EVA-2).
        for (var yard = 0; yard < Depots.Count; yard++)
        {
            var car = FillAnApronBay(
                _apronBays[(TheFirstYard + yard) * _apronStride], (byte)CarCatalog.Shared.Evacuator,
                (byte)PersonCatalog.Shared.Recovery, EvacuatorStream);
            if (car < 0) continue;

            TakeUpTheRecovery(car, Depots.BuildingOf(yard), yard);
            Evacuators++;
        }
    }

    int FillAnApronBay(int bay, byte variant, byte uniform, ulong stream)
    {
        if (bay < 0) return NoCar;

        var car = StandAServiceVehicle(bay, variant, uniform, stream);
        _parking.HoldForTheCar(bay, car);
        return car;
    }

    const int NoCar = -1;

    /// <summary>
    /// <b>How many people a town stands in each service vehicle</b> (SRV-3): the driver, and the hand who
    /// works the street. It is what the walker roster is sized by, so it is a constant here rather than a
    /// figure — a town cannot be given a different number of crew after its rosters are laid.
    /// </summary>
    const int CrewPerServiceVehicle = 2;

    /// <summary>
    /// One service vehicle and its crew, standing in the bay (SRV-3). <b>The same pose a spawned car comes
    /// to rest in</b> (GEN-4i): the bay's ways meet at it, so the first thing it does when it is given
    /// something to do is drive rather than recover.
    /// </summary>
    /// <remarks>
    /// <b>A driver and a hand</b> (SRV-3): one at the wheel for the whole run, and one whose whole job is to
    /// get out and do the work in the street (SRV-6, AMB-10, EVA-5). What makes it a car that acts is the
    /// errand rather than the seat, and what keeps it out of everybody else's trip is the building it stands
    /// on the strength of (<see cref="IsAServiceVehicle"/>) — so a vehicle whose whole crew is out in the
    /// road is still nobody's to drive away.
    /// </remarks>
    /// <param name="uniform">
    /// Which of the person catalogue's service looks this crew wears (SRV-3a). It is named here rather
    /// than drawn, because the wrap an ordinary walker's look comes off cannot reach one.
    /// </param>
    int StandAServiceVehicle(int bay, byte variant, byte uniform, ulong stream)
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

        var driver = StandACrewMember(bay, positionM, headingRad, uniform, stream + CrewStream);
        _containers.TryBoard(car, driver);
        Contain(driver);
        People.Stage[driver] = TripStage.OnDuty;

        var hand = StandACrewMember(bay, positionM, headingRad, uniform, stream + HandStream);
        _containers.TryTakeACrewSeat(car, hand);
        Contain(hand);
        People.Stage[hand] = TripStage.OnDuty;

        return car;
    }

    /// <summary>One of a service vehicle's crew, made and put in the world at the vehicle's own pose.</summary>
    int StandACrewMember(int bay, Vector2 positionM, float headingRad, byte uniform, ulong stream)
    {
        var body = _physics.AddPerson(positionM);
        var person = People.Add(
            body, positionM, headingRad, _physics.MassOf(body), _config.PersonDiameterM * 0.5f, uniform,
            new Rng(_agentSeed, stream + (ulong)bay),
            PersonFleet.DrawsReckless(_agentSeed, stream + (ulong)bay, _config.Driving.RecklessShare));
        if (People.Reckless[person]) RecklessDrivers++;

        _physics.Tag(body, new BodyTag(BodyKind.Person, person));
        _progress.Restart(person);
        return person;
    }

    /// <summary>
    /// Whether this car is one a town stood on purpose (SRV-3), which is what says its crew stays aboard
    /// when a leg ends. <b>It is the look <em>and</em> the building it is on the strength of</b>: the
    /// fleet's wrap cannot reach a service look, so a car wearing one was named by whoever stood it — and a
    /// vehicle struck off its building (EVA-7) is an ordinary car in service paint, whose driver has to be
    /// let out like anybody else's.
    /// </summary>
    bool IsAServiceVehicle(int car) =>
        CarCatalog.Shared.IsService(Cars.Variant[car]) &&
        (_duty.Hospital[car] >= 0 || _beat.Station[car] >= 0 || IsAnEvacuator(car));

    /// <summary>The world seed's streams a police car and an evacuator are drawn from, each belonging to nothing else.</summary>
    const ulong PoliceStream = 0x504C4341;

    const ulong EvacuatorStream = 0x45564143;

    /// <summary>The crew's own offsets within whichever stream stood the vehicle they are inside.</summary>
    const ulong CrewStream = 0x10000;

    const ulong HandStream = 0x20000;
}
