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

    /// <summary>The standoff each node a slip road leaves an intercity road at is held out to (<see cref="Reach"/>).</summary>
    readonly Dictionary<Vector2, float> _leastStandoffM = [];

    /// <summary>
    /// <b>The node standing here stands its arms out at least this far</b>: one a slip road leaves an intercity road at,
    /// whose shallow arm clears the intercity road's carriageway only that far out (<see cref="SimConfig.SlipStandoffM"/>).
    /// </summary>
    public void Reach(Vector2 atM, float standoffM)
    {
        if (!_leastStandoffM.TryGetValue(atM, out var leastM) || leastM < standoffM) _leastStandoffM[atM] = standoffM;
    }

    /// <summary>The bridgeheads nothing but the road carrying their span on meets (<see cref="Joint"/>).</summary>
    readonly HashSet<Vector2> _joints = [];

    /// <summary>
    /// <b>The node standing here is a bridgehead and no junction</b> (GEN-14a): its span and the road carrying it on are
    /// the only two roads at it, lane onto lane, so it stands its arms out only <see cref="CityGenFigures.BridgeheadStandoffM"/>.
    /// </summary>
    /// <returns>Whether it was not one already.</returns>
    public bool Joint(Vector2 atM) => _joints.Add(atM);

    /// <summary>The node standing here a junction again: its span was refused, and whatever meets it there meets it.</summary>
    public void Unjoint(Vector2 atM) => _joints.Remove(atM);

    /// <summary>Whether the node standing here is a bridgehead and no junction (<see cref="Joint"/>).</summary>
    public bool IsJoint(Vector2 atM) => _joints.Contains(atM);

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
        if (_leastStandoffM.TryGetValue(goneM, out var leastM)) Reach(keptM, leastM);
    }

    /// <summary>
    /// <b>Whether a road runs off the map here</b> (GEN-2b): a node on the map's own edge. Nothing else a generated town
    /// lays stands there, every other node being placed inside the margin.
    /// </summary>
    public bool RunsOff(Vector2 atM) => atM.X <= 0f || atM.Y <= 0f || atM.X >= extentM.X || atM.Y >= extentM.Y;

    /// <summary>
    /// <b>The standoff a node's arms end at, which is the disc it is drawn on</b> (<see cref="SimConfig.JunctionRadiusAcrossM"/>):
    /// none where its road runs off the map, whose lanes run up to the edge, and a joint's at a bridgehead (<see cref="Joint"/>).
    /// </summary>
    public float StandoffM(Vector2 atM) =>
        RunsOff(atM) ? 0f
        : _joints.Contains(atM) ? config.CityGen.BridgeheadStandoffM
        : MathF.Max(
            _widestM.TryGetValue(atM, out var widestM) ? config.JunctionRadiusAcrossM(widestM) : config.JunctionRadiusM,
            _leastStandoffM.GetValueOrDefault(atM));
}
