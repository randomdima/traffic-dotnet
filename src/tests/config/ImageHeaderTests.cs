using SixLabors.ImageSharp;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.Config;

/// <summary>
/// The header reader against the library the desktop still carries, over every picture the town ships.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the independent-implementation check, as the physics has against Box2D.</b> The browser's
/// head has no decoder, so <see cref="ImageHeader"/> is the only thing that knows how big a sheet is
/// before the atlas packs it — and a header misread by four bytes is not an error there, it is two
/// sprites packed over one another. ImageSharp is the second opinion, and it is here rather than in the
/// engine for exactly that reason.
/// </para>
/// <para>
/// <b>The corpus is the shipped art and not a fixture</b>, so all three WebP encodings are covered by
/// being what the town is drawn from — extended, lossless and lossy — plus the PNG the typeface is cut
/// as. A format nothing ships is a format nobody is running.
/// </para>
/// </remarks>
[Trait(Tier.Key, Tier.Unit)]
public class ImageHeaderTests
{
    [Fact]
    public void EverySheetTheTownShipsIsSizedTheWayADecoderSizesIt()
    {
        var pictures = Directory.GetFiles(ProjectPaths.Assets, "*", SearchOption.AllDirectories)
            .Where(file => file.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ||
                           file.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(pictures);
        foreach (var picture in pictures)
        {
            var decoder = Image.Identify(picture);
            Assert.Equal((decoder.Width, decoder.Height), ImageHeader.Measure(picture));
        }
    }

    [Fact]
    public void AFileThatIsNoPictureFaultsByNameRatherThanGuessing()
    {
        var trouble = Assert.Throws<InvalidDataException>(
            () => ImageHeader.Measure(ProjectPaths.SharedFiguresFile));
        Assert.Contains(Path.GetFileName(ProjectPaths.SharedFiguresFile), trouble.Message);
    }

    [Fact]
    public void AMissingPictureSaysWhichOne()
    {
        var absent = Path.Combine(ProjectPaths.Assets, "nothing", "here.webp");
        Assert.Contains(absent, Assert.Throws<FileNotFoundException>(() => ImageHeader.Measure(absent)).Message);
    }
}
