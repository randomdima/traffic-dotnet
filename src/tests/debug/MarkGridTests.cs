using System.Numerics;
using TrafficSimulation.App.Debug;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.Debug;

/// <summary>
/// Where the marks down a debug line stand. The whole claim is that the answer is about the ground and
/// not about the line: cut the same street anywhere, run it either way, come onto it out of a bend, and
/// the marks land on the same stones.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P9)]
public class MarkGridTests
{
    const float PitchM = 1.5f;

    /// <summary>Where a stretch of a chain is marked, in the world rather than along the chain.</summary>
    static List<Vector2> MarksOn(ReadOnlySpan<ArcSeg> arcs, float fromM, float toM)
    {
        var atM = new List<Vector2>();
        var grid = new MarkGrid(fromM, toM, PitchM);
        while (grid.MoveNext(arcs)) atM.Add(Spline.SampleAt(arcs, grid.AtM).PositionM);

        return atM;
    }

    static List<Vector2> MarksOn(ReadOnlySpan<ArcSeg> arcs) => MarksOn(arcs, 0f, Spline.TotalLengthM(arcs));

    /// <summary>A straight of an awkward bearing, laid from wherever it is asked for.</summary>
    static ArcSeg Straight(Vector2 fromM, float headingRad, float lengthM) =>
        new(fromM, headingRad, lengthM, 0f);

    /// <summary>How far one mark stands from the nearest of another line's, which is nothing where they agree.</summary>
    static float FurthestApartM(List<Vector2> some, List<Vector2> others)
    {
        var furthestM = 0f;
        foreach (var atM in some)
        {
            var nearestM = float.PositiveInfinity;
            foreach (var otherM in others) nearestM = MathF.Min(nearestM, Vector2.Distance(atM, otherM));

            furthestM = MathF.Max(furthestM, nearestM);
        }

        return furthestM;
    }

    /// <summary>
    /// The one property the grid exists for: a mark stands on a line of it, so every mark in the town is
    /// somewhere one of a whole number of pitches from the origin either across or down.
    /// </summary>
    [Theory]
    [InlineData(0.6f)]
    [InlineData(1.9f)]
    public void EveryMarkStandsWhereTheLineCrossesTheGrid(float headingRad)
    {
        var straight = Straight(new Vector2(103.4f, 41.9f), headingRad, 30f);
        var bend = new ArcSeg(straight.EndM, headingRad, 12f, 0.1f);

        // A crossing on a bend is read off the chord across one step, so it stands within that chord's own
        // sag of the line — a hundredth of a metre at the tightest bend a road is laid to, which is finer
        // than the line the mark sits on is drawn (PathMarks.SagPx).
        foreach (var atM in MarksOn([straight, bend]))
        {
            var across = MathF.Abs(MathF.IEEERemainder(atM.X, PitchM));
            var down = MathF.Abs(MathF.IEEERemainder(atM.Y, PitchM));
            Assert.True(MathF.Min(across, down) < 0.01f, $"the mark at {atM} stands on no line of the grid");
        }
    }

    /// <summary>Which is what makes a line's own start no part of where it is marked.</summary>
    [Theory]
    [InlineData(0.4f)]
    [InlineData(7.3f)]
    public void WhereALineStartsDoesNotMoveItsMarks(float cutM)
    {
        var whole = Straight(new Vector2(103.4f, 41.9f), 0.6f, 40f);
        var cut = Straight(whole.PointAtM(cutM), 0.6f, 40f - cutM);

        Assert.Equal(0f, FurthestApartM(MarksOn([cut]), MarksOn([whole])), 3);
    }

    /// <summary>And how much of a line is being drawn no part of it either.</summary>
    [Fact]
    public void HowMuchOfALineIsDrawnDoesNotMoveItsMarks()
    {
        var line = new[] { Straight(new Vector2(103.4f, 41.9f), 0.6f, 40f) };

        Assert.Equal(0f, FurthestApartM(MarksOn(line, 11.2f, 33.8f), MarksOn(line)), 3);
    }

    /// <summary>
    /// And a bend before a straight moves no mark on the straight, which is the reading a roundabout is
    /// looked at for: every movement running along a ring is marked on the ring's own stones rather than on
    /// a comb of its own that starts wherever that movement was cut.
    /// </summary>
    [Fact]
    public void ABendBeforeAStraightMovesNoMarkOnIt()
    {
        var turnM = 7.3f;
        var turn = new ArcSeg(new Vector2(103.4f, 41.9f), 0.6f - (MathF.PI * 0.5f), turnM, MathF.PI * 0.5f / turnM);
        var straight = Straight(turn.EndM, 0.6f, 40f);

        var afterTheBend = MarksOn([turn, straight], turnM, turnM + straight.LengthM);

        Assert.Equal(0f, FurthestApartM(afterTheBend, MarksOn([straight])), 3);
    }

    /// <summary>
    /// And the two directions of one line mark the same stones as each other, the grid being the ground's
    /// and not the traveller's. It is why a stretch walked both ways can be ticked rather than chevronned
    /// without the two passes fighting.
    /// </summary>
    [Fact]
    public void TheTwoDirectionsOfOneLineMarkTheSameStones()
    {
        var there = Straight(new Vector2(103.4f, 41.9f), 0.6f, 40f);
        var back = Straight(there.EndM, 0.6f - MathF.PI, 40f);

        Assert.Equal(0f, FurthestApartM(MarksOn([back]), MarksOn([there])), 3);
    }

    /// <summary>
    /// <b>No two marks stand on top of each other</b> — a line through a crossing of the grid meets two of
    /// its lines within a stroke, and two chevrons a finger apart read as a fault in the picture rather
    /// than as one mark. The bearing here is the one that walks a line straight through the crossings.
    /// </summary>
    [Fact]
    public void NoTwoMarksStandOnTopOfEachOther()
    {
        var diagonal = new[] { Straight(new Vector2(0f, 0f), MathF.PI * 0.25f, 60f) };

        var marks = MarksOn(diagonal);
        for (var mark = 1; mark < marks.Count; mark++)
        {
            Assert.True(
                Vector2.Distance(marks[mark], marks[mark - 1]) > PitchM * 0.2f,
                $"the marks at {marks[mark - 1]} and {marks[mark]} stand on top of each other");
        }
    }
}
