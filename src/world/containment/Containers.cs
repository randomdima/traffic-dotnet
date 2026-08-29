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
/// (OBJ-5). Refused there is a real state — the building is full — and not a failure.
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

    /// <summary>
    /// <b>The one seat that is not the wheel</b> (AMB-6): the stretcher an ambulance carries a casualty on.
    /// It is a second array rather than a count because CAR-2 is about the driver and nothing else, and a
    /// register that could not say which of two occupants was driving would answer neither question.
    /// </summary>
    readonly int[] _carPassenger;

    /// <summary>
    /// <b>The seats that are neither the wheel nor the stretcher</b> (SRV-3): a service vehicle's crew,
    /// laid <see cref="CrewSeats"/> to a car so that a hand who has got out has a seat to come back to and
    /// nobody else can take it.
    /// </summary>
    /// <remarks>
    /// <b>A stride over the whole fleet and not a list per car.</b> Two ints on every car in the town costs
    /// nothing and buys the property every other roster here has: a car's index means the same thing in this
    /// array as it does everywhere else, and no crew changes size while the town is running.
    /// </remarks>
    readonly int[] _carCrew;
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
        _carPassenger = new int[cars];
        Array.Fill(_carPassenger, NoDriver);
        _carCrew = new int[cars * CrewSeats];
        Array.Fill(_carCrew, NoDriver);
        _personIsIn = personIsIn;
        Array.Fill(_personIsIn, Contained.Nowhere);
    }

    public const int NoDriver = -1;

    /// <summary>
    /// <b>How many a vehicle carries besides its driver and its stretcher</b>. One hand is what every errand
    /// in this town needs — somebody to get out and do the work — and the seat beside it is what says the
    /// figure is a figure rather than a special case.
    /// </summary>
    public const int CrewSeats = 2;

    public int OccupantsOf(int building) => _buildingOccupants[building];

    /// <summary>How many people are on their way to it, which is what makes one with room a better draw than one without.</summary>
    public int ClaimsOn(int building) => _buildingClaims[building];

    /// <summary>Whether a building has room once the people already walking to it are counted (PER-11).</summary>
    public bool LooksLikelyToHaveRoom(int building) =>
        _buildingOccupants[building] + _buildingClaims[building] < _buildingCapacity[building];

    public void Claim(int building) => _buildingClaims[building]++;

    public void GiveUpClaim(int building)
    {
        if (_buildingClaims[building] > 0) _buildingClaims[building]--;
    }

    /// <summary>
    /// OBJ-5: the door. <b>Capacity is checked here and nowhere else</b>, at the moment the person
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
        LeaveTheCrew(car, person);
        _personIsIn[person] = Contained.Nowhere;
    }

    /// <summary>
    /// <b>A crew seat taken</b> (SRV-3), on the same atomic terms the wheel is: refused where every seat is
    /// full, so two hands cannot be handed one.
    /// </summary>
    public bool TryTakeACrewSeat(int car, int person)
    {
        var first = car * CrewSeats;
        for (var seat = first; seat < first + CrewSeats; seat++)
        {
            if (_carCrew[seat] != NoDriver) continue;

            _carCrew[seat] = person;
            _personIsIn[person] = new Contained(ContainerKind.Car, car);
            return true;
        }

        return false;
    }

    /// <summary>The seat given up. Silent where this person was never in one, which is what makes <see cref="Alight"/> one call.</summary>
    void LeaveTheCrew(int car, int person)
    {
        var first = car * CrewSeats;
        for (var seat = first; seat < first + CrewSeats; seat++)
        {
            if (_carCrew[seat] != person) continue;

            _carCrew[seat] = NoDriver;
            return;
        }
    }

    /// <summary>One of this vehicle's crew, or <see cref="NoDriver"/> — the whole of how a hand is found again.</summary>
    public int CrewOf(int car, int seat) => _carCrew[(car * CrewSeats) + seat];

    /// <summary>Whether anybody at all is aboard: the driver, the crew, or the stretcher.</summary>
    public bool AnybodyAboard(int car)
    {
        if (_carDriver[car] != NoDriver || _carPassenger[car] != NoDriver) return true;

        var first = car * CrewSeats;
        for (var seat = first; seat < first + CrewSeats; seat++)
        {
            if (_carCrew[seat] != NoDriver) return true;
        }

        return false;
    }

    /// <summary>
    /// <b>A casualty put aboard</b> (AMB-6). Refused where the stretcher already carries somebody, and it
    /// is the same atomic question <see cref="TryBoard"/> asks of the wheel.
    /// </summary>
    public bool TryLoad(int car, int person)
    {
        if (_carPassenger[car] != NoDriver) return false;

        _carPassenger[car] = person;
        _personIsIn[person] = new Contained(ContainerKind.Car, car);
        return true;
    }

    /// <summary>And taken off it — at a hospital's door, or wherever the car it was in stopped being one.</summary>
    public void Unload(int car, int person)
    {
        if (_carPassenger[car] == person) _carPassenger[car] = NoDriver;
        _personIsIn[person] = Contained.Nowhere;
    }

    /// <summary>Who is on the stretcher, or <see cref="NoDriver"/>.</summary>
    public int PassengerOf(int car) => _carPassenger[car];

    /// <summary>
    /// <b>The stretcher through the door</b> (AMB-8): out of the car and into the building in one
    /// question, refused where the building is full (OBJ-5).
    /// </summary>
    /// <remarks>
    /// <b>It is one call because it has to be atomic.</b> Taken off the car first and then refused at the
    /// door, the casualty is inside nothing at all while still having no body in the world — which is a
    /// person the town has lost. Capacity is checked here like everywhere else, at the moment of asking.
    /// </remarks>
    public bool TryTransfer(int car, int person, int building)
    {
        if (_carPassenger[car] != person) return false;
        if (_buildingOccupants[building] >= _buildingCapacity[building]) return false;

        _carPassenger[car] = NoDriver;
        _buildingOccupants[building]++;
        _personIsIn[person] = new Contained(ContainerKind.Building, building);
        return true;
    }

    /// <summary>Who is driving, or <see cref="NoDriver"/> — which is the whole of CAR-1's question about a car.</summary>
    public int DriverOf(int car) => _carDriver[car];

    public bool IsFree(int car) => _carDriver[car] == NoDriver;

    public Contained WhereIs(int person) => _personIsIn[person];

    public bool IsContained(int person) => _personIsIn[person].Any;
}
