using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>Everything <see cref="GroundShapes.At(Vector2, byte)"/> reads, as data</b> — for a device that answers the same
/// question off its own copy, in the same order and with the same arithmetic. One geometry still (TER-7): these are the
/// shapes the answer is solved against, handed over, and never a second drawing of them.
/// </summary>
internal readonly ref struct GroundTable
{
    public required ChainIndex.ChainTable RoadIndex { get; init; }
    public required GridWindow RoadWindow { get; init; }
    public required float FarthestReachM { get; init; }
    public required ReadOnlySpan<float> RoadHalfM { get; init; }
    public required ReadOnlySpan<float> RoadLengthM { get; init; }
    public required ReadOnlySpan<float> RoadReachM { get; init; }
    public required ReadOnlySpan<byte> RoadLevel { get; init; }
    public required ReadOnlySpan<bool> RoadIsABay { get; init; }
    public required ReadOnlySpan<int> DeckAt { get; init; }
    public required ReadOnlySpan<float> DeckFromM { get; init; }
    public required ReadOnlySpan<float> DeckToM { get; init; }
    public required ReadOnlySpan<float> DeckHalfM { get; init; }
    public required ReadOnlySpan<int> PaintAt { get; init; }
    public required ReadOnlySpan<float> PaintFromM { get; init; }
    public required ReadOnlySpan<float> PaintToM { get; init; }
    public required ReadOnlySpan<Vector2> SlabMinM { get; init; }
    public required ReadOnlySpan<Vector2> SlabSizeM { get; init; }
    public required RingSides Carriageway { get; init; }
    public required RingSides Walk { get; init; }
    public required RingSides Above { get; init; }
    public required GroundShapes.RingTable Water { get; init; }
    public required GroundShapes.RingTable Shore { get; init; }
}

internal sealed partial class GroundShapes
{
    /// <summary>One set of rings as data: the points, each ring's box, and each long ring's edges by cell or none.</summary>
    internal readonly record struct RingTable(CityPlan.RingArrays Rings, Vector2[] LeastM, Vector2[] MostM, EdgeCells?[] Cells);

    /// <inheritdoc cref="GroundTable"/>
    public GroundTable Table
    {
        get
        {
            var roads = _pieces.Roads;
            var level = new byte[_roadHalfM.Length];
            var bay = new bool[_roadHalfM.Length];
            for (var road = 0; road < level.Length; road++)
            {
                level[road] = roads.LevelOf(road);
                bay[road] = roads.IsABay(road);
            }

            return new GroundTable
            {
                RoadIndex = _roadIndex.Table,
                RoadWindow = _roadIndex.Window,
                FarthestReachM = _farthestReachM,
                RoadHalfM = _roadHalfM,
                RoadLengthM = _roadLengthM,
                RoadReachM = _roadReachM,
                RoadLevel = level,
                RoadIsABay = bay,
                DeckAt = _deckAt,
                DeckFromM = _deckFromM,
                DeckToM = _deckToM,
                DeckHalfM = _deckHalfM,
                PaintAt = _paintAt,
                PaintFromM = _paintFromM,
                PaintToM = _paintToM,
                SlabMinM = _slabMinM,
                SlabSizeM = _slabSizeM,
                Carriageway = _carriageway,
                Walk = _walk,
                Above = _above,
                Water = new RingTable(_water.Of, _water.LeastM, _water.MostM, _water.Cells),
                Shore = new RingTable(_shore.Of, _shore.LeastM, _shore.MostM, _shore.Cells),
            };
        }
    }
}
