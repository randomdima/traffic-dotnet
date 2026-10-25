using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Gen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using Xunit;

namespace TrafficSimulation.Tests.CityGen.Gen;

/// <summary>
/// <b>A brief's water as it is drawn</b> (<see cref="TerrainStage"/>, GEN-2c, GEN-14a): a river widening and narrowing
/// no wider than the town's deck spans, and the rivers flowing into it drawn as one water with it.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P6)]
public class TerrainStageTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static TerrainStage.Water Laid(TownBrief brief)
    {
        var draw = new Rng(brief.Seeds.Resolved.Terrain, Streams.Terrain);
        return TerrainStage.Lay(brief, Config, ref draw);
    }

    /// <summary>
    /// <b>A river's tributaries are one water with it</b>: one ring round all of it and one round its shore, so no bank,
    /// fade or foam is drawn across the open river where they meet.
    /// </summary>
    [Fact]
    public void TributariesAreOneWaterWithTheirRiver()
    {
        var water = Laid(new TownBrief
        {
            Name = "Tributaries", Description = "", WidthM = 3000f, HeightM = 2400f, Seeds = new BriefSeeds { Town = Towns.CitySeed },
            Rivers = new RiversBrief { PerKm = 0.34f, BearingDeg = 100f, WidthM = 60f, Tributaries = 3 },
        });

        Assert.True(water.Courses.Count > 1, "no tributary was laid to ask about");
        Assert.Equal((1, 1), (water.Rings.Outline.Count, water.Rings.Shore.Count));
    }

    /// <summary>
    /// <b>A river flowing into the sea is one water with it</b> (GEN-62): one ring round the sea and every river in it, and
    /// one round its shore.
    /// </summary>
    [Fact]
    public void ARiverIntoTheSeaIsOneWaterWithIt()
    {
        var water = Laid(new TownBrief
        {
            Name = "Mouth", Description = "", WidthM = 3000f, HeightM = 2400f, Seeds = new BriefSeeds { Town = Towns.CitySeed },
            Sea = new SeaBrief { BearingDeg = 0f, Share = 0.2f },
            Rivers = new RiversBrief { PerKm = 0.34f, WidthM = 60f },
        });

        Assert.Equal(2, water.Courses.Count);
        Assert.Equal((1, 1), (water.Rings.Outline.Count, water.Rings.Shore.Count));
    }

    /// <summary>
    /// <b>On a map holding both, the sea is never bridged and a river is</b> (GEN-62, GEN-14b): asked of the water under a
    /// span, a span over the sea is refused and one over the river is not.
    /// </summary>
    [Fact]
    public void TheSeaIsNeverBridgedAndARiverIs()
    {
        var brief = new TownBrief
        {
            Name = "Both", Description = "", WidthM = 3000f, HeightM = 2400f, Seeds = new BriefSeeds { Town = Towns.CitySeed },
            Sea = new SeaBrief { BearingDeg = 0f, Share = 0.2f },
            Rivers = new RiversBrief { PerKm = 0.34f, WidthM = 60f },
            Connectors = new ConnectorsBrief { IntercityRoads = 0, Railways = 0 },
        };
        var town = TownGen.Author(brief, Config);

        Assert.All(town.Connectors.Bridges, site => Assert.False(town.Water.InTheSea((site.NearM + site.FarM) * 0.5f)));
        Assert.NotEmpty(town.Connectors.Bridges);
    }

    /// <summary>
    /// <b>A river pools no wider than the town's deck spans square on</b> (GEN-14a), a bridgehead's ground on either bank
    /// included, however far its brief asks it to spread.
    /// </summary>
    [Fact]
    public void ARiverPoolsNoWiderThanTheDeckSpans()
    {
        var water = Laid(Towns.Brief(Towns.CitySeed, waterWidthSpread: 0.9f));
        var nearM = water.Courses.NearOf(0);
        var farM = water.Courses.FarOf(0);

        for (var point = 0; point < nearM.Length; point++)
        {
            Assert.InRange(MathF.Abs(nearM[point] - farM[point]) + (2f * TownGen.CorridorM(Config, LayoutEdge.MostLanes)), 0f, Config.CityGen.BridgeDeckLongestM);
        }
    }

    /// <summary>
    /// <b>Two rings joined are the ground either covers</b> (<see cref="WaterOutline.Joined"/>): a 10 × 4 m pond with a
    /// 2 m channel reaching into it 2 m and out 5 m past it is 40 m² and 10 m² more, which is the area of the one ring.
    /// </summary>
    [Fact]
    public void TwoRingsJoinedAreTheGroundEitherCovers()
    {
        Vector2[] pond = [new(0, 0), new(10, 0), new(10, 4), new(0, 4)];
        Vector2[] channel = [new(3, 2), new(3, 9), new(5, 9), new(5, 2)];

        Assert.True(WaterOutline.Joined(pond, channel, 0f, ChordM, out var joined));
        Assert.Equal(50f, MathF.Abs(Area(joined)), 3);
    }

    /// <summary>
    /// <b>Where two waters meet, the point of land between them is rounded to the radius</b>: the pond's edge meets the
    /// channel's square at (3, 4), and a 1 m circle touching both stands √2 − 1 m off that corner at its nearest.
    /// </summary>
    [Fact]
    public void ThePointOfLandWhereTwoWatersMeetIsRounded()
    {
        Vector2[] pond = [new(0, 0), new(10, 0), new(10, 4), new(0, 4)];
        Vector2[] channel = [new(3, 2), new(3, 9), new(5, 9), new(5, 2)];

        Assert.True(WaterOutline.Joined(pond, channel, 1f, ChordM, out var joined));
        Assert.Equal(MathF.Sqrt(2f) - 1f, NearestM(joined, new Vector2(3f, 4f)), 0.02f);
    }

    /// <summary><b>Rings that do not meet are not joined</b>: a channel standing off the pond stays a water of its own.</summary>
    [Fact]
    public void RingsThatDoNotMeetAreNotJoined()
    {
        Vector2[] pond = [new(0, 0), new(10, 0), new(10, 4), new(0, 4)];
        Vector2[] channel = [new(3, 6), new(3, 9), new(5, 9), new(5, 6)];

        Assert.False(WaterOutline.Joined(pond, channel, 0f, ChordM, out _));
    }

    /// <summary>How far a rounding's chords may stand off its circle in these cases.</summary>
    const float ChordM = 0.01f;

    static float NearestM(ReadOnlySpan<Vector2> ring, Vector2 atM)
    {
        var nearestM = float.PositiveInfinity;
        for (var point = 0; point < ring.Length; point++)
        {
            var (fromM, toM) = (ring[point], ring[(point + 1) % ring.Length]);
            var along = Math.Clamp(Vector2.Dot(atM - fromM, toM - fromM) / Vector2.DistanceSquared(fromM, toM), 0f, 1f);
            nearestM = MathF.Min(nearestM, Vector2.Distance(atM, fromM + ((toM - fromM) * along)));
        }

        return nearestM;
    }

    static float Area(ReadOnlySpan<Vector2> ring)
    {
        var twiceM2 = 0f;
        for (var point = 0; point < ring.Length; point++)
        {
            var (fromM, toM) = (ring[point], ring[(point + 1) % ring.Length]);
            twiceM2 += (fromM.X * toM.Y) - (toM.X * fromM.Y);
        }

        return twiceM2 * 0.5f;
    }
}
