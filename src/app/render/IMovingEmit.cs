using TrafficSimulation.Runtime;

namespace TrafficSimulation.App.Render;

/// <summary>
/// <b>What moves, emitted on the device for the frame that draws it</b> (<c>TownRenderer.DrawTheMovingFrom</c>): recorded
/// once a slot ahead of the pass that draws its two runs, and steered a frame at a time only through memory its own
/// recording reads.
/// </summary>
/// <remarks>
/// An interface because the renderer draws it and the device tick owns it (<c>app/devicetick</c>'s <c>DeviceSprites</c>).
/// On the desktop it is recorded into each image's frame; a page runs it in a batch of its own ahead of its frame
/// (<c>Game.Web.cs</c>), as it does the engine layers (<see cref="IEngineDraw"/>).
/// </remarks>
internal interface IMovingEmit
{
    /// <summary>The over run from the first instance and the run above from <see cref="AboveFrom"/>, as the emit writes them.</summary>
    GpuBuffer Instances { get; }

    /// <summary>The two runs' draws, the over run's then the run above's, sixteen bytes apiece.</summary>
    GpuBuffer Draws { get; }

    int AboveFrom { get; }

    /// <summary>
    /// The emit for slot <paramref name="slot"/>'s frame, recorded ahead of the pass that draws it; the caller makes its
    /// writes visible to the draws that read them.
    /// </summary>
    void Record(Recording commands, int slot);
}
