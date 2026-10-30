using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Terrain;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// The record the traffic leaves on the ground, as strokes of knots (<see cref="DriftMarks"/>): a wheel's stretches carry
/// its stroke on, and a knot is left behind only where the stroke would stray from the one before.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P5)]
public class DriftMarkTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    const float WidthM = 0.22f;

    static DriftMarks Marks(int capacity = 64, int wheels = 2) => new(capacity, wheels, StrokeFit.Of(Config));

    /// <summary>A wheel's stroke laid along <paramref name="pointsM"/>, a stretch between each two.</summary>
    static void Drive(DriftMarks marks, int wheel, ReadOnlySpan<Vector2> pointsM, float intensity = 1f, bool ploughed = false)
    {
        for (var at = 1; at < pointsM.Length; at++) marks.Lay(wheel, pointsM[at - 1], pointsM[at], WidthM, intensity, ploughed);
    }

    static Vector2[] Straight(Vector2 fromM, Vector2 stepM, int stretches)
    {
        var points = new Vector2[stretches + 1];
        for (var at = 0; at <= stretches; at++) points[at] = fromM + (stepM * at);
        return points;
    }

    /// <summary>The knots of the stroke ending in the ring's youngest knot, first to last.</summary>
    static List<DriftKnot> StrokeEndingAt(DriftMarks marks, int slot)
    {
        var stroke = new List<DriftKnot>();
        for (var knot = marks.At(slot); ; knot = marks.At((int)(knot.Previous % (uint)marks.Capacity)))
        {
            stroke.Insert(0, knot);
            if (knot.Previous == DriftMarks.NoKnot) return stroke;
        }
    }

    /// <summary>
    /// <b>A straight skid shorter than a knot's most is two knots</b>, however many stretches the wheel laid it in: the
    /// last moves with the wheel, and stands where the wheel last was.
    /// </summary>
    [Fact]
    public void AStraightSkidIsTwoKnotsHoweverManyStretchesLaidIt()
    {
        var marks = Marks();
        var stretches = (int)(Config.Marks.KnotMostM / Config.Marks.SpacingM) - 1;
        var points = Straight(new Vector2(10f, 4f), new Vector2(Config.Marks.SpacingM, 0f), stretches);

        Drive(marks, 0, points);

        Assert.Equal(2, marks.Count);
        Assert.Equal(points[0], marks.At(0).AtM);
        Assert.Equal(points[^1], marks.At(1).AtM);
        Assert.Equal(0u, marks.At(1).Previous);
    }

    /// <summary>No two knots of a stroke stand further apart than a knot's most, however straight it runs.</summary>
    [Fact]
    public void AStrokeLeavesAKnotEveryMostItRuns()
    {
        var marks = Marks();
        Drive(marks, 0, Straight(Vector2.Zero, new Vector2(Config.Marks.SpacingM, 0f), 100));

        var stroke = StrokeEndingAt(marks, marks.Count - 1);
        for (var at = 1; at < stroke.Count; at++)
        {
            Assert.InRange(Vector2.Distance(stroke[at - 1].AtM, stroke[at].AtM), 0f, Config.Marks.KnotMostM);
        }
    }

    /// <summary>
    /// <b>A corner leaves a knot each time it has turned through the most</b>: no two knots of a stroke round a circle
    /// point further apart than <see cref="MarkFigures.KnotTurnDeg"/>.
    /// </summary>
    [Fact]
    public void ACornerLeavesAKnotEachTurnThroughTheMost()
    {
        var marks = Marks();
        const float RadiusM = 4f;
        var stepRad = Config.Marks.SpacingM / RadiusM;
        var points = new Vector2[(int)(MathF.PI / stepRad) + 1];
        for (var at = 0; at < points.Length; at++) points[at] = new Vector2(MathF.Cos(at * stepRad), MathF.Sin(at * stepRad)) * RadiusM;

        Drive(marks, 0, points);

        var stroke = StrokeEndingAt(marks, marks.Count - 1);
        for (var at = 1; at < stroke.Count; at++)
        {
            Assert.True(
                MathF.Cos(stroke[at].HeadingRad - stroke[at - 1].HeadingRad) >= Config.MarkKnotTurnCos,
                $"knots {at - 1} and {at} turn past the most");
        }
    }

    /// <summary>A stroke grown darker than a knot's step leaves a knot, so its strength runs straight between two.</summary>
    [Fact]
    public void AStrokeGrownDarkerLeavesAKnot()
    {
        var marks = Marks();
        var step = new Vector2(Config.Marks.SpacingM, 0f);
        marks.Lay(0, Vector2.Zero, step, WidthM, 0.2f, ploughed: false);
        marks.Lay(0, step, step * 2f, WidthM, 0.2f + (Config.Marks.KnotStrengthStep * 2f), ploughed: false);

        Assert.Equal(3, marks.Count);
    }

    /// <summary>A stretch that does not carry on from its wheel's last knot begins a stroke of its own.</summary>
    [Fact]
    public void AStretchElsewhereBeginsAStroke()
    {
        var marks = Marks();
        marks.Lay(0, Vector2.Zero, new Vector2(1f, 0f), WidthM, 1f, ploughed: false);
        marks.Lay(0, new Vector2(5f, 5f), new Vector2(6f, 5f), WidthM, 1f, ploughed: false);

        Assert.Equal(4, marks.Count);
        Assert.Equal(DriftMarks.NoKnot, marks.At(2).Previous);
    }

    /// <summary>Ploughing on from where rubber was laid is a stroke of another kind, and begins one.</summary>
    [Fact]
    public void AnotherKindBeginsAStroke()
    {
        var marks = Marks();
        marks.Lay(0, Vector2.Zero, new Vector2(1f, 0f), WidthM, 1f, ploughed: false);
        marks.Lay(0, new Vector2(1f, 0f), new Vector2(2f, 0f), WidthM, 1f, ploughed: true);

        Assert.Equal(DriftMarks.NoKnot, marks.At(2).Previous);
        Assert.True(marks.At(3).Ploughed);
    }

    /// <summary>
    /// <b>Driving on from the ground onto the level above begins a stroke there</b>, its knots raised and as wide as the
    /// tyre, so each level's marks are drawn with it.
    /// </summary>
    [Fact]
    public void AnotherLevelBeginsAStrokeOfItsOwn()
    {
        var marks = Marks();
        marks.Lay(0, Vector2.Zero, new Vector2(1f, 0f), WidthM, 1f, ploughed: false);
        marks.Lay(0, new Vector2(1f, 0f), new Vector2(2f, 0f), WidthM, 1f, ploughed: false, raised: true);

        Assert.Equal((DriftMarks.NoKnot, true, WidthM), (marks.At(2).Previous, marks.At(3).Raised, MathF.Abs(marks.At(3).WidthM)));
    }

    /// <summary>Two wheels laying at once each carry their own stroke on, whatever order their stretches come in.</summary>
    [Fact]
    public void EachWheelCarriesItsOwnStroke()
    {
        var marks = Marks();
        var step = new Vector2(Config.Marks.SpacingM, 0f);
        var other = new Vector2(0f, 2f);
        for (var at = 0; at < 10; at++)
        {
            marks.Lay(0, step * at, step * (at + 1), WidthM, 1f, ploughed: false);
            marks.Lay(1, other + (step * at), other + (step * (at + 1)), WidthM, 1f, ploughed: false);
        }

        Assert.Equal(4, marks.Count);
    }

    /// <summary>Nothing is laid for a wheel that has not moved, or for one that was not working the ground at all.</summary>
    [Fact]
    public void AMarkNobodyMadeIsNotRecorded()
    {
        var marks = Marks();
        marks.Lay(0, Vector2.Zero, Vector2.Zero, WidthM, 1f, false);
        marks.Lay(0, Vector2.Zero, new Vector2(1f, 0f), WidthM, intensity: 0f, ploughed: false);
        marks.Lay(0, Vector2.Zero, new Vector2(1f, 0f), widthM: 0f, intensity: 1f, ploughed: false);

        Assert.Equal(0, marks.Count);
    }

    /// <summary>
    /// <b>A copy takes only what was written since it last looked</b> — a stroke's last knot moved is the one place it
    /// takes — and the place's knot as it now stands.
    /// </summary>
    [Fact]
    public void ACopyTakesOnlyWhatWasWrittenSinceItLooked()
    {
        var marks = Marks();
        var into = new DriftKnot[marks.Capacity];
        var copied = 0L;
        var step = new Vector2(Config.Marks.SpacingM, 0f);
        marks.Lay(0, Vector2.Zero, step, WidthM, 1f, ploughed: false);
        marks.CopyChangedInto(into, ref copied);

        marks.Lay(0, step, step * 2f, WidthM, 1f, ploughed: false);
        var (from, to) = marks.CopyChangedInto(into, ref copied);

        Assert.Equal((1, 2), (from, to));
        Assert.Equal(marks.At(1), into[1]);
    }

    /// <summary>
    /// The ring holds the town's whole history of knots and the oldest is overwritten once it is full, which is the only
    /// sense in which a permanent mark has a limit — and a stroke whose knots it gave up begins again.
    /// </summary>
    [Fact]
    public void TheOldestKnotIsWhatAFullRingGivesUp()
    {
        var marks = Marks(capacity: 2, wheels: 1);
        for (var stroke = 0; stroke < 3; stroke++)
        {
            marks.Lay(0, new Vector2(0f, stroke * 10f), new Vector2(1f, stroke * 10f), WidthM, 1f, false);
        }

        Assert.Equal(2, marks.Count);
        Assert.Equal(6, marks.Total);
        Assert.Equal(new Vector2(1f, 20f), marks.At((int)((marks.Total - 1) % marks.Capacity)).AtM);
    }
}
