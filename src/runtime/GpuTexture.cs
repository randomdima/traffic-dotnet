using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using Image = Silk.NET.Vulkan.Image;

// The library is named here and in nothing the browser's head compiles: this is the desktop's decoder,
// and what it hands over becomes Texel — the town's own four bytes — on the way out of Upload.
using Decoded = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>;

namespace TrafficSimulation.Runtime;

/// <summary>One layer's texels, top row first, written into memory the upload already owns.</summary>
internal delegate void LayerFill(int layer, Span<Texel> into);

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

        using var staging = vk.CreateBuffer((ulong)(width * height * sizeof(Texel)), BufferUsageFlags.TransferSrcBit, hostVisible: true);
        for (var layer = 0; layer < layers; layer++)
        {
            fill(layer, staging.Span<Texel>());
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
        using var decoded = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(path);
        return Upload(vk, decoded, repeats, mipped);
    }

    /// <summary>
    /// A ground surface's looks: its picture is square looks stacked top to bottom, and each is a layer of one array
    /// texture, mipped and repeating as a single surface is. Viewed as an array even with one look, which is what the
    /// ground stage samples.
    /// </summary>
    /// <param name="after">
    /// A second picture of looks laid after the first's in the same array (GEN-75), each look its own layer; one size with
    /// the first's, as every layer of an array is.
    /// </param>
    /// <exception cref="InvalidDataException">A picture that is not a whole number of squares tall, or not the first's width.</exception>
    public static GpuTexture LoadLooks(Vk vk, string path, string? after = null)
    {
        using var decoded = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(path);
        Squares(path, decoded.Width, decoded.Height);
        if (after is null) return Upload(vk, decoded, repeats: true, mipped: true, layers: decoded.Height / decoded.Width, arrayed: true);

        using var later = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(after);
        Squares(after, later.Width, later.Height);
        if (later.Width != decoded.Width)
        {
            throw new InvalidDataException($"{after} is {later.Width} wide and {path} {decoded.Width}: an array's layers are one size.");
        }

        var all = new Texel[decoded.Width * (decoded.Height + later.Height)];
        decoded.CopyPixelDataTo(MemoryMarshal.AsBytes(all.AsSpan(0, decoded.Width * decoded.Height)));
        later.CopyPixelDataTo(MemoryMarshal.AsBytes(all.AsSpan(decoded.Width * decoded.Height)));
        var layers = (decoded.Height + later.Height) / decoded.Width;
        return Upload(vk, all, decoded.Width, decoded.Width, repeats: true, mipped: true, layers, arrayed: true);

        static void Squares(string path, int widthPx, int heightPx)
        {
            if (heightPx % widthPx != 0)
            {
                throw new InvalidDataException($"{path} is {widthPx}x{heightPx}: a surface's looks are squares stacked top to bottom.");
            }
        }
    }

    /// <summary>
    /// An image that ships inside the assembly rather than beside it — the glyph sheet, which has no
    /// path to find.
    /// </summary>
    public static GpuTexture LoadEmbedded(Vk vk, string resource, bool repeats = false, bool mipped = false)
    {
        using var stream = typeof(GpuTexture).Assembly.GetManifestResourceStream(resource)
                           ?? throw new InvalidOperationException($"No embedded resource {resource}: did the project file include it?");
        using var decoded = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(stream);
        return Upload(vk, decoded, repeats, mipped);
    }

    /// <param name="layers">How many layers the picture is, stacked top to bottom: each its own mip chain.</param>
    /// <param name="arrayed">Viewed as an array, whatever the count of layers.</param>
    static GpuTexture Upload(Vk vk, Decoded decoded, bool repeats, bool mipped, int layers = 1, bool arrayed = false)
    {
        // The one crossing between the library's pixel and the town's. Both are four bytes in this
        // order, so it is a copy and never a conversion.
        var all = new Texel[decoded.Width * decoded.Height];
        decoded.CopyPixelDataTo(MemoryMarshal.AsBytes(all.AsSpan()));
        return Upload(vk, all, decoded.Width, decoded.Height / layers, repeats, mipped, layers, arrayed);
    }

    /// <summary>
    /// <b>An array of layers already composed as texels</b>, each mipped and repeating as a ground surface is — a bridge's
    /// art (<c>BridgeSheet</c>), whose pictures are laid out here rather than read from one file.
    /// </summary>
    /// <param name="all">Every layer, top row first, one after another.</param>
    public static GpuTexture FromLayers(Vk vk, Texel[] all, int width, int height, int layers) =>
        Upload(vk, all, width, height, repeats: true, mipped: true, layers, arrayed: true);

    /// <summary>One picture already composed as texels, mipped and repeating as a ground strip is — the track's (<c>TrackSheet</c>).</summary>
    public static GpuTexture FromTexels(Vk vk, Texel[] all, int width, int height) =>
        Upload(vk, all, width, height, repeats: true, mipped: true, layers: 1, arrayed: false);

    static GpuTexture Upload(Vk vk, Texel[] all, int width, int height, bool repeats, bool mipped, int layers, bool arrayed)
    {
        // Layer by layer, every level of each: a buffer copy names one layer and one level, in that order.
        var chains = new List<(Texel[] Pixels, int Width, int Height)>[layers];
        var totalPixels = 0;
        for (var layer = 0; layer < layers; layer++)
        {
            var top = all.AsSpan(layer * width * height, width * height).ToArray();
            chains[layer] = mipped ? MipChain.Build(top, width, height) : [(top, width, height)];
            foreach (var level in chains[layer]) totalPixels += level.Pixels.Length;
        }

        var chain = chains[0];
        using var staging = vk.CreateBuffer((ulong)(totalPixels * sizeof(Texel)), BufferUsageFlags.TransferSrcBit, hostVisible: true);
        var into = staging.Span<Texel>();
        var at = 0;
        foreach (var levels in chains)
        {
            foreach (var level in levels)
            {
                level.Pixels.CopyTo(into[at..]);
                at += level.Pixels.Length;
            }
        }

        var info = new ImageCreateInfo
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            // Unorm and not sRGB: the tint arithmetic stays in the space the art was authored against.
            Format = Format.R8G8B8A8Unorm,
            Extent = new Extent3D((uint)width, (uint)height, 1),
            MipLevels = (uint)chain.Count,
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

        var regions = new BufferImageCopy[chain.Count * layers];
        ulong offset = 0;
        for (var layer = 0; layer < layers; layer++)
        {
            for (var level = 0; level < chain.Count; level++)
            {
                var (pixels, levelWidth, levelHeight) = chains[layer][level];
                regions[(layer * chain.Count) + level] = new BufferImageCopy
                {
                    BufferOffset = offset,
                    ImageSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, (uint)level, (uint)layer, 1),
                    ImageExtent = new Extent3D((uint)levelWidth, (uint)levelHeight, 1),
                };

                offset += (ulong)(pixels.Length * sizeof(Texel));
            }
        }

        vk.OneShot(commands =>
        {
            Transition(vk, commands, image, chain.Count, ImageLayout.Undefined, ImageLayout.TransferDstOptimal, 0, layers);
            fixed (BufferImageCopy* p = regions)
            {
                Vk.Count();
                vk.Api.CmdCopyBufferToImage(commands, staging.Handle, image, ImageLayout.TransferDstOptimal, (uint)regions.Length, p);
            }

            Transition(vk, commands, image, chain.Count, ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal, 0, layers);
        });

        var viewInfo = new ImageViewCreateInfo
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = image,
            ViewType = arrayed ? ImageViewType.Type2DArray : ImageViewType.Type2D,
            Format = Format.R8G8B8A8Unorm,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, (uint)chain.Count, 0, (uint)layers),
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

        return new GpuTexture(vk, image, memory, view, sampler, width, height, chain.Count, layers);
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
        Vk vk, CommandBuffer commands, Image image, int levels, ImageLayout from, ImageLayout to, int layer = 0, int layers = 1)
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
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, (uint)levels, (uint)layer, (uint)layers),
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
