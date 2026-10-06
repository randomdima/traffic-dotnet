using System.Runtime.InteropServices;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;

namespace TrafficSimulation.App.Render;

/// <summary>
/// The town's ground on a canvas: the same four draws the desktop records, recorded once into a render
/// bundle, with the counts in a buffer the CPU writes rather than in the calls.
/// </summary>
/// <remarks>
/// <para>
/// <b>WEB-1 — it is the same design and not a second one.</b> What fills the buffers — the ground mesh, the
/// sprites, the interface — is written once, above this, and is the desktop's own code; what is here
/// is only the submitting. A frame changes three numbers and the memory those numbers count, and the
/// recording never changes.
/// </para>
/// <para>
/// <b>What a browser has not got.</b> There is no mapped memory to write straight into, so the
/// instance buffers are ordinary managed arrays and a frame copies them across — one
/// <c>writeBuffer</c> a stream, from a window onto the heap they already live in. There is no fence
/// either, so <see cref="BlockedMs"/> is nothing — <b>this renderer never waits for anything</b>. What
/// a frame here waits for is the animation callback, which happens between one frame and the next
/// rather than inside either, and is measured where the frame is: <c>Game.Step</c> counts what a frame
/// waited before it began and adds it to the same figure. Reporting it from here would be reporting it
/// twice, and it would be subtracted out of a submit it was never part of.
/// </para>
/// </remarks>
internal sealed class TownRenderer : IDisposable
{
    /// <summary>The five the ground is painted with, each its own binding. See the Vulkan half for why they are not an array.</summary>
    const int Surfaces = 5;

    const int SheetSlots = 512;

    const int GroundStream = 0;
    const int IndexStream = 1;
    const int SpriteStream = 2;
    const int OverlayStream = 3;
    const int UnderlayStream = 4;
    const int CameraStream = 5;
    const int TableStream = 6;

    const int PagesTexture = 0;
    const int GlyphTexture = 1;
    const int FirstSurfaceTexture = 2;

    /// <summary>A uniform block is a multiple of sixteen bytes wide, and the camera is four pairs of floats and two fours.</summary>
    const int CameraBytes = 64;

    public const int OverlayCapacity = 65536;

    public const int UnderlayCapacity = OverlayCapacity;

    /// <summary>And how many marks past them on the level above, for the page's one run (<see cref="UnderlayAbove"/>).</summary>
    public const int UnderlayAboveCapacity = UnderlayCapacity / 4;

    readonly SheetAtlas _atlas;
    readonly byte[] _camera = new byte[CameraBytes];
    readonly byte[] _sprites;
    readonly byte[] _overlay;
    readonly byte[] _underlay;

    /// <summary>The ground it was laid for, kept so a part switched off can be packed out of the draw and back into it (<see cref="ShowGround"/>).</summary>
    readonly GroundMesh _mesh;

    int _indexCount;
    uint _shownParts = GroundParts.All;

    int _spriteCount;
    int _aboveCount;
    int _overlayCount;
    int _underlayCount;
    int _underlayAboveCount;

    TownRenderer(
        GroundMesh mesh, IReadOnlyList<string> surfaceTextures, IReadOnlyList<SheetSource> sheetTextures,
        int spriteCapacity, int aboveCapacity)
    {
        if (sheetTextures.Count > SheetSlots) throw new InvalidOperationException(
            $"{sheetTextures.Count} sheets, and the sprite shader's table holds {SheetSlots}.");

        SpriteCapacity = Math.Max(1, spriteCapacity);
        AboveCapacity = Math.Max(0, aboveCapacity);
        _sprites = new byte[(SpriteCapacity + AboveCapacity) * Marshal.SizeOf<SpriteInstance>()];
        _overlay = new byte[OverlayCapacity * Marshal.SizeOf<OverlayQuad>()];
        _underlay = new byte[(UnderlayCapacity + UnderlayAboveCapacity) * Marshal.SizeOf<OverlayQuad>()];

        _mesh = mesh;
        _indexCount = mesh.Indices.Length;
        WebGpu.Buffer(GroundStream, Bytes(mesh.Vertices), WebGpu.Vertex);
        WebGpu.Buffer(IndexStream, Bytes(mesh.Indices), WebGpu.Index);
        WebGpu.Reserve(SpriteStream, _sprites.Length, WebGpu.Vertex);
        WebGpu.Reserve(OverlayStream, _overlay.Length, WebGpu.Vertex);
        WebGpu.Reserve(UnderlayStream, _underlay.Length, WebGpu.Vertex);
        WebGpu.Reserve(CameraStream, CameraBytes, WebGpu.Uniform);

        // Every sheet onto the layers of one array texture. The table is laid to the shader's full
        // length, so a slot nothing uses is zero.
        _atlas = SheetAtlas.Pack(sheetTextures);
        var table = new SheetPlace[SheetSlots];
        _atlas.Places.CopyTo(table, 0);
        WebGpu.Buffer(TableStream, MemoryMarshal.AsBytes(table.AsSpan()), WebGpu.Uniform);

        Pages();
        Glyphs();
        Ground(surfaceTextures);

        WebGpu.Rebuild(_indexCount);
    }

