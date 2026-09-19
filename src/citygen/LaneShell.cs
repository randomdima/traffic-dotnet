using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>The outside of the ground the town is driven over</b>: every lane, every movement through a box and
/// every way into a bay taken as the band of ground it covers, and all of those merged into one shape
/// (<see cref="BandShell"/>, GEN-4b, TER-5).
/// </summary>
/// <remarks>
/// <para>
/// <b>It is which lines the town hands over and nothing else.</b> The merge is a fact about bands and knows
/// no more about a lane than about any other line with a width (<see cref="BandShell"/>); what is the town's
/// is that the lines it covers are the driven ones — <see cref="Paving.DrivenCount"/>, at
/// <see cref="Paving.DrivenWidthM"/> — and that the index they are weighed against is the town's own
/// (<see cref="Paving.DrivenLines"/>) rather than a second copy laid for this.
/// </para>
/// <para>
/// <b>A lane is an area and not a line</b>, which is what makes this the boundary it is: the perimeter of a
/// plain road is the outer edge of its outer lanes, and at a junction it is whatever piece of whichever band
/// reaches past the rest. A junction has no shape of its own to merge (TER-5).
/// </para>
/// </remarks>
internal static class LaneShell
{
    /// <summary>
    /// <b>The town's driven ground merged into one shape.</b> Laid on the first ask and not before
    /// (<see cref="Paving.Perimeter"/>): nothing the town needs to be laid reads it.
    /// </summary>
    public static BandShell Of(Paving paving, SimConfig config)
    {
        var count = paving.DrivenCount;
        var lines = new ArcSeg[count][];
        var widthM = new float[count];

        for (var line = 0; line < count; line++)
        {
            lines[line] = paving.ArcsOfDriven(line).ToArray();
            widthM[line] = paving.DrivenWidthM(line);
        }

        return BandShell.Of(lines, widthM, paving.DrivenLines(config));
    }
}
