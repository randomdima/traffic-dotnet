using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Terrain;
using Semaphore = Silk.NET.Vulkan.Semaphore;
// This slice's own device wrapper, not the loader of the same name.
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.App.Render;

/// <summary>
/// The town's ground on screen: one pipeline, <b>one command buffer per swapchain image recorded
/// once</b>, and one indirect draw whose index count lives in a buffer the CPU writes rather than in
/// the call.
/// </summary>
/// <remarks>
/// The frame is five crossings — submit and present the frame that was filled, then acquire, wait and
/// reset for the next one — and not one takes the size of the town as an argument. The camera moves by a
/// write into mapped memory and the recording never changes; a panel opening changes a count in the
/// indirect buffer and nothing else. A Vulkan renderer
/// that re-recorded every frame would be worse than the OpenGL it replaced, and avoiding exactly that
/// is what this shape is for. Rebuilding the target is the one place recording happens again.
/// </remarks>
internal sealed unsafe partial class TownRenderer : IDisposable
{
    /// <summary>
    /// The five the ground is painted with, each its own binding. They are different sizes, wrap-
    /// seamless and mipped, so one array texture over them would force a common size and resample the
    /// ground the whole town stands on; the fragment stage switches over them instead.
    /// </summary>
    const int Surfaces = 5;


    /// <summary>
    /// The one set's bindings, in the order the shaders declare them. <b>The shaders and this list are
    /// the same list</b>: a binding added here is a binding added there, and nothing else knows the
    /// numbers.
    /// </summary>
    const int CameraBinding = 0;

    const int SheetTableBinding = 1;
    const int SheetPagesBinding = 2;
    const int GlyphBinding = 3;
    const int FirstSurfaceBinding = 4;

    /// <summary>How the marks are drawn (<see cref="MarkLook"/>), then the ring they are drawn from and its words (<c>mark.vert</c>).</summary>
    const int MarkLookBinding = FirstSurfaceBinding + Surfaces;

    const int MarkRowsBinding = MarkLookBinding + 1;
    const int MarkRingBinding = MarkRowsBinding + 1;

    /// <summary>The scenery's pieces and looks, laid once, and the frame's runs of them (<c>scenery.vert</c>).</summary>
    const int SceneryPiecesBinding = MarkRingBinding + 1;

    const int SceneryLooksBinding = SceneryPiecesBinding + 1;
    const int ScenerySpansBinding = SceneryLooksBinding + 1;

    /// <summary>
    /// The track's strip (<see cref="Surface.Track"/>), the sixth picture the ground is painted with: the last binding and
    /// not beside the five, so none of the bindings after them moved when it came.
    /// </summary>
    const int TrackBinding = ScenerySpansBinding + 1;

    /// <summary>The fans (<see cref="FanTable"/>), the one block the fragment stage reads as well as the vertex stage.</summary>
    const int FanTableBinding = TrackBinding + 1;

    /// <summary>The shore (<see cref="Surface.Shore"/>), the ground's seventh picture, last for the reason the track's strip is.</summary>
    const int ShoreBinding = FanTableBinding + 1;

    /// <summary>The two kerbs' strips (<see cref="Surface.Kerb"/>, <see cref="Surface.Edging"/>), last for the same reason.</summary>
    const int KerbBinding = ShoreBinding + 1;

    const int EdgingBinding = KerbBinding + 1;

    const int Bindings = EdgingBinding + 1;

    /// <summary>The words a frame's scenery runs open with (<c>scenery.vert</c>'s <c>Spans</c>): the draw, the thinning, how many runs.</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct SceneryRunsHead(DrawIndirectCommand Draw, uint Every, uint Runs, uint Pad0 = 0, uint Pad1 = 0);

    /// <summary>What a scenery look is drawn as (<c>scenery.vert</c>'s <c>SceneryLook</c>): its half-size and its sheet.</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct SceneryLook(Vector2 HalfSizeM, uint Sheet, uint Pad = 0);

    /// <summary>
    /// How many interface and debug quads a frame may write. The busiest frame is the debug layers
    /// over a city at a district framing; the buffer is laid for it once, because a buffer that grew
    /// would be a re-recording.
    /// </summary>
    public const int OverlayCapacity = 65536;

    /// <summary>
    /// And how many <em>under</em> the bodies. <b>The same quads through the same pipeline, drawn before
    /// the sprites instead of after them</b> — which is the whole of what puts a stretch of road the town
    /// has spoken for under the car standing on it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two buffers and not one buffer drawn twice.</b> Where the ground marks stop and the interface
    /// begins moves every frame, and an indirect draw may only start at a non-zero instance where the
    /// device offers <c>drawIndirectFirstInstance</c> — a feature this project does not ask for. A second
    /// buffer needs no feature, and both draws start at nothing.
    /// </para>
    /// <para>
    /// <b>It costs no crossing.</b> Both draws are written into the command buffer once per swapchain
    /// image, exactly as the other three are; what a frame changes is the count each of them reads out of
    /// memory. The five a frame actually makes are unmoved.
    /// </para>
    /// </remarks>
    public const int UnderlayCapacity = OverlayCapacity;

