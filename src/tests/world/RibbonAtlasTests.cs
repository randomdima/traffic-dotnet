using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// The ribbon atlas on staged shapes: which ways it marks as sharing ground and where, and which ways a body
/// standing somewhere is found over (TER-4c.4, TER-5c).
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P1)]
public class RibbonAtlasTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    const float WidthM = 3.6f;

    const float LengthM = 40f;

    /// <summary>Ways laid from straights, each at the same width.</summary>
    sealed class Straights(params (Vector2 FromM, Vector2 ToM)[] lines) : IRibbonLines
    {
        readonly ArcSeg[][] _arcs = [.. lines.Select(static line =>
        {
            var along = line.ToM - line.FromM;
            return new[] { new ArcSeg(line.FromM, MathF.Atan2(along.Y, along.X), along.Length(), 0f) };
        })];

        public int WayCount => _arcs.Length;

        public ReadOnlySpan<ArcSeg> LineOf(int way, out float widthM)
        {
            widthM = WidthM;
            return _arcs[way];
        }
    }

    static RibbonAtlas Laid(params (Vector2 FromM, Vector2 ToM)[] lines) =>
        RibbonAtlas.Lay(new Straights(lines), Config.RibbonLatticeStepM, Config.RibbonTouchM);

    /// <summary>
    /// <b>Two ribbons that cross share one section on each</b>, filed under both, and each section is the
    /// crossing ribbon's width of its own way — to within the half step a lattice point stands for.
    /// </summary>
    [Fact]
    public void TwoCrossingWaysAreMarkedOnceOnEachOverTheOthersWidth()
    {
        var atlas = Laid(
            (new Vector2(0f, 0f), new Vector2(LengthM, 0f)),
            (new Vector2(LengthM * 0.5f, -LengthM * 0.5f), new Vector2(LengthM * 0.5f, LengthM * 0.5f)));
        var stepM = atlas.StepM;

        for (var way = 0; way < 2; way++)
        {
            var marks = atlas.Marks.Of(way);
            Assert.Single(marks.ToArray());
            Assert.Equal(1 - way, marks[0].OnWay);
            Assert.InRange(marks[0].MineFromM, (LengthM - WidthM) * 0.5f - stepM, (LengthM - WidthM) * 0.5f);
            Assert.InRange(marks[0].MineToM, (LengthM + WidthM) * 0.5f, (LengthM + WidthM) * 0.5f + stepM);
        }
    }

    /// <summary>
    /// <b>Ribbons laid edge to edge share an edge and no ground</b>: the two lanes of a carriageway, and a
    /// lane and the way that carries on from its end.
    /// </summary>
    [Fact]
    public void RibbonsThatOnlyTouchAreNotMarked()
    {
        var atlas = Laid(
            (new Vector2(0f, 0f), new Vector2(LengthM, 0f)),
            (new Vector2(LengthM, WidthM), new Vector2(0f, WidthM)),
            (new Vector2(LengthM, 0f), new Vector2(LengthM * 2f, 0f)));

        for (var way = 0; way < atlas.Marks.WayCount; way++) Assert.Empty(atlas.Marks.Of(way).ToArray());
    }

    /// <summary><b>A walk across a carriageway is marked against both of its lanes</b>, and the lanes against nothing but the walk.</summary>
    [Fact]
    public void AWayAcrossBothLanesIsMarkedAgainstEach()
    {
        var atlas = Laid(
            (new Vector2(0f, 0f), new Vector2(LengthM, 0f)),
            (new Vector2(LengthM, WidthM), new Vector2(0f, WidthM)),
            (new Vector2(LengthM * 0.5f, -WidthM), new Vector2(LengthM * 0.5f, WidthM * 2f)));

        var across = atlas.Marks.Of(2).ToArray();
        Assert.Equal([0, 1], across.Select(static section => section.OnWay).Order());
        Assert.Equal(2, atlas.Marks.Of(0)[0].OnWay);
        Assert.Single(atlas.Marks.Of(0).ToArray());
        Assert.Equal(2, atlas.Marks.Of(1)[0].OnWay);
        Assert.Single(atlas.Marks.Of(1).ToArray());
    }

    /// <summary>
    /// <b>A box inside one ribbon is on that way alone</b>, over the stretch of it the box covers to within
    /// a lattice step, and on nothing beside it.
    /// </summary>
    [Fact]
    public void ABoxInsideOneLaneIsOnThatLaneAlone()
    {
        var atlas = Laid(
            (new Vector2(0f, 0f), new Vector2(LengthM, 0f)),
            (new Vector2(LengthM, WidthM), new Vector2(0f, WidthM)));
        Span<WayCover> under = stackalloc WayCover[8];

        var count = atlas.UnderBox(new Vector2(20f, 0.3f), Vector2.UnitX, 2f, 1f, under);

        Assert.Equal(1, count);
        Assert.Equal(0, under[0].Way);
        Assert.InRange(under[0].FromM, 18f - atlas.StepM, 18f);
        Assert.InRange(under[0].ToM, 22f, 22f + atlas.StepM);
    }

    /// <summary>
    /// <b>A box over the line between two lanes is on both</b>, and a lane running the other way is measured
    /// in its own metres.
    /// </summary>
    [Fact]
    public void ABoxOverTheLineIsOnBothLanes()
    {
        var atlas = Laid(
            (new Vector2(0f, 0f), new Vector2(LengthM, 0f)),
            (new Vector2(LengthM, WidthM), new Vector2(0f, WidthM)));
        Span<WayCover> under = stackalloc WayCover[8];

        var count = atlas.UnderBox(new Vector2(10f, WidthM * 0.5f), Vector2.UnitX, 2f, 1f, under);

        Assert.Equal(2, count);
        var back = under[0].Way == 1 ? under[0] : under[1];
        Assert.InRange(back.FromM, LengthM - 12f - atlas.StepM, LengthM - 12f);
        Assert.InRange(back.ToM, LengthM - 8f, LengthM - 8f + atlas.StepM);
    }

    /// <summary>
    /// <b>A box is read by its own shape and never by the square round it</b>: a car centred in a lane laid
    /// at 45° to the lattice is on that lane alone, where the square round it reaches well into the next.
    /// </summary>
    [Fact]
    public void ACarOnADiagonalLaneIsOnThatLaneAlone()
    {
        var along = Vector2.Normalize(new Vector2(1f, 1f));
        var aside = Heading.RightOf(along) * WidthM;
        var atlas = Laid(
            (Vector2.Zero, along * LengthM),
            ((along * LengthM) + aside, aside));
        Span<WayCover> under = stackalloc WayCover[8];

        var count = atlas.UnderBox(along * (LengthM * 0.5f), along, 2f, 1f, under);

        Assert.Equal(1, count);
        Assert.Equal(0, under[0].Way);
    }
}
