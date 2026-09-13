using System.Numerics;

namespace TrafficSimulation.App.Debug;

/// <summary>
/// <b>The one cell of the geometry grid a reader has picked out</b> (OBS-2t), held as the place they
/// clicked. A left click on the town picks, a right click puts it back.
/// </summary>
/// <remarks>
/// <para>
/// <b>A place and not a cell number.</b> Which cell a place is in is the index's own answer — it snaps its
/// lattice to the map and grows its cell where a set is spread too far
/// (<see cref="Core.Geometry.ChainIndex"/>) — so a pick held as a pair of cell numbers would be a pick on
/// whatever lattice happened to be current when it was made. Held as the point that was clicked it is
/// resolved against the grid being drawn, every frame.
/// </para>
/// <para>
/// <b>It is dropped when the town is</b>, on the same terms the ruler's tapes are: a place on a town that no
/// longer exists is a place in the middle of a field. It is <em>not</em> dropped when the layer is
/// unticked — unlike a tape, which is a measurement somebody took, this is a place somebody is looking
/// at, and a layer turned off and on again is the same reader still looking at it.
/// </para>
/// </remarks>
internal sealed class DebugPick
{
    /// <summary>Where the reader clicked, or nothing.</summary>
    public Vector2? AtM { get; private set; }

    public void Click(Vector2 pointM) => AtM = pointM;

    public void Clear() => AtM = null;

    public void TownChanged() => Clear();
}