    /// <summary>
    /// <b>And how many on the level above</b> (TER-7b, PHY-1a): the same buffer past <see cref="UnderlayCapacity"/>,
    /// drawn over the bridges and under the bodies on them, as <see cref="SpritesAbove"/> is. A quarter, because the
    /// level above is a town's few bridges and not its streets.
    /// </summary>
    public const int UnderlayAboveCapacity = UnderlayCapacity / 4;

    readonly Vk _vk;
    readonly AppWindow? _window;
    readonly Extent2D _offscreenSize;
    readonly GpuTexture[] _textures;
    readonly SheetAtlas _atlas;
    readonly GpuTexture _sheetPages;
    readonly GpuBuffer _sheetTable;
    readonly GpuTexture _glyphs;
    readonly GpuBuffer _vertices;
    readonly GpuBuffer _indices;

    /// <summary>The ground's triangles filed by band, the order <see cref="_indices"/> holds them in (<see cref="GroundBands"/>).</summary>
    readonly GroundBands _bands;

    /// <summary>
    /// <b>The buildings and props, on the device and laid once</b> (<see cref="LayTown"/>): they never move, so a
    /// frame writes only which stretch of them to draw, and they are one copy rather than one a swapchain image.
    /// </summary>
    readonly GpuBuffer _standing;

    /// <summary>How the marks are drawn, laid with the town (<see cref="LayTown"/>).</summary>
    readonly GpuBuffer _markLook;

    /// <summary>
    /// <b>The scenery's pieces and each look's size and sheet, on the device and laid once</b> (<see cref="LayTown"/>): a
    /// frame writes only which runs of them the view holds (<see cref="_scenerySpans"/>).
    /// </summary>
    readonly GpuBuffer _sceneryPieces;

    readonly GpuBuffer _sceneryLooks;

    /// <summary>The fans, laid with the town (<see cref="FanTable"/>): read by the sprite stages, vertex and fragment.</summary>
    readonly GpuBuffer _fanTable;

    /// <summary>The ground it was laid for: its parts' sizes and its surfaces' periods.</summary>
    readonly GroundMesh _mesh;

    /// <summary>
    /// The view a frame culls the ground by, as the last <see cref="FillUnder"/> took it — or none, and the ground is drawn
    /// whole.
    /// </summary>
    (Vector2 CentreM, Vector2 SpanM)? _groundView;

    /// <summary>The scenery whose runs a frame writes, laid with the town.</summary>
    ScenerySprites _scenery = ScenerySprites.Nothing;

    /// <summary>The prop looks' sheets from and past, and the least half-size one is drawn at this frame (<see cref="CameraView.PropCull"/>).</summary>
    Vector4 _propCull;

    float _propLeastPx;

    uint _shownParts = GroundParts.All;

    DescriptorSetLayout _setLayout;
    PipelineLayout _pipelineLayout;
    Pipeline _pipeline;
    Pipeline _spritePipeline;
    Pipeline _overlayPipeline;
    Pipeline _markPipeline;
    Pipeline _sceneryPipeline;
    Pipeline _cloudPipeline;
    ShaderModule _markVertexShader;
    ShaderModule _sceneryVertexShader;
    ShaderModule _cloudVertexShader;
    ShaderModule _cloudFragmentShader;
    ShaderModule _vertexShader;
    ShaderModule _fragmentShader;
    ShaderModule _spriteVertexShader;
    ShaderModule _spriteFragmentShader;
    ShaderModule _overlayVertexShader;
    ShaderModule _overlayFragmentShader;

    RenderTarget _target;
    DescriptorPool _descriptors;
    DescriptorSet[] _sets = [];
    GpuBuffer[] _cameras = [];

    /// <summary>
    /// <b>One of each per swapchain image.</b> Everything here is written by the CPU every frame, and a
    /// frame is filled while the one or two before it are still being drawn — so a single buffer would
    /// be the town's sprites being rewritten under a draw that is still fetching them. What that looks
    /// like on screen is a moving car winking out for a frame as its slot is read from the other
    /// frame's fill; a standing town writes the same bytes twice and shows nothing. The image is taken
    /// before the fill (<see cref="TakeImage"/>) and its fence is what says the GPU has let go.
    /// </summary>
    GpuBuffer[] _instances = [];

    GpuBuffer[] _spriteIndirect = [];

    /// <summary>Each image's draws of the ground, a class of a part apiece (<see cref="GroundBands.Draws"/>), written by its frame.</summary>
    GpuBuffer[] _groundDraws = [];

    /// <summary>
    /// What emits the moving runs on the device inside each frame (<see cref="DrawTheMovingFrom"/>) — or none, and they
    /// are what a frame fills.
    /// </summary>
    IMovingEmit? _emit;

    /// <summary>What draws the engine's own tables inside each frame (<see cref="DrawTheEngineFrom"/>), or none.</summary>
    IEngineDraw? _engine;

    /// <summary>
    /// <b>The ground's ring of marks as this image's frame copied it</b>, and its words — the host's ring, of which a frame
    /// writes only the marks laid since this image last drew (<see cref="_marksCopied"/>) — or none of them read, where
    /// the device keeps the ring itself (<see cref="_deviceMarks"/>).
    /// </summary>
    GpuBuffer[] _markRows = [];

    GpuBuffer[] _markRings = [];

