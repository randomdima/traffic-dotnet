using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

internal sealed partial class GroundShapes
{
    /// <summary>The roads with no walk beside them, under their plan numbers — none in a town that walks every road.</summary>
    ChainIndex? _unwalked;

    float[] _unwalkedHalfM = [];
    float _widestUnwalkedHalfM;
    float _vergeM;

    /// <summary>
    /// <b>Whether a disc stands in the verge of a road with no walk</b> (GEN-75): within
    /// <see cref="CityGenFigures.UnwalkedVergeM"/> of its carriageway, where nothing is built and nothing planted.
    /// </summary>
    /// <remarks>
    /// <b>Off the road's own line and half, not off the boundary</b>: the boundary does not say which road a stretch of
    /// kerb is, and a verge is one road's. Measured to the nearest such road alone, so where two of them meet the
    /// narrower may answer for the wider by the difference of their halves.
    /// </remarks>
    public bool InAVerge(Scan scan, Vector2 pointM, float reachM)
    {
        if (_unwalked is null) return false;

        var road = _unwalked.Nearest(
            scan.Unwalked ??= _unwalked.NewScan(), pointM, _widestUnwalkedHalfM + _vergeM + reachM, out _, out var offM);
        return road >= 0 && offM <= _unwalkedHalfM[road] + _vergeM + reachM;
    }

    void LayTheVerges(GroundPieces pieces, SimConfig config)
    {
        var roads = pieces.Roads;
        if (roads.Unwalked.Length == 0) return;

        _vergeM = config.CityGen.UnwalkedVergeM;
        _unwalkedHalfM = new float[roads.Count];
        var index = new ChainIndex.Builder();
        for (var road = 0; road < roads.Count; road++)
        {
            if (roads.IsWalked(road)) continue;

            var arcs = roads.SegmentsOf(road);
            _unwalkedHalfM[road] = roads.WidthM[road] * 0.5f;
            _widestUnwalkedHalfM = MathF.Max(_widestUnwalkedHalfM, _unwalkedHalfM[road]);
            index.Add(road, arcs, Spline.TotalLengthM(arcs));
        }

        _unwalked = index.Seal(config.Grid.Main);
    }
}
