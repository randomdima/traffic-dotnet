using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.Agents.Car.Actions;

/// <summary>
/// <b>What each car is doing, one action at a time</b> (CAR-15b): the one place a car's action changes, and what the
/// action it leaves owned — a pass, a manoeuvre, the ground it was granted — dropped with it.
/// </summary>
/// <remarks>
/// <b>Nothing reads a car's doing off its line, its pass or its manoeuvre</b>: those are what its action holds, and
/// the action is what says which of them is in force. Two readings of one car were how a car waiting for its
/// manoeuvre was once answered as a plan down its line and drove it.
/// </remarks>
internal sealed class CarActions(DrivingGround ground, Manoeuvres manoeuvres)
{
    /// <summary>
    /// How many cars share a room for a pass (<see cref="ActionRooms"/>) — a bound on the tables and not a figure
    /// behaviour reads. Odesa's six-minute run holds six passes at once among 692 cars at the most.
    /// </summary>
    const int CarsToAPassRoom = 16;

    /// <summary>The fewest rooms laid, so a small town has one for every car that could want one.</summary>
    const int LeastRooms = 16;

    readonly uint[] _entries = new uint[ground.Cars.Capacity];

    /// <summary>Room for what a pass gets past, held from its drawing until the car leaves the overtake and the backing up for it.</summary>
    public ActionRooms PassRooms { get; } = new(ground.Cars.Capacity, CarsToAPassRoom, LeastRooms);

    /// <summary>
    /// <b>How many times this car has been handed to another action</b> — so whoever kept something it worked out
    /// for one action can tell whether the car has been in any other since, however it came back.
    /// </summary>
    public uint EntriesOf(int car) => _entries[car];

    /// <summary>
    /// <b>A car handed over to another action</b>, and what the one it leaves owned let go: a pass is its overtake's,
    /// and a manoeuvre its bay's — kept from a manoeuvre into a bay to the one straight back out of it, where a leg
    /// turns there (GEN-4l).
    /// </summary>
    public void Enter(int car, CarAction action)
    {
        var cars = ground.Cars;
        var was = cars.Action[car];
        if (was == action) return;

        if (was is CarAction.Overtake or CarAction.Switch) cars.Pass[car] = Overtake.None;
        if (!IsAboutAPass(action)) PassRooms.Free(car);
        if (IsAtABay(was) && !IsAtABay(action)) manoeuvres.Clear(car);

        // <b>A grant is its action's claim</b>, and goes with it (TER-4c.8): the one claim two actions share is a plan
        // down the route's line, handed on between the actions that drive it. Anything else stands until its own is
        // laid.
        if (!(PlansDownTheRoute(was) && PlansDownTheRoute(action) && ground.PlanHold[car] != LaneOccupancy.NoHold))
        {
            cars.AuthorityM[car] = 0f;
            cars.CoveredSinceClaimM[car] = 0f;
        }

        cars.Action[car] = action;
        _entries[car]++;
    }

    /// <summary>Whether an action is a car's manoeuvre at a bay, into one or out of it.</summary>
    public static bool IsAtABay(CarAction action) => action is CarAction.Park or CarAction.Unpark;

    /// <summary>Whether an action is about a pass the car has drawn: getting past something, or backing up for the room to.</summary>
    static bool IsAboutAPass(CarAction action) => action is CarAction.Overtake or CarAction.BackUp;

    /// <summary>
    /// <b>Whether a car's action drives the route's own line</b> — and so plans down it (TER-4c.1): following it,
    /// getting past something on it, moving across off it, backing down it for room, or coming up it to where it waits
    /// for its bay.
    /// </summary>
    public bool DrivesTheRoute(int car) =>
        ground.Cars.Action[car] switch
        {
            CarAction.Follow or CarAction.Overtake or CarAction.Switch or CarAction.BackUp => true,
            CarAction.Park => !manoeuvres.IsBegun(car),
            _ => false,
        };

    /// <summary>Whether a car's line is a piece of its manoeuvre rather than the route's chain.</summary>
    public bool IsManoeuvring(int car) =>
        ground.Cars.Action[car] == CarAction.Unpark
        || (ground.Cars.Action[car] == CarAction.Park && manoeuvres.IsBegun(car));

    /// <summary>Whether an action plans down the route's own line — a car getting into a bay does, up to where it waits.</summary>
    static bool PlansDownTheRoute(CarAction action) =>
        action is CarAction.Follow or CarAction.Overtake or CarAction.Switch or CarAction.BackUp or CarAction.Park;
}
