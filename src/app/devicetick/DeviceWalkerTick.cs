using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The town's walkers ticked on the device</b> (<c>world/town/kernels/walkers_tick.comp</c>): each walker's own tick and a
/// casualty's slide, over a walker table the device keeps from one tick to the next, leaving each walker's impulse where
/// the bodies' phase spends it and reads the walker back into the table after the step (<see cref="DeviceBodyPhase"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Run in shadow</b> (<see cref="Lay"/>, <see cref="Disagreement"/>): the town still ticks its walkers, and the
/// device is handed every walker as the town's tick reads it. <see cref="TickMs"/> is the kernel alone.
/// </para>
/// <para>
/// <b>Resident, for a town whose walkers the device ticks</b> (<see cref="LayWhatChanged"/>): the table is the device's, and
/// only a walker whose row or impulse the town holds other than the device is known to hold is written over it, whole
/// (<c>walkers_patch.comp</c>); what comes back is each walker's declared pace, impulse and heading
/// (<see cref="TicksAfter"/>), and where its body's step left it is learnt from the bodies' phase (<see cref="KnowTheStep"/>).
/// </para>
/// </remarks>
internal sealed partial class DeviceWalkerTick : IDisposable
{
    /// <summary>A walker as the device keeps it (<c>WalkerTableRow</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct WalkerRow(
        Vector2 PositionM, Vector2 VelocityMps, Vector2 DeclaredMps, Vector2 AimM, float HeadingRad, float MassKg, uint Flags,
        float DistanceWalkedM)
    {
        public const uint Walking = 1u, Wounded = 2u, Inside = 4u, Paused = 8u;
    }

    /// <summary>What the town reads of a walker's tick (<c>walkers_tick.comp</c>'s <c>WalkerTickOut</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct TickOut(Vector2 DeclaredMps, Vector2 ImpulseNs, float HeadingRad, float Pad = 0f);

    /// <summary>One walker written whole over the device's table (<c>walkers_patch.comp</c>'s <c>WalkerPatch</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct Patch(WalkerRow Row, Vector2 ImpulseNs, uint Walker, uint Pad = 0u);

    /// <summary>The kernel's figures, field for field with <c>walkers_tick.comp</c>'s, each the host's own.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct Figures(
        uint WalkerCount, float DtS, float WalkSpeedMps, float TurnRateDegPerS, float FootGripMps2, float SlidingGripMps2)
    {
        public static Figures Of(SimConfig config) => new(
            0u, config.TickSeconds, config.PersonWalkSpeedMps, config.PersonTurnRateDegPerS, config.PersonFootGripMps2,
            config.PersonSlidingGripMps2);
    }

    readonly Vk _vk;
    readonly SimConfig _config;
    readonly Figures _figures;
    readonly ResidentRows<WalkerRow> _walkers;
    readonly ResidentRows<Vector2> _impulses;
    readonly ResidentRows<int> _bodies;
    readonly ResidentRows<TickOut> _ticksOut;
    readonly ComputeKernel _kernel;
    readonly DescriptorSet _set;
    readonly GpuTimer _timer;
    int _count;

    /// <summary>Resident: the patches staged and on the device, and the kernel that writes them.</summary>
    readonly GpuBuffer? _patchesIn, _patches;

    readonly ComputeKernel? _patchKernel;
    readonly DescriptorSet _patchSet;
    int _patchCount;

    /// <summary>Resident: every walker's row and impulse as the device is known to hold them, and whether it is known at all.</summary>
    readonly WalkerRow[] _known = [];

    readonly Vector2[] _knownImpulse = [];
    readonly bool[] _isKnown = [];

    /// <summary>Checked: every walker as the town laid it this tick, and the table as the tick read it, copied back.</summary>
    WalkerRow[]? _laidForCheck;

    GpuBuffer? _readForCheck;
    bool _checks;

    /// <param name="resident">Whether the table is the device's, written over only where the town changed a walker.</param>
    public DeviceWalkerTick(Vk vk, SimConfig config, int capacity, bool resident = false)
    {
        _vk = vk;
        _config = config;
        _figures = Figures.Of(config);
        Capacity = capacity;
        Resident = resident;
        _walkers = new ResidentRows<WalkerRow>(vk, capacity, 1, readsBack: true);
        _impulses = new ResidentRows<Vector2>(vk, capacity, 1, readsBack: true);
        _bodies = new ResidentRows<int>(vk, capacity, 1, hostOnly: true);
        _ticksOut = new ResidentRows<TickOut>(vk, capacity, 1, readsBack: true);
        _kernel = new ComputeKernel(vk, "walkers_tick.comp", 3, (uint)Marshal.SizeOf<Figures>());
        _set = _kernel.Bind([_walkers.Device, _impulses.Device, _ticksOut.Device]);
        _timer = new GpuTimer(vk, 2);
        if (!resident) return;

        var bytes = (ulong)(Math.Max(1, capacity) * Marshal.SizeOf<Patch>());
        _patchesIn = vk.CreateBuffer(bytes, BufferUsageFlags.TransferSrcBit, HostAccess.Writes);
        _patches = vk.CreateBuffer(bytes, BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit, HostAccess.None);
        _patchKernel = new ComputeKernel(vk, "walkers_patch.comp", 3, sizeof(uint));
        _patchSet = _patchKernel.Bind([_patches, _walkers.Device, _impulses.Device]);
        _known = new WalkerRow[capacity];
        _knownImpulse = new Vector2[capacity];
        _isKnown = new bool[capacity];
    }

    /// <summary>The most walkers the table holds.</summary>
    public int Capacity { get; }

    /// <summary>Whether the table is the device's, written over only where the town changed a walker (<see cref="LayWhatChanged"/>).</summary>
    public bool Resident { get; }

    /// <summary>How many walkers the last tick was run for.</summary>
    public int Count => _count;

    /// <summary>The walker table, for the bodies' phase that spends its impulses and reads it back.</summary>
    public GpuBuffer Table => _walkers.Device;

    /// <summary>Each walker's impulse, for the bodies' phase that spends it.</summary>
    public GpuBuffer ImpulseTable => _impulses.Device;

    /// <summary>Which body each walker is, for the bodies' phase.</summary>
    public GpuBuffer BodyTable => _bodies.Device;

    /// <summary>Every walker as the last read left it — not resident.</summary>
    public ReadOnlySpan<WalkerRow> WalkersAfter => _walkers.AllRead[.._count];

    /// <summary>Every walker's impulse as the last read left it — not resident.</summary>
    public ReadOnlySpan<Vector2> ImpulsesAfter => _impulses.AllRead[.._count];

    /// <summary>Resident: what every walker's last tick declared, left and faced, as the last read handed it back.</summary>
    public ReadOnlySpan<TickOut> TicksAfter => _ticksOut.AllRead[.._count];

    /// <summary>The impulse a walker's last tick left on the device, however it was handed back.</summary>
    public Vector2 ImpulseAfter(int walker) => Resident ? _ticksOut.AllRead[walker].ImpulseNs : _impulses.AllRead[walker];

    /// <summary>How many walkers the last tick was written over whole, resident.</summary>
    public int Patched => _patchCount;

    /// <summary>
    /// <b>Resident: whether to hold the table, as every tick reads it, to every walker as the town laid it that tick</b>
    /// (<see cref="TickReadUnlike"/>) — a check of what the device is known to hold, at the cost of the table copied back
    /// every tick.
    /// </summary>
    public bool ChecksWhatTheTickReads
    {
        get => _checks;
        set
        {
            _checks = value;
            if (!value || _readForCheck != null) return;

            _laidForCheck = new WalkerRow[Capacity];
            _readForCheck = _vk.CreateBuffer(
                (ulong)(Math.Max(1, Capacity) * Marshal.SizeOf<WalkerRow>()), BufferUsageFlags.TransferDstBit, HostAccess.Reads);
        }
    }

    /// <summary>How many walkers a tick read other than the town laid them, but for how far each has walked, over every tick checked.</summary>
    public long TickReadUnlike { get; private set; }

    /// <summary>The device's own time for the last tick's kernel, in milliseconds.</summary>
    public double TickMs => _timer.BetweenMs(0, 1);

    /// <summary>
    /// <b>Resident: every walker the town holds other than the device is known to</b> — where it stands and moves, its pace,
    /// aim, heading, mass and flags, or its impulse — staged to be written whole over the device's table, and which body each
    /// is, sent only where that changed.
    /// </summary>
    /// <returns>How many walkers the town has.</returns>
    public int LayWhatChanged(TownWorld town)
    {
        var people = town.People;
        var patches = _patchesIn!.Span<Patch>();
        var paused = town.HoldAgents;
        _patchCount = 0;
        for (var person = 0; person < people.Count; person++)
        {
            var row = RowOf(people, person, paused);
            var impulseNs = town.WalkerImpulseNs(person);
            _bodies.Lay(person, people.Body[person].Index);
            if (_checks) _laidForCheck![person] = row;
            if (_isKnown[person] && SameToTheTick(row, _known[person]) && impulseNs == _knownImpulse[person]) continue;

            patches[_patchCount++] = new Patch(row, impulseNs, (uint)person);
            _known[person] = row;
            _knownImpulse[person] = impulseNs;
            _isKnown[person] = true;
        }

        return people.Count;
    }

    /// <summary>
    /// Resident: what the tick just handed back taken as what the device holds — every walker's pace, impulse and heading.
    /// </summary>
    public void KnowTheTick()
    {
        var ticks = TicksAfter;
        for (var walker = 0; walker < ticks.Length; walker++)
        {
            ref readonly var tick = ref ticks[walker];
            _known[walker] = _known[walker] with { DeclaredMps = tick.DeclaredMps, HeadingRad = tick.HeadingRad };
            _knownImpulse[walker] = tick.ImpulseNs;
        }

        if (_checks) TickReadUnlike += UnlikeToTheTick();
    }

    /// <summary>How many walkers the table held, as the last tick read it, other than the town laid them.</summary>
    int UnlikeToTheTick()
    {
        var read = _readForCheck!.Span<WalkerRow>();
        var unlike = 0;
        for (var walker = 0; walker < _count; walker++)
        {
            if (!SameToTheTick(read[walker], _laidForCheck![walker])) unlike++;
        }

        return unlike;
    }

    /// <summary>
    /// Resident: where the bodies' step left every walker the device's table holds in the world
    /// (<c>bodies_walkers_mirror.comp</c>), taken as what the device holds — off the walkers the step handed back.
    /// </summary>
    public void KnowTheStep(ReadOnlySpan<Vector4> walked)
    {
        for (var walker = 0; walker < Math.Min(_count, walked.Length); walker++)
        {
            if ((_known[walker].Flags & WalkerRow.Inside) != 0) continue;

            var at = walked[walker];
            _known[walker] = _known[walker] with { PositionM = new Vector2(at.X, at.Y), VelocityMps = new Vector2(at.Z, at.W) };
        }
    }

    /// <summary>
    /// Resident: a walker's aim and whether it walks, as a rebuild that keeps the walkers wrote them into the table
    /// (<c>walkers_kept_out.comp</c>), taken as what the device holds.
    /// </summary>
    public void KnowTheAim(int walker, Vector2 aimM, bool walking)
    {
        if (!Resident || !_isKnown[walker]) return;

        ref var known = ref _known[walker];
        known = known with { AimM = aimM, Flags = walking ? known.Flags | WalkerRow.Walking : known.Flags & ~WalkerRow.Walking };
    }

    /// <summary>
    /// Resident: whether the device's table is known to hold this walker's body as the town has it — where it stands, how it
    /// moves, the pace it declared and the way it faces — for whoever reads them off the table in the town's place.
    /// </summary>
    public bool HoldsTheBodyOf(Agents.Person.Body.PersonFleet people, int person)
    {
        if (!Resident || !_isKnown[person]) return false;

        ref readonly var known = ref _known[person];
        return known.PositionM == people.PositionM[person] && known.VelocityMps == people.VelocityMps[person]
               && known.DeclaredMps == people.DeclaredMps[person] && known.HeadingRad.Equals(people.HeadingRad[person]);
    }

    /// <summary>
    /// <b>One tick of the first <paramref name="walkers"/> walkers recorded into a caller's submit</b>: what was marked
    /// sent — or, resident, every walker staged written over the table — and the kernel run, leaving its writes for the caller
    /// to barrier before whatever reads them.
    /// </summary>
    public void Record(CommandBuffer commands, int walkers)
    {
        if (walkers > Capacity) throw new ArgumentOutOfRangeException(nameof(walkers), $"The table holds {Capacity} walkers, not {walkers}.");

        _count = walkers;
        if (Resident)
        {
            _vk.Copy(commands, _patchesIn!, _patches!, (ulong)(_patchCount * Marshal.SizeOf<Patch>()));
            _bodies.Send(commands);
            _vk.AfterTransfer(commands);
            if (_patchCount > 0)
            {
                _patchKernel!.Dispatch(commands, _patchSet, (uint)((_patchCount + 63) / 64), (uint)_patchCount);
                _vk.BetweenKernels(commands);
            }

            if (_checks)
            {
                _vk.BeforeTransfer(commands);
                _vk.Copy(commands, _walkers.Device, _readForCheck!, (ulong)(walkers * Marshal.SizeOf<WalkerRow>()));
                _vk.AfterTransfer(commands);
            }
        }
        else
        {
            _walkers.Send(commands);
            _impulses.Send(commands);
            _bodies.Send(commands);
            _vk.AfterTransfer(commands);
        }

        _timer.Reset(commands);
        _timer.Stamp(commands, 0);
        _kernel.Dispatch(commands, _set, (uint)Math.Max(1, (walkers + 63) / 64), _figures with { WalkerCount = (uint)walkers });
        _timer.Stamp(commands, 1);
    }

    /// <summary>What the walkers' tick and the bodies' phase after it left, recorded to be handed back.</summary>
    public void RecordRead(CommandBuffer commands)
    {
        _vk.BeforeTransfer(commands);
        if (Resident)
        {
            _ticksOut.Read(commands, _count);
            return;
        }

        _walkers.Read(commands, _count);
        _impulses.Read(commands, _count);
    }

    /// <summary>The device's stamps of the last tick taken in, once its submit has been waited for.</summary>
    public void ReadTimes() => _timer.Read();

    /// <summary>One walker as the town's tick reads it.</summary>
    static WalkerRow RowOf(Agents.Person.Body.PersonFleet people, int person, bool paused)
    {
        var flags = (people.Walking[person] ? WalkerRow.Walking : 0u) | (people.Wounded[person] ? WalkerRow.Wounded : 0u)
                    | (people.Inside[person].Any ? WalkerRow.Inside : 0u) | (paused ? WalkerRow.Paused : 0u);
        return new WalkerRow(
            people.PositionM[person], people.VelocityMps[person], people.DeclaredMps[person], people.DestinationM[person],
            people.HeadingRad[person], people.MassKg[person], flags, people.DistanceWalkedM[person]);
    }

    /// <summary>
    /// Whether two rows are one to the walker's tick, bit for bit: everything it reads — not how far the walker has walked,
    /// which the device and the town each add up their own way and the tick never reads.
    /// </summary>
    static bool SameToTheTick(in WalkerRow one, in WalkerRow other) =>
        MemoryMarshal.AsBytes(new ReadOnlySpan<WalkerRow>(in one))[..44].SequenceEqual(MemoryMarshal.AsBytes(new ReadOnlySpan<WalkerRow>(in other))[..44]);

    public void Dispose()
    {
        _timer.Dispose();
        _kernel.Dispose();
        _patchKernel?.Dispose();
        _patches?.Dispose();
        _readForCheck?.Dispose();
        _patchesIn?.Dispose();
        _walkers.Dispose();
        _impulses.Dispose();
        _bodies.Dispose();
        _ticksOut.Dispose();
    }
}
