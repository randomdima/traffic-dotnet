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

    /// <summary>Half the box's extent along a direction.</summary>
    float ReachM(Vector2 axis) =>
        (MathF.Abs(Vector2.Dot(Along, axis)) * HalfLengthM) + (MathF.Abs(Vector2.Dot(Heading.RightOf(Along), axis)) * HalfWidthM);
}
