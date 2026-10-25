using System.Numerics;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>How much ground each junction of a layout takes, settled before any road at it is drawn</b> (GEN-46, GEN-67): the
/// widest carriageway that will meet it, and whether a road runs off the map there (GEN-2b).
/// </summary>
/// <remarks>
/// <para>
/// <b>Keyed on where a node stands and not on its number.</b> A layout renumbers its nodes every time it settles,
/// prunes or rings one out, and a node is never moved (GEN-16) — so its place is the one name that survives all of
/// that, the same reason a link's draw is keyed on its two centres (<see cref="ConnectionPoints.Keyed"/>).
/// </para>
/// <para>
/// <b>Asked as the node is placed, never read back off the roads at it.</b> The disc a junction is drawn on is the
/// standoff its arms end at, and an arm is drawn when its road is offered: a disc sized off the roads that turned
/// out to meet there would move the lane ends of every road offered before the widest one.
/// </para>
/// </remarks>
internal sealed class NodeSizes(Vector2 extentM, SimConfig config)
{
    readonly Dictionary<Vector2, float> _widestM = [];

    /// <summary>The sizes of a layout none of whose junctions is wider than a street or stands on the map's edge.</summary>
    public static NodeSizes Streets(Vector2 extentM, SimConfig config) => new(extentM, config);

    /// <summary>A carriageway this wide will meet the node standing here.</summary>
    public void Widen(Vector2 atM, float carriagewayM)
    {
        if (!_widestM.TryGetValue(atM, out var widestM) || widestM < carriagewayM) _widestM[atM] = carriagewayM;
    }

    /// <summary>A node standing here has taken over one standing there (<see cref="TownLayout.SettleTheNodes"/>).</summary>
    public void Took(Vector2 keptM, Vector2 goneM)
    {
        if (_widestM.TryGetValue(goneM, out var widestM)) Widen(keptM, widestM);
    }

    /// <summary>
    /// <b>Whether a road runs off the map here</b> (GEN-2b): a node on the map's own edge. Nothing else a generated town
    /// lays stands there, every other node being placed inside the margin.
    /// </summary>
    public bool RunsOff(Vector2 atM) => atM.X <= 0f || atM.Y <= 0f || atM.X >= extentM.X || atM.Y >= extentM.Y;

    /// <summary>
    /// <b>The standoff a node's arms end at, which is the disc it is drawn on</b> (<see cref="SimConfig.JunctionRadiusAcrossM"/>):
    /// none where its road runs off the map, whose lanes run up to the edge.
    /// </summary>
    public float StandoffM(Vector2 atM) =>
        RunsOff(atM) ? 0f
        : _widestM.TryGetValue(atM, out var widestM) ? config.JunctionRadiusAcrossM(widestM)
        : config.JunctionRadiusM;
}
