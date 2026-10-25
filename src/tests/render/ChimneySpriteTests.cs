using System.Numerics;
using TrafficSimulation.App.Render;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Statics;
using Xunit;

namespace TrafficSimulation.Tests.Render;

/// <summary>
/// OBJ-7: <b>a chimney is the pots its stack carries</b> (<see cref="ChimneySprites.Pots"/>) — every chimney can smoke, and
/// whether it is burning is the stack's, so every pot of one carries the one fire.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P9)]
public class ChimneySpriteTests
{
    [Fact]
    public void EveryPotOfAStackIsLaidAndCarriesItsStacksFire()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Built;
        var catalogue = BuildingCatalog.Shared;
        var uses = BuildingUses.Of(plan);
        var fires = new Dictionary<Vector2, uint>();
        foreach (var row in ChimneySprites.Pots(plan, catalogue, uses, config.Chimneys)) fires[row.AtM] = ChimneySprites.FireOf(row);

        var twins = 0;
        for (var building = 0; building < plan.Buildings.Count; building++)
        {
            var roof = BuildingRoofs.Of(plan, catalogue, uses, building);
            var chimneys = catalogue.Variants[roof.Variant].ChimneysM;
            for (var chimney = 0; chimney < chimneys.Length; chimney++)
            {
                var first = ChimneySprites.PotAtM(plan, catalogue, roof, building, chimney, 0);
                Assert.True(fires.ContainsKey(first), $"building {building}'s chimney {chimney} is not laid");

                for (var pot = 1; pot < chimneys[chimney].Length; pot++)
                {
                    var at = ChimneySprites.PotAtM(plan, catalogue, roof, building, chimney, pot);
                    Assert.True(
                        fires.TryGetValue(at, out var fire) && fire == fires[first],
                        $"building {building}'s chimney {chimney}'s pot {pot} does not burn with its stack");
                }

                twins += chimneys[chimney].Length > 1 ? 1 : 0;
            }
        }

        Assert.True(twins > 0, "no stack of more than one pot was laid, so nothing was asked of one");
    }
}
