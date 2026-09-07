using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

internal sealed partial class GroundShapes
{
    /// <summary>
    /// How many lines through a box may pass within reach of one point before the index's answer stops
    /// being the whole one. A four-armed crossroads of two-way streets admits twelve movements and every
    /// one of them runs near the middle of it; this is that with room to spare, and the walk falls back to
    /// every line in the town rather than truncate.
    /// </summary>
    const int MostTurnsNear = 32;

    ChainIndex _turnIndex = null!;
    ArcSeg[] _turnArcs = [];
    int[] _turnArcAt = [0];
    float[] _turnHalfM = [];
    float[] _turnLengthM = [];
    float _farthestTurnM;

    /// <summary>
    /// <b>Whether a point stands on a line a car is turned through a box on</b> — which is the whole of what
    /// a junction is on the ground (TER-5). There is no disc and no box: the tarmac inside an intersection
    /// is the band swept by the movements the town admits there, so a skewed, one-way or five-armed
    /// junction is answered for correctly without anything here knowing what shape it made.
    /// </summary>
    /// <remarks>
    /// <b>Asked at nought for the tarmac and at a walk for the pavement beside it</b> (TER-3c.3). A
    /// movement swings wider than either of the arms it runs between, so the arms' own bands do not carry
    /// the pavement past the box: what the walk is laid off is the tarmac, and a box's tarmac is these
    /// lines. Left out, the pavement the town walks turned a junction's corner over open grass, because
    /// the wrap follows the movement and the drawn band followed the arms.
    /// </remarks>
    bool Turns(Vector2 pointM, float outM)
    {
        Span<int> near = stackalloc int[MostTurnsNear];
        Span<float> alongM = stackalloc float[MostTurnsNear];
        var found = _turnIndex.Near(pointM, _farthestTurnM + outM, near, alongM);
        var count = Math.Min(found, near.Length);
        for (var at = 0; at < count; at++)
        {
            if (Sweeps(near[at], alongM[at], pointM, outM)) return true;
        }

        // The index answered with more lines than there was room for, so what it gave back is part of the
        // answer rather than the answer (the same bargain <see cref="Roads"/> keeps).
        if (found <= near.Length) return false;

        for (var turn = 0; turn < _turnHalfM.Length; turn++)
        {
            var atM = Spline.ProjectM(ArcsOfTurn(turn), pointM, _turnLengthM[turn] * 0.5f, _turnLengthM[turn]);
            if (Sweeps(turn, atM, pointM, outM)) return true;
        }

        return false;
    }

    /// <summary>
    /// One line's own band, grown by the offset asked for — <b>along its ends as well as across it</b>, so
    /// the ground round the point of hand-over between a lane and the box is the same whichever of the two
    /// is asked.
    /// </summary>
    /// <remarks>
    /// <b>The tarmac is squared off at both ends and the offset turns that end</b>, the way a road's is
    /// (<see cref="Weigh"/>): a band ends where its own line does (TER-7a), and what stands a distance
    /// outside a square end is that end grown by the distance. Only a projection the chain had to clamp
    /// says the point is beyond the line at all — asked as a bare distance along, every point on a bend
    /// answers a little off perpendicular and the whole band reads as outside itself.
    /// </remarks>
    bool Sweeps(int turn, float atM, Vector2 pointM, float outM)
    {
        var on = Spline.SampleAt(ArcsOfTurn(turn), atM);
        var offsetM = pointM - on.PositionM;
        var alongM = Vector2.Dot(offsetM, on.Direction);
        var beyondM = atM <= 0f
            ? MathF.Max(0f, -alongM)
            : atM >= _turnLengthM[turn] ? MathF.Max(0f, alongM) : 0f;

        var acrossM = MathF.Abs(Vector2.Dot(offsetM, on.Right)) - _turnHalfM[turn];
        return OffTheBandM(acrossM, beyondM) <= outM;
    }

    ReadOnlySpan<ArcSeg> ArcsOfTurn(int turn) =>
        _turnArcs.AsSpan(_turnArcAt[turn], _turnArcAt[turn + 1] - _turnArcAt[turn]);

    /// <summary>
    /// The lines a car is turned through a box on, laid over the index that answers which of them reach a
    /// point. <b>Read off the plan's own connectors</b> (<see cref="LaneLines"/>) and never re-derived: the
    /// ground under a movement and the movement itself are one geometry (TER-7).
    /// </summary>
    void LayTheTurns(LaneLines lanes, Vector2 worldSizeM, SimConfig config)
    {
        var laid = 0;
        for (var connector = 0; connector < lanes.ConnectorCount; connector++)
        {
            if (lanes.ArcsOfConnector(connector).Length > 0) laid++;
        }

        _turnArcAt = new int[laid + 1];
        _turnHalfM = new float[laid];
        _turnLengthM = new float[laid];
        var arcs = new List<ArcSeg>();
        var index = new ChainIndex.Builder();

        var turn = 0;
        for (var connector = 0; connector < lanes.ConnectorCount; connector++)
        {
            var line = lanes.ArcsOfConnector(connector);
            if (line.Length == 0) continue;

            foreach (var arc in line) arcs.Add(arc);

            _turnArcAt[turn + 1] = arcs.Count;
            _turnHalfM[turn] = lanes.ConnectorWidthM(connector) * 0.5f;
            _turnLengthM[turn] = lanes.ConnectorLengthM[connector];
            _farthestTurnM = MathF.Max(_farthestTurnM, _turnHalfM[turn]);
            index.Add(turn, line, _turnLengthM[turn]);
            turn++;
        }

        _turnArcs = [.. arcs];
        _turnIndex = index.Seal(config.Terrain.GroundBucketM);
    }
}
