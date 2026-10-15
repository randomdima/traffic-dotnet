namespace TrafficSimulation.World.Road;

/// <summary>
/// The paint each lane meets, projected onto that lane once at load: its own stop bar.
/// </summary>
/// <remarks>
/// Load time and never per tick — neither the paint nor the lane moves. Which lanes a zebra is painted across
/// is the ribbon marks' to say (TER-4c), and nothing here keeps a second answer to it.
/// </remarks>
internal sealed class LaneFurniture
{
    readonly float[] _stopBarM;
    readonly float[] _stopBarThicknessM;

    LaneFurniture(float[] stopBarM, float[] stopBarThicknessM)
    {
        _stopBarM = stopBarM;
        _stopBarThicknessM = stopBarThicknessM;
    }

    /// <param name="bars">
    /// <b>The town's own bars</b> (<see cref="Road.StopBars"/>, TER-6), the laying the picture paints and the
    /// lights hold at — never a second one.
    /// </param>
    /// <param name="roads">The lanes the bars are carried onto.</param>
    public static LaneFurniture Project(StopBars bars, RoadGraph roads)
    {
        var (barM, thicknessM) = BarsOn(bars, roads.LaneCount);
        return new LaneFurniture(barM, thicknessM);
    }

    /// <summary>How far along the lane its painted bar stands, or infinity where nothing was painted on it.</summary>
    public float StopBarAlongM(int lane) => _stopBarM[lane];

    /// <summary>The paint a car stops at the near edge of and has crossed at the far one.</summary>
    public float StopBarThicknessM(int lane) => _stopBarThicknessM[lane];

    /// <summary>
    /// <b>Each lane's own bar</b>, the one it ends at, carried over from the laying in the lane's own metres —
    /// the figure the bar was placed by, and not a projection of its centre back onto the lane. Infinity where
    /// nothing was painted, and a driver there has nothing to stop at short of the box itself.
    /// </summary>
    static (float[] AlongM, float[] ThicknessM) BarsOn(StopBars bars, int laneCount)
    {
        var alongM = new float[laneCount];
        var thicknessM = new float[laneCount];
        Array.Fill(alongM, float.PositiveInfinity);

        for (var bar = 0; bar < bars.Count; bar++)
        {
            alongM[bars.Lane[bar]] = bars.AlongM[bar];
            thicknessM[bars.Lane[bar]] = bars.ThicknessM[bar];
        }

        return (alongM, thicknessM);
    }
}
