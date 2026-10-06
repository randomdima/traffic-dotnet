using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Runtime;
using Image = Silk.NET.Vulkan.Image;
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
    /// Every walker's look, <em>two</em> per car — the car and the wreck it becomes — one per roof, prefab and
    /// prop look, with room over the shipped art's four hundred and ninety-eight. It is the length of the shader's
    /// uniform array of places, so this is the only place the number lives.
    /// </summary>
    /// <remarks>
    /// <b>No longer than sixteen kilobytes of places</b>, thirty-two bytes each: the uniform range every Vulkan
    /// device is bound to offer (<c>maxUniformBufferRange</c>), so the handset's head draws what the desktop's does.
    /// </remarks>
    const int SheetSlots = 512;

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
    const int Bindings = FirstSurfaceBinding + Surfaces;

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
    readonly GpuBuffer _indirect;

    /// <summary>The ground it was laid for, kept so a part switched off can be packed out of the draw and back into it (<see cref="ShowGround"/>).</summary>
    readonly GroundMesh _mesh;

    uint _indexCount;
    uint _shownParts = GroundParts.All;

    DescriptorSetLayout _setLayout;
    PipelineLayout _pipelineLayout;
    Pipeline _pipeline;
    Pipeline _spritePipeline;
    Pipeline _overlayPipeline;
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
    GpuBuffer[] _overlay = [];
    GpuBuffer[] _overlayIndirect = [];
    GpuBuffer[] _underlay = [];
    GpuBuffer[] _underlayIndirect = [];
    CommandBuffer[] _commands = [];
    Fence[] _drawn = [];
    Semaphore[] _rendered = [];
    Semaphore[] _acquired = [];
    long _frame;
    long _acquires;
    int _acquireSlot;
    uint _image;
    bool _holding;
    bool _rebuilding;
    uint _lastImage;

    TownRenderer(
        Vk vk, AppWindow? window, Extent2D offscreenSize, GroundMesh mesh, IReadOnlyList<string> surfaceTextures,
        IReadOnlyList<SheetSource> sheetTextures, int spriteCapacity, int aboveCapacity)
    {
        _vk = vk;
        _window = window;
        _offscreenSize = offscreenSize;

        _textures = new GpuTexture[surfaceTextures.Count];
        for (var texture = 0; texture < _textures.Length; texture++) _textures[texture] = GpuTexture.Load(vk, surfaceTextures[texture]);

        if (sheetTextures.Count > SheetSlots) throw new InvalidOperationException(
            $"{sheetTextures.Count} sheets, and the sprite shader's table holds {SheetSlots}.");

        // Every sheet onto the layers of one array texture. What a sheet was is then a row of the table
        // the vertex shader reads.
        _atlas = SheetAtlas.Pack(sheetTextures);
        _sheetPages = GpuTexture.Layered(vk, SheetAtlas.PagePx, SheetAtlas.PagePx, _atlas.Pages, _atlas.FillPage);

        _sheetTable = vk.CreateBuffer((ulong)(SheetSlots * sizeof(SheetPlace)), BufferUsageFlags.UniformBufferBit, hostVisible: true);
        _atlas.Places.CopyTo(_sheetTable.Span<SheetPlace>());

        // <b>The ground lives on the device, written by a copy</b>: every frame draws all of it, and mapped memory on
        // a discrete card is the host's, across the bus — which a traced city's ground, hundreds of MB, cannot cross
        // every frame.
        _mesh = mesh;
        _indexCount = (uint)mesh.Indices.Length;
        _vertices = vk.CreateBuffer(
            (ulong)(mesh.Vertices.Length * sizeof(GroundVertex)), BufferUsageFlags.VertexBufferBit | BufferUsageFlags.TransferDstBit,
            hostVisible: false);
        _indices = vk.CreateBuffer(
            (ulong)(mesh.Indices.Length * sizeof(uint)), BufferUsageFlags.IndexBufferBit | BufferUsageFlags.TransferDstBit,
            hostVisible: false);
        _indirect = vk.CreateBuffer((ulong)(2 * sizeof(DrawIndexedIndirectCommand)), BufferUsageFlags.IndirectBufferBit, hostVisible: true);
        _vertices.Upload<GroundVertex>(
            mesh.Vertices.Length, into => mesh.Vertices.CopyTo(into),
            PipelineStageFlags2.VertexAttributeInputBit, AccessFlags2.VertexAttributeReadBit);
        _indices.Upload<uint>(
            mesh.Indices.Length, into => mesh.Indices.CopyTo(into), PipelineStageFlags2.IndexInputBit, AccessFlags2.IndexReadBit);

        SpriteCapacity = Math.Max(1, spriteCapacity);
        AboveCapacity = Math.Max(0, aboveCapacity);
        _glyphs = GpuTexture.LoadEmbedded(vk, GlyphSheet.Resource);

        // The draw's count lives here, in memory, which is what lets the recording be final: a ground
        // layer switched off changes a number the GPU reads, and not a command buffer. The ground is
        // written once and rewritten only under a device wait, so unlike what a frame fills it is one
        // buffer rather than one per image.
        var above = (uint)mesh.Parts[(int)GroundPart.Above].IndexCount;
        Count(_indexCount - above, above);

        CreatePipeline();
        _target = NewTarget();
        CreateTargetDependents();
    }

    /// <summary>The town in a window, which is the only target that can resize or go out of date.</summary>
    public static TownRenderer OnScreen(
        Vk vk, AppWindow window, GroundMesh mesh, IReadOnlyList<string> surfaceTextures,
        IReadOnlyList<SheetSource> sheetTextures, int spriteCapacity, int aboveCapacity) =>
        new(vk, window, default, mesh, surfaceTextures, sheetTextures, spriteCapacity, aboveCapacity);

    /// <summary>
    /// The same town drawn into one image with no window under it — what a render check is made of,
    /// and the only way to take a shot on a machine nobody is sitting at.
    /// </summary>
    public static TownRenderer Offscreen(
        Vk vk, int width, int height, GroundMesh mesh, IReadOnlyList<string> surfaceTextures,
        IReadOnlyList<SheetSource> sheetTextures, int spriteCapacity, int aboveCapacity) =>
        new(vk, null, new Extent2D((uint)width, (uint)height), mesh, surfaceTextures, sheetTextures, spriteCapacity, aboveCapacity);

    /// <summary>How many sprites the instance buffer was laid for.</summary>
    public int SpriteCapacity { get; }

    /// <summary>And how many more past them for the bodies on the level above (<see cref="SpritesAbove"/>).</summary>
    public int AboveCapacity { get; }

    /// <summary>
    /// The instance buffer as the caller writes it: mapped memory the driver already owns, so filling
    /// it is a write and not an upload. <b>The one belonging to the image the next frame draws into</b>,
    /// which is the image already taken and waited for — see <see cref="_instances"/>.
    /// </summary>
    public Span<SpriteInstance> Sprites => _instances[(int)_image].Span<SpriteInstance>()[..SpriteCapacity];

    /// <summary>
    /// <b>And the bodies on the level above</b> (PHY-1a): the same buffer past <see cref="Sprites"/>, drawn after the
    /// bridges over the ground and so over them, where the first run is drawn under them.
    /// </summary>
    public Span<SpriteInstance> SpritesAbove =>
        _instances[(int)_image].Span<SpriteInstance>().Slice(SpriteCapacity, AboveCapacity);

    /// <summary>How many of the instances just written are to be drawn, of each run. The only thing a frame changes about the sprite pass.</summary>
    public void SetSpriteCount(int count, int above)
    {
        var draws = _spriteIndirect[(int)_image].Span<DrawIndirectCommand>();
        draws[0] = new DrawIndirectCommand { VertexCount = 4, InstanceCount = (uint)Math.Clamp(count, 0, SpriteCapacity) };
        draws[1] = new DrawIndirectCommand { VertexCount = 4, InstanceCount = (uint)Math.Clamp(above, 0, AboveCapacity) };
    }

    /// <summary>
    /// The interface and the debug layers' own instance buffer, written the same way the sprites'
    /// is: mapped memory, no upload, no crossing.
    /// </summary>
    public Span<OverlayQuad> Overlay => _overlay[(int)_image].Span<OverlayQuad>()[..OverlayCapacity];

    /// <summary>
    /// How many overlay quads are to be drawn. <b>A closed panel writes zero and its draw becomes a
    /// no-op the GPU skips</b> — which is the whole reason an interface opening re-records nothing.
    /// </summary>
    public void SetOverlayCount(int count) =>
        _overlayIndirect[(int)_image].Span<DrawIndirectCommand>()[0] = new DrawIndirectCommand
        {
            VertexCount = 4,
            InstanceCount = (uint)Math.Clamp(count, 0, OverlayCapacity),
        };

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
        var draws = _underlayIndirect[(int)_image].Span<DrawIndirectCommand>();
        draws[0] = new DrawIndirectCommand { VertexCount = 4, InstanceCount = (uint)Math.Clamp(count, 0, UnderlayCapacity) };
        draws[1] = new DrawIndirectCommand
        {
            VertexCount = 4,
            InstanceCount = (uint)Math.Clamp(above, 0, UnderlayAboveCapacity),
        };
    }

    /// <summary>
    /// The size of one frame of a sheet cut into a grid, as width over height — what a sprite's quad is
    /// shaped by. <b>The grid is the caller's</b>: what a sheet is cut into is a fact about the thing it
    /// draws, and this layer knows only how big the image is.
    /// </summary>
    public float SheetFrameAspect(int sheet, int columns, int rows) =>
        (_atlas.Places[sheet].WidthPx / columns) / (_atlas.Places[sheet].HeightPx / rows);

    /// <summary>The whole image's width over its height, for the sheets that are one picture rather than a grid — a roof, a prop look.</summary>
    public float SheetAspect(int sheet) => _atlas.Places[sheet].WidthPx / _atlas.Places[sheet].HeightPx;

    /// <summary>How many triangles of the town's standing ground are being drawn.</summary>
    public int TriangleCount => (int)(_indexCount / 3);

    /// <summary>
    /// <b>Which of the ground's own layers are drawn</b> (OBS-2v), as a bit per <see cref="GroundPart"/>.
    /// The parts asked for are packed to the front of the index buffer and the draw's count is cut to what
    /// was written, so a layer switched off costs one upload at the moment it is switched and nothing per
    /// frame — no recording, no pipeline and no ground laid again.
    /// </summary>
    /// <remarks>
    /// <b>The copy is from the mesh and never from the buffer</b>, so a part switched back on comes back
    /// whole however many times the set has changed. The device is waited on first: the memory being
    /// rewritten is the memory a frame still in flight is reading its triangles out of.
    /// </remarks>
    public void ShowGround(uint parts)
    {
        if (parts == _shownParts) return;

        _shownParts = parts;
        _vk.Api.DeviceWaitIdle(_vk.Device);

        var written = 0;
        for (var part = 0; part < GroundParts.Count; part++)
        {
            if ((parts & (1u << part)) != 0) written += _mesh.Parts[part].IndexCount;
        }

        _indices.Upload<uint>(written, Packed, PipelineStageFlags2.IndexInputBit, AccessFlags2.IndexReadBit);

        // The level above is the last part and so the last run packed: the second draw is whatever of it is shown.
        _indexCount = (uint)written;
        var above = (parts & GroundParts.Bit(GroundPart.Above)) != 0 ? (uint)_mesh.Parts[(int)GroundPart.Above].IndexCount : 0u;
        Count(_indexCount - above, above);

        void Packed(Span<uint> into)
        {
            var at = 0;
            for (var part = 0; part < GroundParts.Count; part++)
            {
                if ((parts & (1u << part)) == 0) continue;

                var tally = _mesh.Parts[part];
                _mesh.Indices.Slice(tally.FirstIndex, tally.IndexCount).CopyTo(into[at..]);
                at += tally.IndexCount;
            }
        }
    }

    /// <summary>
    /// The ground's two draws: everything on the ground from the first index, and the level above after it
    /// (<see cref="GroundPart.Above"/>) — the one drawn under the bodies on the ground and the other over them.
    /// </summary>
    void Count(uint ground, uint above)
    {
        var draws = _indirect.Span<DrawIndexedIndirectCommand>();
        draws[0] = new DrawIndexedIndirectCommand { IndexCount = ground, InstanceCount = 1 };
        draws[1] = new DrawIndexedIndirectCommand { IndexCount = above, InstanceCount = 1, FirstIndex = ground };
    }

    public Extent2D Size => _target.Extent;

    /// <summary>
    /// What the last <see cref="Frame"/> spent waiting on the presentation engine rather than working:
    /// the acquire, and the fence of the frame two back. Not this build's cost, and the whole of what a
    /// frame rate under FIFO is made of.
    /// </summary>
    public double BlockedMs { get; private set; }

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
        _cameras[image].Span<CameraView>()[0] = view with { SurfacePeriodsM = _mesh.SurfacePeriodsM, WaterPeriodM = _mesh.WaterPeriodM };

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

        _acquireSlot = (int)(_acquires++ % _acquired.Length);
        if (!_target.Acquire(_acquired[_acquireSlot], out _image))
        {
            // Out of date: there is no image, and nothing to wait on. The rebuild takes one of its own
            // — and a rebuild already under way is left to finish rather than started again inside
            // itself.
            if (!_rebuilding) Recreate();
            return;
        }

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
                     _overlayVertexShader, _overlayFragmentShader])
        {
            Vk.Count();
            _vk.Api.DestroyShaderModule(_vk.Device, shader, null);
        }

        _vertices.Dispose();
        _indices.Dispose();
        _indirect.Dispose();
        _glyphs.Dispose();
        _sheetTable.Dispose();
        _sheetPages.Dispose();
        foreach (var texture in _textures) texture.Dispose();
    }

}
