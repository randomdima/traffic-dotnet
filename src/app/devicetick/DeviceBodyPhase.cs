using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The bodies' phase on the device</b> (<see cref="TownWorld.StepBodies"/>; <c>world/town/kernels/bodies_*.comp</c>):
/// the tick's impulses spent on the solver's bodies — every car's wheels off the tyre pass's own table
/// (<see cref="DeviceTyres.ImpulseTable"/>), then what the host lays — the solver's step (<see cref="DeviceSolver"/>), and
/// every car read back off its body into the tyre pass's poses (<see cref="DeviceTyres.Table"/>), which the next tick's
/// car tick reads. Kernels in one submit, over tables the device keeps from one tick to the next.
/// </summary>
/// <remarks>
/// <para>
/// <b>What the host still lays</b>: which body each car is and whether it rested parked, sent only where it changed
/// (<see cref="LayCar"/>); every walker's central impulse and both ends of every tow bar, which the device does not yet
/// produce (<see cref="LayImpulse"/>, <see cref="LayCentralImpulse"/>); and the wheels of a car the tyre pass does not
/// step — a car on a tow, whose two trailer wheels are the host's — written over the pass's own rows
/// (<see cref="LayWheels"/>). A car under the hand is stepped by the pass like any other, on the host's command.
/// </para>
/// <para>
/// <b>Run in shadow</b> (<see cref="Lay(TownWorld, float, bool)"/>): the town still steps, and the device is handed its
/// bodies and the impulses its tick wrote before every step, so the two are asked one question. <see cref="KernelsMs"/>
/// is the kernels alone; what was sent and read back is timed apart.
/// </para>
/// <para>
/// <b>Or in the town's place</b> (<see cref="DeviceTownBodies"/>): the solver's table laid once and then only where the
/// town changed it, and a step handing back what <see cref="Reads"/> names — the cars, every walker in the world off
/// its body (<see cref="LayWalker"/>), and how deep each body was into what it touched — and what the solver's settling
/// hands back of itself (<see cref="DeviceSolver.Settles"/>).
/// </para>
/// </remarks>
internal sealed partial class DeviceBodyPhase : IDisposable
{
    /// <summary>Which body a car is, and its flags (<c>CarBodyRow</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct CarBodyRow(int Body, uint Flags)
    {
        public const uint RestsParked = 1u;
    }

    /// <summary>One impulse the host lays (<c>LaidImpulseRow</c>): at <see cref="AtM"/>, or at the centre where <see cref="Central"/> is set.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct LaidImpulseRow(Vector2 ImpulseNs, Vector2 AtM, int Body, uint Central, uint Pad0, uint Pad1);

