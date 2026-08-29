using Silk.NET.Vulkan;
using Image = Silk.NET.Vulkan.Image;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace TrafficSimulation.Runtime;

/// <summary>
/// The images the window is drawn into, and the one place recording happens again: a resize destroys
/// these and rebuilds them, and everything the frame path holds that depends on them is torn down in
/// the same breath rather than spread across the renderer.
/// </summary>
internal sealed unsafe class Swapchain : RenderTarget
{
    readonly Vk _vk;

    Swapchain(Vk vk, SwapchainKHR handle, Format format, Extent2D extent, Image[] images, ImageView[] views)
    {
        _vk = vk;
        Handle = handle;
        Format = format;
        Extent = extent;
        Images = images;
        Views = views;
    }

    public SwapchainKHR Handle { get; }

    public override Format Format { get; }

    public override Extent2D Extent { get; }

    public override Image[] Images { get; }

    public override ImageView[] Views { get; }

    public override ImageLayout FinalLayout => ImageLayout.PresentSrcKhr;

    public override bool AcquireSignals => true;

    /// <summary>
    /// An unorm surface deliberately: it keeps the tint arithmetic in the space the art was authored
    /// against. Pacing is <see cref="Vk.WantedPresentMode"/>'s, falling back to FIFO — the one mode
    /// every driver has.
    /// </summary>
    /// <remarks>
    /// FIFO by default: a run is looked at rather than raced, and a loop that draws frames the display
    /// never shows spends a whole core to no end. What it costs the read-out is the frame figure, which
    /// under FIFO is the refresh rate whatever the town costs (120 fps on Test and 120 on Odesa, two
    /// towns fifty times apart) — the cpu figure is the one that answers for this build, since the wait
    /// on the display is counted apart from it. <c>--present mailbox</c> is for the frame figure itself:
    /// it paces and tears the same but does not block the thread that filled the buffer, at the price of
    /// frames nobody sees.
    /// </remarks>
    public static Swapchain Create(Vk vk, Extent2D wanted)
    {
        Vk.Count();
        Vk.Check(vk.SurfaceApi.GetPhysicalDeviceSurfaceCapabilities(vk.Physical, vk.Surface, out var caps),
            "vkGetPhysicalDeviceSurfaceCapabilitiesKHR");

        var extent = caps.CurrentExtent.Width != uint.MaxValue
            ? caps.CurrentExtent
            : new Extent2D(
                Math.Clamp(wanted.Width, caps.MinImageExtent.Width, caps.MaxImageExtent.Width),
                Math.Clamp(wanted.Height, caps.MinImageExtent.Height, caps.MaxImageExtent.Height));

        var mode = PickPresentMode(vk);

        // Mailbox needs a third image to have anywhere to put the frame it is holding back; with two it
        // degenerates to FIFO's pacing while still spinning the loop, which is the worst of both.
        var count = caps.MinImageCount + (mode == PresentModeKHR.MailboxKhr ? 2u : 1u);
        if (caps.MaxImageCount > 0 && count > caps.MaxImageCount) count = caps.MaxImageCount;

        var format = PickFormat(vk);
        var info = new SwapchainCreateInfoKHR
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = vk.Surface,
            MinImageCount = count,
            ImageFormat = format.Format,
            ImageColorSpace = format.ColorSpace,
            ImageExtent = extent,
            ImageArrayLayers = 1,
            // TransferSrc as well as colour, because --shot reads the finished frame back off it.
            ImageUsage = ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferSrcBit,
            ImageSharingMode = SharingMode.Exclusive,
            PreTransform = caps.CurrentTransform,
            CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
            PresentMode = mode,
            Clipped = true,
        };

        Vk.Count();
        Vk.Check(vk.SwapchainApi.CreateSwapchain(vk.Device, &info, null, out var handle), "vkCreateSwapchainKHR");

        uint imageCount = 0;
        Vk.Count();
        Vk.Check(vk.SwapchainApi.GetSwapchainImages(vk.Device, handle, ref imageCount, null), "vkGetSwapchainImagesKHR");
        var images = new Image[imageCount];
        fixed (Image* p = images)
        {
            Vk.Count();
            Vk.Check(vk.SwapchainApi.GetSwapchainImages(vk.Device, handle, ref imageCount, p), "vkGetSwapchainImagesKHR");
        }

