using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TrafficSimulation.App.Screen;
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
/// either, so <see cref="BlockedMs"/> is nothing: what a frame waits for is the animation callback,
/// and that wait is the browser's rather than this engine's.
/// </para>
/// </remarks>
internal sealed class TownRenderer : IDisposable
{
    /// <summary>The five the ground is painted with, each its own binding. See the Vulkan half for why they are not an array.</summary>
    const int Surfaces = 5;

    const int SheetSlots = 192;

    const int GroundStream = 0;
    const int IndexStream = 1;
    const int SpriteStream = 2;
    const int OverlayStream = 3;
    const int UnderlayStream = 4;
    const int CameraStream = 5;
    const int TableStream = 6;

    const int PagesTexture = 0;
    const int GlyphTexture = 1;
    const int TileTexture = 2;
    const int FirstSurfaceTexture = 3;

    /// <summary>A uniform block is a multiple of sixteen bytes wide, and the camera is twenty-four.</summary>
    const int CameraBytes = 32;

    public const int OverlayCapacity = 65536;

    public const int UnderlayCapacity = OverlayCapacity;

    readonly SheetAtlas _atlas;
    readonly byte[] _camera = new byte[CameraBytes];
    readonly byte[] _sprites;
    readonly byte[] _overlay;
    readonly byte[] _underlay;
    readonly int _indexCount;

    int _spriteCount;
    int _overlayCount;
    int _underlayCount;

    TownRenderer(
        GroundMesh mesh, IReadOnlyList<string> surfaceTextures, IReadOnlyList<SheetSource> sheetTextures,
        int spriteCapacity)
    {
        if (sheetTextures.Count > SheetSlots) throw new InvalidOperationException(
            $"{sheetTextures.Count} sheets, and the sprite shader's table holds {SheetSlots}.");

        SpriteCapacity = Math.Max(1, spriteCapacity);
        _sprites = new byte[SpriteCapacity * Marshal.SizeOf<SpriteInstance>()];
        _overlay = new byte[OverlayCapacity * Marshal.SizeOf<OverlayQuad>()];
        _underlay = new byte[UnderlayCapacity * Marshal.SizeOf<OverlayQuad>()];

        _indexCount = mesh.Indices.Length;
        WebGpu.Buffer(GroundStream, Bytes(mesh.Vertices), WebGpu.Vertex);
        WebGpu.Buffer(IndexStream, Bytes(mesh.Indices), WebGpu.Index);
        WebGpu.Reserve(SpriteStream, _sprites.Length, WebGpu.Vertex);
        WebGpu.Reserve(OverlayStream, _overlay.Length, WebGpu.Vertex);
        WebGpu.Reserve(UnderlayStream, _underlay.Length, WebGpu.Vertex);
        WebGpu.Reserve(CameraStream, CameraBytes, WebGpu.Uniform);

        // Every sheet onto the layers of one array texture, and the one that tiles onto a texture of
        // its own. The table is laid to the shader's full length, so a slot nothing uses is zero.
        _atlas = SheetAtlas.Pack(sheetTextures);
        var table = new SheetPlace[SheetSlots];
        _atlas.Places.CopyTo(table, 0);
        WebGpu.Buffer(TableStream, MemoryMarshal.AsBytes(table.AsSpan()), WebGpu.Uniform);

        Pages();
        Glyphs();
        Tile(sheetTextures);
        for (var surface = 0; surface < Surfaces; surface++)
        {
            var path = surfaceTextures[Math.Min(surface, surfaceTextures.Count - 1)];
            using var decoded = Image.Load<Rgba32>(path);
            Picture(FirstSurfaceTexture + surface, decoded, mipped: true);
        }

        WebGpu.Rebuild(_indexCount);
    }

    /// <summary>The town on a canvas, which is the only target a browser offers.</summary>
    public static TownRenderer OnScreen(
        GroundMesh mesh, IReadOnlyList<string> surfaceTextures, IReadOnlyList<SheetSource> sheetTextures,
        int spriteCapacity) =>
        new(mesh, surfaceTextures, sheetTextures, spriteCapacity);

    /// <summary>How many sprites the instance buffer was laid for.</summary>
    public int SpriteCapacity { get; }

