using TrafficSimulation.Core.Config;

namespace TrafficSimulation.Agents.TrafficLight.Control;

/// <summary>What a signal is showing. A crossing never shows <see cref="Amber"/> (TLT-2).</summary>
internal enum SignalColour : byte
{
    Red,
    Amber,
    Green,
}

/// <summary>
/// The cycle table: which colour an <b>axis</b> of a junction is showing at a moment. Engine-free and
/// stateless, because the safety argument is the table's shape and a table with state in it would have
/// to be argued about instead (TLT-4).
/// </summary>
/// <remarks>
/// <para>
/// <b>Conflicting greens are impossible by the shape of this function</b>, not by a check: it takes an
/// axis, and one of the two axes is green at a time. Both ends of one road are the same axis, so they
/// always agree; the two axes are never the same axis, so they never agree.
/// </para>
/// <para>
/// <b>There is no all-red phase</b> (TLT-4). The box is emptied by the amber tail and by yielding, and
/// the amber is the last stretch of a green rather than time added to it — so each axis has half the cycle,
/// of which the last <see cref="SignalFigures.AmberTailS"/> is amber.
/// </para>
/// <para>
/// <b>The phase is derived from a global clock plus the junction's own offset</b>, so there is no
/// per-bundle state to drift and no update to run: a light is a timer that publishes colours, and this
/// is the whole timer.
/// </para>
/// </remarks>
internal static class SignalCycle
{
    public const int Axes = 2;

    /// <summary>What the given axis of a junction shows, its bundle's offset being <paramref name="offsetS"/>.</summary>
    public static SignalColour ForAxis(SimConfig config, int axis, float offsetS, float timeS) =>
        Shown(axis, At(config, offsetS, timeS));

    /// <summary>Where a junction's cycle stands at a moment, which is everything any of its axes is shown off.</summary>
    public static JunctionPhase At(SimConfig config, float offsetS, float timeS)
    {
        var cycleS = config.Signals.CycleS;
        if (cycleS <= 0f) return JunctionPhase.NoCycle;

        var phaseS = cycleS / Axes;
        var atS = timeS + offsetS;
        atS -= MathF.Floor(atS / cycleS) * cycleS;

        var greenAxis = Math.Min((int)(atS / phaseS), Axes - 1);
        var intoS = atS - (greenAxis * phaseS);
        return new JunctionPhase(greenAxis, intoS >= phaseS - config.Signals.AmberTailS);
    }

    /// <summary>What an axis is shown with its junction's cycle where <paramref name="phase"/> says.</summary>
    public static SignalColour Shown(int axis, JunctionPhase phase) =>
        phase.GreenAxis == JunctionPhase.EveryAxis || (phase.GreenAxis == axis && !phase.InTheAmber)
            ? SignalColour.Green
            : phase.GreenAxis == axis ? SignalColour.Amber : SignalColour.Red;

    /// <summary>What the crossing over a road on <paramref name="roadAxis"/> is shown (<see cref="ForCrossing"/>).</summary>
    public static SignalColour ShownToTheCrossing(int roadAxis, JunctionPhase phase) =>
        Shown(roadAxis, phase) == SignalColour.Red ? SignalColour.Green : SignalColour.Red;

    /// <summary>
    /// What the crossing over a road on that axis shows. <b>A crossing's signal is the negation of its
    /// own road's</b> — green exactly while that road is fully red, amber included — which is why a
    /// walker is never shown an amber to interpret (TLT-2).
    /// </summary>
    public static SignalColour ForCrossing(SimConfig config, int roadAxis, float offsetS, float timeS) =>
        ShownToTheCrossing(roadAxis, At(config, offsetS, timeS));
}

/// <summary>
/// <b>Where one junction's cycle stands</b> (<see cref="SignalCycle.At"/>): the axis that is green, or
/// <see cref="EveryAxis"/> where there is no cycle, and whether that green is in its amber tail.
/// </summary>
internal readonly record struct JunctionPhase(int GreenAxis, bool InTheAmber)
{
    public const int EveryAxis = -1;

    public static JunctionPhase NoCycle => new(EveryAxis, false);
}