    /// <summary>How many of the host's marks (<see cref="DriftMarks.Total"/>) each image's copy holds.</summary>
    long[] _marksCopied = [];

    /// <summary>The ring the device lays its marks into and its words (<see cref="DrawTheMarksFrom"/>), the same for every image.</summary>
    (GpuBuffer Rows, GpuBuffer Ring)? _deviceMarks;

    /// <summary>Each image's runs of the scenery in view, opened by their draw (<see cref="SceneryRunsHead"/>).</summary>
    GpuBuffer[] _scenerySpans = [];

    GpuBuffer[] _overlay = [];
    GpuBuffer[] _overlayIndirect = [];
    GpuBuffer[] _underlay = [];
    GpuBuffer[] _underlayIndirect = [];
    CommandBuffer[] _commands = [];
    Fence[] _drawn = [];
    Semaphore[] _rendered = [];

    /// <summary>
    /// <b>The acquire semaphores, one more than the images</b>: an acquire is handed the spare (<see cref="_acquireSpare"/>),
    /// and the one the image it took was last acquired with becomes the spare. That one's wait was the submit of the image's
    /// last frame, which the image's fence has said is done, so no acquire is handed a semaphore with a wait pending.
    /// </summary>
    Semaphore[] _acquired = [];

    /// <summary>Which of <see cref="_acquired"/> each image was last acquired with.</summary>
    int[] _acquiredBy = [];

    int _acquireSpare;
    long _frame;
    int _acquireSlot;
    uint _image;
    bool _holding;
    bool _rebuilding;
    uint _lastImage;

    TownRenderer(
        Vk vk, AppWindow? window, Extent2D offscreenSize, GroundMesh mesh, IReadOnlyList<string> surfaceTextures,
        IReadOnlyList<SheetSource> sheetTextures, SpriteRoom room)
    {
        _vk = vk;
        _window = window;
        _offscreenSize = offscreenSize;
        // A slot a run at the least: the level above is bound at its own offset, and an offset at the buffer's end
        // is out of it even with nothing drawn there (VUID-vkCmdBindVertexBuffers-pOffsets-00626).
        Room = room with
        {
            Over = Math.Max(1, room.Over), Above = Math.Max(1, room.Above), Marks = Math.Max(1, room.Marks),
            SceneryPieces = Math.Max(1, room.SceneryPieces), SceneryRows = Math.Max(1, room.SceneryRows),
        };
        _standing = vk.CreateBuffer(
            (ulong)(Math.Max(1, room.Standing) * sizeof(SpriteInstance)),
            BufferUsageFlags.VertexBufferBit | BufferUsageFlags.TransferDstBit, hostVisible: false);
        _markLook = vk.CreateBuffer(
            (ulong)sizeof(MarkLook), BufferUsageFlags.UniformBufferBit | BufferUsageFlags.TransferDstBit, hostVisible: false);
        _sceneryPieces = vk.CreateBuffer(
            (ulong)(Room.SceneryPieces * sizeof(SceneryPiece)), BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit,
            hostVisible: false);
        _sceneryLooks = vk.CreateBuffer(
            (ulong)(SheetAtlas.Slots * sizeof(SceneryLook)), BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit,
            hostVisible: false);
        _fanTable = vk.CreateBuffer(
            (ulong)(FanTable.Words * sizeof(uint)), BufferUsageFlags.UniformBufferBit | BufferUsageFlags.TransferDstBit,
            hostVisible: false);

        _textures = new GpuTexture[surfaceTextures.Count];
        // Every surface is its looks, a layer a look (GroundPatches); a strip among them — the track's, a kerb's — is one picture.
        for (var texture = 0; texture < _textures.Length; texture++)
        {
            _textures[texture] = (Surface)texture is Surface.Track or Surface.Kerb or Surface.Edging
                ? GpuTexture.Load(vk, surfaceTextures[texture])
                : GpuTexture.LoadLooks(vk, surfaceTextures[texture]);
        }

        if (sheetTextures.Count > SheetAtlas.Slots) throw new InvalidOperationException(
            $"{sheetTextures.Count} sheets, and the sprite shader's table holds {SheetAtlas.Slots}.");
        var tableBytes = (ulong)(SheetAtlas.Slots * sizeof(SheetPlace));
        if (vk.MaxUniformBufferRangeBytes < tableBytes) throw new NotSupportedException(
            $"{vk.DeviceName} binds {vk.MaxUniformBufferRangeBytes} bytes of uniforms, and the sprite shader's table is {tableBytes}.");

        // Every sheet onto the layers of one array texture. What a sheet was is then a row of the table
        // the vertex shader reads.
        _atlas = SheetAtlas.Pack(sheetTextures);
        _sheetPages = GpuTexture.Layered(vk, SheetAtlas.PagePx, SheetAtlas.PagePx, _atlas.Pages, _atlas.FillPage);

        // Read by every sprite's every corner and written once, so it is the device's own memory and not the host's.
        _sheetTable = vk.CreateBuffer(
            tableBytes, BufferUsageFlags.UniformBufferBit | BufferUsageFlags.TransferDstBit,
            hostVisible: false);
        _sheetTable.Upload<SheetPlace>(
            SheetAtlas.Slots, into =>
            {
                into.Clear();
                _atlas.Places.CopyTo(into);
            }, PipelineStageFlags2.VertexShaderBit, AccessFlags2.UniformReadBit);

        // <b>The ground lives on the device, written by a copy</b>: mapped memory on a discrete card is the host's, across
        // the bus — which a traced city's ground, hundreds of MB, cannot cross every frame. Its triangles are filed by
        // band inside each part (GroundBands), so a frame draws the rows it sees.
        _mesh = mesh;
        _bands = GroundBands.Lay(mesh, out var filed);
        _vertices = vk.CreateBuffer(
            (ulong)(mesh.Vertices.Length * sizeof(GroundVertex)), BufferUsageFlags.VertexBufferBit | BufferUsageFlags.TransferDstBit,
            hostVisible: false);
        _indices = vk.CreateBuffer(
            (ulong)(filed.Length * sizeof(uint)), BufferUsageFlags.IndexBufferBit | BufferUsageFlags.TransferDstBit,
            hostVisible: false);
        _vertices.Upload<GroundVertex>(
            mesh.Vertices.Length, into => mesh.Vertices.CopyTo(into),
            PipelineStageFlags2.VertexAttributeInputBit, AccessFlags2.VertexAttributeReadBit);
        _indices.Upload<uint>(filed.Length, into => filed.CopyTo(into), PipelineStageFlags2.IndexInputBit, AccessFlags2.IndexReadBit);

        _glyphs = GpuTexture.LoadEmbedded(vk, GlyphSheet.Resource);

        CreatePipeline();
        _target = NewTarget();
        CreateTargetDependents();
    }

