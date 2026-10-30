using System.Diagnostics;
using TrafficSimulation.App.Debug;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.App.Drive;
using TrafficSimulation.App.Render;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Main;

/// <summary>
/// The machine the browser run stands on: a canvas, a WebGPU device already started under it, and the
/// counter that says how often the wall between them was crossed.
/// </summary>
/// <remarks>
/// This file and <c>Game.Desktop.cs</c> are the same four answers given twice (WEB-1), and the project file
/// picks which one is compiled. <b>The device is not opened here</b>: WebGPU is asked for over a
/// promise, and a constructor cannot wait, so <see cref="Boot"/> has it running before this is reached.
/// </remarks>
internal sealed partial class Game
{
    /// <summary>The page's device as the world's kernels see it, made the first time a town is handed to it.</summary>
    Vk? _kernels;

    /// <summary>
    /// Every town this page stands has its world stepped on the device (<see cref="DeviceWorld"/>), as a desktop window's
    /// is — unless <c>?managed-world</c> said otherwise, or the device cannot hold the kernels, when it is stepped here
    /// (WEB-10).
    /// </summary>
    /// <remarks>
    /// What moves is drawn off the device's own state by the desktop's emit, and the marks out of the device's ring (WEB-12).
    /// </remarks>
    public bool TicksOnTheDevice { get; init; }

    /// <summary>
    /// The device's world, made as the town is laid — unless <c>?managed-world</c> said otherwise or the device cannot hold
    /// the kernels (WEB-10), when it says so and the town stands the managed world.
    /// </summary>
    private partial DeviceWorldMaker? WorldMaker()
    {
        _handedOver = null;
        if (!TicksOnTheDevice) return null;

        if (!WebGpu.KernelsFit())
        {
            Console.WriteLine("This device cannot hold the world's kernels: the world is stepped in managed code.");
            return null;
        }

        return new DeviceWorldMaker(_kernels ??= new Vk());
    }

    partial void TookTheDeviceWorld(TownWorld town, DeviceWorld world) => SayWhereTheOpenWent(town);

    /// <summary>The page's device as the engine layers' kernels see it, where it can hold them.</summary>
    private partial Vk? EngineDevice() => WebGpu.KernelsFit() ? _kernels ??= new Vk() : null;

    /// <summary>A page has one frame in hand at a time, so one slot.</summary>
    private partial int FrameSlot() => 0;

    private partial int EngineSlots() => 1;

    /// <summary>The frame's kernels' batch, run ahead of each frame that finds the last one landed.</summary>
    ComputeBatch? _frameBatch;

    /// <summary>
    /// <b>The frame's kernels run in a batch of their own ahead of the frame</b> — the emit's, then the engine layers' —
    /// where a desktop records them into it: a page's frame is one call the kernels cannot ride. A frame that finds the last
    /// batch still out draws what it left.
    /// </summary>
    partial void RunTheFrameKernels()
    {
        var sprites = (_world?.World as DeviceWorld)?.Sprites;
        if (sprites is null && _engine is null) return;

        var batch = _frameBatch ??= new ComputeBatch(_kernels!);
        if (!batch.Landed) return;

        var commands = batch.Begin();
        sprites?.Record(commands, 0);
        _engine?.Record(commands, 0);
        batch.Submit();
    }

    /// <summary>Where a page's open went, on its console: it is the desktop's work on one thread, and nothing else measures it.</summary>
    static void SayWhereTheOpenWent(TownWorld world)
    {
        var planMs = 0.0;
        foreach (var (_, ms) in world.Plan.LaidMs) planMs += ms;
        Console.WriteLine(
            $"{world.Plan.Name}: the plan {planMs / 1000:F1} s, the town {world.StoodMs / 1000:F1} s — of it the pavement " +
            $"{world.FootMs / 1000:F1} s and the atlas {world.AtlasMs / 1000:F1} s; plan digest {world.Plan.Digest():x16}");
    }

    /// <summary>
    /// The tick a page says its bodies' digest at, once a town: a desktop's <c>--shot --seconds 10</c> prints the same
    /// figure at the same tick of the same map (WEB-1).
    /// </summary>
    const long DigestTick = 600;

