using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// <b>That the pavement's inner edge is one line and not a run's worth of it at a time.</b> The edge is the
/// walk line half a walk to the road's side, and two offsets of a corner do not meet — so wherever two runs
/// give way to one another the edge has to be carried round the place they both stop at
/// (<see cref="Paving.Corners"/>), or the kerb is missing an L of itself two metres on a side.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P3)]
public class PavedCornerTests
{
    public static TheoryData<string> Maps => Towns.EveryMapWithAFootway();

    /// <summary>
    /// <b>A turn starts where a kerb ended, ends where the next one starts, and every arc of it starts where
    /// the arc before it ended.</b> Only the first point is laid off a run; every one after it falls out of a
    /// sweep and a radius, so a turn that swept the wrong way, too far or about the wrong place lands
    /// somewhere no kerb begins.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryTurnRunsFromOneRunsKerbToAnothers(string map)
    {
        var paving = Towns.Of(map).Paving(SimConfig.Shipped());
        var kerbs = KerbEndsOf(paving);

        foreach (var turn in paving.Corners)
        {
            Assert.True(
                Nearest(kerbs, turn[0].StartM) <= MetM,
                $"{map}: a turn sets off at {turn[0].StartM}, where no run's kerb ends");
            Assert.True(
                Nearest(kerbs, turn[^1].EndM) <= MetM,
                $"{map}: a turn arrives at {turn[^1].EndM}, where no run's kerb starts");

            for (var arc = 1; arc < turn.Length; arc++)
            {
                Assert.True(
                    (turn[arc].StartM - turn[arc - 1].EndM).Length() <= MetM,
                    $"{map}: a turn breaks between {turn[arc - 1].EndM} and {turn[arc].StartM}");
            }
        }
    }

    /// <summary>
    /// <b>And no turn goes more than half way round.</b> Two runs that give way make a wedge of tarmac
    /// between them and the turn crosses it, which is under half the circle however sharply they meet — so a
    /// turn that goes further is one laid between two kerbs that lie along one another, and what it draws is
    /// a ring of kerb standing on the pavement with nothing on either side of it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NoTurnGoesRightRound(string map)
    {
        var paving = Towns.Of(map).Paving(SimConfig.Shipped());

        foreach (var turn in paving.Corners)
        {
            var roundRad = 0f;
            foreach (var arc in turn) roundRad += arc.LengthM * MathF.Abs(arc.Curvature);

            Assert.True(
                roundRad < MathF.PI,
                $"{map}: a turn at {turn[0].StartM} goes {roundRad:F2} rad round the place it turns");
        }
    }

    /// <summary>
    /// <b>And no kerb end where two runs give way is left open.</b> The two runs stop at one place and their
    /// kerbs half a walk from it, as much as a walk apart from each other — so unless the two lie along one
    /// another, something has to carry the edge from the one to the other.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryKerbThatGivesWayIsCarriedRound(string map)
    {
        var paving = Towns.Of(map).Paving(SimConfig.Shipped());
        var kerbs = KerbEndsOf(paving);
        var places = new List<Vector2>();
        foreach (var run in paving.Walk)
        {
            places.Add(run.Line[0].StartM);
            places.Add(run.Line[^1].EndM);
        }

        var turnEnds = new List<Vector2>();
        foreach (var turn in paving.Corners)
        {
            turnEnds.Add(turn[0].StartM);
            turnEnds.Add(turn[^1].EndM);
        }

        for (var end = 0; end < kerbs.Count; end++)
        {
            // A run shorter than the band is wide is a sliver the wrap left between two pieces that all but
            // touch, and a cluster of them puts nine ends inside a foot of one another. What its own kerb
            // does is nothing anybody can see, and it is not what this claim is about.
            if (paving.Walk[end / 2].LengthM < paving.WalkM) continue;

            var others = 0;
            foreach (var place in places)
            {
                if ((place - places[end]).Length() <= OnePlaceM) others++;
            }

            // Its own place, and any run that gives way to it there.
            if (others < 2) continue;

            Assert.True(
                Nearest(turnEnds, kerbs[end]) <= MetM || Other(kerbs, end) <= OnePlaceM,
                $"{map}: the kerb stops dead at {kerbs[end]}, where {others} runs give way at {places[end]}");
        }
    }

    /// <summary>
    /// Where the pavement's inner edge stops on each run: half a walk to the road's side of both ends of the
    /// line, which is the offset the kerb line is struck at (<c>App.Render.GroundMesh</c>).
    /// </summary>
    static List<Vector2> KerbEndsOf(Paving paving)
    {
        var halfWalkM = paving.WalkM * 0.5f;
        var ends = new List<Vector2>(paving.Walk.Length * 2);
        foreach (var run in paving.Walk)
        {
            var head = run.Line[0];
            var tail = run.Line[^1];
            ends.Add(head.StartM + (Heading.RightOf(head.StartUnit) * run.RoadSide * halfWalkM));
            ends.Add(tail.EndM
                     + (Heading.RightOf(Heading.Unit(tail.HeadingAtRad(tail.LengthM))) * run.RoadSide * halfWalkM));
        }

        return ends;
    }

    /// <summary>How near the nearest other kerb end stands to this one — nought where two runs lie along one another.</summary>
    static float Other(List<Vector2> points, int end)
    {
        var nearestM = float.MaxValue;
        for (var other = 0; other < points.Count; other++)
        {
            if (other != end) nearestM = MathF.Min(nearestM, (points[other] - points[end]).Length());
        }

        return nearestM;
    }

    static float Nearest(List<Vector2> points, Vector2 atM)
    {
        var nearestM = float.MaxValue;
        foreach (var point in points) nearestM = MathF.Min(nearestM, (point - atM).Length());
        return nearestM;
    }

    /// <summary>
    /// <b>How near a turn has to land</b>: its two ends are laid on the two kerb ends themselves, so what
    /// separates them is the arithmetic of an arc and nothing else.
    /// </summary>
    const float MetM = Kerbs.RoundingM * 10f;

    /// <summary>And how far apart the two ends of one place stand (<see cref="Kerbs.OnePlaceM"/>).</summary>
    const float OnePlaceM = Kerbs.OnePlaceM;
}
