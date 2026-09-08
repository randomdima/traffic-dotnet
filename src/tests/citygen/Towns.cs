using System.Collections.Concurrent;
using System.Numerics;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.App.Hud;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Gen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Persistence;
using TrafficSimulation.World.Town;
using Xunit;

using TrafficSimulation.World.Statics;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// Every town the suite asks a question of is read or laid once and handed out — a town is a tenth of a
/// second and there are a dozen questions to ask of it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The suite asks its questions of towns it owns, and never of a shipped city.</b> There are two of
/// them: <see cref="Fixture"/>, the file every detailed check is staged on, and <see cref="City"/>, a
/// whole town this class lays from <see cref="Brief"/> at a seed of its own. Between them they are a
/// screen of every kind of ground and a working city with water, bridges, districts, bays, a hospital, a
/// station and a depot on it — which is the whole of what a city was ever being asked for.
/// </para>
/// <para>
/// <b>A shipped city is content and is not the suite's subject</b> (<see cref="Tier.Maps"/>). Whether
/// <em>this</em> generator lays a sound town is asked over several seeds by <c>GeneratorTests</c>; what a
/// laboratory map claims is answered by that map's own watch. A build may ship any number of cities at any
/// number of seeds, and a suite that gated on them would be a function of the content. What is asked of
/// them is asked deliberately, by <c>qq tests maps</c>.
/// </para>
/// <para>
/// <b>A shared plan must not be written to.</b> A test that writes to one reads its own copy through
/// <see cref="Fresh"/> — <c>ServicePlacementTests</c> is the whole of that today, since placing the
/// services is the one authoring step that assigns into a plan — and the generator's determinism cases
/// lay their town through <see cref="LayFresh"/> on purpose, because handed the shared one they would
/// compare a town to itself and pass whatever the generator did.
/// </para>
/// <para>
/// <b>Standing a world up is not writing to a plan</b>, so a <see cref="World.Town.TownWorld"/> is built
/// from <see cref="Of"/>. Nothing in this engine assigns into a plan's arrays; a world reads its plan and
/// keeps its own state, and a hundred and fifty cases each re-laying the same town was a tenth of a second
/// apiece bought nothing.
/// </para>
/// </remarks>
internal static class Towns
{
    /// <summary>The fixture map: one screen, one of every kind of ground, and what detailed checks are staged on.</summary>
    public const string Fixture = "Test";

    /// <summary>
    /// <b>The suite's own city</b>, laid from <see cref="Brief"/> rather than shipped — which is what makes
    /// a question about a whole town answerable without making the answer a fact about somebody's map.
    /// </summary>
    /// <remarks>
    /// It is not a name <see cref="Maps"/> knows and cannot be opened by <c>--map</c>: a town the suite
    /// lays for itself is a fixture, and a fixture on the menu is a map somebody would file a bug about.
    /// </remarks>
    public const string City = "Laid";

    /// <summary>The seed <see cref="City"/> is laid at. One town, so a failure is the same town every time.</summary>
    public const ulong CitySeed = 20260907;

    /// <summary>
    /// The seeds the generator's own properties are asked over. <b>Four and not forty</b>: a property that
    /// holds on four unrelated seeds and fails on the fifth is a property that fails, and the suite is the
    /// wrong instrument for a sweep.
    /// </summary>
    public static TheoryData<ulong> Seeds()
    {
        var seeds = new TheoryData<ulong>();
        foreach (var seed in (ulong[])[1, 7, 4242, 0xDEADBEEF]) seeds.Add(seed);
        return seeds;
    }

