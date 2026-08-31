using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Routing;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// The nodes cut into the roads for the car parks hanging off them (GEN-4h), asked the two questions the
/// rule turns on: <b>a place stands where it does no harm</b>, and <b>a bay's whole approach is on the
/// stretch its own section owns</b>.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class ParkingSectionTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// The slack a bound that is reached rather than approached needs: a way staged exactly a run-in off
    /// its bay sits on it, and single-precision arithmetic then puts it a hair either side.
    /// </summary>
    const float AttainedBoundM = 0.01f;

    public static TheoryData<string> Maps => Towns.EveryTown();

    /// <summary>
    /// <b>A lot hangs off a kerb</b> (GEN-4b), which is the claim the frontage is read against: it stands
    /// on a road, over metres that road has, and its near edge reaches the carriageway rather than
    /// standing back behind a walk. It is what tells the drawing there is no kerb to draw a line for over
    /// that stretch, so a town where it stopped being true would paint a line across every car park mouth.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryLotFrontsTheKerbOfARoadItStandsOn(string map)
    {
        var plan = Towns.Of(map);
        var lengthM = RoadFrontages.RoadLengthsM(plan);
        var fronts = RoadFrontages.Lay(plan, Config);

        Assert.Equal(plan.ParkingLots.Count, fronts.All.Length);
        foreach (var front in fronts.All)
        {
            Assert.InRange(front.Road, 0, plan.Roads.Count - 1);
            Assert.True(front.ToM > front.FromM, $"{map}: lot {front.Lot} fronts no metres of road {front.Road}");
            Assert.True(front.ToM > 0f && front.FromM < lengthM[front.Road],
                $"{map}: lot {front.Lot} fronts road {front.Road} past its own ends");
            Assert.True(front.Side is -1f or 1f);
            Assert.True(front.FrontsTheKerb, $"{map}: lot {front.Lot} stands back off the kerb of road {front.Road}");
        }
    }

    /// <summary>
    /// <b>A place gives way to what the road already carries</b> (GEN-4h). It leaves a stretch on either
    /// side long enough to drive, it stands outside every junction disc, and it is off the paint — a lane
    /// end inside a zebra hands the crossing to the lane after the one a driver is braking on, which is a
    /// car over the paint at road speed.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void APlaceStandsClearOfTheJunctionsAndThePaint(string map)
    {
        var plan = Towns.Of(map);
        var roads = RoadGraph.Build(plan, Config);
        var shortestM = Config.ParkingSectionShortestStretchM;

        for (var node = roads.JunctionCount; node < roads.NodeCount; node++)
        {
            // A place whose cut was refused carries no lane and stands for nothing.
            if (roads.LanesIn(node).Length == 0) continue;

            var atM = roads.NodeCentreM[node];
            for (var junction = 0; junction < plan.Junctions.Count; junction++)
            {
                var awayM = (plan.Junctions.CentreM[junction] - atM).Length();
                Assert.True(
                    awayM >= plan.Junctions.RadiusM[junction] + shortestM,
                    $"{map}: place {node} stands {awayM:F1} m from junction {junction}");
            }

            for (var crossing = 0; crossing < plan.Crosswalks.Count; crossing++)
            {
                var offM = atM - plan.Crosswalks.CentreM[crossing];
                var across = Vector2.Normalize(plan.Crosswalks.Axis[crossing]);
                var deepM = MathF.Abs(Spline.Cross(across, offM));
                var alongM = MathF.Abs(Vector2.Dot(across, offM));
                Assert.True(
                    deepM >= plan.Crosswalks.DepthM[crossing] * 0.5f
                    || alongM >= plan.Crosswalks.SpanM[crossing] * 0.5f,
                    $"{map}: place {node} stands on crossing {crossing}");
            }

            foreach (var lane in roads.LanesIn(node))
            {
                Assert.True(roads.LaneLengthM[lane] >= shortestM, $"{map}: place {node} leaves lane {lane} too short to drive");
            }
        }
    }

    /// <summary>
    /// <b>Every bay is approached over one stretch of road, and its section's node is the end of that
    /// stretch</b> (GEN-4h). The way in leaves its lane a run-in short of the bay and the bay itself stands
    /// abeam of the same lane, so the cut at the section's mouth falls before the run-in and never between
    /// the run-in and the bay — which is the whole reason the cuts are set back from the frontage.
    /// </summary>
    /// <remarks>
    /// <b>The projection is what catches it.</b> A place on a chain is clamped to that chain's own ends, so
    /// a bay past the end of the lane its way in leaves answers at the end rather than being refused; the
    /// bay is off that lane and the last metres of the approach are on the stretch after it.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryBayStandsAbeamTheLaneItsWayInLeaves(string map)
    {
        var plan = Towns.Of(map);
        var roads = RoadGraph.Build(plan, Config);
        var ways = BayWays.Build(plan, roads, Config);

        for (var way = ways.FirstWay; way < ways.TotalWayCount; way++)
        {
            if (!ways.IsEntry(way)) continue;

            var bay = ways.BayOfWay(way);
            var lane = ways.LaneOf(way);
            var lengthM = roads.LaneLengthM[lane];

            // Abeam the axle the way is drawn to and not the middle of the space, because those are a
            // wheelbase's half apart and on opposite sides of it in the two standings (GEN-4j).
            var axleM = BayTemplate.RearAxleOfBayM(
                CarBuild.Nominal(Config, Config.Car.DrivenFrontShare), plan.ParkingLots.SpacePositionM[bay], plan.ParkingLots.SpaceHeadingRad[bay],
                ways.IsNoseIn(way));

            var abeamM = Spline.ProjectM(roads.ArcsOf(lane), axleM, lengthM * 0.5f, lengthM);

            // Nose-first the way leaves the lane short of the bay; backing in, the car has driven past it
            // first and the way leaves beyond it. Either way it is within the run-in.
            var leavesM = ways.AtLaneM(way);
            Assert.InRange(
                ways.IsNoseIn(way) ? abeamM - leavesM : leavesM - abeamM, 0f,
                Config.ParkingStagedInM + AttainedBoundM);

            Assert.InRange(leavesM, 0f, lengthM);
            Assert.True(abeamM < lengthM, $"{map}: bay {bay} stands past the end of lane {lane}");
        }
    }

    /// <summary>
    /// <b>A leg into a bay ends at a node</b> (GEN-4h): the lane the bay's way in leaves is the last piece
    /// of its run, so the goal the search is handed is that run's own end and not a metre inside it.
    /// </summary>
    /// <remarks>
    /// It is what the node was cut for. A route is a chain of nodes, so a destination anywhere else is a
    /// route to somewhere plus a stretch of road the driver worked out for itself — and the price, the
    /// reroute and the goal then mean three slightly different places.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void ALegIntoABayIsAimedAtTheEndOfItsRun(string map)
    {
        var plan = Towns.Of(map);
        var roads = RoadGraph.Build(plan, Config);
        var driving = DrivingNetwork.Build(roads, BayWays.WhereALegMayTurn(roads, BayWays.Build(plan, roads, Config)), plan, Config);
        var ways = BayWays.Build(plan, roads, Config);

        for (var way = ways.FirstWay; way < ways.TotalWayCount; way++)
        {
            if (!ways.IsEntry(way)) continue;

            var lane = ways.LaneOf(way);
            var link = driving.LinkOfLane(lane);
            Assert.NotEqual(TravelGraph.NoLink, link);

            // The one exception the rule allows: a section with no room on its road for a node of its own
            // keeps the node the road already ends at, and where that is a bend the search contracts
            // through, the goal is a metre inside a run. It is the road being out of room and never a cut
            // laid in the wrong place, which is why the fallback is named rather than tolerated.
            var road = roads.LaneRoad[lane];
            if (roads.LaneToNode[lane] == plan.Roads.FromJunction[road]
                || roads.LaneToNode[lane] == plan.Roads.ToJunction[road])
            {
                continue;
            }

            Assert.True(
                MathF.Abs(driving.Runs.LengthM(link) - driving.PlaceOfM(lane, roads.LaneLengthM[lane])) < 0.01f,
                $"{map}: bay {ways.BayOfWay(way)} is reached over lane {lane}, which ends "
                + $"{driving.Runs.LengthM(link) - driving.PlaceOfM(lane, roads.LaneLengthM[lane]):F0} m "
                + $"inside its own run at node {roads.LaneToNode[lane]}");
        }
    }
}
