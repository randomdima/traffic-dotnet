using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Terrain;

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
    /// <summary>
    /// The nine the ground is painted with, in the order <see cref="Surface"/> files them — the track's strip, the shore
    /// and the two kerbs' strips last — each its own binding. See the Vulkan half for why they are not an array.
    /// </summary>
    const int Surfaces = 9;

    const int GroundStream = 0;
    const int IndexStream = 1;
    const int SpriteStream = 2;
    const int OverlayStream = 3;
    const int UnderlayStream = 4;
    const int CameraStream = 5;
    const int TableStream = 6;
    const int StandingStream = 7;
    const int FanStream = 8;

    const int PagesTexture = 0;
    const int GlyphTexture = 1;
    const int FirstSurfaceTexture = 2;

    /// <summary>A bridge's art (<see cref="BridgeSheet"/>), after the surfaces.</summary>
    const int BridgeTexture = FirstSurfaceTexture + Surfaces;

    static readonly int CameraBytes = Unsafe.SizeOf<CameraView>();

    public const int OverlayCapacity = 65536;

    public const int UnderlayCapacity = OverlayCapacity;

    /// <summary>And how many marks past them on the level above (<see cref="UnderlayAbove"/>).</summary>
    public const int UnderlayAboveCapacity = UnderlayCapacity / 4;

    readonly SheetAtlas _atlas;
    readonly byte[] _camera = new byte[CameraBytes];
    readonly byte[] _sprites;
    readonly byte[] _overlay;
    readonly byte[] _underlay;

    /// <summary>The ground it was laid for, kept so a part switched off can be packed out of the draw and back into it (<see cref="ShowGround"/>).</summary>
    readonly GroundMesh _mesh;

    /// <summary>
    /// The index buffer's two draws (TER-7b, PHY-1a): every part shown but the level above, then the level above's, which
    /// is the mesh's last part and drawn over the bodies on the ground.
    /// </summary>
    int _groundIndexCount, _aboveIndexCount;

    uint _shownParts = GroundParts.All;

    SpriteCounts _spriteCounts;
    int _overlayCount;
    int _underlayCount;
    int _underlayAboveCount;

    TownRenderer(
        GroundMesh mesh, IReadOnlyList<string> surfaceTextures, IReadOnlyList<SheetSource> sheetTextures, SpriteRoom room)
    {
        if (sheetTextures.Count > SheetAtlas.Slots) throw new InvalidOperationException(
            $"{sheetTextures.Count} sheets, and the sprite shader's table holds {SheetAtlas.Slots}.");

        // A slot a run at the least: the level above is bound at its own offset, which a buffer's end is not.
        Room = room with { Under = Math.Max(1, room.Under), Over = Math.Max(1, room.Over), Above = Math.Max(1, room.Above) };
        _sprites = new byte[Room.Written * Unsafe.SizeOf<SpriteInstance>()];
        _overlay = new byte[OverlayCapacity * Unsafe.SizeOf<OverlayQuad>()];
        _underlay = new byte[(UnderlayCapacity + UnderlayAboveCapacity) * Unsafe.SizeOf<OverlayQuad>()];

        _mesh = mesh;
        _aboveIndexCount = mesh.Parts[(int)GroundPart.Above].IndexCount;
        _groundIndexCount = mesh.Indices.Length - _aboveIndexCount;
        WebGpu.Buffer(GroundStream, Bytes(mesh.Vertices), WebGpu.Vertex);
        WebGpu.Buffer(IndexStream, Bytes(mesh.Indices), WebGpu.Index);
        WebGpu.Reserve(SpriteStream, _sprites.Length, WebGpu.Vertex);
        WebGpu.Reserve(StandingStream, Math.Max(1, Room.Standing) * Unsafe.SizeOf<SpriteInstance>(), WebGpu.Vertex);
        WebGpu.Reserve(OverlayStream, _overlay.Length, WebGpu.Vertex);
        WebGpu.Reserve(UnderlayStream, _underlay.Length, WebGpu.Vertex);
        WebGpu.Reserve(CameraStream, CameraBytes, WebGpu.Uniform);
        WebGpu.Reserve(FanStream, FanTable.Words * sizeof(uint), WebGpu.Uniform);

        // Every sheet onto the layers of one array texture. The table is laid to the shader's full
        // length, so a slot nothing uses is zero.
        _atlas = SheetAtlas.Pack(sheetTextures);
        var table = new SheetPlace[SheetAtlas.Slots];
        _atlas.Places.CopyTo(table, 0);
        WebGpu.Buffer(TableStream, MemoryMarshal.AsBytes(table.AsSpan()), WebGpu.Uniform);

        Pages();
        Glyphs();
        Ground(surfaceTextures);

        Rebuild();
    }

    /// <summary>The town on a canvas, which is the only target a browser offers.</summary>
    public static TownRenderer OnScreen(
        GroundMesh mesh, IReadOnlyList<string> surfaceTextures, IReadOnlyList<SheetSource> sheetTextures, SpriteRoom room) =>
        new(mesh, surfaceTextures, sheetTextures, room);

    /// <summary>How many instances each run of the sprite pass was laid for.</summary>
    public SpriteRoom Room { get; }

    /// <summary>
    /// The instance buffer, run by run. It is this engine's own memory, and the frame hands the browser a window onto it.
    /// <b>What lies under is written here</b> (<see cref="FillUnder"/>): a page draws the marks and the scenery as
    /// instances, where the desktop draws them out of rows of their own.
    /// </summary>
    Span<SpriteInstance> SpritesUnder => MemoryMarshal.Cast<byte, SpriteInstance>(_sprites.AsSpan())[..Room.Under];

    public Span<SpriteInstance> SpritesOver => MemoryMarshal.Cast<byte, SpriteInstance>(_sprites.AsSpan()).Slice(Room.Under, Room.Over);

    /// <summary>
    /// <b>And the bodies on the level above</b>, past them: drawn after the level above's ground, as the desktop draws
    /// them — a train on a rail bridge over one under it.
    /// </summary>
    public Span<SpriteInstance> SpritesAbove =>
        MemoryMarshal.Cast<byte, SpriteInstance>(_sprites.AsSpan()).Slice(Room.Under + Room.Over, Room.Above);

    /// <summary>
    /// The town's buildings and props handed to the page once, as the desktop's are (<see cref="StandingSprites.Range"/>),
    /// and the looks a frame's marks and scenery are written from, and which sheets are car bodies the vertex stage tilts
    /// (<see cref="CameraView.CarTilt"/>). A page draws every prop it is handed.
    /// </summary>
    public void LayTown(TownSprites looks, ViewFigures view)
    {
        _looks = looks;
        _carTilt = new Vector4(
            looks.FirstCarSheet, looks.FirstCarSheet + (2 * looks.Cars.SheetCount), view.CarTiltEyeHeightM, 0f);
        var standing = looks.Standing.Instances;
        var count = Math.Min(standing.Length, Math.Max(1, Room.Standing));
        if (count == 0) return;

        // A buffer given anew, which the Rebuild below binds.
        WebGpu.Buffer(FanStream, MemoryMarshal.AsBytes(looks.Fans.AsSpan()), WebGpu.Uniform);

        WebGpu.Buffer(StandingStream, Bytes<SpriteInstance>(standing.Span[..count]), WebGpu.Vertex);
        Rebuild();
    }

    /// <summary>What lies under everything that stands, written as instances into the run under it; answers how many.</summary>
    public int FillUnder(DriftMarks marks, Vector2 viewCentreM, Vector2 viewSpanM, float pixelsPerMetre) =>
        _underCount = _looks?.FillUnder(marks, viewCentreM, viewSpanM, SpritesUnder) ?? 0;

    TownSprites? _looks;
    int _underCount;

    /// <summary>The car bodies' sheets from and past, and the height their tilt is seen from (<see cref="CameraView.CarTilt"/>).</summary>
    Vector4 _carTilt;

    /// <summary>
    /// <b>The engine's own tables drawn where its kernels wrote them</b> (<see cref="IEngineDraw"/>), from the next frame on —
    /// its kernels run in a batch of their own ahead of each frame (<c>Game.Web.cs</c>) — or none of it, for <c>null</c>.
    /// </summary>
    public void DrawTheEngineFrom(IEngineDraw? engine) =>
        WebGpu.Engine(engine?.Quads.Id ?? 0, engine?.Draws.Id ?? 0, (engine?.RunRoom ?? 0) * Unsafe.SizeOf<OverlayQuad>());

    /// <summary>The recording again, over the buffers as they now are: a buffer given anew is a buffer the last recording does not read.</summary>
    void Rebuild() => WebGpu.Rebuild(
        _groundIndexCount, _aboveIndexCount, Room.Under * Unsafe.SizeOf<SpriteInstance>(),
        (Room.Under + Room.Over) * Unsafe.SizeOf<SpriteInstance>(), UnderlayCapacity * Unsafe.SizeOf<OverlayQuad>());

    public Span<OverlayQuad> Overlay => MemoryMarshal.Cast<byte, OverlayQuad>(_overlay.AsSpan());

    public Span<OverlayQuad> Underlay => MemoryMarshal.Cast<byte, OverlayQuad>(_underlay.AsSpan())[..UnderlayCapacity];

    /// <summary>And the marks on the level above, past them — drawn over its ground and under the bodies on it.</summary>
    public Span<OverlayQuad> UnderlayAbove =>
        MemoryMarshal.Cast<byte, OverlayQuad>(_underlay.AsSpan()).Slice(UnderlayCapacity, UnderlayAboveCapacity);

    /// <summary>How many triangles of the town's standing ground are being drawn.</summary>
    public int TriangleCount => (_groundIndexCount + _aboveIndexCount) / 3;

    /// <summary>
    /// <b>Which of the ground's own layers are drawn</b> (OBS-2v), as a bit per <see cref="GroundPart"/>.
    /// The parts asked for are packed into one run and handed over as the index buffer again, which is
    /// the browser's counterpart of the desktop's shorter draw: a page has no indirect count to cut, so
    /// what the recording is made again for is the number of indices in it. The level above is the last part, so it
    /// stays the packed run's last.
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

        _aboveIndexCount = (parts & GroundParts.Bit(GroundPart.Above)) != 0 ? _mesh.Parts[(int)GroundPart.Above].IndexCount : 0;
        _groundIndexCount = written - _aboveIndexCount;
        WebGpu.Buffer(IndexStream, MemoryMarshal.AsBytes(packed.AsSpan(0, written)), WebGpu.Index);
        Rebuild();
    }

    /// <summary>
    /// Nothing: a browser hands a frame to the compositor and never waits on a fence for it, so the
    /// desktop's figure has no counterpart here rather than a different value.
    /// </summary>
    public double BlockedMs => 0;

    /// <summary>What the last <see cref="Frame"/> handed the page's buffers: the camera and every run and quad it counted.</summary>
    public long WrittenBytes { get; private set; }

    /// <summary>How much of each run is to be drawn. The only thing a frame changes about the sprite pass.</summary>
    public void SetSpriteCount(in SpriteCounts counts) => _spriteCounts = new SpriteCounts(
        Math.Clamp(_underCount, 0, Room.Under), Math.Clamp(counts.StandingFirst, 0, Room.Standing),
        Math.Clamp(counts.StandingCount, 0, Room.Standing), Math.Clamp(counts.Over, 0, Room.Over),
        Math.Clamp(counts.Above, 0, Room.Above));

    public void SetOverlayCount(int count) => _overlayCount = Math.Clamp(count, 0, OverlayCapacity);

    public void SetUnderlayCount(int count, int above)
    {
        _underlayCount = Math.Clamp(count, 0, UnderlayCapacity);
        _underlayAboveCount = Math.Clamp(above, 0, UnderlayAboveCapacity);
    }

    /// <summary>Where every sheet was packed, which is what a sprite's quad is shaped by.</summary>
    public SheetAtlas Atlas => _atlas;

    /// <summary>One frame. Everything that changes between frames is in the arrays this hands over, and the recording is untouched.</summary>
    public void Frame(CameraView view)
    {
        var size = Unsafe.SizeOf<SpriteInstance>();
        var quad = Unsafe.SizeOf<OverlayQuad>();
        var counts = _spriteCounts;
        var camera = view with
        {
            SurfacePeriodsM = _mesh.SurfacePeriodsM, WaterPeriodM = _mesh.WaterPeriodM, CarTilt = _carTilt, Patches = _mesh.Patches,
        };
        MemoryMarshal.Write(_camera, in camera);
        WrittenBytes = CameraBytes + ((long)(counts.Under + counts.Over + counts.Above) * size)
                       + ((long)(_overlayCount + _underlayCount + _underlayAboveCount) * quad);
        _frameCounts[WebGpu.FrameCount.Under] = counts.Under;
        _frameCounts[WebGpu.FrameCount.StandingFirst] = counts.StandingFirst;
        _frameCounts[WebGpu.FrameCount.Standing] = counts.StandingCount;
        _frameCounts[WebGpu.FrameCount.Over] = counts.Over;
        _frameCounts[WebGpu.FrameCount.Above] = counts.Above;
        _frameCounts[WebGpu.FrameCount.Overlay] = _overlayCount;
        _frameCounts[WebGpu.FrameCount.Underlay] = _underlayCount;
        _frameCounts[WebGpu.FrameCount.UnderlayAbove] = _underlayAboveCount;
        WebGpu.Frame(
            _camera,
            _sprites.AsSpan(0, counts.Under * size),
            _sprites.AsSpan(Room.Under * size, counts.Over * size),
            _sprites.AsSpan((Room.Under + Room.Over) * size, counts.Above * size),
            _overlay.AsSpan(0, _overlayCount * quad),
            _underlay.AsSpan(0, _underlayCount * quad),
            _underlay.AsSpan(UnderlayCapacity * quad, _underlayAboveCount * quad),
            _frameCounts);
    }

    readonly int[] _frameCounts = new int[WebGpu.FrameCount.Length];

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
        var page = new byte[SheetAtlas.PagePx * SheetAtlas.PagePx * Unsafe.SizeOf<Texel>()];
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
    /// What the ground is drawn from, or stand-ins where there is no ground — the seven bindings the
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

            var path = surfaceTextures[Math.Min(surface, surfaceTextures.Count - 1)];
            if ((Surface)surface is Surface.Track)
            {
                var (texels, widthPx, heightPx) = TrackSheet.Compose();
                Upload(FirstSurfaceTexture + surface, texels, widthPx, heightPx, mipped: true);
            }
            else if ((Surface)surface is Surface.Kerb or Surface.Edging)
            {
                Picture(FirstSurfaceTexture + surface, path, mipped: true);
            }
            else
            {
                Looks(FirstSurfaceTexture + surface, path);
            }
        }

        // A bridge's art after them, one array of its strips and fittings (BridgeSheet), and a stand-in where there is no ground.
        if (surfaceTextures.Count == 0)
        {
            Upload(BridgeTexture, new Texel[1], 1, 1, mipped: false);
            return;
        }

        var sheet = BridgeSheet.Compose();
        const int layerTexels = BridgeSheet.LayerWidthPx * BridgeSheet.LayerHeightPx;
        for (var layer = 0; layer < BridgeSheet.Layers; layer++)
        {
            var chain = MipChain.Build(
                sheet.AsSpan(layer * layerTexels, layerTexels).ToArray(), BridgeSheet.LayerWidthPx, BridgeSheet.LayerHeightPx);
            for (var level = 0; level < chain.Count; level++)
            {
                var (pixels, width, height) = chain[level];
                WebGpu.Texture(BridgeTexture, MemoryMarshal.AsBytes(pixels.AsSpan()), width, height, BridgeSheet.Layers, layer, level, chain.Count);
            }
        }
    }

    /// <summary>A surface's looks, a layer each and each its own chain, as the desktop lays them (<c>GpuTexture.LoadLooks</c>).</summary>
    static void Looks(int slot, string path)
    {
        var (widthPx, heightPx) = ImageHeader.Measure(path);
        if (heightPx % widthPx != 0)
        {
            throw new InvalidDataException($"{path} is {widthPx}x{heightPx}: a surface's looks are squares stacked top to bottom.");
        }

        var all = new Texel[widthPx * heightPx];
        Texels.Decode(path, all);
        var layers = heightPx / widthPx;
        for (var layer = 0; layer < layers; layer++)
        {
            var chain = MipChain.Build(all.AsSpan(layer * widthPx * widthPx, widthPx * widthPx).ToArray(), widthPx, widthPx);
            for (var level = 0; level < chain.Count; level++)
            {
                var (pixels, width, height) = chain[level];
                WebGpu.Texture(slot, MemoryMarshal.AsBytes(pixels.AsSpan()), width, height, layers, layer, level, chain.Count);
            }
        }
    }

    /// <summary>
    /// What the mesh holds, as the bytes the wall takes: a view crosses as a writable span and a mesh hands out a
    /// read-only one, but the page only reads it — and a copy was a traced city's mesh over again at its open's peak.
    /// </summary>
    static Span<byte> Bytes<T>(ReadOnlySpan<T> from) where T : unmanaged
    {
        var bytes = MemoryMarshal.AsBytes(from);
        return MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(bytes), bytes.Length);
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