    /// <summary>The town on a canvas, which is the only target a browser offers.</summary>
    public static TownRenderer OnScreen(
        GroundMesh mesh, IReadOnlyList<string> surfaceTextures, IReadOnlyList<SheetSource> sheetTextures,
        int spriteCapacity, int aboveCapacity) =>
        new(mesh, surfaceTextures, sheetTextures, spriteCapacity, aboveCapacity);

    /// <summary>How many sprites the instance buffer was laid for.</summary>
    public int SpriteCapacity { get; }

    /// <summary>And how many more past them for the bodies on the level above.</summary>
    public int AboveCapacity { get; }

    /// <summary>The instance buffer as the caller writes it. It is this engine's own memory, and the frame hands the browser a window onto it.</summary>
    public Span<SpriteInstance> Sprites => MemoryMarshal.Cast<byte, SpriteInstance>(_sprites.AsSpan())[..SpriteCapacity];

    /// <summary>
    /// <b>And the bodies on the level above</b>, past them. <b>The page draws the two as one run over the whole
    /// ground</b>, the level above included: no traced map reaches it (WEB-4), and no other map lays a level.
    /// </summary>
    public Span<SpriteInstance> SpritesAbove =>
        MemoryMarshal.Cast<byte, SpriteInstance>(_sprites.AsSpan()).Slice(SpriteCapacity, AboveCapacity);

    public Span<OverlayQuad> Overlay => MemoryMarshal.Cast<byte, OverlayQuad>(_overlay.AsSpan());

    public Span<OverlayQuad> Underlay => MemoryMarshal.Cast<byte, OverlayQuad>(_underlay.AsSpan())[..UnderlayCapacity];

    /// <summary>And the marks on the level above, past them — drawn in the one run with them, as the bodies are.</summary>
    public Span<OverlayQuad> UnderlayAbove =>
        MemoryMarshal.Cast<byte, OverlayQuad>(_underlay.AsSpan()).Slice(UnderlayCapacity, UnderlayAboveCapacity);

    /// <summary>How many triangles of the town's standing ground are being drawn.</summary>
    public int TriangleCount => _indexCount / 3;

    /// <summary>
    /// <b>Which of the ground's own layers are drawn</b> (OBS-2v), as a bit per <see cref="GroundPart"/>.
    /// The parts asked for are packed into one run and handed over as the index buffer again, which is
    /// the browser's counterpart of the desktop's shorter draw: a page has no indirect count to cut, so
    /// what the recording is made again for is the number of indices in it.
    /// </summary>
    public void ShowGround(uint parts)
    {
        if (parts == _shownParts) return;

        _shownParts = parts;
        var all = _mesh.Indices;
        var packed = new uint[all.Length];
        var written = 0;
        for (var part = 0; part < GroundParts.Count; part++)
        {
            if ((parts & (1u << part)) == 0) continue;

            var tally = _mesh.Parts[part];
            all.Slice(tally.FirstIndex, tally.IndexCount).CopyTo(packed.AsSpan(written));
            written += tally.IndexCount;
        }

        _indexCount = written;
        WebGpu.Buffer(IndexStream, MemoryMarshal.AsBytes(packed.AsSpan(0, written)), WebGpu.Index);
        WebGpu.Rebuild(_indexCount);
    }

