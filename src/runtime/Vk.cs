
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace TrafficSimulation.Runtime;

/// <summary>
/// The one wall between managed code and Vulkan. Every entry point reachable from the frame path
/// goes through here and nowhere else, so <see cref="Crossings"/> is the whole count: a call made
/// around the wall is the defect, not the number it produces.
/// </summary>
/// <remarks>
/// The frame's crossing count is O(1) in the size of the town — five, on a town of twelve cars and on
/// one of five hundred. Setup is not counted against that and is not held down: it happens once.
/// Memory is allocated per resource rather than out of a sub-allocator, because this engine makes
/// about ten allocations in its life and all are large and long-lived.
/// </remarks>
internal sealed unsafe partial class Vk : IDisposable
{
    const string ValidationLayer = "VK_LAYER_KHRONOS_validation";

    readonly Silk.NET.Vulkan.Vk _api;
    Instance _instance;
    Device _device;
    CommandPool _pool;
    KhrSurface? _surfaceApi;
    SurfaceKHR _surface;

    Vk(Silk.NET.Vulkan.Vk api, Instance instance, PhysicalDevice physical, string deviceName, uint deviceApiVersion)
    {
        _api = api;
        _instance = instance;
        Physical = physical;
        DeviceName = deviceName;
        DeviceApiVersion = deviceApiVersion;
    }

    /// <summary>Crossings of this wall since the process started. DEBUG only, and free in a measured run.</summary>
    public static long Crossings { get; private set; }

    public string DeviceName { get; }

    public uint DeviceApiVersion { get; }

    public string DeviceApiVersionText =>
        $"{DeviceApiVersion >> 22}.{(DeviceApiVersion >> 12) & 0x3FF}.{DeviceApiVersion & 0xFFF}";

    public Silk.NET.Vulkan.Vk Api => _api;

    public Instance Instance => _instance;

    public PhysicalDevice Physical { get; }

    public Device Device => _device;

    public Queue Queue { get; private set; }

    /// <summary>One family for graphics and present both, which every desktop driver offers.</summary>
    public uint QueueFamily { get; private set; }

    public KhrSurface SurfaceApi => _surfaceApi ?? throw new InvalidOperationException("Opened without a window, so there is no surface.");

    public SurfaceKHR Surface => _surface;

    public KhrSwapchain SwapchainApi { get; private set; } = null!;

    /// <summary>
    /// How a finished frame reaches the glass. Set once before the first swapchain and read by every
    /// one built after — a resize rebuilds the images and must not change the pacing under them.
    /// </summary>
    /// <remarks>
    /// It is what a frame rate from this build means: under FIFO the loop is paced by the display and
    /// the read-out is the refresh rate whatever the town costs — 120 fps on Test and on Odesa alike,
    /// two towns fifty times apart. What the build costs under it is the read-out's cpu figure, since
    /// the wait is measured apart from it (<c>FrameParts.BlockedMs</c>).
    /// <see cref="Swapchain"/> carries the argument for each mode.
    /// </remarks>
    public PresentModeKHR WantedPresentMode { get; set; } = PresentModeKHR.FifoKhr;

