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
/// This is not a route and does not pretend to be, exactly as <see cref="Person.Control.Wander"/> is
/// not a trip. It is the smallest thing that keeps cars on the road indefinitely so that the follower,
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
        if (connectors.Count == 0) return CarFleetNoLane;

        var total = 0f;
        foreach (var connector in connectors) total += Weight(graph, config, connector);
        if (total <= 0f) return graph.ConnectorTo(connectors[draw.NextInt(connectors.Count)]);

        var drawn = draw.NextFloat(0f, total);
        foreach (var connector in connectors)
        {
            drawn -= Weight(graph, config, connector);
            if (drawn <= 0f) return graph.ConnectorTo(connector);
        }

        return graph.ConnectorTo(connectors[connectors.Count - 1]);
    }

    /// <summary>The cheaper the turn, the likelier it is drawn — at the prices the router quotes.</summary>
    static float Weight(RoadGraph graph, SimConfig config, int connector)
    {
        // A lane with no connector out of it is a dead end, and the way back out of one is a park and an
        // unpark in a bay of its own. Declining it keeps a car nobody is routing on roads it can drive off
        // again; it is not a rule — a dead end is a real place and a real driver goes down it.
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
