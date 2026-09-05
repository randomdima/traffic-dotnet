using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>A town from a brief and a seed</b> (GEN-1): the six stages, in the one order they can run in, each
/// of them once.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every stage runs in a single pass and nothing is ever laid and taken back</b> (GEN-10). No candidate town is
/// generated and rejected, and no stage retries: what makes that possible is that each stage constrains the
/// next rather than checking it afterwards — the water is cut before a node is placed, the districts are
/// convex so their streets mostly cannot cross, the arterials carry a node wherever a street meets one, a
/// frontage slot claims its own padding before anything fills it, and the props take what is left. Where the
/// ground cannot afford what the brief asked for, the town is what fitted and the census reports the
/// shortfall.
/// </para>
/// <para>
/// <b>Where the arrangement is not enough on its own, the answer is still deletion and never a retry</b>
/// (GEN-8): the layout is settled by four passes of its own before it is given a shape — the local nodes
/// merged, the crossings unpicked (GEN-17), the stranded pieces dropped and the dead ends pruned — and each
/// of them is one pass over what the stages before it laid.
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
    const ulong SignalStream = 0x7369_676E_616C_7300;
    const ulong SlotStream = 0x736C_6F74_7300_0000;
    const ulong PropStream = 0x7072_6F70_7300_0000;
    const ulong SpawnStream = 0x7370_6177_6E73_0000;

    public static CityPlan Lay(TownBrief brief, SimConfig config, ReadOnlySpan<Vector2> roofsM)
    {
        brief.Check(brief.Name);

        var worldSizeM = new Vector2(brief.WidthM, brief.HeightM);
        var claims = GenClaims.Over(worldSizeM, brief.CellSizeM);

        // <b>The ground, asked about while the town is still being laid.</b> Every stage below reads it to
        // decide where a thing may stand, and it is the same reading the finished map answers with — the
        // shapes laid so far and nothing else (TER-7). It is remade as each stage adds its own, because
        // what is on the ground at a point is a fact about the shapes there are.
        var bare = GroundPieces.None(worldSizeM, config.PavementWidthM);

        var terrain = new Rng(brief.Seed, TerrainStream);
        var water = TerrainStage.Lay(brief, config, ref terrain);
        var wet = new GroundShapes(bare.With(water.Rings), config);
        var rules = new WaterRules(
            wet, config.Terrain.GroundStepM, water, config.CityGen.BridgeDeckLongestM,
            Lattice.CorridorM(config), (config.RoadWidthM * 0.5f) + config.PavementWidthM);

        var district = new Rng(brief.Seed, DistrictStream);
        var districts = Districts.Lay(brief, config, wet, water, ref district);

        // Nothing shorter than the ground two junctions' own discs and corners take is a road at all.
        var shortestRoadM = Lattice.CorridorM(config) * 2f;
        var layout = new TownLayout(shortestRoadM, config.ArmsApartMinRad, config.CityGen.LocalityM, rules);
        var marginM = MarginM(config);
        var arterials = Arterials.Lay(layout, districts, brief, rules, shortestRoadM, marginM);
        Lattice.Lay(layout, districts, arterials, brief, wet, config, marginM);
        arterials.Close(layout, rules);
        layout.MergeTheLocalNodes();
        layout.UnpickTheCrossings(config.RoadFootprintM, RoadStage.StraysM(layout, districts, config));
        layout.KeepTheLargestComponent();
        layout.PruneTheDeadEnds();

        var shape = new Rng(brief.Seed, ShapeStream);
        var signals = new Rng(brief.Seed, SignalStream);
        var roads = RoadStage.Lay(layout, districts, brief, config, ref shape, ref signals);

        var paved = bare.With(water.Rings).With(
            roads.Roads, roads.Bridges, roads.Junctions, roads.Corners, roads.Crosswalks);
        var streets = new GroundShapes(paved, config);

        var chains = ChainsOf(roads.Roads);
        var slot = new Rng(brief.Seed, SlotStream);
        var statics = SlotStage.Lay(layout, chains, brief, streets, claims, config, roofsM, ref slot);

        var prop = new Rng(brief.Seed, PropStream);
        var built = new GroundShapes(paved.With(statics.ParkingLots), config);
        var props = PropStage.Lay(brief, chains, statics.ParkingLots, built, claims, config, ref prop);

        var spawn = new Rng(brief.Seed, SpawnStream);
        var spawns = SpawnStage.Lay(brief, statics.Buildings, statics.ParkingLots, ref spawn);

        return new CityPlan
        {
            Seed = brief.Seed,
            Name = brief.Name,
            WorldSizeM = new Vector2(brief.WidthM, brief.HeightM),
            PavementWidthM = config.PavementWidthM,
            Junctions = roads.Junctions,
            JunctionCorners = roads.Corners,

            // The pavement's own inner corners are solved against the finished ground when the town is
            // built (TER-3c.4). A generated map carries none, as the maps this build lays never have.
            PavementCorners = new CityPlan.PavementCornerArrays
            {
                CornerM = [], NormalA = [], NormalB = [], RadiusM = [],
            },
            Roads = roads.Roads,
            Bridges = roads.Bridges,
            PavedAreas = CityPlan.PavedAreaArrays.None,
            Crosswalks = roads.Crosswalks,
            StopLines = roads.StopLines,
            ParkingLots = statics.ParkingLots,
            Buildings = statics.Buildings,
            Props = props,
            Spawns = spawns,
            Water = water.Rings,
        };
    }

    /// <summary>
    /// The ground kept clear round the edge of the world: a road, its pavement and the deepest building that
    /// could stand behind it, so nothing a town lays runs off the map it is laid on.
    /// </summary>
    static float MarginM(SimConfig config) =>
        (config.RoadWidthM * 0.5f) + config.PavementWidthM + config.CityGen.BuildingSideMaxM;

    /// <summary>The roads' arcs road by road, which is how every stage after the road stage walks them.</summary>
    static ArcSeg[][] ChainsOf(CityPlan.RoadArrays roads)
    {
        var chains = new ArcSeg[roads.Count][];
        for (var road = 0; road < roads.Count; road++) chains[road] = roads.SegmentsOf(road).ToArray();
        return chains;
    }
}
