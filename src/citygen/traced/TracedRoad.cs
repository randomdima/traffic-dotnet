namespace TrafficSimulation.CityGen.Traced;

/// <summary>
/// <b>One road as a map holds it, where its roads are its own</b> (<see cref="Map.TownMap.Roads"/>): the OSM way it was
/// traced off, its class, whether it is a bridge or part of a roundabout, how many lanes it is driven in each way and
/// whether that count is tagged or assumed, where its carriageway's middle stands off its line, how wide its carriageway
/// was measured, and its line.
/// </summary>
internal sealed record TracedRoad
{
    /// <summary>The OSM way it was imported off, to look it up or fetch it again by; nought for a road drawn by hand.</summary>
    public required long OsmId { get; init; }

    /// <summary>OSM's own <c>highway</c> value: <c>primary</c>, <c>residential</c>, <c>trunk_link</c>.</summary>
    public required string Highway { get; init; }

    public bool Bridge { get; init; }

    /// <summary>Whether it is a roundabout's circulating carriageway, or a piece of one.</summary>
    public bool Roundabout { get; init; }

    /// <summary>The lanes OSM counts driven the way its points run.</summary>
    public required int LanesForward { get; init; }

    /// <summary>The lanes driven against them.</summary>
    public required int LanesBackward { get; init; }

    /// <summary>The lanes driven both ways: a single lane two-way traffic shares, or a centre turning lane.</summary>
    public required int LanesShared { get; init; }

    /// <summary>Whether OSM's count is tagged rather than assumed for an untagged way of its class (Key:lanes).</summary>
    public bool LanesTagged { get; init; }

    /// <summary>Whether any lane has a <c>turn</c>, <c>change</c> or bus <c>:lanes</c> entry, which counts its lanes lane by lane.</summary>
    public bool Marked { get; init; }

    /// <summary>How far its carriageway's middle stands off its points, to the right of them as drawn, as a share of its width.</summary>
    public float CentreOffsetShare { get; init; }

    /// <summary>
    /// How wide its carriageway was measured kerb to kerb, where a measure is one to lay it by — its tag, the surface
    /// OSM outlines it with, or imagery under a street, on a way OSM gives no width lane by lane nor places off its
    /// middle (<see cref="TracedMapImport"/>) — or null.
    /// </summary>
    public float? WidthM { get; init; }

    /// <summary>Indices into <see cref="Map.TownMap.PointM"/>, in the way's own order.</summary>
    public required int[] Points { get; init; }

    public int Lanes => LanesForward + LanesBackward + LanesShared;

    /// <summary>
    /// How much a way matters by its class, for which class a road walked through several ways is read as: nought for
    /// any class that is not a street.
    /// </summary>
    internal static int Rank(string highway) => highway switch
    {
        "motorway" => 7,
        "trunk" => 6,
        "primary" => 5,
        "secondary" => 4,
        "tertiary" => 3,
        "unclassified" or "residential" => 2,
        "living_street" => 1,
        _ => 0,
    };
}

/// <summary>A place in metres held in doubles, for the arithmetic done before a place is a float.</summary>
internal readonly record struct Vector2D(double X, double Y);