    /// <summary>The instance buffer as the caller writes it. It is this engine's own memory, and the frame hands the browser a window onto it.</summary>
    public Span<SpriteInstance> Sprites => MemoryMarshal.Cast<byte, SpriteInstance>(_sprites.AsSpan());

    public Span<OverlayQuad> Overlay => MemoryMarshal.Cast<byte, OverlayQuad>(_overlay.AsSpan());

    public Span<OverlayQuad> Underlay => MemoryMarshal.Cast<byte, OverlayQuad>(_underlay.AsSpan());

    /// <summary>How many triangles the town's standing ground came to.</summary>
    public int TriangleCount => _indexCount / 3;

    /// <summary>
    /// Nothing: a browser hands a frame to the compositor and never waits on a fence for it, so the
    /// desktop's figure has no counterpart here rather than a different value.
    /// </summary>
    public double BlockedMs => 0;

    /// <summary>How many of the instances just written are to be drawn. The only thing a frame changes about the sprite pass.</summary>
    public void SetSpriteCount(int count) => _spriteCount = Math.Clamp(count, 0, SpriteCapacity);

    public void SetOverlayCount(int count) => _overlayCount = Math.Clamp(count, 0, OverlayCapacity);

    public void SetUnderlayCount(int count) => _underlayCount = Math.Clamp(count, 0, UnderlayCapacity);

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
        MemoryMarshal.Write(_camera, in view);
        WebGpu.Frame(
            _camera,
            _sprites.AsSpan(0, _spriteCount * Marshal.SizeOf<SpriteInstance>()),
            _overlay.AsSpan(0, _overlayCount * Marshal.SizeOf<OverlayQuad>()),
            _underlay.AsSpan(0, _underlayCount * Marshal.SizeOf<OverlayQuad>()),
            _spriteCount, _overlayCount, _underlayCount);
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
        var page = new byte[SheetAtlas.PagePx * SheetAtlas.PagePx * Marshal.SizeOf<Rgba32>()];
        var texels = MemoryMarshal.Cast<byte, Rgba32>(page.AsSpan());
        for (var at = 0; at < _atlas.Pages; at++)
        {
            _atlas.FillPage(at, texels);
            WebGpu.Texture(PagesTexture, page, SheetAtlas.PagePx, SheetAtlas.PagePx, _atlas.Pages, at, level: 0, levels: 1);
        }
    }

    void Glyphs()
    {
        using var stream = typeof(TownRenderer).Assembly.GetManifestResourceStream(GlyphSheet.Resource)
                           ?? throw new InvalidOperationException(
                               $"No embedded resource {GlyphSheet.Resource}: did the project file include it?");
        using var decoded = Image.Load<Rgba32>(stream);
        Picture(GlyphTexture, decoded, mipped: false);
    }

    /// <summary>
    /// The one sheet the atlas could not hold, and a stand-in where the town has none: a binding the
    /// shader declares must be filled whether or not anything samples it.
    /// </summary>
    void Tile(IReadOnlyList<SheetSource> sheets)
    {
        if (_atlas.TileSheet < 0)
        {
            using var nothing = new Image<Rgba32>(1, 1);
            Picture(TileTexture, nothing, mipped: false);
            return;
        }

        var tile = sheets[_atlas.TileSheet];
        using var decoded = tile.Path is { } path
            ? Image.Load<Rgba32>(path)
            : Image.LoadPixelData<Rgba32>(tile.Rgba!, tile.WidthPx, tile.HeightPx);
        Picture(TileTexture, decoded, tile.Mipped);
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

    /// <summary>One picture and, where it is mipped, every level of it — box-filtered here, as the desktop's are.</summary>
    static void Picture(int slot, Image<Rgba32> decoded, bool mipped)
    {
        var top = new Rgba32[decoded.Width * decoded.Height];
        decoded.CopyPixelDataTo(top);

        var chain = mipped
            ? MipChain.Build(top, decoded.Width, decoded.Height)
            : [(top, decoded.Width, decoded.Height)];
        for (var level = 0; level < chain.Count; level++)
        {
            var (pixels, width, height) = chain[level];
            WebGpu.Texture(
                slot, MemoryMarshal.AsBytes(pixels.AsSpan()), width, height, layers: 1, layer: 0,
                level, chain.Count);
        }
    }
}
