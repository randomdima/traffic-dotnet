using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// Where the town's walks are cut beside its roads (<see cref="KerbEnds"/>) — here, which roads are cut at
/// both ends and which are short enough to be cut once between them. Where a town's kerbs end at all is
/// asked of whole towns in <see cref="KerbEndsInATownTests"/>.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P6)]
public class KerbEndsTests
{
    /// <summary>
    /// <b>A road whose two stations stand closer together than the figure is crossed once, midway between
    /// them, and held at its two kerb ends instead of at them</b> (WLK-10a,
    /// <see cref="RoadFigures.CrossedOnceBelowM"/>): two zebras a walker can stand between are one zebra in
    /// the wrong two places, and a bar left standing at a station the paint has gone from is a driver
    /// holding a carriageway's width out from the box they are held for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Read twice off the one town, at two figures</b> — at nought, where nothing is welded and every road
    /// is held at the stations themselves, and at <see cref="WideM"/>, which is wide enough that a good many
    /// of the fixture's streets are welded and the rest are not. What is staged that way is the rule's own
    /// arithmetic rather than whether the shipped figure happens to catch a street of this fixture, and the
    /// reading at nought is where the stations are.
    /// </para>
    /// <para>
    /// <b>Measured along the road's own line</b> — the two stations, the crossing and the hold — because that
    /// is the one frame they stand in: each is read off one of the road's two kerbs, so they stand on lines
    /// half a carriageway either side of it and their own metres run against each other.
    /// </para>
    /// </remarks>
    [Fact]
    public void ARoadCutTwiceCloseTogetherIsCrossedOnceBetweenThemAndHeldAtItsKerbs()
    {
        var plan = Towns.Of(Towns.Fixture);

        // The one paving, asked twice: the figure moves nothing the boundary or the lines are read from, so
        // what changes between the two readings is this rule and nothing else.
        var paving = plan.Paving(SimConfig.Shipped());
        var config = Below(WideM);
        var stations = Cuts(KerbEnds.Of(paving, Below(0f)).HeldM);
        var ends = KerbEnds.Of(paving, config);
        var crossed = Cuts(ends.CrossedM);
        var held = Cuts(ends.HeldM);
        var clearM = config.Road.FootNodeClearM;

        var welded = 0;
        var worstM = 0f;
        foreach (var (road, both) in stations)
        {
            if (both.Count != 2) continue;

            var arcs = plan.Roads.SegmentsOf(road);
            var lengthM = Spline.TotalLengthM(arcs);
            var oneM = AlongM(arcs, lengthM, both[0]);
            var otherM = AlongM(arcs, lengthM, both[1]);
            var paint = crossed[road];
            if (MathF.Abs(oneM - otherM) >= WideM)
            {
                Assert.Equal(2, paint.Count);
                Assert.DoesNotContain(paint, one => one.Cut == KerbCut.Midway);
                Assert.All(held[road], one => Assert.True(one.Painted));
                continue;
            }

            welded++;
            var single = Assert.Single(paint);
            Assert.Equal(KerbCut.Midway, single.Cut);
            var middleM = (oneM + otherM) * 0.5f;
            worstM = MathF.Max(worstM, MathF.Abs(AlongM(arcs, lengthM, single) - middleM));

            // Held at the kerb ends, which stand the clearance the stations were struck at back towards the
            // box — each of them on the side of the middle its own station was.
            foreach (var one in held[road])
            {
                Assert.False(one.Painted);
                var atM = AlongM(arcs, lengthM, one);
                var wasM = atM < middleM ? MathF.Min(oneM, otherM) : MathF.Max(oneM, otherM);
                worstM = MathF.Max(
                    worstM, MathF.Abs(atM - (wasM + (atM < middleM ? -clearM : clearM))));
            }
        }

        // The staging and not the claim: a fixture with no street short enough would ask nothing, and pass.
        Assert.True(welded > 0, $"no street of the fixture is cut twice within {WideM:F0} m");
        Assert.InRange(worstM, 0f, FramesM);
    }

    /// <summary>Where one station stands in the metres of its road's own line.</summary>
    static float AlongM(ReadOnlySpan<ArcSeg> arcs, float lengthM, KerbNodes cut) =>
        Spline.ProjectM(arcs, (cut.NearM + cut.FarM) * 0.5f, lengthM * 0.5f, lengthM);

    /// <summary>One of a reading's two answers, by the road each of its stations belongs to.</summary>
    static Dictionary<int, List<KerbNodes>> Cuts(ReadOnlySpan<KerbNodes> stations)
    {
        var cuts = new Dictionary<int, List<KerbNodes>>();
        foreach (var nodes in stations)
        {
            if (!cuts.TryGetValue(nodes.Road, out var standing)) cuts[nodes.Road] = standing = [];

            standing.Add(nodes);
        }

        return cuts;
    }

    /// <summary>The shipped figures with the one this asks about moved.</summary>
    static SimConfig Below(float onceBelowM) =>
        new() { Road = new RoadFigures { CrossedOnceBelowM = onceBelowM } };

    /// <summary>
    /// The figure the fixture is read at: wide enough that some of its streets are cut once and others
    /// twice, so that both halves of the rule are asked of the same town.
    /// </summary>
    const float WideM = 60f;

    /// <summary>
    /// How far the station a pair was welded into may stand off the middle of that pair: the two are averaged
    /// in the metres of one kerb's lane and weighed here in the road's own, which are the same metres to
    /// within what half a carriageway of offset costs over the bend a short street makes.
    /// </summary>
    /// <remarks>
    /// <b>Measured on the fixture rather than derived</b>, so the claim is stated as a bound it does not lean
    /// on, and <b>the worst of them is what is weighed</b> — a reading that grows says by how much rather
    /// than naming whichever street first crossed a line. It is a bound on what <see cref="WideM"/> asks and
    /// not on what the town lays: the two frames part over the length between the stations, and the shipped
    /// figure welds a pair less than half as far apart as this reading does.
    /// </remarks>
    const float FramesM = 0.2f;
}
