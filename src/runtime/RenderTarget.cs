using Silk.NET.Vulkan;
using Image = Silk.NET.Vulkan.Image;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace TrafficSimulation.Runtime;

/// <summary>
/// The images a frame is drawn into: a window's swapchain, or one image nobody is looking at.
/// </summary>
/// <remarks>
/// The renderer knows the difference in exactly three places — the three members below the geometry.
/// Pipeline, descriptor sets, recording and draw are written once and produce the same picture either
/// way, which is what makes an offscreen shot worth comparing against a window's. A target owns its
/// own acquire and present so the offscreen path pays for no call it has no use for: a windowed frame
/// is five managed→native crossings and an offscreen one three, neither taking the town's size as an
/// argument.
/// </remarks>
internal abstract class RenderTarget : IDisposable
{
    public abstract Format Format { get; }

    public abstract Extent2D Extent { get; }

    public abstract Image[] Images { get; }

    public abstract ImageView[] Views { get; }

    public int ImageCount => Images.Length;

    /// <summary>The layout the recorded frame leaves an image in: ready to be shown, or read back.</summary>
    public abstract ImageLayout FinalLayout { get; }

    /// <summary>Whether <see cref="Acquire"/> signals the semaphore the submit must wait on.</summary>
    public abstract bool AcquireSignals { get; }

    /// <summary>
    /// Which image the next frame draws into. <c>false</c> means the target is out of date and has to
    /// be rebuilt before anything is drawn — which only a window can be.
    /// </summary>
    public abstract bool Acquire(Semaphore acquired, out uint image);

    /// <summary>Hands the finished image on. <c>false</c> means the target has to be rebuilt.</summary>
    public abstract bool Present(Semaphore rendered, uint image);

    public abstract void Dispose();
}
