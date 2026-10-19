using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Train.Body;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Rail;
using TrafficSimulation.World.Town;
using static TrafficSimulation.App.DeviceTick.DeviceHoldings;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The railway and the rail vehicles' wheels on the device</b> (TRN-8, TRN-9; <c>world/town/kernels/world_rail.comp</c>):
/// the track network laid once, each switch's position, each vehicle's wheels, weight and pedals, and where each of its
/// wheels stands on the track — stepped as <see cref="ManagedWorld"/> steps them, to the bit.
/// </summary>
/// <remarks>
/// <para>
/// <b>The host writes what a signal asks of a rail vehicle</b> — its pedals, a switch, a wheel put down afresh — where the
/// pass reads it, between steps: <c>world_signals.comp</c> already binds the sixteen storage buffers a browser lets one
/// stage have. Where a vehicle is put down and taken out is the signals' own, as for any body.
/// </para>
/// <para>
/// <b>The wheels' cursors are the device's alone</b>, and a put-down is a bit on the vehicle's pedals for the one step
/// after it rather than a write into them: a page never sends the host's copy of a buffer the kernel writes.
/// </para>
/// </remarks>
internal sealed class DeviceRail
{
    /// <summary>A rail vehicle's pedals as the pass reads them (<c>RailCommandRow</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct RailCommandRow(float ThrottleMps2, float BrakeMps2, uint Flags, uint Unused)
    {
        /// <summary><see cref="PutDown"/>: its wheels found afresh by the nearest track this step, before they are followed.</summary>
        public const uint Handbrake = 1, Reverse = 2, PutDown = 4;

        public static RailCommandRow Of(in DriveCommand command) => new(
            command.ThrottleMps2, command.BrakeMps2, (command.Handbrake ? Handbrake : 0u) | (command.Reverse ? Reverse : 0u), 0u);
    }

    readonly int _wagons;
    readonly GpuBuffer _commands, _cursors, _switches;
    readonly ComputeKernel _kernel;
    readonly BoundSet _set;

    /// <summary>The vehicles whose <see cref="RailCommandRow.PutDown"/> the last step was sent, to take off before the next.</summary>
    readonly int[] _putDown;
    int _putDownCount;

    public DeviceRail(DeviceHoldings held, WorldLaying laying, GpuBuffer pose, GpuBuffer motion, GpuBuffer mass, GpuBuffer filter, uint pushBytes)
    {
        _wagons = laying.Wagons.Length;
        var rails = laying.Rails;
        var builds = new RailBuild[Math.Max(1, _wagons)];
        for (var wagon = 0; wagon < _wagons; wagon++) builds[wagon] = laying.Wagons[wagon].Build;

        var points = new List<Vector4>();
        var segments = new int[Math.Max(1, rails.SegmentCount) * 4];
        for (var segment = 0; segment < rails.SegmentCount; segment++)
        {
            var line = rails.Segments[segment];
            (segments[segment * 4], segments[(segment * 4) + 1]) = (points.Count, line.PointM.Length);
            (segments[(segment * 4) + 2], segments[(segment * 4) + 3]) = (rails.NodeOfEnd[segment * 2], rails.NodeOfEnd[(segment * 2) + 1]);
            for (var point = 0; point < line.PointM.Length; point++) points.Add(new Vector4(line.PointM[point], line.AtM[point], 0f));
        }

        var nodes = new int[Math.Max(1, rails.NodeCount) * 4];
        for (var node = 0; node < rails.NodeCount; node++)
        {
            (nodes[node * 4], nodes[(node * 4) + 1]) = (rails.EndOffsets[node], rails.EndOffsets[node + 1] - rails.EndOffsets[node]);
            nodes[(node * 4) + 2] = rails.SwitchOfNode[node];
        }

        var ends = rails.Ends.Length == 0 ? [0] : rails.Ends;
        _commands = held.Written<RailCommandRow>(Math.Max(1, _wagons));
        _commands.Span<RailCommandRow>().Clear();
        var nowhere = new RailCursor[Math.Max(1, _wagons * TrainVehicle.MostBogies)];
        Array.Fill(nowhere, RailCursor.Nowhere);
        _cursors = held.Laid<RailCursor>(nowhere);
        _switches = held.Written<uint>(Math.Max(1, rails.SwitchCount));
        _switches.Span<uint>().Clear();
        _putDown = new int[Math.Max(1, _wagons)];
        _kernel = held.Kernel("world_rail.comp", 12, pushBytes);
        _set = _kernel.Bind(
        [
            pose, motion, mass, filter, held.Laid<RailBuild>(builds), _commands, _cursors,
            held.Laid<Vector4>(points.Count == 0 ? [Vector4.Zero] : [.. points]), held.Laid<int>(segments), held.Laid<int>(nodes),
            held.Laid<int>(ends), _switches,
        ]);
    }

    /// <summary>A rail vehicle's pedals as the signal leaves them (<see cref="ManagedWorld"/>'s own), a put-down kept.</summary>
    public void Drive(int wagon, in DriveCommand command)
    {
        ref var row = ref _commands.Span<RailCommandRow>()[wagon];
        var driven = RailCommandRow.Of(command);
        row = driven with { Flags = driven.Flags | (row.Flags & RailCommandRow.PutDown) };
    }

    /// <summary>A switch set (TRN-9).</summary>
    public void Set(int railSwitch, int position) => _switches.Span<uint>()[railSwitch] = (uint)position;

    /// <summary>A rail vehicle put down afresh: its wheels found again by the nearest track next step, as the host finds them.</summary>
    public void PutDown(int wagon)
    {
        ref var row = ref _commands.Span<RailCommandRow>()[wagon];
        if ((row.Flags & RailCommandRow.PutDown) != 0) return;

        row = row with { Flags = row.Flags | RailCommandRow.PutDown };
        _putDown[_putDownCount++] = wagon;
    }

    /// <summary>
    /// The put-downs the last step was sent taken off the pedals, before this step's signals are written: never under a
    /// step in flight, which on Vulkan reads the pedals where the host writes them.
    /// </summary>
    public void ForgetThePutDowns()
    {
        var rows = _commands.Span<RailCommandRow>();
        foreach (var wagon in _putDown.AsSpan(0, _putDownCount)) rows[wagon] = rows[wagon] with { Flags = rows[wagon].Flags & ~RailCommandRow.PutDown };
        _putDownCount = 0;
    }

    /// <summary>Every rail vehicle's wheels stepped, recorded; nothing where the town has none.</summary>
    public void Record(Recording commands, DeviceWorld.WorldPush push)
    {
        if (_wagons > 0) _kernel.Dispatch(commands, _set, Groups(_wagons), push with { Count = (uint)_wagons });
    }
}
