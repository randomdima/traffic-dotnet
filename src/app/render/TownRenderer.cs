
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Runtime;
using Image = Silk.NET.Vulkan.Image;
using Semaphore = Silk.NET.Vulkan.Semaphore;
// This slice's own device wrapper, not the loader of the same name.
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.App.Render;

/// <summary>What the vertex shader is told about the view. Written into mapped memory, never pushed.</summary>
/// <remarks>
/// The screen's size is here rather than in a push constant for the same reason the camera is: a value
/// pushed per frame would be recorded per frame. It is the window in interface pixels and not the
/// framebuffer — on a scaled desktop those differ by <see cref="AppWindow.UiScale"/>, and that division
/// is what makes a panel the size it was designed to be on a display of any density.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct CameraView(Vector2 CentreM, Vector2 ClipPerM, Vector2 UiPx);

/// <summary>
/// The town's ground on screen: one pipeline, <b>one command buffer per swapchain image recorded
/// once</b>, and one indirect draw whose index count lives in a buffer the CPU writes rather than in
/// the call.
/// </summary>
/// <remarks>
/// The frame is five crossings — acquire, wait, reset, submit, present — and not one takes the size of
/// the town as an argument. The camera moves by a write into mapped memory and the recording never
/// changes; a panel opening changes a count in the indirect buffer and nothing else. A Vulkan renderer
/// that re-recorded every frame would be worse than the OpenGL it replaced, and avoiding exactly that
/// is what this shape is for. Rebuilding the target is the one place recording happens again.
/// </remarks>
internal sealed unsafe partial class TownRenderer : IDisposable
{
    /// <summary>Each shader's array is fixed-size, so every slot is written whether or not anything uses it.</summary>
    const int SurfaceSlots = 8;

    /// <summary>
    /// Every walker's look, <em>two</em> per car — the car and the wreck it becomes — one per roof and
    /// one per prop look, with room over the shipped art's hundred and forty-six. The descriptor array
    /// in the shader is unsized, so this is the only place the number lives.
    /// </summary>
    const int SheetSlots = 192;

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

    readonly Vk _vk;
    readonly AppWindow? _window;
    readonly Extent2D _offscreenSize;
    readonly GpuTexture[] _textures;
    readonly GpuTexture[] _sheets;
    readonly GpuTexture _glyphs;
    readonly GpuBuffer _vertices;
    readonly GpuBuffer _indices;
    readonly GpuBuffer _indirect;
    readonly GpuBuffer _instances;
    readonly GpuBuffer _spriteIndirect;
    readonly GpuBuffer _overlay;
    readonly GpuBuffer _overlayIndirect;
    readonly GpuBuffer _underlay;
    readonly GpuBuffer _underlayIndirect;
    readonly uint _indexCount;

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
    CommandBuffer[] _commands = [];
    Fence[] _drawn = [];
    Semaphore[] _rendered = [];
    Semaphore[] _acquired = [];
    long _frame;
    uint _lastImage;

