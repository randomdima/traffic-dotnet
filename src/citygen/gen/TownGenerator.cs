using System.Diagnostics;
using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>A town from a brief and a seed</b> (GEN-1): the seven stages, in the one order they can run in, each
/// of them once.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every stage runs in a single pass and nothing is ever laid and taken back</b> (GEN-10). No candidate town is
/// generated and rejected, and no stage retries: what makes that possible is that each stage constrains the
/// next rather than checking it afterwards — the water is cut before a node is placed, the districts are
/// convex so their streets mostly cannot cross, the arterials carry a node wherever a street meets one, a
/// building claims its own padding as it is stood, and the props take what is left. Where the ground cannot
/// afford what the brief asked for, the town is what fitted and the census reports the shortfall.
/// </para>
/// <para>
/// <b>The nodes are placed, then settled, then joined — and no road is drawn twice</b> (GEN-16, GEN-49).
/// Everything that puts a junction down does so before the first road is laid; the clusters standing inside a
/// locality of each other are made one node there and then (<see cref="TownLayout.SettleTheNodes"/>); and only
/// then is a line drawn for anything. <b>Nothing is merged after the fact</b>, which is what a merge would
/// cost: every road in the town offered again, and until that re-offer no road held to the ground another
/// road holds, two junctions a stride apart being a pair about to become one.
/// </para>
/// <para>
/// <b>Where the arrangement is not enough on its own, the answer is deletion and never a retry</b>
/// (GEN-8): the stranded pieces are dropped and the dead ends pruned, and the roads that would share ground
/// are refused as they are laid (GEN-49) — each of them one pass over what the stages before it laid, and
/// none of them a second attempt at what one of them refused.
/// </para>
/// <para>
/// <b>What is left is then opened out and directed</b>, in that order and on the settled layout: the
/// junctions a district leaves by become roundabouts (<see cref="Roundabouts"/>, GEN-19), and the streets
/// that can afford it are taken one way (<see cref="OneWayStreets"/>, GEN-18). Neither deletes anything, and
/// each is asked of the town there actually is rather than of the arrangement it was laid in.
/// </para>
/// <para>
/// <b>Each stage draws on its own stream of the world seed</b> (GEN-11). Retuning what the props do cannot move
/// where the roads went, which is what makes a stage worth changing at all — and it is why a town is the
/// same every time it is opened without any stage having to know about the others' draws.
/// </para>
/// </remarks>
internal static class TownGenerator
{
    // One stream a stage, so a change to a later stage leaves every earlier one exactly where it was.
    const ulong TerrainStream = 0x7465_7272_6169_6E00;
    const ulong DistrictStream = 0x6469_7374_7269_6374;
    const ulong ShapeStream = 0x7368_6170_6573_0000;
    const ulong PropStream = 0x7072_6F70_7300_0000;
    const ulong SpawnStream = 0x7370_6177_6E73_0000;
    const ulong CarParkStream = 0x6361_7270_6172_6B00;
    const ulong BuildingStream = 0x6275_696C_6469_6E67;
    const ulong SignalStream = 0x7369_676E_616C_7300;

    /// <summary>Nothing has been refused yet when the layout is settled, so every corner is offered to the join (GEN-51).</summary>
    static readonly HashSet<Vector2> NothingHeld = [];

