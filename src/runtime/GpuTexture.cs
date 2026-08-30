using Silk.NET.Vulkan;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Image = Silk.NET.Vulkan.Image;

namespace TrafficSimulation.Runtime;

/// <summary>One layer's texels, top row first, written into memory the upload already owns.</summary>
internal delegate void LayerFill(int layer, Span<Rgba32> into);

/// <summary>
/// One ground surface on the GPU, with its own mip chain, decoded at startup: there is no bake step to
/// forget. The chain is box-filtered here rather than blitted by the driver, so how sharp the ground
/// reads is a decision in one place. Un-mipped tarmac shimmers the moment the camera pulls back.
/// </summary>
internal sealed unsafe class GpuTexture : IDisposable
{
    readonly Vk _vk;

    GpuTexture(
        Vk vk, Image image, DeviceMemory memory, ImageView view, Sampler sampler,
        int width, int height, int levels, int layers = 1)
    {
        _vk = vk;
        Handle = image;
        Memory = memory;
        View = view;
        Sampler = sampler;
        Width = width;
        Height = height;
        Levels = levels;
        Layers = layers;
    }

    public Image Handle { get; }

    public DeviceMemory Memory { get; }

    public ImageView View { get; }

    public Sampler Sampler { get; }

    public int Width { get; }

    public int Height { get; }

    public int Levels { get; }

    /// <summary>How many array layers the view carries. One for everything but the sheet atlas.</summary>
    public int Layers { get; }

    /// <summary>
    /// An array texture, filled a layer at a time: the sheet atlas, whose pages are its layers.
    /// </summary>
    /// <remarks>
    /// Clamped and un-mipped, because a page is sheets side by side — the packer's gutter is what
    /// makes clamping true at a sheet's own edge, and a mip level would average two sheets together.
    /// The staging buffer is one layer's worth and is reused, so a fifty-megapixel atlas never has
    /// more than one page of it in memory at a time.
    /// </remarks>
    public static GpuTexture Layered(Vk vk, int width, int height, int layers, LayerFill fill)
    {
        var info = new ImageCreateInfo
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Format = Format.R8G8B8A8Unorm,
            Extent = new Extent3D((uint)width, (uint)height, 1),
            MipLevels = 1,
            ArrayLayers = (uint)layers,
            Samples = SampleCountFlags.Count1Bit,
            Tiling = ImageTiling.Optimal,
            Usage = ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit,
            SharingMode = SharingMode.Exclusive,
            InitialLayout = ImageLayout.Undefined,
        };

        Vk.Count();
        Vk.Check(vk.Api.CreateImage(vk.Device, &info, null, out var image), "vkCreateImage");
        var memory = Bind(vk, image);

