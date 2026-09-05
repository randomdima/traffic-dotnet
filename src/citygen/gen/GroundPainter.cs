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

            // <b>The two bearings are reduced once for the whole width</b> and not once a cell: every cell
            // of one stride across a road carries one of the same two directions, and quantising them per
            // cell is a rounding done a million times a town for one of two answers.
            var withM = Quantised(on.Direction);
            var againstM = Quantised(-on.Direction);
            var stepM = on.Right * _stepM;
            var atLeftM = on.PositionM - (on.Right * halfM);
            for (var acrossM = -halfM; acrossM <= halfM; acrossM += _stepM, atLeftM += stepM)
            {
                var cell = CellAt(atLeftM);
                if (cell < 0) continue;

                cells[cell] = Ground.Road;
                var along = acrossM * roadSideSign >= 0f ? withM : againstM;
                laneDirs[cell * 2] = along.X;
                laneDirs[(cell * 2) + 1] = along.Y;
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
        var directional = GroundIsDirectional(ground);
        for (var atM = 0f; atM <= lengthM; atM += _stepM)
        {
            var on = Spline.SampleAt(chain, MathF.Min(atM, lengthM));
            var stepM = on.Right * _stepM;
            var nearM = on.PositionM + (on.Right * fromM);
            var farM = on.PositionM - (on.Right * fromM);
            for (var acrossM = fromM; acrossM <= toM; acrossM += _stepM, nearM += stepM, farM -= stepM)
            {
                Lay(nearM, ground, directional);
                Lay(farM, ground, directional);
            }
        }
    }

    /// <summary>
    /// A disc of one ground about a point: the mouth a junction's arms share, the head a dead end is
    /// turned round in (TER-5a), and the pavement that runs round the outside of either.
    /// </summary>
    /// <remarks>
    /// <b>A disc and not a square</b> (TER-5): the ground a junction's arms share is the same shape from
    /// every bearing, tangent to every arm whatever bearing that arm stands on. A square classifies its
    /// own four corners as ground a car drives on and leaves them drawn as the pavement they are.
    /// </remarks>
    public void Disc(Vector2 centreM, float radiusM, Ground ground)
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
    /// (TER-5), which is the ground a turning car takes. <b>The triangle the two kerbs make with the
    /// chord between their tangent points, less what the arc cuts off it</b> — the arc is where the block
    /// begins and is left as it was.
    /// </summary>
    /// <remarks>
    /// <b>The shape is the corner's own and not its bounding box.</b> The wedge stands on whatever
    /// bearing the two arms leave the junction at, so a box drawn round it paves the pavement either
    /// side of the corner on most bearings and collapses to nothing where the bisector runs down an
    /// axis. It is the same piece <c>GroundMesh</c> draws, laid cell by cell.
    /// </remarks>
    public void Fillet(Vector2 cornerM, Vector2 tangentAM, Vector2 tangentBM, Vector2 arcCentreM, float radiusM)
    {
        var minM = Vector2.Min(cornerM, Vector2.Min(tangentAM, tangentBM));
        var maxM = Vector2.Max(cornerM, Vector2.Max(tangentAM, tangentBM));
        var acrossX = StepsBetween(minM.X, maxM.X);
        var acrossY = StepsBetween(minM.Y, maxM.Y);
        for (var stepX = 0; stepX <= acrossX; stepX++)
        {
            for (var stepY = 0; stepY <= acrossY; stepY++)
            {
                var pointM = new Vector2(
                    Between(minM.X, maxM.X, stepX), Between(minM.Y, maxM.Y, stepY));
                if ((pointM - arcCentreM).LengthSquared() < radiusM * radiusM) continue;
                if (!Within(pointM, cornerM, tangentAM, tangentBM)) continue;

                Lay(pointM, Ground.Intersection);
            }
        }
    }

    /// <summary>Whether a point stands inside a triangle, by the side of each edge it falls on.</summary>
    static bool Within(Vector2 pointM, Vector2 aM, Vector2 bM, Vector2 cM)
    {
        var alongAB = Side(pointM, aM, bM);
        var alongBC = Side(pointM, bM, cM);
        var alongCA = Side(pointM, cM, aM);
        return (alongAB >= 0f && alongBC >= 0f && alongCA >= 0f)
               || (alongAB <= 0f && alongBC <= 0f && alongCA <= 0f);
    }

    static float Side(Vector2 pointM, Vector2 fromM, Vector2 toM) =>
        ((toM.X - fromM.X) * (pointM.Y - fromM.Y)) - ((toM.Y - fromM.Y) * (pointM.X - fromM.X));

    /// <summary>
    /// A crossing's paint: the band across a carriageway, <b>laid only over ground a car drives on</b> and
    /// keeping the direction of travel underneath it (TER-6), so a car on the paint is still held to its
    /// lane and the pavement either end of the band is still somewhere to step off onto.
    /// </summary>
    /// <remarks>
    /// <b>The band is swept a cell wider than the paint and laid nowhere but the carriageway.</b> The
    /// ground is classified cell by cell, so a road's own edge stands wherever its half-width rounded to —
    /// up to a cell past the kerb the paint is drawn to. Swept to the paint's own span, what is left over
    /// is a strip of carriageway at each kerb that a walker crossing has to step over and no crossing
    /// covers; widening the paint to reach it instead puts the end bar of every zebra on the pavement.
    /// <b>The two are different questions and this is the one the ground answers.</b>
    /// </remarks>
    public void Crossing(Vector2 centreM, Vector2 axis, float depthM, float spanM)
    {
        var across = Heading.RightOf(axis);
        var reachM = (spanM * 0.5f) + cellSizeM;
        for (var along = 0; along <= StepsAcross(depthM * 0.5f); along++)
        {
            for (var band = 0; band <= StepsAcross(reachM); band++)
            {
                var cell = CellAt(
                    centreM + (axis * At(along, depthM * 0.5f)) + (across * At(band, reachM)));
                if (cell < 0 || cells[cell] != Ground.Road) continue;

                cells[cell] = Ground.Crosswalk;
            }
        }
    }

    /// <summary>The ground a car park covers: its own rectangle, laid on the bearing the lot stands at.</summary>
    public void Lot(Vector2 centreM, Vector2 axis, Vector2 halfExtentM)
    {
        var side = Heading.RightOf(axis);
        for (var along = 0; along <= StepsAcross(halfExtentM.X); along++)
        {
            for (var across = 0; across <= StepsAcross(halfExtentM.Y); across++)
            {
                Lay(
                    centreM + (axis * At(along, halfExtentM.X)) + (side * At(across, halfExtentM.Y)),
                    Ground.Parking);
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

    /// <summary>
    /// How many steps a walk across a shape takes, and where each of them lands. <b>The last one is the
    /// shape's own edge</b> rather than the last multiple of the step that fits inside it: a stride is
    /// shorter than a cell, so a shape whose edge falls between two strides leaves the cell under that edge
    /// unpainted — a sliver of pavement inside a kerb fillet, or of grass inside a car park.
    /// </summary>
    int StepsAcross(float halfM) => (int)MathF.Ceiling(halfM * 2f / _stepM);

    float At(int step, float halfM) => MathF.Min(-halfM + (step * _stepM), halfM);

    int StepsBetween(float fromM, float toM) => (int)MathF.Ceiling((toM - fromM) / _stepM);

    float Between(float fromM, float toM, int step) => MathF.Min(fromM + (step * _stepM), toM);

    void Lay(Vector2 pointM, Ground ground) => Lay(pointM, ground, GroundIsDirectional(ground));

    void Lay(Vector2 pointM, Ground ground, bool directional)
    {
        var cell = CellAt(pointM);
        if (cell < 0) return;

        cells[cell] = ground;
        if (directional) return;

        laneDirs[cell * 2] = 0;
        laneDirs[(cell * 2) + 1] = 0;
    }

    /// <summary>
    /// Whether the ground under a cell reads a bearing off it. It is the plan's own vocabulary rather than
    /// the permission table's, which lives above this folder and may not be read from here.
    /// </summary>
    static bool GroundIsDirectional(Ground ground) => ground is Ground.Road or Ground.Crosswalk;

    /// <summary>A bearing as the plan carries it: each component to 1/127 of a unit vector.</summary>
    static (sbyte X, sbyte Y) Quantised(Vector2 direction) => (Quantised(direction.X), Quantised(direction.Y));

    static sbyte Quantised(float component) => (sbyte)Math.Clamp(MathF.Round(component * 127f), -127f, 127f);

    int CellAt(Vector2 pointM)
    {
        var x = (int)MathF.Floor(pointM.X / cellSizeM);
        var y = (int)MathF.Floor(pointM.Y / cellSizeM);
        return x < 0 || y < 0 || x >= gridWidth || y >= gridHeight ? -1 : (y * gridWidth) + x;
    }
}
