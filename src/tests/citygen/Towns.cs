using System.Collections.Concurrent;
using System.Numerics;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.App.Hud;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Persistence;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// Every town the suite asks a question of is read once and handed out. Reading Odesa is a tenth of
/// a second and there are a dozen questions to ask of it; the reference suite spent forty-six of its
/// fifty-four seconds laying the same handful of towns over and over.
/// </summary>
/// <remarks>
/// <para>
/// <b>A shared plan must not be written to.</b> A test that writes to one reads its own copy through
/// <see cref="Fresh"/> — <c>ServicePlacementTests</c> is the whole of that today, since placing the
/// services is the one authoring step that assigns into a plan — and the day this engine has a
/// generator, its two determinism tests take a fresh town twice on purpose, because handed the shared
/// one they would compare a town to itself and pass whatever it did.
/// </para>
/// <para>
/// <b>Standing a world up is not writing to a plan</b>, so a <see cref="World.Town.TownWorld"/> is built
/// from <see cref="Of"/>. Nothing in this engine assigns into a plan's arrays; a world reads its plan and
/// keeps its own state, and a hundred and fifty cases each re-parsing the same file was a tenth of a
/// second apiece bought nothing.
/// </para>
/// </remarks>
internal static class Towns
{
    /// <summary>The fixture map: one screen, one of every kind of ground, and what detailed checks are staged on.</summary>
    public const string Fixture = "Test";

    static readonly ConcurrentDictionary<string, CityPlan> Shared = new();

    public static IEnumerable<string> Shipped => ProjectPaths.ShippedMaps();

    public static CityPlan Of(string map) => Shared.GetOrAdd(map, Fresh);

    public static CityPlan Fresh(string map) => TownReader.ReadFile(ProjectPaths.TownFile(map));

    /// <summary>Every shipped map, as xUnit wants its cases: one row per map, so a failure names it.</summary>
    public static TheoryData<string> EveryShippedMap()
    {
        var maps = new TheoryData<string>();
        foreach (var map in Shipped) maps.Add(map);
        return maps;
    }

    /// <summary>
    /// <b>The towns a city's own questions are asked of</b>: the cities, and the fixture map every detailed
    /// check is staged on.
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
    /// <b>It is also the set a soak is worth driving.</b> A soak asks after a state that turns up rarely
    /// and what makes one turn up is traffic, so ten minutes of a lap with six cars on it witnesses nothing
    /// the first ten seconds did not.
    /// </para>
    /// <para>
    /// <b>What stays on <see cref="EveryShippedMap"/> is what is about the shipped set itself</b>: that
    /// every file reads back as it was written, that every map conforms, that every one of them is watched
    /// against something, and the two gates this whole project answers to.
    /// </para>
    /// </remarks>
    public static TheoryData<string> EveryTown()
    {
        var maps = new TheoryData<string>();
        foreach (var map in Shipped)
        {
            if (MapCatalogue.Describe(map).Kind == MapKind.Place || map == Fixture) maps.Add(map);
        }

        return maps;
    }

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
            CellSizeM = plan.CellSizeM,
            PavementWidthM = plan.PavementWidthM,
            GridWidth = plan.GridWidth,
            GridHeight = plan.GridHeight,
            Cells = plan.Cells,
            LaneDirs = plan.LaneDirs,
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

    /// <summary>
    /// Every shipped map that was laid with a pavement — <b>which is what the walking network's own
    /// questions are asked of</b>. A map laid without one (<see cref="CityPlan.PavementWidthM"/>) has no
    /// footway, no kerb and nobody on it, and every claim about corners, mitres and crossings there is
    /// vacuously true rather than checked.
    /// </summary>
    public static TheoryData<string> EveryMapWithAFootway()
    {
        var maps = new TheoryData<string>();
        foreach (var map in Shipped)
        {
            if (Of(map).PavementWidthM > 0f) maps.Add(map);
        }

        return maps;
    }
}
