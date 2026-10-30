using System.Diagnostics;
using System.Reflection;
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
/// on the far side of one call, and a browser has no fence to wait on. <b>What is counted is the calls
/// this side makes</b> (<see cref="Crossings"/>): the callback coming in is the browser's and is not among
/// them.
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
    public const int Storage = 128;
    public const int Indirect = 256;

    static long _crossings;

    /// <summary>
    /// Calls out through this wall since the page loaded — every method here, and never the animation
    /// callback coming in. DEBUG only, and free in a Release build for the same reason
    /// <see cref="Vk.Crossings"/> is.
    /// </summary>
    public static long Crossings => _crossings;

    /// <summary>The device, the canvas and the pipelines. Answers the reason it could not, or the empty string.</summary>
    public static Task<string> Start(string wgsl)
    {
        Count();
        return StartJs(wgsl);
    }

    /// <summary>The most device pixels the canvas is drawn at to the display's point (WEB-11), the canvas laid again at it.</summary>
    public static void MostPixelRatio(float ratio)
    {
        Count();
        MostPixelRatioJs(ratio);
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

    /// <summary>What the device was opened to hold the atlas in — RGBA, BC7 or ASTC (<c>SheetFormat</c>'s order) — as the page chose it off the adapter.</summary>
    public static int SheetFormat()
    {
        Count();
        return SheetFormatJs();
    }

    /// <summary>
    /// The atlas as blocks, every box written straight out of the block file the page fetched (<paramref name="path"/>,
    /// relative to the page): six numbers a box in <paramref name="boxes"/> — page, x, y, width, height, offset. Once an
    /// open, so no frame's crossing.
    /// </summary>
    public static void SheetBlocks(int slot, int format, int pages, string path, Span<int> boxes)
    {
        Count();
        SheetBlocksJs(slot, format, pages, path, System.Runtime.InteropServices.MemoryMarshal.AsBytes(boxes));
    }

    [JSImport("town.sheetFormat", "town.js")]
    private static partial int SheetFormatJs();

    [JSImport("town.sheetBlocks", "town.js")]
    static partial void SheetBlocksJs(int slot, int format, int pages, string path, [JSMarshalAs<JSType.MemoryView>] Span<byte> boxes);

    /// <summary>The bind group and the recording, made again when a map is opened and never while a frame is drawn.</summary>
    /// <param name="groundIndexCount">The index buffer's first draw: the ground, under every body.</param>
    /// <param name="aboveIndexCount">Its second, straight after the first: the level above, over the bodies on the ground.</param>
    /// <param name="overOffset">Where the sprites drawn over the buildings start in their buffer, in bytes.</param>
    /// <param name="aboveOffset">Where the sprites on the level above start in the same buffer, in bytes.</param>
    /// <param name="underlayAboveOffset">Where the marks on the level above start in the underlay's, in bytes.</param>
    /// <param name="marksAbove">Whether the ring of marks is drawn over the level above as well as under the bodies on the ground.</param>
    public static void Rebuild(
        int groundIndexCount, int aboveIndexCount, int overOffset, int aboveOffset, int underlayAboveOffset, bool marksAbove)
    {
        Count();
        RebuildJs(groundIndexCount, aboveIndexCount, overOffset, aboveOffset, underlayAboveOffset, marksAbove);
    }

    /// <summary>Everything the device holds for a town being taken down, and the recording that read it.</summary>
    public static void Release()
    {
        Count();
        ReleaseJs();
    }

    /// <summary>
    /// The engine layers' quads and draws — kernel buffers both, named as the page names them — drawn from the next recording
    /// on, each run <paramref name="runBytes"/> past the last; or none, for nought.
    /// </summary>
    public static void Engine(int quads, int draws, int runBytes)
    {
        Count();
        EngineJs(quads, draws, runBytes);
    }

    /// <summary>
    /// What moves, as the emit writes it — its instances and its two draws, kernel buffers both — drawn from the next
    /// recording on in place of the frame's own runs, the run above <paramref name="aboveBytes"/> into the instances.
    /// </summary>
    public static void Moving(int instances, int draws, int aboveBytes)
    {
        Count();
        MovingJs(instances, draws, aboveBytes);
    }

    /// <summary>
    /// The whole frame: the memory the simulation just wrote, and the counts that say how much of it to read — the
    /// sprites under the buildings, the stretch of the buildings laid once, the sprites over them and those on the level
    /// above, the interface, and the marks on the ground and on the level above — at <see cref="FrameCount"/>'s places;
    /// and the knots of the host's ring written since the last frame, at <see cref="FrameCount.MarkKnotsAt"/> bytes into
    /// the page's copy, with the ring's words — both empty where the device keeps the ring.
    /// </summary>
    public static void Frame(
        Span<byte> camera, Span<byte> under, Span<byte> over, Span<byte> above, Span<byte> overlay, Span<byte> underlay,
        Span<byte> underlayAbove, Span<byte> markKnots, Span<byte> markRing, Span<int> counts)
    {
        Count();
        FrameJs(camera, under, over, above, overlay, underlay, underlayAbove, markKnots, markRing, counts);
    }

    /// <summary>Where each of <see cref="Frame"/>'s counts stands (<c>town.js</c>'s <c>COUNT</c>).</summary>
    public static class FrameCount
    {
        public const int Under = 0, StandingFirst = 1, Standing = 2, Over = 3, Above = 4, Overlay = 5, Underlay = 6,
            UnderlayAbove = 7, MarkKnotsAt = 8, Length = 9;
    }

    /// <summary>The device's ring of knots and its words — kernel buffers both — drawn from the next recording on.</summary>
    public static void DeviceMarks(int knots, int ring)
    {
        Count();
        DeviceMarksJs(knots, ring);
    }

    /// <summary>What the page has seen since the last frame asked, into the run's own memory.</summary>
    /// <param name="inbox">Where what reached a served town's link since the last pump is written (<see cref="ServerLink"/>).</param>
    /// <param name="outbox">And what the run wrote to go, sent and emptied.</param>
    public static void Pump(Span<byte> keys, Span<double> axes, Span<byte> inbox, Span<byte> outbox)
    {
        Count();
        PumpJs(keys, axes, inbox, outbox);
    }

    /// <summary>A served town's socket opened (MP-3) — once a join, so no frame's crossing.</summary>
    public static void Join(string url)
    {
        Count();
        JoinJs(url);
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
    /// A town about to be stood up, said on the opening card (WEB-8) — brought back over the canvas, so a map picked on the
    /// menu is opened behind it as the first one was.
    /// </summary>
    public static void StandUp(string map)
    {
        Count();
        StandUpJs(map);
    }

    /// <summary>A stage of standing the town up passed (<c>OpenWatch</c>), for the card to say how far the open has got.</summary>
    public static void Standing(string stage)
    {
        Count();
        StandingJs(stage);
    }

    /// <summary>
    /// Once the browser has painted a frame: what an open done a stage at a time (<c>Staged</c>) awaits between two, the
    /// one way a page's card moves while the thread is the town's. At once in a tab nobody can see, which paints nothing.
    /// </summary>
    public static Task Painted()
    {
        Count();
        return PaintedJs();
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
    /// The frame callback, and the one run each time a batch of the device's lands. <b>This and not a loop is how a
    /// browser is driven</b>: the page paints between the calls, and a run that blocked would be a page that never
    /// painted at all — nor could it wait for the device, whose answer only lands once the run has handed the thread back.
    /// </summary>
    public static void Ticker(Func<bool> step, Action between)
    {
        Count();
        TickerJs(step, between);
    }

    /// <summary>
    /// Files asked for before anything waits on them, held on the far side until <see cref="Grab"/>
    /// reads each one out. The paths are one string, newline apart, and nothing is awaited: this
    /// returns when the fetches have been started and not when they arrive.
    /// </summary>
    /// <remarks>
    /// <b>It is what puts one download beside another (WEB-9).</b> A page fetching in the order it happens to
    /// read waits for the sum of what it asked for; the listing, the figures and the briefs are wanted one
    /// after the next at boot (<c>Data.Boot</c>), and the art is wanted while the runtime is still coming
    /// down (<c>main.js</c>).
    /// Nothing above this changes — <see cref="Grab"/> reads a prefetched file where it would have
    /// fetched one — and <b>a prefetch that fails costs an ordinary fetch and nothing else</b>.
    /// </remarks>
    public static void Prefetch(string paths)
    {
        Count();
        PrefetchJs(paths);
    }

    /// <summary>
    /// One archive of files, held on the far side exactly as a <see cref="Prefetch"/>ed file is, answering
    /// the paths it held — newline apart, because a list crosses the wall as one string.
    /// </summary>
    /// <remarks>
    /// <b>One round trip and not three hundred</b>, which is the whole of what it is for: the town's art
    /// is small files, so what a page waited on was latency and not bytes. The archive is a plain tar
    /// and the browser undoes the gzip, which is the one decompressor a page has that this runtime does
    /// not.
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
    /// <b>The same, for a file already on this side</b> — unpacked, or prefetched and arrived — answering −1 for one that
    /// is not, which <see cref="Grab"/> waits for. Synchronous, because every await is a turn of the page's event loop,
    /// and the art's nine hundred files a turn apiece were seconds of an open.
    /// </summary>
    public static int GrabHeld(string path)
    {
        Count();
        return GrabHeldJs(path);
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
    /// Every sheet of a batch, decoded at once out of the files the fetch is holding, and kept on that
    /// side under the names given — which are the run's own, as <see cref="Texels"/> asks for them.
    /// </summary>
    /// <remarks>
    /// <b>One at a time they are four times slower.</b> A loop awaiting each decode in turn is one
    /// decode at a time, and a browser decodes on threads a page has not got: the town's sheets are
    /// 216 ms in a row against 57 ms asked for together. It is the same half of the split
    /// <see cref="Picture"/> is — the half that can wait — and it is called where waiting is allowed.
    /// </remarks>
    public static Task Decode(string names, string saying)
    {
        Count();
        return DecodeJs(names, saying);
    }

    /// <summary>
    /// The texels of a picture <see cref="Picture"/> or <see cref="Decode"/> made, parked on the far
    /// side, answering how many
    /// bytes they came to. Synchronous, which is the whole reason the decode was done in two parts.
    /// </summary>
    public static int Texels(string path)
    {
        Count();
        return TexelsJs(path);
    }

    /// <summary>Whether the device the page was given can run the world's kernels (<c>compute.js</c>'s <c>NEEDED</c>).</summary>
    public static bool KernelsFit()
    {
        Count();
        return KernelsFitJs();
    }

    /// <summary>
    /// <b>The memory the run has been given</b>, in bytes: a page's only grows, so this is the most it has ever needed at
    /// once — an open's peak, read after it.
    /// </summary>
    public static long HeapBytes()
    {
        Count();
        return (long)HeapBytesJs();
    }

    /// <summary>
    /// <b>A buffer a kernel may bind</b>, made once with what reads it and never between steps; where the host writes or
    /// reads it, <paramref name="hostAddress"/> is the pinned memory of its own it does so in (<see cref="GpuBuffer"/>).
    /// </summary>
    public static void KernelBuffer(int id, int byteLength, int hostAddress)
    {
        Count();
        KernelBufferJs(id, byteLength, hostAddress);
    }

    public static void KernelBufferGone(int id)
    {
        Count();
        KernelBufferGoneJs(id);
    }

    /// <summary>Bytes laid into a kernel's buffer from <paramref name="offsetBytes"/> on, ahead of every step submitted after.</summary>
    public static void KernelWrite(int id, int offsetBytes, Span<byte> data)
    {
        Count();
        KernelWriteJs(id, offsetBytes, data);
    }

    /// <summary>One kernel's pipeline, off the WGSL the build wrote for it (<c>Kernels.Web.targets</c>).</summary>
    public static void KernelPipeline(int id, string name, string wgsl, int pushBytes)
    {
        Count();
        KernelPipelineJs(id, name, wgsl, pushBytes);
    }

    /// <summary>
    /// <b>Every kernel the build shipped, built on this device before any town asks for one</b> (WEB-10): a driver may
    /// refuse a kernel the limits allow, and a world stepped through a pipeline that failed is every batch refused. Answers
    /// the refusals, newline apart, or the empty string; where there is any, <see cref="KernelsFit"/> answers no from then on.
    /// </summary>
    public static Task<string> ProveKernels()
    {
        foreach (var resource in Assembly.GetExecutingAssembly().GetManifestResourceNames())
        {
            if (!resource.StartsWith(KernelResource, StringComparison.Ordinal) || !resource.EndsWith(".comp.wgsl", StringComparison.Ordinal))
            {
                continue;
            }

            var shader = resource[KernelResource.Length..^".wgsl".Length];
            Count();
            ProveKernelJs(shader, Vk.KernelSource(shader));
        }

        Count();
        return KernelsProvenJs();
    }

    const string KernelResource = "Shaders/";

    /// <summary>A kernel's buffers bound as one group, by their ids in binding order; made again in place under the same id.</summary>
    public static void KernelBind(int id, int kernel, Span<int> buffers)
    {
        Count();
        KernelBindJs(id, kernel, buffers);
    }

    /// <summary>
    /// <b>A batch of kernels, recorded on this side and submitted in one call</b> (<see cref="ComputeBatch"/>): the
    /// operations, the push constants they are run with, and where the page writes <paramref name="serial"/> once
    /// everything the batch reads back has landed in the host's own memory.
    /// </summary>
    public static void KernelSubmit(Span<int> operations, Span<byte> push, int serial, int landedAddress)
    {
        Count();
        KernelSubmitJs(operations, push, serial, landedAddress);
    }

    [Conditional("DEBUG")]
    static void Count() => _crossings++;

    [JSImport("town.kernelsFit", "town.js")]
    private static partial bool KernelsFitJs();

    [JSImport("town.heapBytes", "town.js")]
    [return: JSMarshalAs<JSType.Number>]
    private static partial double HeapBytesJs();

    [JSImport("town.kernelBuffer", "town.js")]
    static partial void KernelBufferJs(int id, int byteLength, int hostAddress);

    [JSImport("town.kernelBufferGone", "town.js")]
    static partial void KernelBufferGoneJs(int id);

    [JSImport("town.kernelWrite", "town.js")]
    static partial void KernelWriteJs(int id, int offsetBytes, [JSMarshalAs<JSType.MemoryView>] Span<byte> data);

    [JSImport("town.kernelPipeline", "town.js")]
    static partial void KernelPipelineJs(int id, string name, string wgsl, int pushBytes);

    [JSImport("town.proveKernel", "town.js")]
    static partial void ProveKernelJs(string name, string wgsl);

    [JSImport("town.kernelsProven", "town.js")]
    private static partial Task<string> KernelsProvenJs();

    [JSImport("town.kernelBind", "town.js")]
    static partial void KernelBindJs(int id, int kernel, [JSMarshalAs<JSType.MemoryView>] Span<int> buffers);

    [JSImport("town.kernelSubmit", "town.js")]
    static partial void KernelSubmitJs(
        [JSMarshalAs<JSType.MemoryView>] Span<int> operations, [JSMarshalAs<JSType.MemoryView>] Span<byte> push, int serial,
        int landedAddress);

    [JSImport("town.start", "town.js")]
    private static partial Task<string> StartJs(string wgsl);

    [JSImport("town.mostPixelRatio", "town.js")]
    static partial void MostPixelRatioJs(float ratio);

    [JSImport("town.reserve", "town.js")]
    static partial void ReserveJs(int slot, int byteLength, int usage);

    [JSImport("town.buffer", "town.js")]
    static partial void BufferJs(int slot, [JSMarshalAs<JSType.MemoryView>] Span<byte> data, int usage);

    [JSImport("town.texture", "town.js")]
    static partial void TextureJs(
        int slot, [JSMarshalAs<JSType.MemoryView>] Span<byte> rgba,
        int width, int height, int layers, int layer, int level, int levels);

    [JSImport("town.rebuild", "town.js")]
    static partial void RebuildJs(
        int groundIndexCount, int aboveIndexCount, int overOffset, int aboveOffset, int underlayAboveOffset, bool marksAbove);

    [JSImport("town.release", "town.js")]
    static partial void ReleaseJs();

    [JSImport("town.engine", "town.js")]
    static partial void EngineJs(int quads, int draws, int runBytes);

    [JSImport("town.moving", "town.js")]
    static partial void MovingJs(int instances, int draws, int aboveBytes);

    [JSImport("town.frame", "town.js")]
    static partial void FrameJs(
        [JSMarshalAs<JSType.MemoryView>] Span<byte> camera,
        [JSMarshalAs<JSType.MemoryView>] Span<byte> under,
        [JSMarshalAs<JSType.MemoryView>] Span<byte> over,
        [JSMarshalAs<JSType.MemoryView>] Span<byte> above,
        [JSMarshalAs<JSType.MemoryView>] Span<byte> overlay,
        [JSMarshalAs<JSType.MemoryView>] Span<byte> underlay,
        [JSMarshalAs<JSType.MemoryView>] Span<byte> underlayAbove,
        [JSMarshalAs<JSType.MemoryView>] Span<byte> markKnots,
        [JSMarshalAs<JSType.MemoryView>] Span<byte> markRing,
        [JSMarshalAs<JSType.MemoryView>] Span<int> counts);

    [JSImport("town.deviceMarks", "town.js")]
    static partial void DeviceMarksJs(int knots, int ring);

    [JSImport("town.pump", "town.js")]
    static partial void PumpJs(
        [JSMarshalAs<JSType.MemoryView>] Span<byte> keys,
        [JSMarshalAs<JSType.MemoryView>] Span<double> axes,
        [JSMarshalAs<JSType.MemoryView>] Span<byte> inbox,
        [JSMarshalAs<JSType.MemoryView>] Span<byte> outbox);

    [JSImport("town.join", "town.js")]
    static partial void JoinJs(string url);

    [JSImport("town.fullscreen", "town.js")]
    static partial void FullscreenJs();

    [JSImport("town.progress", "town.js")]
    static partial void ProgressJs(int done, int total);

    [JSImport("town.standUp", "town.js")]
    static partial void StandUpJs(string map);

    [JSImport("town.standing", "town.js")]
    static partial void StandingJs(string stage);

    [JSImport("town.painted", "town.js")]
    private static partial Task PaintedJs();

    [JSImport("town.shut", "town.js")]
    static partial void ShutJs();

    [JSImport("town.say", "town.js")]
    static partial void SayJs(string line);

    [JSImport("town.prefetch", "town.js")]
    static partial void PrefetchJs(string paths);

    [JSImport("town.unpack", "town.js")]
    private static partial Task<string> UnpackJs(string path);

    [JSImport("town.grab", "town.js")]
    private static partial Task<int> GrabJs(string path);

    [JSImport("town.grabHeld", "town.js")]
    private static partial int GrabHeldJs(string path);

    [JSImport("town.take", "town.js")]
    static partial void TakeJs([JSMarshalAs<JSType.MemoryView>] Span<byte> into);

    [JSImport("town.park", "town.js")]
    static partial void ParkJs([JSMarshalAs<JSType.MemoryView>] Span<byte> file);

    [JSImport("town.picture", "town.js")]
    private static partial Task PictureJs(string path);

    [JSImport("town.decode", "town.js")]
    private static partial Task DecodeJs(string names, string saying);

    [JSImport("town.texels", "town.js")]
    private static partial int TexelsJs(string path);

    [JSImport("town.ticker", "town.js")]
    static partial void TickerJs(
        [JSMarshalAs<JSType.Function<JSType.Boolean>>] Func<bool> step, [JSMarshalAs<JSType.Function>] Action between);
}
