using TrafficSimulation.App.Render;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Statics;
using Xunit;

namespace TrafficSimulation.Tests.Render;

/// <summary>
/// MP-9: <b>a player's page packs every sheet it draws</b> — every roof and prop of its town — however many of the
/// catalogue's it leaves out.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P5)]
public class SheetsAsShownTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    [Theory]
    [InlineData(Towns.Fixture)]
    [InlineData(Towns.City)]
    public void EverySheetAStandingSpriteDrawsIsPacked(string map)
    {
        var plan = Towns.Of(map);
        var uses = BuildingUses.Of(plan);
        var looks = TownSprites.Load(Config);
        looks.Lay(plan, uses, Config);

        var shown = looks.SheetsAsShown(plan, uses);

        foreach (var instance in looks.Standing.Instances.Span) Assert.False(shown[instance.Sheet].Unused, $"sheet {instance.Sheet} is drawn and not packed");
    }
}
