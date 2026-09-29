using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.Agents.Car.Control;

/// <summary>
/// Where a car goes when nothing has told it: at every junction it draws one of the turns the graph
/// offers, weighted by what that turn is priced at.
/// </summary>
/// <remarks>
/// <para>
/// This is not a route and does not pretend to be. It is the smallest thing that keeps cars on the road
/// indefinitely so that the follower,
/// the tyres, the looking and the claims can be watched doing their work, and the seam it is pushed
/// through is the one a router uses.
/// </para>
/// <para>
/// The prices are the router's own — half a car length for a near-side turn, four across the oncoming
/// stream — so a toured town and a routed one prefer the same roads. They are a preference between routes
/// and never a time.
/// </para>
/// <para>
/// <b>A tour turns nowhere a route could not</b> (TER-5f): the graph offers it no way back down the
/// stretch it is on, so a car with nothing told it stays out of the streets it could only leave by
/// turning at a car park — which is a leg's manoeuvre and not a thing to wander into.
/// </para>
/// </remarks>
internal static class LaneTour
{
    public static int NextLane(RoadGraph graph, SimConfig config, int lane, ref Rng draw)
    {
        var connectors = graph.ConnectorsFrom(lane);
        var total = 0f;
        var streets = 0;
        foreach (var connector in connectors)
        {
            total += Weight(graph, config, connector);
            if (!graph.IsABayArm(graph.ConnectorTo(connector))) streets++;
        }

        if (streets == 0) return CarFleetNoLane;
        if (total <= 0f) return TheStreetDrawn(graph, connectors, draw.NextInt(streets));

        var drawn = draw.NextFloat(0f, total);
        foreach (var connector in connectors)
        {
            drawn -= Weight(graph, config, connector);
            if (drawn <= 0f) return graph.ConnectorTo(connector);
        }

        return TheStreetDrawn(graph, connectors, streets - 1);
    }

    /// <summary>The <paramref name="nth"/> of the movements onto the carriageway, a car park's arms not counted.</summary>
    static int TheStreetDrawn(RoadGraph graph, ConnectorRun connectors, int nth)
    {
        foreach (var connector in connectors)
        {
            var onto = graph.ConnectorTo(connector);
            if (graph.IsABayArm(onto)) continue;
            if (nth-- == 0) return onto;
        }

        return CarFleetNoLane;
    }

    /// <summary>
    /// The cheaper the turn, the likelier it is drawn — at the prices the router quotes. <b>Never onto a car
    /// park's arm</b> (GEN-4h): an arm is a bay, driven only as the bay's own ways.
    /// </summary>
    static float Weight(RoadGraph graph, SimConfig config, int connector)
    {
        if (graph.IsABayArm(graph.ConnectorTo(connector))) return 0f;

        // A lane with no connector out of it is a dead end, and nothing turns a car round in one: driven
        // in, a car stands at the end until its leg's clock runs out. Declining it keeps a car nobody is
        // routing on roads it can drive off again; it is not a rule — a dead end is a real place and a real
        // driver goes down it.
        if (graph.ConnectorsFrom(graph.ConnectorTo(connector)).Count == 0) return 0f;

        return graph.KindOf(connector) switch
        {
            LaneTurn.Straight => 1f,
            LaneTurn.NearSide => 1f / (1f + config.Driving.TurnPriceNearSideCarLengths),
            _ => 1f / (1f + config.Driving.TurnPriceAcrossOncomingCarLengths),
        };
    }

    const int CarFleetNoLane = -1;
}
