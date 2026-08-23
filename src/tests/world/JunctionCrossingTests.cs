using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// TER-5c asked of every shipped map: <b>what a way through a junction takes off the other ways through
/// it is the ground its own line is driven over, and nothing else</b>. Both halves are asserted, because
/// the whole point of the table is that a junction admits more than one car at a time: everything that
/// shares ground is in it, and every pair left out really does clear the other.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class JunctionCrossingTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    public static TheoryData<string> Maps => Towns.EveryShippedMap();

    static RoadGraph GraphOf(string map) => RoadGraph.Build(Towns.Of(map), Config);

    /// <summary>How finely a section is measured for itself — well under the step the table was built at.</summary>
    const float StepM = 0.1f;

    /// <summary>
    /// <b>Being driven over is mutual</b>, and a movement is never driven over itself. A one-sided entry
    /// would be a junction where the car that asked second is refused and the car that asked first is not.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void CrossingIsMutualAndNeverWithItself(string map)
    {
        var roads = GraphOf(map);

        for (var slot = 0; slot < roads.TurnCount; slot++)
        {
            foreach (ref readonly var section in roads.Crossings.Of(slot))
            {
                Assert.NotEqual(slot, section.OnTurn);

                var back = false;
                foreach (ref readonly var other in roads.Crossings.Of(section.OnTurn))
                {
                    back |= other.OnTurn == slot;
                }

                Assert.True(back, $"{map}: turn {slot} takes ground off {section.OnTurn} and not the other way round");
            }
        }
    }

    /// <summary>A movement can only be driven over by one through the same junction.</summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NothingIsCrossedAcrossTwoJunctions(string map)
    {
        var roads = GraphOf(map);
        var lanes = LanesOfTurns(roads);

        for (var slot = 0; slot < roads.TurnCount; slot++)
        {
            var node = roads.LaneToNode[lanes[slot]];
            foreach (ref readonly var section in roads.Crossings.Of(slot))
            {
                Assert.Equal(node, roads.LaneToNode[lanes[section.OnTurn]]);
            }
        }
    }

    /// <summary>
    /// <b>A section is ground the two lines actually share</b> — every metre of it stands within the
    /// clearance of the movement that took it. A section wider than the crossing is a junction shut over
    /// ground nothing is driven on, which is the failure the whole table was rebuilt to stop.
    /// </summary>
    /// <remarks>
    /// Measured against the clearance the table was built at plus the step it was sampled at, since a
    /// section is deliberately opened out by one step either way — the true edge of a crossing lies between
    /// the last sample outside it and the first one in.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EverySectionIsGroundTheTwoLinesShare(string map)
    {
        var roads = GraphOf(map);
        var reachM = Config.JunctionCrossingClearanceM + LaidStepM(roads);
        var measured = 0;

        for (var slot = 0; slot < roads.TurnCount; slot++)
        {
            foreach (ref readonly var section in roads.Crossings.Of(slot))
            {
                measured++;
                var apartM = NearestOverTheSection(roads, slot, section.OnTurn, section.FromM, section.ToM);
                Assert.True(
                    apartM <= reachM,
                    $"{map}: turn {slot} takes {section.FromM:0.0}–{section.ToM:0.0} m of {section.OnTurn}, "
                    + $"whose far end stands {apartM:0.00} m off it");
            }
        }

        Assert.Equal(roads.Crossings.MostCrossedByOne > 0, measured > 0);
    }

    /// <summary>
    /// And the other half: <b>two lines that come near each other are in the table</b>. A test that only
    /// checked the entries would pass on an empty one.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NothingDrivenOverTheSameGroundIsLeftOut(string map)
    {
        var roads = GraphOf(map);
        var lanes = LanesOfTurns(roads);

        for (var node = 0; node < roads.NodeCount; node++)
        {
            var atTheNode = TurnsAt(roads, node);
            for (var first = 0; first < atTheNode.Count; first++)
            {
                for (var second = first + 1; second < atTheNode.Count; second++)
                {
                    var a = atTheNode[first];
                    var b = atTheNode[second];
                    if (roads.JoinArcs(a).Length == 0 || roads.JoinArcs(b).Length == 0) continue;

                    var apartM = NearestOverTheSection(roads, a, b, 0f, roads.JoinLengthM(b));
                    if (apartM > Config.JunctionCrossingClearanceM) continue;

                    Assert.True(
                        Takes(roads, a, b),
                        $"{map}: turns {a} and {b} at node {roads.LaneToNode[lanes[a]]} pass {apartM:0.00} m "
                        + "from each other and neither takes ground off the other");
                }
            }
        }
    }

    /// <summary>
    /// <b>A junction admits more than one car at a time</b>, and by a margin rather than by one pair. A
    /// table that took everything off everything would satisfy every assertion above and would be the
    /// whole-box claim this replaced, written as ground.
    /// </summary>
    /// <remarks>
    /// The bar is a share of the movements at a node, because the figure that matters is what one car in a
    /// box leaves for the rest of the junction. A town of dead ends and mid-block crossings has no node
    /// with two movements to have an opinion about, and says so rather than passing.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NoMovementIsDrivenOverEveryOtherAtItsJunction(string map)
    {
        var roads = GraphOf(map);
        var pairs = 0;
        var free = 0;

        for (var node = 0; node < roads.NodeCount; node++)
        {
            var atTheNode = TurnsAt(roads, node);
            foreach (var slot in atTheNode)
            {
                foreach (var other in atTheNode)
                {
                    if (other == slot) continue;

                    pairs++;
                    if (!Takes(roads, slot, other)) free++;
                }

                Assert.True(
                    roads.Crossings.Of(slot).Length < atTheNode.Count - 1 || atTheNode.Count < 2,
                    $"{map}: movement {slot} is driven over every other one at its junction");
            }
        }

        if (pairs == 0) return;

        // A third, which is a bar against a blanket table rather than a tuned figure: the relation this
        // replaced left four pairs in five refusing each other and would fail it many times over.
        Assert.True(
            free * 3 >= pairs,
            $"{map}: only {free} of {pairs} pairs of movements at a junction clear each other");
    }

    /// <summary>
    /// <b>Two cars going straight through one junction in opposite directions never hold each other up.</b>
    /// They are the two halves of one carriageway passing side by side — the commonest movement there is,
    /// and the one a junction that stopped for it would be a level crossing rather than a road.
    /// </summary>
    /// <remarks>
    /// The pair is named by the road and not by an angle: each one's arriving lane is the reverse of the
    /// other's leaving lane, which is exactly what "the same street, the other way" means and needs no
    /// tolerance to say. Counted as well as asserted, since a town of dead ends would satisfy it vacuously.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void TwoStraightsInOppositeDirectionsClearEachOther(string map)
    {
        var roads = GraphOf(map);
        var lanes = LanesOfTurns(roads);
        var pairs = 0;

        for (var node = 0; node < roads.NodeCount; node++)
        {
            var atTheNode = TurnsAt(roads, node);
            foreach (var a in atTheNode)
            {
                foreach (var b in atTheNode)
                {
                    if (b == a || !FaceEachOther(roads, lanes, a, b)) continue;

                    pairs++;
                    Assert.False(
                        Takes(roads, a, b),
                        $"{map}: straight movements {a} and {b} at node {node} are the two directions of one "
                        + "street and take ground off each other");
                }
            }
        }

        Assert.True(pairs > 0 || !AnyStreetRunsBothWays(roads, lanes), $"{map}: no opposing straights were checked");
    }

    /// <summary>Whether two movements are one street's two directions driven straight through one junction.</summary>
    static bool FaceEachOther(RoadGraph roads, int[] lanes, int a, int b) =>
        KindOf(roads, lanes, a) == LaneTurn.Straight
        && KindOf(roads, lanes, b) == LaneTurn.Straight
        && roads.LaneReverse[lanes[a]] == roads.TurnToLane(b)
        && roads.LaneReverse[lanes[b]] == roads.TurnToLane(a);

    /// <summary>Whether any straight movement in the town has a lane running back the other way at all — the vacuous case.</summary>
    static bool AnyStreetRunsBothWays(RoadGraph roads, int[] lanes)
    {
        for (var slot = 0; slot < roads.TurnCount; slot++)
        {
            if (KindOf(roads, lanes, slot) == LaneTurn.Straight && roads.LaneReverse[lanes[slot]] >= 0) return true;
        }

        return false;
    }

    static LaneTurn KindOf(RoadGraph roads, int[] lanes, int slot) =>
        roads.TurnKindsFrom(lanes[slot])[slot - roads.TurnSlotAt(lanes[slot], 0)];

    /// <summary>
    /// <b>A movement's own runs are the near side of the same crossings</b>: every metre of one stands
    /// within the clearance of a join this one is driven over. Held as the span from the first crossing
    /// point to the last, a movement across a wide box took the whole of its own way through it — and the
    /// metres in the middle, which nothing comes near, were a junction shut over ground nobody crosses.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryOwnRunIsGroundACrossingStandsOn(string map)
    {
        var roads = GraphOf(map);
        var reachM = Config.JunctionCrossingClearanceM + LaidStepM(roads);
        var measured = 0;

        for (var slot = 0; slot < roads.TurnCount; slot++)
        {
            foreach (ref readonly var run in roads.Crossings.OwnRuns(slot))
            {
                measured++;
                for (var alongM = run.FromM; alongM <= run.ToM; alongM += StepM)
                {
                    var apartM = NearestCrossedM(roads, slot, alongM);
                    Assert.True(
                        apartM <= reachM,
                        $"{map}: turn {slot} holds {run.FromM:0.0}–{run.ToM:0.0} m of its own join, whose "
                        + $"{alongM:0.0} m mark stands {apartM:0.00} m off the nearest thing it crosses");
                }
            }
        }

        Assert.Equal(roads.Crossings.MostOwnRuns > 0, measured > 0);
    }

    /// <summary>How far one place on a movement's own join stands off the nearest join it is driven over.</summary>
    static float NearestCrossedM(RoadGraph roads, int slot, float alongM)
    {
        var atM = Spline.SampleAt(roads.JoinArcs(slot), alongM).PositionM;
        var leastM = float.PositiveInfinity;
        foreach (ref readonly var section in roads.Crossings.Of(slot))
        {
            var crossed = section.OnTurn;
            leastM = MathF.Min(leastM, ToChainM(roads.JoinArcs(crossed), roads.JoinLengthM(crossed), atM));
        }

        return leastM;
    }

    static bool Takes(RoadGraph roads, int slot, int other)
    {
        foreach (ref readonly var section in roads.Crossings.Of(slot))
        {
            if (section.OnTurn == other) return true;
        }

        return false;
    }

    /// <summary>The step the table sampled a join at, which is what a section's edges are rounded out by.</summary>
    static float LaidStepM(RoadGraph roads)
    {
        var mostM = 0f;
        for (var slot = 0; slot < roads.TurnCount; slot++) mostM = MathF.Max(mostM, roads.JoinLengthM(slot));

        return mostM;
    }

    /// <summary>How far the far end of a stretch of one join stands off another join's whole line.</summary>
    static float NearestOverTheSection(RoadGraph roads, int over, int crossed, float fromM, float toM)
    {
        var arcs = roads.JoinArcs(over);
        var overM = roads.JoinLengthM(over);
        var mostM = 0f;
        for (var alongM = fromM; alongM <= toM; alongM += StepM)
        {
            var atM = Spline.SampleAt(roads.JoinArcs(crossed), alongM).PositionM;
            mostM = MathF.Max(mostM, ToChainM(arcs, overM, atM));
        }

        return mostM;
    }

    static float ToChainM(ReadOnlySpan<ArcSeg> arcs, float lengthM, Vector2 pointM)
    {
        var leastM = float.PositiveInfinity;
        for (var alongM = 0f; alongM <= lengthM; alongM += StepM)
        {
            leastM = MathF.Min(leastM, (Spline.SampleAt(arcs, alongM).PositionM - pointM).Length());
        }

        return leastM;
    }

    static List<int> TurnsAt(RoadGraph roads, int node)
    {
        var turns = new List<int>();
        foreach (var lane in roads.LanesIn(node))
        {
            for (var turn = 0; turn < roads.TurnsFrom(lane).Length; turn++) turns.Add(roads.TurnSlotAt(lane, turn));
        }

        return turns;
    }

    static int[] LanesOfTurns(RoadGraph roads)
    {
        var of = new int[roads.TurnCount];
        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            for (var turn = 0; turn < roads.TurnsFrom(lane).Length; turn++) of[roads.TurnSlotAt(lane, turn)] = lane;
        }

        return of;
    }
}
