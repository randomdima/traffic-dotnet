using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.CityGen;
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
/// <b>How a car park meets the road it hangs off</b> (GEN-4b, GEN-4h): a lot fronts the kerb of a road it
/// stands on, and every bay is approached off the lane it stands abeam of. The road is not cut for either
/// of them, so both are questions about metres of a lane rather than about a stretch of the network.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P4)]
public class BayApproachTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// The slack a bound that is reached rather than approached needs: a way staged exactly a run-in off
    /// its bay sits on it, and single-precision arithmetic then puts it a hair either side.
    /// </summary>
    const float AttainedBoundM = 0.01f;

    public static TheoryData<string> Maps => Towns.EveryTown();

    /// <summary>
    /// How much of a stretch a lane loses to being driven inside its road's own bends: the lane's offset
    /// times the turn a stretch that long holds at the tightest radius a street may be laid to. <b>A cut is
    /// placed in the road's own metres and the lane is measured in the lane's</b>, and this is the whole of
    /// what the two disagree by.
    /// </summary>
    static float InsideTheBendM(float stretchM) =>
        Config.LaneOffsetM * stretchM
        / Config.CarCorneringRadiusM(Config.CityGen.StreetDesignSpeedMps, Config.Terrain.PavedCoefficient);

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
        var lengthM = RoadFrontages.RoadLengthsM(plan.Ground);
        var fronts = RoadFrontages.Lay(plan.Ground, Config);

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
    /// <b>Every bay is approached over the one lane it stands abeam of</b> (GEN-4h): the way in leaves that
    /// lane a run-in short of the bay — or a run-in past it, backing in (GEN-4j) — and both the mouth and
    /// the bay lie on the lane's own metres. It is what makes a leg into a car park a place on a link and
    /// not a node cut into the road.
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
                CarBuild.Nominal(Config, Config.Car.DrivenFrontShare).CentreAheadOfAxleM,
                plan.ParkingLots.SpacePositionM[bay], plan.ParkingLots.SpaceHeadingRad[bay],
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

}
