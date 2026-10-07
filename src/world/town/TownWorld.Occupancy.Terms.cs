using TrafficSimulation.Agents.TrafficLight.Control;

namespace TrafficSimulation.World.Town;

/// <summary>What a car's body is laid as (<see cref="TownWorld.LayingOf"/>).</summary>
/// <param name="Occupant">The car it is held under: itself, or the truck it hangs from (EVA-5).</param>
internal readonly record struct CarLaying(int Occupant, bool Travelling, bool RunsOnPastItself, bool Blocked);

/// <summary>Where a claims rebuild has got to, as the town hands it to a watcher (<see cref="TownWorld.WatchingTheClaims"/>).</summary>
internal enum ClaimsStage
{
    /// <summary>Begun, with nothing laid.</summary>
    Begun,

    /// <summary>Every walker stationed on its walk, and nothing laid.</summary>
    Stationed,

    /// <summary>Every walker's body laid, and no car's.</summary>
    WalkersLaid,

    /// <summary>Every body laid, and no pass.</summary>
    CarsLaid,

    /// <summary>Every pass laid — an overtake's, a switch's, a manoeuvre's and a sidestep's — and no light.</summary>
    PassesLaid,

    /// <summary>Every body, pass and light laid, and no car's claim yet cleared and laid again.</summary>
    PlansDue,

    /// <summary>
    /// Every claim of a car that plans nothing down its line laid where it stands — a hand's, a truck's, a manoeuvre's —
    /// and no plan down a line.
    /// </summary>
    DrivesDue,

    /// <summary>Every car's plan laid whole, and no walker's.</summary>
    WalksDue,

    /// <summary>Every plan laid whole, and none answered.</summary>
    PlansLaid,

    /// <summary>Every plan answered and cut where its answer says.</summary>
    PlansAnswered,

    /// <summary>Every car's grant read, and no walker's.</summary>
    WalkersDue,

    /// <summary>Every car's and walker's grant read back off what its plan came to: the rebuild done.</summary>
    GrantsRead,
}

/// <summary>
/// <b>The claims handed to the instruments</b> — for a device laying and answering them beside the town
/// (<c>app/devicetick</c>). Nothing that decides anything reads them.
/// </summary>
internal sealed partial class TownWorld
{
    /// <summary>
    /// <b>For the instruments</b>: handed the town at every stage of every claims rebuild
    /// (<see cref="RebuildLaneOccupancy"/>). Nothing that decides anything sets it.
    /// </summary>
    public Action<TownWorld, ClaimsStage>? WatchingTheClaims { get; set; }

    void Watch(ClaimsStage stage) => WatchingTheClaims?.Invoke(this, stage);

    /// <summary>
    /// For the instruments: <b>what a car's body is laid as</b> (<see cref="LayTheCarsBody"/>) — the car it is held under,
    /// whether that car drives its route's line, whether that line runs on past it, and whether it is blocked.
    /// </summary>
    public CarLaying LayingOf(int car)
    {
        var occupant = LaidAs(car);
        return new CarLaying(occupant, IsUnderWay(occupant), _ground.RunsOnPastItself(occupant), Cars.Context[occupant].Blocked);
    }

    /// <summary>For the instruments: whether this car's claim this rebuild is a plan down its own line (<see cref="PlansDownItsLine"/>).</summary>
    public bool PlansItsLine(int car) => PlansDownItsLine(car);

    /// <summary>
    /// For the instruments: <b>the cars this laying asked</b>, in index order — the only ones whose claim was cleared and
    /// laid again and whose grant was read; every other stands as it was laid last (<see cref="LayTheCarsBodies"/>).
    /// </summary>
    public ReadOnlySpan<int> CarsAskedThisLaying => CarsAsked;

    /// <summary>For the instruments: the room this car keeps short of something it may get past (<c>Overtaking.KeptOffWhatItMayPassM</c>).</summary>
    public float KeptOffWhatItMayPassM(int car) => _overtaking.KeptOffWhatItMayPassM(car);

    /// <summary>For the instruments: the hold this car's claim laid this rebuild, or none.</summary>
    public int PlanHoldOf(int car) => _ground.PlanHold[car];

    /// <summary>For the instruments: the stretches the lights hold (<see cref="LightTheWays"/>).</summary>
    public SignalHolds SignalHolds => _signalHolds;

    /// <summary>For the instruments: where this car was sent to stop and how near it has to stand (<see cref="TheSceneOf"/>).</summary>
    public bool SceneOf(int car, out System.Numerics.Vector2 placeM, out float reachM) => TheSceneOf(car, out placeM, out reachM);
}
