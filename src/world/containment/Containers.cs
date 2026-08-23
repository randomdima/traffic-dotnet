namespace TrafficSimulation.World.Containment;

/// <summary>What a person can be inside. PHY-7's two container kinds and nothing else.</summary>
internal enum ContainerKind : byte
{
    None,
    Building,
    Car,
}

/// <summary>Where a person is, when they are not in the town: which kind of container, and which one.</summary>
internal readonly record struct Contained(ContainerKind Kind, int Index)
{
    public static Contained Nowhere => new(ContainerKind.None, -1);

    public bool Any => Kind != ContainerKind.None;
}

/// <summary>
/// <b>One containment contract for both container kinds</b> (PHY-7): who is inside what, how many a
/// building will hold (OBJ-5) and which car has its one driver (CAR-2). It is bookkeeping and nothing
/// else — where a body goes when it comes out is <see cref="ExitSpots"/>'s, and what a person may do
/// while inside is the trip's.
/// </summary>
/// <remarks>
/// <para>
/// <b>A claim is not an occupancy.</b> A person walking to a building holds a claim on it so that a
/// town's worth of walkers do not all set off for the last free place in one building; the claim is
/// advisory and only keeps the crowd down, and <b>capacity is checked atomically at the door</b>
/// (`P-9`). Refused there is a real state — the building is full — and not a failure.
/// </para>
/// <para>
/// <b>Nothing here decides whether a car may be boarded.</b> PER-4's free, stopped and intact is asked
/// of the car's own arrays at the moment of boarding, because two of the three are about motion and
/// damage and neither belongs in a register of who is inside what.
/// </para>
/// </remarks>
internal sealed class Containers
{
    readonly int[] _buildingCapacity;
    readonly int[] _buildingOccupants;
    readonly int[] _buildingClaims;
    readonly int[] _carDriver;
    readonly Contained[] _personIsIn;

    /// <param name="personIsIn">
    /// The roster's own array, written here rather than copied: <b>where a person is, is a field of the
    /// person</b> — what draws them, what picks them and what steps them all ask it — and a second copy
    /// of it would be a second truth.
    /// </param>
    public Containers(ReadOnlySpan<int> buildingCapacity, int cars, Contained[] personIsIn)
    {
        _buildingCapacity = buildingCapacity.ToArray();
        _buildingOccupants = new int[buildingCapacity.Length];
        _buildingClaims = new int[buildingCapacity.Length];
        _carDriver = new int[cars];
        Array.Fill(_carDriver, NoDriver);
        _personIsIn = personIsIn;
        Array.Fill(_personIsIn, Contained.Nowhere);
    }

    public const int NoDriver = -1;

    public int BuildingCount => _buildingCapacity.Length;

    public int CapacityOf(int building) => _buildingCapacity[building];

    public int OccupantsOf(int building) => _buildingOccupants[building];

    /// <summary>How many people are on their way to it, which is what makes one with room a better draw than one without.</summary>
    public int ClaimsOn(int building) => _buildingClaims[building];

    /// <summary>Whether a building has room once the people already walking to it are counted (`P-10`).</summary>
    public bool LooksLikelyToHaveRoom(int building) =>
        _buildingOccupants[building] + _buildingClaims[building] < _buildingCapacity[building];

    public void Claim(int building) => _buildingClaims[building]++;

    public void GiveUpClaim(int building)
    {
        if (_buildingClaims[building] > 0) _buildingClaims[building]--;
    }

    /// <summary>
    /// `P-9`: the door. <b>Capacity is checked here and nowhere else</b>, at the moment the person
    /// asks — a place counted when the trip was drawn is a place somebody else has since walked into.
    /// </summary>
    public bool TryAdmit(int building, int person)
    {
        if (_buildingOccupants[building] >= _buildingCapacity[building]) return false;

        _buildingOccupants[building]++;
        _personIsIn[person] = new Contained(ContainerKind.Building, building);
        return true;
    }

    public void LeaveBuilding(int building, int person)
    {
        if (_buildingOccupants[building] > 0) _buildingOccupants[building]--;
        _personIsIn[person] = Contained.Nowhere;
    }

    /// <summary>CAR-2: a car contains at most one driver, and taking the seat is one atomic question (`P-6`).</summary>
    public bool TryBoard(int car, int person)
    {
        if (_carDriver[car] != NoDriver) return false;

        _carDriver[car] = person;
        _personIsIn[person] = new Contained(ContainerKind.Car, car);
        return true;
    }

    public void Alight(int car, int person)
    {
        if (_carDriver[car] == person) _carDriver[car] = NoDriver;
        _personIsIn[person] = Contained.Nowhere;
    }

    /// <summary>Who is driving, or <see cref="NoDriver"/> — which is the whole of CAR-1's question about a car.</summary>
    public int DriverOf(int car) => _carDriver[car];

    public bool IsFree(int car) => _carDriver[car] == NoDriver;

    public Contained WhereIs(int person) => _personIsIn[person];

    public bool IsContained(int person) => _personIsIn[person].Any;
}
