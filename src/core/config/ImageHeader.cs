using System.Buffers.Binary;

namespace TrafficSimulation.Core.Config;

/// <summary>
/// How big a picture is, read out of its own header rather than by decoding it.
/// </summary>
/// <remarks>
/// <para>
/// <b>A sheet's size is wanted long before its texels are.</b> The atlas packs by size, and a car's
/// lenses are placed in its sprite's own texel grid, so both want a width and a height off a file
/// neither of them is going to decode. A decoder answers that by decoding — megabytes of work for four
/// numbers, and on the browser's head an asynchronous call into the page for something the caller is
/// standing still for.
/// </para>
/// <para>
/// <b>PNG and WebP in all three of its shapes, because that is what this town ships</b> — the lossless
/// and extended forms the art is written in and the lossy one a few sheets still are. Anything else
/// faults by name rather than guessing: a wrong size is an atlas packing two sheets over one another,
/// and nothing about that picture says a header was misread.
/// </para>
/// </remarks>
internal static class ImageHeader
{
    /// <summary>Enough for the longest of the four headers below — VP8X's canvas ends at thirty.</summary>
    const int HeadBytes = 32;

    static ReadOnlySpan<byte> Png => [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>What stands at the head of a lossy WebP's key frame, and only of a key frame.</summary>
    static ReadOnlySpan<byte> KeyFrame => [0x9D, 0x01, 0x2A];

    /// <summary>The size of the picture in <paramref name="path"/>, without decoding a texel of it.</summary>
    public static (int WidthPx, int HeightPx) Measure(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException($"No picture at {path}.");

        Span<byte> head = stackalloc byte[HeadBytes];
        using var file = File.OpenRead(path);
        return Measure(head[..file.ReadAtLeast(head, HeadBytes, throwOnEndOfStream: false)], path);
    }

    /// <summary>The same, of a picture already in hand — the glyph sheet the assembly carries.</summary>
    /// <param name="named">What to call the picture if its header is not one this knows.</param>
    public static (int WidthPx, int HeightPx) Measure(ReadOnlySpan<byte> head, string named)
    {
        if (head.StartsWith(Png)) return head.Length >= 24
            ? (Big(head[16..]), Big(head[20..]))
            : throw Short(named, "PNG");

        // RIFF····WEBP, and then which of the three encodings it is. The payload of that chunk starts
        // at twenty, and each of them says its size in a different place a few bytes in.
        if (head.Length >= 16 && head[..4].SequenceEqual("RIFF"u8) && head[8..12].SequenceEqual("WEBP"u8))
        {
            var kind = head[12..16];

            // Extended: a canvas the frames sit on, and its size is the picture's whatever they hold.
            if (kind.SequenceEqual("VP8X"u8)) return head.Length >= 30
                ? (Little24(head[24..]) + 1, Little24(head[27..]) + 1)
                : throw Short(named, "VP8X");

            // Lossless: a signature byte, then two fourteen-bit fields packed into the next four.
            if (kind.SequenceEqual("VP8L"u8))
            {
                if (head.Length < 25) throw Short(named, "VP8L");
                var packed = BinaryPrimitives.ReadUInt32LittleEndian(head[21..]);
                return ((int)(packed & 0x3FFF) + 1, (int)((packed >> 14) & 0x3FFF) + 1);
            }

            // Lossy: a three-byte frame tag, the key frame's start code, then the two sizes. The top
            // two bits of each are an upscaling hint that nothing here draws with.
            if (kind.SequenceEqual("VP8 "u8))
            {
                if (head.Length < 30) throw Short(named, "VP8");
                if (!head[23..26].SequenceEqual(KeyFrame)) throw new InvalidDataException(
                    $"{named}: a lossy WebP whose first frame is not a key frame, which this cannot size.");

                return (BinaryPrimitives.ReadUInt16LittleEndian(head[26..]) & 0x3FFF,
                        BinaryPrimitives.ReadUInt16LittleEndian(head[28..]) & 0x3FFF);
            }
        }

        throw new InvalidDataException(
            $"{named}: not a PNG or a WebP. This town's art is both of those and nothing else, and a " +
            "size guessed off an unknown header is an atlas that packs sheets over one another.");
    }

    static int Big(ReadOnlySpan<byte> at) => BinaryPrimitives.ReadInt32BigEndian(at);

    static int Little24(ReadOnlySpan<byte> at) => at[0] | (at[1] << 8) | (at[2] << 16);

    static InvalidDataException Short(string named, string kind) =>
        new($"{named}: a {kind} header that stops before its size.");
}
