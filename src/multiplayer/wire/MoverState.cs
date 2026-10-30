namespace TrafficSimulation.Multiplayer.Wire;

/// <summary>
/// <b>What is drawn of one moving thing</b>, quantised as it is sent (<see cref="WireShape"/>): a car, a walker or a
/// rail vehicle, named by its mover number (<see cref="MoverIds"/>). The same record for all three, because the codec
/// sends only the fields that changed, and a field a kind never moves costs nothing after it enters.
/// </summary>
internal struct MoverState
{
    /// <summary>The mover's number: stable for the whole run, since nothing in a town is ever removed or renumbered.</summary>
    public int Id;

    /// <summary>The middle of the body, in position quanta from the town's corner.</summary>
    public int X;

    public int Y;

    /// <summary>The way it faces, in angle quanta from 0 to a whole turn.</summary>
    public ushort Heading;

    /// <summary>A car's road wheels' angle, in small-angle quanta either way.</summary>
    public short Steer;

    /// <summary>A car's lean on its springs along and across its body, in small-angle quanta either way.</summary>
    public short TiltAlong;

    public short TiltAcross;

    /// <summary>A car's lamps (<c>CarLampSet</c>); a walker's and a wagon's are nought.</summary>
    public byte Lamps;

    public MoverFlags Flags;

    /// <summary>The collision channels it stands on, which say which level it is drawn at.</summary>
    public byte Channels;

    /// <summary>
    /// The seat whose player this is, plus one — the body on foot, the car it drives, the front of the train it drives —
    /// and nought for anything no player is (MP-12).
    /// </summary>
    public byte Driver;

    /// <summary>The look it is drawn in. Sent only as it enters a player's area: it never changes.</summary>
    public ushort Look;

    /// <summary>Whether everything but the number is the same, which is what costs a mover nothing in a snapshot.</summary>
    public readonly bool SameAs(in MoverState other) =>
        X == other.X && Y == other.Y && Heading == other.Heading && Steer == other.Steer &&
        TiltAlong == other.TiltAlong && TiltAcross == other.TiltAcross && Lamps == other.Lamps &&
        Flags == other.Flags && Channels == other.Channels && Driver == other.Driver && Look == other.Look;
}

[Flags]
internal enum MoverFlags : byte
{
    None = 0,

    /// <summary>A car that is a wreck (PHY-3).</summary>
    Wrecked = 1,

    /// <summary>A walker walking rather than standing.</summary>
    Walking = 2,

    /// <summary>A walker lying where a car left it (PER-11).</summary>
    Down = 4,
}

/// <summary>
/// <b>One numbering for everything that moves</b>: the cars from nought, then the walkers, then the rail vehicles — the
/// fleets' own indices end to end, so a number names one thing for the whole run and its kind is read off where it falls.
/// </summary>
internal readonly record struct MoverIds(int Cars, int People, int Wagons)
{
    public int Count => Cars + People + Wagons;

    public int OfCar(int car) => car;

    public int OfPerson(int person) => Cars + person;

    public int OfWagon(int wagon) => Cars + People + wagon;

    public MoverKind KindOf(int id) => id < Cars ? MoverKind.Car : id < Cars + People ? MoverKind.Person : MoverKind.Wagon;

    /// <summary>The index in its own fleet.</summary>
    public int IndexOf(int id) => id < Cars ? id : id < Cars + People ? id - Cars : id - Cars - People;
}

internal enum MoverKind : byte
{
    Car,
    Person,
    Wagon,
}
