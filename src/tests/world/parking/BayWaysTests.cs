using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.World.Parking;

/// <summary>
/// The ways at a bay, read off the car parks of the suite's own city (GEN-4f, GEN-4h, GEN-4j): what each bay
/// lays, where each way meets its lane and its pose, and which of them are driven in reverse.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P5)]
[Collection(nameof(TownGeometryCollection))]
public class BayWaysTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>How near two readings of one point on the town's own lines have to come.</summary>
    const float ExactM = 1e-3f;

    /// <summary>How far apart two readings of one heading may come: an angle read back off a dot product.</summary>
    const float SquareDeg = 0.1f;

    static (RoadGraph Roads, BayWays Bays) Laid()
    {
        var plan = Towns.Built;
        var roads = RoadGraph.Build(plan, Config);
        return (roads, BayWays.Build(plan, roads, Config));
    }

    /// <summary>
    /// <b>Every bay can be parked in and left</b> (GEN-4): it lays a way in and a way out for at least one of
    /// the two standings (GEN-4j).
    /// </summary>
    [Fact]
    public void EveryBayCanBeParkedInAndLeft()
    {
        var (_, bays) = Laid();

        Assert.True(bays.BayCount > 0, "the built city cut no car park");
        for (var bay = 0; bay < bays.BayCount; bay++)
        {
            Assert.True(
                bays.CanStand(bay, noseIn: true) || bays.CanStand(bay, noseIn: false),
                $"bay {bay} lays {bays.WayCountOf(bay)} ways and no standing has both a way in and a way out");
        }
    }

    /// <summary>
    /// <b>No way in leaves a car standing with no way out</b> (GEN-4j): whichever standing a bay is driven
    /// into, that standing is one the bay also lays a way out of.
    /// </summary>
    [Fact]
    public void EveryWayInHasAWayOutOfTheSameStanding()
    {
        var (_, bays) = Laid();

        for (var way = bays.FirstWay; way < bays.TotalWayCount; way++)
        {
            if (!bays.IsEntry(way)) continue;

            Assert.True(
                bays.CanStand(bays.BayOfWay(way), bays.IsNoseIn(way)),
                $"way {way} parks a car {(bays.IsNoseIn(way) ? "nose in" : "backed in")} in bay {bays.BayOfWay(way)}, " +
                "which lays no way out that way round");
        }
    }

    /// <summary>
    /// <b>A way in sets off from its lane where it says it does</b> — the metre a route down that lane is cut
    /// at, so the car is brought to rest on the mouth of the way it drives next.
    /// </summary>
    [Fact]
    public void AWayInSetsOffFromItsLaneAtItsOwnMetre()
    {
        var (roads, bays) = Laid();

        for (var way = bays.FirstWay; way < bays.TotalWayCount; way++)
        {
            if (!bays.IsEntry(way)) continue;

            var laneM = Spline.SampleAt(roads.ArcsOf(bays.LaneOf(way)), bays.AtLaneM(way)).PositionM;
            var wayM = Spline.SampleAt(bays.ArcsOf(way), 0f).PositionM;

            Assert.True(
                (wayM - laneM).Length() <= ExactM,
                $"way {way} of bay {bays.BayOfWay(way)} leaves its lane {(wayM - laneM).Length():0.000} m off the metre it names");
        }
    }

    /// <summary>
    /// <b>A way out lands on its street where it says it does</b> (GEN-4f): on the street's own line through
    /// the car park, at the metre the car is seated on that line by when it pulls away — inside the box as
    /// often as not, where no lane runs.
    /// </summary>
    [Fact]
    public void AWayOutLandsOnItsStreetAtItsOwnMetre()
    {
        var (roads, bays) = Laid();

        for (var way = bays.FirstWay; way < bays.TotalWayCount; way++)
        {
            if (bays.IsEntry(way)) continue;

            var streetM = Spline.SampleAt(TheStreet(roads, bays.StreetOf(way)), bays.OnTheStreetM(way)).PositionM;
            var wayM = Spline.SampleAt(bays.ArcsOf(way), bays.DrivenLengthM(way)).PositionM;

            Assert.True(
                (wayM - streetM).Length() <= ExactM,
                $"way {way} of bay {bays.BayOfWay(way)} lands {(wayM - streetM).Length():0.000} m off the metre of its street it names");
        }
    }

    /// <summary>
    /// <b>A car out of a bay stands facing the way its street runs</b> (GEN-4f): one backing out has swung its
    /// tail up the street and pulls away forwards, and one driving out has turned down it.
    /// </summary>
    [Fact]
    public void ACarOutOfABayStandsFacingTheWayItsStreetRuns()
    {
        var (roads, bays) = Laid();

        for (var way = bays.FirstWay; way < bays.TotalWayCount; way++)
        {
            if (bays.IsEntry(way)) continue;

            var street = Spline.SampleAt(TheStreet(roads, bays.StreetOf(way)), bays.OnTheStreetM(way));
            var travel = Spline.SampleAt(bays.ArcsOf(way), bays.DrivenLengthM(way)).Direction;
            var facing = bays.IsDrivenInReverse(way) ? -travel : travel;
            var offDeg = MathF.Acos(Math.Clamp(Vector2.Dot(facing, street.Direction), -1f, 1f)) * 180f / MathF.PI;

            Assert.True(
                offDeg <= SquareDeg,
                $"way {way} of bay {bays.BayOfWay(way)} leaves its car {offDeg:0.0}° off the way its street runs");
        }
    }

    /// <summary>
    /// <b>A way at a bay turns once, on a circle a car holds</b> (GEN-4f): no tighter than the car park's own
    /// turn (<see cref="SimConfig.CarParkTurnRadiusM"/>) and no wider than the car's own
    /// (<see cref="SimConfig.CarParkingTemplateRadiusM"/>), with straight either side of it.
    /// </summary>
    [Fact]
    public void AWayAtABayTurnsOnceOnACircleACarHolds()
    {
        var (_, bays) = Laid();

        for (var way = bays.FirstWay; way < bays.TotalWayCount; way++)
        {
            var turns = 0;
            foreach (var arc in bays.ArcsOf(way))
            {
                if (arc.Curvature == 0f) continue;

                turns++;
                var radiusM = 1f / MathF.Abs(arc.Curvature);
                Assert.InRange(radiusM, Config.CarParkTurnRadiusM - ExactM, Config.CarParkingTemplateRadiusM + ExactM);
            }

            Assert.True(turns == 1, $"way {way} of bay {bays.BayOfWay(way)} turns {turns} times");
        }
    }

    /// <summary>
    /// <b>A car backing out of a bay goes no further up its street than its run-out past the turn</b> (GEN-4f,
    /// <see cref="SimConfig.ParkingRunOutM"/>), and pulls away from there — not back across the car park's box
    /// to where it would have turned in.
    /// </summary>
    [Fact]
    public void ACarBackingOutGoesNoFurtherUpItsStreetThanItsRunOut()
    {
        var (_, bays) = Laid();

        for (var way = bays.FirstWay; way < bays.TotalWayCount; way++)
        {
            if (!bays.IsDrivenInReverse(way)) continue;

            var last = bays.ArcsOf(way)[^1];
            var backedM = last.Curvature == 0f ? last.LengthM : 0f;
            Assert.True(
                backedM <= Config.ParkingRunOutM + ExactM,
                $"way {way} of bay {bays.BayOfWay(way)} backs {backedM:0.00} m up its street past its turn " +
                $"against a run-out of {Config.ParkingRunOutM:0.00} m");
        }
    }

    /// <summary>The street's own line on one side of a car park, as a way out is measured along it.</summary>
    static ReadOnlySpan<ArcSeg> TheStreet(RoadGraph roads, int lane)
    {
        var onward = BayWays.ThroughTheBox(roads, lane);
        int[] lanes = onward < 0 ? [lane] : [lane, onward];
        var arcs = new ArcSeg[LineAssembler.ArcsFor(roads)];
        var laid = LineAssembler.Assemble(roads, lanes, arcs, new float[lanes.Length], new float[lanes.Length]);
        return arcs.AsSpan(0, laid.ArcCount);
    }

    /// <summary>
    /// <b>And its bay at the pose</b> (GEN-4i): a way in is driven as far as the axle of a car standing square
    /// in the space, and a way out sets off from there.
    /// </summary>
    [Fact]
    public void AWayIsDrivenToThePose()
    {
        var (_, bays) = Laid();

        for (var way = bays.FirstWay; way < bays.TotalWayCount; way++)
        {
            var arcs = bays.ArcsOf(way);
            var poseM = bays.IsEntry(way)
                ? Spline.SampleAt(arcs, bays.DrivenLengthM(way)).PositionM
                : Spline.SampleAt(arcs, 0f).PositionM;

            Assert.True(
                (poseM - bays.AtTheBayM(way)).Length() <= ExactM,
                $"way {way} of bay {bays.BayOfWay(way)} is driven to {(poseM - bays.AtTheBayM(way)).Length():0.000} m off its pose");
        }
    }
}