    /// <summary>The town in a window, which is the only target that can resize or go out of date.</summary>
    public static TownRenderer OnScreen(
        Vk vk, AppWindow window, GroundMesh mesh, IReadOnlyList<string> surfaceTextures,
        IReadOnlyList<SheetSource> sheetTextures, SpriteRoom room) =>
        new(vk, window, default, mesh, surfaceTextures, sheetTextures, room);

    /// <summary>
    /// The same town drawn into one image with no window under it — what a render check is made of,
    /// and the only way to take a shot on a machine nobody is sitting at.
    /// </summary>
    public static TownRenderer Offscreen(
        Vk vk, int width, int height, GroundMesh mesh, IReadOnlyList<string> surfaceTextures,
        IReadOnlyList<SheetSource> sheetTextures, SpriteRoom room) =>
        new(vk, null, new Extent2D((uint)width, (uint)height), mesh, surfaceTextures, sheetTextures, room);

    /// <summary>How many instances each run of the sprite pass was laid for.</summary>
    public SpriteRoom Room { get; }

    /// <summary>
    /// The instance buffer as the caller writes it, run by run: mapped memory the driver already owns, so
    /// filling it is a write and not an upload. <b>The one belonging to the image the next frame draws into</b>,
    /// which is the image already taken and waited for — see <see cref="_instances"/>.
    /// </summary>
    public Span<SpriteInstance> SpritesOver => _instances[(int)_image].Span<SpriteInstance>()[..Room.Over];

    /// <summary>
    /// <b>And the bodies on the level above</b> (PHY-1a): the same buffer past the others, drawn after the bridges
    /// over the ground and so over them, where the first runs are drawn under them.
    /// </summary>
    public Span<SpriteInstance> SpritesAbove => _instances[(int)_image].Span<SpriteInstance>().Slice(Room.Over, Room.Above);

    /// <summary>
    /// <b>What of a town is laid on the device once</b>: its buildings and props in the order <see cref="StandingSprites"/>
    /// files them — what <see cref="SpriteCounts.StandingFirst"/> counts from — its scenery's pieces and each look's size
    /// and sheet, how its marks are drawn, and which sheets are props for the frame to leave out under
    /// <paramref name="propLeastPx"/> across. Called before the first frame of a town is drawn, and so under no frame
    /// still reading the buffers.
    /// </summary>
    public void LayTown(TownSprites looks, float propLeastPx)
    {
        var standing = looks.Standing.Instances;
        var count = Math.Min(standing.Length, Math.Max(1, Room.Standing));
        _standing.Upload<SpriteInstance>(
            count, into => standing.Span[..count].CopyTo(into),
            PipelineStageFlags2.VertexAttributeInputBit, AccessFlags2.VertexAttributeReadBit);

        _scenery = looks.Scenery;
        var scenery = looks.Scenery;
        _sceneryPieces.Upload<SceneryPiece>(
            Math.Min(scenery.Count, Room.SceneryPieces), into => scenery.Pieces[..into.Length].CopyTo(into),
            PipelineStageFlags2.VertexShaderBit, AccessFlags2.ShaderStorageReadBit);
        _sceneryLooks.Upload<SceneryLook>(
            SheetAtlas.Slots, into =>
            {
                into.Clear();
                var sizes = scenery.HalfSizeM;
                for (var look = 0; look < Math.Min(sizes.Length, into.Length); look++)
                {
                    into[look] = new SceneryLook(sizes[look], (uint)(scenery.FirstSheet + look));
                }
            }, PipelineStageFlags2.VertexShaderBit, AccessFlags2.ShaderStorageReadBit);

        var markLook = looks.MarkLook;
        _markLook.Upload<MarkLook>(1, into => into[0] = markLook, PipelineStageFlags2.VertexShaderBit, AccessFlags2.UniformReadBit);

        _propCull = new Vector4(0f, looks.FirstPropSheet, looks.FirstPropSheet + looks.Props.Count, 0f);
        _fanTable.Upload<uint>(
            looks.Fans, PipelineStageFlags2.VertexShaderBit | PipelineStageFlags2.FragmentShaderBit, AccessFlags2.UniformReadBit);
        _propLeastPx = propLeastPx;
    }

