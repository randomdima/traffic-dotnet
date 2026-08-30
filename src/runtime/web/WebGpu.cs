using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;

namespace TrafficSimulation.Runtime;

/// <summary>
/// The one wall between managed code and the browser's machine. Every entry point reachable from this
/// engine to WebGPU is a method on this class, and the crossings of it are counted.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the wall <see cref="Vk"/> is</b>, in the same place and kept for the same reason: a
/// frame's crossings must be flat in the size of the town (WEB-2). The desktop's five are an acquire, a wait,
/// a reset, a submit and a present. A standing town here makes <b>three</b> — the animation callback
/// coming in, the input going out, and the frame — because the pass, the bundle and the submit are all
/// on the far side of one call, and a browser has no fence to wait on.
/// </para>
/// <para>
/// <b>Nothing is marshalled and no window onto this heap is kept.</b> Memory crosses as
/// <see cref="JSType.MemoryView"/> over <see cref="Span{T}"/>, which is a <c>Uint8Array</c> onto the
/// WebAssembly heap the managed array already lives in: no copy on this side, and no pinning that
/// outlives the call. <b>It must not outlive it</b> — a view is detached the moment the runtime grows
/// its memory, so a view kept across frames is one that fails on whichever frame the town happens to
/// grow the heap.
/// </para>
/// </remarks>
internal static partial class WebGpu
{
    /// <summary>What a buffer is for, in the browser's own flags.</summary>
    public const int Index = 16;

    public const int Vertex = 32;
    public const int Uniform = 64;

    static long _crossings;

    /// <summary>
    /// Crossings of this wall since the page loaded. DEBUG only, and free in a Release build for the
    /// same reason <see cref="Vk.Crossings"/> is.
    /// </summary>
    public static long Crossings => _crossings;

    /// <summary>The device, the canvas and the pipelines. Answers the reason it could not, or the empty string.</summary>
    public static Task<string> Start(string wgsl)
    {
        Count();
        return StartJs(wgsl);
    }

    /// <summary>A buffer laid for what a frame may write into it, and left empty.</summary>
    public static void Reserve(int slot, int byteLength, int usage)
    {
        Count();
        ReserveJs(slot, byteLength, usage);
    }

    /// <summary>A buffer laid for what it is being given and written once: the ground, and the table of places.</summary>
    public static void Buffer(int slot, Span<byte> data, int usage)
    {
        Count();
        BufferJs(slot, data, usage);
    }

    /// <summary>One picture, one layer of the atlas, or one level of a chain.</summary>
    public static void Texture(
        int slot, Span<byte> rgba, int width, int height, int layers, int layer, int level, int levels)
    {
        Count();
        TextureJs(slot, rgba, width, height, layers, layer, level, levels);
    }

    /// <summary>The bind group and the recording, made again when a map is opened and never while a frame is drawn.</summary>
    public static void Rebuild(int indexCount)
    {
        Count();
        RebuildJs(indexCount);
    }

    /// <summary>Everything the device holds for a town being taken down, and the recording that read it.</summary>
    public static void Release()
    {
        Count();
        ReleaseJs();
    }

    /// <summary>The whole frame: the memory the simulation just wrote, and the counts that say how much of it to read.</summary>
    public static void Frame(
        Span<byte> camera, Span<byte> sprites, Span<byte> overlay, Span<byte> underlay,
        int spriteCount, int overlayCount, int underlayCount)
    {
        Count();
        FrameJs(camera, sprites, overlay, underlay, spriteCount, overlayCount, underlayCount);
    }

    /// <summary>What the page has seen since the last frame asked, into the run's own memory.</summary>
    public static void Pump(Span<byte> keys, Span<double> axes)
    {
        Count();
        PumpJs(keys, axes);
    }

    public static void Fullscreen()
    {
        Count();
        FullscreenJs();
    }

    /// <summary>A line under the canvas: what the desktop puts on stdout, where a page can be read.</summary>
    public static void Say(string line)
    {
        Count();
        SayJs(line);
    }

    /// <summary>
    /// The frame callback. <b>This and not a loop is how a browser is driven</b>: the page paints
    /// between the calls, and a run that blocked would be a page that never painted at all.
    /// </summary>
    public static void Ticker(Func<bool> step)
    {
        Count();
        TickerJs(step);
    }

    /// <summary>Where the page was served from, which is what a path in the manifest is relative to.</summary>
    public static string Origin()
    {
        Count();
        return OriginJs();
    }

    [Conditional("DEBUG")]
    static void Count() => _crossings++;

    [JSImport("town.start", "town.js")]
    private static partial Task<string> StartJs(string wgsl);

    [JSImport("town.reserve", "town.js")]
    static partial void ReserveJs(int slot, int byteLength, int usage);

    [JSImport("town.buffer", "town.js")]
    static partial void BufferJs(int slot, [JSMarshalAs<JSType.MemoryView>] Span<byte> data, int usage);

    [JSImport("town.texture", "town.js")]
    static partial void TextureJs(
        int slot, [JSMarshalAs<JSType.MemoryView>] Span<byte> rgba,
        int width, int height, int layers, int layer, int level, int levels);

    [JSImport("town.rebuild", "town.js")]
    static partial void RebuildJs(int indexCount);

    [JSImport("town.release", "town.js")]
    static partial void ReleaseJs();

    [JSImport("town.frame", "town.js")]
    static partial void FrameJs(
        [JSMarshalAs<JSType.MemoryView>] Span<byte> camera,
        [JSMarshalAs<JSType.MemoryView>] Span<byte> sprites,
        [JSMarshalAs<JSType.MemoryView>] Span<byte> overlay,
        [JSMarshalAs<JSType.MemoryView>] Span<byte> underlay,
        int spriteCount, int overlayCount, int underlayCount);

    [JSImport("town.pump", "town.js")]
    static partial void PumpJs(
        [JSMarshalAs<JSType.MemoryView>] Span<byte> keys,
        [JSMarshalAs<JSType.MemoryView>] Span<double> axes);

    [JSImport("town.fullscreen", "town.js")]
    static partial void FullscreenJs();

    [JSImport("town.say", "town.js")]
    static partial void SayJs(string line);

    [JSImport("town.origin", "town.js")]
    private static partial string OriginJs();

    [JSImport("town.ticker", "town.js")]
    static partial void TickerJs([JSMarshalAs<JSType.Function<JSType.Boolean>>] Func<bool> step);
}
