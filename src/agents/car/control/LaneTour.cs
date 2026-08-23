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
/// the tyres, the looking and the reservation can be watched doing their work, and the seam it is pushed
/// through is the one a router uses.
/// </para>
/// <para>
/// The prices are the router's own — half a car length for a near-side turn, four across the oncoming
/// stream, twenty for a turn-around — so a toured town and a routed one prefer the same roads. They are
/// a preference between routes and never a time.
/// </para>
/// <para>
/// <b>A turn-around is drawn only where there is nothing else</b> — which is a dead end, and is the one
/// place the manoeuvre is not a last resort but the only way out (TER-5a).
/// </para>
/// </remarks>
internal static class LaneTour
{
    public static int NextLane(RoadGraph graph, SimConfig config, int lane, ref Rng draw)
    {
        var turns = graph.TurnsFrom(lane);
        var kinds = graph.TurnKindsFrom(lane);
        if (turns.Length == 0) return CarFleetNoLane;

        var wayOut = false;
        for (var turn = 0; turn < turns.Length; turn++) wayOut |= kinds[turn] != LaneTurn.TurnAround;

        var total = 0f;
        for (var turn = 0; turn < turns.Length; turn++) total += Weight(graph, config, turns[turn], kinds[turn], wayOut);
        if (total <= 0f) return turns[draw.NextInt(turns.Length)];

        var drawn = draw.NextFloat(0f, total);
        for (var turn = 0; turn < turns.Length; turn++)
        {
            drawn -= Weight(graph, config, turns[turn], kinds[turn], wayOut);
            if (drawn <= 0f) return turns[turn];
        }

        return turns[^1];
    }

    /// <summary>The cheaper the turn, the likelier it is drawn — and a turn-around costs what the router says it costs.</summary>
    static float Weight(RoadGraph graph, SimConfig config, int lane, LaneTurn turn, bool wayOut)
    {
        // A lane into a dead end is a lane whose only way back out is a turn-around, which is a
        // manoeuvre this engine cannot yet make. Declining it keeps the fleet on roads it can actually
        // drive; it is not a rule — a dead end is a real place and a real driver goes down it.
        if (turn != LaneTurn.TurnAround && graph.LanesOut(graph.LaneToNode[lane]).Length <= 1) return 0f;

        return turn switch
        {
            LaneTurn.TurnAround => wayOut ? 0f : 1f,
            LaneTurn.Straight => 1f,
            LaneTurn.NearSide => 1f / (1f + config.Driving.TurnPriceNearSideCarLengths),
            _ => 1f / (1f + config.Driving.TurnPriceAcrossOncomingCarLengths),
        };
    }

    const int CarFleetNoLane = -1;
}
