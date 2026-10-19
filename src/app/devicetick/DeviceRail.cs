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
/// <b>The host writes what a signal asks of a rail vehicle</b> — its pedals, a switch, a wheel put down afresh — where the
/// pass reads it, between steps: <c>world_signals.comp</c> already binds the sixteen storage buffers a browser lets one
/// stage have. Where a vehicle is put down and taken out is the signals' own, as for any body.
/// </remarks>
internal sealed class DeviceRail
{
    /// <summary>A rail vehicle's pedals as the pass reads them (<c>RailCommandRow</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct RailCommandRow(float ThrottleMps2, float BrakeMps2, uint Flags, uint Unused)
    {
        public const uint Handbrake = 1, Reverse = 2;

        public static RailCommandRow Of(in DriveCommand command) => new(
            command.ThrottleMps2, command.BrakeMps2, (command.Handbrake ? Handbrake : 0u) | (command.Reverse ? Reverse : 0u), 0u);
    }

    readonly int _wagons;
    readonly GpuBuffer _commands, _cursors, _switches;
    readonly ComputeKernel _kernel;
    readonly BoundSet _set;

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
        _commands = held.Mapped<RailCommandRow>(Math.Max(1, _wagons));
        _cursors = held.Mapped<RailCursor>(Math.Max(1, _wagons * TrainVehicle.MostBogies));
        _commands.Span<RailCommandRow>().Clear();
        _cursors.Span<RailCursor>().Fill(RailCursor.Nowhere);
        _switches = held.Mapped<uint>(Math.Max(1, rails.SwitchCount));
        _switches.Span<uint>().Clear();
        _kernel = held.Kernel("world_rail.comp", 12, pushBytes);
        _set = _kernel.Bind(
        [
            pose, motion, mass, filter, held.Laid<RailBuild>(builds), _commands, _cursors,
            held.Laid<Vector4>(points.Count == 0 ? [Vector4.Zero] : [.. points]), held.Laid<int>(segments), held.Laid<int>(nodes),
            held.Laid<int>(ends), _switches,
        ]);
    }

    /// <summary>A rail vehicle's pedals as the signal leaves them (<see cref="ManagedWorld"/>'s own).</summary>
    public void Drive(int wagon, in DriveCommand command) => _commands.Span<RailCommandRow>()[wagon] = RailCommandRow.Of(command);

    /// <summary>A switch set (TRN-9).</summary>
    public void Set(int railSwitch, int position) => _switches.Span<uint>()[railSwitch] = (uint)position;

    /// <summary>A rail vehicle put down afresh: its wheels found again by the nearest track, as the host finds them.</summary>
    public void PutDown(int wagon) => _cursors.Span<RailCursor>().Slice(wagon * TrainVehicle.MostBogies, TrainVehicle.MostBogies).Fill(RailCursor.Nowhere);

    /// <summary>Every rail vehicle's wheels stepped, recorded; nothing where the town has none.</summary>
    public void Record(Recording commands, DeviceWorld.WorldPush push)
    {
        if (_wagons > 0) _kernel.Dispatch(commands, _set, Groups(_wagons), push with { Count = (uint)_wagons });
    }
}
