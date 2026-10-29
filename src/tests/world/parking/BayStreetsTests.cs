using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.World.Parking;

/// <summary>
/// <b>Which lanes of its street a bay is worked off</b> (GEN-4h): the kerb lane beside it, and the lane across only where a
/// car may cross the street's middle line (CAR-6.2b) — staged on the suite's one town with car parks.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P4)]
public class BayStreetsTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly RoadGraph Roads = RoadGraph.Build(Towns.Built, Config);

    static readonly BayStreets Streets = BayStreets.Build(Towns.Built, Roads);

    /// <summary>
    /// <b>A bay off a street whose middle line is not crossed is worked off one lane, the one with no lane between it and
    /// the bay</b>: on its kerb side the outermost lane of its way, and on the other side of a one-way street its
    /// innermost — never a lane further out, nor one of the way across.
    /// </summary>
    [Fact]
    public void ABayOffAStreetNotCrossedIsWorkedOffTheLaneBesideIt()
    {
        var plan = Towns.Built;
        var parks = plan.CarParks;
        var asked = 0;
        for (var park = 0; park < parks.Count; park++)
        {
            if (plan.Roads.LineCrossedToPass(parks.Street[park])) continue;

            for (var bay = parks.BayOffsets[park]; bay < parks.BayOffsets[park + 1]; bay++)
            {
                var lanes = Streets.LanesOf(bay);
                Assert.True(lanes.Length == 1, $"bay {bay} off road {parks.Street[park]} is worked off {lanes.Length} lanes");

                var lane = lanes[0];
                var mouthM = plan.Roads.SegmentsOf(parks.Road[bay])[0].StartM;
                var at = Spline.SampleAt(Roads.ArcsOf(lane), Streets.AtLaneM(bay, lane));
                var onItsKerbSide = Vector2.Dot(mouthM - at.PositionM, at.Right) * Config.RoadSideSign > 0f;
                var between = Roads.LaneBeside(lane, inward: !onItsKerbSide);
                Assert.True(
                    between == RoadGraph.NoLane && (onItsKerbSide || Roads.LaneReverse[lane] == RoadGraph.NoLane),
                    $"bay {bay} at {mouthM} is worked off lane {lane} with lane {between} between them, kerb side {onItsKerbSide}");
                asked++;
            }
        }

        Assert.True(asked > 0, "the town lays no bay off a street whose middle line is not crossed");
    }

    /// <summary>
    /// <b>A bay off a street of one lane each way is worked off both</b>, the lane beside it and the one across, which is
    /// the one street a car may cross the middle of (CAR-6.2b).
    /// </summary>
    [Fact]
    public void ABayOffAStreetOfOneLaneEachWayIsWorkedOffBoth()
    {
        var plan = Towns.Built;
        var parks = plan.CarParks;
        for (var park = 0; park < parks.Count; park++)
        {
            if (!plan.Roads.LineCrossedToPass(parks.Street[park])) continue;

            for (var bay = parks.BayOffsets[park]; bay < parks.BayOffsets[park + 1]; bay++)
            {
                var lanes = Streets.LanesOf(bay);
                Assert.True(
                    lanes.Length == 2 && Roads.LaneReverse[lanes[0]] == lanes[1],
                    $"bay {bay} off road {parks.Street[park]} is worked off {lanes.Length} lanes, not its street's two ways");
            }
        }
    }
}
