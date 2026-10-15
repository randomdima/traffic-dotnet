using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.App.Debug;

/// <summary>
/// The box each chain of one of the town's rosters fits in (<see cref="ChainIndex.Box"/>), laid the first time the
/// roster is drawn and kept while it is the town's. <b>A box is its arcs sampled a metre at a time</b>, and asked
/// afresh of every lane and footway at each relay it was most of what the nodes layer cost.
/// </summary>
internal sealed class ChainBoxes
{
    Vector2[] _leastM = [];
    Vector2[] _mostM = [];
    object? _of;

    /// <summary>
    /// Whether the boxes are laid for this roster already; if not, room for <paramref name="count"/> is made and the
    /// caller lays each with <see cref="Lay"/> before asking.
    /// </summary>
    public bool LaidFor(object roster, int count)
    {
        if (ReferenceEquals(roster, _of)) return true;

        _leastM = new Vector2[count];
        _mostM = new Vector2[count];
        _of = roster;
        return false;
    }

    public void Lay(int chain, ReadOnlySpan<ArcSeg> arcs) => (_leastM[chain], _mostM[chain]) = Of(arcs);

    public bool OnScreen(int chain, Vector2 viewCentreM, Vector2 viewSpanM) =>
        Meets(_leastM[chain], _mostM[chain], viewCentreM, viewSpanM);

    /// <summary>The box a chain fits in; one with no arcs is inside out, and meets no view.</summary>
    public static (Vector2 LeastM, Vector2 MostM) Of(ReadOnlySpan<ArcSeg> arcs)
    {
        var leastM = new Vector2(float.MaxValue);
        var mostM = new Vector2(float.MinValue);
        foreach (ref readonly var arc in arcs) ChainIndex.Box(arc, ref leastM, ref mostM);

        return (leastM, mostM);
    }

    /// <summary>
    /// <b>Whether any part of a chain could be on screen</b>, asked of the box the chain fits in and never of the circle
    /// over its own two ends.
    /// </summary>
    /// <remarks>
    /// <b>A chain is not inside the circle over its ends, and a town's pavement is full of the case.</b> A walking lane
    /// that runs down one side of the wedge between two roads and back up the other stands its two ends beside each
    /// other with a hundred and fifty metres of line between them, so that circle rejects it at every framing that
    /// shows the wedge — and a frame taken there drew the concrete with no line on it at all, which is a picture
    /// claiming the town has a hole it has not got. <b>A cull may cost a quad and may never cost a line.</b>
    /// </remarks>
    public static bool Meets(Vector2 leastM, Vector2 mostM, Vector2 viewCentreM, Vector2 viewSpanM)
    {
        var halfM = viewSpanM * 0.5f;
        return leastM.X <= viewCentreM.X + halfM.X && mostM.X >= viewCentreM.X - halfM.X
               && leastM.Y <= viewCentreM.Y + halfM.Y && mostM.Y >= viewCentreM.Y - halfM.Y;
    }
}
