using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Statics;
using Xunit;

namespace TrafficSimulation.Tests.Config;

/// <summary>
/// What an asset file is allowed to be. The shapes are checked against the shipped catalogues rather
/// than a fixture wherever they can be, because a format that parses a fixture and not the art is a
/// format nobody is running.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class AssetJsonTests
{
    [Fact]
    public void EveryShippedCatalogueLoadsAndNamesArtThatIsThere()
    {
        string[] art =
        [
            .. CarCatalog.Load().Variants.SelectMany(car => new[] { car.SpritePath, car.WreckSpritePath }),
            .. PersonCatalog.Load().Variants.Select(person => person.SheetPath),
            .. BuildingCatalog.Load().Variants.Select(building => building.SpritePath),
            .. PropCatalog.Load().Variants.Select(prop => prop.SpritePath),
        ];

        Assert.All(art, path => Assert.True(File.Exists(path), $"a catalogue names {path}, which is not there"));
    }

    /// <summary>
    /// The reason a path is written the way it is: a variant folder names its own art, so the folder
    /// can be moved without every path inside it going stale.
    /// </summary>
    [Fact]
    public void APathIsResolvedAgainstTheFileThatNamedIt()
    {
        var variant = Path.Combine(ProjectPaths.Assets, "agents", "car", "variants", "hatch_teal", "hatch_teal.json");

        Assert.Equal(
            Path.Combine(ProjectPaths.Assets, "agents", "car", "variants", "hatch_teal", "hatch_teal.png"),
            AssetJson.Beside(variant, "hatch_teal.png"));

        Assert.Equal(
            Path.Combine(ProjectPaths.Assets, "agents", "car", "variants", "common", "Fleet.json"),
            AssetJson.Beside(variant, "../common/Fleet.json"));
    }

    [Fact]
    public void AMisspeltKeyIsRefusedRatherThanDropped()
    {
        var path = Scratch.Write(
            "misspelt-variant.json",
            """
            {
              "id": "x", "sprite": "x.png", "footprintM": [1, 2], "frontAxleM": 1, "rearAxleM": -1,
              "trackM": 1, "drivetrain": 0, "handlng": { "maxSpeed": 1 }
            }
            """);

        Assert.Throws<InvalidDataException>(() => AssetJson.Read(path, CarVariantJson.Default.CarVariantFile));
    }

    [Fact]
    public void AMissingFigureIsRefusedRatherThanDefaulted()
    {
        var path = Scratch.Write("short-variant.json", """{ "id": "x", "sprite": "x.png" }""");

        Assert.Throws<InvalidDataException>(() => AssetJson.Read(path, CarVariantJson.Default.CarVariantFile));
    }

    [Fact]
    public void AnEmptyCatalogueIsRefused()
    {
        var path = Scratch.Write("empty-catalogue.json", """{ "variants": [] }""");

        Assert.Throws<InvalidDataException>(() => AssetJson.Catalog(path));
    }
}
