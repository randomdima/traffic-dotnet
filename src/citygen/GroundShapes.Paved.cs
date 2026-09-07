using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

internal sealed partial class GroundShapes
{
    /// <summary>
    /// How many runs of the walk may pass within half a walk of one point before the index's answer stops
    /// being the whole one. Where two mouths and a corner meet, four is the most that has been seen; this is
    /// that with room to spare, and <see cref="Paved"/> falls back to every run rather than truncate.
    /// </summary>
    const int MostRunsNear = 16;

    ChainIndex _pavedIndex = null!;
    PavedRun[] _paved = [];
    float _halfWalkM;

    /// <summary>
    /// <b>Whether a point is pavement</b>: within half a walk of the line the pavement is walked down
    /// (TER-3c.3). One distance, one figure — so the band is a walk wide everywhere, and the kerb and the
    /// shell against the grass are the two offsets of one curve.
    /// </summary>
    /// <remarks>
    /// <b>Measured to the run and not across it</b>, so where a run ends the band ends in a half-round
    /// rather than square. It is what fills the wedge between two runs that give way to one another at a
    /// corner, and it is the same shape <c>GroundMesh</c> closes those joints with.
    /// </remarks>
    bool Paved(Vector2 pointM)
    {
        Span<int> near = stackalloc int[MostRunsNear];
        Span<float> alongM = stackalloc float[MostRunsNear];
        var found = _pavedIndex.Near(pointM, _halfWalkM, near, alongM);
        var count = Math.Min(found, near.Length);
        for (var at = 0; at < count; at++)
        {
            if (Reaches(near[at], alongM[at], pointM)) return true;
        }

        // The index answered with more runs than there was room for, so what it gave back is part of the
        // answer rather than the answer (the same bargain <see cref="Roads"/> keeps).
        if (found <= near.Length) return false;

        for (var run = 0; run < _paved.Length; run++)
        {
            var atM = Spline.ProjectM(_paved[run].Line, pointM, _paved[run].LengthM * 0.5f, _paved[run].LengthM);
            if (Reaches(run, atM, pointM)) return true;
        }

        return false;
    }

    bool Reaches(int run, float atM, Vector2 pointM) => Reaches(run, atM, pointM, _halfWalkM);

    /// <summary>Whether any of it stands within reach of a point — what a prop asks to be clear of.</summary>
    bool PavedWithin(Vector2 pointM, float reachM)
    {
        Span<int> near = stackalloc int[MostRunsNear];
        Span<float> alongM = stackalloc float[MostRunsNear];
        var withinM = _halfWalkM + reachM;
        var found = _pavedIndex.Near(pointM, withinM, near, alongM);
        var count = Math.Min(found, near.Length);
        for (var at = 0; at < count; at++)
        {
            if (Reaches(near[at], alongM[at], pointM, withinM)) return true;
        }

        if (found <= near.Length) return false;

        for (var run = 0; run < _paved.Length; run++)
        {
            var atM = Spline.ProjectM(_paved[run].Line, pointM, _paved[run].LengthM * 0.5f, _paved[run].LengthM);
            if (Reaches(run, atM, pointM, withinM)) return true;
        }

        return false;
    }

    bool Reaches(int run, float atM, Vector2 pointM, float withinM) =>
        Vector2.DistanceSquared(Spline.SampleAt(_paved[run].Line, atM).PositionM, pointM) <= withinM * withinM;

    /// <summary>
    /// The line the pavement is walked down, laid over the index that answers which runs of it reach a
    /// point. <b>Read off <see cref="Paving"/> and never re-derived</b>: the band drawn, the band answered
    /// for and the lanes walked down it are one construction (TER-7).
    /// </summary>
    void LayThePaving(Paving paving, SimConfig config)
    {
        _paved = paving.Walk;
        _halfWalkM = paving.WalkM * 0.5f;

        var index = new ChainIndex.Builder();
        for (var run = 0; run < _paved.Length; run++)
        {
            index.Add(run, _paved[run].Line, _paved[run].LengthM);
        }

        _pavedIndex = index.Seal(config.Terrain.GroundBucketM);
    }
}
