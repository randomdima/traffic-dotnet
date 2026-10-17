using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>The painted bands of a set of crossings, asked which of them a mark would stand on</b> — any band, not only its own
/// road's: two roads drawn over each other share one band (WLK-10b), filed under one of them. Build-time only.
/// </summary>
/// <remarks>
/// <b>Filed by the cell a band's middle stands in</b>, a cell as wide as the widest band and the longest mark asked about
/// together, so a mark is weighed against the bands of its own cell and the eight round it.
/// </remarks>
internal sealed class PaintedBands
{
    readonly Crossings _zebras;
    readonly Dictionary<(int X, int Y), List<int>> _filed = [];
    readonly float _cellM;

    /// <param name="markM">The longest mark that will be asked about, half of whose reach the cells must hold.</param>
    public PaintedBands(Crossings zebras, float markM)
    {
        _zebras = zebras;
        var widestM = 0f;
        for (var crossing = 0; crossing < zebras.Count; crossing++)
        {
            widestM = MathF.Max(widestM, MathF.Max(zebras.SpanM[crossing], zebras.DepthM[crossing]));
        }

        _cellM = MathF.Max(widestM + markM, 1f);
        for (var crossing = 0; crossing < zebras.Count; crossing++)
        {
            if (zebras.DepthM[crossing] <= 0f) continue;

            var cell = CellOf(zebras.CentreM[crossing]);
            if (!_filed.TryGetValue(cell, out var filed)) _filed[cell] = filed = [];
            filed.Add(crossing);
        }
    }

    /// <summary>The ground one band covers: its depth along the traffic crossing it and its span across.</summary>
    public OrientedBox Band(int crossing) =>
        new(_zebras.CentreM[crossing], _zebras.Axis[crossing], _zebras.DepthM[crossing] * 0.5f, _zebras.SpanM[crossing] * 0.5f);

    /// <summary>The first painted band a mark's ground reaches into, or <see cref="Crossings.None"/>.</summary>
    public int Under(in OrientedBox mark)
    {
        var (x, y) = CellOf(mark.CentreM);
        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                if (!_filed.TryGetValue((x + dx, y + dy), out var near)) continue;

                foreach (var crossing in near)
                {
                    if (mark.Overlaps(Band(crossing), 0f)) return crossing;
                }
            }
        }

        return Crossings.None;
    }

    (int X, int Y) CellOf(Vector2 pointM) => ((int)MathF.Floor(pointM.X / _cellM), (int)MathF.Floor(pointM.Y / _cellM));
}
