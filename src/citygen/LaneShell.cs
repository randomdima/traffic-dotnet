using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>The outside of the ground the town is driven over</b>: every lane, every movement through a box and
/// every bay taken as the band of ground it covers, and all of those merged into one shape
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
/// <para>
/// <b>Two lanes laid over one line are one band</b> (<see cref="LaneLines.LaneOverOneLine"/>): a lane and its
/// reverse there cover the same ground, so only the one running with its road is handed over and the other is
/// handed over empty, keeping the numbering the index is in. Handed over twice, the two bands' ends lie on one
/// another facing opposite ways, and where nothing else covers them — a dead end of one lane both ways share —
/// the merge keeps half of the one end and neither half of the other, and the boundary does not close.
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
        var lanes = paving.Lanes;

        for (var line = 0; line < count; line++)
        {
            var twin = line < lanes.LaneCount && lanes.LaneOverOneLine[line] && !lanes.LaneForward[line] && lanes.LaneReverse[line] >= 0;
            lines[line] = twin ? [] : paving.ArcsOfDriven(line).ToArray();
            widthM[line] = paving.DrivenWidthM(line);
        }

        return BandShell.Of(lines, widthM, paving.DrivenLines(config));
    }
}