    /// <param name="sizes">
    /// The footprints the buildings are sized at, handed down from the catalogue that read them
    /// (<see cref="BuildingSizes"/>). A town laid with none stands no building.
    /// </param>
    public static CityPlan Lay(TownBrief brief, SimConfig config, BuildingSizes sizes)
    {
        brief.Check(brief.Name);

        var laidMs = new List<(string Stage, double Ms)>();
        var stageAt = Stopwatch.GetTimestamp();
        void Took(string stage)
        {
            laidMs.Add((stage, Stopwatch.GetElapsedTime(stageAt).TotalMilliseconds));
            stageAt = Stopwatch.GetTimestamp();
        }

        var worldSizeM = new Vector2(brief.WidthM, brief.HeightM);
        var claims = GenClaims.Over(config.Grid, worldSizeM, brief.CellSizeM);

        // <b>The ground, asked about while the town is still being laid.</b> Every stage below reads it to
        // decide where a thing may stand, and it is the same reading the finished map answers with — the
        // shapes laid so far and nothing else (TER-7). It is remade as each stage adds its own, because
        // what is on the ground at a point is a fact about the shapes there are.
        var bare = GroundPieces.None(brief.Seed, worldSizeM, config.PavementWidthM);

        var terrain = new Rng(brief.Seed, TerrainStream);
        var water = TerrainStage.Lay(brief, config, ref terrain);
        var wet = new GroundShapes(bare.With(water.Rings), config);
        var rules = new WaterRules(
            wet, config.Terrain.GroundStepM, water, config.CityGen.BridgeDeckLongestM,
            Lattice.CorridorM(config), (config.RoadWidthM * 0.5f) + config.PavementWidthM);
        Took("water");

        var district = new Rng(brief.Seed, DistrictStream);
        var districts = Districts.Lay(brief, config, wet, water, ref district);
        Took("districts");

        // Nothing shorter than the ground two junctions' own discs and corners take is a road at all.
        var shortestRoadM = Lattice.CorridorM(config) * 2f;

        // <b>The line a link would be laid as is what says whether it is a road</b> (GEN-10,
        // <see cref="RoadLines"/>), so the geometry is handed to the layout rather than run over it
        // afterwards: every road the layout holds is one that could be drawn where it stands.
        var shapes = new RoadLines(brief.Seed, config, districts, worldSizeM, rules);
        var layout = new TownLayout(
            shortestRoadM, config.ArmsApartMinRad, config.CityGen.LocalityM, config.Grid.Main, rules, shapes);
        var marginM = MarginM(config);

        // <b>Every node the town will have is placed before its first road is laid</b>, which is what lets
        // every road be laid once (GEN-10): the arterials carry the nodes the streets hang off them, so they
        // are closed only once the lattice has placed those — and laid before the streets, so a street
        // offered against ground an arterial holds is the one refused (GEN-49).
        var arterials = Arterials.Lay(layout, districts, brief, rules, shortestRoadM, marginM);
        var lattice = Lattice.Place(layout, districts, arterials, brief, wet, config, marginM);
        Took("nodes");

        // <b>And the nodes are settled between the placing and the laying</b> (GEN-16): every cluster standing
        // inside a locality of itself is one junction before a single road is drawn, so nothing is merged
        // afterwards, no road is drawn twice, and the ground bound holds from the first of them (GEN-49).
        var moved = layout.SettleTheNodes();
        Took("settle");
        arterials.TheNodesMoved(moved);
        arterials.Close(layout, rules);
        Lattice.Lay(layout, lattice, moved);
        Took("streets");

        layout.KeepTheLargestComponent();
        layout.PruneTheDeadEnds();
        Took("prune");
        ThroughRoads.Lay(layout, config);
        Took("through");
        Roundabouts.Lay(layout, districts, rules, config, worldSizeM, marginM);
        Took("roundabouts");
        OneWayStreets.Lay(layout, config);
        Took("one-way");

        // <b>And the car parks are cut into what that leaves</b> (GEN-52, GEN-53), which is the last thing
        // done to a layout: a cut road's arms are its line's own, so nothing may be offered to the layout
        // after one (<see cref="CutJunctions"/>).
        var carPark = new Rng(brief.Seed, CarParkStream);
        var carParks = CarParks.Lay(layout, brief, config, districts.HubM, ref carPark);
        Took("car parks");

        var signals = new Rng(brief.Seed, SignalStream);
        var roads = RoadStage.Lay(layout, brief, config, carParks, ref signals);
        Took("roads");

        var paved = bare.With(water.Rings).With(
            roads.Roads, roads.Bridges, roads.Junctions, roads.Corners, roads.Roundabouts, roads.Crosswalks);

        // <b>The boundary is settled once the roads are laid</b>, and nothing below adds driven ground — so
        // this is the boundary the finished map answers with, and the one everything left is cleared
        // against. It is also where the lanes come from: the stages below want the lines rather than the
        // records, and laying them twice would be two towns (TER-7).
        var paving = Paving.Lay(paved, config);
        Took("paving");
        var streets = new GroundShapes(paving, config);
        Took("ground");

        // <b>And the buildings stand against the boundary that settles</b> (GEN-54): every one of them off
        // the pavement's own outer face, the services first and each on the yard cut for it (GEN-55). It is
        // before the props because a verge carries what is left over beside what is built (GEN-6b).
        var building = new Rng(brief.Seed, BuildingStream);
        var buildings = BuildingStage.Lay(
            brief, carParks, roads.Roads, roads.Junctions.CentreM, paving, streets, claims, sizes, config,
            ref building);
        Took("buildings");

        var prop = new Rng(brief.Seed, PropStream);
        var props = PropStage.Lay(brief, paving, streets, claims, config, ref prop);
        Took("props");

        var spawn = new Rng(brief.Seed, SpawnStream);
        var spawns = SpawnStage.Lay(brief, paving, buildings, config, ref spawn);
        Took("spawns");

        return new CityPlan
        {
            Seed = brief.Seed,
            Name = brief.Name,
            WorldSizeM = new Vector2(brief.WidthM, brief.HeightM),
            PavementWidthM = config.PavementWidthM,
            Junctions = roads.Junctions,
            JunctionCorners = roads.Corners,
            Roads = roads.Roads,
            Bridges = roads.Bridges,
            Roundabouts = roads.Roundabouts,
            CarParks = new CityPlan.CarParkArrays
            {
                Junction = carParks.Junction, BayOffsets = carParks.BayOffsets, Road = carParks.Road,
                Right = carParks.Right,
            },
            PavedAreas = CityPlan.PavedAreaArrays.None,
            Crosswalks = roads.Crosswalks,

            // <b>No bay</b>: a car park is the junction its arms are cut as (GEN-53) and the spaces on them
            // are not laid, which is named in the known gaps rather than half-kept.
            ParkingLots = paved.ParkingLots,
            Buildings = buildings,
            Props = props,
            Spawns = spawns,
            Water = water.Rings,

            // The lanes, the movements and the ground the stages above stood on, handed over rather than
            // thrown away for whoever opens the town to lay again.
            PavingLaidWithIt = paving,
            LaidMs = [.. laidMs],
        };
    }

    /// <summary>
    /// The ground kept clear round the edge of the world: a road and the walk beside it, so nothing a town
    /// lays runs off the map it is laid on.
    /// </summary>
    static float MarginM(SimConfig config) => (config.RoadWidthM * 0.5f) + config.PavementWidthM;
}
