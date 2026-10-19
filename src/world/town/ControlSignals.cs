using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>What a control signal asks of the agent it names (<see cref="ControlSignal"/>).</summary>
internal enum ControlAction : ushort
{
    /// <summary>A car's wheel and pedals (<see cref="DriveCommand"/>): steer, throttle, brake; flags <see cref="ControlSignal.Handbrake"/>, <see cref="ControlSignal.Reverse"/>, <see cref="ControlSignal.UnderAHand"/>. Kept until the next.</summary>
    Drive = 1,

    /// <summary>A walker's aim (x, y); flags <see cref="ControlSignal.Moving"/>, <see cref="ControlSignal.OnFeet"/>, <see cref="ControlSignal.Faces"/>. Kept until the next.</summary>
    Walk,

    /// <summary>
    /// A body put down at (x, y) facing a heading, moving along it at a speed — a rail vehicle put on its run-in at its
    /// line's (TRN-7), every other body at rest — and back in the world (<see cref="PhysicsWorld.Release"/>); a rail
    /// vehicle's look in its flags, for the world to draw.
    /// </summary>
    Place,

    /// <summary>A body taken out of the world into a container (<see cref="PhysicsWorld.Contain"/>).</summary>
    Contain,

    /// <summary>A body moved onto the collision layer its flags name (<see cref="CollisionLayer"/>).</summary>
    Layer,

    /// <summary>A body put on the collision channels its flags name (PHY-1a).</summary>
    Channels,

    /// <summary>A car put on the bar of the tractor whose agent is the first parameter's bits, or taken off it where that is −1; flag <see cref="ControlSignal.ByTheTail"/> (EVA-5).</summary>
    Tow,

    /// <summary>A car's tyres forgotten: rims stopped, throttle recovered, no mark under way.</summary>
    ClearWheels,

    /// <summary>What a car shows, for the world to draw: its lamps (a <see cref="CarLampSet"/>) in the low byte of its flags, and <see cref="ControlSignal.Wrecked"/>. Kept until the next.</summary>
    Show,

    /// <summary>
    /// The lanes a car's line is on, for the world to read it along (<see cref="LineFollowed"/>): the lane's and the
    /// join's bits, where the stretch stops, and where along it the car is; its serial in the flags. Kept until the next.
    /// </summary>
    Follow,

    /// <summary>
    /// A rail vehicle coupled behind the one whose agent is the first parameter's bits, or uncoupled where that is −1: the
    /// solver's bar between the two coupler heads (SOL-38).
    /// </summary>
    Couple,

    /// <summary>A switch set (TRN-9): its number in the first parameter's bits, its position in the flags. Any agent may name it.</summary>
    Switch,
}

