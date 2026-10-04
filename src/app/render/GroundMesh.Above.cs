using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.App.Render;

/// <summary>The decks, and the level above the ground drawn as roads of its own.</summary>
internal sealed partial class GroundMesh
{
    /// <summary>
    /// <b>Every deck of one level</b>, each its road's own line stroked out to the deck's width, every rim before
    /// any deck so where two decks meet the one is not edged across the other. On the level above, the movements
    /// through a junction on it are decked too, at a lane and a walk either side: a road's deck stops at the mouth
    /// of its box, and the ground would show through the box.
    /// </summary>
    void Decks(CityPlan plan, LaneLines lanes, byte level, SimConfig config)
    {
        var rimM = config.Road.EdgeLineWidthM * 2f;
        foreach (var (shade, inM) in (ReadOnlySpan<(Vector3, float)>)[(Edge, 0f), (Plain, rimM)])
        {
            for (var bridge = 0; bridge < plan.Bridges.Count; bridge++)
            {
                var road = plan.Bridges.Road[bridge];
                if (road < 0 || plan.Roads.LevelOf(road) != level) continue;

                Stroke(plan.Roads.SegmentsOf(road), plan.Bridges.DeckWidthM[bridge] - inM, closed: false, Surface.Deck, shade);
            }

            if (level == CityPlan.RoadArrays.Ground) continue;

            for (var connector = 0; connector < lanes.ConnectorCount; connector++)
            {
                if (lanes.ConnectorLevel(connector) != level) continue;

                var widthM = lanes.ConnectorWidthM(connector) + (config.WalkOuterM * 2f) - inM;
                Stroke(lanes.ArcsOfConnector(connector), widthM, closed: false, Surface.Deck, shade);
            }
        }
    }

    /// <summary>
    /// <b>The level above, as a road of its own over the ground and over the bodies on it</b> (TER-7b, PHY-1a): the
    /// decks, then the carriageway kerb to kerb (<see cref="Paving.Above"/>), then its kerb, then its paint — the
    /// ground's own stack again for the bridges over other roads, and nothing in it that is not on a bridge.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Its kerb stops where it lands</b>: at a bridgehead the bridge's end is the ground's road carried on, so the
    /// stretch of its boundary straight across the end is not struck (<see cref="Landings"/>). The ground runs on
    /// under it there (<see cref="SimConfig.UnderTheDeckM"/>), so the end of the deck meets tarmac. A bridge running
    /// off the map is cut by its edge, as the ground is.
    /// </para>
    /// <para>
    /// <b>Filled from the line itself and not thinned under a kerb</b>, because there is no kerb across its ends:
    /// a fill a share of a kerb short of a bridge's end is a stripe of deck across the road.
    /// </para>
    /// </remarks>
    void Above(
        CityPlan plan, Paving paving, SimConfig config, Crossings held, StopBars bars, LaneArrows arrows,
        CentrelineRuns runs, Crossings surveyed)
    {
        var rings = paving.Above(config);
        if (rings.Length == 0) return;

        const byte above = CityPlan.RoadArrays.Over;
        var kerbM = config.Road.KerbWidthM;
        Decks(plan, paving.Lanes, above, config);

        var line = MapCut.Rings(Line(rings), plan.WorldSizeM);
        Shell(line, Surface.Tarmac, Plain);
        KerbAbove(line, Landings(paving.Lanes), plan.WorldSizeM, kerbM * 0.5f, kerbM);

        LaneDashes(runs, plan, config, held, Paint, above);
        Zebras(surveyed, plan, config, Paint, above);
        Bars(bars, paving.Lanes, Paint, above);
        Arrows(paving.Lanes, arrows, config, Paint, above);
    }

    /// <summary>
    /// One end of a bridge where it lands on the ground: the line across its lanes' ends, as a place on it, the way
    /// along the bridge and how far either side of that place the lanes reach.
    /// </summary>
    readonly record struct Landing(Vector2 AtM, Vector2 Along, float FromM, float ToM);

    /// <summary>Every end of a lane above the ground at a bridgehead, gathered by road end into the line across it.</summary>
    static List<Landing> Landings(LaneLines lanes)
    {
        var landings = new List<Landing>();
        var byEnd = new Dictionary<(int Road, int Junction), int>();
        for (var lane = 0; lane < lanes.LaneCount; lane++)
        {
            if (lanes.LaneLevel[lane] == CityPlan.RoadArrays.Ground) continue;

            var arcs = lanes.ArcsOf(lane);
            foreach (var atStart in (ReadOnlySpan<bool>)[true, false])
            {
                var junction = atStart ? lanes.LaneFromJunction[lane] : lanes.LaneToJunction[lane];
                if (!lanes.IsBridgehead(junction)) continue;

                var end = Spline.SampleAt(arcs, atStart ? 0f : lanes.LaneLengthM[lane]);
                if (!byEnd.TryGetValue((lanes.LaneRoad[lane], junction), out var at))
                {
                    at = landings.Count;
                    byEnd[(lanes.LaneRoad[lane], junction)] = at;
                    landings.Add(new Landing(end.PositionM, end.Direction, 0f, 0f));
                }

                var landing = landings[at];
                var acrossM = Vector2.Dot(end.PositionM - landing.AtM, Heading.RightOf(landing.Along));
                var halfM = lanes.LaneWidthM[lane] * 0.5f;
                landings[at] = landing with
                {
                    FromM = MathF.Min(landing.FromM, acrossM - halfM), ToM = MathF.Max(landing.ToM, acrossM + halfM),
                };
            }
        }

        return landings;
    }

    /// <summary>
    /// The level above's kerb: each ring struck as the town's kerb is (<see cref="Paint"/>), less every stretch that
    /// runs straight across a bridge's end or along the map's edge.
    /// </summary>
    void KerbAbove(
        ReadOnlySpan<Vector2[]> line, List<Landing> landings, Vector2 worldM, float withinM, float kerbM)
    {
        Stroke(line, Across, kerbM, Surface.Tarmac, Paint);

        bool Across(Vector2 fromM, Vector2 toM)
        {
            if (MapCut.AlongTheEdge(fromM, toM, worldM)) return true;

            foreach (var landing in landings)
            {
                if (On(landing, fromM) && On(landing, toM)) return true;
            }

            return false;
        }

        bool On(in Landing landing, Vector2 pointM)
        {
            var offM = pointM - landing.AtM;
            var acrossM = Vector2.Dot(offM, Heading.RightOf(landing.Along));
            return MathF.Abs(Vector2.Dot(offM, landing.Along)) <= withinM
                   && acrossM >= landing.FromM - withinM && acrossM <= landing.ToM + withinM;
        }
    }
}
