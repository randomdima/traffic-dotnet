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

    /// <summary>How near an end of a section has to come to where the two ribbons stop touching.</summary>
    const float ExactM = 1e-3f;

    /// <summary>Ways laid from pieces, each at the same width — every one of them driven and none of them paint, unless said.</summary>
    sealed class Lines(params ArcSeg[][] lines) : IRibbonLines
    {
        /// <summary>The zebra each way paints, by way; a way past the end of it paints none.</summary>
        public int[] Zebras { get; init; } = [];

        /// <summary>Where the walked ways begin.</summary>
        public int FirstWalked { get; init; } = int.MaxValue;

        public int WayCount => lines.Length;

        public ReadOnlySpan<ArcSeg> LineOf(int way, out float widthM)
        {
            widthM = WidthM;
            return lines[way];
        }

        public int ZebraOf(int way) => way < Zebras.Length ? Zebras[way] : RibbonMarks.NoZebra;

        public bool IsDriven(int way) => way < FirstWalked;
    }

    static ArcSeg[] Straight(Vector2 fromM, Vector2 toM)
    {
        var along = toM - fromM;
        return [new ArcSeg(fromM, MathF.Atan2(along.Y, along.X), along.Length(), 0f)];
    }

    static RibbonAtlas Laid(params ArcSeg[][] lines) =>
        RibbonAtlas.Lay(new Lines(lines), Config.RibbonLevel, Config.RibbonTouchM);

    static RibbonAtlas Laid(params (Vector2 FromM, Vector2 ToM)[] lines) =>
        Laid([.. lines.Select(static line => Straight(line.FromM, line.ToM))]);

    /// <summary>
    /// <b>Two ribbons that cross share one section on each</b>, filed under both, and each section is exactly
    /// the crossing ribbon's width of its own way, less the touch at either side.
    /// </summary>
    [Fact]
    public void TwoCrossingWaysAreMarkedOnceOnEachOverTheOthersWidth()
    {
        var atlas = Laid(
            (new Vector2(0f, 0f), new Vector2(LengthM, 0f)),
            (new Vector2(LengthM * 0.5f, -LengthM * 0.5f), new Vector2(LengthM * 0.5f, LengthM * 0.5f)));
        var sharedM = WidthM - (2f * Config.RibbonTouchM);

        for (var way = 0; way < 2; way++)
        {
            var marks = atlas.Marks.Of(way);
            Assert.Single(marks.ToArray());
            Assert.Equal(1 - way, marks[0].OnWay);
            Assert.Equal((LengthM - sharedM) * 0.5f, marks[0].MineFromM, ExactM);
            Assert.Equal((LengthM + sharedM) * 0.5f, marks[0].MineToM, ExactM);
        }
    }

    /// <summary>
    /// <b>Two ways out of one mouth are marked to exactly where their ribbons part</b> (TER-5c), each worn back
    /// by the touch: a straight and a turn of radius R leave it together, and the turn's outer edge leaves the
    /// straight's far edge at the angle whose cosine is (R − h)/(R + h) round the turn — 2√(Rh) down the
    /// straight — for h the half-width less the touch.
    /// </summary>
    [Fact]
    public void TwoWaysOutOfOneMouthAreMarkedToWhereTheirRibbonsPart()
    {
        const float RadiusM = 12f;
        var touchM = Config.RibbonTouchM;
        var halfM = (WidthM * 0.5f) - touchM;
        var atlas = Laid(
            Straight(Vector2.Zero, new Vector2(LengthM, 0f)),
            [new ArcSeg(Vector2.Zero, 0f, RadiusM * MathF.PI * 0.5f, 1f / RadiusM)]);

        var mark = Assert.Single(atlas.Marks.Of(0).ToArray());
        Assert.Equal(touchM, mark.MineFromM, ExactM);
        Assert.Equal(2f * MathF.Sqrt(RadiusM * halfM), mark.MineToM, ExactM);
        Assert.Equal(touchM, mark.FromM, ExactM);
        Assert.Equal(RadiusM * MathF.Acos((RadiusM - halfM) / (RadiusM + halfM)), mark.ToM, ExactM);
    }

    /// <summary>How far apart two lines stand whose ribbons meet edge to edge.</summary>
    const float EdgeToEdgeM = WidthM;

    /// <summary>
    /// <b>Ribbons laid edge to edge share an edge and no ground</b>: the two lanes of a carriageway, and a
    /// lane and the way that carries on from its end.
    /// </summary>
    [Fact]
    public void RibbonsThatOnlyTouchAreNotMarked()
    {
        var atlas = Laid(
            (new Vector2(0f, 0f), new Vector2(LengthM, 0f)),
            (new Vector2(LengthM, EdgeToEdgeM), new Vector2(0f, EdgeToEdgeM)),
            (new Vector2(LengthM, 0f), new Vector2(LengthM * 2f, 0f)));

        for (var way = 0; way < atlas.Marks.WayCount; way++) Assert.Empty(atlas.Marks.Of(way).ToArray());
    }

    /// <summary><b>A walk across a carriageway is marked against both of its lanes</b>, and the lanes against nothing but the walk.</summary>
    [Fact]
    public void AWayAcrossBothLanesIsMarkedAgainstEach()
    {
        var atlas = Laid(
            (new Vector2(0f, 0f), new Vector2(LengthM, 0f)),
            (new Vector2(LengthM, EdgeToEdgeM), new Vector2(0f, EdgeToEdgeM)),
            (new Vector2(LengthM * 0.5f, -WidthM), new Vector2(LengthM * 0.5f, WidthM * 2f)));

        var across = atlas.Marks.Of(2).ToArray();
        Assert.Equal([0, 1], across.Select(static section => section.OnWay).Order());
        Assert.Equal(2, atlas.Marks.Of(0)[0].OnWay);
        Assert.Single(atlas.Marks.Of(0).ToArray());
        Assert.Equal(2, atlas.Marks.Of(1)[0].OnWay);
        Assert.Single(atlas.Marks.Of(1).ToArray());
    }

    /// <summary>
    /// <b>A zebra is marked whole</b> (TER-5c.3): each of its two walking lanes, a band apart, against both
    /// lanes of the carriageway — all of the walking lane against the whole of what the zebra covers of each
    /// lane, from the near edge of the one to the far edge of the other.
    /// </summary>
    [Fact]
    public void AZebrasWalkingLanesAndTheLanesUnderItAreMarkedWhole()
    {
        const float AtM = LengthM * 0.5f;
        var lines = new Lines(
            Straight(new Vector2(0f, 0f), new Vector2(LengthM, 0f)),
            Straight(new Vector2(LengthM, EdgeToEdgeM), new Vector2(0f, EdgeToEdgeM)),
            Straight(new Vector2(AtM, -WidthM), new Vector2(AtM, WidthM * 2f)),
            Straight(new Vector2(AtM + EdgeToEdgeM, WidthM * 2f), new Vector2(AtM + EdgeToEdgeM, -WidthM)))
        {
            Zebras = [RibbonMarks.NoZebra, RibbonMarks.NoZebra, 0, 0], FirstWalked = 2,
        };
        var marks = RibbonAtlas.Lay(lines, Config.RibbonLevel, Config.RibbonTouchM).Marks;

        var paintM = WidthM * 3f;
        var nearM = AtM - (WidthM * 0.5f) + Config.RibbonTouchM;
        var farM = AtM + EdgeToEdgeM + (WidthM * 0.5f) - Config.RibbonTouchM;
        (float FromM, float ToM)[] under = [(nearM, farM), (LengthM - farM, LengthM - nearM)];

        for (var lane = 0; lane < 2; lane++)
        {
            var sections = marks.Of(lane).ToArray();
            Assert.Equal([2, 3], sections.Select(static section => section.OnWay));
            foreach (var section in sections)
            {
                Assert.Equal(0f, section.FromM);
                Assert.Equal(paintM, section.ToM, ExactM);
                Assert.Equal(under[lane].FromM, section.MineFromM, ExactM);
                Assert.Equal(under[lane].ToM, section.MineToM, ExactM);
            }
        }

        for (var paint = 2; paint < 4; paint++)
        {
            var sections = marks.Of(paint).ToArray();
            Assert.Equal([0, 1], sections.Select(static section => section.OnWay));
            foreach (var section in sections)
            {
                Assert.Equal(0f, section.MineFromM);
                Assert.Equal(paintM, section.MineToM, ExactM);
                Assert.Equal(under[section.OnWay].FromM, section.FromM, ExactM);
                Assert.Equal(under[section.OnWay].ToM, section.ToM, ExactM);
            }
        }
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
    /// <b>A box up to the edge of the next lane's ribbon is not on it</b> (TER-4c.2): the lattice files points
    /// a reach past every band, and a body is read over only the ground it and the band both hold.
    /// </summary>
    [Fact]
    public void ABoxUpToTheNextLanesEdgeIsNotOnIt()
    {
        const float HalfWidthM = 1f;
        var atlas = Laid(
            (new Vector2(0f, 0f), new Vector2(LengthM, 0f)),
            (new Vector2(LengthM, WidthM), new Vector2(0f, WidthM)));
        Span<WayCover> under = stackalloc WayCover[8];

        var count = atlas.UnderBox(
            new Vector2(20f, (WidthM * 0.5f) - HalfWidthM), Vector2.UnitX, 2f, HalfWidthM, under);

        Assert.Equal(1, count);
        Assert.Equal(0, under[0].Way);
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

    /// <summary>How many poses a recalled body is asked at.</summary>
    const int Poses = 4000;

    /// <summary>
    /// The pose a recalled body is asked at: a shuffle of a tenth of a millimetre and a tenth of a milliradian — a
    /// parked car's — on a drift of a millimetre a pose, a quarter of it across the lanes, and a turn. So it is
    /// kept for runs of poses, and read afresh as the drift takes a point over an edge or a band's reach.
    /// </summary>
    static (Vector2 CentreM, Vector2 Forward) PoseAt(int pose)
    {
        var shuffle = MathF.Sin(pose * 0.7f) * 1e-4f;
        var centreM = new Vector2(10f + (pose * 1e-3f) + shuffle, 0.3f + (pose * 2.5e-4f) - shuffle);
        return (centreM, Heading.Unit((pose * 5e-5f) + shuffle));
    }

    /// <summary>
    /// <b>A car kept in a recall is found over exactly what a fresh read finds</b>, at every pose of a shuffle
    /// and a drift that carries it from inside one lane across the line and over both.
    /// </summary>
    [Fact]
    public void ABoxKeptInARecallIsFoundOverWhatAFreshReadFinds()
    {
        var atlas = Laid(
            (new Vector2(0f, 0f), new Vector2(LengthM, 0f)),
            (new Vector2(LengthM, WidthM), new Vector2(0f, WidthM)));
        var recall = new RibbonAtlas.Recall(1);
        Span<WayCover> fresh = stackalloc WayCover[RibbonAtlas.MostWaysUnderABody];

        for (var pose = 0; pose < Poses; pose++)
        {
            var (centreM, forward) = PoseAt(pose);
            var count = atlas.UnderBox(centreM, forward, 2f, 1f, fresh);

            Assert.Equal(fresh[..count].ToArray(), atlas.UnderBox(centreM, forward, 2f, 1f, recall, 0).ToArray());
        }
    }

    /// <summary><b>The same for a walker</b>, whose disc is narrower than a lattice step.</summary>
    [Fact]
    public void ADiscKeptInARecallIsFoundOverWhatAFreshReadFinds()
    {
        const float RadiusM = 0.3f;
        var atlas = Laid(
            (new Vector2(0f, 0f), new Vector2(LengthM, 0f)),
            (new Vector2(LengthM, WidthM), new Vector2(0f, WidthM)));
        var recall = new RibbonAtlas.Recall(1);
        Span<WayCover> fresh = stackalloc WayCover[RibbonAtlas.MostWaysUnderABody];

        for (var pose = 0; pose < Poses; pose++)
        {
            var (centreM, _) = PoseAt(pose);
            var count = atlas.UnderDisc(centreM, RadiusM, fresh);

            Assert.Equal(fresh[..count].ToArray(), atlas.UnderDisc(centreM, RadiusM, recall, 0).ToArray());
        }
    }
}