        using var staging = vk.CreateBuffer((ulong)(width * height * sizeof(Rgba32)), BufferUsageFlags.TransferSrcBit, hostVisible: true);
        for (var layer = 0; layer < layers; layer++)
        {
            fill(layer, staging.Span<Rgba32>());
            var at = layer;
            vk.OneShot(commands =>
            {
                var region = new BufferImageCopy
                {
                    ImageSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, 0, (uint)at, 1),
                    ImageExtent = new Extent3D((uint)width, (uint)height, 1),
                };

                Transition(vk, commands, image, 1, ImageLayout.Undefined, ImageLayout.TransferDstOptimal, at);
                Vk.Count();
                vk.Api.CmdCopyBufferToImage(commands, staging.Handle, image, ImageLayout.TransferDstOptimal, 1, &region);
                Transition(vk, commands, image, 1, ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal, at);
            });
        }

        var viewInfo = new ImageViewCreateInfo
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = image,
            ViewType = ImageViewType.Type2DArray,
            Format = Format.R8G8B8A8Unorm,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, (uint)layers),
        };

        Vk.Count();
        Vk.Check(vk.Api.CreateImageView(vk.Device, &viewInfo, null, out var view), "vkCreateImageView");

        return new GpuTexture(vk, image, memory, view, Clamped(vk), width, height, 1, layers);
    }

    /// <summary>
    /// One image on the GPU.
    /// </summary>
    /// <param name="repeats">
    /// True for a ground surface, which is wrap-seamless and anchored to the world origin. <b>False
    /// for a sprite sheet</b>, whose neighbouring texel across an edge belongs to a different pose.
    /// </param>
    /// <param name="mipped">
    /// True for a ground surface. False for a sprite sheet: a mip level averages texels either side of
    /// it, and on a sheet those are two different pictures — a chain would bleed the walker's next pose
    /// into this one. If a sprite is ever seen to shimmer the fix is a padded atlas or one array layer
    /// per cell, not a chain over the sheet as it stands.
    /// </param>
    public static GpuTexture Load(Vk vk, string path, bool repeats = true, bool mipped = true)
    {
        using var decoded = SixLabors.ImageSharp.Image.Load<Rgba32>(path);
        return Upload(vk, decoded, repeats, mipped);
    }

    /// <summary>
    /// An image that ships inside the assembly rather than beside it — the glyph sheet, which has no
    /// path to find.
    /// </summary>
    public static GpuTexture LoadEmbedded(Vk vk, string resource, bool repeats = false, bool mipped = false)
    {
        using var stream = typeof(GpuTexture).Assembly.GetManifestResourceStream(resource)
                           ?? throw new InvalidOperationException($"No embedded resource {resource}: did the project file include it?");
        using var decoded = SixLabors.ImageSharp.Image.Load<Rgba32>(stream);
        return Upload(vk, decoded, repeats, mipped);
    }

    /// <summary>
    /// An image this engine builds rather than reads — the mark brushes, which are a gradient a few
    /// texels tall and not a picture. Rows of RGBA bytes, top row first.
    /// </summary>
    public static GpuTexture FromPixels(Vk vk, ReadOnlySpan<byte> rgba, int width, int height, bool repeats = false, bool mipped = false)
    {
        using var decoded = SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(rgba, width, height);
        return Upload(vk, decoded, repeats, mipped);
    }

    static GpuTexture Upload(Vk vk, SixLabors.ImageSharp.Image<Rgba32> decoded, bool repeats, bool mipped)
    {
        var width = decoded.Width;
        var height = decoded.Height;

        var top = new Rgba32[width * height];
        decoded.CopyPixelDataTo(top);

        var chain = mipped
            ? MipChain(top, width, height)
            : [(top, width, height)];
        var totalPixels = 0;
        foreach (var level in chain) totalPixels += level.Pixels.Length;

        using var staging = vk.CreateBuffer((ulong)(totalPixels * sizeof(Rgba32)), BufferUsageFlags.TransferSrcBit, hostVisible: true);
        var into = staging.Span<Rgba32>();
        var at = 0;
        foreach (var level in chain)
        {
            level.Pixels.CopyTo(into[at..]);
            at += level.Pixels.Length;
        }

        var info = new ImageCreateInfo
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            // Unorm and not sRGB: the tint arithmetic stays in the space the art was authored against.
            Format = Format.R8G8B8A8Unorm,
            Extent = new Extent3D((uint)width, (uint)height, 1),
            MipLevels = (uint)chain.Count,
            ArrayLayers = 1,
            Samples = SampleCountFlags.Count1Bit,
            Tiling = ImageTiling.Optimal,
            Usage = ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit,
            SharingMode = SharingMode.Exclusive,
            InitialLayout = ImageLayout.Undefined,
        };

        Vk.Count();
        Vk.Check(vk.Api.CreateImage(vk.Device, &info, null, out var image), "vkCreateImage");
        var memory = Bind(vk, image);

        var regions = new BufferImageCopy[chain.Count];
        ulong offset = 0;
        for (var level = 0; level < chain.Count; level++)
        {
            regions[level] = new BufferImageCopy
            {
                BufferOffset = offset,
                ImageSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, (uint)level, 0, 1),
                ImageExtent = new Extent3D((uint)chain[level].Width, (uint)chain[level].Height, 1),
            };

            offset += (ulong)(chain[level].Pixels.Length * sizeof(Rgba32));
        }

        vk.OneShot(commands =>
        {
            Transition(vk, commands, image, chain.Count, ImageLayout.Undefined, ImageLayout.TransferDstOptimal);
            fixed (BufferImageCopy* p = regions)
            {
                Vk.Count();
                vk.Api.CmdCopyBufferToImage(commands, staging.Handle, image, ImageLayout.TransferDstOptimal, (uint)regions.Length, p);
            }

            Transition(vk, commands, image, chain.Count, ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal);
        });

        var viewInfo = new ImageViewCreateInfo
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = image,
            ViewType = ImageViewType.Type2D,
            Format = Format.R8G8B8A8Unorm,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, (uint)chain.Count, 0, 1),
        };

        Vk.Count();
        Vk.Check(vk.Api.CreateImageView(vk.Device, &viewInfo, null, out var view), "vkCreateImageView");

        var address = repeats ? SamplerAddressMode.Repeat : SamplerAddressMode.ClampToEdge;
        var samplerInfo = new SamplerCreateInfo
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = Filter.Linear,
            MinFilter = Filter.Linear,
            MipmapMode = SamplerMipmapMode.Linear,
            // A ground texture is wrap-seamless and anchored to the world origin, so repeat is the
            // whole addressing story; a sheet clamps, because its neighbour is another pose.
            AddressModeU = address,
            AddressModeV = address,
            AddressModeW = address,
            MaxLod = chain.Count,
        };

        Vk.Count();
        Vk.Check(vk.Api.CreateSampler(vk.Device, &samplerInfo, null, out var sampler), "vkCreateSampler");

        return new GpuTexture(vk, image, memory, view, sampler, width, height, chain.Count);
    }

    public void Dispose()
    {
        Vk.Count();
        _vk.Api.DestroySampler(_vk.Device, Sampler, null);
        Vk.Count();
        _vk.Api.DestroyImageView(_vk.Device, View, null);
        Vk.Count();
        _vk.Api.DestroyImage(_vk.Device, Handle, null);
        Vk.Count();
        _vk.Api.FreeMemory(_vk.Device, Memory, null);
    }

    /// <summary>Each level the box average of the one above it, down to a single texel.</summary>
    static List<(Rgba32[] Pixels, int Width, int Height)> MipChain(Rgba32[] top, int width, int height)
    {
        var chain = new List<(Rgba32[] Pixels, int Width, int Height)> { (top, width, height) };
        while (width > 1 || height > 1)
        {
            var (source, sourceWidth, sourceHeight) = chain[^1];
            var nextWidth = Math.Max(1, sourceWidth / 2);
            var nextHeight = Math.Max(1, sourceHeight / 2);
            var next = new Rgba32[nextWidth * nextHeight];

            for (var y = 0; y < nextHeight; y++)
            {
                for (var x = 0; x < nextWidth; x++)
                {
                    var x0 = Math.Min(x * 2, sourceWidth - 1);
                    var x1 = Math.Min(x * 2 + 1, sourceWidth - 1);
                    var y0 = Math.Min(y * 2, sourceHeight - 1);
                    var y1 = Math.Min(y * 2 + 1, sourceHeight - 1);
                    next[y * nextWidth + x] = Average(
                        source[y0 * sourceWidth + x0], source[y0 * sourceWidth + x1],
                        source[y1 * sourceWidth + x0], source[y1 * sourceWidth + x1]);
                }
            }

            chain.Add((next, nextWidth, nextHeight));
            width = nextWidth;
            height = nextHeight;
        }

        return chain;
    }

    static Rgba32 Average(Rgba32 a, Rgba32 b, Rgba32 c, Rgba32 d) => new(
        (byte)((a.R + b.R + c.R + d.R) / 4),
        (byte)((a.G + b.G + c.G + d.G) / 4),
        (byte)((a.B + b.B + c.B + d.B) / 4),
        (byte)((a.A + b.A + c.A + d.A) / 4));

    /// <summary>The memory under an image, allocated device-local and bound.</summary>
    static DeviceMemory Bind(Vk vk, Image image)
    {
        Vk.Count();
        vk.Api.GetImageMemoryRequirements(vk.Device, image, out var requirements);
        var allocate = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = requirements.Size,
            MemoryTypeIndex = vk.MemoryTypeIndex(requirements.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit),
        };

        Vk.Count();
        Vk.Check(vk.Api.AllocateMemory(vk.Device, &allocate, null, out var memory), "vkAllocateMemory");
        Vk.Count();
        Vk.Check(vk.Api.BindImageMemory(vk.Device, image, memory, 0), "vkBindImageMemory");
        return memory;
    }

    static Sampler Clamped(Vk vk)
    {
        var info = new SamplerCreateInfo
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = Filter.Linear,
            MinFilter = Filter.Linear,
            MipmapMode = SamplerMipmapMode.Linear,
            AddressModeU = SamplerAddressMode.ClampToEdge,
            AddressModeV = SamplerAddressMode.ClampToEdge,
            AddressModeW = SamplerAddressMode.ClampToEdge,
            MaxLod = 1,
        };

        Vk.Count();
        Vk.Check(vk.Api.CreateSampler(vk.Device, &info, null, out var sampler), "vkCreateSampler");
        return sampler;
    }

    static void Transition(
        Vk vk, CommandBuffer commands, Image image, int levels, ImageLayout from, ImageLayout to, int layer = 0)
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
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, (uint)levels, (uint)layer, 1),
        };

        var dependency = new DependencyInfo
        {
            SType = StructureType.DependencyInfo,
            ImageMemoryBarrierCount = 1,
            PImageMemoryBarriers = &barrier,
        };

        Vk.Count();
        vk.Api.CmdPipelineBarrier2(commands, &dependency);
    }
}
