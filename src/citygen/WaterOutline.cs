using System.Numerics;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>The water as a map may carry it: cut to the map's own edges</b> (GEN-2b).
/// </summary>
/// <remarks>
/// <b>A shore is drawn past the town on purpose and cut afterwards.</b> A bank that closed inside the extent
/// would be a lake, and a sea has no far shore on the map at all, so the shape is laid well outside and this
/// is what makes it a shape the map can hold: the outline is also what the water is <em>drawn</em> from, and
/// an uncut ring paints open sea over the void beyond the world. It is the one place the cut is made, for the
/// towns that are generated and the fixtures that arrive as files alike.
/// </remarks>
internal static class WaterOutline
{
    /// <summary>
    /// The ring clipped against the four edges of the map, a half-plane at a time. Nothing is added that was
    /// not on the ring except the points where it crosses an edge, so a shore that was already inside comes
    /// back as it went in.
    /// </summary>
    public static Vector2[] CutToTheMap(ReadOnlySpan<Vector2> ringM, Vector2 extentM)
    {
        var ring = new List<Vector2>(ringM.Length * 2);
        ring.AddRange(ringM);

        var cut = new List<Vector2>(ring.Count);
        for (var edge = 0; edge < 4; edge++)
        {
            cut.Clear();
            for (var point = 0; point < ring.Count; point++)
            {
                var atM = ring[point];
                var nextM = ring[(point + 1) % ring.Count];
                var inside = Inside(atM, edge, extentM);
                if (inside) cut.Add(atM);
                if (inside != Inside(nextM, edge, extentM)) cut.Add(Crossing(atM, nextM, edge, extentM));
            }

            (ring, cut) = (cut, ring);
            if (ring.Count == 0) return [];
        }

        return [.. ring];
    }

    /// <summary>Which side of one of the map's four edges a point stands, the inside being the map's own ground.</summary>
    static bool Inside(Vector2 atM, int edge, Vector2 extentM) => edge switch
    {
        0 => atM.X >= 0f,
        1 => atM.X <= extentM.X,
        2 => atM.Y >= 0f,
        _ => atM.Y <= extentM.Y,
    };

    /// <summary>Where a run between two points meets that edge.</summary>
    static Vector2 Crossing(Vector2 fromM, Vector2 toM, int edge, Vector2 extentM)
    {
        var atM = edge switch
        {
            0 => 0f,
            1 => extentM.X,
            2 => 0f,
            _ => extentM.Y,
        };

        var along = edge < 2
            ? (atM - fromM.X) / (toM.X - fromM.X)
            : (atM - fromM.Y) / (toM.Y - fromM.Y);
        var crossingM = Vector2.Lerp(fromM, toM, Math.Clamp(along, 0f, 1f));

        // On the edge and not a rounding either side of it: the next edge's cut reads this point back, and a
        // shore a millionth of a metre off the map is still off the map.
        return edge < 2 ? crossingM with { X = atM } : crossingM with { Y = atM };
    }
}