    /// <summary>
    /// <b>What lies under everything that stands, for the frame being filled</b>: the marks laid on the host since this
    /// image last drew, copied into its ring (none, where the device keeps the ring — <see cref="DrawTheMarksFrom"/>),
    /// and the runs of the scenery the view holds. <paramref name="pixelsPerMetre"/> is the frame's scale, which says
    /// which props are too small to draw. Answers how many instances that draws, the device's marks uncounted.
    /// </summary>
    public int FillUnder(DriftMarks marks, Vector2 viewCentreM, Vector2 viewSpanM, float pixelsPerMetre)
    {
        _propCull.X = _propLeastPx * 0.5f / pixelsPerMetre;
        _groundView = (viewCentreM, viewSpanM);
        var image = (int)_image;
        if (_deviceMarks is null) CopyTheMarks(marks, image);

        var runs = _scenerySpans[image];
        var spans = MemoryMarshal.Cast<byte, ScenerySpan>(runs.Span<byte>()[sizeof(SceneryRunsHead)..]);
        var count = _scenery.Spans(viewCentreM, viewSpanM, int.MaxValue, spans, out var every, out var drawn);
        runs.Span<SceneryRunsHead>()[0] = new SceneryRunsHead(
            new DrawIndirectCommand { VertexCount = 4, InstanceCount = (uint)drawn }, (uint)every, (uint)count);
        _written += sizeof(SceneryRunsHead) + ((long)count * sizeof(ScenerySpan));
        return drawn + (_deviceMarks is null ? marks.Count : 0);
    }

    /// <summary>
    /// The marks laid since this image last drew, from the host's ring into the image's own: <b>only what is new</b>, the
    /// ring whole only once it has turned over past what the image holds, or been cleared under it.
    /// </summary>
    void CopyTheMarks(DriftMarks marks, int image)
    {
        var rows = _markRows[image].Span<DriftMarkRow>();
        var capacity = marks.Capacity;
        if (capacity > rows.Length) return;

        ref var copied = ref _marksCopied[image];
        var total = marks.Total;
        if (copied > total) copied = 0;

        for (var mark = Math.Max(copied, total - capacity); mark < total; mark++)
        {
            var slot = (int)(mark % capacity);
            rows[slot] = DriftMarkRow.Of(marks.At(slot));
            _written += sizeof(DriftMarkRow);
        }

        copied = total;
        _markRings[image].Span<DriftMarkRing>()[0] = DriftMarkRing.After(total, capacity);
        _written += sizeof(DriftMarkRing);
    }

    /// <summary>
    /// <b>The marks drawn from the ring the device lays them into</b> (<c>drift_marks.glsl</c>) and its words, in place of
    /// the copies a frame fills from the host's — which the host's ring then stays out of. Every image's frame is written
    /// down again once the device has let go of them.
    /// </summary>
    public void DrawTheMarksFrom(GpuBuffer rows, GpuBuffer ring)
    {
        Vk.Count();
        _vk.Api.DeviceWaitIdle(_vk.Device);
        _deviceMarks = (rows, ring);
        for (var image = 0; image < _commands.Length; image++)
        {
            WriteTheMarkSet(image);
            Record(image);
        }
    }

    /// <summary>
    /// <b>What moves emitted on the device inside each frame and drawn where it is written</b> — the emit recorded into
    /// every image's command buffer ahead of the pass, the over run drawn from the first of its instances and the run above
    /// from its <see cref="IMovingEmit.AboveFrom"/>, each as many as its draws say — in place of what a frame fills
    /// (<see cref="SpritesOver"/>, <see cref="SpritesAbove"/>), whose counts are then not read. Every image's frame is
    /// written down again once the device has let go of them; what a frame steers the emit by is written through
    /// <see cref="ImageIndex"/>.
    /// </summary>
    public void DrawTheMovingFrom(IMovingEmit emit)
    {
        Vk.Count();
        _vk.Api.DeviceWaitIdle(_vk.Device);
        _emit = emit;
        for (var image = 0; image < _commands.Length; image++) Record(image);
    }

    /// <summary>
    /// <b>The engine's own tables drawn on the device inside each frame</b> (<see cref="IEngineDraw"/>): its kernels recorded
    /// into every image's command buffer ahead of the pass, its ground run over the town's own ground marks, its run above
    /// over the level above's, and its run over the bodies under the interface — or none of it, for <c>null</c>. Every image's
    /// frame is written down again once the device has let go of them.
    /// </summary>
    public void DrawTheEngineFrom(IEngineDraw? engine)
    {
        Vk.Count();
        _vk.Api.DeviceWaitIdle(_vk.Device);
        _engine = engine;
        for (var image = 0; image < _commands.Length; image++) Record(image);
    }