    /// <summary>
    /// <b>A town as the suite authors it.</b> Big enough to hold everything a question about a city needs —
    /// districts on their own bearings, an orbital, water with a bridge over it, frontages, bays, a roster
    /// of services and a crowd to use them — and a fifth of the size of a shipped one, because what makes a
    /// question about a town expensive is the bodies on it rather than the arrangement.
    /// </summary>
    /// <remarks>
    /// <b>It is not smaller than a wheel.</b> Below about two kilometres the orbital and its spokes are most
    /// of what the ground holds, the lattice inside them is a handful of points, and what comes out is a
    /// chain of streets rather than a town with blocks in it.
    /// </remarks>
    /// <param name="parkingSlotShare">
    /// Overridden only by the case that asks whether retuning a later stage moves the roads — which needs
    /// the same brief with one figure changed, and would silently ask nothing if it hand-copied the rest.
    /// </param>
    public static TownBrief Brief(ulong seed, WaterKind water = WaterKind.River, float parkingSlotShare = 0.3f) => new()
    {
        Name = City,
        Description = "The suite's own town, laid to ask questions of a city without shipping one",
        Seed = seed,
        WidthM = 2400f,
        HeightM = 1800f,
        Districts = 6,
        GridDistrictShare = 0.5f,
        BearingSpreadDeg = 30f,
        RingShare = 0.34f,
        UnregulatedJunctionShare = 0.15f,
        Buildings = 400,
        ParkingSlotShare = parkingSlotShare,
        People = 180,
        Cars = 180,
        Hospitals = 2,
        PoliceStations = 2,
        Depots = 1,
        Water = water,
        WaterBearingDeg = 100f,
        WaterMeander = 0.6f,

        // Narrow enough that the deck a town builds spans it (GEN-14a): a river this town cannot bridge is
        // a town in two halves, and the half that is deleted takes the properties being asked about with it.
        WaterShare = water == WaterKind.Coast ? 0.2f : 0.06f,
    };

    /// <summary>
    /// The roofs the generator's own properties are asked over. <b>Two of them and not the catalogue's</b>:
    /// what is asked of a laid town there is what the generator does with the footprints it is handed, and a
    /// property that read the art would go red the day somebody drew a wider house.
    /// </summary>
    /// <remarks>
    /// <see cref="City"/> is furnished from the catalogue instead, because it stands in for a city: what is
    /// asked of it includes what the roofs look like on the buildings under them.
    /// </remarks>
    static readonly Vector2[] RoofsM = [new(12f, 12f), new(18f, 16f)];

    static readonly ConcurrentDictionary<string, CityPlan> Shared = new();

    static readonly ConcurrentDictionary<(ulong Seed, WaterKind Water), CityPlan> Laid = new();

    /// <summary>The shipped set: the briefs, and the maps this build lays in code.</summary>
    public static IEnumerable<string> Shipped => Maps.Shipped();

    static readonly SimConfig Figures = SimConfig.Shipped();

    /// <summary>The town behind a name, laid or read once and handed to every case that asks for it.</summary>
    public static CityPlan Of(string map) => Shared.GetOrAdd(map, Fresh);

    /// <summary>A copy of a town nobody else holds — for the one test that writes into a plan.</summary>
    public static CityPlan Fresh(string map) =>
        map == City
            ? TownGenerator.Lay(Brief(CitySeed), Figures, BuildingCatalog.Shared.OrdinaryFootprintsM())
            : Maps.Plan(map, Figures, BuildingCatalog.Shared.OrdinaryFootprintsM());

    /// <summary>
    /// A generated town at one seed and one water, laid once per pair however many questions are asked of
    /// it. <b>Twenty-six properties over four seeds is a hundred and nine cases and eight towns</b>, and
    /// laying one per case was three quarters of the tier this project runs after every edit.
    /// </summary>
    public static CityPlan LaidFrom(ulong seed, WaterKind water = WaterKind.River) =>
        Laid.GetOrAdd((seed, water), key => LayFresh(key.Seed, key.Water));

    /// <summary>
    /// The same town, laid again and shared with nobody. <b>Only the determinism cases want this</b>: handed
    /// the memoised one they would compare a town to itself and pass whatever the generator did.
    /// </summary>
    public static CityPlan LayFresh(ulong seed, WaterKind water = WaterKind.River) =>
        LayFresh(Brief(seed, water));

    /// <inheritdoc cref="LayFresh(ulong, WaterKind)"/>
    public static CityPlan LayFresh(TownBrief brief) => TownGenerator.Lay(brief, Figures, RoofsM);

    /// <summary>
    /// <b>Whether a map stands any car up at all.</b> A question about what traffic does at a junction is
    /// vacuous on a map with no traffic — the walking exam is a lattice of junctions with nothing driving
    /// on it (<see cref="FootwayPlan"/>) — and a guard that says "nothing happened" would report that
    /// absence as a failure of the engine.
    /// </summary>
    public static bool AnythingDrives(string map) => Array.IndexOf(Of(map).Spawns.Kind, SpawnKindCar) >= 0;

