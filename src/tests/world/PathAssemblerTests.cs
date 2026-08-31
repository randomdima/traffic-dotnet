using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Terrain;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// The assembled line asked of every shipped map. A lane's own line is the graph's
/// (<see cref="RoadGraphTests"/>); what is asked here is the part the assembler <em>draws</em> — the
/// join through each junction, which is the only geometry in the town that no plan carries.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class PathAssemblerTests
{
    public static TheoryData<string> Maps => Towns.EveryTown();

    /// <summary>
    /// Five centimetres, and the figure is <see cref="ArcSeg"/>'s arithmetic rather than the join's
    /// geometry: a nearly straight arc is held as a huge radius and a tiny curvature, and walking it
    /// back to a point differences two sines that agree to five figures. The worst join in any shipped
    /// town is 2.4 cm out, against a lane 3 m wide.
    /// </summary>
    const float JoinToleranceM = 0.05f;

    /// <summary>A hundredth of a radian — half a degree — which is the same arithmetic seen as an angle.</summary>
    const float KinkToleranceRad = 0.01f;

    /// <summary>The millimetre the walk over an assembled line steps in, and so the shortest stretch of one it can see.</summary>
    const float WalkedStepM = 1e-3f;

    /// <summary>
    /// <b>Every turn a car may take is a line a car may drive over.</b> A join that leaves the paved
    /// ground is a car driven onto the pavement while following its own line perfectly, which no
    /// measure of lane discipline can see.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryTurnIsDrawnOverGroundACarMayBeOn(string map)
    {
        var plan = Towns.Of(map);
        var config = SimConfig.Shipped();
        var graph = RoadGraph.Build(plan, config);
        var terrain = new TerrainGrid(plan, config);
        var arcs = new ArcSeg[PathAssembler.ArcsFor(graph)];
        var starts = new float[PathAssembler.MostLanes];
        var ends = new float[PathAssembler.MostLanes];

        var turns = 0;
        var offRoad = 0;
        var worst = string.Empty;
        Span<int> pair = stackalloc int[2];

        for (var lane = 0; lane < graph.LaneCount; lane++)
        {
            foreach (var onto in graph.TurnsFrom(lane))
            {
                pair[0] = lane;
                pair[1] = onto;
                var line = PathAssembler.Assemble(graph, pair, arcs, starts, ends);
                turns++;

                var offM = 0f;
                for (var alongM = ends[0]; alongM <= starts[1]; alongM += 0.25f)
                {
                    var pointM = Spline.SampleAt(arcs.AsSpan(0, line.ArcCount), alongM).PositionM;
                    if (terrain.At(pointM).Drivable) continue;

                    offM = alongM;
                    if (worst.Length == 0)
                    {
                        worst = $"lane {lane} onto {onto} ({graph.TurnBetween(lane, onto)}) crosses " +
                                $"{terrain.GroundAt(pointM)} at {pointM}";
                    }
                }

                if (offM > 0f) offRoad++;
            }
        }

        Assert.True(offRoad == 0, $"{map}: {offRoad} of {turns} turns are drawn over ground a car may not be on — {worst}");
    }

    /// <summary>
    /// <b>An assembled line is one line.</b> Every piece starts where the piece before it ended and
    /// heads the way that one was heading — a break of either kind is a place a car cannot be on both
    /// sides of, and the follower would report it as a car that had left its lane.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryLineIsContinuousThroughEveryTurn(string map)
    {
        var plan = Towns.Of(map);
        var config = SimConfig.Shipped();
        var graph = RoadGraph.Build(plan, config);
        var arcs = new ArcSeg[PathAssembler.ArcsFor(graph)];
        var starts = new float[PathAssembler.MostLanes];
        var ends = new float[PathAssembler.MostLanes];
        Span<int> pair = stackalloc int[2];

        for (var lane = 0; lane < graph.LaneCount; lane++)
        {
            foreach (var onto in graph.TurnsFrom(lane))
            {
                pair[0] = lane;
                pair[1] = onto;
                var line = PathAssembler.Assemble(graph, pair, arcs, starts, ends);

                for (var arc = 1; arc < line.ArcCount; arc++)
                {
                    var before = arcs[arc - 1];
                    var gapM = (arcs[arc].StartM - before.EndM).Length();
                    var kinkRad = MathF.Abs(Spline.WrapRad(arcs[arc].HeadingRad - before.HeadingAtRad(before.LengthM)));

                    Assert.True(gapM < JoinToleranceM, $"{map}: lane {lane} onto {onto} breaks by {gapM:F3} m at piece {arc}");
                    Assert.True(kinkRad < KinkToleranceRad, $"{map}: lane {lane} onto {onto} kinks by {kinkRad:F3} rad at piece {arc}");
                }
            }
        }
    }

    /// <summary>
    /// <b>A place on a lane is the same place on the line</b>, and the whole point is that it is the same
    /// place <em>on the ground</em>: a lane's metres are its own arclength, so a bar or a crossing carried
    /// onto a driven line has been carried round every bend between them rather than along a chord.
    /// </summary>
    /// <remarks>
    /// The town measures its furniture against lanes and a driver meets all of it on one assembled line, so
    /// this conversion stands under `P-8`'s bar and a crossing's paint alike. Checked by sampling both — the
    /// arithmetic can only agree by describing one point.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void APlaceOnALaneIsTheSamePlaceOnTheLine(string map)
    {
        var config = SimConfig.Shipped();
        var graph = RoadGraph.Build(Towns.Of(map), config);
        var arcs = new ArcSeg[PathAssembler.ArcsFor(graph)];
        var starts = new float[PathAssembler.MostLanes];
        var ends = new float[PathAssembler.MostLanes];
        Span<int> pair = stackalloc int[2];

        var worstM = 0f;
        var worst = string.Empty;

        for (var lane = 0; lane < graph.LaneCount; lane++)
        {
            var turns = graph.TurnsFrom(lane);
            for (var turn = 0; turn < turns.Length; turn++)
            {
                var slot = graph.TurnSlotAt(lane, turn);
                pair[0] = lane;
                pair[1] = turns[turn];
                var line = PathAssembler.Assemble(graph, pair, arcs, starts, ends);
                var driven = arcs.AsSpan(0, line.ArcCount);

                // Each lane over the stretch of it the line was laid from: the leaving join's setback short
                // of the end on the first, the arriving join's setback in on the second.
                for (var at = 0; at < 2; at++)
                {
                    var of = pair[at];
                    var fromM = at == 0 ? 0f : graph.JoinToM(slot);
                    var toM = at == 0 ? graph.LaneLengthM[of] - graph.JoinFromM(slot) : graph.LaneLengthM[of];

                    for (var alongM = fromM; alongM <= toM; alongM += 1f)
                    {
                        var onLineM = PathAssembler.OnTheLineM(graph, pair, starts, ends, at, alongM);
                        var offM = (Spline.SampleAt(driven, onLineM).PositionM
                                    - Spline.SampleAt(graph.ArcsOf(of), alongM).PositionM).Length();

                        if (offM <= worstM) continue;

                        worstM = offM;
                        worst = $"lane {of} at {alongM:F1} m reads {onLineM:F1} m along the line of {lane} onto {turns[turn]}";
                    }
                }
            }
        }

        Assert.True(worstM < JoinToleranceM, $"{map}: a place on a lane lands {worstM:F2} m off itself on the line — {worst}");
    }

    /// <summary>
    /// <b>The stretch of a line between two lanes is the town's own join, arc for arc.</b> It is the
    /// whole reason the join is laid once with the town: the line a car is handed and the movement an
    /// overlay draws through a box are then one shape rather than two constructions that agree until one
    /// of them is changed.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void TheJunctionStretchOfALineIsTheTownsOwnJoin(string map)
    {
        var config = SimConfig.Shipped();
        var graph = RoadGraph.Build(Towns.Of(map), config);
        var arcs = new ArcSeg[PathAssembler.ArcsFor(graph)];
        var starts = new float[PathAssembler.MostLanes];
        var ends = new float[PathAssembler.MostLanes];
        Span<int> pair = stackalloc int[2];

        for (var lane = 0; lane < graph.LaneCount; lane++)
        {
            var turns = graph.TurnsFrom(lane);
            for (var turn = 0; turn < turns.Length; turn++)
            {
                var slot = graph.TurnSlotAt(lane, turn);
                pair[0] = lane;
                pair[1] = turns[turn];
                var line = PathAssembler.Assemble(graph, pair, arcs, starts, ends);

                // The junction is the stretch between where the first lane's own pieces end and where
                // the second's begin, which is what the assembler reports in those two spans.
                // A centimetre, and it is the running sum's arithmetic rather than the join's: the
                // assembler totals the same arc lengths in a different order.
                var acrossM = MathF.Abs(graph.JoinLengthM(slot) - (starts[1] - ends[0]));
                Assert.True(acrossM < 0.01f, $"{map}: lane {lane} onto {turns[turn]} crosses {acrossM:F3} m more than its join is long");

                // A place cut into a road (GEN-4h) has its two lanes meeting at a point, so the movement
                // between them is a join of no length — and one drawn a rounding off a point is the same
                // thing wearing float noise. There is no stretch to walk in either case.
                if (graph.JoinLengthM(slot) < WalkedStepM) continue;

                var join = graph.JoinArcs(slot);
                var laid = 0;
                var atM = 0f;
                for (var arc = 0; arc < line.ArcCount; arc++)
                {
                    if (atM >= ends[0] - WalkedStepM && atM < starts[1] - WalkedStepM)
                    {
                        Assert.True(laid < join.Length, $"{map}: lane {lane} onto {turns[turn]} crosses on more arcs than the town laid");
                        Assert.Equal(join[laid++], arcs[arc]);
                    }

                    atM += arcs[arc].LengthM;
                }

                Assert.Equal(join.Length, laid);
            }
        }
    }
}
