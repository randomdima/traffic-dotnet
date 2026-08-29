using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// The cells a laid plan writes its ground into, and the direction of travel it writes under a
/// carriageway. <b>One brush for every plan this build lays</b>: a map is a classification of cells plus
/// the records that point into it, and two plans painting the same band two ways would disagree about
/// where a road ends.
/// </summary>
/// <remarks>
/// <b>The order the strokes are laid in is the caller's and is load-bearing.</b> Every one of them writes
/// over whatever was there, so a plan lays the pavement, then the carriageway over it, then the ground a
/// junction shares, then the paint — and what a cell ends up as is the last thing laid over it. The one
/// exception says so in its own name (<see cref="Pad"/>).
/// </remarks>
internal readonly struct GroundPainter(
    Ground[] cells, sbyte[] laneDirs, int gridWidth, int gridHeight, float cellSizeM, float roadSideSign)
{
    /// <summary>How fine a shape is walked across the cells. Under half a cell, so no cell of a band is stepped over.</summary>
    readonly float _stepM = cellSizeM * 0.4f;

    /// <summary>
    /// The cells under a road: carriageway out to half its width, and the direction of travel on it —
    /// <b>each half of the road carrying the way traffic goes on it</b>, which is the only thing "the
    /// lane's direction" can mean where two of them share a centreline.
    /// </summary>
    public void Road(ReadOnlySpan<ArcSeg> chain, float widthM)
    {
        var halfM = widthM * 0.5f;
        var lengthM = Spline.TotalLengthM(chain);
        for (var atM = 0f; atM <= lengthM; atM += _stepM)
        {
            var on = Spline.SampleAt(chain, MathF.Min(atM, lengthM));
            for (var acrossM = -halfM; acrossM <= halfM; acrossM += _stepM)
            {
                var cell = CellAt(on.PositionM + (on.Right * acrossM));
                if (cell < 0) continue;

                cells[cell] = Ground.Road;
                Along(cell, acrossM * roadSideSign >= 0f ? on.Direction : -on.Direction);
            }
        }
    }

    /// <summary>
    /// The pavement either side of a road: the band from the kerb out to a walk's width of it, laid the
    /// whole length of the centreline and over whatever the ground was.
    /// </summary>
    public void Verge(ReadOnlySpan<ArcSeg> chain, float fromM, float toM, Ground ground)
    {
        var lengthM = Spline.TotalLengthM(chain);
        for (var atM = 0f; atM <= lengthM; atM += _stepM)
        {
            var on = Spline.SampleAt(chain, MathF.Min(atM, lengthM));
            for (var acrossM = fromM; acrossM <= toM; acrossM += _stepM)
            {
                Lay(on.PositionM + (on.Right * acrossM), ground);
                Lay(on.PositionM - (on.Right * acrossM), ground);
            }
        }
    }

    /// <summary>
    /// The ground the arms of a junction share: a square of it about the node, wide enough to cover every
    /// carriageway that meets there. <b>It carries no direction</b> — a body in a box is driven along the
    /// join the graph laid, and a cell that still claimed a lane's bearing would be a second answer to the
    /// same question.
    /// </summary>
    public void Mouth(Vector2 centreM, float halfSideM)
    {
        for (var alongM = -halfSideM; alongM <= halfSideM; alongM += _stepM)
        {
            for (var acrossM = -halfSideM; acrossM <= halfSideM; acrossM += _stepM)
            {
                Lay(centreM + new Vector2(alongM, acrossM), Ground.Intersection);
            }
        }
    }

    /// <summary>
    /// The head of a dead end: the disc a car works itself round on (TER-5a), which no arm covers — and,
    /// laid wider and first, the pavement that runs round the outside of it.
    /// </summary>
    public void Head(Vector2 centreM, float radiusM, Ground ground)
    {
        for (var alongM = -radiusM; alongM <= radiusM; alongM += _stepM)
        {
            for (var acrossM = -radiusM; acrossM <= radiusM; acrossM += _stepM)
            {
                var offsetM = new Vector2(alongM, acrossM);
                if (offsetM.LengthSquared() > radiusM * radiusM) continue;

                Lay(centreM + offsetM, ground);
            }
        }
    }

    /// <summary>
    /// One kerb fillet: the wedge between two carriageways paved back to the arc tangent to both of them
    /// (TER-5), which is the ground a turning car takes. <b>Everything the corner square holds outside the
    /// arc</b> — the arc itself is where the block begins and is left as it was.
    /// </summary>
    public void Fillet(Vector2 cornerM, Vector2 arcCentreM, float radiusM)
    {
        var minM = Vector2.Min(cornerM, arcCentreM);
        var maxM = Vector2.Max(cornerM, arcCentreM);
        for (var x = minM.X; x <= maxM.X; x += _stepM)
        {
            for (var y = minM.Y; y <= maxM.Y; y += _stepM)
            {
                var pointM = new Vector2(x, y);
                if ((pointM - arcCentreM).LengthSquared() < radiusM * radiusM) continue;

                Lay(pointM, Ground.Intersection);
            }
        }
    }

    /// <summary>
    /// A crossing's paint: the band across a carriageway, <b>laid only over ground a car drives on</b> and
    /// keeping the direction of travel underneath it (TER-6), so a car on the paint is still held to its
    /// lane and the pavement either end of the band is still somewhere to step off onto.
    /// </summary>
    public void Crossing(Vector2 centreM, Vector2 axis, float depthM, float spanM)
    {
        var across = Heading.RightOf(axis);
        for (var alongM = -depthM * 0.5f; alongM <= depthM * 0.5f; alongM += _stepM)
        {
            for (var acrossM = -spanM * 0.5f; acrossM <= spanM * 0.5f; acrossM += _stepM)
            {
                var cell = CellAt(centreM + (axis * alongM) + (across * acrossM));
                if (cell < 0 || cells[cell] != Ground.Road) continue;

                cells[cell] = Ground.Crosswalk;
            }
        }
    }

    /// <summary>The ground a car park covers: its own rectangle, laid on the bearing the lot stands at.</summary>
    public void Lot(Vector2 centreM, Vector2 axis, Vector2 halfExtentM)
    {
        var side = Heading.RightOf(axis);
        for (var alongM = -halfExtentM.X; alongM <= halfExtentM.X; alongM += _stepM)
        {
            for (var acrossM = -halfExtentM.Y; acrossM <= halfExtentM.Y; acrossM += _stepM)
            {
                Lay(centreM + (axis * alongM) + (side * acrossM), Ground.Parking);
            }
        }
    }

    /// <summary>
    /// A square of paving to stand on, laid <b>only over ground nothing else took</b>: the roads are
    /// painted first, and a pad that overwrote one would be a hole in the map where somebody is standing.
    /// </summary>
    public void Pad(Vector2 centreM, float sideM)
    {
        var halfM = sideM * 0.5f;
        for (var alongM = -halfM; alongM <= halfM; alongM += _stepM)
        {
            for (var acrossM = -halfM; acrossM <= halfM; acrossM += _stepM)
            {
                var cell = CellAt(centreM + new Vector2(alongM, acrossM));
                if (cell < 0 || cells[cell] != Ground.Grass) continue;

                cells[cell] = Ground.Sidewalk;
            }
        }
    }

    void Lay(Vector2 pointM, Ground ground)
    {
        var cell = CellAt(pointM);
        if (cell < 0) return;

        cells[cell] = ground;
        if (!GroundIsDirectional(ground)) Along(cell, Vector2.Zero);
    }

    /// <summary>
    /// Whether the ground under a cell reads a bearing off it. It is the plan's own vocabulary rather than
    /// the permission table's, which lives above this folder and may not be read from here.
    /// </summary>
    static bool GroundIsDirectional(Ground ground) => ground is Ground.Road or Ground.Crosswalk;

    void Along(int cell, Vector2 direction)
    {
        laneDirs[cell * 2] = Quantised(direction.X);
        laneDirs[(cell * 2) + 1] = Quantised(direction.Y);
    }

    /// <summary>To 1/127 of a unit vector, which is what the format carries and what the reader expands.</summary>
    static sbyte Quantised(float component) => (sbyte)Math.Clamp(MathF.Round(component * 127f), -127f, 127f);

    int CellAt(Vector2 pointM)
    {
        var x = (int)MathF.Floor(pointM.X / cellSizeM);
        var y = (int)MathF.Floor(pointM.Y / cellSizeM);
        return x < 0 || y < 0 || x >= gridWidth || y >= gridHeight ? -1 : (y * gridWidth) + x;
    }
}
