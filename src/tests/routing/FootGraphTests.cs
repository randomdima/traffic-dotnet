using System.Collections.Concurrent;
using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Terrain;
using Xunit;

namespace TrafficSimulation.Tests.Routing;

/// <summary>
/// The fine foot graph swept against the cells on every shipped map. <b>A foot edge is a nominal line and
/// nothing else in the town reads it</b>, so a line over water or through a wall is silent until a walker
/// goes into the river — which makes this sweep the only thing standing between a derivation and a town
/// that quietly walks people off the pavement.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class FootGraphTests
{
    /// <summary>
    /// Every town, <b>and the lattice the walking exam is staged on</b>: it is a map whose whole purpose is
    /// the pavement, and a sweep of the pavement that skipped it would be leaving out the one place the
    /// network is looked at hardest.
    /// </summary>
    public static TheoryData<string> Maps
    {
        get
        {
            var maps = Towns.EveryTown();
            maps.Add(FootwayPlan.Name);
            return maps;
        }
    }

    /// <summary>One map's foot graph, built once and read by every claim about it.</summary>
    static FootGraph Of(string map) => Built.GetOrAdd(map, at => FootGraph.Build(Towns.Of(at), SimConfig.Shipped()));

    static readonly ConcurrentDictionary<string, FootGraph> Built = new();

    /// <summary>
    /// <b>Every metre of pavement stands half a walk from the tarmac</b> (TER-3c.3) — which is the whole of
    /// what laying it by wrapping the carriageway claims, and the one thing a junction, a car park and a
    /// straight street all have to answer the same.
    /// </summary>
    /// <remarks>
    /// <b>Asked of the ground the town answers with and not of the shapes the line was cut by.</b> Put to
    /// <see cref="Kerbs"/> it would be the derivation restated (VER-12): the wrap keeps exactly the stations
    /// that pass it. Put to <see cref="GroundShapes"/> it is the other book, and what it catches is a line
    /// laid off a record the ground disagrees with — which is what a pavement pieced together at the
    /// junctions was, everywhere it was pieced.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryStretchOfPavementStandsHalfAWalkFromTheTarmac(string map)
    {
        var plan = Towns.Of(map);
        var shapes = new GroundShapes(plan.Ground, SimConfig.Shipped());
        var foot = Of(map);
        var wantedM = plan.PavementWidthM * 0.5f;

        var tooNear = new SortedDictionary<string, (int Count, Vector2 First)>();
        var tooFar = new SortedDictionary<string, (int Count, Vector2 First)>();
        for (var edge = 0; edge < foot.EdgeCount; edge += 2)
        {
            if (foot.KindOf(edge) != FootEdgeKind.Pavement) continue;

            var arcs = foot.ArcsOf(edge);
            var lengthM = foot.LengthM(edge);
            var stations = Math.Max(1, (int)MathF.Ceiling(lengthM / StationM));
            for (var station = 0; station <= stations; station++)
            {
                var atM = Spline.SampleAt(arcs, lengthM * station / stations).PositionM;
                var key = $"{foot.LengthM(edge):F1} m from {foot.AnchorM(foot.FromNode(edge))}";
                if (AnyTarmacRound(shapes, atM, wantedM - SlackM))
                {
                    tooNear[key] = tooNear.TryGetValue(key, out var near) ? (near.Count + 1, near.First) : (1, atM);
                }

                if (!AnyTarmacRound(shapes, atM, wantedM + SlackM))
                {
                    tooFar[key] = tooFar.TryGetValue(key, out var far) ? (far.Count + 1, far.First) : (1, atM);
                }
            }
        }

        Assert.True(tooNear.Count == 0, $"{map}: pavement inside half a walk of the tarmac — {Breakdown(tooNear)}");
        Assert.True(tooFar.Count == 0, $"{map}: pavement with no tarmac half a walk off it — {Breakdown(tooFar)}");
    }

    /// <summary>How finely a stretch is stationed when the ground round it is read.</summary>
    const float StationM = 0.5f;

    /// <summary>
    /// How much either way of half a walk the reading is allowed: <b>a body's width</b>. It is a coarse
    /// instrument on purpose — what it is here to catch is a pavement laid off something other than the
    /// kerb beside it, which is metres out and not centimetres. Two things go into the figure: the ring's
    /// own coarseness, and that the two books measure a bend differently — the ground answers a road as an
    /// offset across the frame of the point it projects onto, and the wrap as a distance to the arc.
    /// </summary>
    static float SlackM => SimConfig.Shipped().PersonDiameterM;

    const int Bearings = 32;

    /// <summary>Whether any of the town's tarmac stands on the ring of one radius about a point.</summary>
    static bool AnyTarmacRound(GroundShapes shapes, Vector2 atM, float radiusM)
    {
        for (var bearing = 0; bearing < Bearings; bearing++)
        {
            var on = shapes.At(atM + (radiusM * Heading.Unit(MathF.Tau * bearing / Bearings)));
            if (on is Ground.Road or Ground.Intersection or Ground.Parking or Ground.Crosswalk) return true;
        }

        return false;
    }

    /// <summary>Every edge's own line stands on ground a person may stand on, sampled the whole way along it.</summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryStretchIsWalkedOnGroundAPersonMayStandOn(string map)
    {
        var plan = Towns.Of(map);
        var terrain = new GroundLocator(plan, SimConfig.Shipped());
        var foot = Of(map);

        var offFoot = 0;
        var sampled = 0;
        var where = new SortedDictionary<string, (int Count, Vector2 First)>();
        for (var edge = 0; edge < foot.EdgeCount; edge += 2)
        {
            foreach (var pointM in Along(foot, edge, SimConfig.Shipped().Terrain.GroundStepM))
            {
                sampled++;
                if (terrain.At(pointM).Walkable) continue;

                offFoot++;
                var key = $"{foot.KindOf(edge)} on {terrain.GroundAt(pointM)} " +
                          $"[{foot.LengthM(edge):F1} m from {foot.AnchorM(foot.FromNode(edge))} to {foot.AnchorM(foot.ToNode(edge))}]";
                where[key] = where.TryGetValue(key, out var seen) ? (seen.Count + 1, seen.First) : (1, pointM);
            }
        }

        Assert.True(offFoot == 0, $"{map}: {offFoot} of {sampled} samples are off the pavement — {Breakdown(where)}");
    }

    /// <summary>
    /// <b>A walker enters a parking lot only to reach or leave a car parked in it</b>, and that is a fact
    /// about which edges exist rather than a price: no edge enters a lot, ever.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NoStretchIsWalkedAcrossAParkingLot(string map)
    {
        var plan = Towns.Of(map);
        var terrain = new GroundLocator(plan, SimConfig.Shipped());
        var foot = Of(map);

        var onLot = 0;
        var worst = string.Empty;
        for (var edge = 0; edge < foot.EdgeCount; edge += 2)
        {
            foreach (var pointM in Along(foot, edge, SimConfig.Shipped().Terrain.GroundStepM))
            {
                if (terrain.GroundAt(pointM) != Ground.Parking) continue;

                onLot++;
                if (worst.Length == 0) worst = $"a {foot.KindOf(edge)} stretch at {pointM}";
            }
        }

        Assert.True(onLot == 0, $"{map}: {onLot} samples stand on a lot — {worst}");
    }

    /// <summary>
    /// Every crossing the town painted is an edge of the network, because <b>a crossing is the only edge
    /// that touches a carriageway</b> — one the graph never heard of is a road nobody can lawfully cross.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryCrossingThePlanCarriesIsAnEdge(string map)
    {
        var plan = Towns.Of(map);
        var foot = Of(map);

        var crossings = 0;
        for (var edge = 0; edge < foot.EdgeCount; edge += 2)
        {
            if (foot.KindOf(edge) == FootEdgeKind.Crossing) crossings++;
        }

        Assert.Equal(plan.Crosswalks.Count, crossings);
    }

    /// <summary>
    /// Every crossing has pavement at both ends of it. A crossing spliced onto one bank and nothing at the
    /// other is a walk onto a road and a stop, and it reads from outside as a walker changing its mind.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryCrossingHasPavementAtBothEndsOfIt(string map)
    {
        var foot = Of(map);

        for (var edge = 0; edge < foot.EdgeCount; edge += 2)
        {
            if (foot.KindOf(edge) != FootEdgeKind.Crossing) continue;

            foreach (var end in (ReadOnlySpan<int>)[foot.FromNode(edge), foot.ToNode(edge)])
            {
                var pavements = 0;
                foreach (var leaving in foot.EdgesOut(end))
                {
                    if (foot.KindOf(leaving) != FootEdgeKind.Crossing) pavements++;
                }

                Assert.True(pavements > 0, $"{map}: a crossing at {foot.AnchorM(end)} has nothing to step off onto");
            }
        }
    }

    /// <summary>
    /// No two nodes stand at one place. A curve can end a weld's width the wrong side of its own node, and
    /// a line laid through those stations steps backwards and crosses itself.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NoTwoNodesStandAtOnePlace(string map)
    {
        var foot = Of(map);
        var weldM = SimConfig.Shipped().Network.FootGraphNodeWeldM;

        for (var node = 0; node < foot.NodeCount; node++)
        {
            for (var other = node + 1; other < foot.NodeCount; other++)
            {
                var apartM = (foot.AnchorM(node) - foot.AnchorM(other)).Length();
                Assert.True(apartM > weldM, $"{map}: nodes {node} and {other} stand {apartM:F3} m apart at {foot.AnchorM(node)}");
            }
        }
    }

    /// <summary>
    /// <b>A crossing is the only stretch that stands on ground a car drives on</b>, which is the whole of
    /// what makes crossing at a crossing structural rather than priced.
    /// </summary>
    /// <remarks>
    /// Walkable ground is not the same question: a crossing's own paint is walkable, and so is a lot, so a
    /// band laid over either passes the sweep above and is still a way over a road that is not a crossing.
    /// This is the sweep that stands over anything laid between two arms of a junction, where the ground is
    /// the only thing refusing the carriageway between them.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void OnlyACrossingStandsOnGroundACarDrivesOn(string map)
    {
        var plan = Towns.Of(map);
        var terrain = new GroundLocator(plan, SimConfig.Shipped());
        var foot = Of(map);

        var onRoad = 0;
        var sampled = 0;
        var where = new SortedDictionary<string, (int Count, Vector2 First)>();
        for (var edge = 0; edge < foot.EdgeCount; edge += 2)
        {
            if (foot.KindOf(edge) == FootEdgeKind.Crossing) continue;

            foreach (var pointM in Along(foot, edge, SimConfig.Shipped().Terrain.GroundStepM))
            {
                sampled++;
                if (!terrain.At(pointM).Drivable) continue;

                onRoad++;
                var key = $"{foot.KindOf(edge)} on {terrain.GroundAt(pointM)} " +
                          $"[{foot.LengthM(edge):F1} m from {foot.AnchorM(foot.FromNode(edge))} to {foot.AnchorM(foot.ToNode(edge))}]";
                where[key] = where.TryGetValue(key, out var seen) ? (seen.Count + 1, seen.First) : (1, pointM);
            }
        }

        Assert.True(onRoad == 0, $"{map}: {onRoad} of {sampled} samples stand on a carriageway — {Breakdown(where)}");
    }

    /// <summary>The two directions of one stretch are the same line walked the other way, station for station.</summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void AnOutAndBackOverOneStretchIsItsOwnReverse(string map)
    {
        var foot = Of(map);

        for (var edge = 0; edge < foot.EdgeCount; edge += 2)
        {
            var back = foot.Reverse(edge);
            Assert.Equal(foot.FromNode(edge), foot.ToNode(back));
            Assert.Equal(foot.ToNode(edge), foot.FromNode(back));
            Assert.Equal(foot.LengthM(edge), foot.LengthM(back), 3);

            var lengthM = foot.LengthM(edge);
            for (var step = 0; step <= 8; step++)
            {
                var out_ = Spline.SampleAt(foot.ArcsOf(edge), lengthM * step / 8f).PositionM;
                var home = Spline.SampleAt(foot.ArcsOf(back), lengthM * (8 - step) / 8f).PositionM;
                Assert.True((out_ - home).Length() < 0.01f, $"{map}: stretch {edge} and its reverse part by {(out_ - home).Length():F3} m");
            }
        }
    }

    /// <summary>
    /// <b>No stretch of pavement runs alongside another one</b> (TER-3c.5). The walk is the outside of the
    /// tarmac and the outside is one line, so a stretch every metre of which lies inside another stretch's
    /// band is the same pavement laid twice — two more lanes down a footway that already has two, and a
    /// walk that crosses from one side of the band to the other to get onto them.
    /// </summary>
    /// <remarks>
    /// <b>Against the other line's middle and not its whole length</b>, because a stretch that closes a gap
    /// between two others necessarily begins and ends at their ends — that is what a gap is — and measured
    /// against a whole line every corner of the town reads as doubled.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NoStretchOfPavementIsLaidAlongsideAnother(string map)
    {
        var foot = Of(map);
        var bandM = SimConfig.Shipped().PavementWidthM;

        for (var edge = 0; edge < foot.EdgeCount; edge += 2)
        {
            if (foot.KindOf(edge) != FootEdgeKind.Pavement) continue;

            // The furthest any station of it stands from the nearest other line: a stretch is the same
            // pavement twice only where every metre of it lies inside another's band, so it is the metre
            // that stands clearest that answers.
            var clearestM = 0f;
            var alongside = -1;
            foreach (var atM in Along(foot, edge, bandM * 0.25f))
            {
                var nearestM = NearestOtherLineM(foot, edge, atM, out var other);
                if (nearestM <= clearestM) continue;

                clearestM = nearestM;
                alongside = other;
            }

            Assert.True(
                clearestM > bandM * 0.5f,
                $"{map}: no metre of stretch {edge} from {foot.AnchorM(foot.FromNode(edge))} stands further "
                + $"than {clearestM:F2} m from the middle of another line ({alongside} at its clearest), "
                + "which is the same pavement twice");
        }
    }

    /// <summary>
    /// How far a point stands from the nearest <em>middle</em> of another stretch's line — a projection that
    /// lands on an end is a line this one runs up to rather than one it runs beside.
    /// </summary>
    static float NearestOtherLineM(FootGraph foot, int edge, Vector2 pointM, out int other)
    {
        other = -1;
        var nearestM = float.MaxValue;
        for (var at = 0; at < foot.EdgeCount; at += 2)
        {
            if (at == edge || foot.KindOf(at) != FootEdgeKind.Pavement) continue;

            var lengthM = foot.LengthM(at);
            var arcs = foot.ArcsOf(at);
            var alongM = Spline.ProjectM(arcs, pointM, lengthM * 0.5f, lengthM);
            if (alongM <= EndM || alongM >= lengthM - EndM) continue;

            var apartM = (Spline.SampleAt(arcs, alongM).PositionM - pointM).Length();
            if (apartM >= nearestM) continue;

            nearestM = apartM;
            other = at;
        }

        return nearestM;
    }

    /// <summary>How near a projection has to land to a line's end to be that end rather than its middle.</summary>
    const float EndM = 0.01f;

    /// <summary>What went wrong and where, so a sweep failure names the shape at fault rather than one point of it.</summary>
    static string Breakdown(SortedDictionary<string, (int Count, Vector2 First)> where) =>
        string.Join("; ", where.Select(row => $"{row.Value.Count}× {row.Key} (first at {row.Value.First})"));

    /// <summary>Every sample down one edge's own line, a cell apart, so nothing between two stations is missed.</summary>
    static IEnumerable<Vector2> Along(FootGraph foot, int edge, float cellSizeM)
    {
        var arcs = foot.ArcsOf(edge).ToArray();
        var lengthM = foot.LengthM(edge);
        var steps = Math.Max(2, (int)MathF.Ceiling(lengthM / cellSizeM));
        for (var step = 0; step <= steps; step++) yield return Spline.SampleAt(arcs, lengthM * step / steps).PositionM;
    }
}
