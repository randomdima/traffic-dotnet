using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;
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
    internal readonly record struct PhasePush(uint Count, uint WheelsACar, float DtS, float LoadSettledShare);

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
    readonly GpuBuffer _posesOut;
    readonly GpuBuffer _appliedOut;
    readonly ComputeKernel _wheels, _laidKernel, _mirror;
    readonly DescriptorSet _wheelsSet, _laidSet, _mirrorSet;
    readonly ComputeBatch _batch;
    readonly GpuTimer _timer;
    int _wheelRunCount;
    int _laidCount;
    int _cars;

    /// <param name="solver">The solver whose table the phase spends the impulses on and steps.</param>
    /// <param name="tyres">The tyre pass whose impulses the phase spends and whose poses it writes; its capacity is the fleet's.</param>
    /// <param name="mostLaid">The most impulses the host lays a tick: one a walker and two a tow bar.</param>
    public DeviceBodyPhase(Vk vk, SimConfig config, DeviceSolver solver, DeviceTyres tyres, int mostLaid)
    {
        _vk = vk;
        _config = config;
        _solver = solver;
        _tyres = tyres;
        var capacity = tyres.Capacity;

        // No car is body -1, so every car's first lay differs from what was "sent" and is.
        _carBodies = new ResidentRows<CarBodyRow>(vk, capacity, 1);
        _carBodies.All.Fill(new CarBodyRow(-1, 0u));
        _laid = new ResidentRows<LaidImpulseRow>(vk, mostLaid, 1);
        _wheelsIn = vk.CreateBuffer(
            (ulong)(Math.Max(1, capacity * WheelsACar) * Unsafe.SizeOf<Vector4>()), BufferUsageFlags.TransferSrcBit, HostAccess.Writes);
        _wheelRuns = new BufferCopy[Math.Max(1, capacity)];
        _posesOut = vk.CreateBuffer(
            (ulong)(Math.Max(1, capacity) * Unsafe.SizeOf<DeviceTyres.PoseRow>()), BufferUsageFlags.TransferDstBit, HostAccess.Reads);
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

        _batch = new ComputeBatch(vk);
        _timer = new GpuTimer(vk, Enum.GetValues<Stage>().Length);
    }

    /// <summary>The solver the phase steps.</summary>
    public DeviceSolver Solver => _solver;

    /// <summary>The device's own time for the last step's kernels — the impulses, the solver's step and the read-back — in milliseconds.</summary>
    public double KernelsMs => ApplyMs + StepMs + MirrorMs;

    /// <summary>The device's time spending the last step's impulses, in milliseconds.</summary>
    public double ApplyMs => _timer.BetweenMs((int)Stage.Sent, (int)Stage.Applied);

    /// <summary>The device's time for the solver's step alone, as the solver stamps it, in milliseconds.</summary>
    public double StepMs => _solver.BetweenMs(DeviceSolver.Stage.Began, DeviceSolver.Stage.Integrated);

    /// <summary>The device's time reading every car off its body, in milliseconds.</summary>
    public double MirrorMs => _timer.BetweenMs((int)Stage.Stepped, (int)Stage.Mirrored);

    /// <summary>The device's time taking the last step's rows in, in milliseconds.</summary>
    public double SentMs => _timer.BetweenMs((int)Stage.Began, (int)Stage.Sent);

    /// <summary>The device's time handing the last step's answers back, in milliseconds — the shadow's alone.</summary>
    public double ReadMs => _timer.BetweenMs((int)Stage.Mirrored, (int)Stage.Read);

    /// <summary>How many bytes the last step took in, every table's and the solver's.</summary>
    public ulong SentBytes { get; private set; }

    /// <summary>Every car as the last step read it off its body, where it was asked to be read back (<see cref="Step"/>).</summary>
    public ReadOnlySpan<DeviceTyres.PoseRow> PosesAfter => _posesOut.Span<DeviceTyres.PoseRow>()[.._cars];

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
        var row = new CarBodyRow(body, restsParked ? CarBodyRow.RestsParked : 0u);
        ref var sent = ref _carBodies.Rows(car)[0];
        if (sent == row) return;

        sent = row;
        _carBodies.Mark(car);
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
    public void Record(CommandBuffer commands, int cars, float dtS, bool readBack)
    {
        if (cars > _tyres.Capacity) throw new ArgumentOutOfRangeException(nameof(cars), $"The phase holds {_tyres.Capacity} cars, not {cars}.");

        _cars = cars;
        var push = new PhasePush((uint)cars, WheelsACar, dtS, TownWorld.LoadSettledShare(_config, dtS));
        var vk = _vk;
        _timer.Reset(commands);
        _timer.Stamp(commands, (int)Stage.Began);

        _laid.Mark(0, _laidCount);
        var poses = _tyres.Table.Poses;
        SentBytes = poses.MarkedBytes + _carBodies.MarkedBytes + _laid.MarkedBytes + WheelBytesMarked();
        poses.Send(commands);
        _carBodies.Send(commands);
        _laid.Send(commands);
        vk.Copy(commands, _wheelsIn, _tyres.ImpulseTable, _wheelRuns.AsSpan(0, _wheelRunCount));
        _wheelRunCount = 0;
        _solver.Send(commands);
        SentBytes += _solver.SentBytes;
        vk.AfterTransfer(commands);
        _timer.Stamp(commands, (int)Stage.Sent);

        _wheels.Dispatch(commands, _wheelsSet, Groups(cars), push);
        vk.BetweenKernels(commands);
        _laidKernel.Dispatch(commands, _laidSet, Groups(_laidCount), push with { Count = (uint)_laidCount });
        _laidCount = 0;
        _timer.Stamp(commands, (int)Stage.Applied);
        if (readBack)
        {
            vk.BeforeTransfer(commands);
            vk.Copy(commands, _solver.Bodies.Motion, _appliedOut, _appliedOut.SizeBytes);
        }

        vk.BetweenKernels(commands);
        _solver.Record(commands, dtS);
        vk.BetweenKernels(commands);
        _timer.Stamp(commands, (int)Stage.Stepped);

        _mirror.Dispatch(commands, _mirrorSet, Groups(cars), push);
        vk.BeforeTransfer(commands);
        _timer.Stamp(commands, (int)Stage.Mirrored);

        _solver.RecordReadBack(commands, everything: false);
        if (readBack) vk.Copy(commands, poses.Device, _posesOut, (ulong)(cars * Unsafe.SizeOf<DeviceTyres.PoseRow>()));
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
        _carBodies.Dispose();
        _laid.Dispose();
        _wheelsIn.Dispose();
        _posesOut.Dispose();
        _appliedOut.Dispose();
    }
}
