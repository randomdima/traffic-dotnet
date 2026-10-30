using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// MP-6: <b>a town drawn and never stepped shows the lights and the crossings a stepped town does</b> — every head where it
/// stands and every crossing's gear and lamps — though it lays nothing a car or a walker moves over.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P6)]
public class TownSceneTests
{
    static readonly Lazy<TrafficSimulation.CityGen.CityPlan> Railway = new(() => Towns.LayFresh(Towns.Brief(Towns.CitySeed, buildings: 120, railTracks: 2)));

    [Fact]
    public void ASceneStandsTheLightsOfItsTown()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        using var world = new TownWorld(plan, config);

        var scene = TownWorld.LayingTheScene(plan, config).Through();

        Assert.Equal(world.Heads.Heads, scene.Heads.Heads);
    }

    [Fact]
    public void ASceneStandsTheCrossingsOfItsRailway()
    {
        var config = SimConfig.Shipped();
        using var world = new TownWorld(Railway.Value, config);
        Assert.True(world.CrossingGear.Lights.Length > 0, "the railway crosses no road at grade, so nothing was asked");

        var scene = TownWorld.LayingTheScene(Railway.Value, config).Through();

        Assert.Equal(world.CrossingGear.PivotM, scene.CrossingGear.PivotM);
        Assert.Equal(world.CrossingGear.Lights, scene.CrossingGear.Lights);
    }
}
