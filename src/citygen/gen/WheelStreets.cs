using System.Numerics;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.CityGen.Zones;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>A wheel's streets, laid off its zones</b> (GEN-1, GEN-58): the hub, the spokes and the orbital its whole map's
/// zone says, a lattice in each district its zones say, settled, joined, pruned, opened out and directed — and the car
/// parks off what that leaves — each stage once and in the one order they can run in.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every stage runs in a single pass and nothing is ever laid and taken back</b> (GEN-10). No candidate town is
/// generated and rejected, and no stage retries: what makes that possible is that each stage constrains the
/// next rather than checking it afterwards — the water stands before a node is placed, the districts are
/// convex so their streets mostly cannot cross, the arterials carry a node wherever a street meets one, a
/// building claims its own padding as it is stood, and the props take what is left. Where the ground cannot
/// afford what the map asked for, the town is what fitted and the census reports the shortfall.
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
/// </remarks>
internal static class WheelStreets
{
    /// <summary>The streets a wheel came out with, its car parks among them, and the districts they were laid in.</summary>
    internal sealed record Laid(RoadStage.Laid Roads, CarParks.Laid CarParks, Districts Districts);

    /// <param name="water">The water the town stands on, which no node is placed in and only a bridge crosses.</param>
    /// <param name="bridgeable">Whether that water is a river, which a road may span (GEN-14b).</param>
    /// <param name="sizes">The service buildings' footprints, which say how far across its rank a yard reaches.</param>
    /// <param name="took">Told each stage's name as it finishes.</param>
    public static Laid Lay(
        TownMap.ZoneArrays zones, ulong seed, Vector2 worldSizeM, CityPlan.WaterArrays water, bool bridgeable, SimConfig config,
        BuildingSizes sizes, Action<string> took)
    {
        // <b>The ground, asked about while the town is still being laid.</b> Every stage below reads it to
        // decide where a thing may stand, and it is the same reading the finished map answers with — the
        // shapes laid so far and nothing else (TER-7).
        var wet = new GroundShapes(GroundPieces.None(seed, worldSizeM, config.PavementWidthM).With(water), config);
        var rules = new WaterRules(
            wet, config.Terrain.GroundStepM, bridgeable, config.CityGen.BridgeDeckLongestM,
            Lattice.CorridorM(config), (config.RoadWidthM * 0.5f) + config.PavementWidthM);
        var districts = Districts.Of(zones);
        took("districts");

        // Nothing shorter than the ground two junctions' own discs and corners take is a road at all.
        var shortestRoadM = Lattice.CorridorM(config) * 2f;

        // <b>The line a link would be laid as is what says whether it is a road</b> (GEN-10,
        // <see cref="RoadLines"/>), so the geometry is handed to the layout rather than run over it
        // afterwards: every road the layout holds is one that could be drawn where it stands.
        var shapes = new RoadLines(seed, config, districts, worldSizeM, rules);
        var layout = new TownLayout(
            shortestRoadM, config.ArmsApartMinRad, config.CityGen.LocalityM, config.Grid.Main, rules, shapes);
        var marginM = MarginM(config);

        // <b>Every node the town will have is placed before its first road is laid</b>, which is what lets
        // every road be laid once (GEN-10): the arterials carry the nodes the streets hang off them, so they
        // are closed only once the lattice has placed those — and laid before the streets, so a street
        // offered against ground an arterial holds is the one refused (GEN-49).
        var arterials = Arterials.Lay(layout, districts, worldSizeM, rules, shortestRoadM, marginM);
        var lattice = Lattice.Place(layout, districts, arterials, seed, worldSizeM, wet, config, marginM);
        took("nodes");

        // <b>And the nodes are settled between the placing and the laying</b> (GEN-16): every cluster standing
        // inside a locality of itself is one junction before a single road is drawn, so nothing is merged
        // afterwards, no road is drawn twice, and the ground bound holds from the first of them (GEN-49).
        var moved = layout.SettleTheNodes();
        took("settle");
        arterials.TheNodesMoved(moved);
        arterials.Close(layout, rules);
        Lattice.Lay(layout, lattice, moved);
        took("streets");

        layout.KeepTheLargestComponent();
        layout.PruneTheDeadEnds();
        took("prune");
        ThroughRoads.Lay(layout, config);
        took("through");
        Roundabouts.Lay(layout, districts, rules, config, worldSizeM, marginM);
        took("roundabouts");
        OneWayStreets.Lay(layout, config);
        took("one-way");

        // <b>And the car parks are laid off the kerbs of what that leaves</b> (GEN-53), which is the last thing
        // done to a layout: a bay's arms are its line's own, so nothing may be offered to the layout after one.
        var planned = (int)(zones.Own(TownMap.ZoneArrays.Root, ZoneParam.Buildings) ?? 0f);
        var carPark = new Rng(seed, Streams.CarPark);
        var carParks = CarParks.Lay(layout, planned, config, districts.Wheel, sizes, ref carPark);
        took("car parks");

        var signals = new Rng(seed, Streams.Signal);
        var unregulated = zones.Own(TownMap.ZoneArrays.Root, ZoneParam.UnregulatedShare) ?? 0f;
        var roads = RoadStage.Lay(layout, unregulated, config, carParks, ref signals);
        took("roads");

        return new Laid(roads, carParks, districts);
    }

    /// <summary>
    /// The ground kept clear round the edge of the world: a road and the walk beside it, so nothing a town
    /// lays runs off the map it is laid on.
    /// </summary>
    static float MarginM(SimConfig config) => (config.RoadWidthM * 0.5f) + config.PavementWidthM;
}