    /// <summary>
    /// Nothing: a browser hands a frame to the compositor and never waits on a fence for it, so the
    /// desktop's figure has no counterpart here rather than a different value.
    /// </summary>
    public double BlockedMs => 0;

    /// <summary>How many of the instances just written are to be drawn. The only thing a frame changes about the sprite pass.</summary>
    public void SetSpriteCount(int count, int above)
    {
        _spriteCount = Math.Clamp(count, 0, SpriteCapacity);
        _aboveCount = Math.Clamp(above, 0, AboveCapacity);
    }

    public void SetOverlayCount(int count) => _overlayCount = Math.Clamp(count, 0, OverlayCapacity);

    public void SetUnderlayCount(int count, int above)
    {
        _underlayCount = Math.Clamp(count, 0, UnderlayCapacity);
        _underlayAboveCount = Math.Clamp(above, 0, UnderlayAboveCapacity);
    }

    /// <summary>
    /// The size of one frame of a sheet cut into a grid, as width over height. <b>The grid is the
    /// caller's</b>: what a sheet is cut into is a fact about the thing it draws.
    /// </summary>
    public float SheetFrameAspect(int sheet, int columns, int rows) =>
        (_atlas.Places[sheet].WidthPx / columns) / (_atlas.Places[sheet].HeightPx / rows);

    /// <summary>The whole image's width over its height, for the sheets that are one picture rather than a grid.</summary>
    public float SheetAspect(int sheet) => _atlas.Places[sheet].WidthPx / _atlas.Places[sheet].HeightPx;

    /// <summary>One frame. Everything that changes between frames is in the arrays this hands over, and the recording is untouched.</summary>
    public void Frame(CameraView view)
    {
        // The bodies above moved down to follow the ground's, so the page's one run is both.
        var size = Marshal.SizeOf<SpriteInstance>();
        _sprites.AsSpan(SpriteCapacity * size, _aboveCount * size).CopyTo(_sprites.AsSpan(_spriteCount * size));
        var drawn = _spriteCount + _aboveCount;

        // And the marks above after the ground's, the same way.
        var quad = Marshal.SizeOf<OverlayQuad>();
        _underlay.AsSpan(UnderlayCapacity * quad, _underlayAboveCount * quad).CopyTo(_underlay.AsSpan(_underlayCount * quad));
        var marks = _underlayCount + _underlayAboveCount;

        var camera = view with { SurfacePeriodsM = _mesh.SurfacePeriodsM, WaterPeriodM = _mesh.WaterPeriodM };
        MemoryMarshal.Write(_camera, in camera);
        WebGpu.Frame(
            _camera,
            _sprites.AsSpan(0, drawn * size),
            _overlay.AsSpan(0, _overlayCount * quad),
            _underlay.AsSpan(0, marks * quad),
            drawn, _overlayCount, marks);
    }

    /// <summary>
    /// Nothing: the canvas reconfigures its own surface when it is resized, and the recording is made
    /// of formats rather than of sizes, so a resize costs the browser a swapchain and this engine
    /// nothing at all.
    /// </summary>
    public void Recreate()
    {
    }

    /// <summary>
    /// Never asked for: a frame is read back for a drive steered from a file on a disk (DRV-7), and a page
    /// has neither.
    /// </summary>
    public void Shot(string path, int widestPx = 0) =>
        throw new PlatformNotSupportedException("A page has no disk to write a frame to.");

    /// <summary>
    /// The buffers and the pictures, given back. <b>It matters here more than it looks</b>: opening a
    /// map builds the next renderer and disposes this one, and an atlas is two hundred megabytes on
    /// the device — holding two of them at once is how a page loses its adapter outright.
    /// </summary>
    public void Dispose() => WebGpu.Release();

