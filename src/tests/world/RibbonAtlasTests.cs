using System.Numerics;
using TrafficSimulation.CityGen;
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

        /// <summary>The channels each way is on, by way; a way past the end of it is on the ground's.</summary>
        public byte[] Channels { get; init; } = [];

        public int WayCount => lines.Length;

        public ReadOnlySpan<ArcSeg> LineOf(int way, out float widthM)
        {
            widthM = WidthM;
            return lines[way];
        }

        public int ZebraOf(int way) => way < Zebras.Length ? Zebras[way] : RibbonMarks.NoZebra;

        public bool IsDriven(int way) => way < FirstWalked;

        public byte ChannelsOf(int way) => way < Channels.Length ? Channels[way] : Ground;
    }

    const byte Ground = CityPlan.RoadArrays.GroundChannel;

    static readonly byte Over = CityPlan.RoadArrays.ChannelOf(CityPlan.RoadArrays.Over);

    /// <summary>A bridgehead's: the bridge's channel and the ground's.</summary>
    static readonly byte Both = (byte)(Ground | Over);

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
    /// <b>A bridge's lane and the road under it share no ground</b> (TER-5c, PHY-1a): the same two crossing ribbons,
    /// one of them on the bridge's channel, are marked against nothing.
    /// </summary>
    [Fact]
    public void TwoWaysCrossingOnTwoChannelsAreNotMarked()
    {
        var atlas = Crossing(Over, Ground);

        Assert.Empty(atlas.Marks.Of(0).ToArray());
        Assert.Empty(atlas.Marks.Of(1).ToArray());
    }

    /// <summary>
    /// <b>A body is read onto the ways of its own channels alone</b> (TER-4c.2, PHY-1a): a box where a bridge's lane
    /// crosses the road under it is on the bridge's lane read on the bridge's channel, and on the road's read on the
    /// ground's.
    /// </summary>
    [Fact]
    public void ABoxWhereTwoChannelsCrossIsOnItsOwnChannelsWayAlone()
    {
        var atlas = Crossing(Over, Ground);

        Assert.Equal([0], WaysUnderTheCrossing(atlas, Over));
        Assert.Equal([1], WaysUnderTheCrossing(atlas, Ground));
    }

    /// <summary>
    /// <b>A way on two channels is found from either</b> (TER-4c.2, PHY-1a): where a bridgehead's way crosses a road
    /// on the ground, a box read on the bridge's channel is on the bridgehead's way alone, and one read on the ground's
    /// is on both.
    /// </summary>
    [Fact]
    public void AWayOnTwoChannelsIsFoundFromEither()
    {
        var atlas = Crossing(Both, Ground);

        Assert.Equal([0], WaysUnderTheCrossing(atlas, Over));
        Assert.Equal([0, 1], WaysUnderTheCrossing(atlas, Ground));
    }

    /// <summary>
    /// <b>A wheel is on the ways of its own channels alone</b> (TER-7b, PHY-1a): on a deck whose lanes are all the
    /// bridge's, a wheel read on the bridge's channel stands on a way, and one read on the ground's — a car under the
    /// deck — stands on none.
    /// </summary>
    [Fact]
    public void AWheelIsOnTheWaysOfItsOwnChannelsAlone()
    {
        var atlas = Crossing(Over, Over);
        var wheelM = new Vector2(LengthM * 0.5f, 0f);

        Assert.True(OnAWay(atlas, wheelM, Over));
        Assert.False(OnAWay(atlas, wheelM, Ground));
    }

    /// <summary>
    /// <b>A wheel grips as on a way out to the lattice's reach past its band, and stands on it only inside</b> (TER-7b,
    /// TER-4c.2): on a lattice point a quarter step past the edge it is on paving and on no way, so the seam two bands
    /// leave short of each other is not grass; a reach and a step past the edge it is on neither.
    /// </summary>
    [Fact]
    public void AWheelGripsOutToTheReachPastABandAndStandsOnItOnlyInside()
    {
        var level = Config.RibbonLevel;
        var stepM = level.CellM;
        var pointM = level.MiddleM(80, 80);
        var lineY = pointM.Y - (WidthM * 0.5f) - (stepM * 0.25f);
        var atlas = Laid((new Vector2(0f, lineY), new Vector2(LengthM * 2f, lineY)));
        Span<WayCover> under = stackalloc WayCover[8];

        Assert.Equal(0, atlas.UnderWheels([pointM], 0f, under, out var justPast));
        Assert.Equal(1, justPast);
        var farM = pointM + new Vector2(0f, RibbonAtlas.ReachOf(stepM) + stepM);
        Assert.False(OnAWay(atlas, farM));
    }

    /// <summary>
    /// <b>A car is on the lane its wheels stand on over the stretch its wheels hold</b> (TER-4c.2): from its hindmost
    /// wheel less what is held either side of a wheel to its foremost wheel and as much again, to within a lattice step —
    /// and every wheel on a way.
    /// </summary>
    [Fact]
    public void ACarIsOnItsWheelsLaneOverTheStretchItsWheelsHold()
    {
        const float FrontM = 21.3f, RearM = 18.7f, HalfTrackM = 0.8f, AroundM = 0.9f;
        var atlas = Laid(
            (new Vector2(0f, 0f), new Vector2(LengthM, 0f)),
            (new Vector2(LengthM, WidthM), new Vector2(0f, WidthM)));
        Span<WayCover> under = stackalloc WayCover[8];
        Vector2[] wheelsM = [new(FrontM, HalfTrackM), new(FrontM, -HalfTrackM), new(RearM, HalfTrackM), new(RearM, -HalfTrackM)];

        var count = atlas.UnderWheels(wheelsM, AroundM, under, out var onAWay);

        Assert.Equal(0b1111, onAWay);
        Assert.Equal(1, count);
        Assert.Equal(0, under[0].Way);
        Assert.InRange(under[0].FromM, RearM - AroundM - atlas.StepM, RearM - AroundM);
        Assert.InRange(under[0].ToM, FrontM + AroundM, FrontM + AroundM + atlas.StepM);
    }

    /// <summary>
    /// <b>A car with no wheel on a way is on none</b> (TER-4c.2), however near its body is to one: on the grass past the
    /// lattice's reach beside a lane it holds nothing of it, and every wheel grips as on grass (TER-7b).
    /// </summary>
    [Fact]
    public void ACarWithNoWheelOnAWayIsOnNone()
    {
        const float FrontM = 21.3f, RearM = 18.7f, TrackM = 1.6f, AroundM = 0.9f;
        var atlas = Laid((new Vector2(0f, 0f), new Vector2(LengthM, 0f)));
        Span<WayCover> under = stackalloc WayCover[8];
        var nearM = (WidthM * 0.5f) + RibbonAtlas.ReachOf(atlas.StepM) + atlas.StepM + ClearM;
        Vector2[] wheelsM = [new(FrontM, nearM), new(FrontM, nearM + TrackM), new(RearM, nearM), new(RearM, nearM + TrackM)];

        Assert.Equal(0, atlas.UnderWheels(wheelsM, AroundM, under, out var onAWay));
        Assert.Equal(0, onAWay);
    }

    /// <summary>How much clearer than a distance a wheel is stood, so no rounding stands it back on the edge.</summary>
    const float ClearM = 0.01f;

    /// <summary>Whether one wheel stands on a way, read as a car's four are (<see cref="RibbonAtlas.UnderWheels"/>).</summary>
    static bool OnAWay(RibbonAtlas atlas, Vector2 wheelM, byte channels = Ground)
    {
        Span<WayCover> under = stackalloc WayCover[8];
        atlas.UnderWheels([wheelM], 0f, under, out var onAWay, channels);
        return onAWay != 0;
    }

    /// <summary>Two ways crossing square at their middles, on these channels.</summary>
    static RibbonAtlas Crossing(byte first, byte second)
    {
        var lines = new Lines(
            Straight(new Vector2(0f, 0f), new Vector2(LengthM, 0f)),
            Straight(new Vector2(LengthM * 0.5f, -LengthM * 0.5f), new Vector2(LengthM * 0.5f, LengthM * 0.5f)))
        {
            Channels = [first, second],
        };
        return RibbonAtlas.Lay(lines, Config.RibbonLevel, Config.RibbonTouchM);
    }

    static int[] WaysUnderTheCrossing(RibbonAtlas atlas, byte channels)
    {
        Span<WayCover> under = stackalloc WayCover[RibbonAtlas.MostWaysUnderABody];
        var count = atlas.UnderBox(new Vector2(LengthM * 0.5f, 0f), Vector2.UnitX, 2f, 1f, under, channels);
        return [.. under[..count].ToArray().Select(cover => cover.Way).Order()];
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
    /// <b>A box just past a bend is on the stretch of the bend it stands over</b> (TER-4c.2), and not back to the
    /// joint: the straight before the bend, carried on past its end, runs outside the bend and is nearer the
    /// points out there than the bend is — and is not where they are.
    /// </summary>
    [Fact]
    public void ABoxJustPastABendIsOnTheStretchItStandsOver()
    {
        const float StraightM = 20f;
        const float BendRadiusM = 15f;
        const float PastTheJointM = 3f;
        const float HalfLengthM = 2f;
        var bend = new ArcSeg(new Vector2(StraightM, 0f), 0f, StraightM, 1f / BendRadiusM);
        var atlas = Laid([new ArcSeg(Vector2.Zero, 0f, StraightM, 0f), bend]);
        Span<WayCover> under = stackalloc WayCover[8];

        var count = atlas.UnderBox(
            bend.PointAtM(PastTheJointM), Heading.Unit(bend.HeadingAtRad(PastTheJointM)), HalfLengthM, 1f, under);

        Assert.Equal(1, count);
        Assert.InRange(under[0].FromM, StraightM + PastTheJointM - HalfLengthM - atlas.StepM, StraightM + PastTheJointM);
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

    /// <summary>
    /// <b>A walker stands as a single wheel at its centre</b> (TER-4c.2): on the lane under it alone, held its radius
    /// either side of where it stands along it, to within a lattice step — however near the other lane its body reaches.
    /// </summary>
    [Fact]
    public void AWalkerIsOnTheLaneUnderItsCentreOverItsRadiusEitherSide()
    {
        const float AlongM = 20f, RadiusM = 0.5f;
        var atlas = Laid(
            (new Vector2(0f, 0f), new Vector2(LengthM, 0f)),
            (new Vector2(LengthM, WidthM), new Vector2(0f, WidthM)));
        Span<WayCover> under = stackalloc WayCover[8];
        var centreM = new Vector2(AlongM, (WidthM * 0.5f) - RibbonAtlas.ReachOf(atlas.StepM) - ClearM);

        var count = atlas.UnderAWheel(centreM, RadiusM, under);

        Assert.Equal(1, count);
        Assert.Equal(0, under[0].Way);
        Assert.InRange(under[0].FromM, AlongM - RadiusM - atlas.StepM, AlongM - RadiusM);
        Assert.InRange(under[0].ToM, AlongM + RadiusM, AlongM + RadiusM + atlas.StepM);
    }

}
