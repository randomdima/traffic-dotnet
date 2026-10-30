using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.App.Render;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.Tests.World;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>
/// PHY-1b on the device: <b>a walker on a deck over other roads is drawn with the deck</b> — in the run above, over the
/// deck, and not under it in the run over the ground (<c>sprites_count.comp</c>, <c>sprites_write.comp</c>).
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P6)]
public class DeviceDeckWalkerTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    static readonly SimConfig Config = SimConfig.Shipped();

    [Fact]
    public void AWalkerOnADeckIsDrawnInTheRunAbove()
    {
        var plan = DeckEdgesInATownTests.Plan;
        using var town = new TownWorld(plan, Config) { HoldAgents = true };
        town.HandTheWorldTo(laying => new DeviceWorld(device.Vk, laying));
        var world = (DeviceWorld)town.World;
        var looks = TownSprites.Load(Config);
        looks.ReadAspects(SheetAtlas.Pack(looks.Sheets));
        var sprites = world.DrawWith(town, looks, looks.RoomFor(plan, Config));
        const int Person = 0;
        var atM = DeckEdgesInATownTests.DeckQuarterM;
        town.StandTheWalkerAt(Person, atM, 0f);
        town.PutTheWalkerOnTheChannels(Person, CityPlan.RoadArrays.ChannelOf(CityPlan.RoadArrays.Over));
        new SimLoop<TownWorld>(town, Config).Advance(SimFigures.WorldStepsAhead + 2);

        var spanM = plan.WorldSizeM;
        sprites.Emit(town, spanM * 0.5f, spanM, 1600f / spanM.X);

        Assert.Equal((0, 1), (Walkers(sprites.Over, looks), Walkers(sprites.Above, looks)));
    }

    static int Walkers(ReadOnlySpan<SpriteInstance> run, TownSprites looks)
    {
        var walkers = 0;
        foreach (ref readonly var instance in run) walkers += instance.Sheet < looks.People.SheetCount ? 1 : 0;
        return walkers;
    }
}