    /// <summary>
    /// The atlas, a page at a time. The page's memory is one buffer reused, so a fifty-megapixel
    /// atlas never has more than one page of it in the heap at a time.
    /// </summary>
    void Pages()
    {
        var page = new byte[SheetAtlas.PagePx * SheetAtlas.PagePx * Marshal.SizeOf<Texel>()];
        var texels = MemoryMarshal.Cast<byte, Texel>(page.AsSpan());
        for (var at = 0; at < _atlas.Pages; at++)
        {
            _atlas.FillPage(at, texels);
            WebGpu.Texture(PagesTexture, page, SheetAtlas.PagePx, SheetAtlas.PagePx, _atlas.Pages, at, level: 0, levels: 1);
        }
    }

    /// <summary>
    /// The typeface. <b>Its bytes are in the assembly and its bitmap was made at boot</b>, under the
    /// resource's own name rather than a path (<see cref="Main.Data"/>) — so the size is read off the
    /// header here, which is the one thing the page's decoder is not asked for.
    /// </summary>
    void Glyphs()
    {
        using var stream = typeof(TownRenderer).Assembly.GetManifestResourceStream(GlyphSheet.Resource)
                           ?? throw new InvalidOperationException(
                               $"No embedded resource {GlyphSheet.Resource}: did the project file include it?");

        Span<byte> head = stackalloc byte[32];
        var read = stream.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
        var (widthPx, heightPx) = ImageHeader.Measure(head[..read], GlyphSheet.Resource);
        Picture(GlyphTexture, GlyphSheet.Resource, widthPx, heightPx, mipped: false);
    }

    /// <summary>
    /// What the ground is drawn from, or stand-ins where there is no ground — the five bindings the
    /// shader declares must be filled whether or not anything samples them.
    /// </summary>
    /// <remarks>
    /// <b>The menu's renderer takes the stand-ins</b>, and it is what lets a page open on one file: the
    /// surfaces are the only pictures a town-less run would otherwise have to fetch, and it draws no
    /// ground to put them on (<see cref="Main.Data.Boot"/>).
    /// </remarks>
    void Ground(IReadOnlyList<string> surfaceTextures)
    {
        for (var surface = 0; surface < Surfaces; surface++)
        {
            if (surfaceTextures.Count == 0)
            {
                Upload(FirstSurfaceTexture + surface, new Texel[1], 1, 1, mipped: false);
                continue;
            }

            Picture(FirstSurfaceTexture + surface, surfaceTextures[Math.Min(surface, surfaceTextures.Count - 1)],
                mipped: true);
        }
    }

    /// <summary>
    /// A copy of what the mesh holds, because what crosses the wall must be memory the browser may
    /// write a view over and a mesh hands out a read-only one. It is a copy made twice in a run.
    /// </summary>
    static byte[] Bytes<T>(ReadOnlySpan<T> from) where T : unmanaged
    {
        var bytes = new byte[from.Length * Marshal.SizeOf<T>()];
        MemoryMarshal.AsBytes(from).CopyTo(bytes);
        return bytes;
    }

    /// <summary>One picture from a file: its size off the header, its texels off the page's decoder.</summary>
    static void Picture(int slot, string path, bool mipped)
    {
        var (widthPx, heightPx) = ImageHeader.Measure(path);
        Picture(slot, path, widthPx, heightPx, mipped);
    }

    /// <summary>The same, where the size has already been read — the typeface, whose file is a resource.</summary>
    static void Picture(int slot, string named, int widthPx, int heightPx, bool mipped)
    {
        var top = new Texel[widthPx * heightPx];
        Texels.Decode(named, top);
        Upload(slot, top, widthPx, heightPx, mipped);
    }

    /// <summary>Texels in hand and, where it is mipped, every level under them — box-filtered here, as the desktop's are.</summary>
    static void Upload(int slot, Texel[] top, int widthPx, int heightPx, bool mipped)
    {
        var chain = mipped ? MipChain.Build(top, widthPx, heightPx) : [(top, widthPx, heightPx)];
        for (var level = 0; level < chain.Count; level++)
        {
            var (pixels, width, height) = chain[level];
            WebGpu.Texture(
                slot, MemoryMarshal.AsBytes(pixels.AsSpan()), width, height, layers: 1, layer: 0,
                level, chain.Count);
        }
    }
}