        var views = new ImageView[imageCount];
        for (var image = 0; image < images.Length; image++)
        {
            var viewInfo = new ImageViewCreateInfo
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = images[image],
                ViewType = ImageViewType.Type2D,
                Format = format.Format,
                SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1),
            };

            Vk.Count();
            Vk.Check(vk.Api.CreateImageView(vk.Device, &viewInfo, null, out views[image]), "vkCreateImageView");
        }

        return new Swapchain(vk, handle, format.Format, extent, images, views);
    }

    public override bool Acquire(Semaphore acquired, out uint image)
    {
        image = 0;
        Vk.Count();
        var result = _vk.SwapchainApi.AcquireNextImage(_vk.Device, Handle, ulong.MaxValue, acquired, default, ref image);
        if (result is Result.ErrorOutOfDateKhr) return false;
        if (result is not (Result.Success or Result.SuboptimalKhr)) Vk.Check(result, "vkAcquireNextImageKHR");

        return true;
    }

    public override bool Present(Semaphore rendered, uint image)
    {
        var swapchain = Handle;
        var info = new PresentInfoKHR
        {
            SType = StructureType.PresentInfoKhr,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &rendered,
            SwapchainCount = 1,
            PSwapchains = &swapchain,
            PImageIndices = &image,
        };

        Vk.Count();
        var result = _vk.SwapchainApi.QueuePresent(_vk.Queue, &info);
        if (result is Result.ErrorOutOfDateKhr or Result.SuboptimalKhr) return false;

        Vk.Check(result, "vkQueuePresentKHR");
        return true;
    }

    public override void Dispose()
    {
        foreach (var view in Views)
        {
            Vk.Count();
            _vk.Api.DestroyImageView(_vk.Device, view, null);
        }

        Vk.Count();
        _vk.SwapchainApi.DestroySwapchain(_vk.Device, Handle, null);
    }

    /// <summary>The wanted mode where the surface offers it, and FIFO — which every surface offers — where it does not.</summary>
    static PresentModeKHR PickPresentMode(Vk vk)
    {
        var wanted = vk.WantedPresentMode;
        if (wanted == PresentModeKHR.FifoKhr) return wanted;

        uint count = 0;
        Vk.Count();
        Vk.Check(vk.SurfaceApi.GetPhysicalDeviceSurfacePresentModes(vk.Physical, vk.Surface, ref count, null),
            "vkGetPhysicalDeviceSurfacePresentModesKHR");
        var modes = new PresentModeKHR[count];
        fixed (PresentModeKHR* p = modes)
        {
            Vk.Count();
            Vk.Check(vk.SurfaceApi.GetPhysicalDeviceSurfacePresentModes(vk.Physical, vk.Surface, ref count, p),
                "vkGetPhysicalDeviceSurfacePresentModesKHR");
        }

        foreach (var mode in modes)
        {
            if (mode == wanted) return mode;
        }

        return PresentModeKHR.FifoKhr;
    }

    static SurfaceFormatKHR PickFormat(Vk vk)
    {
        uint count = 0;
        Vk.Count();
        Vk.Check(vk.SurfaceApi.GetPhysicalDeviceSurfaceFormats(vk.Physical, vk.Surface, ref count, null),
            "vkGetPhysicalDeviceSurfaceFormatsKHR");
        var formats = new SurfaceFormatKHR[count];
        fixed (SurfaceFormatKHR* p = formats)
        {
            Vk.Count();
            Vk.Check(vk.SurfaceApi.GetPhysicalDeviceSurfaceFormats(vk.Physical, vk.Surface, ref count, p),
                "vkGetPhysicalDeviceSurfaceFormatsKHR");
        }

        foreach (var format in formats)
        {
            if (format.Format is Format.B8G8R8A8Unorm or Format.R8G8B8A8Unorm &&
                format.ColorSpace == ColorSpaceKHR.SpaceSrgbNonlinearKhr)
            {
                return format;
            }
        }

        return formats[0];
    }
}
