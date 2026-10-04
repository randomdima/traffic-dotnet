using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>The outside of the ground the town is driven over</b>: every lane, every movement through a box and every
/// bay taken as the band of ground it covers, and all of those merged into one shape (<see cref="BandShell"/>,
/// GEN-4b, TER-5) — <b>one a level</b> (TER-7b).
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
/// plain road is the outer edge of its outer lanes — its roadside, where it has one (<see cref="LaneLines.IsRoadside"/>)
/// — and at a junction it is whatever piece of whichever band reaches past the rest. A junction has no shape of its
/// own to merge (TER-5). <b>The one band that is no lane is a taper</b> (<see cref="LaneLines.Tapers"/>): where a road
/// loses its roadside, the outline would turn round the strip's square end, and a rounding cannot take a corner that
/// sticks out off without cutting the lane it is the corner of — so the ground eased in over is handed over as a band.
/// </para>
/// <para>
/// <b>Two lanes laid over one line are one band</b> (<see cref="LaneLines.LaneOverOneLine"/>): a lane and its
/// reverse there cover the same ground, so only the one running with its road is handed over and the other is
/// handed over empty, keeping the numbering the index is in. Handed over twice, the two bands' ends lie on one
/// another facing opposite ways, and where nothing else covers them — a dead end of one lane both ways share —
/// the merge keeps half of the one end and neither half of the other, and the boundary does not close.
/// </para>
/// <para>
/// <b>A line on another level is handed over empty</b>, for the same reason: a bridge over a road shares no
/// ground with it (PHY-1a), so the two are two shapes and never one. <b>The ground runs on under a bridge past
/// each bridgehead</b> (<see cref="SimConfig.UnderTheDeckM"/>) — the first metres of every lane above handed to
/// the ground's merge as well — so the end the ground's boundary turns round stands under the deck drawn over it
/// and the road meets the bridge straight. <b>And on past the map's edge where a road runs off it</b>
/// (<see cref="OffTheMap"/>), so the end it turns round is off the map and what the map keeps of the boundary is
/// cut by the edge rather than turned on it (GEN-2b).
/// </para>
/// </remarks>
internal static class LaneShell
{
    /// <summary>
    /// <b>The town's driven ground on one level merged into one shape.</b> Laid on the first ask and not
    /// before (<see cref="Paving.Perimeter"/>, <see cref="Paving.Above"/>): nothing the town needs to be laid
    /// reads it.
    /// </summary>
    public static BandShell Of(Paving paving, SimConfig config, byte level)
    {
        var (lines, widthM) = Bands(paving, config, level);
        return BandShell.Of(lines, widthM, lines.Length == paving.DrivenCount ? paving.DrivenLines(config) : Indexed(lines, config));
    }

    /// <summary>
    /// <b>Every band the driven ground on one level is merged from</b>, a line and its width each: the town's driven
    /// lines under their own numbers — those on another level or laid over a twin's line empty — and after them the
    /// stubs under the decks and past the map's edge, and the tapers a kerb eases in over where a roadside is lost
    /// (<see cref="LaneLines.Tapers"/>).
    /// </summary>
    internal static (ArcSeg[][] Lines, float[] WidthM) Bands(Paving paving, SimConfig config, byte level)
    {
        var count = paving.DrivenCount;
        var lines = new List<ArcSeg[]>(count);
        var widthM = new List<float>(count);
        var lanes = paving.Lanes;

        for (var line = 0; line < count; line++)
        {
            lines.Add(Twin(lanes, line) || paving.DrivenLevel(line) != level ? [] : paving.ArcsOfDriven(line).ToArray());
            widthM.Add(paving.DrivenWidthM(line));
        }

        if (level == CityPlan.RoadArrays.Ground) UnderTheDecks(paving, config, lines, widthM);
        OffTheMap(paving, config, level, lines, widthM);
        foreach (var taper in lanes.Tapers)
        {
            if (taper.Level != level) continue;

            lines.Add(taper.Line);
            widthM.Add(taper.WidthM);
        }

        return ([.. lines], [.. widthM]);
    }

    /// <summary>The bands' own index, for a set of them that is not the town's driven lines under their numbers.</summary>
    internal static ChainIndex Indexed(ReadOnlySpan<ArcSeg[]> lines, SimConfig config)
    {
        var building = new ChainIndex.Builder();
        for (var line = 0; line < lines.Length; line++)
        {
            building.Add(line, lines[line], Spline.TotalLengthM(lines[line]));
        }

        return building.Seal(config.Grid.Main);
    }

    static bool Twin(LaneLines lanes, int line) =>
        line < lanes.LaneCount && lanes.LaneOverOneLine[line] && !lanes.LaneForward[line] && lanes.LaneReverse[line] >= 0;

