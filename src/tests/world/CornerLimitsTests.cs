using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// <b>What each arc of a line may be entered at</b> (S-2), laid once with the line: a bend is braked for on the
/// arcs before it, and nothing else bounds a straight.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P2)]
public class CornerLimitsTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static float[] Entries(params ArcSeg[] arcs)
    {
        var entryM = new float[arcs.Length];
        CornerLimits.Lay(arcs, entryM, Config);
        return entryM;
    }

    static ArcSeg Straight(float lengthM) => new(Vector2.Zero, 0f, lengthM, 0f);

    static ArcSeg Bend(float lengthM, float radiusM) => new(Vector2.Zero, 0f, lengthM, 1f / radiusM);

    /// <summary>A straight with nothing past it bounds nothing: the end of the line is a stop point of its own.</summary>
    [Fact]
    public void AStraightWithNothingPastItBoundsNothing()
    {
        Assert.All(Entries(Straight(50f), Straight(80f)), entryM => Assert.True(float.IsPositiveInfinity(entryM)));
    }

    /// <summary>
    /// <b>A bend is braked for on the straight before it</b>: the straight may be entered faster than the bend, and
    /// the further off the bend, the faster.
    /// </summary>
    [Fact]
    public void ABendIsBrakedForOnTheStraightBeforeIt()
    {
        var near = Entries(Straight(30f), Bend(20f, 15f));
        var far = Entries(Straight(90f), Bend(20f, 15f));

        Assert.True(float.IsFinite(near[0]), "a straight before a bend is bounded by it");
        Assert.True(near[0] > near[1], "and may be entered faster than the bend itself");
        Assert.True(far[0] > near[0], "and faster the further off the bend is");
    }

    /// <summary>
    /// <b>A tighter bend past a gentler one is seen through it</b>: the gentle arc may be entered at no more than
    /// braking into the tight one allows, which is less than it bounds on its own.
    /// </summary>
    [Fact]
    public void ATighterBendPastAGentlerOneIsSeenThroughIt()
    {
        var alone = Entries(Bend(10f, 100f));
        var beforeATighter = Entries(Bend(10f, 100f), Bend(20f, 10f));

        Assert.True(beforeATighter[0] < alone[0]);
    }

    /// <summary>
    /// <b>Where the corners let a car be at rest only moves on as the car does</b> (TER-4c.1): read from every metre
    /// of a line that brakes into a tight bend, eases through a gentler one and runs out on a straight, it never
    /// draws back.
    /// </summary>
    [Fact]
    public void WhereTheCornersLetACarRestOnlyMovesOnAsTheCarDoes()
    {
        ArcSeg[] line = [Straight(60f), Bend(12f, 8f), Straight(20f), Bend(30f, 40f), Bend(10f, 12f), Straight(80f)];
        var entryM = Entries(line);

        var restM = CornerLimits.RestToM(line, entryM, 0f, Config);
        for (var fromM = 0.25f; fromM < 212f; fromM += 0.25f)
        {
            var nextM = CornerLimits.RestToM(line, entryM, fromM, Config);
            Assert.True(nextM >= restM - Rounding, $"from {fromM:0.00} m the corners let it rest at {nextM:0.00} m, short of {restM:0.00} m");
            restM = nextM;
        }
    }

    /// <summary>
    /// <b>A tight bend ahead lets a car be at rest no further than the bend itself</b>, however far off it is; once
    /// the car is in it, the straight past it bounds nothing.
    /// </summary>
    [Fact]
    public void ATightBendAheadLetsACarRestNoFurtherThanTheBend()
    {
        ArcSeg[] line = [Straight(60f), Bend(12f, 8f), Straight(80f)];
        var entryM = Entries(line);

        var beforeM = CornerLimits.RestToM(line, entryM, 10f, Config);
        var withinM = CornerLimits.RestToM(line, entryM, 65f, Config);

        Assert.InRange(beforeM, 60f, 72f);
        Assert.True(float.IsPositiveInfinity(withinM));
    }

    /// <summary>Where an arc ends, the rest read either side of it is one figure summed two ways: what differs is the float.</summary>
    const float Rounding = 1e-3f;
}
