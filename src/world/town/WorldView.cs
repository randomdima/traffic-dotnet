using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>One car as a step of the world left it: the shallow state the actor layer reads, and nothing it decides.</summary>
/// <remarks>
/// <b>A body frozen and not through the last step froze at it</b>, so a reader that reads every view tells one freezing
/// from the next by that alone (SOL-37; <c>TownWorld.ReadTheWorld</c>).
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal struct CarView
{
    public Vector2 PositionM;
    public Vector2 VelocityMps;
    public float HeadingRad;
    public float YawRateRadPerS;

    /// <summary><see cref="Frozen"/>, <see cref="FrozenThroughLastStep"/>, which wheels stand on a way at bits 8–11, its channels at bits 16–23.</summary>
    public uint Flags;

    /// <summary>
    /// How deep the body was into everything touching it at the step's own manifolds, in metres
    /// (<see cref="PhysicsWorld.OverlapOf"/>) — what a watch holds to nothing inside anything, every tick. An instrument's,
    /// in what would otherwise be the row's padding.
    /// </summary>
    public float OverlapM;

    /// <summary><see cref="HeadingRad"/> as the body's own rotation, so the reader reduces no angle (<c>CarFleet.Forward</c>).</summary>
    public Vector2 Forward;

    /// <summary>Where along the line it follows the rear axle was read (<see cref="LineFollowed.ProgressM"/>), NaN where it follows none.</summary>
    public float ProgressM;

    /// <summary>How far off that line the rear axle stands (<see cref="LineFollowed.OffLineM"/>).</summary>
    public float OffLineM;

    /// <summary>Which follow the reading was taken against (<see cref="LineFollowed.Serial"/>).</summary>
    public uint FollowSerial;

    /// <summary>
    /// How far along the line the wheel was aimed at (<see cref="AimRad"/>): the reading and the lookahead at the speed the
    /// step left (<see cref="LineReading.Lead"/>), or NaN where that runs past what the car follows.
    /// </summary>
    public float LeadAtM;

    /// <summary>The wheel pure pursuit asks for to pass through that point of the line (S-1, <c>CarFollower.AimAt</c>).</summary>
    public float AimRad;

    readonly uint _pad;

    public const uint Frozen = 1, FrozenThroughLastStep = 2;

    public readonly byte WheelsOnAWay => (byte)((Flags >> 8) & 0xF);

    public readonly byte Channels => (byte)(Flags >> 16);

    public static uint FlagsOf(bool frozen, bool frozenThrough, byte wheelsOnAWay, byte channels) =>
        (frozen ? Frozen : 0) | (frozenThrough ? FrozenThroughLastStep : 0) | ((uint)(wheelsOnAWay & 0xF) << 8)
        | ((uint)channels << 16);
}

/// <summary>One walker as the world's last step left it.</summary>
/// <remarks>24 B, a whole number of the 8 B std430 aligns the device's row to for its <c>vec2</c>s.</remarks>
[StructLayout(LayoutKind.Sequential)]
internal struct WalkerView
{
    public Vector2 PositionM;
    public Vector2 VelocityMps;

    /// <summary>The way it faces: its own, turned towards its aim at its turn rate (PER-3).</summary>
    public float HeadingRad;

    /// <inheritdoc cref="CarView.OverlapM"/>
    public float OverlapM;
}

/// <summary>Where one body's covers are in its kind's list (<see cref="WorldView.CarCovers"/>), and how many.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct CoverRange(int From, int Count);

