using System.Numerics;
using TrafficSimulation.App.Debug;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.App.Render;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Runtime;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Debug;

/// <summary>
/// <b>The engine layers draw what the engine holds, whatever it is</b> (<see cref="EngineLayers"/>): a town with a railway
/// and trains running on it, its body table, ways and claims drawn by the kernels on lavapipe and read back.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P8)]
public class EngineLayersTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    static readonly SimConfig Config = new() { Train = new TrainFigures { FirstLeastS = 0.1f, FirstMostS = 0.5f } };

    /// <summary>Long enough for a train to be on the map and running.</summary>
    const int Ticks = 240;

    static readonly Lazy<TrafficSimulation.CityGen.CityPlan> Plan = new(() => Towns.LayFresh(Towns.Brief(seed: 3, cars: 0, railTracks: 2)));

    /// <summary>
    /// <b>Every collision layer a body in view is on is drawn in its own colour, and every kind of way in view in its
    /// own</b> — read off the table and the ways rather than off a list of kinds, so a rail vehicle and a track are drawn
    /// the day they are laid, and a kind added to either is drawn or fails here.
    /// </summary>
    [Fact]
    public void EveryLayerAndEveryKindOfWayInViewIsDrawn()
    {
        using var town = new TownWorld(Plan.Value, Config);
        town.World.KeepTheBodiesAtTheView();
        Run(town);
        Assert.True(town.Trains.Sent > 0, "no train was sent, so the picture has no rail vehicle in it");

        var (centreM, spanM) = Framing(town);
        var (ground, _, over) = Drawn(town, centreM, spanM);

        var table = town.PhysicsForInstruments!.Table;
        for (var body = 0; body < table.Count; body++)
        {
            if ((table.Flags[body] & BodyFlags.Enabled) == 0 || !Inside(table.PositionM[body], centreM, spanM)) continue;

            var layer = System.Numerics.BitOperations.TrailingZeroCount(table.Category[body]);
            Assert.True(
                over.Any(quad => quad.Colour == Theme.KindColour(layer)),
                $"body {body} is on {(CollisionLayer)table.Category[body]} and nothing is drawn in its colour");
        }

        for (var way = 0; way < town.Ways.Count; way++)
        {
            var line = town.LineOfWay(way, out _);
            if (line.Length == 0 || !Inside(line[0].StartM, centreM, spanM)) continue;

            var colour = Theme.KindColour(EngineLayers.WayKindColourFrom + (int)town.Ways.KindOf(way));
            Assert.True(ground.Any(quad => quad.Colour == colour), $"way {way}, a {town.Ways.KindOf(way)}, is not drawn");
        }
    }

    /// <summary>
    /// <b>A world on the device and a world stepped here draw the same picture</b> of one town at one tick: the same
    /// kernels over the device's own buffers and over the managed world's table as its view was written.
    /// </summary>
    [Fact]
    public void TheDeviceWorldAndTheManagedWorldDrawTheSameQuads()
    {
        using var managed = new TownWorld(Plan.Value, Config);
        using var onTheDevice = new TownWorld(Plan.Value, Config);
        onTheDevice.HandTheWorldTo(laying => new DeviceWorld(device.Vk, laying));
        managed.World.KeepTheBodiesAtTheView();
        onTheDevice.World.KeepTheBodiesAtTheView();
        Run(managed);
        Run(onTheDevice);

        var (centreM, spanM) = Framing(managed);
        var fromManaged = Drawn(managed, centreM, spanM);
        var fromDevice = Drawn(onTheDevice, centreM, spanM);

        foreach (var (one, other, run) in (ReadOnlySpan<(OverlayQuad[], OverlayQuad[], string)>)
                 [(fromManaged.Ground, fromDevice.Ground, "ground"), (fromManaged.Over, fromDevice.Over, "over")])
        {
            Assert.NotEmpty(one);
            Assert.Equal(Sorted(one), Sorted(other));
        }
    }

    /// <summary>
    /// <b>A bridge's ways are drawn over its deck</b> (TER-7b, PHY-1a), asked of one street carried over another: the run
    /// drawn over the bridges holds the bridge's ways and no way of the street under it, whose lanes cross the bridge's in
    /// plan.
    /// </summary>
    [Fact]
    public void ABridgesWaysAreDrawnOverItsDeck()
    {
        using var town = new TownWorld(CityGen.Traced.TracedPlanTests.OverAStreet(), SimConfig.Shipped());

        AssertAllOnTheBridge(town, DrawnAbove(town, DebugLayer.Nodes));
    }

    /// <summary><b>The ground a car on a bridge holds is drawn over its deck</b>, and none a car under it holds.</summary>
    [Fact]
    public void AClaimOnABridgeIsDrawnOverItsDeck()
    {
        using var town = new TownWorld(CityGen.Traced.TracedPlanTests.OverAStreet(), SimConfig.Shipped());
        town.LayTheClaims();

        AssertAllOnTheBridge(town, DrawnAbove(town, DebugLayer.Claims));
    }

    /// <summary>
    /// <b>Every slot of a swapchain draws the frame</b>: an engine laid for three images, as a window's is, draws the same
    /// quads in each as an engine laid for one — a set of each kernel per slot, and not one between them.
    /// </summary>
    [Fact]
    public void EverySlotOfASwapchainDrawsTheFrame()
    {
        using var town = new TownWorld(Plan.Value, Config);
        town.World.KeepTheBodiesAtTheView();
        Run(town);
        var (centreM, spanM) = Framing(town);
        var one = Drawn(town, centreM, spanM);

        const int images = 3;
        using var engine = new EngineLayers(device.Vk, town, images);
        for (var slot = 0; slot < images; slot++)
        {
            var drawn = Drawn(engine, slot, centreM, spanM, AllLayers());
            Assert.Equal(Sorted(one.Over), Sorted(drawn.Over));
        }
    }

    OverlayQuad[] DrawnAbove(TownWorld town, DebugLayer layer)
    {
        var plan = town.Plan;
        var bridge = plan.Roads.SegmentsOf(plan.Bridges.Road[0]);
        var centreM = (bridge[0].StartM + bridge[^1].EndM) * 0.5f;
        return Drawn(town, centreM, new Vector2(800f), layer).Above;
    }

    /// <summary>Every quad stands on the bridge's own road — between its two ends and inside its width — and there is one.</summary>
    static void AssertAllOnTheBridge(TownWorld town, OverlayQuad[] drawn)
    {
        var plan = town.Plan;
        var bridge = plan.Bridges.Road[0];
        var arcs = plan.Roads.SegmentsOf(bridge);
        var fromX = MathF.Min(arcs[0].StartM.X, arcs[^1].EndM.X);
        var toX = MathF.Max(arcs[0].StartM.X, arcs[^1].EndM.X);
        var halfWidthM = plan.Roads.WidthM[bridge] * 0.5f;

        Assert.NotEmpty(drawn);
        foreach (var quad in drawn)
        {
            Assert.InRange(quad.Centre.X, fromX, toX);
            Assert.InRange(quad.Centre.Y, arcs[0].StartM.Y - halfWidthM, arcs[0].StartM.Y + halfWidthM);
        }
    }

    static void Run(TownWorld town)
    {
        var loop = new SimLoop<TownWorld>(town, Config);
        loop.Advance(Ticks);
    }

    /// <summary>A view over the first rail vehicle in the world, two hundred metres across.</summary>
    static (Vector2 CentreM, Vector2 SpanM) Framing(TownWorld town)
    {
        foreach (ref readonly var wagon in town.World.View.Wagons)
        {
            if (wagon.IsInWorld) return (wagon.PositionM, new Vector2(200f));
        }

        throw new InvalidOperationException("no rail vehicle is in the world");
    }

    static bool Inside(Vector2 atM, Vector2 centreM, Vector2 spanM)
    {
        var off = Vector2.Abs(atM - centreM);
        return off.X <= spanM.X * 0.5f && off.Y <= spanM.Y * 0.5f;
    }

    /// <summary>
    /// The engine layers with <paramref name="layers"/> on — every layer of theirs where none is named — emitted for one
    /// view and read back run by run.
    /// </summary>
    (OverlayQuad[] Ground, OverlayQuad[] Above, OverlayQuad[] Over) Drawn(
        TownWorld town, Vector2 centreM, Vector2 spanM, params ReadOnlySpan<DebugLayer> layers)
    {
        using var engine = new EngineLayers(device.Vk, town);
        return Drawn(engine, 0, centreM, spanM, layers.IsEmpty ? AllLayers() : layers);
    }

    static ReadOnlySpan<DebugLayer> AllLayers() => [DebugLayer.Collision, DebugLayer.Nodes, DebugLayer.Claims, DebugLayer.AgentLines];

    /// <summary>One slot of <paramref name="engine"/>'s frame, emitted for one view and read back run by run.</summary>
    (OverlayQuad[] Ground, OverlayQuad[] Above, OverlayQuad[] Over) Drawn(
        EngineLayers engine, int slot, Vector2 centreM, Vector2 spanM, ReadOnlySpan<DebugLayer> layers)
    {
        var vk = device.Vk;
        var switches = new DebugSwitches();
        foreach (var layer in layers) switches.Toggle(layer);

        engine.Write(slot, new EngineView(centreM, spanM, PixelsPerMetre: 8f, PointerM: centreM, Picks: false), switches);
        using var quads = vk.CreateBuffer(engine.Quads.SizeBytes, BufferUse.TransferDst, HostAccess.Reads);
        using var draws = vk.CreateBuffer(engine.Draws.SizeBytes, BufferUse.TransferDst, HostAccess.Reads);
        using var batch = new ComputeBatch(vk);
        var commands = batch.Begin();
        engine.Record(commands, slot);
        vk.BeforeTransfer(commands);
        vk.Copy(commands, engine.Quads, quads, engine.Quads.SizeBytes);
        vk.Copy(commands, engine.Draws, draws, engine.Draws.SizeBytes);
        batch.SubmitAndWait();

        return (RunOf(EngineRun.Ground), RunOf(EngineRun.Above), RunOf(EngineRun.Over));

        OverlayQuad[] RunOf(EngineRun run) =>
            quads.Span<OverlayQuad>().Slice((int)run * engine.RunRoom, (int)draws.Span<uint>()[((int)run * 4) + 1]).ToArray();
    }

    /// <summary>A run in an order of its own: the kernels append from many threads, so the order is the device's.</summary>
    static OverlayQuad[] Sorted(OverlayQuad[] quads) =>
        [.. quads.OrderBy(quad => quad.Centre.X).ThenBy(quad => quad.Centre.Y).ThenBy(quad => quad.Rotation).ThenBy(quad => quad.HalfSize.X)];
}
