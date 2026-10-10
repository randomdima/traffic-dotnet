using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;
using static TrafficSimulation.Runtime.GpuBuffer;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>A watch over every body kept on the device</b> (<see cref="ITownDevice.WatchesTheTown"/>; <c>kernels/town_watch.comp</c>,
/// <c>town_watch_total.comp</c>): recorded behind each step of the bodies, it carries every body's runs on off what the step
/// left — how deep each is into anything, how far past its grant each car is, where each walker stands and how each is
/// told to walk, how fast each driven car goes — and hands back only the step's deepest, longest and furthest, the metres
/// driven, and what it has against the few bodies noted.
/// </summary>
/// <remarks>
/// <b>A car is read as whoever ticked it last left it</b> (<see cref="Tables.Cars"/>): the car tick writes every car it
/// takes (<c>car_out.comp</c>), and the town every car it ticked itself, laid here (<see cref="LayCar"/>) — a car left at
/// rest keeping the row its last tick by the town laid. Bodies are numbered walkers first, then cars, as the watch numbers
/// them.
/// </remarks>
internal sealed class DeviceTownWatch : IDisposable
{
    /// <summary>A car as the watch reads it (<c>WatchCarRow</c>): how far past its grant, and whether it is driven and whole.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct CarRow(float PastM, uint Driven)
    {
        /// <summary>
        /// A car read off what the town holds of it: how far past its grant — nought where the grant is infinite, which
        /// nothing cut — and whether it is driven and not wrecked.
        /// </summary>
        public static CarRow Of(float grantLeftM, bool drivenAndWhole) =>
            new(float.IsFinite(grantLeftM) ? MathF.Max(0f, -grantLeftM) : 0f, drivenAndWhole ? 1u : 0u);
    }

    /// <summary>The device's tables the watch reads, each as its own kernels keep it.</summary>
    /// <param name="OverlapBits">How deep each body is into what it touches, a float's bits a body row (<see cref="DeviceSolver.Overlap"/>).</param>
    /// <param name="Pose">Each body row's pose, its position first.</param>
    /// <param name="Motion">Each body row's motion, its velocity first.</param>
    /// <param name="WalkerBodies">Which body row each walker is.</param>
    /// <param name="CarBodies">Which body row each car is (<see cref="DeviceBodyPhase.CarBodyRow"/>).</param>
    /// <param name="Walkers">Each walker as its tick reads it, how it is told to walk among it (<see cref="DeviceWalkerTick.Table"/>).</param>
    /// <param name="Cars">Each car as the watch reads it (<see cref="CarRow"/>), which the town's rows are copied into.</param>
    internal readonly record struct Tables(
        GpuBuffer OverlapBits, GpuBuffer Pose, GpuBuffer Motion, GpuBuffer WalkerBodies, GpuBuffer CarBodies, GpuBuffer Walkers,
        GpuBuffer Cars);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct WatchPush(
        uint WalkerCount, uint CarCount, float OverlapAllowanceM, float PastAllowanceM, float MovedM, float TickSeconds);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct TotalPush(uint Groups, uint NoteCount, uint WalkerCount, uint Pad = 0u);

    /// <summary>How many bodies a step notes at most (<see cref="Note"/>).</summary>
    public const int MostNoted = 8;

    const int Group = 256;

    readonly Vk _vk;
    readonly Tables _tables;
    readonly int _walkers;
    readonly int _cars;
    readonly WatchPush _push;
    readonly List<GpuBuffer> _owned = [];
    readonly GpuBuffer _stuckTicks, _pastM, _pastTicks, _stillFromM, _stillTicks, _partials, _noted, _out;
    readonly GpuBuffer _stuckIn, _pastIn, _pastTicksIn, _stillFromIn, _stillTicksIn, _carsIn;
    readonly ComputeKernel _watch, _total;
    readonly DescriptorSet _watchSet, _totalSet;
    readonly BufferCopy[] _carRuns;
    readonly TownWatchNote[] _notes = new TownWatchNote[MostNoted];
    int _carRunCount;
    int _noteCount;
    int _notesRead;
    bool _seedDue;

    /// <param name="walkers">How many walkers the watch covers, numbered ahead of every car.</param>
    /// <param name="cars">How many cars it covers.</param>
    public DeviceTownWatch(Vk vk, in TownWatchTerms terms, float tickSeconds, int walkers, int cars, in Tables tables)
    {
        _vk = vk;
        _tables = tables;
        _walkers = walkers;
        _cars = cars;
        _push = new WatchPush((uint)walkers, (uint)cars, terms.OverlapAllowanceM, terms.PastTheGrantAllowanceM, terms.MovedM, tickSeconds);
        var bodies = walkers + cars;

        _stuckTicks = Device<uint>(bodies);
        _pastM = Device<float>(bodies);
        _pastTicks = Device<uint>(bodies);
        _stillFromM = Device<Vector2>(walkers);
        _stillTicks = Device<uint>(walkers);
        _partials = Device<TownWatchedTick>(Groups);
        _noted = Own(vk.CreateBuffer(Bytes<uint>(MostNoted), BufferUsageFlags.StorageBufferBit, HostAccess.Writes));
        _out = Own(
            vk.CreateBuffer(
                (ulong)(Unsafe.SizeOf<TownWatchedTick>() + (MostNoted * Unsafe.SizeOf<TownWatchNote>())), BufferUsageFlags.StorageBufferBit,
                HostAccess.Reads));

        _stuckIn = Staging<uint>(bodies);
        _pastIn = Staging<float>(bodies);
        _pastTicksIn = Staging<uint>(bodies);
        _stillFromIn = Staging<Vector2>(walkers);
        _stillTicksIn = Staging<uint>(walkers);
        _carsIn = Staging<CarRow>(cars);
        _carRuns = new BufferCopy[Math.Max(1, cars)];

        _watch = new ComputeKernel(vk, "town_watch.comp", 13, (uint)Marshal.SizeOf<WatchPush>());
        _watchSet = _watch.Bind(
        [
            tables.OverlapBits, tables.Pose, tables.Motion, tables.WalkerBodies, tables.CarBodies, tables.Walkers, tables.Cars,
            _stuckTicks, _pastM, _pastTicks, _stillFromM, _stillTicks, _partials,
        ]);
        _total = new ComputeKernel(vk, "town_watch_total.comp", 9, (uint)Marshal.SizeOf<TotalPush>());
        _totalSet = _total.Bind(
            [_partials, _out, _noted, tables.OverlapBits, tables.WalkerBodies, tables.CarBodies, _stuckTicks, _pastM, _pastTicks]);
    }

    /// <summary>What the last step read, once its submit was waited for (<see cref="Read"/>).</summary>
    public TownWatchedTick Watched { get; private set; }

    /// <summary>What the watch had against each body the last step noted (<see cref="Note"/>).</summary>
    public ReadOnlySpan<TownWatchNote> Notes => _notes.AsSpan(0, _notesRead);

    int Groups => Math.Max(1, (_walkers + _cars + Group - 1) / Group);

    /// <summary>The runs every body kept before the device took the watch over, taken in by the next step.</summary>
    public void Seed(TownWatchRuns runs)
    {
        if (runs.Walkers != _walkers || runs.Cars != _cars)
        {
            throw new ArgumentException($"The runs cover {runs.Walkers} walkers and {runs.Cars} cars, not {_walkers} and {_cars}.", nameof(runs));
        }

        MemoryMarshal.Cast<int, uint>(runs.StuckTicks).CopyTo(_stuckIn.Span<uint>());
        runs.PastM.CopyTo(_pastIn.Span<float>());
        MemoryMarshal.Cast<int, uint>(runs.PastTicks).CopyTo(_pastTicksIn.Span<uint>());
        runs.StillFromM.CopyTo(_stillFromIn.Span<Vector2>());
        MemoryMarshal.Cast<int, uint>(runs.StillTicks).CopyTo(_stillTicksIn.Span<uint>());
        _seedDue = true;
    }

    /// <summary>A car as the town ticked it this step, laid over the car tick's row — at most once a step.</summary>
    public void LayCar(int car, CarRow row)
    {
        _carsIn.Span<CarRow>()[car] = row;
        Run((ulong)car * (ulong)Unsafe.SizeOf<CarRow>(), (ulong)Unsafe.SizeOf<CarRow>());
    }

    /// <summary>Whether a car the car tick took this step is driven and whole, laid over its row — a wreck the town still has driven.</summary>
    public void LayDriven(int car, bool drivenAndWhole)
    {
        _carsIn.Span<CarRow>()[car] = new CarRow(0f, drivenAndWhole ? 1u : 0u);
        Run(((ulong)car * (ulong)Unsafe.SizeOf<CarRow>()) + sizeof(float), sizeof(uint));
    }

    /// <summary>The bodies the next step says what the watch has against, at most <see cref="MostNoted"/> of them.</summary>
    public void Note(ReadOnlySpan<int> bodies)
    {
        _noteCount = Math.Min(bodies.Length, MostNoted);
        MemoryMarshal.Cast<int, uint>(bodies[.._noteCount]).CopyTo(_noted.Span<uint>());
    }

    /// <summary>
    /// <b>One step's watch recorded behind the step</b> in its submit: the runs and the cars laid since copied in, every body
    /// swept, and the step's whole written for the host — read once the submit is waited for (<see cref="Read"/>).
    /// </summary>
    public void Record(CommandBuffer commands)
    {
        var vk = _vk;
        if (_seedDue)
        {
            vk.Copy(commands, _stuckIn, _stuckTicks, _stuckIn.SizeBytes);
            vk.Copy(commands, _pastIn, _pastM, _pastIn.SizeBytes);
            vk.Copy(commands, _pastTicksIn, _pastTicks, _pastTicksIn.SizeBytes);
            vk.Copy(commands, _stillFromIn, _stillFromM, _stillFromIn.SizeBytes);
            vk.Copy(commands, _stillTicksIn, _stillTicks, _stillTicksIn.SizeBytes);
            _seedDue = false;
        }

        vk.Copy(commands, _carsIn, _tables.Cars, _carRuns.AsSpan(0, _carRunCount));
        _carRunCount = 0;
        vk.AfterTransfer(commands);
        _watch.Dispatch(commands, _watchSet, (uint)Groups, _push);
        vk.BetweenKernels(commands);
        _total.Dispatch(commands, _totalSet, 1u, new TotalPush((uint)Groups, (uint)_noteCount, (uint)_walkers));
        _notesRead = _noteCount;
    }

    /// <summary>What the step recorded last read, taken once its submit has been waited for.</summary>
    public void Read()
    {
        var bytes = _out.Span<byte>();
        Watched = MemoryMarshal.Read<TownWatchedTick>(bytes);
        MemoryMarshal.Cast<byte, TownWatchNote>(bytes[Unsafe.SizeOf<TownWatchedTick>()..])[.._notesRead].CopyTo(_notes);
    }

    void Run(ulong offset, ulong bytes)
    {
        if (_carRunCount > 0)
        {
            ref var last = ref _carRuns[_carRunCount - 1];
            if (last.DstOffset + last.Size == offset)
            {
                last.Size += bytes;
                return;
            }
        }

        _carRuns[_carRunCount++] = new BufferCopy(offset, offset, bytes);
    }

    GpuBuffer Device<T>(int count) where T : unmanaged =>
        Own(_vk.CreateBuffer(Bytes<T>(count), BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit, HostAccess.None));

    GpuBuffer Staging<T>(int count) where T : unmanaged =>
        Own(_vk.CreateBuffer(Bytes<T>(count), BufferUsageFlags.TransferSrcBit, HostAccess.Writes));

    GpuBuffer Own(GpuBuffer buffer)
    {
        _owned.Add(buffer);
        return buffer;
    }

    public void Dispose()
    {
        _watch.Dispose();
        _total.Dispose();
        foreach (var buffer in _owned) buffer.Dispose();
    }
}