    const byte SpawnKindCar = 1;

    /// <summary>
    /// <b>The towns the suite's own questions are asked of</b>: the fixture every detailed check is staged
    /// on, and the city this class lays.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A laboratory map is not asked them.</b> A map laid to measure one thing holds whatever that
    /// question needed and nothing else — no bay, no pavement, no station, no crossing and, on the idle
    /// ring, nobody on foot at all — so asking it about parking, walking or the police is asking about
    /// ground nobody laid, and what comes back is a pass over an empty set that reads like coverage.
    /// <b>What each of those maps is for, it claims itself</b> (<see cref="Bench.Scenarios"/>), and that is
    /// where it is answered.
    /// </para>
    /// <para>
    /// <b>Nor is a shipped city</b>, for the reason on <see cref="Tier.Maps"/>: a city is content, and what
    /// this engine owes a town is asked of a town the suite laid.
    /// </para>
    /// </remarks>
    public static TheoryData<string> EveryTown()
    {
        var maps = new TheoryData<string>();
        maps.Add(Fixture);
        maps.Add(City);
        return maps;
    }

    /// <summary>The city alone, without the fixture: what a question about a whole town is asked of.</summary>
    public static TheoryData<string> EveryCity()
    {
        var maps = new TheoryData<string>();
        maps.Add(City);
        return maps;
    }

    /// <summary>
    /// <b>Every town the suite owns</b> — the fixture, the crossings map, the seven laboratories and the
    /// city this class lays — which is the set the shallow bar is a question about. A generated city is left
    /// out: it is content, and its bar is <see cref="Tier.Maps"/>'.
    /// </summary>
    public static TheoryData<string> EveryLaidMap()
    {
        var maps = new TheoryData<string>();
        foreach (var map in LaidMaps) maps.Add(map);
        return maps;
    }

    /// <inheritdoc cref="EveryLaidMap"/>
    public static IEnumerable<string> LaidMaps
    {
        get
        {
            yield return City;
            foreach (var map in Shipped)
            {
                if (!Maps.IsGenerated(map)) yield return map;
            }
        }
    }

    /// <summary>
    /// <b>The shipped cities</b>: the briefs in <c>towns/</c>, laid as they ship. Nothing outside
    /// <see cref="Tier.Maps"/> may ask a question of these.
    /// </summary>
    public static TheoryData<string> EveryShippedCity()
    {
        var maps = new TheoryData<string>();
        foreach (var map in Shipped)
        {
            if (Maps.IsGenerated(map)) maps.Add(map);
        }

        return maps;
    }

    /// <summary>
    /// Every map there is. <b>Only what is about the shipped <em>set</em></b> may ask this — that every map
    /// has a catalogue row, that every file reads back as it was written, that every one of them is watched
    /// against something — because those are questions about the list rather than about any town on it.
    /// </summary>
    public static TheoryData<string> EveryShippedMap()
    {
        var maps = new TheoryData<string>();
        foreach (var map in Shipped) maps.Add(map);
        return maps;
    }

    /// <summary>
    /// <b>The towns a gate is re-taken on</b>: the suite's own two, and the laboratories whose traffic
    /// actually piles up. A gate is a question about the engine and a town is only the load it is put
    /// under, so what it wants is the worst load this build can stand up rather than every map on the menu.
    /// </summary>
    public static TheoryData<string> EveryMapWorthAGate()
    {
        var maps = new TheoryData<string>();
        maps.Add(Fixture);
        maps.Add(City);
        maps.Add(ExamPlan.Name);
        maps.Add(TrackPlan.NameOf(TrackLap.Drunk));
        maps.Add(TrackPlan.NameOf(TrackLap.Fleet));
        return maps;
    }

    /// <summary>
    /// <b>How long a town takes to reach the worst load it ever reaches</b>, which is what a gate measuring a
    /// steady state has to warm past: the contact arrays settle at the most contacts the solver has ever had
    /// at once, and a town still growing its capacities is not the steady state a rule is about.
    /// </summary>
    /// <remarks>
    /// <b>It is a fact about the map and not a constant.</b> A lap with fifteen bodies reeling into the
    /// carriageway piles up worse at four minutes than at ten seconds, and paying that warm-up on a town
    /// whose traffic disperses buys nothing but the wait — which is what one figure for every map cost.
    /// </remarks>
    public static int SettleTicks(string map) =>
        map == TrackPlan.NameOf(TrackLap.Drunk) || map == TrackPlan.NameOf(TrackLap.Fleet) ? 24_000 : 3_600;

