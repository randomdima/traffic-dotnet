using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Gen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// <b>A car park is a junction cut into a road that already stands</b> (GEN-52, GEN-53), and the one thing
/// that makes a cut a cut rather than a junction laid late: <b>the road does not move</b>.
/// </summary>
/// <remarks>
/// <b>Asked of a town laid twice at one seed</b>, once with car parks and once without. A cut is the last
/// thing done to a layout and every road's shape is a function of its own link (GEN-11), so the two towns
/// are the same town apart from what the cuts put in it — which is what lets the question be asked as "is
/// every metre of the town without them still driven in the town with them" rather than as an arithmetic
/// about arcs.
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P3)]
public class CarParkTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// How many buildings the towns here plan, which is what their car parks are counted off
    /// (<see cref="SimConfig.CarParksFor"/>). <b>Enough that both shapes of a car park are laid</b> — the one
    /// with a rank on each side of the street and the one with a bare side — on every seed.
    /// </summary>
    const int Buildings = 48;

    /// <summary>Tighter than any line a town draws, so what it tells apart is a straight from a turn.</summary>
    const float StraightCurvature = 1e-6f;

    public static TheoryData<ulong> Seeds() => Towns.Seeds();

    static CityPlan Cut(ulong seed) => Laid.GetOrAdd(seed, at => Towns.LayFresh(Towns.Brief(at, buildings: Buildings)));

    static readonly ConcurrentDictionary<ulong, CityPlan> Laid = new();

    /// <summary>
    /// <b>A lane a cut parted is the lane it was</b> (GEN-52), which is the whole of what a cut promises:
    /// walked end to end, the piece before the junction, the movement across it and the piece after it are
    /// the one line that lane was before anything was cut into it — the same length, and the same point at
    /// every metre of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Asked of the same town laid twice</b>, once with car parks and once without. A cut is the last
    /// thing done to a layout and every road's shape is a function of its own link (GEN-11), so the pair
    /// differ by the cuts and by nothing else.
    /// </para>
    /// <para>
    /// <b>Held to the rounding two computations of one distance disagree by</b>
    /// (<see cref="LineTolerance.RoundingM"/>), because nothing here is an approximation of anything: a
    /// carriageway that moved at all would be one stepping sideways at the place a reader is looking. It is
    /// walked at a stride rather than projected onto, a projection over a chain of arcs being the coarser
    /// instrument of the two by more than the figure being asked about.
    /// </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryLaneACutPartedIsTheLaneItWas(ulong seed)
    {
        var was = Towns.LaidFrom(seed);
        var wasLanes = was.Paving(Config).Lanes;
        var now = Cut(seed);
        var lanes = now.Paving(Config).Lanes;

        var parted = 0;
        for (var road = 0; road < was.Roads.Count; road++)
        {
            if (!now.Roads.WasCut(road)) continue;

            var pieces = Pieces(was, now, road);

            // <b>Every way the road is driven and no way it is not</b> (TER-4d): a street the scatter took
            // carries one lane, and the piece it was parted into carries the same one (GEN-18).
            foreach (var forward in WaysOf(was.Roads.Flow[road]))
            {
                if (!forward) pieces.Reverse();

                var whole = new List<ArcSeg>();
                for (var piece = 0; piece < pieces.Count; piece++)
                {
                    var lane = LaneOf(lanes, pieces[piece], forward);
                    whole.AddRange(lanes.ArcsOf(lane));
                    if (piece + 1 == pieces.Count) break;

                    var onto = LaneOf(lanes, pieces[piece + 1], forward);
                    whole.AddRange(lanes.ArcsOfConnector(Movement(lanes, lane, onto)));
                }

                SameLine(wasLanes.ArcsOf(LaneOf(wasLanes, road, forward)), whole, road, forward, pieces.Count);
                parted++;
            }
        }

        Assert.True(parted > 0, $"seed {seed} parted no lane to ask about");
    }

    /// <summary>
    /// <b>And a cut moves nothing else</b> (GEN-52): every road the town had that no car park was cut into
    /// carries the arcs it carried, piece for piece.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void ACutMovesNoRoadItWasNotCutInto(ulong seed)
    {
        var was = Towns.LaidFrom(seed);
        var now = Cut(seed);

        for (var road = 0; road < was.Roads.Count; road++)
        {
            if (now.Roads.WasCut(road)) continue;

            Assert.Equal(was.Roads.SegmentsOf(road).ToArray(), now.Roads.SegmentsOf(road).ToArray());
        }
    }

    /// <summary>
    /// Whether two chains are the same line: the same length, and the same point at every metre of it.
    /// <b>Within a rounding for each chain the walk is laid out of</b> — two computations of one distance
    /// disagree by that much, and a road cut once is walked as two pieces and the movement between them,
    /// cut twice as three and two.
    /// </summary>
    static void SameLine(ReadOnlySpan<ArcSeg> was, List<ArcSeg> now, int road, bool forward, int pieces)
    {
        var wasM = Spline.TotalLengthM(was);
        var nowM = Spline.TotalLengthM(CollectionsMarshal.AsSpan(now));
        var way = forward ? "forward" : "backward";
        var roundingM = LineTolerance.RoundingM * ((2 * pieces) - 1);
        Assert.True(
            MathF.Abs(wasM - nowM) <= roundingM,
            $"the {way} lane of road {road} was {wasM:F3} m and is {nowM:F3} m once it is cut");

        var steps = Math.Max(1, (int)MathF.Ceiling(wasM));
        for (var step = 0; step <= steps; step++)
        {
            var alongM = wasM * step / steps;
            var offM = Vector2.Distance(
                Spline.SampleAt(was, alongM).PositionM,
                Spline.SampleAt(CollectionsMarshal.AsSpan(now), alongM).PositionM);

            Assert.True(
                offM <= roundingM,
                $"the {way} lane of road {road} moved {offM * 1000f:F1} mm at {alongM:F1} m of {wasM:F1} m");
        }
    }

    /// <summary>
    /// The pieces one parted road came out as, in the order it is driven. <b>A road may be cut more than
    /// once</b> — a car park is cut into whatever the layout holds, and what it holds after the first one is
    /// two pieces of a street like any other — so this walks from the road's own junction to its own far one
    /// rather than assuming a piece either side of one node.
    /// </summary>
    static List<int> Pieces(CityPlan was, CityPlan now, int road)
    {
        var pieces = new List<int> { road };
        var lastM = was.Roads.ToJunction[road];
        while (now.Roads.ToJunction[pieces[^1]] != lastM)
        {
            var node = now.Roads.ToJunction[pieces[^1]];
            var onward = -1;
            for (var piece = 0; piece < now.Roads.Count; piece++)
            {
                if (now.Roads.FromJunction[piece] != node || now.Roads.IsABay(piece)) continue;

                Assert.Equal(-1, onward);
                onward = piece;
            }

            Assert.True(onward >= 0, $"road {road} was cut and the piece past junction {node} is not in the town");
            pieces.Add(onward);
        }

        return pieces;
    }

    /// <summary>Which ways a road of this flow is driven, in the order it was laid (TER-4d).</summary>
    static bool[] WaysOf(RoadFlow flow) => flow switch
    {
        RoadFlow.BothWays => [true, false],
        RoadFlow.WithTheRoad => [true],
        _ => [false],
    };

    /// <summary>One of a road's two lanes, by the way it is driven.</summary>
    static int LaneOf(LaneLines lanes, int road, bool forward)
    {
        for (var lane = 0; lane < lanes.LaneCount; lane++)
        {
            if (lanes.LaneRoad[lane] == road && lanes.LaneForward[lane] == forward) return lane;
        }

        Assert.Fail($"road {road} carries no lane driven {(forward ? "forward" : "backward")}");
        return -1;
    }

    /// <summary>The movement joining one lane to the next, which a junction cut into a road always offers.</summary>
    static int Movement(LaneLines lanes, int from, int onto)
    {
        for (var at = lanes.ConnectorAt[from]; at < lanes.ConnectorAt[from + 1]; at++)
        {
            if (lanes.ConnectorToLane[at] == onto) return at;
        }

        Assert.Fail($"lane {from} is not driven on to lane {onto} across the junction between them");
        return -1;
    }

    /// <summary>
    /// <b>A car park carries an arm for every bay it has</b> (GEN-53), on top of the road it was cut into
    /// either side of it — so a junction of four bays one side and five the other has eleven arms, and one
    /// with a side of none has the road's two and the bays of the side that does.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryCarParkCarriesAnArmForEveryBay(ulong seed)
    {
        var plan = Cut(seed);
        Assert.True(plan.CarParks.Count > 0, $"seed {seed} cut no car park to ask about");

        var arms = ArmsOf(plan);
        var oneSided = 0;
        for (var carPark = 0; carPark < plan.CarParks.Count; carPark++)
        {
            var bays = plan.CarParks.RoadsOf(carPark).Length;
            Assert.Equal(2 + bays, arms[plan.CarParks.Junction[carPark]]);
            if (plan.CarParks.BaysOn(carPark, right: true) == 0
                || plan.CarParks.BaysOn(carPark, right: false) == 0)
            {
                oneSided++;
            }
        }

        Assert.True(oneSided < plan.CarParks.Count, $"seed {seed} gave every car park bays on one side only");
    }

    /// <summary>
    /// <b>A side of a car park carries a handful of bays or none of them</b> (GEN-4b, GEN-53), and
    /// <b>not none on both sides</b> — a car park with no bay either side is a junction cut into a road for
    /// nothing.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EverySideOfACarParkCarriesAHandfulOfBaysOrNone(ulong seed)
    {
        var plan = Cut(seed);
        for (var carPark = 0; carPark < plan.CarParks.Count; carPark++)
        {
            var right = plan.CarParks.BaysOn(carPark, right: true);
            var left = plan.CarParks.BaysOn(carPark, right: false);

            Assert.True(right + left > 0, $"car park {carPark} was cut for no bay at all");
            foreach (var bays in (int[])[right, left])
            {
                Assert.True(
                    bays == 0
                    || (bays >= Config.CityGen.BaysPerLotFewest && bays <= Config.CityGen.BaysPerLotMost),
                    $"car park {carPark} carries {bays} bays on one side, which is neither none nor a handful");
            }
        }
    }

    /// <summary>
    /// <b>A side's bays stand in a rank a bay's length past where its turn ends, a lane apart and centred on
    /// the node</b> (GEN-53, <see cref="SimConfig.CarParkArmStandM"/>). <b>Every one of them runs square to
    /// the street</b>, so a rank is parallel ways off one line and not a fan out of the node — which is what a
    /// car backing straight out of a bay needs of it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryBayStandsInItsSidesRankAReachOffTheRoad(ulong seed)
    {
        var plan = Cut(seed);
        var ground = plan.Ground;

        for (var carPark = 0; carPark < plan.CarParks.Count; carPark++)
        {
            var node = plan.CarParks.Junction[carPark];
            var nodeM = plan.Junctions.CentreM[node];
            // <b>The street's bearing through the cut is the line between where it stands off either side of
            // it</b> — and not the way out to one of them, because a street the scatter took is drawn half a
            // lane off the line its junctions stand on (TER-4d, <c>RoadStage.OntoTheDrivenHalf</c>) and that
            // step aside is the same step at both ends, so the chord between them is free of it.
            var afterM = ConnectionPoints.ArmOf(ground, Config, AfterPieceAt(plan, carPark, node), atFrom: true)
                .StandM;
            var beforeM = ConnectionPoints
                .ArmOf(ground, Config, BeforePieceAt(plan, carPark, node), atFrom: false).StandM;
            var along = Vector2.Normalize(afterM - beforeM);
            var across = Heading.RightOf(along);
            var mostBays = plan.CarParks.MostBaysOnASide(carPark);
            var flow = plan.Roads.Flow[AfterPieceAt(plan, carPark, node)];

            foreach (var right in (bool[])[true, false])
            {
                // <b>A side's rank stands off the lane that side's bays are turned off</b> (GEN-53), which on
                // a street driven one way is the same lane for both sides and so a different reach for each.
                var reachM = Config.CarParkArmStandM(mostBays, CarParks.LaneTowardM(Config, flow, right));
                var alongM = new List<float>();
                for (var bay = plan.CarParks.BayOffsets[carPark];
                     bay < plan.CarParks.BayOffsets[carPark + 1];
                     bay++)
                {
                    if (plan.CarParks.Right[bay] != right) continue;

                    var road = plan.CarParks.Road[bay];
                    Assert.Equal(node, ground.Roads.FromJunction[road]);

                    var way = ground.Roads.SegmentsOf(road);
                    Assert.Equal(right ? 1f : -1f, Vector2.Dot(way[0].StartUnit, across), 3);

                    var offM = way[^1].EndM - nodeM;
                    var standsM = Vector2.Dot(offM, across) * (right ? 1f : -1f);
                    Assert.True(
                        MathF.Abs(standsM - reachM) <= LineTolerance.RoundingM,
                        $"a bay of car park {carPark} stands {standsM:F3} m off a rank laid at {reachM:F3} m");
                    alongM.Add(Vector2.Dot(offM, along));
                }

                ARank(alongM, carPark, right);
            }
        }
    }

    /// <summary>A rank: a lane apart along the road, in order, and centred on the node the car park was cut at.</summary>
    static void ARank(List<float> alongM, int carPark, bool right)
    {
        if (alongM.Count == 0) return;

        var side = right ? "right" : "left";
        alongM.Sort();
        for (var bay = 1; bay < alongM.Count; bay++)
        {
            Assert.Equal(Config.LaneWidthM, alongM[bay] - alongM[bay - 1], 3);
        }

        Assert.Equal(
            0f, (alongM[0] + alongM[^1]) * 0.5f, 3);
        Assert.True(
            alongM.Count <= Config.CityGen.BaysPerLotMost,
            $"the {side} rank of car park {carPark} holds {alongM.Count} bays");
    }

    /// <summary>
    /// <b>A bay's way is one lane wide and driven both ways over that one line</b> (GEN-53, GEN-4f): a car
    /// drives in over it and comes back out over it, so its two connection points stand on the arm's own line
    /// rather than half a lane either side of it — which is what connects every bay to the way in and to the
    /// way out of the junction.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryBaysWayIsOneLaneDrivenBothWaysOverOneLine(ulong seed)
    {
        var plan = Cut(seed);
        var ground = plan.Ground;
        var lanes = plan.Paving(Config).Lanes;

        Span<ConnectionPoint> points = stackalloc ConnectionPoint[ConnectionPoints.MostPerArm];
        foreach (var road in plan.CarParks.Road)
        {
            Assert.True(ground.Roads.DrivenOverOneLine(road));
            Assert.Equal(RoadFlow.BothWays, ground.Roads.Flow[road]);
            Assert.Equal(Config.LaneWidthM, ground.Roads.WidthM[road], 3);

            // Both points of the arm at the junction: one a way in and one a way out, on the same ground.
            Assert.Equal(2, ConnectionPoints.At(ground, Config, road, atFrom: true, points));
            Assert.NotEqual(points[0].End, points[1].End);
            Assert.Equal(0f, Vector2.Distance(points[0].AtM, points[1].AtM), 3);

            var one = LaneOf(lanes, road, forward: true);
            var other = LaneOf(lanes, road, forward: false);
            Assert.True(lanes.LaneOverOneLine[one] && lanes.LaneOverOneLine[other]);
            Assert.Equal(Config.LaneWidthM, lanes.LaneWidthM[one], 3);
            Assert.Equal(0f, Vector2.Distance(lanes.ArcsOf(one).ToArray()[0].StartM, lanes.ArcsOf(other)[^1].EndM), 3);
        }
    }

    /// <summary>
    /// <b>No movement joins one bay to another</b> (GEN-53). A car park's bays all hang off one node, so the
    /// junction's own arithmetic would otherwise offer a turn out of every bay into every other — over
    /// ground that is the car park's to cross and not a road with a right of way on it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NoMovementOfACarParkJoinsOneBayToAnother(ulong seed)
    {
        var plan = Cut(seed);
        var roads = plan.Ground.Roads;
        var lanes = plan.Paving(Config).Lanes;

        for (var connector = 0; connector < lanes.ConnectorCount; connector++)
        {
            var from = lanes.LaneRoad[lanes.ConnectorFromLane[connector]];
            var onto = lanes.LaneRoad[lanes.ConnectorToLane[connector]];
            Assert.False(
                roads.IsABay(from) && roads.IsABay(onto),
                $"seed {seed} joins bay {from} to bay {onto}");
        }
    }

    /// <summary>
    /// <b>And every bay is reached from every way its street runs and leaves onto each</b> (GEN-53). A car
    /// park's junction is the two pieces of one street and its bays, and the bays are refused each other, so
    /// a movement onto a bay's way and one off it for each way the street runs is every movement its junction
    /// has to offer — two on an ordinary street, one on a street the scatter took (GEN-18).
    /// </summary>
    /// <remarks>
    /// <b>This is what the standoff is for</b> (<see cref="SimConfig.CarParkStandoffM"/>). The street stands
    /// off the whole rank, so every bay lies ahead of both arrivals; a rank reaching past the lane end a car
    /// arrives on would put its far bays behind that car, and a turn back into one is tighter than the
    /// junction corners (<see cref="SimConfig.JunctionCorneringRadiusM"/>) and so no movement at all.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryBayIsReachedFromEveryWayItsStreetRunsAndLeavesOntoEach(ulong seed)
    {
        var plan = Cut(seed);
        var lanes = plan.Paving(Config).Lanes;

        for (var carPark = 0; carPark < plan.CarParks.Count; carPark++)
        {
            var bays = plan.CarParks.RoadsOf(carPark);
            var street = AfterPieceAt(plan, carPark, plan.CarParks.Junction[carPark]);
            var ways = plan.Roads.Flow[street] == RoadFlow.BothWays ? 2 : 1;
            var of = $"car park {carPark} of {plan.CarParks.BaysOn(carPark, true)}"
                + $"+{plan.CarParks.BaysOn(carPark, false)} at node {plan.CarParks.Junction[carPark]}"
                + $" on a street of {ways} ways";

            for (var bay = 0; bay < bays.Length; bay++)
            {
                var into = LaneOf(lanes, bays[bay], forward: true);
                var outOf = LaneOf(lanes, bays[bay], forward: false);
                Assert.True(Onto(lanes, into) == ways, $"bay {bay} of {of} is reached {Onto(lanes, into)} ways");
                Assert.True(
                    lanes.ConnectorAt[outOf + 1] - lanes.ConnectorAt[outOf] == ways,
                    $"bay {bay} of {of} leaves "
                    + $"{lanes.ConnectorAt[outOf + 1] - lanes.ConnectorAt[outOf]} ways");
            }
        }
    }

    /// <summary>
    /// <b>A car hooks into a bay on one circle and holds the street either side of it</b> (GEN-53,
    /// <see cref="Spline.StraightArcStraightInto"/>): every movement at a bay bends in exactly one of its
    /// pieces, and that piece is laid on <see cref="SimConfig.CarParkTurnRadiusM"/> — the circle the town
    /// turns a bay on, and not whatever the room between the two lane ends affords.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryMovementAtABayIsOneTurnOnTheBaysOwnCircle(ulong seed)
    {
        var plan = Cut(seed);
        var roads = plan.Ground.Roads;
        var lanes = plan.Paving(Config).Lanes;

        var asked = 0;
        for (var connector = 0; connector < lanes.ConnectorCount; connector++)
        {
            var intoABay = roads.IsABay(lanes.LaneRoad[lanes.ConnectorToLane[connector]]);
            var outOfABay = roads.IsABay(lanes.LaneRoad[lanes.ConnectorFromLane[connector]]);
            if (!intoABay && !outOfABay) continue;

            asked++;
            var turns = 0;
            foreach (var arc in lanes.ArcsOfConnector(connector))
            {
                if (MathF.Abs(arc.Curvature) < StraightCurvature) continue;

                turns++;
                Assert.Equal(Config.CarParkTurnRadiusM, 1f / MathF.Abs(arc.Curvature), 2);
            }

            Assert.Equal(1, turns);
        }

        Assert.True(asked > 0, $"seed {seed} laid no movement at a bay to ask about");
    }

    /// <summary>How many movements end on one lane.</summary>
    static int Onto(LaneLines lanes, int lane)
    {
        var onto = 0;
        for (var connector = 0; connector < lanes.ConnectorCount; connector++)
        {
            if (lanes.ConnectorToLane[connector] == lane) onto++;
        }

        return onto;
    }

    /// <summary>The piece of the road a car park was cut into that carries on past its junction.</summary>
    static int AfterPieceAt(CityPlan plan, int carPark, int node)
    {
        var arms = plan.CarParks.RoadsOf(carPark);
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            if (plan.Roads.FromJunction[road] == node && !arms.Contains(road)) return road;
        }

        Assert.Fail($"car park {carPark} carries no piece of the road it was cut into");
        return -1;
    }

    /// <summary>And the piece of it that arrives there, which is the same road before the cut.</summary>
    static int BeforePieceAt(CityPlan plan, int carPark, int node)
    {
        var arms = plan.CarParks.RoadsOf(carPark);
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            if (plan.Roads.ToJunction[road] == node && !arms.Contains(road)) return road;
        }

        Assert.Fail($"car park {carPark} carries no piece of the road arriving at it");
        return -1;
    }

    /// <summary>
    /// <b>Every piece of a parted street runs the way that street ran</b> (GEN-18, GEN-52). A cut parts a
    /// street and does not choose one, so a street the scatter took one way is one way in both its pieces —
    /// which is what makes the pair of them at the car park one street rather than two of them meeting.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryPieceOfAPartedStreetRunsTheWayTheStreetRan(ulong seed)
    {
        var was = Towns.LaidFrom(seed);
        var now = Cut(seed);

        for (var road = 0; road < was.Roads.Count; road++)
        {
            if (!now.Roads.WasCut(road)) continue;

            foreach (var piece in Pieces(was, now, road))
            {
                Assert.Equal(was.Roads.Flow[road], now.Roads.Flow[piece]);
            }
        }
    }

    /// <summary>
    /// <b>And the two pieces at a car park are not two one-way streets meeting</b> (GEN-18): they are the one
    /// street the scatter took, parted, as a ring's arcs are one carriageway.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NoTwoOneWayStreetsMeetInATownWithCarParksInIt(ulong seed) =>
        Assert.Null(OneWays.Meeting(Cut(seed)));

    /// <summary>
    /// <b>Nor are they two of them crowding each other</b> (GEN-18): a street's chord is between its own two
    /// ends however many pieces a cut left it in, so the spacing that scatters them is measured once.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NoTwoOneWayStreetsCrowdInATownWithCarParksInIt(ulong seed) =>
        Assert.Null(OneWays.Crowding(Cut(seed), Config.CityGen.OneWayApartMinM));

    /// <summary>
    /// <b>A car park stands a locality clear of every junction but its own</b> (GEN-16, GEN-53). <b>Its own
    /// are exempt</b> on the same terms a roundabout's nodes are: an arm's far node is a piece of one car
    /// park laid out along a stub, not two spacings that landed on the same ground.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryCarParkStandsALocalityOffEveryJunctionThatIsNotItsOwn(ulong seed)
    {
        var plan = Cut(seed);
        var ownedBy = OwnedBy(plan);

        for (var junction = 0; junction < plan.Junctions.Count; junction++)
        {
            if (ownedBy[junction] < 0) continue;

            for (var other = 0; other < plan.Junctions.Count; other++)
            {
                if (other == junction || ownedBy[junction] == ownedBy[other]) continue;

                var apartM = Vector2.Distance(plan.Junctions.CentreM[junction], plan.Junctions.CentreM[other]);
                Assert.True(
                    apartM >= Config.CityGen.LocalityM,
                    $"junctions {junction} (car park {ownedBy[junction]}) and {other} (car park "
                    + $"{ownedBy[other]}) stand {apartM:F1} m apart, inside a locality of "
                    + $"{Config.CityGen.LocalityM:F0} m");
            }
        }
    }

    /// <summary>Which car park each junction belongs to — its own node and the nodes its arms end at — or −1.</summary>
    static int[] OwnedBy(CityPlan plan)
    {
        var owner = new int[plan.Junctions.Count];
        Array.Fill(owner, -1);
        for (var carPark = 0; carPark < plan.CarParks.Count; carPark++)
        {
            owner[plan.CarParks.Junction[carPark]] = carPark;
            foreach (var road in plan.CarParks.RoadsOf(carPark)) owner[plan.Roads.ToJunction[road]] = carPark;
        }

        return owner;
    }

    /// <summary>
    /// <b>A cut road arrives on the bearings its arms are read with</b> (GEN-52) — the same thing every other
    /// road owes the arms it was drawn to (<c>RoadSplineTests</c>), read the other way round. It is what says
    /// the reading and the cut are one construction and not two that nearly agree.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryCutRoadArrivesOnTheBearingsItsArmsAreReadWith(ulong seed)
    {
        var plan = Cut(seed);
        var ground = plan.Ground;
        var openRad = RoadStage.CreaseRad(Config);
        var standoffM = StandoffOfEachJunction(plan);

        var cut = 0;
        for (var road = 0; road < ground.Roads.Count; road++)
        {
            if (!ground.Roads.WasCut(road)) continue;

            cut++;
            var arcs = ground.Roads.SegmentsOf(road);
            var from = ConnectionPoints.ArmOf(ground, Config, road, atFrom: true);
            var to = ConnectionPoints.ArmOf(ground, Config, road, atFrom: false);

            Assert.Equal(0f, Apart(arcs[0].StartUnit, from.StandUnit), openRad * 2f);
            Assert.Equal(
                0f, Apart(Heading.Unit(arcs[^1].HeadingAtRad(arcs[^1].LengthM)), -to.StandUnit), openRad * 2f);

            // And the lead each arm reads really is the road standing off the junction it is read at — asked
            // of the far end of every cut road and of the near end of all but a bay's, whose foot stands its
            // own step along the street rather than on the node (GEN-53). <b>And only of a road drawn on the
            // line its junctions stand on</b>: a street the scatter took is moved onto its driven half
            // afterwards (TER-4d), so the arc from its node out to where it stands is longer than the
            // standoff by that step aside, at a car park exactly as at every other junction it has.
            if (ground.Roads.Flow[road] != RoadFlow.BothWays) continue;

            Assert.Equal(standoffM[ground.Roads.ToJunction[road]], Lead(to), 2);
            if (Array.IndexOf(plan.CarParks.Road, road) < 0)
            {
                Assert.Equal(standoffM[ground.Roads.FromJunction[road]], Lead(from), 2);
            }
        }

        Assert.True(cut > 0, $"seed {seed} cut no road to ask about");
    }

    /// <summary>
    /// What each junction's arms stand off it: <b>a car park's own reach along the street</b>
    /// (<see cref="SimConfig.CarParkStandoffM"/>, GEN-53), <b>its own lead off the street at the far end of
    /// every bay</b> (<see cref="SimConfig.CarParkBayLeadM"/>), and the standoff every other node keeps.
    /// </summary>
    static float[] StandoffOfEachJunction(CityPlan plan)
    {
        var standoffM = new float[plan.Junctions.Count];
        Array.Fill(standoffM, Config.CityGen.ConnectionStandoffM);
        for (var carPark = 0; carPark < plan.CarParks.Count; carPark++)
        {
            var node = plan.CarParks.Junction[carPark];
            var mostBays = plan.CarParks.MostBaysOnASide(carPark);
            var flow = plan.Roads.Flow[AfterPieceAt(plan, carPark, node)];
            standoffM[node] = Config.CarParkStandoffM(mostBays);
            for (var bay = plan.CarParks.BayOffsets[carPark];
                 bay < plan.CarParks.BayOffsets[carPark + 1];
                 bay++)
            {
                var laneTowardM = CarParks.LaneTowardM(Config, flow, plan.CarParks.Right[bay]);
                standoffM[plan.Roads.ToJunction[plan.CarParks.Road[bay]]] =
                    Config.CarParkBayLeadM(mostBays, laneTowardM);
            }
        }

        return standoffM;
    }

    /// <summary>How long the arc joining a node to the stand point of one of its arms is.</summary>
    static float Lead(in ConnectionPoints.Arm arm) =>
        Spline.ArcThrough(arm.NodeM, MathF.Atan2(arm.OutwardUnit.Y, arm.OutwardUnit.X), arm.StandM).LengthM;

    static float Apart(Vector2 one, Vector2 other) =>
        MathF.Acos(Math.Clamp(Vector2.Dot(one, other), -1f, 1f));

    /// <summary>How many roads meet at each junction of a plan.</summary>
    static int[] ArmsOf(CityPlan plan)
    {
        var arms = new int[plan.Junctions.Count];
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            if (plan.Roads.SegmentsOf(road).Length == 0) continue;

            arms[plan.Roads.FromJunction[road]]++;
            arms[plan.Roads.ToJunction[road]]++;
        }

        return arms;
    }
}
