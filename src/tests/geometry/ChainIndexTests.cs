using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.Geometry;

/// <summary>
/// The index's whole contract: <b>the same chain the whole-network scan would have named, and the same
/// distance along it</b> — not a nearer one, not an equally near one, the same one. It replaced a scan
/// that decides which lane a car reacquires and which pavement a walker sets off down, so an index that
/// merely found <em>a</em> nearest chain would be a town that routes differently on a tie.
/// </summary>
/// <remarks>
/// Asserted against brute force over the same set, at three cell sizes, on a real town's lanes rather
/// than on a made-up scatter — and probed well outside the town as well as inside it, because a point
/// off the far edge of the grid is the one case the ring search cannot bound and has to fall back for.
/// </remarks>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P1)]
public class ChainIndexTests
{
    /// <summary>Well under, about, and well over a road's own width.</summary>
    public static TheoryData<float> CellSizes => [3f, 16f, 200f];

    [Theory]
    [MemberData(nameof(CellSizes))]
    public void NamesTheChainTheScanWouldHave(float cellSizeM)
    {
        var config = new SimConfig();
        var roads = RoadGraph.Build(Towns.Of(Towns.Fixture), config);

        var builder = new ChainIndex.Builder();
        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            builder.Add(lane, roads.ArcsOf(lane), roads.LaneLengthM[lane]);
        }

        var index = builder.Seal(cellSizeM);
        Assert.Equal(roads.LaneCount, index.ChainCount);

        // An index of nothing agrees with a scan of nothing, so the population is asserted rather than
        // assumed: this test passing over an empty graph would say nothing at all.
        Assert.True(roads.LaneCount > 8, $"the fixture has {roads.LaneCount} lanes to choose between");

        var rng = new Random(1);
        var probed = 0;
        foreach (var pointM in Probes(roads, rng))
        {
            var wanted = Scan(roads, pointM, out var wantedAlongM);
            var got = index.Nearest(pointM, out var gotAlongM);

            Assert.Equal(wanted, got);
            Assert.Equal(wantedAlongM, gotAlongM);
            probed++;
        }