    /// <summary>
    /// Every town of the suite's own that was laid with a pavement — <b>which is what the walking network's
    /// own questions are asked of</b>. A town laid without one (<see cref="CityPlan.PavementWidthM"/>) has
    /// no footway, no kerb and nobody on it, and every claim about corners, mitres and crossings there is
    /// vacuously true rather than checked.
    /// </summary>
    public static TheoryData<string> EveryMapWithAFootway()
    {
        var maps = new TheoryData<string>();
        foreach (var map in (string[])[Fixture, City])
        {
            if (Of(map).PavementWidthM > 0f) maps.Add(map);
        }

        return maps;
    }

    /// <summary>
    /// The four ring sets a town's water is drawn from, each with the name to say which one a failure is
    /// about — so a question asked of the water is asked of every ring of it and not of the wet one alone.
    /// </summary>
    public static (string What, CityPlan.RingArrays Rings)[] WaterRingsOf(CityPlan.WaterArrays water) =>
    [
        ("outline", water.Outline), ("shore", water.Shore),
        ("shore edge", water.ShoreEdge), ("water edge", water.WaterEdge),
    ];

    /// <summary>
    /// <b>The walker standing nearest a carriageway</b>, or −1 — who a staged casualty is made of.
    /// <see cref="Bench.RescueProbe"/>'s own choice, for its own reason: a service vehicle arrives along the
    /// road, so somebody knocked down in the middle of a park is a call nothing can be got to (AMB-9).
    /// </summary>
    /// <remarks>Crews are passed over: knocking a paramedic down stages the rescue against itself.</remarks>
    public static int NearestWalkerToARoad(TownWorld world)
    {
        var best = -1;
        var bestM = float.PositiveInfinity;
        for (var person = 0; person < world.People.Count; person++)
        {
            if (world.People.Inside[person].Any || world.People.Wounded[person]) continue;
            if (world.People.Stage[person] is TripStage.OnDuty or TripStage.Attending) continue;

            var positionM = world.People.PositionM[person];
            var lane = world.Roads.NearestLane(positionM, out var alongM);
            if (lane < 0) continue;

            var offM = (Spline.SampleAt(world.Roads.ArcsOf(lane), alongM).PositionM - positionM).Length();
            if (offM >= bestM) continue;

            best = person;
            bestM = offM;
        }

        return best;
    }

    /// <summary>
    /// The fixture map with a different number of buildings on it, for the questions a roster drawn over
    /// buildings asks (AMB-1, SRV-1) — a village, a city and a map with nobody living on it.
    /// </summary>
    public static CityPlan WithBuildings(int buildings)
    {
        var plan = Of(Fixture);
        var entryOffsets = new int[buildings + 1];
        for (var building = 0; building <= buildings; building++) entryOffsets[building] = building;

        return new CityPlan
        {
            Seed = plan.Seed,
            Name = plan.Name,
            WorldSizeM = plan.WorldSizeM,
            PavementWidthM = plan.PavementWidthM,
            Junctions = plan.Junctions,
            JunctionCorners = plan.JunctionCorners,
            PavementCorners = plan.PavementCorners,
            Roads = plan.Roads,
            Bridges = plan.Bridges,
            PavedAreas = plan.PavedAreas,
            Crosswalks = plan.Crosswalks,
            StopLines = plan.StopLines,
            ParkingLots = plan.ParkingLots,
            Buildings = new CityPlan.BuildingArrays
            {
                CentreM = new Vector2[buildings],
                SizeM = new Vector2[buildings],
                HeadingRad = new float[buildings],
                Capacity = new int[buildings],
                Use = new BuildingUse[buildings],
                EntryOffsets = entryOffsets,
                EntryPointM = new Vector2[buildings],
            },
            Props = plan.Props,
            Spawns = plan.Spawns,
            Water = plan.Water,
        };
    }
}
