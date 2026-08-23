using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Routing;

namespace TrafficSimulation.World.Road;

/// <summary>
/// The driving side of the global tier: <b>the road graph contracted so that a node is a junction a
/// driver actually chooses at</b>, and a link is the whole run of road between two of them, one way.
/// </summary>
/// <remarks>
/// <para>
/// <b>Turn prices are the substance of this graph.</b> A near-side turn is nearly free, a turn across the
/// oncoming stream costs a gap that has to be waited for, and a turn-around is the longest single junction
/// occupancy there is — so a driver takes three sides of a block over one whenever the block is there to
/// take. A planner blind to this hands drivers routes made of the most expensive manoeuvres available and
/// the town then jams at the junctions. The prices are quoted in a <b>nominal</b> car length and never in
/// the length of whichever car is asking: the network is laid once for a whole town, and what a turn price
/// expresses is a preference between routes.
/// </para>
/// <para>
/// <b>A turn a body cannot make is priced, not refused</b> — a fact about a body is not the global tier's,
/// and the manoeuvre refuses the shape if it comes to it.
/// </para>
/// </remarks>
internal sealed class DrivingNetwork
{
    readonly RoadGraph _roads;
    readonly int[] _linkOfLane;
    readonly int[] _slotOfLane;

    DrivingNetwork(RoadGraph roads, RunNetwork runs, int[] linkOfLane, int[] slotOfLane)
    {
        _roads = roads;
        Runs = runs;
        _linkOfLane = linkOfLane;
        _slotOfLane = slotOfLane;
    }

    public RunNetwork Runs { get; }

    public TravelGraph Graph => Runs.Graph;

    /// <summary>The run a lane is part of. Every lane belongs to exactly one, which is what a contraction may not lose.</summary>
    public int LinkOfLane(int lane) => _linkOfLane[lane];

    /// <summary>Where in that run the lane stands, as an index into its pieces.</summary>
    public int SlotOfLane(int lane) => _slotOfLane[lane];

    /// <summary>How far into its own run a place on a lane stands.</summary>
    public float PlaceOfM(int lane, float alongLaneM) =>
        Runs.PlaceOfM(_linkOfLane[lane], _slotOfLane[lane], alongLaneM);

    /// <summary>
    /// Where a driver joins the network. <b>A body under way enters by the link it is already on</b>,
    /// priced at what is left of it — the node nearest a car halfway down a street is regularly the one
    /// behind it, and a route anchored there is a car told to turn round.
    /// </summary>
    public RouteEntry EntryOnLane(int lane, float alongLaneM)
    {
        var link = _linkOfLane[lane];
        var alongM = PlaceOfM(lane, alongLaneM);
        return new RouteEntry(link, alongM, Runs.LengthM(link) - alongM);
    }

    /// <summary>
    /// Where a driver standing still joins it: the lane it is nearest that it is also <em>pointing
    /// along</em>, and only then the nearest centreline of any kind. The nearest line to a car standing
    /// near a centreline is as likely to be the oncoming lane as its own.
    /// </summary>
    public RouteEntry EntryNear(Vector2 pointM, Vector2 forward)
    {
        var best = -1;
        var bestAlongM = 0f;
        var bestDistanceSq = float.MaxValue;
        var bestPointing = false;

        for (var lane = 0; lane < _roads.LaneCount; lane++)
        {
            var arcs = _roads.ArcsOf(lane);
            var alongM = Spline.ProjectM(arcs, pointM, _roads.LaneLengthM[lane] * 0.5f, _roads.LaneLengthM[lane]);
            var on = Spline.SampleAt(arcs, alongM);
            var pointing = Vector2.Dot(on.Direction, forward) > 0f;
            var distanceSq = (on.PositionM - pointM).LengthSquared();
            if (bestPointing && !pointing) continue;
            if (pointing == bestPointing && distanceSq >= bestDistanceSq) continue;

            bestPointing = pointing;
            bestDistanceSq = distanceSq;
            bestAlongM = alongM;
            best = lane;
        }

        return best < 0 ? new RouteEntry(TravelGraph.NoLink, 0f, 0f) : EntryOnLane(best, bestAlongM);
    }