    /// <summary>The swapchain image the frame being filled is drawn into, for a writer of memory that image's recording reads.</summary>
    public int ImageIndex => (int)_image;

    /// <summary>How many images the target holds, each with a recording of its own: every <see cref="ImageIndex"/> is below it.</summary>
    public int ImageCount => _commands.Length;

    /// <summary>How much of each run is to be drawn. The only thing a frame changes about the sprite pass.</summary>
    /// <remarks><see cref="SpriteCounts.Under"/> is the page's: what lies under is drawn here from its own rows (<see cref="FillUnder"/>).</remarks>
    public void SetSpriteCount(in SpriteCounts counts)
    {
        var moving = _emit is null ? counts.Over + counts.Above : 0;
        _written += ((long)moving * sizeof(SpriteInstance)) + (SpriteDraws * sizeof(DrawIndirectCommand));
        var draws = _spriteIndirect[(int)_image].Span<DrawIndirectCommand>();
        draws[0] = new DrawIndirectCommand
        {
            VertexCount = 4,
            InstanceCount = (uint)Math.Clamp(counts.StandingCount, 0, Room.Standing),
            FirstInstance = (uint)Math.Clamp(counts.StandingFirst, 0, Room.Standing),
        };
        draws[1] = new DrawIndirectCommand { VertexCount = 4, InstanceCount = (uint)Math.Clamp(counts.Over, 0, Room.Over) };
        draws[2] = new DrawIndirectCommand { VertexCount = 4, InstanceCount = (uint)Math.Clamp(counts.Above, 0, Room.Above) };
    }

    /// <summary>The sprite pass's draws a frame counts: the stretch of what stands, what moves over it, and what moves above.</summary>
    const int SpriteDraws = 3;

    /// <summary>
    /// The interface and the debug layers' own instance buffer, written the same way the sprites'
    /// is: mapped memory, no upload, no crossing.
    /// </summary>
    public Span<OverlayQuad> Overlay => _overlay[(int)_image].Span<OverlayQuad>()[..OverlayCapacity];

    /// <summary>
    /// How many overlay quads are to be drawn. <b>A closed panel writes zero and its draw becomes a
    /// no-op the GPU skips</b> — which is the whole reason an interface opening re-records nothing.
    /// </summary>
    public void SetOverlayCount(int count)
    {
        _written += ((long)Math.Clamp(count, 0, OverlayCapacity) * sizeof(OverlayQuad)) + sizeof(DrawIndirectCommand);
        _overlayIndirect[(int)_image].Span<DrawIndirectCommand>()[0] = new DrawIndirectCommand
        {
            VertexCount = 4,
            InstanceCount = (uint)Math.Clamp(count, 0, OverlayCapacity),
        };
    }

    /// <summary>The same buffer's worth of quads drawn <em>under</em> the bodies — the town's own ground marks.</summary>
    public Span<OverlayQuad> Underlay => _underlay[(int)_image].Span<OverlayQuad>()[..UnderlayCapacity];

    /// <summary>
    /// <b>And the marks on the level above</b> (<see cref="UnderlayAboveCapacity"/>): past <see cref="Underlay"/>,
    /// drawn after the bridges over the ground, where the first run is drawn under them.
    /// </summary>
    public Span<OverlayQuad> UnderlayAbove =>
        _underlay[(int)_image].Span<OverlayQuad>().Slice(UnderlayCapacity, UnderlayAboveCapacity);

    /// <summary>And how many of each run are to be drawn, on the same terms.</summary>
    public void SetUnderlayCount(int count, int above)
    {
        _written += ((long)(Math.Clamp(count, 0, UnderlayCapacity) + Math.Clamp(above, 0, UnderlayAboveCapacity)) * sizeof(OverlayQuad))
                    + (2 * sizeof(DrawIndirectCommand));
        var draws = _underlayIndirect[(int)_image].Span<DrawIndirectCommand>();
        draws[0] = new DrawIndirectCommand { VertexCount = 4, InstanceCount = (uint)Math.Clamp(count, 0, UnderlayCapacity) };
        draws[1] = new DrawIndirectCommand
        {
            VertexCount = 4,
            InstanceCount = (uint)Math.Clamp(above, 0, UnderlayAboveCapacity),
        };
    }

    /// <summary>Where every sheet was packed, which is what a sprite's quad is shaped by (<see cref="TownSprites.ReadAspects"/>).</summary>
    public SheetAtlas Atlas => _atlas;

    /// <summary>How many triangles of the town's standing ground the layers shown hold.</summary>
    public int TriangleCount
    {
        get
        {
            var indices = 0;
            for (var part = 0; part < GroundParts.Count; part++)
            {
                if ((_shownParts & (1u << part)) != 0) indices += _mesh.Parts[part].IndexCount;
            }

            return indices / 3;
        }
    }

    /// <summary>
    /// <b>Which of the ground's own layers are drawn</b> (OBS-2v), as a bit per <see cref="GroundPart"/>. A layer
    /// switched off draws nought in each of its draws from the next frame — no upload, no recording, no pipeline and no
    /// ground laid again.
    /// </summary>
    public void ShowGround(uint parts) => _shownParts = parts;