    /// <summary>
    /// Opens the loader, creates an instance, picks a physical device preferring a discrete GPU, and
    /// creates the logical device and its one queue. With a window's surface it also picks a queue
    /// family that can present to it and loads the swapchain extension; without one it stops at a
    /// device that can draw, which is what the dependency read-out asks for.
    /// </summary>
    /// <param name="validation">The Khronos validation layer. Workshop only — it costs most of the
    /// frame rate, so a measured run never has it on.</param>
    public static Vk Open(string appName, bool validation, IVkSurface? windowSurface = null)
    {
        var api = Silk.NET.Vulkan.Vk.GetApi();

        var enabledLayers = validation && HasLayer(api, ValidationLayer)
            ? new[] { ValidationLayer }
            : Array.Empty<string>();

        var appNamePtr = (byte*)SilkMarshal.StringToPtr(appName);
        var engineNamePtr = (byte*)SilkMarshal.StringToPtr("traffic-dotnet");
        var layersPtr = enabledLayers.Length == 0 ? 0 : SilkMarshal.StringArrayToPtr(enabledLayers);
        try
        {
            var appInfo = new ApplicationInfo
            {
                SType = StructureType.ApplicationInfo,
                PApplicationName = appNamePtr,
                ApplicationVersion = Silk.NET.Vulkan.Vk.MakeVersion(0, 1, 0),
                PEngineName = engineNamePtr,
                EngineVersion = Silk.NET.Vulkan.Vk.MakeVersion(0, 1, 0),
                // 1.3 core carries dynamic rendering, synchronization2, timeline semaphores, buffer
                // device address and descriptor indexing — what keeps the renderer small enough to read.
                ApiVersion = Silk.NET.Vulkan.Vk.Version13,
            };

            var extensions = windowSurface is null
                ? (byte**)null
                : windowSurface.GetRequiredExtensions(out var extensionCount);
            var createInfo = new InstanceCreateInfo
            {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = &appInfo,
                EnabledLayerCount = (uint)enabledLayers.Length,
                PpEnabledLayerNames = (byte**)layersPtr,
                EnabledExtensionCount = 0,
                PpEnabledExtensionNames = extensions,
            };

            if (windowSurface is not null)
            {
                windowSurface.GetRequiredExtensions(out var count);
                createInfo.EnabledExtensionCount = count;
            }

            Count();
            Check(api.CreateInstance(&createInfo, null, out var instance), "vkCreateInstance");

            var surface = default(SurfaceKHR);
            KhrSurface? surfaceApi = null;
            if (windowSurface is not null)
            {
                if (!api.TryGetInstanceExtension(instance, out surfaceApi)) throw new InvalidOperationException("VK_KHR_surface is not present.");

                Count();
                surface = windowSurface.Create<AllocationCallbacks>(instance.ToHandle(), null).ToSurface();
            }

            var (physical, name, apiVersion, family) = PickPhysicalDevice(api, instance, surfaceApi, surface);
            if (physical.Handle == 0)
            {
                Count();
                api.DestroyInstance(instance, null);
                throw new InvalidOperationException("No Vulkan physical device: the loader answered, no driver did.");
            }

            var vk = new Vk(api, instance, physical, name, apiVersion) { QueueFamily = family, _surfaceApi = surfaceApi, _surface = surface };
            vk.CreateDevice(windowSurface is not null);
            return vk;
        }
        finally
        {
            SilkMarshal.Free((nint)appNamePtr);
            SilkMarshal.Free((nint)engineNamePtr);
            if (layersPtr != 0) SilkMarshal.Free(layersPtr);
        }
    }

    public void Dispose()
    {
        if (_device.Handle != 0)
        {
            Count();
            _api.DeviceWaitIdle(_device);
            if (_pool.Handle != 0)
            {
                Count();
                _api.DestroyCommandPool(_device, _pool, null);
            }

            Count();
            _api.DestroyDevice(_device, null);
            _device = default;
        }

        if (_surface.Handle != 0 && _surfaceApi is not null)
        {
            Count();
            _surfaceApi.DestroySurface(_instance, _surface, null);
            _surface = default;
        }

        if (_instance.Handle == 0) return;
        Count();
        _api.DestroyInstance(_instance, null);
        _instance = default;
        _api.Dispose();
    }
    /// <summary>A buffer and the memory under it. Host-visible memory is mapped once and never unmapped.</summary>
    public GpuBuffer CreateBuffer(ulong sizeBytes, BufferUsageFlags usage, bool hostVisible)
    {
        var info = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = sizeBytes,
            Usage = usage,
            SharingMode = SharingMode.Exclusive,
        };

        Count();
        Check(_api.CreateBuffer(_device, &info, null, out var buffer), "vkCreateBuffer");

        Count();
        _api.GetBufferMemoryRequirements(_device, buffer, out var requirements);

