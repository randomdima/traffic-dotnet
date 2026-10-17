using System.Numerics;

namespace TrafficSimulation.Core.Geometry;

/// <summary>
/// <b>A rectangle turned to any heading</b>: its middle, the unit direction its length runs along, and half its length
/// and half its width.
/// </summary>
internal readonly record struct OrientedBox(Vector2 CentreM, Vector2 Along, float HalfLengthM, float HalfWidthM)
{
    /// <summary>
    /// <b>Whether this and another reach more than <paramref name="intoM"/> into each other</b>, by separating axes: on no
    /// one of the four sides' directions do the two stand further apart than their reaches less the margin.
    /// </summary>
    public bool Overlaps(in OrientedBox other, float intoM)
    {
        var apart = other.CentreM - CentreM;
        ReadOnlySpan<Vector2> axes = [Along, Heading.RightOf(Along), other.Along, Heading.RightOf(other.Along)];
        foreach (var axis in axes)
        {
            if (MathF.Abs(Vector2.Dot(apart, axis)) > ReachM(axis) + other.ReachM(axis) - intoM) return false;
        }

        return true;
    }

    /// <summary>
    /// <b>The stretch of a straight strip the part of the box over it covers</b>, in metres along the strip from
    /// <paramref name="stripAtM"/> — the least and the most of every corner inside it and every point a side crosses one of
    /// its edges at. False where none of the box is over it.
    /// </summary>
    /// <remarks>
    /// <b>Not the whole box's reach along it</b>: a box turned across a strip with one end over it and the other off it
    /// covers only as far along as the end that is over it.
    /// </remarks>
    /// <param name="stripAlong">The unit direction the strip runs.</param>
    public bool CoversAlong(Vector2 stripAtM, Vector2 stripAlong, float stripHalfWidthM, out float fromM, out float toM)
    {
        var across = Heading.RightOf(stripAlong);
        var side = Heading.RightOf(Along) * HalfWidthM;
        var end = Along * HalfLengthM;
        Span<Vector2> corners = [CentreM + end + side, CentreM + end - side, CentreM - end - side, CentreM - end + side];
        fromM = float.PositiveInfinity;
        toM = float.NegativeInfinity;
        for (var at = 0; at < corners.Length; at++)
        {
            var (aM, bM) = (corners[at] - stripAtM, corners[(at + 1) % corners.Length] - stripAtM);
            var (aAlongM, aAcrossM) = (Vector2.Dot(aM, stripAlong), Vector2.Dot(aM, across));
            var (bAlongM, bAcrossM) = (Vector2.Dot(bM, stripAlong), Vector2.Dot(bM, across));
            if (MathF.Abs(aAcrossM) <= stripHalfWidthM) (fromM, toM) = (MathF.Min(fromM, aAlongM), MathF.Max(toM, aAlongM));

            foreach (var edgeM in (ReadOnlySpan<float>)[-stripHalfWidthM, stripHalfWidthM])
            {
                if ((aAcrossM - edgeM) * (bAcrossM - edgeM) >= 0f) continue;

                var crossesM = aAlongM + ((bAlongM - aAlongM) * (edgeM - aAcrossM) / (bAcrossM - aAcrossM));
                (fromM, toM) = (MathF.Min(fromM, crossesM), MathF.Max(toM, crossesM));
            }
        }

        return fromM <= toM;
    }

    /// <summary>Half the box's extent along a direction.</summary>
    float ReachM(Vector2 axis) =>
        (MathF.Abs(Vector2.Dot(Along, axis)) * HalfLengthM) + (MathF.Abs(Vector2.Dot(Heading.RightOf(Along), axis)) * HalfWidthM);
}
