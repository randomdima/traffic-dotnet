using SixLabors.ImageSharp.PixelFormats;

namespace TrafficSimulation.Runtime;

/// <summary>
/// A texture's mip chain, box-filtered here rather than blitted by the driver, so how sharp the ground
/// reads is a decision in one place. Un-mipped tarmac shimmers the moment the camera pulls back.
/// </summary>
/// <remarks>
/// It is arithmetic over pixels and knows about no graphics API at all, which is why both machines
/// build their chains with it: one hands the levels to Vulkan as regions of a staging buffer, the
/// other writes them into a WebGPU texture a level at a time, and neither has an opinion about the
/// filter.
/// </remarks>
internal static class MipChain
{
    /// <summary>Each level the box average of the one above it, down to a single texel.</summary>
    public static List<(Rgba32[] Pixels, int Width, int Height)> Build(Rgba32[] top, int width, int height)
    {
        var chain = new List<(Rgba32[] Pixels, int Width, int Height)> { (top, width, height) };
        while (width > 1 || height > 1)
        {
            var (source, sourceWidth, sourceHeight) = chain[^1];
            var nextWidth = Math.Max(1, sourceWidth / 2);
            var nextHeight = Math.Max(1, sourceHeight / 2);
            var next = new Rgba32[nextWidth * nextHeight];

            for (var y = 0; y < nextHeight; y++)
            {
                for (var x = 0; x < nextWidth; x++)
                {
                    var x0 = Math.Min(x * 2, sourceWidth - 1);
                    var x1 = Math.Min((x * 2) + 1, sourceWidth - 1);
                    var y0 = Math.Min(y * 2, sourceHeight - 1);
                    var y1 = Math.Min((y * 2) + 1, sourceHeight - 1);
                    next[(y * nextWidth) + x] = Average(
                        source[(y0 * sourceWidth) + x0], source[(y0 * sourceWidth) + x1],
                        source[(y1 * sourceWidth) + x0], source[(y1 * sourceWidth) + x1]);
                }
            }

            chain.Add((next, nextWidth, nextHeight));
            width = nextWidth;
            height = nextHeight;
        }

        return chain;
    }

    static Rgba32 Average(Rgba32 a, Rgba32 b, Rgba32 c, Rgba32 d) => new(
        (byte)((a.R + b.R + c.R + d.R) / 4),
        (byte)((a.G + b.G + c.G + d.G) / 4),
        (byte)((a.B + b.B + c.B + d.B) / 4),
        (byte)((a.A + b.A + c.A + d.A) / 4));
}