    TownRenderer(
        Vk vk, AppWindow? window, Extent2D offscreenSize, GroundMesh mesh, IReadOnlyList<string> surfaceTextures,
        IReadOnlyList<SheetSource> sheetTextures, int spriteCapacity)
    {
        _vk = vk;
        _window = window;
        _offscreenSize = offscreenSize;

        _textures = new GpuTexture[surfaceTextures.Count];
        for (var texture = 0; texture < _textures.Length; texture++) _textures[texture] = GpuTexture.Load(vk, surfaceTextures[texture]);

        _sheets = new GpuTexture[Math.Max(1, sheetTextures.Count)];
        for (var sheet = 0; sheet < sheetTextures.Count; sheet++)
        {
            var source = sheetTextures[sheet];
            _sheets[sheet] = source.Path is { } path
                ? GpuTexture.Load(vk, path, source.Repeats, source.Mipped)
                : GpuTexture.FromPixels(vk, source.Rgba!, source.WidthPx, source.HeightPx, source.Repeats, source.Mipped);
        }

        // A renderer with no sheets still has to fill every slot of the shader's array, so the ground
        // stands in for the sprites nobody asked for.
        for (var sheet = sheetTextures.Count; sheet < _sheets.Length; sheet++) _sheets[sheet] = _textures[0];

        var vertices = mesh.Vertices;
        var indices = mesh.Indices;
        _indexCount = (uint)indices.Length;
        _vertices = vk.CreateBuffer((ulong)(vertices.Length * sizeof(GroundVertex)), BufferUsageFlags.VertexBufferBit, hostVisible: true);
        _indices = vk.CreateBuffer((ulong)(indices.Length * sizeof(uint)), BufferUsageFlags.IndexBufferBit, hostVisible: true);
        _indirect = vk.CreateBuffer((ulong)sizeof(DrawIndexedIndirectCommand), BufferUsageFlags.IndirectBufferBit, hostVisible: true);
        _vertices.Write(vertices);
        _indices.Write(indices);

        SpriteCapacity = Math.Max(1, spriteCapacity);
        _instances = vk.CreateBuffer((ulong)(SpriteCapacity * sizeof(SpriteInstance)), BufferUsageFlags.VertexBufferBit, hostVisible: true);
        _spriteIndirect = vk.CreateBuffer((ulong)sizeof(DrawIndirectCommand), BufferUsageFlags.IndirectBufferBit, hostVisible: true);

        _glyphs = GpuTexture.LoadEmbedded(vk, GlyphSheet.Resource);
        _overlay = vk.CreateBuffer((ulong)(OverlayCapacity * sizeof(OverlayQuad)), BufferUsageFlags.VertexBufferBit, hostVisible: true);
        _overlayIndirect = vk.CreateBuffer((ulong)sizeof(DrawIndirectCommand), BufferUsageFlags.IndirectBufferBit, hostVisible: true);
        _underlay = vk.CreateBuffer((ulong)(UnderlayCapacity * sizeof(OverlayQuad)), BufferUsageFlags.VertexBufferBit, hostVisible: true);
        _underlayIndirect = vk.CreateBuffer((ulong)sizeof(DrawIndirectCommand), BufferUsageFlags.IndirectBufferBit, hostVisible: true);

        // The draws' counts live here, in memory, which is what lets the recording be final: a town
        // that gains a walker changes a number the GPU reads, and not a command buffer.
        _indirect.Span<DrawIndexedIndirectCommand>()[0] = new DrawIndexedIndirectCommand
        {
            IndexCount = _indexCount,
            InstanceCount = 1,
        };
        _spriteIndirect.Span<DrawIndirectCommand>()[0] = new DrawIndirectCommand { VertexCount = 4, InstanceCount = 0 };
        _overlayIndirect.Span<DrawIndirectCommand>()[0] = new DrawIndirectCommand { VertexCount = 4, InstanceCount = 0 };
        _underlayIndirect.Span<DrawIndirectCommand>()[0] = new DrawIndirectCommand { VertexCount = 4, InstanceCount = 0 };

        CreatePipeline();
        _target = NewTarget();
        CreateTargetDependents();
    }

    /// <summary>The town in a window, which is the only target that can resize or go out of date.</summary>
    public static TownRenderer OnScreen(
        Vk vk, AppWindow window, GroundMesh mesh, IReadOnlyList<string> surfaceTextures,
        IReadOnlyList<SheetSource> sheetTextures, int spriteCapacity) =>
        new(vk, window, default, mesh, surfaceTextures, sheetTextures, spriteCapacity);

    /// <summary>
    /// The same town drawn into one image with no window under it — what a render check is made of,
    /// and the only way to take a shot on a machine nobody is sitting at.
    /// </summary>
    public static TownRenderer Offscreen(
        Vk vk, int width, int height, GroundMesh mesh, IReadOnlyList<string> surfaceTextures,
        IReadOnlyList<SheetSource> sheetTextures, int spriteCapacity) =>
        new(vk, null, new Extent2D((uint)width, (uint)height), mesh, surfaceTextures, sheetTextures, spriteCapacity);

    /// <summary>How many sprites the instance buffer was laid for.</summary>
    public int SpriteCapacity { get; }

    /// <summary>
    /// The instance buffer as the caller writes it: mapped memory the driver already owns, so filling
    /// it is a write and not an upload.
    /// </summary>
    public Span<SpriteInstance> Sprites => _instances.Span<SpriteInstance>()[..SpriteCapacity];

    /// <summary>How many of the instances just written are to be drawn. The only thing a frame changes about the sprite pass.</summary>
    public void SetSpriteCount(int count) =>
        _spriteIndirect.Span<DrawIndirectCommand>()[0] = new DrawIndirectCommand
        {
            VertexCount = 4,
            InstanceCount = (uint)Math.Clamp(count, 0, SpriteCapacity),
        };

    /// <summary>
    /// The interface and the debug layers' own instance buffer, written the same way the sprites'
    /// is: mapped memory, no upload, no crossing.
    /// </summary>
    public Span<OverlayQuad> Overlay => _overlay.Span<OverlayQuad>()[..OverlayCapacity];

