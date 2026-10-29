using System.Numerics;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.App.Render;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>
/// PER-31 on the device: <b>the walkers head the over run in the order they are drawn</b> (<c>sprites_order.comp</c>) —
/// each after every one it ranks after (<see cref="PersonSprites.Depth"/>), however the roster numbers them.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P6)]
public class DeviceWalkerOrderTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>Long enough for the town's people to be out on its pavements.</summary>
    const int Ticks = 600;

    [Fact]
    public void TheWalkersShownAreWrittenInTheOrderDrawn()
    {
        var plan = Towns.Built;
        using var town = new TownWorld(plan, Config);
        town.HandTheWorldTo(laying => new DeviceWorld(device.Vk, laying));
        var world = (DeviceWorld)town.World;
        var looks = TownSprites.Load(Config);
        looks.ReadAspects(SheetAtlas.Pack(looks.Sheets));
        var sprites = world.DrawWith(town, looks, looks.RoomFor(plan, Config));
        var loop = new SimLoop<TownWorld>(town, Config);
        for (var tick = 1; tick <= Ticks; tick++) loop.Advance();

        var spanM = plan.WorldSizeM;
        sprites.Emit(town, spanM * 0.5f, spanM, 1600f / spanM.X);

        var over = sprites.Over;
        var walkers = 0;
        while (walkers < over.Length && IsWalker(over[walkers], looks)) walkers++;
        Assert.True(walkers >= 2, $"{walkers} walkers shown: nothing to order");

        for (var at = 1; at < walkers; at++)
        {
            var behind = PersonSprites.Depth.Of(over[at - 1], looks.FirstDownSheet);
            var drawn = PersonSprites.Depth.Of(over[at], looks.FirstDownSheet);
            Assert.True(behind.CompareTo(drawn) <= 0, $"walker {at} ({drawn}) is drawn after one it ranks before ({behind})");
        }
    }

    static bool IsWalker(in SpriteInstance instance, TownSprites looks) =>
        instance.Sheet < looks.People.SheetCount
        || (instance.Sheet >= looks.FirstDownSheet && instance.Sheet < looks.FirstDownSheet + looks.People.SheetCount);
}
