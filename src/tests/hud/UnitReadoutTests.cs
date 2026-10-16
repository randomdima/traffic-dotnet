using System.Numerics;
using TrafficSimulation.App.Hud;
using TrafficSimulation.Bench;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Hud;

/// <summary>
/// <b>What a row of the read-out says about one car a hand is or is not on</b> (OBS-2m, CTL-5): each row is about the
/// unit it names, and a reading the car has no figure for says so rather than quoting a nought.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P7)]
public class UnitReadoutTests
{
    static readonly HandInput Held = new(Held: true, Throttle: 0f, Steer: 0f, Handbrake: true, WalkDirection: Vector2.Zero);

    /// <summary>
    /// A car in the hand says so, and that it is read no grant (S-7) — so the road ahead of it is not quoted as its own.
    /// </summary>
    [Fact]
    public void ACarInTheHandIsReadNoGrant()
    {
        using var world = new TownWorld(Towns.Of(Towns.Fixture), SimConfig.Shipped());
        var car = new Selection(SelectionKind.Car, 0);
        world.Select(car);
        world.Hands(Held);

        Assert.Equal("in hand", Row(world, car, "wheel"));
        Assert.Equal("no grant, driven by hand", Row(world, car, "room"));
    }

    /// <summary>A wreck under the hand is refused it (CTL-5), and its wheel row says that rather than that it is held.</summary>
    [Fact]
    public void AWreckInTheHandsSelectionIsRefusedIt()
    {
        using var world = new TownWorld(Towns.Of(Towns.Fixture), SimConfig.Shipped());
        var car = new Selection(SelectionKind.Car, 0);
        world.BreakWhereItStands(car.Index);
        world.Select(car);
        world.Hands(Held);

        Assert.Equal("refused, wrecked", Row(world, car, "wheel"));
    }

    /// <summary>
    /// A car with no line — a wreck's, or one a hand drove off it — has nothing to be off, and says so rather than
    /// reading as dead on it.
    /// </summary>
    [Fact]
    public void ACarWithNoLineSaysSo()
    {
        using var world = new TownWorld(Towns.Of(Towns.Fixture), SimConfig.Shipped());
        var car = new Selection(SelectionKind.Car, 0);
        world.Cars.SetLine(car.Index, default);

        Assert.Equal("no line", Row(world, car, "off line"));
    }

    /// <summary>The figure of the row of that name, or null where none was written.</summary>
    static string? Row(TownWorld world, Selection unit, string name)
    {
        var text = new char[UnitReadout.MostRows * UnitReadout.RoomPerRow];
        var ends = new int[UnitReadout.MostRows];
        var count = UnitReadout.Lines(world, unit, ReadOnlySpan<ScenarioWatch>.Empty, text, ends);
        for (var row = 0; row < count; row++)
        {
            var start = row == 0 ? 0 : ends[row - 1];
            var line = new string(text, start, ends[row] - start);
            if (line[..UnitReadout.ValueColumn].TrimEnd() == name) return line[UnitReadout.ValueColumn..];
        }

        return null;
    }
}