    /// <summary>
    /// How many overlay quads are to be drawn. <b>A closed panel writes zero and its draw becomes a
    /// no-op the GPU skips</b> — which is the whole reason an interface opening re-records nothing.
    /// </summary>
    public void SetOverlayCount(int count) =>
        _overlayIndirect.Span<DrawIndirectCommand>()[0] = new DrawIndirectCommand
        {
            VertexCount = 4,
            InstanceCount = (uint)Math.Clamp(count, 0, OverlayCapacity),
        };

    /// <summary>The same buffer's worth of quads drawn <em>under</em> the bodies — the town's own ground marks.</summary>
    public Span<OverlayQuad> Underlay => _underlay.Span<OverlayQuad>()[..UnderlayCapacity];

    /// <summary>And how many of those are to be drawn, on the same terms.</summary>
    public void SetUnderlayCount(int count) =>
        _underlayIndirect.Span<DrawIndirectCommand>()[0] = new DrawIndirectCommand
        {
            VertexCount = 4,
            InstanceCount = (uint)Math.Clamp(count, 0, UnderlayCapacity),
        };

    /// <summary>
    /// The size of one frame of a sheet cut into a grid, as width over height — what a sprite's quad is
    /// shaped by. <b>The grid is the caller's</b>: what a sheet is cut into is a fact about the thing it
    /// draws, and this layer knows only how big the image is.
    /// </summary>
    public float SheetFrameAspect(int sheet, int columns, int rows) =>
        (_sheets[sheet].Width / (float)columns) / (_sheets[sheet].Height / (float)rows);

    /// <summary>The whole image's width over its height, for the sheets that are one picture rather than a grid — a roof, a prop look.</summary>
    public float SheetAspect(int sheet) => _sheets[sheet].Width / (float)_sheets[sheet].Height;

    /// <summary>How many triangles the town's standing ground came to.</summary>
    public int TriangleCount => (int)(_indexCount / 3);

    public Extent2D Size => _target.Extent;

    /// <summary>
    /// What the last <see cref="Frame"/> spent waiting on the presentation engine rather than working:
    /// the acquire, and the fence of the frame two back. Not this build's cost, and the whole of what a
    /// frame rate under FIFO is made of.
    /// </summary>
    public double BlockedMs { get; private set; }

    /// <summary>
    /// One frame. Everything that changes between frames is already in mapped memory, so what is left
    /// is the five calls the design is named for.
    /// </summary>
    public void Frame(CameraView view)
    {
        var api = _vk.Api;
        var acquired = _acquired[(int)(_frame % _acquired.Length)];
        var waitedFrom = Stopwatch.GetTimestamp();

        if (!_target.Acquire(acquired, out var image))
        {
            Recreate();
            return;
        }

        var fence = _drawn[image];
        Vk.Count();
        Vk.Check(api.WaitForFences(_vk.Device, 1, &fence, true, ulong.MaxValue), "vkWaitForFences");
        BlockedMs = Stopwatch.GetElapsedTime(waitedFrom).TotalMilliseconds;
        Vk.Count();
        Vk.Check(api.ResetFences(_vk.Device, 1, &fence), "vkResetFences");

        _cameras[image].Span<CameraView>()[0] = view;

        var commands = _commands[image];
        var rendered = _rendered[image];
        var waitStage = PipelineStageFlags.ColorAttachmentOutputBit;

        // Nothing is being shown offscreen, so there is nothing to wait for the presenter to let go
        // of and nothing to tell it when the frame is done: the fence above is the whole story.
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
        Vk.Check(api.QueueSubmit(_vk.Queue, 1, &submit, fence), "vkQueueSubmit");

        if (!_target.Present(rendered, image)) Recreate();

        _lastImage = image;
        _frame++;
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

        Vk.Count();
        _vk.Api.DeviceWaitIdle(_vk.Device);
        DestroyTargetDependents();
        _target.Dispose();
        _target = NewTarget();
        CreateTargetDependents();
    }

    /// <summary>
    /// The frame that was last drawn, read back off the image it was drawn into. The reason every target
    /// carries <c>TRANSFER_SRC</c>.
    /// </summary>
    public void Shot(string path)
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
        shot.SaveAsPng(path);
    }

    public void Dispose()
    {
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
        _instances.Dispose();
        _spriteIndirect.Dispose();
        _overlay.Dispose();
        _overlayIndirect.Dispose();
        _underlay.Dispose();
        _underlayIndirect.Dispose();
        _glyphs.Dispose();
        foreach (var texture in _textures) texture.Dispose();

        // The stand-in slots are the ground's own textures, already disposed above.
        for (var sheet = 0; sheet < _sheets.Length; sheet++)
        {
            if (Array.IndexOf(_textures, _sheets[sheet]) < 0) _sheets[sheet].Dispose();
        }
    }

}
