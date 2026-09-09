using TrafficSimulation.App.Render;
using TrafficSimulation.Bench;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
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
    LaidTown(CityPlan plan, GroundMesh ground, TownWorld world, ScenarioWatch[] scenario)
    {
        Plan = plan;
        Ground = ground;
        World = world;
        Scenario = scenario;
    }

    public CityPlan Plan { get; }

    public GroundMesh Ground { get; }

    public TownWorld World { get; }

    /// <summary>
    /// What the town claims about itself, built with it rather than once it is running: a watch that
    /// stages its map's own setup has to be there for the first tick (<see cref="Scenarios.For"/>).
    /// </summary>
    public ScenarioWatch[] Scenario { get; }

    public static LaidTown Lay(string map, SimConfig config)
    {
        var plan = Maps.Plan(map, config, BuildingCatalog.Shared.OrdinaryFootprintsM());
        var ground = GroundMesh.Build(plan, config);
        var world = new TownWorld(plan, config);
        return new LaidTown(plan, ground, world, Scenarios.For(world, config));
    }
}