/// <summary>
/// <b>One instruction from the actor layer to the world</b>: the agent it names — its index on the roster
/// (<see cref="Core.Simulation.AgentRoster"/>), never a body — what it asks, and up to four figures. Blittable, so a
/// tick's list crosses to the device as it is written.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct ControlSignal(int Agent, ControlAction Action, ushort Flags, Vector4 Params)
{
    public const ushort Handbrake = 1, Reverse = 2, UnderAHand = 4;

    /// <summary>A walker's flags: it walks at its aim; it stands on its feet rather than sliding; it turns to face its aim rather than holding its stance where it stands.</summary>
    public const ushort Moving = 1, OnFeet = 2, Faces = 4;

    public const ushort ByTheTail = 1;

    public const ushort Wrecked = 0x100;

    public static ControlSignal Show(int agent, CarLampSet lamps, bool wrecked) =>
        new(agent, ControlAction.Show, (ushort)((ushort)lamps | (wrecked ? Wrecked : 0)), default);

    public static ControlSignal Drive(int agent, in DriveCommand command, bool underAHand) => new(
        agent, ControlAction.Drive,
        (ushort)((command.Handbrake ? Handbrake : 0) | (command.Reverse ? Reverse : 0) | (underAHand ? UnderAHand : 0)),
        new Vector4(command.SteerRad, command.ThrottleMps2, command.BrakeMps2, 0f));

    public static ControlSignal Walk(int agent, Vector2 aimM, bool moving, bool onFeet, bool faces) => new(
        agent, ControlAction.Walk, (ushort)((moving ? Moving : 0) | (onFeet ? OnFeet : 0) | (faces ? Faces : 0)),
        new Vector4(aimM, 0f, 0f));

    public static ControlSignal Place(int agent, Vector2 atM, float headingRad, float speedMps = 0f, int look = 0) =>
        new(agent, ControlAction.Place, (ushort)look, new Vector4(atM, headingRad, speedMps));

    public static ControlSignal Contain(int agent) => new(agent, ControlAction.Contain, 0, default);

    /// <param name="ahead">The agent of the rail vehicle it is coupled behind, or −1 to uncouple it.</param>
    public static ControlSignal Couple(int agent, int ahead) =>
        new(agent, ControlAction.Couple, 0, new Vector4(BitConverter.Int32BitsToSingle(ahead), 0f, 0f, 0f));

    public static ControlSignal Switch(int agent, int railSwitch, int position) =>
        new(agent, ControlAction.Switch, (ushort)position, new Vector4(BitConverter.Int32BitsToSingle(railSwitch), 0f, 0f, 0f));

    public static ControlSignal Layer(int agent, CollisionLayer layer) => new(agent, ControlAction.Layer, (ushort)layer, default);

    public static ControlSignal Channels(int agent, byte channels) => new(agent, ControlAction.Channels, channels, default);

    /// <param name="tractor">The tractor's agent, or −1 to take the car off the bar.</param>
    public static ControlSignal Tow(int towed, int tractor, bool byTheTail) => new(
        towed, ControlAction.Tow, byTheTail ? ByTheTail : (ushort)0,
        new Vector4(BitConverter.Int32BitsToSingle(tractor), 0f, 0f, 0f));

    public static ControlSignal ClearWheels(int agent) => new(agent, ControlAction.ClearWheels, 0, default);

    public static ControlSignal Follow(int agent, in LineFollowed followed) => new(
        agent, ControlAction.Follow, (ushort)followed.Serial,
        new Vector4(
            BitConverter.Int32BitsToSingle(followed.Lane), BitConverter.Int32BitsToSingle(followed.Join), followed.StopsAtM,
            followed.AroundM));

    /// <summary>What a <see cref="ControlAction.Follow"/> asks the world to follow, read around where it says the car is.</summary>
    public LineFollowed Followed => new(
        BitConverter.SingleToInt32Bits(Params.X), BitConverter.SingleToInt32Bits(Params.Y), Params.Z, Params.W, Flags);

    /// <summary>The command a <see cref="ControlAction.Drive"/> carries.</summary>
    public DriveCommand Command => new(
        Params.X, Params.Y, Params.Z, (Flags & Handbrake) != 0, (Flags & Reverse) != 0);

    /// <summary>The tractor's agent a <see cref="ControlAction.Tow"/> names, the vehicle ahead a <see cref="ControlAction.Couple"/> does, or −1; and the switch a <see cref="ControlAction.Switch"/> sets.</summary>
    public int Tractor => BitConverter.SingleToInt32Bits(Params.X);
}

/// <summary>
/// <b>A tick's control signals</b>, written by the actor layer in the order it decides and read once by the world's
/// step. Laid for twice the agents, so a steady tick never grows it.
/// </summary>
/// <remarks>
/// <b>Sparse by construction</b>: a car's command and a walker's aim are signalled only where they changed
/// (<see cref="TownWorld"/> keeps what it last sent), and everything else is an event.
/// </remarks>
internal sealed class ControlSignals(int agents)
{
    ControlSignal[] _signals = new ControlSignal[Math.Max(64, agents * 2)];

    public int Count { get; private set; }

    /// <summary>Signals written to another world before it took them, carried to the one handed over in its place.</summary>
    public void AddRange(ReadOnlySpan<ControlSignal> signals)
    {
        foreach (ref readonly var signal in signals) Add(signal);
    }

    /// <summary>How many it holds before it grows.</summary>
    public int Capacity => _signals.Length;

    public ReadOnlySpan<ControlSignal> Written => _signals.AsSpan(0, Count);

    public void Add(in ControlSignal signal)
    {
        if (Count == _signals.Length) Array.Resize(ref _signals, _signals.Length * 2);

        _signals[Count++] = signal;
    }

    /// <summary>Emptied once the world has taken them.</summary>
    public void Clear() => Count = 0;
}
