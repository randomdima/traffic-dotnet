using TrafficSimulation.App.Render;

namespace TrafficSimulation.App.Debug;

/// <summary>
/// <b>OBS-2v — which layers of the ground are drawn at all</b>, one switch a part of the mesh
/// (<see cref="GroundPart"/>). It is what a reader takes the carriageway off with to see what the walk
/// under it is really shaped like, and what says the layer is being paid for.
/// </summary>
/// <remarks>
/// <para>
/// <b>These start on, where every switch in <see cref="DebugSwitches"/> starts off</b> (OBS-2b), and that
/// is the difference between the two: those draw instrumentation over the town and these take the town
/// itself apart. A run nobody has asked for anything draws the whole ground.
/// </para>
/// <para>
/// <b>What is hidden is hidden from the picture and from the wireframe over it alike</b> (OBS-2o): the
/// mesh still holds every triangle, and a net drawn over ground nobody can see would be the one layer
/// arguing with the picture it is a reading of.
/// </para>
/// </remarks>
internal sealed class GroundSwitches
{
    /// <summary>The parts being drawn, as the bit per part the renderer is handed (<c>TownRenderer.ShowGround</c>).</summary>
    public uint Shown { get; private set; } = GroundParts.All;

    /// <summary>Whether the ground is the whole town, which is what a run that has touched nothing draws.</summary>
    public bool Whole => Shown == GroundParts.All;

    public bool this[GroundPart part] => (Shown & GroundParts.Bit(part)) != 0;

    /// <summary>
    /// A number that changes whenever a part does, which <see cref="DebugSwitches.Generation"/> carries:
    /// what the wireframe drew is a picture of the parts showing, so a part switched off has to lay that
    /// cache again exactly as a switch does.
    /// </summary>
    public int Generation { get; private set; }

    public void Toggle(GroundPart part) => Set(Shown ^ GroundParts.Bit(part));

    /// <summary>Every layer back, which is the way out of a page somebody has taken the town apart on.</summary>
    public void ShowWhole() => Set(GroundParts.All);

    void Set(uint parts)
    {
        if (parts == Shown) return;

        Shown = parts;
        Generation++;
    }
}
