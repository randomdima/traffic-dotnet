using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.App.Render;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Physics;
using static TrafficSimulation.App.DeviceTick.DeviceHoldings;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The smoke's tracks, laid behind every step</b> (CAR-54, TRN-15; <c>app/render/kernels/smoke_track.comp</c>): each car
/// and each rail vehicle as the step left it, and a ring of the last <see cref="SmokeFigures.TrackPoints"/> points of its way, one each time the
/// step's clock passes a mark of <see cref="SmokeFigures.TrackEveryS"/> — what the emit lays every puff along
/// (<see cref="DeviceSprites"/>). A rail vehicle's ring is <see cref="SmokeFigures.StackTrackPoints"/> long and laid every
/// <see cref="SmokeFigures.StackTrackEveryS"/>, for a stack's longer-lived smoke, past every car's.
/// </summary>
/// <remarks>
/// <para>
/// <b>The device's alone, and only for a world whose moving is drawn there</b> (<see cref="DeviceWorld.DrawWith"/>): the
/// tracks are laid from then on, so a page, whose world may be the device's but whose moving is filled on the host, pays
/// nothing for them. Some 300 bytes a car, and no crossing a step: the clock is the push.
/// </para>
/// <para>
/// <b>The step's own clock</b>, a sum of the steps' lengths kept here: a point is when the step that laid it ended, and
/// the emit ages every puff off the newest step's clock rather than the frame's, so a puff is laid off the very way the
/// car drove whatever the frame rate.
/// </para>
/// </remarks>
internal sealed class DeviceSmoke
{
    /// <summary>A point of a car's way (<c>SmokePoint</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct Point(
        Vector2 PositionM, Vector2 Forward, Vector2 VelocityMps, float Running, float Throttle, float TimeS, float Pad);

    /// <summary>A car's track as a whole (<c>SmokeHead</c>); all nought is a car with no way yet.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct Head(Point Live, uint Newest, uint Count, float RanS, float ShownS);

    /// <summary><c>smoke_track.comp</c>'s push constants.</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct SmokePush(
        uint Cars, uint Wagons, uint FirstWagonBody, uint EnabledBit, uint Points, uint StackPoints, float TimeS,
        float PerEveryS, float StackPerEveryS, float ResetMps, float PerFullThrottle, float PerFullRailThrottle);

    readonly ComputeKernel _kernel;
    readonly BoundSet _set;
    readonly int _cars, _wagons, _firstWagonBody;
    readonly SmokeLook _look;

    /// <summary>The step's clock: every step's length summed, in doubles so an hour's run lays its points as its first minute did.</summary>
    double _clockS;

    /// <param name="cars">Each car's world row (<c>WorldCar</c>): its body, and what it shows.</param>
    /// <param name="firstWagonBody">The first rail vehicle's body, the rest after it in order.</param>
    /// <param name="filter">Every body's flags: whether a rail vehicle is in the world (TRN-7).</param>
    /// <param name="commands">Each car's pedals and wheel (<c>TyreCommand</c>): how hard its driver asks of the throttle.</param>
    /// <param name="railCommands">And each rail vehicle's (<c>RailCommandRow</c>).</param>
    public DeviceSmoke(
        DeviceHoldings held, SimConfig config, int cars, int wagons, int firstWagonBody, GpuBuffer carRows, GpuBuffer pose,
        GpuBuffer motion, GpuBuffer filter, GpuBuffer commands, GpuBuffer railCommands)
    {
        _cars = cars;
        _wagons = wagons;
        _firstWagonBody = firstWagonBody;
        _look = SmokeSprites.Look(config);
        var rows = Math.Max(1, cars + wagons);
        Heads = held.Laid<Head>(new Head[rows]);
        Points = held.Room<Point>(Math.Max(1, (cars * _look.Tailpipe.TrackPoints) + (wagons * _look.Stack.TrackPoints)));
        _kernel = held.Kernel("smoke_track.comp", 8, (uint)Marshal.SizeOf<SmokePush>());
        _set = _kernel.Bind([carRows, pose, motion, commands, Heads, Points, filter, railCommands]);
    }

    /// <summary>Every car's track as a whole, a <see cref="Head"/> a car, then every rail vehicle's from <see cref="FirstWagon"/>.</summary>
    public GpuBuffer Heads { get; }

    /// <summary>Every track's ring of points, <see cref="SmokeFigures.TrackPoints"/> a car's, then <see cref="SmokeFigures.StackTrackPoints"/> a rail vehicle's.</summary>
    public GpuBuffer Points { get; }

    /// <summary>Where the rail vehicles' tracks start: past every car's.</summary>
    public int FirstWagon => _cars;

    /// <summary>The pass laid behind a step of <paramref name="dtS"/>, once the bodies stand where it left them.</summary>
    public void Record(Recording commands, float dtS)
    {
        _clockS += dtS;
        if (_cars + _wagons == 0) return;

        var (tailpipe, stack) = (_look.Tailpipe, _look.Stack);
        _kernel.Dispatch(commands, _set, Groups(_cars + _wagons), new SmokePush(
            (uint)_cars, (uint)_wagons, (uint)_firstWagonBody, (uint)BodyFlags.Enabled, (uint)tailpipe.TrackPoints,
            (uint)stack.TrackPoints, (float)_clockS, tailpipe.PerTrackEveryS, stack.PerTrackEveryS, _look.ResetMps,
            tailpipe.PerFullThrottle, stack.PerFullThrottle));
    }
}
