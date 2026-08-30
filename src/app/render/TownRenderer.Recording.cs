using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TrafficSimulation.Runtime;
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

        var sizes = stackalloc DescriptorPoolSize[2];
        sizes[0] = new DescriptorPoolSize(DescriptorType.CombinedImageSampler, (uint)((Bindings - SheetPagesBinding) * images));
        sizes[1] = new DescriptorPoolSize(DescriptorType.UniformBuffer, (uint)(SheetPagesBinding * images));
        var poolInfo = new DescriptorPoolCreateInfo
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            MaxSets = (uint)images,
            PoolSizeCount = 2,
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
        _commands = _vk.AllocateCommandBuffers(images);
        _drawn = new Fence[images];
        _rendered = new Semaphore[images];
        _acquired = new Semaphore[images];

        // The pictures, in binding order: the atlas, the glyphs, the tile and the five surfaces. A town
        // with no tiling sheet binds the ground in that slot, which nothing then samples.
        const int firstPicture = SheetPagesBinding;
        var pictures = stackalloc DescriptorImageInfo[Bindings - firstPicture];
        pictures[SheetPagesBinding - firstPicture] = Picture(_sheetPages);
        pictures[GlyphBinding - firstPicture] = Picture(_glyphs);
        pictures[TileBinding - firstPicture] = Picture(_tile ?? _textures[0]);
        for (var surface = 0; surface < Surfaces; surface++)
        {
            pictures[FirstSurfaceBinding - firstPicture + surface] =
                Picture(_textures[Math.Min(surface, _textures.Length - 1)]);
        }

        var table = new DescriptorBufferInfo(_sheetTable.Handle, 0, _sheetTable.SizeBytes);

        var writes = stackalloc WriteDescriptorSet[Bindings];
        for (var image = 0; image < images; image++)
        {
            _cameras[image] = _vk.CreateBuffer((ulong)sizeof(CameraView), BufferUsageFlags.UniformBufferBit, hostVisible: true);

            var camera = new DescriptorBufferInfo(_cameras[image].Handle, 0, (ulong)sizeof(CameraView));
            writes[CameraBinding] = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = _sets[image],
                DstBinding = CameraBinding,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.UniformBuffer,
                PBufferInfo = &camera,
            };
            writes[SheetTableBinding] = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = _sets[image],
                DstBinding = SheetTableBinding,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.UniformBuffer,
                PBufferInfo = &table,
            };
            for (var binding = firstPicture; binding < Bindings; binding++)
            {
                writes[binding] = new WriteDescriptorSet
                {
                    SType = StructureType.WriteDescriptorSet,
                    DstSet = _sets[image],
                    DstBinding = (uint)binding,
                    DescriptorCount = 1,
                    DescriptorType = DescriptorType.CombinedImageSampler,
                    PImageInfo = pictures + binding - firstPicture,
                };
            }

            Vk.Count();
            api.UpdateDescriptorSets(_vk.Device, Bindings, writes, 0, null);

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

        Vk.Count();
        api.CmdDrawIndexedIndirect(commands, _indirect.Handle, 0, 1, (uint)sizeof(DrawIndexedIndirectCommand));

        // The town's own ground marks, over the ground and under everything that stands on it: the
        // stretches of road the book says are spoken for, and the networks under them. They are marks
        // about the *ground* rather than about a body, so a car standing on a reservation has to read
        // over it — drawn after the bodies, the wash tints every sprite it covers.
        Vk.Count();
        api.CmdBindPipeline(commands, PipelineBindPoint.Graphics, _overlayPipeline);
        var underlayBuffer = _underlay.Handle;
        Vk.Count();
        api.CmdBindVertexBuffers(commands, 0, 1, &underlayBuffer, &offset);
        Vk.Count();
        api.CmdDrawIndirect(commands, _underlayIndirect.Handle, 0, 1, (uint)sizeof(DrawIndirectCommand));

        // The bodies, over the ground: the same set, a different pipeline, and an instance buffer
        // whose contents and count both live in memory the CPU writes. A town that gains five hundred
        // walkers changes a number here and nothing about this recording.
        Vk.Count();
        api.CmdBindPipeline(commands, PipelineBindPoint.Graphics, _spritePipeline);
        var instanceBuffer = _instances.Handle;
        Vk.Count();
        api.CmdBindVertexBuffers(commands, 0, 1, &instanceBuffer, &offset);
        Vk.Count();
        api.CmdDrawIndirect(commands, _spriteIndirect.Handle, 0, 1, (uint)sizeof(DrawIndirectCommand));

        // The interface and everything that annotates a body, over all of it: the same pipeline the
        // ground marks used, a buffer of its own, and one more indirect draw already written down here.
        // A panel opening changes the count in that buffer and nothing else, which is why the interface
        // is inside the five crossings.
        Vk.Count();
        api.CmdBindPipeline(commands, PipelineBindPoint.Graphics, _overlayPipeline);
        var overlayBuffer = _overlay.Handle;
        Vk.Count();
        api.CmdBindVertexBuffers(commands, 0, 1, &overlayBuffer, &offset);
        Vk.Count();
        api.CmdDrawIndirect(commands, _overlayIndirect.Handle, 0, 1, (uint)sizeof(DrawIndirectCommand));

        Vk.Count();
        api.CmdEndRendering(commands);

        Barrier(commands, _target.Images[image], ImageLayout.ColorAttachmentOptimal, _target.FinalLayout);

        Vk.Count();
        Vk.Check(api.EndCommandBuffer(commands), "vkEndCommandBuffer");
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

        _drawn = [];
        _rendered = [];
        _acquired = [];
        _cameras = [];
        _commands = [];
        _sets = [];
    }
}
