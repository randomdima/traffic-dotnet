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

    /// <summary>
    /// How far through a countable stage the opening is (WEB-8). A total of zero is a stage nothing can
    /// count, and the bar sweeps rather than filling.
    /// </summary>
    public static void Progress(int done, int total)
    {
        Count();
        ProgressJs(done, total);
    }

    /// <summary>
    /// The tab, closed. <b>A browser grants this only to a page it opened itself</b>, so a tab somebody
    /// followed a link into stays open and the banner is what says the run has stopped
    /// (<see cref="AppWindow.Close"/>).
    /// </summary>
    public static void Shut()
    {
        Count();
        ShutJs();
    }

    /// <summary>
    /// What the desktop puts on stdout, where a page can be read (WEB-8): the opening card's own line
    /// while the opening is up, and a banner under the canvas once the town is behind it. An empty
    /// line is this run saying it has finished opening, and it is what takes the card away.
    /// </summary>
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

    /// <summary>
    /// A whole batch of files, in flight at once and held on the far side until <see cref="Grab"/>
    /// reads each one out. The paths are one string, newline apart, and <paramref name="saying"/> is
    /// what the banner counts them off under.
    /// </summary>
    /// <remarks>
    /// <b>This is what a page's opening costs, and it is latency and not bytes.</b> A fetch is a round
    /// trip before it is a byte, and the town's art is three hundred small files: asked for one after
    /// the next that is a minute of waiting against a second of downloading. Nothing above this changes
    /// — <see cref="Grab"/> reads a warmed file where it would have fetched one — and nothing here is
    /// on a frame's path, so the batch may cross the wall as slowly as it likes.
    /// </remarks>
    public static Task Warm(string paths, string saying)
    {
        Count();
        return WarmJs(paths, saying);
    }

    /// <summary>
    /// One archive of files, held on the far side exactly as a <see cref="Warm"/>ed batch is, answering
    /// the paths it held — newline apart, because a list crosses the wall as one string.
    /// </summary>
    /// <remarks>
    /// <b>One round trip and not three hundred</b>, which is the whole of what it is for: the town's art
    /// is small files, so what a page waited on was latency and not bytes. The archive is a plain tar
    /// and the browser undoes the gzip, which is the one decompressor a page has that this runtime does
    /// not — the same fact that keeps the towns on gzip rather than brotli.
    /// </remarks>
    public static Task<string> Unpack(string path)
    {
        Count();
        return UnpackJs(path);
    }

    /// <summary>
    /// One file the page was served, parked on the far side, answering how many bytes it came to. The
    /// path is relative to the page, which is what a path in the manifest is written against.
    /// </summary>
    public static Task<int> Grab(string path)
    {
        Count();
        return GrabJs(path);
    }

    /// <summary>
    /// The bytes the last <see cref="Grab"/> parked, into memory this side made at the length it
    /// answered. <b>Two calls and not one</b>: a <see cref="JSType.MemoryView"/> is a window onto this
    /// heap handed out, and there is no shape that hands one back.
    /// </summary>
    public static void Take(Span<byte> into)
    {
        Count();
        TakeJs(into);
    }

    /// <summary>
    /// Bytes out of this run's own memory, parked on the far side exactly as a fetched file is — the
    /// glyph sheet, which ships inside the assembly and so is never grabbed.
    /// </summary>
    public static void Park(Span<byte> file)
    {
        Count();
        ParkJs(file);
    }

    /// <summary>
    /// The parked file, decoded by the browser and kept on its side under <paramref name="path"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the half of a decode that has to wait</b>, and it is called where waiting is allowed:
    /// <c>createImageBitmap</c> is a promise, and the atlas that wants the texels is filled from inside
    /// a frame. What is kept is a bitmap and not a page of texels — the browser stores that as it
    /// pleases, and this heap holds none of it until <see cref="Texels"/> asks for one sheet.
    /// </para>
    /// <para>
    /// <b>It reads what was parked rather than taking the bytes</b>, so the art costs no copy at all:
    /// <see cref="Grab"/> has already put the file on that side, and <see cref="Take"/> leaves it there.
    /// A view cannot cross on a call that returns a promise in any case — the heap moves under it while
    /// the promise is outstanding, which is the same rule that made <see cref="Take"/> a second call.
    /// </para>
    /// </remarks>
    public static Task Picture(string path)
    {
        Count();
        return PictureJs(path);
    }

    /// <summary>
    /// The texels of a picture <see cref="Picture"/> decoded, parked on the far side, answering how many
    /// bytes they came to. Synchronous, which is the whole reason the decode was done in two parts.
    /// </summary>
    public static int Texels(string path)
    {
        Count();
        return TexelsJs(path);
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

    [JSImport("town.progress", "town.js")]
    static partial void ProgressJs(int done, int total);

    [JSImport("town.shut", "town.js")]
    static partial void ShutJs();

    [JSImport("town.say", "town.js")]
    static partial void SayJs(string line);

    [JSImport("town.warm", "town.js")]
    private static partial Task WarmJs(string paths, string saying);

    [JSImport("town.unpack", "town.js")]
    private static partial Task<string> UnpackJs(string path);

    [JSImport("town.grab", "town.js")]
    private static partial Task<int> GrabJs(string path);

    [JSImport("town.take", "town.js")]
    static partial void TakeJs([JSMarshalAs<JSType.MemoryView>] Span<byte> into);

    [JSImport("town.park", "town.js")]
    static partial void ParkJs([JSMarshalAs<JSType.MemoryView>] Span<byte> file);

    [JSImport("town.picture", "town.js")]
    private static partial Task PictureJs(string path);

    [JSImport("town.texels", "town.js")]
    private static partial int TexelsJs(string path);

    [JSImport("town.ticker", "town.js")]
    static partial void TickerJs([JSMarshalAs<JSType.Function<JSType.Boolean>>] Func<bool> step);
}