        var wanted = hostVisible
            ? MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit
            : MemoryPropertyFlags.DeviceLocalBit;
        var allocation = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = requirements.Size,
            MemoryTypeIndex = MemoryTypeIndex(requirements.MemoryTypeBits, wanted),
        };

        Count();
        Check(_api.AllocateMemory(_device, &allocation, null, out var memory), "vkAllocateMemory");

        Count();
        Check(_api.BindBufferMemory(_device, buffer, memory, 0), "vkBindBufferMemory");

        void* mapped = null;
        if (hostVisible)
        {
            Count();
            Check(_api.MapMemory(_device, memory, 0, requirements.Size, 0, &mapped), "vkMapMemory");
        }

        return new GpuBuffer(this, buffer, memory, sizeBytes, mapped);
    }

    public void DestroyBuffer(Buffer buffer, DeviceMemory memory)
    {
        Count();
        _api.DestroyBuffer(_device, buffer, null);
        Count();
        _api.FreeMemory(_device, memory, null);
    }

    /// <summary>
    /// One command buffer, submitted and waited on — how everything that happens once gets done:
    /// an upload, a layout transition, a mip chain, a screenshot's copy.
    /// </summary>
    public void OneShot(Action<CommandBuffer> record)
    {
        var allocate = new CommandBufferAllocateInfo
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = Pool(),
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1,
        };

        Count();
        Check(_api.AllocateCommandBuffers(_device, &allocate, out var commands), "vkAllocateCommandBuffers");

        var begin = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
        };

        Count();
        Check(_api.BeginCommandBuffer(commands, &begin), "vkBeginCommandBuffer");
        record(commands);
        Count();
        Check(_api.EndCommandBuffer(commands), "vkEndCommandBuffer");

        var submit = new SubmitInfo
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &commands,
        };

        Count();
        Check(_api.QueueSubmit(Queue, 1, &submit, default), "vkQueueSubmit");
        Count();
        Check(_api.QueueWaitIdle(Queue), "vkQueueWaitIdle");
        Count();
        _api.FreeCommandBuffers(_device, Pool(), 1, &commands);
    }

    public CommandBuffer[] AllocateCommandBuffers(int count)
    {
        var allocate = new CommandBufferAllocateInfo
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = Pool(),
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = (uint)count,
        };

        var buffers = new CommandBuffer[count];
        fixed (CommandBuffer* p = buffers)
        {
            Count();
            Check(_api.AllocateCommandBuffers(_device, &allocate, p), "vkAllocateCommandBuffers");
        }

        return buffers;
    }

    /// <summary>A shader, as SPIR-V embedded in this assembly by the project file's own glslc step.</summary>
    public ShaderModule LoadShader(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Shaders/{name}.spv")
                           ?? throw new FileNotFoundException($"Shaders/{name}.spv is not embedded: did the CompileShaders target run?");
        var code = new byte[stream.Length];
        stream.ReadExactly(code);

        fixed (byte* p = code)
        {
            var info = new ShaderModuleCreateInfo
            {
                SType = StructureType.ShaderModuleCreateInfo,
                CodeSize = (nuint)code.Length,
                PCode = (uint*)p,
            };

            Count();
            Check(_api.CreateShaderModule(_device, &info, null, out var module), "vkCreateShaderModule");
            return module;
        }
    }

    public uint MemoryTypeIndex(uint allowed, MemoryPropertyFlags wanted)
    {
        Count();
        _api.GetPhysicalDeviceMemoryProperties(Physical, out var properties);
        for (var type = 0; type < properties.MemoryTypeCount; type++)
        {
            var usable = (allowed & (1u << type)) != 0;
            if (usable && (properties.MemoryTypes[type].PropertyFlags & wanted) == wanted) return (uint)type;
        }

        throw new InvalidOperationException($"No memory type is both usable by this resource and {wanted}.");
    }

    public CommandPool Pool()
    {
        if (_pool.Handle != 0) return _pool;

        var info = new CommandPoolCreateInfo
        {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = QueueFamily,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
        };

        Count();
        Check(_api.CreateCommandPool(_device, &info, null, out _pool), "vkCreateCommandPool");
        return _pool;
    }

    public static void Check(Result result, string call)
    {
        if (result != Result.Success) throw new InvalidOperationException($"{call} returned {result}");
    }

    [Conditional("DEBUG")]
    public static void Count() => Crossings++;

    void CreateDevice(bool withSwapchain)
    {
        var priority = 1f;
        var queue = new DeviceQueueCreateInfo
        {
            SType = StructureType.DeviceQueueCreateInfo,
            QueueFamilyIndex = QueueFamily,
            QueueCount = 1,
            PQueuePriorities = &priority,
        };

        // Dynamic rendering removes render passes and framebuffers outright; descriptor indexing
        // removes nearly all descriptor plumbing — one set bound once instead of a set per material.
        var thirteen = new PhysicalDeviceVulkan13Features
        {
            SType = StructureType.PhysicalDeviceVulkan13Features,
            DynamicRendering = true,
            Synchronization2 = true,
        };

        var twelve = new PhysicalDeviceVulkan12Features
        {
            SType = StructureType.PhysicalDeviceVulkan12Features,
            PNext = &thirteen,
            DescriptorIndexing = true,
            ShaderSampledImageArrayNonUniformIndexing = true,
            RuntimeDescriptorArray = true,
        };

        var features = new PhysicalDeviceFeatures2
        {
            SType = StructureType.PhysicalDeviceFeatures2,
            PNext = &twelve,
        };

        var extensionsPtr = withSwapchain ? SilkMarshal.StringArrayToPtr(new[] { KhrSwapchain.ExtensionName }) : 0;
        try
        {
            var info = new DeviceCreateInfo
            {
                SType = StructureType.DeviceCreateInfo,
                PNext = &features,
                QueueCreateInfoCount = 1,
                PQueueCreateInfos = &queue,
                EnabledExtensionCount = withSwapchain ? 1u : 0u,
                PpEnabledExtensionNames = (byte**)extensionsPtr,
            };

            Count();
            Check(_api.CreateDevice(Physical, &info, null, out _device), "vkCreateDevice");
        }
        finally
        {
            if (extensionsPtr != 0) SilkMarshal.Free(extensionsPtr);
        }

        Count();
        _api.GetDeviceQueue(_device, QueueFamily, 0, out var handle);
        Queue = handle;

        if (!withSwapchain) return;
        if (!_api.TryGetDeviceExtension(_instance, _device, out KhrSwapchain swapchain)) throw new InvalidOperationException("VK_KHR_swapchain is not present.");

        SwapchainApi = swapchain;
    }

    static (PhysicalDevice Device, string Name, uint ApiVersion, uint QueueFamily) PickPhysicalDevice(
        Silk.NET.Vulkan.Vk api, Instance instance, KhrSurface? surfaceApi, SurfaceKHR surface)
    {
        uint count = 0;
        Count();
        Check(api.EnumeratePhysicalDevices(instance, ref count, null), "vkEnumeratePhysicalDevices");
        if (count == 0) return (default, string.Empty, 0, 0);

        var devices = new PhysicalDevice[count];
        fixed (PhysicalDevice* p = devices)
        {
            Count();
            Check(api.EnumeratePhysicalDevices(instance, ref count, p), "vkEnumeratePhysicalDevices");
        }

        PhysicalDevice best = default;
        string bestName = string.Empty;
        uint bestApi = 0;
        uint bestFamily = 0;
        var bestIsDiscrete = false;

        foreach (var device in devices)
        {
            var family = PickQueueFamily(api, device, surfaceApi, surface);
            if (family is null) continue;

            PhysicalDeviceProperties props;
            Count();
            api.GetPhysicalDeviceProperties(device, &props);
            var isDiscrete = props.DeviceType == PhysicalDeviceType.DiscreteGpu;
            if (best.Handle != 0 && !(isDiscrete && !bestIsDiscrete)) continue;

            best = device;
            bestName = Marshal.PtrToStringUTF8((nint)props.DeviceName) ?? "unnamed";
            bestApi = props.ApiVersion;
            bestFamily = family.Value;
            bestIsDiscrete = isDiscrete;
        }

        return (best, bestName, bestApi, bestFamily);
    }

    /// <summary>One family that can draw and, where there is a surface, present to it.</summary>
    static uint? PickQueueFamily(Silk.NET.Vulkan.Vk api, PhysicalDevice device, KhrSurface? surfaceApi, SurfaceKHR surface)
    {
        uint count = 0;
        Count();
        api.GetPhysicalDeviceQueueFamilyProperties(device, ref count, null);
        var families = new QueueFamilyProperties[count];
        fixed (QueueFamilyProperties* p = families)
        {
            Count();
            api.GetPhysicalDeviceQueueFamilyProperties(device, ref count, p);
        }

        for (var family = 0u; family < count; family++)
        {
            if ((families[family].QueueFlags & QueueFlags.GraphicsBit) == 0) continue;
            if (surfaceApi is null) return family;

            Count();
            surfaceApi.GetPhysicalDeviceSurfaceSupport(device, family, surface, out var supported);
            if (supported) return family;
        }

        return null;
    }

    static bool HasLayer(Silk.NET.Vulkan.Vk api, string name)
    {
        uint count = 0;
        Count();
        api.EnumerateInstanceLayerProperties(ref count, null);
        if (count == 0) return false;

        var layers = new LayerProperties[count];
        fixed (LayerProperties* p = layers)
        {
            Count();
            api.EnumerateInstanceLayerProperties(ref count, p);
        }

        foreach (var layer in layers)
        {
            if (Marshal.PtrToStringUTF8((nint)layer.LayerName) == name) return true;
        }

        return false;
    }
}