    /// <summary>
    /// This image's draws of the ground written: every class of every part shown, the rows the frame's view reaches — or
    /// every row, where no view was handed over.
    /// </summary>
    void WriteTheGround(int image)
    {
        var draws = _groundDraws[image].Span<DrawIndexedIndirectCommand>();
        if (_groundView is { } view)
        {
            _bands.Write(_shownParts, view.CentreM.Y - (view.SpanM.Y * 0.5f), view.CentreM.Y + (view.SpanM.Y * 0.5f), draws);
        }
        else
        {
            _bands.WriteWhole(_shownParts, draws);
        }
        _written += (long)_bands.Draws * sizeof(DrawIndexedIndirectCommand);
    }

    public Extent2D Size => _target.Extent;

    /// <summary>
    /// What the last <see cref="Frame"/> spent waiting on the presentation engine rather than working:
    /// the acquire, and the fence of the frame two back. Not this build's cost, and the whole of what a
    /// frame rate under FIFO is made of.
    /// </summary>
    public double BlockedMs { get; private set; }

    /// <summary>
    /// What the last <see cref="Frame"/> wrote into memory the device reads: the runs and quads its counts say were
    /// filled, the counts themselves, the camera, and whatever another writer declared (<see cref="Wrote"/>). An
    /// instrument (<see cref="Debug.FrameParts.WrittenBytes"/>).
    /// </summary>
    public long WrittenBytes { get; private set; }

    long _written;

    /// <summary>Bytes a writer other than this renderer put into mapped memory for the frame being filled.</summary>
    public void Wrote(long bytes) => _written += bytes;

    /// <summary>
    /// One frame. Everything that changes between frames is already in mapped memory — the memory of
    /// the image taken before it was written — so what is left is the five calls the design is named
    /// for: submit and present this frame, then acquire, wait and reset for the next.
    /// </summary>
    public void Frame(CameraView view)
    {
        // A rebuild that could not take an image — a window with no area, an acquire that came back
        // out of date twice — leaves nothing to draw into, and a frame is skipped rather than drawn
        // into an image nobody holds.
        if (!_holding) TakeImage();
        if (!_holding) return;

        var api = _vk.Api;
        var image = _image;
        _cameras[image].Span<CameraView>()[0] = view with
        {
            SurfacePeriodsM = _mesh.SurfacePeriodsM, WaterPeriodM = _mesh.WaterPeriodM, PropCull = _propCull,
            Patches = _mesh.Patches,
        };
        WriteTheGround((int)image);
        WrittenBytes = _written + sizeof(CameraView);
        _written = 0;

        var acquired = _acquired[_acquireSlot];
        var commands = _commands[image];
        var rendered = _rendered[image];
        var waitStage = PipelineStageFlags.ColorAttachmentOutputBit;

        // Nothing is being shown offscreen, so there is nothing to wait for the presenter to let go
        // of and nothing to tell it when the frame is done: the fence is the whole story.
        var synchronised = _target.AcquireSignals;
        var submit = new SubmitInfo
        {
            SType = StructureType.SubmitInfo,
            WaitSemaphoreCount = synchronised ? 1u : 0u,
            PWaitSemaphores = synchronised ? &acquired : null,
            PWaitDstStageMask = synchronised ? &waitStage : null,
            CommandBufferCount = 1,
            PCommandBuffers = &commands,
            SignalSemaphoreCount = synchronised ? 1u : 0u,
            PSignalSemaphores = synchronised ? &rendered : null,
        };

        Vk.Count();
        Vk.Check(api.QueueSubmit(_vk.Queue, 1, &submit, _drawn[image]), "vkQueueSubmit");
        _holding = false;

        _lastImage = image;
        _frame++;

        if (!_target.Present(rendered, image)) Recreate();
        else TakeImage();
    }

    /// <summary>
    /// The image the next frame is drawn into, taken and waited for <em>before</em> that frame is
    /// written rather than after. <b>This is what makes filling the buffers safe</b>: they are this
    /// image's own, and the fence says the draw that last read them has finished — see
    /// <see cref="_instances"/>.
    /// </summary>
    void TakeImage()
    {
        var api = _vk.Api;
        var waitedFrom = Stopwatch.GetTimestamp();

        var slot = _acquireSpare;
        if (!_target.Acquire(_acquired[slot], out _image))
        {
            // Out of date: there is no image, and nothing to wait on. The rebuild takes one of its own
            // — and a rebuild already under way is left to finish rather than started again inside
            // itself.
            if (!_rebuilding) Recreate();
            return;
        }

        _acquireSpare = _acquiredBy[_image];
        _acquiredBy[_image] = slot;
        _acquireSlot = slot;

        var fence = _drawn[_image];
        Vk.Count();
        Vk.Check(api.WaitForFences(_vk.Device, 1, &fence, true, ulong.MaxValue), "vkWaitForFences");
        BlockedMs = Stopwatch.GetElapsedTime(waitedFrom).TotalMilliseconds;
        Vk.Count();
        Vk.Check(api.ResetFences(_vk.Device, 1, &fence), "vkResetFences");
        _holding = true;
    }

