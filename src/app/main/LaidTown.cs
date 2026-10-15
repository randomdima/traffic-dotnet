using TrafficSimulation.App.Render;
using TrafficSimulation.Bench;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Main;

/// <summary>
/// A town laid but not standing: the whole of opening a map that touches neither the device nor the town
/// already up. It is the seconds of an open, and it is here rather than in <see cref="Game"/> so that a
/// desktop run can spend them off the loop's thread (<c>Game.Desktop.cs</c>).
/// </summary>
/// <remarks>
/// <b>Nothing it reads is written by a run in progress</b> — the plan, the ground and the world are all
/// new, and the catalogues it reads them out of are immutable — which is what makes laying one on
/// another thread safe while the town on screen keeps ticking. What is left for the loop is
/// <see cref="Game.Stand"/>: the renderer, the sheets and the standing sprites, which belong to the
/// device and to the town being replaced.
/// </remarks>
internal sealed class LaidTown
{
    LaidTown(CityPlan plan, GroundMesh ground, TownWorld world, ScenarioWatch[] scenario, bool worldHandedOver)
    {
        Plan = plan;
        Ground = ground;
        World = world;
        Scenario = scenario;
        WorldHandedOver = worldHandedOver;
    }

    /// <summary>Whether the world was handed over as the town was laid, so standing it hands nothing over.</summary>
    public bool WorldHandedOver { get; }

    public CityPlan Plan { get; }

    public GroundMesh Ground { get; }

    public TownWorld World { get; }

    /// <summary>
    /// What the town claims about itself, built with it rather than once it is running: a watch that
    /// stages its map's own setup has to be there for the first tick (<see cref="Scenarios.For"/>).
    /// </summary>
    public ScenarioWatch[] Scenario { get; }

    /// <param name="worldMaker">
    /// The device's world, made as the town is laid where the caller is the thread that may (<see cref="IWorldMaker"/>): the
    /// atlas filed straight into it, and no managed world stood to be thrown away — on a traced city a gigabyte off the
    /// open's peak, which a page keeps for its whole run (root decision-log.md, 2026-10-15). <b>Not for a map whose
    /// watches stage it</b> (<see cref="Scenarios.Stages"/>): that one is handed over after.
    /// </param>
    /// <param name="handTheWorldOver">
    /// The world handed to the device's after the scenario has staged the town, where it is to be: <b>between the world and
    /// the ground</b>, so the ground is laid into what the host's atlas held.
    /// </param>
    public static LaidTown Lay(
        string map, SimConfig config, IWorldMaker? worldMaker = null, Action<TownWorld>? handTheWorldOver = null) =>
        Laying(map, config, worldMaker, handTheWorldOver).Through();

    /// <summary>
    /// <see cref="Lay"/> a stage at a time: the plan's own stages (<see cref="Maps.Planning"/>), then the town, the
    /// handover and the ground, each one block.
    /// </summary>
    public static Staged<LaidTown> Laying(
        string map, SimConfig config, IWorldMaker? worldMaker = null, Action<TownWorld>? handTheWorldOver = null) =>
        new(made => Laying(map, config, worldMaker, handTheWorldOver, made));

    static IEnumerable<string> Laying(
        string map, SimConfig config, IWorldMaker? worldMaker, Action<TownWorld>? handTheWorldOver, Action<LaidTown> laid)
    {
        var planning = Maps.Planning(map, config, BuildingCatalog.Roofs);
        foreach (var stage in planning.Stages) yield return stage;

        var plan = planning.Made;

        // The world before the ground, because laying the world's atlas is an open's peak and the mesh laid
        // first stood under it; laid after, it is laid into what the atlas worked in.
        var made = worldMaker is not null && !Scenarios.Stages(plan.Name);
        var world = new TownWorld(plan, config, worldMaker: made ? worldMaker : null);
        var scenario = Scenarios.For(world, config);
        yield return OpenWatch.Passed("the town's watches");

        var handed = !made && handTheWorldOver is not null;
        if (handed) handTheWorldOver!(world);

        // From here, a frame that never held what the lay and the handover let go of (TownWorld.CollectWhatTheLayLeft).
        if (made || handed)
        {
            world.CollectWhatTheLayLeft();
            yield return OpenWatch.Passed("the world the device's, and collected");
        }

        var ground = GroundMesh.Build(plan, config);
        plan.Paving(config).LetGoOfTheLayingIndexes();
        world.Roads.LetGoOfTheLayingIndex();
        yield return OpenWatch.Passed("the ground");
        laid(new LaidTown(plan, ground, world, scenario, made || handed));
    }
}