    /// <summary>
    /// A destination as the search takes it: <b>a place on a link</b>. Both directions of the stretch the
    /// place stands on are offered, because either may be the one that reaches it first and only the
    /// search can compare them.
    /// </summary>
    public int GoalsAt(Vector2 pointM, Span<RouteGoal> into)
    {
        var lane = _roads.NearestLane(pointM, out var alongM);
        if (lane < 0) return 0;

        var count = 0;
        into[count++] = new RouteGoal(_linkOfLane[lane], PlaceOfM(lane, alongM));

        var reverse = _roads.LaneReverse[lane];
        if (reverse >= 0 && into.Length > 1)
        {
            var backM = MathF.Max(0f, _roads.LaneLengthM[reverse] - alongM);
            into[count++] = new RouteGoal(_linkOfLane[reverse], PlaceOfM(reverse, backM));
        }

        return count;
    }

    public static DrivingNetwork Build(RoadGraph roads, CityPlan plan, SimConfig config)
    {
        var runs = RunNetwork.Contract(new Fine(roads, plan.Junctions.CentreM), new Pricer(roads, config));

        var linkOfLane = new int[roads.LaneCount];
        var slotOfLane = new int[roads.LaneCount];
        Array.Fill(linkOfLane, TravelGraph.NoLink);
        for (var link = 0; link < runs.LinkCount; link++)
        {
            var lanes = runs.PiecesOf(link);
            for (var slot = 0; slot < lanes.Length; slot++)
            {
                linkOfLane[lanes[slot]] = link;
                slotOfLane[lanes[slot]] = slot;
            }
        }

        return new DrivingNetwork(roads, runs, linkOfLane, slotOfLane);
    }

    /// <summary>The road graph read as the fine graph a contraction consumes: junctions as nodes, directed lanes as edges.</summary>
    readonly struct Fine(RoadGraph roads, Vector2[] junctionCentreM) : IFineGraph
    {
        public int NodeCount => roads.NodeCount;

        public int EdgeCount => roads.LaneCount;

        public Vector2 AnchorM(int node) => junctionCentreM[node];

        public int FromNode(int edge) => roads.LaneFromNode[edge];

        public int ToNode(int edge) => roads.LaneToNode[edge];

        public float LengthM(int edge) => roads.LaneLengthM[edge];

        public int Reverse(int edge) => roads.LaneReverse[edge];

        public ReadOnlySpan<int> EdgesOut(int node) => roads.LanesOut(node);
    }

    /// <summary>The three turn prices, in nominal car lengths, over the classification the road graph filled once.</summary>
    /// <remarks>
    /// A turn-around is priced out of reach rather than at its nominal twenty car lengths, and it is the
    /// one departure this graph makes: the line between two opposing lanes is a 1.5 m semicircle no car
    /// can hold, because turning round is a manoeuvre with a reverse in it and that entry is unbuilt. A
    /// route through one is a route the driver cannot drive, and a car handed one leaves its lane rather
    /// than declining it. It comes out the day the catalogue lands.
    /// </remarks>
    readonly struct Pricer(RoadGraph roads, SimConfig config) : IEdgeTurnPricer
    {
        public float PriceM(int fromEdge, int toEdge) => roads.TurnBetween(fromEdge, toEdge) switch
        {
            LaneTurn.NearSide => config.Driving.NominalCarLengthM * config.Driving.TurnPriceNearSideCarLengths,
            LaneTurn.FarSide => config.Driving.NominalCarLengthM * config.Driving.TurnPriceAcrossOncomingCarLengths,
            LaneTurn.TurnAround => float.PositiveInfinity,
            _ => 0f,
        };
    }
}
