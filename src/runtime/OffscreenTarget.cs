using Silk.NET.Vulkan;
using Image = Silk.NET.Vulkan.Image;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace TrafficSimulation.Runtime;

/// <summary>
/// One image nobody is looking at: the same frame with no window, surface or swapchain under it, and
/// what a render check is made of — a shot off a swapchain needs a display, a compositor, a window that
/// came up at the size it asked for and a frame that was actually presented, none of which the picture
/// is about.
/// </summary>
/// <remarks>
/// Exactly one image, not the swapchain's two or three: nothing is being shown, so there is nothing
/// for the next frame to race against and the renderer's per-image fence is the whole synchronisation.
/// Acquire signals nothing and present does nothing, so an offscreen frame is three crossings.
/// </remarks>
internal sealed unsafe class OffscreenTarget : RenderTarget
{
    readonly Vk _vk;
    readonly DeviceMemory _memory;

    OffscreenTarget(Vk vk, DeviceMemory memory, Extent2D extent, Image image, ImageView view)
    {
        _vk = vk;
        _memory = memory;
        Extent = extent;
        Images = [image];
        Views = [view];
    }

    /// <summary>Unorm, as the swapchain picks: the tint arithmetic has to land in the same space.</summary>
    public override Format Format => Format.R8G8B8A8Unorm;

    public override Extent2D Extent { get; }

    public override Image[] Images { get; }

    public override ImageView[] Views { get; }

    /// <summary>Read back, never shown — so the recording leaves it where <c>--shot</c> wants it.</summary>
    public override ImageLayout FinalLayout => ImageLayout.TransferSrcOptimal;

    public override bool AcquireSignals => false;

    public static OffscreenTarget Create(Vk vk, Extent2D extent)
    {
        var info = new ImageCreateInfo
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Format = Format.R8G8B8A8Unorm,
            Extent = new Extent3D(extent.Width, extent.Height, 1),
            MipLevels = 1,
            ArrayLayers = 1,
            Samples = SampleCountFlags.Count1Bit,
            Tiling = ImageTiling.Optimal,
            Usage = ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferSrcBit,
            SharingMode = SharingMode.Exclusive,
            InitialLayout = ImageLayout.Undefined,
        };

        Vk.Count();
        Vk.Check(vk.Api.CreateImage(vk.Device, &info, null, out var image), "vkCreateImage");

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

        var viewInfo = new ImageViewCreateInfo
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = image,
            ViewType = ImageViewType.Type2D,
            Format = Format.R8G8B8A8Unorm,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1),
        };

        Vk.Count();
        Vk.Check(vk.Api.CreateImageView(vk.Device, &viewInfo, null, out var view), "vkCreateImageView");

        return new OffscreenTarget(vk, memory, extent, image, view);
    }

    public override bool Acquire(Semaphore acquired, out uint image)
    {
        image = 0;
        return true;
    }

    public override bool Present(Semaphore rendered, uint image) => true;

    public override void Dispose()
    {
        Vk.Count();
        _vk.Api.DestroyImageView(_vk.Device, Views[0], null);
        Vk.Count();
        _vk.Api.DestroyImage(_vk.Device, Images[0], null);
        Vk.Count();
        _vk.Api.FreeMemory(_vk.Device, _memory, null);
    }
}