/// <summary>
/// <b>The shallow world state</b>: every dynamic body's pose and motion after the world's last step, the lane ribbons
/// each stood on where the world read them, and the touches the step began — the whole of what crosses from the world
/// to the actor layer.
/// </summary>
/// <remarks>
/// <para>
/// <b>Covers are read on the claims' clock</b> (TER-49) and stand until the next reading. A car's are under its four
/// wheels (TER-4c.2), a walker's under its centre as one wheel; a body inside a container has none. Each is a stretch
/// of one way, laid in agent order. Their ranges are written with them and meaningful only on a view whose step read
/// them (<see cref="CoversRead"/>).
/// </para>
/// <para>
/// <b>The rows are read where the world wrote them</b>: arrays of the view's own, or memory the world maps its writes
/// into (<see cref="ReadOver"/>), which must stay put for as long as the view is read and is not written while it is.
/// </para>
/// </remarks>
internal sealed unsafe class WorldView
{
    CarView* _cars;
    WalkerView* _walkers;
    readonly int _carCount, _walkerCount;

    /// <summary>The view's own rows, pinned, until it reads over somebody else's.</summary>
    CarView[]? _ownCars;

    WalkerView[]? _ownWalkers;

    public WorldView(int people, int cars, int slot)
    {
        Slot = slot;
        (_carCount, _walkerCount) = (cars, people);
        _ownCars = GC.AllocateArray<CarView>(Math.Max(1, cars), pinned: true);
        _ownWalkers = GC.AllocateArray<WalkerView>(Math.Max(1, people), pinned: true);
        _cars = (CarView*)Unsafe.AsPointer(ref MemoryMarshal.GetArrayDataReference(_ownCars));
        _walkers = (WalkerView*)Unsafe.AsPointer(ref MemoryMarshal.GetArrayDataReference(_ownWalkers));
        CarCoverRanges = new CoverRange[cars];
        WalkerCoverRanges = new CoverRange[people];
        CarCovers = new WayCover[Math.Max(16, cars * 4)];
        WalkerCovers = new WayCover[Math.Max(16, people * 2)];
    }

    /// <summary>Which of the world's two views this is (<see cref="WorldLayer"/>), which a world writing each into memory of its own binds by.</summary>
    public int Slot { get; }

    public Span<CarView> Cars => new(_cars, _carCount);

    public Span<WalkerView> Walkers => new(_walkers, _walkerCount);

    /// <summary>The rows read straight out of memory the world writes them into, from here on, and no copy of them kept.</summary>
    /// <param name="cars">At least a row a car, staying put for the view's life.</param>
    /// <param name="walkers">At least a row a walker, the same.</param>
    public void ReadOver(Span<CarView> cars, Span<WalkerView> walkers)
    {
        if (cars.Length < _carCount || walkers.Length < _walkerCount)
        {
            throw new ArgumentException("A view reads over a row for every car and every walker.");
        }

        _cars = (CarView*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(cars));
        _walkers = (WalkerView*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(walkers));
        (_ownCars, _ownWalkers) = (null, null);
    }

    public readonly CoverRange[] CarCoverRanges;

    public readonly CoverRange[] WalkerCoverRanges;

    public WayCover[] CarCovers;

    public WayCover[] WalkerCovers;

    public int CarCoverCount, WalkerCoverCount;

    /// <summary>Whether the step this view is of read the covers.</summary>
    public bool CoversRead;

    /// <summary>
    /// How many batches of signals the state includes (<see cref="WorldLayer.SignalsTaken"/>): a body a later batch set
    /// somewhere is not yet where this view has it.
    /// </summary>
    public long SignalsTaken;

    public Touch[] Touches = new Touch[64];

    public int TouchCount;

    public ReadOnlySpan<WayCover> CoversOfCar(int car) =>
        CarCovers.AsSpan(CarCoverRanges[car].From, CarCoverRanges[car].Count);

    public ReadOnlySpan<WayCover> CoversOfWalker(int person) =>
        WalkerCovers.AsSpan(WalkerCoverRanges[person].From, WalkerCoverRanges[person].Count);

    public ReadOnlySpan<Touch> TouchesBegun => Touches.AsSpan(0, TouchCount);

    public void AddTouch(in Touch touch)
    {
        if (TouchCount == Touches.Length) Array.Resize(ref Touches, Touches.Length * 2);

        Touches[TouchCount++] = touch;
    }
}
