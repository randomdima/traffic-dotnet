namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The claims handed to the instruments</b> — for a device laying and answering the plans beside the town
/// (<c>app/devicetick/DevicePlans</c>, <c>DeviceClaims</c>). Nothing that decides anything reads them.
/// </summary>
internal sealed partial class TownWorld
{
    /// <summary>
    /// <b>For the instruments</b>: handed the town once every body, pass and light is laid and before any car's claim is
    /// cleared and laid again (<see cref="RebuildLaneOccupancy"/>). Nothing that decides anything sets it.
    /// </summary>
    public Action<TownWorld>? BeforeThePlansAreLaid { get; set; }

    /// <summary>
    /// <b>For the instruments</b>: handed the town with every plan laid whole and none answered yet, and again once the
    /// answers are laid (<see cref="AnswerThePlans"/>). Nothing that decides anything sets either.
    /// </summary>
    public Action<TownWorld>? BeforeThePlansAreAnswered { get; set; }

    /// <inheritdoc cref="BeforeThePlansAreAnswered"/>
    public Action<TownWorld>? AfterThePlansAreAnswered { get; set; }

    /// <summary>For the instruments: whether this car's claim this rebuild is a plan down its own line (<see cref="PlansDownItsLine"/>).</summary>
    public bool PlansItsLine(int car) => PlansDownItsLine(car);

    /// <summary>For the instruments: the room this car keeps short of something it may get past (<c>Overtaking.KeptOffWhatItMayPassM</c>).</summary>
    public float KeptOffWhatItMayPassM(int car) => _overtaking.KeptOffWhatItMayPassM(car);

    /// <summary>For the instruments: the hold this car's claim laid this rebuild, or none.</summary>
    public int PlanHoldOf(int car) => _ground.PlanHold[car];
}
