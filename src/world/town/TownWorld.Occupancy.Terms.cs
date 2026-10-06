namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The claims handed to the instruments</b> — for a device answering the plans beside the town
/// (<c>app/devicetick/DeviceClaims</c>). Nothing that decides anything reads them.
/// </summary>
internal sealed partial class TownWorld
{
    /// <summary>
    /// <b>For the instruments</b>: handed the town with every plan laid whole and none answered yet, and again once the
    /// answers are laid (<see cref="AnswerThePlans"/>). Nothing that decides anything sets either.
    /// </summary>
    public Action<TownWorld>? BeforeThePlansAreAnswered { get; set; }

    /// <inheritdoc cref="BeforeThePlansAreAnswered"/>
    public Action<TownWorld>? AfterThePlansAreAnswered { get; set; }
}
