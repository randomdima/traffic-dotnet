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
/// oncoming stream costs a gap that has to be waited for, and turning at a car park costs the whole of
/// parking and unparking — so a driver takes three sides of a block over either of them whenever the block
/// is there to take. A planner blind to this hands drivers routes made of the most expensive manoeuvres available and
/// the town then jams at the junctions. The prices are quoted in a <b>nominal</b> car length and never in
/// the length of whichever car is asking: the network is laid once for a whole town, and what a turn price
/// expresses is a preference between routes.
/// </para>
/// <para>
/// <b>A turn a body cannot make is priced, not refused</b> — a fact about a body is not the global tier's,
/// and a leg that cannot be driven is given up by its own clock (CAR-15a).
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
    /// A destination as the search takes it: <b>a place on a link</b>. Both directions of the stretch the
    /// place stands on are offered, because either may be the one that reaches it first and only the
    /// search can compare them.
    /// </summary>
    public int GoalsAt(Vector2 pointM, Span<RouteGoal> into)
    {
        var lane = _roads.NearestStreetLane(pointM, out var alongM);
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

    /// <summary>
    /// <b>A place on one named lane as the only goal</b> — for a leg that has to arrive on that lane and not on
    /// the other side of its street, as a police car standing at the mouth of a lane it is closing does.
    /// </summary>
    public int GoalOnLane(int lane, float alongLaneM, Span<RouteGoal> into)
    {
        if (into.Length == 0 || _linkOfLane[lane] == TravelGraph.NoLink) return 0;

        into[0] = new RouteGoal(_linkOfLane[lane], PlaceOfM(lane, alongLaneM));
        return 1;
    }

    /// <param name="turnsAtALot">
    /// One flag per lane: <b>whether a leg may come back down the other side of this stretch</b> by parking
    /// in a bay near its end and leaving the other way (GEN-4l). It is handed in as data rather than read off
    /// the car parks, which stand beside the road and are above it (<see cref="Parking.BayStreets.WhereALegMayTurn"/>).
    /// </param>
    public static DrivingNetwork Build(
        RoadGraph roads, ReadOnlySpan<bool> turnsAtALot, CityPlan plan, SimConfig config)
    {
        // <b>The graph's own places and not a second reading of them</b> (SIM-7): where the lanes meet is the
        // network that laid them to answer, and the router and the claims reading two answers is how they come
        // to disagree about which lane ends are one piece of the world.
        var runs = RunNetwork.Contract(
            new Fine(roads), new Pricer(roads, turnsAtALot.ToArray(), config), roads.Places);

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

    /// <summary>
    /// The road graph read as the fine graph a contraction consumes: <b>how long each lane is and which is
    /// the same stretch driven back</b>. Where the lanes meet is the graph's own answer and is handed over
    /// beside this.
    /// </summary>
    readonly struct Fine(RoadGraph roads) : IFineGraph
    {
        public int LaneCount => roads.LaneCount;

        public float LengthM(int lane) => roads.LaneLengthM[lane];

        public int Reverse(int lane) => roads.LaneReverse[lane];
    }

    /// <summary>
    /// The turn prices, in nominal car lengths, over the classification the road graph filled once — and
    /// <b>the one movement that is not a turn at all</b>: coming back down the other side of a car park's
    /// frontage, which the driver makes by parking and unparking (GEN-4l).
    /// </summary>
    /// <remarks>
    /// <b>A pair of lanes the road did not join is refused and never free.</b> No box admits a movement that
    /// reverses the direction of travel (TER-5f), so the two lanes of one stretch have no turn between them
    /// anywhere: priced at nothing, every junction in the town would offer a free turn-around, which is the
    /// one way this graph could hand a driver a route with no ground under it. A stretch with a bay near its
    /// end is the exception, and it is priced rather than free because what happens there is a whole park and
    /// a whole unpark with the traffic given way to twice.
    /// <para>
    /// <b>A bay is never a lane a route runs down</b> (GEN-4h): it is joined to nothing and got into by the
    /// car's own manoeuvre (GEN-4f), and a route is aimed at the street lane its mouth stands abeam of.
    /// Offered as a lane, every bay is a dead end the search may turn a leg at
    /// (<see cref="Parking.BayStreets.WhereALegMayTurn"/>) and nothing turns the car there.
    /// </para>
    /// </remarks>
    readonly struct Pricer(RoadGraph roads, bool[] turnsAtALot, SimConfig config) : IEdgeTurnPricer
    {
        public float PriceM(int fromEdge, int toEdge) => roads.IsABayArm(fromEdge) || roads.IsABayArm(toEdge)
            ? float.PositiveInfinity
            : roads.TurnBetween(fromEdge, toEdge) switch
            {
                LaneTurn.NearSide => config.Driving.NominalCarLengthM * config.Driving.TurnPriceNearSideCarLengths,
                LaneTurn.FarSide => config.Driving.NominalCarLengthM * config.Driving.TurnPriceAcrossOncomingCarLengths,
                LaneTurn.Straight => 0f,
                _ => TurnsAtTheLotM(fromEdge, toEdge),
            };

        /// <summary>
        /// What the pair costs where the road has no turn between them: the park and the unpark where they
        /// are the two sides of a stretch a bay is worked off both ways, and out of reach everywhere else.
        /// </summary>
        float TurnsAtTheLotM(int fromEdge, int toEdge) =>
            roads.LaneReverse[fromEdge] == toEdge && turnsAtALot[fromEdge]
                ? config.Driving.NominalCarLengthM * config.Driving.TurnPriceComingBackCarLengths
                : float.PositiveInfinity;
    }
}