    /// <summary>The kernels' push constants (<c>bodies_phase.glsl</c>), field for field.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct PhasePush(
        uint Count, uint WheelsACar, float DtS, float LoadSettledShare, uint EnabledBit, uint WalkerTableBound, uint Pad1, uint Pad2);

    /// <summary>One walker as a step left its body (<c>WalkedRow</c>, <c>bodies_walkers_read.comp</c>), and how far it has walked.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct WalkedRow(
        Vector2 PositionM, Vector2 VelocityMps, float DistanceWalkedM, float Pad0 = 0f, float Pad1 = 0f, float Pad2 = 0f);

    /// <summary>What a step hands back to the host.</summary>
    [Flags]
    internal enum Reads
    {
        None = 0,

        /// <summary>Every car as the step read it off its body (<see cref="PosesAfter"/>).</summary>
        Poses = 1,

        /// <summary>Every body's motion as the impulses left it, before the step (<see cref="MotionApplied"/>) — the shadow's.</summary>
        Applied = 2,

        /// <summary>Every walker in the world as the step left its body (<see cref="WalkersAfter"/>).</summary>
        Walkers = 4,

        /// <summary>How deep each body was into what it touched (<see cref="DeviceSolver.OverlapBits"/>) — an instrument's.</summary>
        Overlap = 8,
    }

    /// <summary>The stamps a step is timed between.</summary>
    enum Stage { Began, Sent, Applied, Stepped, Mirrored, Read }

    const int WheelsACar = TyreModel.Wheels;

    readonly Vk _vk;
    readonly SimConfig _config;
    readonly DeviceSolver _solver;
    readonly DeviceTyres _tyres;
    readonly ResidentRows<CarBodyRow> _carBodies;
    readonly ResidentRows<LaidImpulseRow> _laid;
    readonly GpuBuffer _wheelsIn;
    readonly BufferCopy[] _wheelRuns;

    /// <summary>
    /// What a step hands back of every car and walker, two of each and a step's to each in turn: what the last step taken
    /// back left stays readable while the next writes the other (<see cref="Side"/>).
    /// </summary>
    readonly GpuBuffer[] _posesOut;

    readonly GpuBuffer _appliedOut;
    readonly ComputeKernel _wheels, _laidKernel, _mirror;
    readonly DescriptorSet _wheelsSet, _laidSet, _mirrorSet;
    DeviceWalkerTick? _walkers;
    ComputeKernel? _walkerKernel, _walkerMirror;
    DescriptorSet _walkerSet, _walkerMirrorSet;
    GpuBuffer? _walkerImpulsesIn;
    BufferCopy[] _walkerImpulseRuns = [];
    int _walkerImpulseRunCount;
    readonly ResidentRows<int>? _walkerBodies;
    readonly GpuBuffer[] _walkersOut = [];
    readonly ComputeKernel? _walkersRead;
    readonly DescriptorSet[] _walkersReadSets = [];
    readonly ComputeBatch _batch;
    readonly GpuTimer _timer;
    int _wheelRunCount;
    int _laidCount;
    int _cars;
    int _walkersHanded;
    int _side;

    /// <param name="solver">The solver whose table the phase spends the impulses on and steps.</param>
    /// <param name="tyres">The tyre pass whose impulses the phase spends and whose poses it writes; its capacity is the fleet's.</param>
    /// <param name="mostLaid">The most impulses the host lays a tick: one a walker and two a tow bar.</param>
    /// <param name="walkers">How many walkers a step can hand back off their bodies (<see cref="Reads.Walkers"/>), or none.</param>
    public DeviceBodyPhase(Vk vk, SimConfig config, DeviceSolver solver, DeviceTyres tyres, int mostLaid, int walkers = 0)
    {
        _vk = vk;
        _config = config;
        _solver = solver;
        _tyres = tyres;
        var capacity = tyres.Capacity;

        _carBodies = new ResidentRows<CarBodyRow>(vk, capacity, 1, hostOnly: true);
        _laid =new ResidentRows<LaidImpulseRow>(vk, mostLaid, 1);
        _wheelsIn = vk.CreateBuffer(
            (ulong)(Math.Max(1, capacity * WheelsACar) * Unsafe.SizeOf<Vector4>()), BufferUsageFlags.TransferSrcBit, HostAccess.Writes);
        _wheelRuns = new BufferCopy[Math.Max(1, capacity)];
        var poseBytes = (ulong)(Math.Max(1, capacity) * Unsafe.SizeOf<DeviceTyres.PoseRow>());
        _posesOut =
        [
            vk.CreateBuffer(poseBytes, BufferUsageFlags.TransferDstBit, HostAccess.Reads),
            vk.CreateBuffer(poseBytes, BufferUsageFlags.TransferDstBit, HostAccess.Reads),
        ];
        _appliedOut = vk.CreateBuffer(
            (ulong)(Math.Max(1, solver.BodyCount) * Unsafe.SizeOf<Vector4>()), BufferUsageFlags.TransferDstBit, HostAccess.Reads);

        var bodies = solver.Bodies;
        var pushBytes = (uint)Marshal.SizeOf<PhasePush>();
        _wheels = new ComputeKernel(vk, "bodies_wheels.comp", 5, pushBytes);
        _wheelsSet = _wheels.Bind([_carBodies.Device, tyres.ImpulseTable, bodies.Pose, bodies.Motion, bodies.Mass]);
        _laidKernel = new ComputeKernel(vk, "bodies_laid.comp", 4, pushBytes);
        _laidSet = _laidKernel.Bind([_laid.Device, bodies.Pose, bodies.Motion, bodies.Mass]);
        _mirror = new ComputeKernel(vk, "bodies_mirror.comp", 6, pushBytes);
        _mirrorSet = _mirror.Bind(
            [_carBodies.Device, tyres.ImpulseTable, bodies.Pose, bodies.Heading, bodies.Motion, tyres.Table.Poses.Device]);
        if (walkers > 0)
        {
            _walkerBodies = new ResidentRows<int>(vk, walkers, 1, hostOnly: true);
            var walkedBytes = (ulong)(walkers * Unsafe.SizeOf<WalkedRow>());
            _walkersOut =
            [
                vk.CreateBuffer(walkedBytes, BufferUsageFlags.StorageBufferBit, HostAccess.Reads),
                vk.CreateBuffer(walkedBytes, BufferUsageFlags.StorageBufferBit, HostAccess.Reads),
            ];

            // Bound twice again where the walkers' tick is taken (TakeTheWalkers), its table in place of the stand-in.
            _walkersRead = new ComputeKernel(vk, "bodies_walkers_read.comp", 6, pushBytes, sets: 4);
            _walkersReadSets = [WalkersReadSet(0, _walkersOut[0]), WalkersReadSet(1, _walkersOut[1])];
        }

        _batch = new ComputeBatch(vk);
        _timer = new GpuTimer(vk, Enum.GetValues<Stage>().Length);
    }

    /// <summary>The solver the phase steps.</summary>
    public DeviceSolver Solver => _solver;

    /// <summary>
    /// <b>The walkers' impulses taken off the device's own walker tick</b> from here on, rather than laid by the host, and
    /// every walker read back into its table after the step (<c>bodies_walkers.comp</c>, <c>bodies_walkers_mirror.comp</c>).
    /// </summary>
    public void TakeTheWalkers(DeviceWalkerTick walkers)
    {
        var bodies = _solver.Bodies;
        var pushBytes = (uint)Marshal.SizeOf<PhasePush>();
        _walkers = walkers;
        _walkerImpulsesIn = _vk.CreateBuffer(
            (ulong)(Math.Max(1, walkers.Capacity) * Unsafe.SizeOf<Vector2>()), BufferUsageFlags.TransferSrcBit, HostAccess.Writes);
        _walkerImpulseRuns = new BufferCopy[Math.Max(1, walkers.Capacity)];
        _walkerKernel = new ComputeKernel(_vk, "bodies_walkers.comp", 6, pushBytes);
        _walkerSet = _walkerKernel.Bind([walkers.BodyTable, walkers.Table, walkers.ImpulseTable, bodies.Motion, bodies.Mass, bodies.Filter]);
        _walkerMirror = new ComputeKernel(_vk, "bodies_walkers_mirror.comp", 4, pushBytes);
        _walkerMirrorSet = _walkerMirror.Bind([walkers.BodyTable, walkers.Table, bodies.Pose, bodies.Motion]);
        if (_walkersRead == null) return;

        _walkersReadSets[0] = WalkersReadSet(0, walkers.Table);
        _walkersReadSets[1] = WalkersReadSet(1, walkers.Table);
    }

    /// <summary>The walkers' read-back bound to write one side, reading how far each has walked off <paramref name="walkerTable"/>.</summary>
    DescriptorSet WalkersReadSet(int side, GpuBuffer walkerTable)
    {
        var bodies = _solver.Bodies;
        return _walkersRead!.Bind([_walkerBodies!.Device, bodies.Filter, bodies.Pose, bodies.Motion, _walkersOut[side], walkerTable]);
    }

    /// <summary>The device's own time for the last step's kernels — the impulses, the solver's step and the read-back — in milliseconds.</summary>
    public double KernelsMs => ApplyMs + StepMs + MirrorMs;

    /// <summary>The device's time spending the last step's impulses, in milliseconds.</summary>
    public double ApplyMs => _timer.BetweenMs((int)Stage.Sent, (int)Stage.Applied);

    /// <summary>The device's time for the solver's step alone, as the solver stamps it, in milliseconds.</summary>
    public double StepMs => _solver.BetweenMs(DeviceSolver.Stage.Began, DeviceSolver.Stage.Settled);

    /// <summary>Of <see cref="StepMs"/>, the device's time working out what the step froze and woke (<see cref="DeviceSolver.Settles"/>), in milliseconds.</summary>
    public double SettleMs => _solver.BetweenMs(DeviceSolver.Stage.Integrated, DeviceSolver.Stage.Settled);

    /// <summary>The device's time reading every car off its body, in milliseconds.</summary>
    public double MirrorMs => _timer.BetweenMs((int)Stage.Stepped, (int)Stage.Mirrored);

    /// <summary>The device's time taking the last step's rows in, in milliseconds.</summary>
    public double SentMs => _timer.BetweenMs((int)Stage.Began, (int)Stage.Sent);

    /// <summary>The device's time handing the last step's answers back, in milliseconds.</summary>
    public double ReadMs => _timer.BetweenMs((int)Stage.Mirrored, (int)Stage.Read);

    /// <summary>How many bytes the last step took in, every table's and the solver's.</summary>
    public ulong SentBytes { get; private set; }

    /// <summary>How many of <see cref="SentBytes"/> were wheels the host laid over the tyre pass's (<see cref="LayWheels"/>).</summary>
    public ulong WheelBytes { get; private set; }

    /// <summary>Every car as the last step read it off its body, where it was asked to be read back (<see cref="Step"/>).</summary>
    public ReadOnlySpan<DeviceTyres.PoseRow> PosesAfter => PosesOn(_side, _cars);

    /// <summary>
    /// Every walker as the last step left its body, where it was asked to be handed back (<see cref="Reads.Walkers"/>). A
    /// walker whose body is out of the world is not written, and holds whatever it last did.
    /// </summary>
    public ReadOnlySpan<WalkedRow> WalkersAfter => WalkersOn(_side, _walkersHanded);

    /// <summary>Which of the two hand-backs the last step recorded wrote, which stays as it left it until the step after that.</summary>
    public int Side => _side;

    /// <summary>The first <paramref name="cars"/> cars as the step that wrote <paramref name="side"/> left them.</summary>
    public ReadOnlySpan<DeviceTyres.PoseRow> PosesOn(int side, int cars) => _posesOut[side].Span<DeviceTyres.PoseRow>()[..cars];

    /// <summary>The first <paramref name="walkers"/> walkers as the step that wrote <paramref name="side"/> left them.</summary>
    public ReadOnlySpan<WalkedRow> WalkersOn(int side, int walkers) =>
        _walkersOut.Length == 0 ? default : _walkersOut[side].Span<WalkedRow>()[..walkers];

    /// <summary>How many bytes the last step handed back, where it was asked to, but the walkers': those are as many as are in the world.</summary>
    public ulong ReadBytes { get; private set; }

    /// <summary>Which body a walker is, marked to be sent only where it differs from what was sent last (<see cref="Reads.Walkers"/>).</summary>
    public void LayWalker(int walker, int body) => _walkerBodies!.Lay(walker, body);

    /// <summary>Which body each walker is, as the last step was sent it, on the device — none where the phase hands back no walker.</summary>
    public GpuBuffer? WalkerBodies => _walkerBodies?.Device;

    /// <summary>Which body each car is (<see cref="CarBodyRow"/>), as the last step was sent it, on the device.</summary>
    public GpuBuffer CarBodies => _carBodies.Device;

    /// <summary>
    /// A walker's impulse as the town made it, written over the walker tick's own row for this tick — for a walker the
    /// tick did not answer for (<see cref="TakeTheWalkers"/>). A walker is laid at most once a step.
    /// </summary>
    public void LayWalkerImpulse(int walker, Vector2 impulseNs)
    {
        _walkerImpulsesIn!.Span<Vector2>()[walker] = impulseNs;
        var bytes = (ulong)Unsafe.SizeOf<Vector2>();
        var offset = (ulong)walker * bytes;
        if (_walkerImpulseRunCount > 0)
        {
            ref var last = ref _walkerImpulseRuns[_walkerImpulseRunCount - 1];
            if (last.DstOffset + last.Size == offset)
            {
                last.Size += bytes;
                return;
            }
        }

        _walkerImpulseRuns[_walkerImpulseRunCount++] = new BufferCopy(offset, offset, bytes);
    }

    /// <summary>
    /// Every body's motion as the last step's impulses left it, before the solver stepped it — velocity, yaw rate — where
    /// it was asked to be read back.
    /// </summary>
    public ReadOnlySpan<Vector4> MotionApplied => _appliedOut.Span<Vector4>()[.._solver.BodyCount];

    /// <summary>
    /// Which body a car is and whether it rested parked on its last tick, marked to be sent only where it differs from
    /// what was sent last.
    /// </summary>
    public void LayCar(int car, int body, bool restsParked)
    {
        _carBodies.Lay(car, new CarBodyRow(body, restsParked ? CarBodyRow.RestsParked : 0u));
    }

    /// <summary>
    /// A car's wheels as the host spent them, written over the tyre pass's own rows for this tick — for a car the pass does
    /// not step. A car is laid at most once a step.
    /// </summary>
    public void LayWheels(int car, ReadOnlySpan<WheelImpulse> wheels)
    {
        var rows = _wheelsIn.Span<Vector4>().Slice(car * WheelsACar, WheelsACar);
        for (var wheel = 0; wheel < WheelsACar; wheel++)
        {
            rows[wheel] = new Vector4(wheels[wheel].ImpulseNs, wheels[wheel].AtM.X, wheels[wheel].AtM.Y);
        }

        var bytes = (ulong)(WheelsACar * Unsafe.SizeOf<Vector4>());
        var offset = (ulong)car * bytes;
        if (_wheelRunCount > 0 && _wheelRuns[_wheelRunCount - 1].DstOffset + _wheelRuns[_wheelRunCount - 1].Size == offset)
        {
            _wheelRuns[_wheelRunCount - 1].Size += bytes;
            return;
        }

        _wheelRuns[_wheelRunCount++] = new BufferCopy(offset, offset, bytes);
    }

    /// <summary>An impulse spent at a point of a body after every wheel. A body is laid at most once a step.</summary>
    public void LayImpulse(int body, Vector2 impulseNs, Vector2 atM) =>
        _laid.All[_laidCount++] = new LaidImpulseRow(impulseNs, atM, body, 0u, 0u, 0u);

    /// <summary>An impulse spent at a body's centre after every wheel. A body is laid at most once a step.</summary>
    public void LayCentralImpulse(int body, Vector2 impulseNs) =>
        _laid.All[_laidCount++] = new LaidImpulseRow(impulseNs, Vector2.Zero, body, 1u, 0u, 0u);

    /// <summary>
    /// <b>One step of the first <paramref name="cars"/> cars' bodies</b>: what was laid sent, the impulses spent, the
    /// solver's step, every car read back into the tyre pass's poses — and, where <paramref name="readBack"/>, the motion
    /// the impulses left and the poses handed back. One submit and one wait, and a copy a table, whatever the count.
    /// </summary>
    public void Step(int cars, float dtS, bool readBack)
    {
        var commands = _batch.Begin();
        Record(commands, cars, dtS, readBack);
        _batch.SubmitAndWait();
        ReadTimes();
    }

    /// <summary>
    /// <b>One step recorded into a caller's submit</b>, as <see cref="Step"/> runs it — the caller having made whatever
    /// wrote the tyre pass's impulses and poses visible to a copy first (<see cref="Vk.BeforeTransfer"/>).
    /// </summary>
    public void Record(CommandBuffer commands, int cars, float dtS, bool readBack) =>
        Record(commands, cars, 0, dtS, readBack ? Reads.Poses | Reads.Applied : Reads.None);

    /// <summary>
    /// The same, handing back what <paramref name="reads"/> names — the first <paramref name="walkers"/> walkers, where
    /// it names them.
    /// </summary>
    public void Record(CommandBuffer commands, int cars, int walkers, float dtS, Reads reads)
    {
        if (cars > _tyres.Capacity) throw new ArgumentOutOfRangeException(nameof(cars), $"The phase holds {_tyres.Capacity} cars, not {cars}.");

        _cars = cars;
        _side ^= 1;
        var push = new PhasePush(
            (uint)cars, WheelsACar, dtS, TownWorld.LoadSettledShare(_config, dtS), (uint)BodyFlags.Enabled, _walkers != null ? 1u : 0u, 0u, 0u);
        var vk = _vk;
        _timer.Reset(commands);
        _timer.Stamp(commands, (int)Stage.Began);

        _laid.Mark(0, _laidCount);
        var poses = _tyres.Table.Poses;
        WheelBytes = WheelBytesMarked();
        SentBytes = poses.MarkedBytes + _carBodies.MarkedBytes + _laid.MarkedBytes + WheelBytes + (_walkerBodies?.MarkedBytes ?? 0);
        poses.Send(commands);
        _carBodies.Send(commands);
        _laid.Send(commands);
        _walkerBodies?.Send(commands);
        vk.Copy(commands, _wheelsIn, _tyres.ImpulseTable, _wheelRuns.AsSpan(0, _wheelRunCount));
        _wheelRunCount = 0;
        if (_walkers != null)
        {
            foreach (ref readonly var run in _walkerImpulseRuns.AsSpan(0, _walkerImpulseRunCount)) SentBytes += run.Size;
            vk.Copy(commands, _walkerImpulsesIn!, _walkers.ImpulseTable, _walkerImpulseRuns.AsSpan(0, _walkerImpulseRunCount));
            _walkerImpulseRunCount = 0;
        }
        _solver.Send(commands);
        SentBytes += _solver.SentBytes;
        vk.AfterTransfer(commands);
        if (_solver.RecordLaidRows(commands)) vk.BetweenKernels(commands);

        _timer.Stamp(commands, (int)Stage.Sent);

        _wheels.Dispatch(commands, _wheelsSet, Groups(cars), push);
        if (_walkers != null)
        {
            _walkerKernel!.Dispatch(commands, _walkerSet, Groups(_walkers.Count), push with { Count = (uint)_walkers.Count });
        }

        vk.BetweenKernels(commands);
        _laidKernel.Dispatch(commands, _laidSet, Groups(_laidCount), push with { Count = (uint)_laidCount });
        _laidCount = 0;
        _timer.Stamp(commands, (int)Stage.Applied);
        if ((reads & Reads.Applied) != 0)
        {
            vk.BeforeTransfer(commands);
            vk.Copy(commands, _solver.Bodies.Motion, _appliedOut, _appliedOut.SizeBytes);
        }

        vk.BetweenKernels(commands);
        _solver.Record(commands, dtS);
        vk.BetweenKernels(commands);
        _timer.Stamp(commands, (int)Stage.Stepped);

        _mirror.Dispatch(commands, _mirrorSet, Groups(cars), push);
        if (_walkers != null)
        {
            _walkerMirror!.Dispatch(commands, _walkerMirrorSet, Groups(_walkers.Count), push with { Count = (uint)_walkers.Count });
        }

        vk.BeforeTransfer(commands);
        _timer.Stamp(commands, (int)Stage.Mirrored);

        _solver.RecordReadBack(commands, everything: false);
        ReadBytes = DeviceSolver.Totals.Count * sizeof(uint);
        if ((reads & Reads.Poses) != 0)
        {
            var bytes = (ulong)(cars * Unsafe.SizeOf<DeviceTyres.PoseRow>());
            vk.Copy(commands, poses.Device, _posesOut[_side], bytes);
            ReadBytes += bytes;
        }

        _walkersHanded = 0;
        if ((reads & Reads.Walkers) != 0 && walkers > 0)
        {
            var most = _walkerBodies?.Items ?? 0;
            if (walkers > most) throw new ArgumentOutOfRangeException(nameof(walkers), $"The phase hands back {most} walkers, not {walkers}.");

            _walkersHanded = walkers;
            _walkersRead!.Dispatch(commands, _walkersReadSets[_side], Groups(walkers), push with { Count = (uint)walkers });
        }

        if ((reads & Reads.Overlap) != 0) ReadBytes += _solver.RecordTheOverlapRead(commands);

        _timer.Stamp(commands, (int)Stage.Read);
    }

    /// <summary>The device's stamps of the last step taken in, the solver's with them, once its submit has been waited for.</summary>
    public void ReadTimes()
    {
        _timer.Read();
        _solver.ReadTimes();
    }

    ulong WheelBytesMarked()
    {
        var bytes = 0ul;
        foreach (ref readonly var run in _wheelRuns.AsSpan(0, _wheelRunCount)) bytes += run.Size;
        return bytes;
    }

    static uint Groups(int count) => (uint)Math.Max(1, (count + 63) / 64);

    public void Dispose()
    {
        _timer.Dispose();
        _batch.Dispose();
        _mirror.Dispose();
        _laidKernel.Dispose();
        _wheels.Dispose();
        _walkerKernel?.Dispose();
        _walkerMirror?.Dispose();
        _walkersRead?.Dispose();
        _walkerBodies?.Dispose();
        _walkerImpulsesIn?.Dispose();
        foreach (var buffer in _walkersOut) buffer.Dispose();
        _carBodies.Dispose();
        _laid.Dispose();
        _wheelsIn.Dispose();
        foreach (var buffer in _posesOut) buffer.Dispose();
        _appliedOut.Dispose();
    }
}
