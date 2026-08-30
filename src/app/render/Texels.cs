using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TrafficSimulation.Runtime;

namespace TrafficSimulation.App.Render;

/// <summary>
/// A picture's texels, off the decoder this machine has. The desktop's is the library it already
/// carries for writing shots.
/// </summary>
/// <remarks>
/// <para>
/// <b>WEB-1 — <c>web/Texels.Web.cs</c> is the other half</b>, and the two project files pick which is
/// compiled. Above them <see cref="SheetAtlas"/> knows only that a path became texels, which is why the
/// browser's head needs no image codec of its own.
/// </para>
/// <para>
/// <b>The caller sizes the span, and it does so from the file's own header</b>
/// (<see cref="Core.Config.ImageHeader"/>). The atlas has to know every sheet's size before it decodes
/// the first one, so the size is never a thing a decode hands back — here or on the other side.
/// </para>
/// </remarks>
internal static class Texels
{
    /// <summary>The picture at <paramref name="path"/>, top row first, into a span laid at its size.</summary>
    public static void Decode(string path, Span<Texel> into)
    {
        using var decoded = Image.Load<Rgba32>(path);
        if (decoded.Width * decoded.Height != into.Length) throw new InvalidDataException(
            $"{path}: {decoded.Width}x{decoded.Height} texels into room for {into.Length}.");

        decoded.CopyPixelDataTo(MemoryMarshal.AsBytes(into));
    }
}
