using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Terrain;
using Image = Silk.NET.Vulkan.Image;
using Semaphore = Silk.NET.Vulkan.Semaphore;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.App.Render;

/// <summary>Everything tied to the current target: the framebuffers and sets rebuilt on a resize, and the one command buffer per image the frame is written into.</summary>
internal sealed unsafe partial class TownRenderer
{
    void CreateTargetDependents()
    {
        var api = _vk.Api;
        var images = _target.ImageCount;

        CreateGroundPipeline();
        CreateSpritePipeline();
        CreateOverlayPipeline();
        CreateMarkPipeline();
        CreateSceneryPipeline();

        var sizes = stackalloc DescriptorPoolSize[3];
        for (var type = 0; type < 3; type++)
        {
            var kind = (ReadOnlySpan<DescriptorType>)[DescriptorType.CombinedImageSampler, DescriptorType.UniformBuffer, DescriptorType.StorageBuffer];
            var count = 0;
            for (var binding = 0; binding < Bindings; binding++)
            {
                if (TypeOf(binding) == kind[type]) count++;
            }

            sizes[type] = new DescriptorPoolSize(kind[type], (uint)(count * images));
        }

        var poolInfo = new DescriptorPoolCreateInfo
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            MaxSets = (uint)images,
            PoolSizeCount = 3,
            PPoolSizes = sizes,
        };

        Vk.Count();
        Vk.Check(api.CreateDescriptorPool(_vk.Device, &poolInfo, null, out _descriptors), "vkCreateDescriptorPool");

        var layouts = new DescriptorSetLayout[images];
        Array.Fill(layouts, _setLayout);
        _sets = new DescriptorSet[images];
        fixed (DescriptorSetLayout* layoutsPtr = layouts)
        fixed (DescriptorSet* setsPtr = _sets)
        {
            var allocate = new DescriptorSetAllocateInfo
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = _descriptors,
                DescriptorSetCount = (uint)images,
                PSetLayouts = layoutsPtr,
            };

