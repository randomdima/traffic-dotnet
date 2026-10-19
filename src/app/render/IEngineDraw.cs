using TrafficSimulation.Runtime;

namespace TrafficSimulation.App.Render;

/// <summary>
/// <b>The engine's own tables drawn as overlay quads on the device</b> (<c>app/debug</c>'s <c>EngineLayers</c>): three runs
/// of <see cref="RunRoom"/> quads in <see cref="Quads"/> — the ground's, the level above's, and the run over the bodies —
/// each drawn by the overlay pipeline as many as its draw in <see cref="Draws"/> says.
/// </summary>
/// <remarks>
/// An interface because the renderer records the draws and the debug slice owns what writes them. On the desktop the kernels
/// are recorded into each image's frame ahead of its pass (<see cref="Record"/>), so the frame's crossings stay what they
/// are with every layer on; a page runs them in a batch of their own ahead of its frame.
/// </remarks>
internal interface IEngineDraw
{
    GpuBuffer Quads { get; }

    /// <summary>Three draws, a run each, sixteen bytes apiece (four vertices, the run's count, nought, nought).</summary>
    GpuBuffer Draws { get; }

    int RunRoom { get; }

    /// <summary>The kernels for slot <paramref name="slot"/>'s frame, recorded ahead of the pass that draws the runs.</summary>
    void Record(Recording commands, int slot);
}

/// <summary>Where the runs stand in <see cref="IEngineDraw.Quads"/>, in the order the frame draws them.</summary>
internal enum EngineRun
{
    Ground,
    Above,
    Over,
}
