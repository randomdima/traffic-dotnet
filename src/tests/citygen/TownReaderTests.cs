using System.Buffers.Binary;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Persistence;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// The one thing that crosses between the engines at run time, read back. What is asserted here is
/// the format's own contract and the two shapes the reader adds on the way in:
/// the dense lane-direction grid, and a run stored as a flat array with its offsets beside it.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class TownReaderTests
{
    public static TheoryData<string> Maps => Towns.EveryShippedMap();

    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryShippedMapLoads(string map)
    {
        var plan = Towns.Of(map);

        Assert.Equal(map, plan.Name);
        Assert.True(plan.GridWidth > 0 && plan.GridHeight > 0);
        Assert.Equal(plan.CellCount, plan.Cells.Length);
        Assert.Equal(plan.CellCount * 2, plan.LaneDirs.Length);
        Assert.Equal(plan.GridWidth * plan.CellSizeM, plan.WorldSizeM.X, tolerance: plan.CellSizeM);
        Assert.Equal(plan.GridHeight * plan.CellSizeM, plan.WorldSizeM.Y, tolerance: plan.CellSizeM);
    }

    /// <summary>The header figures of the fixture map, which is the town every detailed check is staged on.</summary>
    [Fact]
    public void TheFixtureMapIsTheOneTownPlanMdDescribes()
    {
        var plan = Towns.Of(Towns.Fixture);

        Assert.Equal(480f, plan.WorldSizeM.X);
        Assert.Equal(320f, plan.WorldSizeM.Y);
        Assert.Equal(1f, plan.CellSizeM);
        Assert.Equal(SimConfig.Shipped().PavementWidthM, plan.PavementWidthM);
    }

    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryRunIsFlatWithItsOffsetsBesideIt(string map)
    {
        var plan = Towns.Of(map);

        AssertOffsets(plan.Roads.SegmentOffsets, plan.Roads.Count, plan.Roads.Segments.Length);
        AssertOffsets(plan.ParkingLots.SpaceOffsets, plan.ParkingLots.Count, plan.ParkingLots.SpaceCount);
        AssertOffsets(plan.Buildings.EntryOffsets, plan.Buildings.Count, plan.Buildings.EntryPointM.Length);
        AssertOffsets(plan.Water.Outline.Offsets, plan.Water.Outline.Count, plan.Water.Outline.PointM.Length);
        Assert.Equal(plan.ParkingLots.SpaceCount, plan.ParkingLots.SpaceHeadingRad.Length);
    }

    /// <summary>
    /// The sparse triples the file carries, read back off the dense grid the reader expands them
    /// into: a direction exists only on carriageway, so a cell with one is a directional cell.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void LaneDirectionsAreExpandedOntoTheCarriagewayAndNowhereElse(string map)
    {
        var plan = Towns.Of(map);

        var directional = 0;
        for (var cell = 0; cell < plan.CellCount; cell++)
        {
            var x = plan.LaneDirs[cell * 2];
            var y = plan.LaneDirs[cell * 2 + 1];
            if (x == 0 && y == 0) continue;

            directional++;
            var lengthSquared = (x / 127f * (x / 127f)) + (y / 127f * (y / 127f));
            Assert.InRange(lengthSquared, 0.9f, 1.1f);
        }

        Assert.True(directional > 0, $"{map} carries no lane direction at all");
    }

    [Fact]
    public void AFileThatIsNotATownIsRefused()
    {
        var bytes = new byte[64];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, 0x0102030405060708);

        var refusal = Assert.Throws<FormatException>(() => TownReader.Read(bytes));
        Assert.Contains("not a .town file", refusal.Message);
    }

    /// <summary>A version the reader does not know is refused rather than guessed at.</summary>
    [Fact]
    public void AFormatVersionThisEngineDoesNotReadIsRefused()
    {
        var bytes = new byte[64];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, TownReader.Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), TownReader.Version + 1);

        var refusal = Assert.Throws<FormatException>(() => TownReader.Read(bytes));
        Assert.Contains($"version {TownReader.Version + 1}", refusal.Message);
    }

    [Fact]
    public void ATruncatedTownIsRefusedRatherThanHalfRead()
    {
        var whole = File.ReadAllBytes(ProjectPaths.TownFile(Towns.Fixture));

        Assert.Throws<FormatException>(() => TownReader.Read(whole.AsSpan(0, whole.Length / 2)));
    }

    /// <summary>
    /// The other half of the same rule: a file with bytes after the last field the format declares is
    /// a file written by something this reader does not agree with, and agreeing with it by accident
    /// is the outcome that looks like it worked.
    /// </summary>
    [Fact]
    public void ATownWithBytesLeftOverIsRefused()
    {
        var whole = File.ReadAllBytes(ProjectPaths.TownFile(Towns.Fixture));
        var padded = new byte[whole.Length + 1];
        whole.CopyTo(padded, 0);

        var refusal = Assert.Throws<FormatException>(() => TownReader.Read(padded));
        Assert.Contains("bytes after the last field", refusal.Message);
    }

    static void AssertOffsets(int[] offsets, int count, int flatLength)
    {
        Assert.Equal(count + 1, offsets.Length);
        Assert.Equal(0, offsets[0]);
        Assert.Equal(flatLength, offsets[^1]);
        for (var i = 1; i < offsets.Length; i++) Assert.True(offsets[i] >= offsets[i - 1], "offsets run backwards");
    }
}
