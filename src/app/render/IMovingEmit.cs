using Silk.NET.Vulkan;
using TrafficSimulation.Runtime;

namespace TrafficSimulation.App.Render;

/// <summary>
/// <b>What moves, emitted on the device inside the frame that draws it</b> (<see cref="TownRenderer.DrawTheMovingFrom"/>):
/// recorded into each swapchain image's command buffer once, ahead of the pass that draws its two runs, and steered a frame
/// at a time only through memory its own recording reads.
/// </summary>
/// <remarks>
/// An interface because the renderer records it and the device tick owns it (<c>app/devicetick</c>'s <c>DeviceSprites</c>);
/// it is called at recording and never in a frame.
/// </remarks>
internal interface IMovingEmit
{
    /// <summary>The over run from the first instance and the run above from <see cref="AboveFrom"/>, as the emit writes them.</summary>
    GpuBuffer Instances { get; }

    /// <summary>The two runs' draws, the over run's then the run above's.</summary>
    GpuBuffer Draws { get; }

    int AboveFrom { get; }

    /// <summary>
    /// The emit for image <paramref name="image"/>'s frame, recorded into its command buffer ahead of the pass, with its
    /// writes made visible to the draws that read them.
    /// </summary>
    void Record(CommandBuffer commands, int image);
}
