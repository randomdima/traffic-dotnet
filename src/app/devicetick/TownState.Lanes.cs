using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>RoadGraph's lane indexes</b> (<c>world/town/kernels/town_map.glsl</c>, ids 722–742): every lane's, the carriageway's,
/// and the carriageway's a level at a time — each slot's chain and box and each cell's run of slots, as
/// <see cref="ChainIndex"/> keeps them. A chain's arcs and length are its lane's, laid with the map.
/// </summary>
internal sealed partial class TownState
{
    const int RoadsNearestChainsId = 722;
    const int RoadsNearestLeastMId = 723;
    const int RoadsNearestMostMId = 724;
    const int RoadsNearestCellStartId = 725;
    const int RoadsNearestEntrySlotId = 726;
    const int RoadsNearestStreetChainsId = 727;
    const int RoadsNearestStreetLeastMId = 728;
    const int RoadsNearestStreetMostMId = 729;
    const int RoadsNearestStreetCellStartId = 730;
    const int RoadsNearestStreetEntrySlotId = 731;
    const int RoadsNearestLevel0ChainsId = 732;
    const int RoadsNearestLevel0LeastMId = 733;
    const int RoadsNearestLevel0MostMId = 734;
    const int RoadsNearestLevel0CellStartId = 735;
    const int RoadsNearestLevel0EntrySlotId = 736;
    const int RoadsNearestLevel1ChainsId = 737;
    const int RoadsNearestLevel1LeastMId = 738;
    const int RoadsNearestLevel1MostMId = 739;
    const int RoadsNearestLevel1CellStartId = 740;
    const int RoadsNearestLevel1EntrySlotId = 741;
    const int LaneIndexStateId = 742;

    /// <summary>One index's window and how many chains it holds (<c>ChainIndexWindow</c>), field for field.</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct ChainIndexWindowRow(int FromX, int FromY, int Width, int Height, float PerCellM, float CellM, int Chains, int Pad = 0);

    /// <summary>The four indexes' windows, and whether the carriageway's are kept a level at a time (<c>LaneIndexState</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct LaneIndexStateRow(
        ChainIndexWindowRow Nearest, ChainIndexWindowRow NearestStreet, ChainIndexWindowRow Level0, ChainIndexWindowRow Level1,
        uint Levelled, uint Pad0 = 0, uint Pad1 = 0, uint Pad2 = 0);

    void LayTheLaneIndexes(TownWorld town)
    {
        var (nearest, street, onLevels) = town.Roads.LaneIndexes;
        var nearestRow = LayIndex(RoadsNearestChainsId, nearest);
        var streetRow = LayIndex(RoadsNearestStreetChainsId, street);
        var level0 = LayIndex(RoadsNearestLevel0ChainsId, onLevels.Length > 0 ? onLevels[0] : null);
        var level1 = LayIndex(RoadsNearestLevel1ChainsId, onLevels.Length > 1 ? onLevels[1] : null);
        LayOne(LaneIndexStateId, new LaneIndexStateRow(nearestRow, streetRow, level0, level1, onLevels.Length > 0 ? 1u : 0u));
    }

    /// <summary>One index's five tables from its first id, in <c>town_map.glsl</c>'s order — or empty ones for none.</summary>
    ChainIndexWindowRow LayIndex(int firstId, ChainIndex? index)
    {
        if (index is null)
        {
            Lay<int>(firstId, []);
            Lay<Vector2>(firstId + 1, []);
            Lay<Vector2>(firstId + 2, []);
            Lay<int>(firstId + 3, [0]);
            Lay<int>(firstId + 4, []);
            return default;
        }

        var kept = index.Kept;
        Lay<int>(firstId, kept.ChainId);
        Lay<Vector2>(firstId + 1, kept.SlotLeastM);
        Lay<Vector2>(firstId + 2, kept.SlotMostM);
        Lay<int>(firstId + 3, kept.CellStart);
        Lay<int>(firstId + 4, kept.EntrySlot);

        var window = index.Window;
        return new ChainIndexWindowRow(
            window.FromX, window.FromY, window.Width, window.Height, window.Level.PerCellM, window.Level.CellM, index.ChainCount);
    }
}
