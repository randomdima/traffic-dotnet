using System.Numerics;
using System.Text.Json;
using TrafficSimulation.World.Statics;
using Xunit;

namespace TrafficSimulation.Tests.World.Statics;

/// <summary>
/// <b>A prop look's size is its file's</b> (GEN-6e, <see cref="PropCatalog"/>): a box at its footprint, a round look's
/// picture with its longest side the diameter, the disc the town keeps for either derived from that and never stated,
/// and a file stating its size twice or not at all refused.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P6)]
public class PropCatalogTests
{
    const string Bench = """
        { "id": "bench", "sprite": "bench.webp", "kind": 2, "footprintM": [2.0, 0.6], "cornerRadiusM": 0.05, "turns": true,
          "describe": "a park bench, wooden slats on two concrete ends" }
        """;

    const string Oak = """{ "id": "oak", "sprite": "oak.webp", "kind": 0, "diameterM": 4.2, "describe": "a young oak's crown" }""";

    /// <param name="aspect">The picture's width over its height.</param>
    static PropVariant Read(string json, float aspect = 1f) =>
        PropCatalog.Of(JsonSerializer.Deserialize(json, PropVariantJson.Default.PropVariantFile)!, "look.json", "look.webp", aspect);

    /// <summary><b>A box is the footprint its file states</b>, width by depth.</summary>
    [Fact]
    public void ABoxIsTheFootprintItsFileStates() => Assert.Equal(new Vector2(2.0f, 0.6f), Read(Bench, 2.0f / 0.6f).SizeM);

    /// <summary>
    /// <b>A box's girth is its diagonal</b>, so the picture stays inside its disc whichever way it is turned: a skip 3 m
    /// by 4 m keeps a disc 5 m across.
    /// </summary>
    [Fact]
    public void ABoxsGirthIsItsDiagonal() =>
        Assert.Equal(5f, Read("""{ "id": "skip", "sprite": "skip.webp", "kind": 2, "footprintM": [3.0, 4.0] }""", 0.75f).GirthM, 1e-5f);

    /// <summary>
    /// <b>A round look's picture is its diameter along its longest side</b> and its aspect's along the other: a crown
    /// 4.2 m across drawn half again as wide as it is tall is 4.2 m by 2.8 m.
    /// </summary>
    [Theory]
    [InlineData(1.5f, 4.2f, 2.8f)]
    [InlineData(0.75f, 3.15f, 4.2f)]
    public void ARoundLooksPictureIsItsDiameterAlongItsLongestSide(float aspect, float widthM, float depthM)
    {
        var sizeM = Read(Oak, aspect).SizeM;

        Assert.Equal(widthM, sizeM.X, 1e-5f);
        Assert.Equal(depthM, sizeM.Y, 1e-5f);
    }

    /// <summary>
    /// <b>A round look's girth is its diameter</b>, however its picture is shaped: a crown fills its disc and the
    /// picture's corners are bare.
    /// </summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void ARoundLooksGirthIsItsDiameter(float aspect) => Assert.Equal(4.2f, Read(Oak, aspect).GirthM, 1e-5f);

    /// <summary><b>A look stands as the collider its file states</b>, a street tree's trunk under its crown.</summary>
    [Fact]
    public void ALookStandsAsItsCollider() =>
        Assert.Equal(0.5f, Read("""{ "id": "lime", "sprite": "lime.webp", "kind": 1, "diameterM": 4.0, "colliderM": 0.5 }""").BodyM);

    /// <summary><b>A look stating no collider stands as its girth</b>: a skip 3 m by 4 m stands as a disc 5 m across.</summary>
    [Fact]
    public void ALookStatingNoColliderStandsAsItsGirth() =>
        Assert.Equal(5f, Read("""{ "id": "skip", "sprite": "skip.webp", "kind": 2, "footprintM": [3.0, 4.0] }""", 0.75f).BodyM, 1e-5f);

    /// <summary>
    /// <b>A look stating both a diameter and a footprint, or neither, is refused</b>: it is round or a box. <b>And so is
    /// one whose body is wider than its girth</b>, which would stand in ground the town never cleared for it.
    /// </summary>
    [Theory]
    [InlineData("""{ "id": "both", "sprite": "both.webp", "kind": 1, "diameterM": 1.0, "footprintM": [1.0, 1.0] }""")]
    [InlineData("""{ "id": "neither", "sprite": "neither.webp", "kind": 1 }""")]
    [InlineData("""{ "id": "wide", "sprite": "wide.webp", "kind": 1, "diameterM": 1.0, "colliderM": 2.0 }""")]
    public void ALookWhoseSizeCannotStandIsRefused(string json) =>
        Assert.Throws<InvalidDataException>(() => Read(json));

    /// <summary><b>A look is drawn as often as its file's share says</b>, and one stating none is drawn at a share of one.</summary>
    [Theory]
    [InlineData("""{ "id": "bin", "sprite": "bin.webp", "kind": 2, "diameterM": 0.5, "share": 3.0 }""", 3f)]
    [InlineData("""{ "id": "bin", "sprite": "bin.webp", "kind": 2, "diameterM": 0.5 }""", 1f)]
    public void ALookIsDrawnAtItsFilesShare(string json, float share) => Assert.Equal(share, Read(json).Share);

    /// <summary><b>A share of nothing is refused</b>: a look no pass ever draws is a look the art should not ship (GEN-6b).</summary>
    [Theory]
    [InlineData("""{ "id": "never", "sprite": "never.webp", "kind": 2, "diameterM": 0.5, "share": 0 }""")]
    [InlineData("""{ "id": "never", "sprite": "never.webp", "kind": 2, "diameterM": 0.5, "share": -1 }""")]
    public void AShareOfNothingIsRefused(string json) => Assert.Throws<InvalidDataException>(() => Read(json));
}
