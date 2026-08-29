using TrafficSimulation.App.Screen;

namespace TrafficSimulation.App.Hud;

/// <summary>
/// The one ladder of round numbers the legend and the ruler are both graduated on, and the one way a
/// distance is written with its unit.
/// </summary>
/// <remarks>
/// <b>They share it because they are read against each other.</b> A tape graduated on one ladder and
/// a legend on another is two instruments that disagree about what a round number is, and the pair of
/// reference frames exists precisely to be compared.
/// </remarks>
internal static class Ladder
{
    /// <summary>
    /// The round number at or below <paramref name="roughM"/>: 1, 2 or 5 times a power of ten. A
    /// graduation at 3.7 m is not a graduation anybody reads a distance off.
    /// </summary>
    public static float StepM(float roughM)
    {
        if (!float.IsFinite(roughM) || roughM <= 0f) return 1f;

        var decade = MathF.Pow(10f, MathF.Floor(MathF.Log10(roughM)));
        var mantissa = roughM / decade;
        return decade * (mantissa >= 5f ? 5f : mantissa >= 2f ? 2f : 1f);
    }

    /// <summary>
    /// A distance with the unit that suits it — centimetres under a metre, kilometres over a
    /// thousand, metres in between — written into the caller's buffer rather than into a string.
    /// </summary>
    public static void WriteDistance(ref TextBuffer into, float metres)
    {
        WriteFigure(ref into, metres, alongsideM: metres);

        var magnitude = MathF.Abs(metres);
        into.Add(magnitude < 1f ? " cm" : magnitude >= 1000f ? " km" : " m");
    }

    /// <summary>
    /// The bare number, in whatever unit <see cref="WriteDistance"/> would give
    /// <paramref name="alongsideM"/> — the marks of a bar whose last figure is the one carrying the
    /// unit, so that a 40 cm bar is graduated 0, 20, 40 cm and not 0, 0, 40 cm.
    /// </summary>
    public static void WriteFigure(ref TextBuffer into, float metres, float alongsideM)
    {
        var magnitude = MathF.Abs(alongsideM);
        if (magnitude < 1f)
        {
            into.Add(metres * 100f, "F0");
            return;
        }

        if (magnitude >= 1000f)
        {
            into.Add(metres / 1000f, magnitude >= 10000f ? "F1" : "F2");
            return;
        }

        // A tenth is what tells 4.7 m from 4.8 m on a tape; on a legend's own round step there is no
        // tenth to tell, and "5.0 m" over a mark standing at five metres reads as a measurement.
        into.Add(metres, magnitude < 10f && metres != MathF.Truncate(metres) ? "F1" : "F0");
    }
}
