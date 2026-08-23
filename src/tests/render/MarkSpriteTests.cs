using System.Numerics;
using TrafficSimulation.App.Render;
using TrafficSimulation.World.Terrain;
using Xunit;

namespace TrafficSimulation.Tests.Render;

/// <summary>
/// The record the traffic leaves on the ground, as instances: what kind of mark it is decides which
/// brush it is stamped through and what colour it is laid in, and how hard the tyre was working decides
/// nothing but its strength.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class MarkSpriteTests
{
    const int Rubber = 3;
    const int Soil = 4;

    static DriftMarks Marks(int capacity = 8) => new(capacity);

    static SpriteInstance[] Drawn(DriftMarks marks, Vector2 viewCentreM, float viewSpanM = 1_000f)
    {
        var into = new SpriteInstance[marks.Count];
        var written = MarkSprites.Fill(marks, Rubber, Soil, viewCentreM, new Vector2(viewSpanM), into);
        return into[..written];
    }

    /// <summary>A mark spans the wheel's travel, at the width of the tyre that made it and along the way it went.</summary>
    [Fact]
    public void AMarkIsTheStretchOfGroundTheWheelDraggedOver()
    {
        var marks = Marks();
        marks.Mark(new Vector2(10f, 4f), new Vector2(14f, 4f), widthM: 0.22f, intensity: 1f, ploughed: false);

        var drawn = Drawn(marks, new Vector2(12f, 4f));

        Assert.Single(drawn);
        Assert.Equal(new Vector2(12f, 4f), drawn[0].CentreM);
        Assert.Equal(2f, drawn[0].HalfSizeM.X, 1e-4f);
        Assert.Equal(0.11f, drawn[0].HalfSizeM.Y, 1e-4f);
        Assert.Equal(0f, drawn[0].HeadingRad, 1e-5f);
    }

    /// <summary>
    /// Rubber is laid black over a road that is still there and soil is laid over a lawn that is not,
    /// each through its own brush — the two kinds differ in what happened to the ground and not in
    /// degree.
    /// </summary>
    [Fact]
    public void TheTwoKindsAreDrawnThroughTheirOwnBrushes()
    {
        var marks = Marks();
        marks.Mark(Vector2.Zero, new Vector2(1f, 0f), 0.22f, intensity: 1f, ploughed: false);
        marks.Mark(new Vector2(0f, 5f), new Vector2(1f, 5f), 0.22f, intensity: 1f, ploughed: true);

        var drawn = Drawn(marks, new Vector2(0.5f, 2.5f));

        Assert.Equal((uint)Rubber, drawn[0].Sheet);
        Assert.Equal(Vector3.Zero, new Vector3(drawn[0].Tint.X, drawn[0].Tint.Y, drawn[0].Tint.Z));

        Assert.Equal((uint)Soil, drawn[1].Sheet);
        Assert.True(drawn[1].Tint.X > drawn[1].Tint.Z, "a rut is soil-coloured rather than a darker green");
        Assert.True(drawn[1].HalfSizeM.Y > drawn[0].HalfSizeM.Y, "a rut's soft edge sits outside the width that made it");
    }

    /// <summary>How hard the tyre worked the ground is the mark's opacity and nothing else, so a corner taken twice is darker than one taken once.</summary>
    [Fact]
    public void HowHardTheTyreWorkedIsTheMarksStrength()
    {
        var marks = Marks();
        marks.Mark(Vector2.Zero, new Vector2(1f, 0f), 0.22f, intensity: 1f, ploughed: false);
        marks.Mark(new Vector2(0f, 1f), new Vector2(1f, 1f), 0.22f, intensity: 0.25f, ploughed: false);

        var drawn = Drawn(marks, new Vector2(0.5f, 0.5f));

        Assert.Equal(drawn[0].Tint.W * 0.25f, drawn[1].Tint.W, 1e-5f);
        Assert.InRange(drawn[0].Tint.W, 0f, 1f);
    }

    /// <summary>Nothing is laid for a wheel that has not moved, or for one that was not working the ground at all.</summary>
    [Fact]
    public void AMarkNobodyMadeIsNotRecorded()
    {
        var marks = Marks();
        marks.Mark(Vector2.Zero, Vector2.Zero, 0.22f, 1f, false);
        marks.Mark(Vector2.Zero, new Vector2(1f, 0f), 0.22f, intensity: 0f, ploughed: false);
        marks.Mark(Vector2.Zero, new Vector2(1f, 0f), widthM: 0f, intensity: 1f, ploughed: false);

        Assert.Equal(0, marks.Count);
    }

    /// <summary>
    /// The ring holds the town's whole history of marks and the oldest is overwritten once it is full,
    /// which is the only sense in which a permanent mark has a limit.
    /// </summary>
    [Fact]
    public void TheOldestMarkIsWhatAFullRingGivesUp()
    {
        var marks = Marks(capacity: 2);
        for (var mark = 0; mark < 5; mark++)
        {
            marks.Mark(new Vector2(mark, 0f), new Vector2(mark + 1f, 0f), 0.22f, 1f, false);
        }

        Assert.Equal(2, marks.Count);

        // The two most recent, and nothing of the three the ring has given up.
        var drawn = Drawn(marks, new Vector2(4f, 0f));
        Assert.Equal(2, drawn.Length);
        Assert.Equal([4.5f, 3.5f], [drawn[0].CentreM.X, drawn[1].CentreM.X]);
    }

    /// <summary>A mark off the screen is not written into the buffer, the same cull every body gets.</summary>
    [Fact]
    public void AMarkNobodyCanSeeIsNotDrawn()
    {
        var marks = Marks();
        marks.Mark(new Vector2(500f, 0f), new Vector2(501f, 0f), 0.22f, 1f, false);

        Assert.Empty(Drawn(marks, Vector2.Zero, viewSpanM: 20f));
    }
}
