using TrafficSimulation.Agents.Person.Body;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Person;

/// <summary>
/// The one fact about the art this engine has to get right, and the one it cannot check by looking:
/// at an 8×8 sheet's scale a head turned the wrong way just reads as a head, and the reference build
/// signed off a wrong row that way.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class PersonCatalogTests
{
    /// <summary>
    /// The rows run <b>anticlockwise from up</b>, and "up" is negative y because this world's y grows
    /// downwards. These eight rows are the whole mapping.
    /// </summary>
    [Theory]
    [InlineData(0f, -1f, 0)]   // up
    [InlineData(-1f, -1f, 1)]  // up-left
    [InlineData(-1f, 0f, 2)]   // left
    [InlineData(-1f, 1f, 3)]   // down-left
    [InlineData(0f, 1f, 4)]    // down
    [InlineData(1f, 1f, 5)]    // down-right
    [InlineData(1f, 0f, 6)]    // right
    [InlineData(1f, -1f, 7)]   // up-right
    public void EachHeadingPicksItsOwnFacingRow(float dx, float dy, int row)
    {
        Assert.Equal(row, PersonCatalog.FacingRow(MathF.Atan2(dy, dx)));
    }

    [Fact]
    public void EveryHeadingLandsOnARowThatExists()
    {
        for (var degree = -720; degree <= 720; degree++)
        {
            var row = PersonCatalog.FacingRow(degree * MathF.PI / 180f);
            Assert.InRange(row, 0, PersonCatalog.FacingRows - 1);
        }
    }

    /// <summary>
    /// Stepped by distance and never by time, so ground that slows a walker slows its stride: half a
    /// cycle further along is half the columns further along, whatever the walker's pace was.
    /// </summary>
    [Fact]
    public void TheWalkColumnIsSteppedByDistance()
    {
        Assert.Equal(0, PersonCatalog.WalkColumn(0f, strideCycleM: 2f));
        Assert.Equal(4, PersonCatalog.WalkColumn(1f, strideCycleM: 2f));
        Assert.Equal(0, PersonCatalog.WalkColumn(2f, strideCycleM: 2f));
        Assert.Equal(4, PersonCatalog.WalkColumn(3f, strideCycleM: 2f));
    }

    [Fact]
    public void EveryColumnIsOnTheSheet()
    {
        for (var tenthMetre = 0; tenthMetre < 1_000; tenthMetre++)
        {
            Assert.InRange(PersonCatalog.WalkColumn(tenthMetre * 0.1f, 1.82f), 0, PersonCatalog.WalkColumns - 1);
        }
    }

    /// <summary>
    /// The shipped looks, read out of the shared catalogue. Each sheet has to divide into whole cells
    /// both ways, because a sheet that does not is one whose frames this engine would sample across.
    /// </summary>
    [Fact]
    public void TheShippedLooksLoadAndTheirSheetsAreWholeCells()
    {
        var catalog = PersonCatalog.Load();

        Assert.NotEmpty(catalog.Variants);
        foreach (var variant in catalog.Variants)
        {
            Assert.True(File.Exists(variant.SheetPath), variant.SheetPath);
            Assert.True(variant.HeightM > 0f, variant.Id);

            using var sheet = SixLabors.ImageSharp.Image.Load(variant.SheetPath);
            Assert.Equal(0, sheet.Width % PersonCatalog.WalkColumns);
            Assert.Equal(0, sheet.Height % PersonCatalog.FacingRows);
        }
    }
}