            Vk.Count();
            Vk.Check(api.AllocateDescriptorSets(_vk.Device, &allocate, setsPtr), "vkAllocateDescriptorSets");
        }

        _cameras = new GpuBuffer[images];
        _markRows = new GpuBuffer[images];
        _markRings = new GpuBuffer[images];
        _marksCopied = new long[images];
        _scenerySpans = new GpuBuffer[images];
        _groundDraws = new GpuBuffer[images];
        _instances = new GpuBuffer[images];
        _spriteIndirect = new GpuBuffer[images];
        _overlay = new GpuBuffer[images];
        _overlayIndirect = new GpuBuffer[images];
        _underlay = new GpuBuffer[images];
        _underlayIndirect = new GpuBuffer[images];
        _commands = _vk.AllocateCommandBuffers(images);
        _drawn = new Fence[images];
        _rendered = new Semaphore[images];
        _acquired = new Semaphore[images + 1];
        _acquiredBy = new int[images];
        for (var image = 0; image < images; image++) _acquiredBy[image] = image;
        _acquireSpare = images;
        _image = 0;

        // The pictures, in binding order: the atlas, the glyphs and the five surfaces.
        const int firstPicture = SheetPagesBinding;
        var pictures = stackalloc DescriptorImageInfo[Bindings - firstPicture];
        pictures[SheetPagesBinding - firstPicture] = Picture(_sheetPages);
        pictures[GlyphBinding - firstPicture] = Picture(_glyphs);
        for (var surface = 0; surface < Surfaces; surface++)
        {
            pictures[FirstSurfaceBinding - firstPicture + surface] =
                Picture(_textures[Math.Min(surface, _textures.Length - 1)]);
        }

        var buffers = stackalloc DescriptorBufferInfo[Bindings];
        var writes = stackalloc WriteDescriptorSet[Bindings];
        for (var image = 0; image < images; image++)
        {
            _cameras[image] = _vk.CreateBuffer((ulong)sizeof(CameraView), BufferUsageFlags.UniformBufferBit, HostAccess.Streams);
            CreateFrameBuffers(image);

            buffers[CameraBinding] = new DescriptorBufferInfo(_cameras[image].Handle, 0, (ulong)sizeof(CameraView));
            buffers[SheetTableBinding] = Whole(_sheetTable);
            buffers[MarkLookBinding] = Whole(_markLook);
            buffers[SceneryPiecesBinding] = Whole(_sceneryPieces);
            buffers[SceneryLooksBinding] = Whole(_sceneryLooks);
            buffers[ScenerySpansBinding] = Whole(_scenerySpans[image]);
            // The two the marks are drawn from are written apart (WriteTheMarkSet), being the device's own once it keeps the ring.
            var written = 0;
            for (var binding = 0; binding < Bindings; binding++)
            {
                if (binding is MarkRowsBinding or MarkRingBinding) continue;

                var picture = TypeOf(binding) == DescriptorType.CombinedImageSampler;
                writes[written++] = new WriteDescriptorSet
                {
                    SType = StructureType.WriteDescriptorSet,
                    DstSet = _sets[image],
                    DstBinding = (uint)binding,
                    DescriptorCount = 1,
                    DescriptorType = TypeOf(binding),
                    PImageInfo = picture ? pictures + binding - firstPicture : null,
                    PBufferInfo = picture ? null : buffers + binding,
                };
            }

            Vk.Count();
            api.UpdateDescriptorSets(_vk.Device, (uint)written, writes, 0, null);
            WriteTheMarkSet(image);

            var fenceInfo = new FenceCreateInfo { SType = StructureType.FenceCreateInfo, Flags = FenceCreateFlags.SignaledBit };
            Vk.Count();
            Vk.Check(api.CreateFence(_vk.Device, &fenceInfo, null, out _drawn[image]), "vkCreateFence");

            var semaphoreInfo = new SemaphoreCreateInfo { SType = StructureType.SemaphoreCreateInfo };
            Vk.Count();
            Vk.Check(api.CreateSemaphore(_vk.Device, &semaphoreInfo, null, out _rendered[image]), "vkCreateSemaphore");
            Vk.Count();
            Vk.Check(api.CreateSemaphore(_vk.Device, &semaphoreInfo, null, out _acquired[image]), "vkCreateSemaphore");

            Record(image);
        }

        var spareInfo = new SemaphoreCreateInfo { SType = StructureType.SemaphoreCreateInfo };
        Vk.Count();
        Vk.Check(api.CreateSemaphore(_vk.Device, &spareInfo, null, out _acquired[images]), "vkCreateSemaphore");

        // The first image, taken before anything is filled — every one after it is taken at the end of
        // the frame before.
        TakeImage();
    }

    /// <summary>
    /// The buffers one image's frame is written into: its sprites, the quads over and under them, and
    /// the three counts the indirect draws read. One set per image because the CPU fills the next frame
    /// while the last is still being drawn — <see cref="_instances"/>.
    /// </summary>
    void CreateFrameBuffers(int image)
    {
        _instances[image] = _vk.CreateBuffer(
            (ulong)(Room.Moving * sizeof(SpriteInstance)), BufferUsageFlags.VertexBufferBit, HostAccess.Streams);

        // The marks' copy starts empty and is filled whole by the first frame drawn into it; the scenery's runs hold none
        // until a frame writes them.
        _markRows[image] = _vk.CreateBuffer(
            (ulong)(Room.Marks * sizeof(DriftMarkRow)), BufferUsageFlags.StorageBufferBit, HostAccess.Streams);
        _markRings[image] = _vk.CreateBuffer(
            (ulong)sizeof(DriftMarkRing), BufferUsageFlags.StorageBufferBit | BufferUsageFlags.IndirectBufferBit, HostAccess.Streams);
        _markRings[image].Span<DriftMarkRing>()[0] = DriftMarkRing.After(0, Room.Marks);
        _marksCopied[image] = 0;
        _scenerySpans[image] = _vk.CreateBuffer(
            (ulong)(sizeof(SceneryRunsHead) + (Room.SceneryRows * sizeof(ScenerySpan))),
            BufferUsageFlags.StorageBufferBit | BufferUsageFlags.IndirectBufferBit, HostAccess.Streams);
        _scenerySpans[image].Span<SceneryRunsHead>()[0] = new SceneryRunsHead(new DrawIndirectCommand { VertexCount = 4 }, 1, 0);
        _overlay[image] = _vk.CreateBuffer(
            (ulong)(OverlayCapacity * sizeof(OverlayQuad)), BufferUsageFlags.VertexBufferBit, HostAccess.Streams);
        _underlay[image] = _vk.CreateBuffer(
            (ulong)((UnderlayCapacity + UnderlayAboveCapacity) * sizeof(OverlayQuad)), BufferUsageFlags.VertexBufferBit,
            HostAccess.Streams);

        _groundDraws[image] = _vk.CreateBuffer(
            (ulong)(_bands.Draws * sizeof(DrawIndexedIndirectCommand)), BufferUsageFlags.IndirectBufferBit, HostAccess.Streams);
        _bands.WriteWhole(_shownParts, _groundDraws[image].Span<DrawIndexedIndirectCommand>());
        _spriteIndirect[image] = Counter(draws: SpriteDraws);
        _overlayIndirect[image] = Counter(draws: 1);
        _underlayIndirect[image] = Counter(draws: 2);

        // An image whose frame has not been filled yet draws nothing of its own rather than whatever
        // the memory happened to hold.
        GpuBuffer Counter(int draws)
        {
            var buffer = _vk.CreateBuffer(
                (ulong)(draws * sizeof(DrawIndirectCommand)), BufferUsageFlags.IndirectBufferBit, HostAccess.Streams);
            buffer.Span<DrawIndirectCommand>().Fill(new DrawIndirectCommand { VertexCount = 4, InstanceCount = 0 });
            return buffer;
        }
    }

    /// <summary>Which kind of descriptor a binding of the frame's one set is: the three uniform blocks, the pictures, and the storage the marks and the scenery are read out of.</summary>
    static DescriptorType TypeOf(int binding) => binding switch
    {
        CameraBinding or SheetTableBinding or MarkLookBinding => DescriptorType.UniformBuffer,
        < MarkLookBinding => DescriptorType.CombinedImageSampler,
        _ => DescriptorType.StorageBuffer,
    };

    static DescriptorBufferInfo Whole(GpuBuffer buffer) => new(buffer.Handle, 0, buffer.SizeBytes);

    /// <summary>One image's set pointed at the ring its marks are drawn from: its own copy of the host's, or the device's.</summary>
    void WriteTheMarkSet(int image)
    {
        var (rows, ring) = _deviceMarks ?? (_markRows[image], _markRings[image]);
        var infos = stackalloc DescriptorBufferInfo[2] { Whole(rows), Whole(ring) };
        var writes = stackalloc WriteDescriptorSet[2];
        for (var at = 0; at < 2; at++)
        {
            writes[at] = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = _sets[image],
                DstBinding = (uint)(MarkRowsBinding + at),
                DescriptorCount = 1,
                DescriptorType = DescriptorType.StorageBuffer,
                PBufferInfo = infos + at,
            };
        }

        Vk.Count();
        _vk.Api.UpdateDescriptorSets(_vk.Device, 2, writes, 0, null);
    }

    /// <summary>
    /// The whole frame, written down once. What is <em>not</em> in here is as important as what is:
    /// no camera, no counts, and nothing that knows how big the town is.
    /// </summary>
    void Record(int image)
    {
        var api = _vk.Api;
        var commands = _commands[image];

        var begin = new CommandBufferBeginInfo { SType = StructureType.CommandBufferBeginInfo };
        Vk.Count();
        Vk.Check(api.BeginCommandBuffer(commands, &begin), "vkBeginCommandBuffer");

        // What moves, emitted on the device ahead of the pass that draws it, where the device emits it.
        _emit?.Record(commands, image);

        Barrier(commands, _target.Images[image], ImageLayout.Undefined, ImageLayout.ColorAttachmentOptimal);

        var clear = new ClearValue(new ClearColorValue(0f, 0f, 0f, 1f));
        var attachment = new RenderingAttachmentInfo
        {
            SType = StructureType.RenderingAttachmentInfo,
            ImageView = _target.Views[image],
            ImageLayout = ImageLayout.ColorAttachmentOptimal,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            ClearValue = clear,
        };

        var rendering = new RenderingInfo
        {
            SType = StructureType.RenderingInfo,
            RenderArea = new Rect2D(new Offset2D(0, 0), _target.Extent),
            LayerCount = 1,
            ColorAttachmentCount = 1,
            PColorAttachments = &attachment,
        };

        Vk.Count();
        api.CmdBeginRendering(commands, &rendering);

        var viewport = new Viewport(0f, 0f, _target.Extent.Width, _target.Extent.Height, 0f, 1f);
        Vk.Count();
        api.CmdSetViewport(commands, 0, 1, &viewport);
        var scissor = new Rect2D(new Offset2D(0, 0), _target.Extent);
        Vk.Count();
        api.CmdSetScissor(commands, 0, 1, &scissor);

        Vk.Count();
        api.CmdBindPipeline(commands, PipelineBindPoint.Graphics, _pipeline);

        var set = _sets[image];
        Vk.Count();
        api.CmdBindDescriptorSets(commands, PipelineBindPoint.Graphics, _pipelineLayout, 0, 1, &set, 0, null);

        var vertexBuffer = _vertices.Handle;
        ulong offset = 0;
        Vk.Count();
        api.CmdBindVertexBuffers(commands, 0, 1, &vertexBuffer, &offset);
        Vk.Count();
        api.CmdBindIndexBuffer(commands, _indices.Handle, 0, IndexType.Uint32);

        // The ground's parts bottom to top (TER-7b), each a draw a class of its rows (GroundBands); the level above is the
        // last part, drawn after the bodies on the ground.
        for (var part = 0; part < (int)GroundPart.Above; part++) DrawTheGround(commands, image, part);

        // The town's own ground marks, over the ground and under everything that stands on it: the
        // stretches of road somebody has claimed, and the networks under them. They are marks about
        // the *ground* rather than about a body, so a car standing on a claim reads over it — drawn
        // after the bodies, the wash would tint every sprite it covers.
        Vk.Count();
        api.CmdBindPipeline(commands, PipelineBindPoint.Graphics, _overlayPipeline);
        var underlayBuffer = _underlay[image].Handle;
        Vk.Count();
        api.CmdBindVertexBuffers(commands, 0, 1, &underlayBuffer, &offset);
        Vk.Count();
        api.CmdDrawIndirect(commands, _underlayIndirect[image].Handle, 0, 1, (uint)sizeof(DrawIndirectCommand));

        // What lies under everything that stands, in painter's order: the marks, out of their ring and as many as its
        // words say (the device's own ring, or this image's copy of the host's), then the scenery, out of its pieces by
        // the runs this image's frame wrote. Neither has a vertex buffer: each instance reads its own row.
        var markRing = (_deviceMarks?.Ring ?? _markRings[image]).Handle;
        Vk.Count();
        api.CmdBindPipeline(commands, PipelineBindPoint.Graphics, _markPipeline);
        Vk.Count();
        api.CmdDrawIndirect(commands, markRing, 0, 1, (uint)sizeof(DrawIndirectCommand));
        Vk.Count();
        api.CmdBindPipeline(commands, PipelineBindPoint.Graphics, _sceneryPipeline);
        Vk.Count();
        api.CmdDrawIndirect(commands, _scenerySpans[image].Handle, 0, 1, (uint)sizeof(DrawIndirectCommand));

        // The bodies, over the ground: the same set, a different pipeline, and an instance buffer
        // whose contents and count both live in memory the CPU writes. A town that gains five hundred
        // walkers changes a number here and nothing about this recording. Two runs in painter's order —
        // the buildings and props laid on the device once (where the frame writes only which stretch),
        // and what moves over them.
        Vk.Count();
        api.CmdBindPipeline(commands, PipelineBindPoint.Graphics, _spritePipeline);
        var standingBuffer = _standing.Handle;
        Vk.Count();
        api.CmdBindVertexBuffers(commands, 0, 1, &standingBuffer, &offset);
        Vk.Count();
        api.CmdDrawIndirect(commands, _spriteIndirect[image].Handle, 0, 1, (uint)sizeof(DrawIndirectCommand));

        var moving = MovingRunsOf(image);
        Vk.Count();
        api.CmdBindVertexBuffers(commands, 0, 1, &moving.Instances, &moving.OverAt);
        Vk.Count();
        api.CmdDrawIndirect(commands, moving.Draws, moving.OverDrawAt, 1, (uint)sizeof(DrawIndirectCommand));

        // <b>The level above, over the bodies on the ground, its marks over it, and the bodies on it over those</b>
        // (TER-7b, PHY-1a): the ground's second draw — its last part, past the first draw's — then the marks and the
        // instances laid past the ground's own in their buffers. A town with nothing above its ground draws nothing in
        // any of the three, and records them the same.
        Vk.Count();
        api.CmdBindPipeline(commands, PipelineBindPoint.Graphics, _pipeline);
        Vk.Count();
        api.CmdBindVertexBuffers(commands, 0, 1, &vertexBuffer, &offset);
        DrawTheGround(commands, image, (int)GroundPart.Above);

        Vk.Count();
        api.CmdBindPipeline(commands, PipelineBindPoint.Graphics, _overlayPipeline);
        var underlayAboveOffset = (ulong)(UnderlayCapacity * sizeof(OverlayQuad));
        Vk.Count();
        api.CmdBindVertexBuffers(commands, 0, 1, &underlayBuffer, &underlayAboveOffset);
        Vk.Count();
        api.CmdDrawIndirect(
            commands, _underlayIndirect[image].Handle, (ulong)sizeof(DrawIndirectCommand), 1, (uint)sizeof(DrawIndirectCommand));

        Vk.Count();
        api.CmdBindPipeline(commands, PipelineBindPoint.Graphics, _spritePipeline);
        Vk.Count();
        api.CmdBindVertexBuffers(commands, 0, 1, &moving.Instances, &moving.AboveAt);
        Vk.Count();
        api.CmdDrawIndirect(commands, moving.Draws, moving.AboveDrawAt, 1, (uint)sizeof(DrawIndirectCommand));

        // The interface and everything that annotates a body, over all of it: the same pipeline the
        // ground marks used, a buffer of its own, and one more indirect draw already written down here.
        // A panel opening changes the count in that buffer and nothing else, which is why the interface
        // is inside the five crossings.
        Vk.Count();
        api.CmdBindPipeline(commands, PipelineBindPoint.Graphics, _overlayPipeline);
        var overlayBuffer = _overlay[image].Handle;
        Vk.Count();
        api.CmdBindVertexBuffers(commands, 0, 1, &overlayBuffer, &offset);
        Vk.Count();
        api.CmdDrawIndirect(commands, _overlayIndirect[image].Handle, 0, 1, (uint)sizeof(DrawIndirectCommand));

        Vk.Count();
        api.CmdEndRendering(commands);

        Barrier(commands, _target.Images[image], ImageLayout.ColorAttachmentOptimal, _target.FinalLayout);

        Vk.Count();
        Vk.Check(api.EndCommandBuffer(commands), "vkEndCommandBuffer");
    }

    /// <summary>One part of the ground recorded: a draw a class, each as many triangles from where this image's frame wrote.</summary>
    void DrawTheGround(CommandBuffer commands, int image, int part)
    {
        var stride = (uint)sizeof(DrawIndexedIndirectCommand);
        for (var @class = 0; @class < _bands.Classes; @class++)
        {
            Vk.Count();
            _vk.Api.CmdDrawIndexedIndirect(
                commands, _groundDraws[image].Handle, (ulong)(((part * _bands.Classes) + @class) * stride), 1, stride);
        }
    }

    /// <summary>Where one image's frame draws what moves from: the buffer and where its two runs start in it, and the buffer and where their two draws are.</summary>
    struct MovingRuns
    {
        public Silk.NET.Vulkan.Buffer Instances;
        public ulong OverAt;
        public ulong AboveAt;
        public Silk.NET.Vulkan.Buffer Draws;
        public ulong OverDrawAt;
        public ulong AboveDrawAt;
    }

    /// <summary>
    /// What moves, drawn from this image's own buffer as a frame filled it — or from the device's emit where that is what
    /// draws it (<see cref="DrawTheMovingFrom"/>), the same buffers for every image.
    /// </summary>
    MovingRuns MovingRunsOf(int image)
    {
        var instance = (ulong)sizeof(SpriteInstance);
        var draw = (ulong)sizeof(DrawIndirectCommand);
        if (_emit is { } device)
        {
            return new MovingRuns
            {
                Instances = device.Instances.Handle,
                AboveAt = (ulong)device.AboveFrom * instance,
                Draws = device.Draws.Handle,
                AboveDrawAt = draw,
            };
        }

        return new MovingRuns
        {
            Instances = _instances[image].Handle,
            AboveAt = (ulong)Room.Over * instance,
            Draws = _spriteIndirect[image].Handle,
            OverDrawAt = draw,
            AboveDrawAt = 2 * draw,
        };
    }

    static DescriptorImageInfo Picture(GpuTexture texture) =>
        new(texture.Sampler, texture.View, ImageLayout.ShaderReadOnlyOptimal);

    void Barrier(CommandBuffer commands, Image image, ImageLayout from, ImageLayout to)
    {
        var barrier = new ImageMemoryBarrier2
        {
            SType = StructureType.ImageMemoryBarrier2,
            SrcStageMask = PipelineStageFlags2.AllCommandsBit,
            SrcAccessMask = AccessFlags2.MemoryWriteBit,
            DstStageMask = PipelineStageFlags2.AllCommandsBit,
            DstAccessMask = AccessFlags2.MemoryReadBit | AccessFlags2.MemoryWriteBit,
            OldLayout = from,
            NewLayout = to,
            Image = image,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1),
        };

        var dependency = new DependencyInfo
        {
            SType = StructureType.DependencyInfo,
            ImageMemoryBarrierCount = 1,
            PImageMemoryBarriers = &barrier,
        };

        Vk.Count();
        _vk.Api.CmdPipelineBarrier2(commands, &dependency);
    }

    void DestroyTargetDependents()
    {
        var api = _vk.Api;

        foreach (var fence in _drawn)
        {
            Vk.Count();
            api.DestroyFence(_vk.Device, fence, null);
        }

        foreach (var semaphore in _rendered)
        {
            Vk.Count();
            api.DestroySemaphore(_vk.Device, semaphore, null);
        }

        foreach (var semaphore in _acquired)
        {
            Vk.Count();
            api.DestroySemaphore(_vk.Device, semaphore, null);
        }

        foreach (var camera in _cameras) camera.Dispose();
        foreach (var buffers in (ReadOnlySpan<GpuBuffer[]>)[
                     _instances, _spriteIndirect, _overlay, _overlayIndirect, _underlay, _underlayIndirect, _markRows, _markRings,
                     _scenerySpans, _groundDraws])
        {
            foreach (var buffer in buffers) buffer.Dispose();
        }

        if (_commands.Length > 0)
        {
            fixed (CommandBuffer* p = _commands)
            {
                Vk.Count();
                api.FreeCommandBuffers(_vk.Device, _vk.Pool(), (uint)_commands.Length, p);
            }
        }

        if (_descriptors.Handle != 0)
        {
            Vk.Count();
            api.DestroyDescriptorPool(_vk.Device, _descriptors, null);
            _descriptors = default;
        }

        if (_pipeline.Handle != 0)
        {
            Vk.Count();
            api.DestroyPipeline(_vk.Device, _pipeline, null);
            _pipeline = default;
        }

        if (_spritePipeline.Handle != 0)
        {
            Vk.Count();
            api.DestroyPipeline(_vk.Device, _spritePipeline, null);
            _spritePipeline = default;
        }

        if (_overlayPipeline.Handle != 0)
        {
            Vk.Count();
            api.DestroyPipeline(_vk.Device, _overlayPipeline, null);
            _overlayPipeline = default;
        }

        if (_markPipeline.Handle != 0)
        {
            Vk.Count();
            api.DestroyPipeline(_vk.Device, _markPipeline, null);
            _markPipeline = default;
        }

        if (_sceneryPipeline.Handle != 0)
        {
            Vk.Count();
            api.DestroyPipeline(_vk.Device, _sceneryPipeline, null);
            _sceneryPipeline = default;
        }

        _drawn = [];
        _rendered = [];
        _acquired = [];
        _acquiredBy = [];
        _cameras = [];
        _markRows = [];
        _markRings = [];
        _marksCopied = [];
        _scenerySpans = [];
        _groundDraws = [];
        _instances = [];
        _spriteIndirect = [];
        _overlay = [];
        _overlayIndirect = [];
        _underlay = [];
        _underlayIndirect = [];
        _commands = [];
        _sets = [];
        _holding = false;
    }
}
