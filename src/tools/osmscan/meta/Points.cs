namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>What stands about the town that is neither road nor building</b>: the places a trip is made to, the street
/// furniture along the kerbs with the road and side each stands by, the barriers a pedestrian cannot cross, and the
/// roads OSM knows are being built or were given up.
/// </summary>
internal static class Points
{
    /// <summary>The farthest from a road a piece of street furniture is put to it.</summary>
    const double StreetM = 20;

    /// <summary>The keys a place is named by, the first its tags carry.</summary>
    static readonly string[] PlaceKeys = ["shop", "amenity", "office", "tourism", "craft", "healthcare", "leisure", "historic", "club"];

    /// <summary>Amenities that are furniture rather than places, laid with the furniture.</summary>
    static readonly HashSet<string> Furnishing =
        ["bench", "waste_basket", "shelter", "telephone", "post_box", "vending_machine", "drinking_water", "recycling", "clock", "bicycle_parking", "parking_space"];

    static readonly string[] FurnitureKeys = ["natural", "highway", "amenity", "man_made", "power", "advertising", "emergency", "leisure", "tourism"];

    public static void Lay(Town town, string into, List<Written> written)
    {
        var places = new List<PointRecord>();
        foreach (var element in Element.Read(town.Fetched.Places))
        {
            if (PlaceKeys.FirstOrDefault(element.Tags.ContainsKey) is not { } key || (key == "amenity" && Furnishing.Contains(element.Tags[key]))) continue;
            if (element.Lat == 0 && element.Lon == 0) continue;

            places.Add(new PointRecord { Id = element.Key, Kind = $"{key}={element.Tags[key]}", Name = element.Tag("name"), At = [element.Lat, element.Lon], Tags = element.Tags });
        }

        written.Add(Layers.Write(into, "places",
            "Every place a trip may be made to — shop, amenity, office, sight, sport — at one point each, with its kind, name and tags.",
            ["osm-places"], places,
            new { kinds = places.GroupBy(record => record.Kind.Split('=')[0]).ToDictionary(g => g.Key, g => g.Count()), top = places.GroupBy(record => record.Kind).OrderByDescending(g => g.Count()).Take(30).ToDictionary(g => g.Key, g => g.Count()) }));

        var furniture = new List<PointRecord>();
        foreach (var element in Element.Read(town.Fetched.Furniture))
        {
            var key = FurnitureKeys.FirstOrDefault(element.Tags.ContainsKey);
            if (key is null) continue;

            var at = element.Type == 'n' ? town.Plane.At(element.Lat, element.Lon) : town.Plane.Line(element.Geometry) is { Length: > 0 } line ? Shape.Along(line, Shape.Length(line) / 2) : default;
            var near = town.NearestRoad(at, StreetM);
            string? side = null;
            if (near is { } beside)
            {
                var (a, b) = (town.RoadLineM[beside.Road][beside.Segment], town.RoadLineM[beside.Road][beside.Segment + 1]);
                side = Pt.Cross(b - a, at - a) > 0 ? "right" : "left";
            }

            furniture.Add(new PointRecord
            {
                Id = element.Key,
                Kind = $"{key}={element.Tags[key]}",
                Name = element.Tag("name"),
                At = element.Type == 'n' ? [element.Lat, element.Lon] : null,
                Line = element.Type == 'w' ? element.Geometry : null,
                Road = near is { } road ? town.Roads[road.Road].Id : null,
                RoadM = near is { } off ? Math.Round(off.OffM, 1) : null,
                Side = side,
                Tags = element.Tags,
            });
        }

        written.Add(Layers.Write(into, "furniture",
            "What stands along the streets — trees, lamps, benches, shelters, cabinets, poles, hydrants — each with the road within reach it stands by and on which side as the road is drawn.",
            ["osm-furniture"], furniture,
            new { kinds = furniture.GroupBy(record => record.Kind).OrderByDescending(g => g.Count()).Take(30).ToDictionary(g => g.Key, g => g.Count()), byARoad = furniture.Count(record => record.Road is not null) }));

        var barriers = Element.Read(town.Fetched.Barriers, 'w').Where(way => way.Geometry.Length >= 4)
            .Select(way => new PointRecord { Id = way.Key, Kind = $"barrier={way.Tag("barrier")}", Name = way.Tag("name"), Line = way.Geometry, Tags = way.Tags }).ToList();
        written.Add(Layers.Write(into, "barriers",
            "Every barrier OSM draws as a line — wall, fence, hedge, kerb, guard rail, retaining wall — which a pedestrian or car does not pass but at a gap or gate.",
            ["osm-barriers"], barriers,
            new { kinds = barriers.GroupBy(record => record.Kind).OrderByDescending(g => g.Count()).ToDictionary(g => g.Key, g => g.Count()) }));

        var unbuilt = Element.Read(town.Fetched.Unbuilt, 'w').Where(way => way.Geometry.Length >= 4)
            .Select(way => new PointRecord { Id = way.Key, Kind = $"highway={way.Tag("highway")}", Name = way.Tag("name"), Line = way.Geometry, Tags = way.Tags }).ToList();
        written.Add(Layers.Write(into, "unbuilt",
            "Roads OSM knows are under construction, proposed, disused or abandoned, which a later survey may find built or gone.",
            ["osm-unbuilt"], unbuilt,
            new { kinds = unbuilt.GroupBy(record => record.Kind).ToDictionary(g => g.Key, g => g.Count()) }));
    }
}

internal sealed class PointRecord
{
    /// <summary><c>n123</c>, <c>w123</c> or <c>r123</c>.</summary>
    public required string Id { get; init; }

    /// <summary>The tag it is named by, as key=value.</summary>
    public required string Kind { get; init; }

    public string? Name { get; init; }

    /// <summary>Lat, lon in 1e-7°; a way's or relation's centre for a place.</summary>
    public int[]? At { get; init; }

    /// <summary>A line's lat, lon pairs in 1e-7°.</summary>
    public int[]? Line { get; init; }

    public long? Road { get; init; }

    public double? RoadM { get; init; }

    public string? Side { get; init; }

    public required Dictionary<string, string> Tags { get; init; }
}