    /// <summary>
    /// The image held for a frame that will now never be drawn, given back. Its acquire has already
    /// signalled a semaphore nothing is going to wait on, and a semaphore carrying a signal nobody
    /// takes may not be destroyed — so one empty submit consumes it on the way into a rebuild.
    /// </summary>
    void DrainAcquire()
    {
        if (!_holding) return;

        _holding = false;
        if (!_target.AcquireSignals) return;

        var acquired = _acquired[_acquireSlot];
        var waitStage = PipelineStageFlags.ColorAttachmentOutputBit;
        var submit = new SubmitInfo
        {
            SType = StructureType.SubmitInfo,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &acquired,
            PWaitDstStageMask = &waitStage,
        };

        Vk.Count();
        Vk.Check(_vk.Api.QueueSubmit(_vk.Queue, 1, &submit, default), "vkQueueSubmit");
    }

    /// <summary>
    /// The window changed size, so the images it is drawn into are rebuilt and re-recorded. An
    /// offscreen target is the size it was asked for and has nothing to react to.
    /// </summary>
    public void Recreate()
    {
        if (_window is null) return;

        var size = _window.FramebufferSize;
        if (size.X == 0 || size.Y == 0) return;

        _rebuilding = true;
        try
        {
            DrainAcquire();
            Vk.Count();
            _vk.Api.DeviceWaitIdle(_vk.Device);
            DestroyTargetDependents();
            _target.Dispose();
            _target = NewTarget();
            CreateTargetDependents();
        }
        finally
        {
            _rebuilding = false;
        }
    }

    /// <summary>
    /// The frame that was last drawn, read back off the image it was drawn into. The reason every target
    /// carries <c>TRANSFER_SRC</c>.
    /// </summary>
    /// <param name="widestPx">
    /// How wide the picture written out may be, or nought for the frame as it was drawn. <b>It is about
    /// the file and never about the frame</b>: the town is drawn at whatever the window is, and a reader
    /// who only wants to see what happened is handed a smaller copy of the same picture.
    /// </param>
    public void Shot(string path, int widestPx = 0)
    {
        if (_frame == 0) throw new InvalidOperationException("Nothing has been drawn yet: there is no frame to read back.");

        Vk.Count();
        _vk.Api.DeviceWaitIdle(_vk.Device);

        var width = (int)_target.Extent.Width;
        var height = (int)_target.Extent.Height;
        var image = _target.Images[_lastImage];
        var drawnIn = _target.FinalLayout;
        using var readback = _vk.CreateBuffer((ulong)(width * height * 4), BufferUsageFlags.TransferDstBit, hostVisible: true);

        _vk.OneShot(commands =>
        {
            Barrier(commands, image, drawnIn, ImageLayout.TransferSrcOptimal);
            var region = new BufferImageCopy
            {
                ImageSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, 0, 0, 1),
                ImageExtent = new Extent3D((uint)width, (uint)height, 1),
            };

            Vk.Count();
            _vk.Api.CmdCopyImageToBuffer(commands, image, ImageLayout.TransferSrcOptimal, readback.Handle, 1, &region);
            Barrier(commands, image, ImageLayout.TransferSrcOptimal, drawnIn);
        });

        var pixels = readback.Span<byte>();
        var rgba = new Rgba32[width * height];
        var bgr = _target.Format is Format.B8G8R8A8Unorm or Format.B8G8R8A8Srgb;
        for (var pixel = 0; pixel < rgba.Length; pixel++)
        {
            var at = pixel * 4;
            rgba[pixel] = bgr
                ? new Rgba32(pixels[at + 2], pixels[at + 1], pixels[at], 255)
                : new Rgba32(pixels[at], pixels[at + 1], pixels[at + 2], 255);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var shot = SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(rgba, width, height);
        if (widestPx > 0 && width > widestPx)
        {
            shot.Mutate(picture => picture.Resize(widestPx, height * widestPx / width));
        }

        shot.SaveAsPng(path);
    }

    public void Dispose()
    {
        DrainAcquire();
        Vk.Count();
        _vk.Api.DeviceWaitIdle(_vk.Device);

        DestroyTargetDependents();
        _target.Dispose();

        Vk.Count();
        _vk.Api.DestroyPipelineLayout(_vk.Device, _pipelineLayout, null);
        Vk.Count();
        _vk.Api.DestroyDescriptorSetLayout(_vk.Device, _setLayout, null);
        foreach (var shader in (ReadOnlySpan<ShaderModule>)[
                     _vertexShader, _fragmentShader, _spriteVertexShader, _spriteFragmentShader,
                     _overlayVertexShader, _overlayFragmentShader, _markVertexShader, _sceneryVertexShader,
                     _cloudVertexShader, _cloudFragmentShader])
        {
            Vk.Count();
            _vk.Api.DestroyShaderModule(_vk.Device, shader, null);
        }

        _vertices.Dispose();
        _indices.Dispose();
        _glyphs.Dispose();
        _sheetTable.Dispose();
        _sheetPages.Dispose();
        _standing.Dispose();
        _markLook.Dispose();
        _sceneryPieces.Dispose();
        _sceneryLooks.Dispose();
        _fanTable.Dispose();
        foreach (var texture in _textures) texture.Dispose();
    }

}
