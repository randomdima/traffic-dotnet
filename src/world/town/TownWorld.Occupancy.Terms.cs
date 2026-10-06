namespace TrafficSimulation.World.Town;

/// <summary>What a car's body is laid as (<see cref="TownWorld.LayingOf"/>).</summary>
/// <param name="Occupant">The car it is held under: itself, or the truck it hangs from (EVA-5).</param>
internal readonly record struct CarLaying(int Occupant, bool Travelling, bool RunsOnPastItself, bool Blocked);

/// <summary>Where a claims rebuild has got to, as the town hands it to a watcher (<see cref="TownWorld.WatchingTheClaims"/>).</summary>
internal enum ClaimsStage
{
    /// <summary>Begun, with nothing laid.</summary>
    Begun,

    /// <summary>Every body, pass and light laid, and no car's claim yet cleared and laid again.</summary>
    PlansDue,

    /// <summary>Every plan laid whole, and none answered.</summary>
    PlansLaid,

    /// <summary>Every plan answered and cut where its answer says.</summary>
    PlansAnswered,

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

    /// <summary>For the instruments: the room this car keeps short of something it may get past (<c>Overtaking.KeptOffWhatItMayPassM</c>).</summary>
    public float KeptOffWhatItMayPassM(int car) => _overtaking.KeptOffWhatItMayPassM(car);

    /// <summary>For the instruments: the hold this car's claim laid this rebuild, or none.</summary>
    public int PlanHoldOf(int car) => _ground.PlanHold[car];
}