        Assert.True(probed > 8, $"only {probed} points were asked");
    }

    /// <summary>
    /// <b>A candidate query leaves out nothing the exact one would have found.</b> That is the whole of what
    /// makes narrowing a caller's search safe: the caller's own test is unchanged, so the answer can only
    /// differ by a pair the grid never offered — and every pair the grid must offer is one
    /// <see cref="ChainIndex.Near"/> would have named at the same radius.
    /// </summary>
    [Theory]
    [MemberData(nameof(CellSizes))]
    public void EveryChainWithinTheRadiusIsOfferedAsACandidate(float cellSizeM)
    {
        var config = new SimConfig();
        var roads = RoadGraph.Build(Towns.Of(Towns.Fixture), config);
        var index = Laid(roads, cellSizeM);

        var exact = new int[roads.LaneCount];
        var alongM = new float[roads.LaneCount];
        var offered = new int[roads.LaneCount];
        var rng = new Random(1);
        var asked = 0;
        foreach (var pointM in Probes(roads, rng))
        {
            foreach (var radiusM in (float[])[0f, 2f, 25f])
            {
                var within = index.Near(pointM, radiusM, exact, alongM);
                var candidates = index.Around(pointM, radiusM, offered);
                var set = offered.AsSpan(0, Math.Min(candidates, offered.Length));
                for (var at = 0; at < within; at++)
                {
                    Assert.True(
                        set.Contains(exact[at]),
                        $"lane {exact[at]} passes within {radiusM} m of {pointM} and was not offered");
                }

                asked++;
            }
        }

        Assert.True(asked > 8, $"only {asked} questions were put");
    }

    /// <summary>
    /// <b>And nothing that crosses a line is left out of what could cross it</b> — asked of a lattice of
    /// lines whose crossings are known by construction rather than of a town, since what is being checked
    /// is that the answer holds every one of them and not that a town has any.
    /// </summary>
    /// <remarks>
    /// <b>It is also asked to narrow.</b> A query that hands back the whole set is a superset too and would
    /// pass the first half of this on its own, so the lines are spread a long way apart and one of them is
    /// asked: what comes back is the eight it crosses and not the sixteen there are.
    /// </remarks>
    [Fact]
    public void EveryLineThatCrossesOneIsOfferedAndTheRestAreNot()
    {
        const int lines = 8;
        const float apartM = 400f;
        const float lengthM = apartM * lines;

        var builder = new ChainIndex.Builder();
        for (var line = 0; line < lines; line++)
        {
            var atM = apartM * (line + 0.5f);
            builder.Add(line, [new ArcSeg(new Vector2(0f, atM), 0f, lengthM, 0f)], lengthM);
            builder.Add(
                lines + line, [new ArcSeg(new Vector2(atM, 0f), MathF.PI * 0.5f, lengthM, 0f)], lengthM);
        }

        var index = builder.Seal(50f);
        var offered = new int[index.ChainCount];

        var acrossM = apartM * 0.5f;
        var across = new ArcSeg[] { new(new Vector2(acrossM, 0f), MathF.PI * 0.5f, lengthM, 0f) };
        var found = index.Crossing(across, 0f, offered);
        var set = offered.AsSpan(0, found);

        for (var line = 0; line < lines; line++)
        {
            Assert.True(set.Contains(line), $"the line across crosses {line} and it was not offered");
        }

        // The seven lines running the same way as this one stand four hundred metres off it and share no
        // cell with it, so a candidate set holding them would be the whole set wearing a narrowing's name.
        Assert.Equal(lines + 1, found);
    }

    /// <summary>
    /// <b>The lattice belongs to the map and not to the set</b>: its origin stands on a whole cell, so two
    /// indexes sealed at one cell size lay their cells on the same lines and one picture of the grid is a
    /// picture of the ground every index is asked over (OBS-2r).
    /// </summary>
    [Theory]
    [MemberData(nameof(CellSizes))]
    public void TwoIndexesOfDifferentGroundShareOneLattice(float cellSizeM)
    {
        var config = new SimConfig();
        var roads = RoadGraph.Build(Towns.Of(Towns.Fixture), config);

        var whole = Laid(roads, cellSizeM);
        var half = new ChainIndex.Builder();
        for (var lane = 0; lane < roads.LaneCount; lane += 2)
        {
            half.Add(lane, roads.ArcsOf(lane), roads.LaneLengthM[lane]);
        }

        var some = half.Seal(cellSizeM);

        Assert.Equal(whole.CellM, some.CellM);

        var offsetM = whole.OriginM - some.OriginM;
        foreach (var alongM in (float[])[offsetM.X, offsetM.Y])
        {
            var cells = alongM / whole.CellM;
            Assert.Equal(MathF.Round(cells), cells, tolerance: 1e-3f);
        }
    }

    /// <summary>
    /// <b>The chains a cell hands back are the chains it counts</b> (OBS-2t): the same set, once each, and
    /// every one of them really has a piece in that cell — which is what a reader clicking a cell to see
    /// what a question asked there would be narrowed to is being shown.
    /// </summary>
    /// <remarks>
    /// Asked of every cell of a real town's lanes, and checked against the cells the chain was written into
    /// rather than against a second walk of its geometry: the index's answer is what the picture draws, so
    /// what has to hold is that the ids and the count are two readings of one table.
    /// </remarks>
    [Fact]
    public void ACellHandsBackTheChainsItCounts()
    {
        var config = new SimConfig();
        var roads = RoadGraph.Build(Towns.Of(Towns.Fixture), config);
        var index = Laid(roads, 16f);
        var held = new int[index.ChainCount];
        var seen = new bool[index.ChainCount];
        var asked = 0;

        for (var y = 0; y < index.Height; y++)
        {
            for (var x = 0; x < index.Width; x++)
            {
                var count = index.ChainsInCell(x, y);
                Assert.Equal(count, index.ChainsInCell(x, y, held));
                if (count == 0) continue;

                asked++;
                Array.Clear(seen);
                for (var at = 0; at < count; at++)
                {
                    Assert.InRange(held[at], 0, roads.LaneCount - 1);
                    Assert.False(seen[held[at]], $"lane {held[at]} was offered twice by cell {x},{y}");
                    seen[held[at]] = true;

                    // The cell really holds it: the chain has a station inside the square, which is the
                    // binning's own claim about why it is in there.
                    Assert.True(
                        Stations(roads.ArcsOf(held[at]), roads.LaneLengthM[held[at]])
                            .Any(atM => Inside(index, x, y, atM)),
                        $"cell {x},{y} holds lane {held[at]}, {roads.LaneLengthM[held[at]]:F1} m over "
                        + $"{roads.ArcsOf(held[at]).Length} arcs from {roads.StartOf(held[at]).PositionM} "
                        + $"to {roads.EndOf(held[at]).PositionM}, and no station of it is in the cell");
                }
            }
        }

        Assert.True(asked > 0, "no cell of the fixture's lanes held anything");
    }

    /// <summary>
    /// A lane walked at the step the index bins it by, so a station of it lands in every cell it runs
    /// through. <b>Piece by piece and not by distance along the chain</b>, which is how the binning walks
    /// it: a chain's own length and the sum of its pieces' are two computations of one number, and asking
    /// this question by the first would leave the last metres of a long piece unwalked.
    /// </summary>
    static IEnumerable<Vector2> Stations(ReadOnlySpan<ArcSeg> arcs, float lengthM)
    {
        var walked = new List<Vector2>();
        foreach (var arc in arcs)
        {
            for (var atM = 0f; ; atM += 0.25f)
            {
                walked.Add(arc.PointAtM(MathF.Min(atM, arc.LengthM)));
                if (atM >= arc.LengthM) break;
            }
        }

        return walked;
    }

    static bool Inside(ChainIndex index, int atX, int atY, Vector2 pointM)
    {
        var offM = pointM - index.OriginM - new Vector2(atX * index.CellM, atY * index.CellM);

        // A piece claims the cells within half a sample step of it, which is what the margin here is.
        return offM.X >= -0.5f && offM.Y >= -0.5f && offM.X <= index.CellM + 0.5f && offM.Y <= index.CellM + 0.5f;
    }

    static ChainIndex Laid(RoadGraph roads, float cellSizeM)
    {
        var builder = new ChainIndex.Builder();
        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            builder.Add(lane, roads.ArcsOf(lane), roads.LaneLengthM[lane]);
        }

        return builder.Seal(cellSizeM);
    }

    /// <summary>
    /// Points on the lanes, points a street away from them, and points right outside the town — the
    /// three cases being on the grid, one ring off it, and past its far corner.
    /// </summary>
    static IEnumerable<Vector2> Probes(RoadGraph roads, Random rng)
    {
        for (var lane = 0; lane < roads.LaneCount; lane += 3)
        {
            var alongM = (float)rng.NextDouble() * roads.LaneLengthM[lane];
            var onM = Spline.SampleAt(roads.ArcsOf(lane), alongM).PositionM;
            yield return onM;
            yield return onM + new Vector2((float)rng.NextDouble() * 60f - 30f, (float)rng.NextDouble() * 60f - 30f);
        }

        foreach (var farM in (Vector2[])[new(-5_000f, -5_000f), new(50_000f, 0f), new(0f, 50_000f), new(1e6f, 1e6f)])
        {
            yield return farM;
        }
    }

    /// <summary>The scan the index replaced, kept here as the thing it is measured against and nowhere else.</summary>
    static int Scan(RoadGraph roads, Vector2 pointM, out float progressM)
    {
        var best = -1;
        var bestDistanceSq = float.MaxValue;
        progressM = 0f;

        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            var arcs = roads.ArcsOf(lane);
            var alongM = Spline.ProjectM(arcs, pointM, roads.LaneLengthM[lane] * 0.5f, roads.LaneLengthM[lane]);
            var distanceSq = (Spline.SampleAt(arcs, alongM).PositionM - pointM).LengthSquared();
            if (distanceSq >= bestDistanceSq) continue;

            bestDistanceSq = distanceSq;
            progressM = alongM;
            best = lane;
        }

        return best;
    }
}
