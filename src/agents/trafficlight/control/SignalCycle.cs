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
/// the amber is the last stretch of a green rather than time added to it — so a 15 s cycle is 7.5 s an
/// axis, of which the last 1.5 s is amber.
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
    public static SignalColour ForAxis(SimConfig config, int axis, float offsetS, float timeS)
    {
        var cycleS = config.Signals.CycleS;
        if (cycleS <= 0f) return SignalColour.Green;

        var phaseS = cycleS / Axes;
        var atS = timeS + offsetS;
        atS -= MathF.Floor(atS / cycleS) * cycleS;

        var greenAxis = Math.Min((int)(atS / phaseS), Axes - 1);
        if (greenAxis != axis) return SignalColour.Red;

        var intoS = atS - (greenAxis * phaseS);
        return intoS >= phaseS - config.Signals.AmberTailS ? SignalColour.Amber : SignalColour.Green;
    }

    /// <summary>
    /// What the crossing over a road on that axis shows. <b>A crossing's signal is the negation of its
    /// own road's</b> — green exactly while that road is fully red, amber included — which is why a
    /// walker is never shown an amber to interpret (TLT-2).
    /// </summary>
    public static SignalColour ForCrossing(SimConfig config, int roadAxis, float offsetS, float timeS) =>
        ForAxis(config, roadAxis, offsetS, timeS) == SignalColour.Red ? SignalColour.Green : SignalColour.Red;
}
