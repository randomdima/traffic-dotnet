using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>The railway's lanes as the town's ways are laid from</b> (RAIL-9): each lane's line, the collision channels it is
/// on, and the one width every rail lane's ribbon is — handed down by the railway as data, since the road and the walk
/// are laid below it.
/// </summary>
internal sealed record RailWayLines(ArcSeg[][] Arcs, byte[] Channels, float WidthM)
{
    public static RailWayLines None { get; } = new([], [], 0f);

    public int Count => Arcs.Length;

    /// <summary>Each lane's length, which is all the way table takes of it (<see cref="TownWays"/>).</summary>
    public float[] LengthsM()
    {
        var lengthM = new float[Arcs.Length];
        for (var lane = 0; lane < Arcs.Length; lane++) lengthM[lane] = Spline.TotalLengthM(Arcs[lane]);
        return lengthM;
    }
}
