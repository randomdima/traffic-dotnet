using System.Runtime.InteropServices;
using TrafficSimulation.Runtime;

namespace TrafficSimulation.App.Render;

/// <summary>
/// A picture's texels, off the decoder the page already has. Nothing here decodes anything: the
/// browser did that as the sheet was fetched, and this reads the result out.
/// </summary>
/// <remarks>
/// <para>
/// <b>The decode is split in two because only half of it can wait.</b> <c>createImageBitmap</c> is a
/// promise, and the atlas is filled from inside <c>Game.Start</c>, which is reached from a frame and
/// cannot await anything. But <c>drawImage</c> and <c>getImageData</c> are synchronous — so
/// <see cref="Main.Data"/> makes every sheet's bitmap on the way in, where waiting is allowed, and this
/// reads one out where the packer stands. The browser holds the bitmaps in its own memory in the
/// meantime, not in this heap.
/// </para>
/// <para>
/// <b>So a sheet the fetch did not decode is a fault and not a second fetch.</b> There is no way back to an
/// asynchronous call from here, and a picture that arrived late would be a page that half-drew rather
/// than one that said what was missing.
/// </para>
/// </remarks>
internal static class Texels
{
    /// <summary>The picture at <paramref name="path"/>, top row first, into a span laid at its size.</summary>
    public static void Decode(string path, Span<Texel> into)
    {
        var bytes = WebGpu.Texels(path);
        if (bytes != into.Length * Marshal.SizeOf<Texel>()) throw new InvalidDataException(
            $"{path}: {bytes} bytes of texels into room for {into.Length * Marshal.SizeOf<Texel>()}.");

        WebGpu.Take(MemoryMarshal.AsBytes(into));
    }
}
