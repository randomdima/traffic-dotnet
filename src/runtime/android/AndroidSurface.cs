using Silk.NET.Core;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace TrafficSimulation.Runtime;

/// <summary>
/// A Vulkan surface made of the activity's own glass: the one place the graphics API and the handset
/// meet, and the handset's answer to what <c>IWindow.VkSurface</c> hands the desktop.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is handed a window and never a Java object.</b> Turning the activity's <c>Surface</c> into the
/// <c>ANativeWindow*</c> below needs a JNI environment and a <c>jobject</c>, which is the one thing only
/// the head above can do (<c>TownActivity</c>); what arrives here is a pointer, so this file — and the
/// whole of <c>runtime/</c> — knows nothing about Android.
/// </para>
/// <para>
/// The window outlives the surface: releasing it is the activity's, which acquired it.
/// </para>
/// </remarks>
internal sealed unsafe class AndroidSurface(nint nativeWindow) : IVkSurface
{
    /// <summary>
    /// The two extension names, as one allocation kept for the life of the process. <b>The question is
    /// asked twice</b> — <see cref="Vk.Open"/> reads the pointer and then the count — and answering it
    /// with a fresh array the second time would hand a create info that has already read the first one a
    /// pointer to different memory.
    /// </summary>
    static nint _extensions;

    public byte** GetRequiredExtensions(out uint count)
    {
        if (_extensions == 0)
            _extensions = SilkMarshal.StringArrayToPtr([KhrSurface.ExtensionName, KhrAndroidSurface.ExtensionName]);

        count = 2;
        return (byte**)_extensions;
    }

    public VkNonDispatchableHandle Create<TAllocator>(VkHandle instance, TAllocator* allocator)
        where TAllocator : unmanaged
    {
        var api = Silk.NET.Vulkan.Vk.GetApi();
        var handle = new Instance(instance.Handle);
        if (!api.TryGetInstanceExtension(handle, out KhrAndroidSurface android))
            throw new InvalidOperationException(
                "VK_KHR_android_surface is not present: the loader answered and this driver cannot present to a window.");

        var info = new AndroidSurfaceCreateInfoKHR
        {
            SType = StructureType.AndroidSurfaceCreateInfoKhr,
            Window = (nint*)nativeWindow,
        };

        var result = android.CreateAndroidSurface(handle, &info, (AllocationCallbacks*)allocator, out var surface);
        if (result != Result.Success)
            throw new InvalidOperationException($"vkCreateAndroidSurfaceKHR: {result}");

        return new VkNonDispatchableHandle(surface.Handle);
    }
}