    partial void Ticked(TownWorld world, long tick)
    {
        if (tick == DigestTick) Console.WriteLine($"{world.Plan.Name}: bodies digest {world.Digest():x16} at tick {tick}");
    }

    /// <summary>The handover, for a town whose watches staged it on the managed world first (<see cref="LaidTown.Lay"/>).</summary>
    partial void TickOnTheDevice(TownWorld? world)
    {
        if (world is null) return;

        SayWhereTheOpenWent(world);
        if (!TicksOnTheDevice || !WebGpu.KernelsFit()) return;

        var kernels = _kernels ??= new Vk();
        var handedAt = Stopwatch.GetTimestamp();
        _handedOver = Watched(world);
        world.HandTheWorldTo(laying => new DeviceWorld(kernels, laying));
        Console.WriteLine(
            $"the world handed to the device in {Stopwatch.GetElapsedTime(handedAt).TotalSeconds:F1} s; the page holds {HeldMiB()} MiB");
    }

    /// <summary>The host's atlas and the managed world the last handover replaced, watched without being held.</summary>
    (WeakReference Atlas, WeakReference World)? _handedOver;

    /// <summary>
    /// Whether the last handover's leavings are gone, for the line a page's open ends on: <b>a runtime that reads its stack
    /// conservatively keeps what a standing frame once pointed at</b>, and on a traced city that is a gigabyte.
    /// </summary>
    public string WhatTheHandoverLeft => _handedOver is not var (atlas, world)
        ? string.Empty
        : $"; the host's atlas {(atlas.IsAlive ? "still held" : "let go")}, the managed world {(world.IsAlive ? "still held" : "let go")}";

    /// <summary>What the handover is to let go of, watched without holding it — made in a frame of its own, so no slot of the caller's does.</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static (WeakReference Atlas, WeakReference World) Watched(TownWorld world) => (new(world.Atlas), new(world.World));

    /// <summary>The page's memory, which only grows, so the most an open has needed so far.</summary>
    static long HeldMiB() => WebGpu.HeapBytes() / (1024 * 1024);

    private partial AppWindow Boot(
        int width, int height, bool validate, float uiScale, Pacing pacing, bool fullscreen, string? display) =>
        new AppWindow(uiScale);

    /// <summary>
    /// <b>No ground either, before a town is opened.</b> A renderer laid for no sheets is the menu's
    /// (<see cref="_sheets"/>), and it draws no ground to put a surface on — which is what lets the
    /// page open on one file instead of six, since a picture in a page is a fetch and a picture on the
    /// desktop is already on the disk.
    /// </summary>
    private partial TownRenderer NewRenderer(GroundMesh mesh, SpriteRoom room) => TownRenderer.OnScreen(
        mesh, _sheets.Count == 0 ? [] : GroundMesh.SurfaceFiles(), _sheets, room);

    private partial long Crossings() => WebGpu.Crossings;

    /// <summary>
    /// <b>Nothing, in a page.</b> A second seat is an instrument driven from a file on a disk (DRV-8), and
    /// a page has neither — so nothing here opens a second target for one to look through.
    /// </summary>
    private partial BotEye? NewEye(CityPlan plan, GroundMesh ground, int widthPx, int heightPx) => null;

    /// <summary>
    /// <b>Nothing, in a page.</b> The art has to be fetched before a town can be drawn and this is the end
    /// of a frame, so the loop would be waiting on the network; the boot's own <c>await</c> drains
    /// <see cref="TakeWanted"/> instead, and the card stands over every frame drawn until it has
    /// (<see cref="Data.Art"/>).
    /// </summary>
    partial void OpenWhatWasPicked()
    {
    }

    partial void Banner(string line) => WebGpu.Say(line);

    /// <summary>The device belongs to the page and outlives the run, so there is nothing to give back.</summary>
    partial void Shutdown()
    {
    }

    /// <summary>
    /// <b>Nothing, in a page.</b> A WebAssembly memory only ever grows, so what a collection frees stays the
    /// page's whatever it compacts — and Mono has no large-object compaction mode to ask for.
    /// </summary>
    static partial void HandBackTheOpen()
    {
    }
}