    /// <summary>
    /// The first metres of every lane above the ground at each bridgehead it ends at, as lines of the ground's own
    /// (<see cref="SimConfig.UnderTheDeckM"/>): numbered after the town's driven lines, so they need an index of
    /// their own.
    /// </summary>
    static void UnderTheDecks(Paving paving, SimConfig config, List<ArcSeg[]> lines, List<float> widthM)
    {
        var lanes = paving.Lanes;
        if (!lanes.Levelled) return;

        var scratch = new ArcSeg[lanes.LaneArcs.Length];
        for (var lane = 0; lane < lanes.LaneCount; lane++)
        {
            if (lanes.LaneLevel[lane] == CityPlan.RoadArrays.Ground || Twin(lanes, lane)) continue;

            var lengthM = lanes.LaneLengthM[lane];
            var stubM = MathF.Min(config.UnderTheDeckM, lengthM);
            if (lanes.IsBridgehead(lanes.LaneFromJunction[lane])) Stub(0f, stubM);
            if (lanes.IsBridgehead(lanes.LaneToJunction[lane])) Stub(lengthM - stubM, lengthM);

            void Stub(float fromM, float toM)
            {
                var pieces = Spline.SubChainInto(lanes.ArcsOf(lane), fromM, toM, scratch);
                if (pieces == 0) return;

                lines.Add(scratch.AsSpan(0, pieces).ToArray());
                widthM.Add(lanes.LaneWidthM[lane]);
            }
        }
    }

    /// <summary>
    /// <b>Every lane that runs off the map carried on straight past its edge</b> (<see cref="CityPlan.JunctionArrays.RunsOffTheMap"/>,
    /// GEN-2b): out to where the whole of its width has left the map and <see cref="SimConfig.PastTheMapEdgeM"/> on, so
    /// the boundary turns round its end off the map and the map keeps a road that runs to its edge. Numbered after the
    /// town's driven lines, as the decks' are.
    /// </summary>
    static void OffTheMap(Paving paving, SimConfig config, byte level, List<ArcSeg[]> lines, List<float> widthM)
    {
        var junctions = paving.Of.Junctions;
        if (junctions.RunsOffTheMap.Length == 0) return;

        var lanes = paving.Lanes;
        var worldM = paving.Of.WorldSizeM;
        for (var lane = 0; lane < lanes.LaneCount; lane++)
        {
            if (lanes.LaneLevel[lane] != level || Twin(lanes, lane)) continue;

            PastTheEdge(
                junctions, worldM, config, lanes.ArcsOf(lane), lanes.LaneWidthM[lane], lanes.LaneFromJunction[lane],
                lanes.LaneToJunction[lane], lines, widthM);
        }
    }

    /// <summary>
    /// A band run on straight past the map's edge from whichever of its ends stands where a road runs off the map: out
    /// to where the whole of its width has left the map and <see cref="SimConfig.PastTheMapEdgeM"/> on.
    /// </summary>
    static void PastTheEdge(
        CityPlan.JunctionArrays junctions, Vector2 worldM, SimConfig config, ReadOnlySpan<ArcSeg> arcs, float bandM,
        int fromJunction, int toJunction, List<ArcSeg[]> lines, List<float> widthM)
    {
        if (junctions.RunsOff(fromJunction)) Stub(arcs[0].StartM, arcs[0].HeadingRad + MathF.PI);
        if (junctions.RunsOff(toJunction)) Stub(arcs[^1].EndM, arcs[^1].HeadingAtRad(arcs[^1].LengthM));

        void Stub(Vector2 fromM, float headingRad)
        {
            var along = Heading.Unit(headingRad);
            var acrossM = Heading.RightOf(along) * (bandM * 0.5f);
            var leftM = MathF.Max(OutOf(worldM, fromM + acrossM, along), OutOf(worldM, fromM - acrossM, along));
            lines.Add([new ArcSeg(fromM, headingRad, leftM + config.PastTheMapEdgeM, 0f)]);
            widthM.Add(bandM);
        }
    }

    /// <summary>How far a place on the map goes along a direction before it leaves the map — nought from off it.</summary>
    static float OutOf(Vector2 worldM, Vector2 fromM, Vector2 along)
    {
        var leftM = float.PositiveInfinity;
        if (along.X != 0f) leftM = MathF.Min(leftM, ((along.X > 0f ? worldM.X : 0f) - fromM.X) / along.X);
        if (along.Y != 0f) leftM = MathF.Min(leftM, ((along.Y > 0f ? worldM.Y : 0f) - fromM.Y) / along.Y);
        return MathF.Max(0f, leftM);
    }
}
