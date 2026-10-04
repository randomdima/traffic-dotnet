namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>Every surface that is a place rather than a road</b> — car parks, fuel stations, garage blocks, yards of
/// industry and port, schools, markets, squares, parks, water and every land use under the town — each with the
/// survey roads that run inside it, so a car park's aisles are known for the zone they are and not for streets.
/// </summary>
/// <remarks>
/// <para>
/// <b>A road is a zone's where most of it runs inside</b>: a road sampled every few metres, inside the smallest
/// zone of a kind that holds roads (<see cref="Holds"/>) at half its samples or more, is that zone's. A road
/// crossing the zone's edge is one of its ways in.
/// </para>
/// <para>
/// <b>A car park no mapper outlined is still a car park</b>: parking aisles (<c>service=parking_aisle</c>) that no
/// outlined lot holds, joined where they share a node, are given the hull round them as a lot of their own, and
/// say they were derived.
/// </para>
/// </remarks>
internal static class Zones
{
    /// <summary>How far apart a road is sampled to find the zone it runs in.</summary>
    const double StepM = 5;

    /// <summary>The share of a road's samples inside a zone that makes it the zone's.</summary>
    const double InsideShare = 0.5;

    const double CellM = 200;

    /// <summary>The kinds of zone whose roads are its own ways about it rather than streets.</summary>
    static readonly HashSet<string> Holds =
    [
        "parking", "parking_space", "fuel", "car_wash", "charging_station", "bus_station", "garages", "industrial", "port",
        "railway", "military", "construction", "education", "health", "market", "allotments", "services", "airport",
        "cemetery", "leisure", "square", "civic", "religious", "agriculture", "pier", "bridge",
    ];

    /// <summary>The kinds of zone that are a paved place a car crosses as a surface rather than along a road.</summary>
    static readonly HashSet<string> Paved = ["parking", "fuel", "car_wash", "bus_station", "garages", "square", "market"];

    /// <summary>The kinds of zone a road's surroundings are read as, the land use under it.</summary>
    static readonly HashSet<string> LandUse = ["residential", "commercial", "retail", "industrial", "port", "railway", "military", "agriculture", "green", "nature", "garages", "allotments", "education", "religious", "cemetery", "construction", "waste_land"];

    public static ZonesFound Lay(Town town, Flags flags, string into, List<Written> written)
    {
        var found = new ZonesFound();
        var zones = new List<(Element Element, Outline? Outline, string Kind, string? Sub)>();
        foreach (var element in Element.Read(town.Fetched.Zones))
        {
            if (Kind(element.Tags) is not { } kinded) continue;

            var (kind, sub) = kinded;
            var outline = Outline.Of(element, town.Plane);
            if (element.Type != 'n' && outline is null) continue;

            zones.Add((element, outline, kind, sub));
        }

        found.Named.AddRange(zones.Where(zone => zone.Kind is "square" or "leisure").Select(zone => zone.Element.Tags));
        found.Paved.AddRange(zones.Where(zone => zone.Outline is not null && Paved.Contains(zone.Kind)).Select(zone => zone.Outline!));
        var grid = new Grid(CellM);
        for (var at = 0; at < zones.Count; at++)
        {
            if (zones[at].Outline is not { } outline) continue;

            grid.Add(at, outline.Bounds);
            if (zones[at].Kind == "bridge") found.Bridges.Add((Key(zones[at].Element), zones[at].Element.Tag("name"), outline));
        }

        var waysIn = new Dictionary<int, List<long>>();
        for (var road = 0; road < town.Roads.Length; road++)
        {
            var (zone, landUse, edges) = Inside(town, road, zones, grid);
            if (zone is { } held) found.ZoneOf[road] = (Key(zones[held].Element), zones[held].Kind, zones[held].Sub);

            foreach (var edge in edges) (waysIn.TryGetValue(edge, out var list) ? list : waysIn[edge] = []).Add(town.Roads[road].Id);
            if (landUse is { } use) found.LandUseOf[road] = zones[use].Sub ?? zones[use].Kind;
        }

        // A lot derived round aisles another zone held takes them from it, so a zone's roads are read off last.
        var derived = DerivedLots(town, found, flags).ToList();
        var roadsOf = found.ZoneOf.GroupBy(pair => pair.Value.Id).ToDictionary(group => group.Key, group => group.Select(pair => town.Roads[pair.Key].Id).Order().ToArray());

        var spaces = zones.Select((zone, at) => (zone, at)).Where(pair => pair.zone.Kind == "parking_space").ToArray();
        var records = new List<ZoneRecord>(zones.Count);
        for (var at = 0; at < zones.Count; at++)
        {
            var (element, outline, kind, sub) = zones[at];
            var inside = kind == "parking" && outline is not null
                ? spaces.Count(space => outline.Holds(space.zone.Outline is { } own ? own.RingsM[0][0] : town.Plane.At(space.zone.Element.Lat, space.zone.Element.Lon)))
                : 0;
            records.Add(new ZoneRecord
            {
                Id = Key(element),
                Kind = kind,
                Sub = sub,
                Name = element.Tag("name"),
                Mapped = true,
                At = element.Type == 'n' ? [element.Lat, element.Lon] : null,
                Outer = outline?.Outer,
                Inner = outline is { Inner.Length: > 0 } ? outline.Inner : null,
                AreaM2 = outline is null ? null : Math.Round(outline.AreaM2),
                Roads = roadsOf.GetValueOrDefault(Key(element)),
                WaysIn = waysIn.TryGetValue(at, out var ways) ? [.. ways.Distinct()] : null,
                Spaces = inside > 0 ? inside : null,
                Tags = element.Tags,
            });
        }

        records.AddRange(derived);
        written.Add(Layers.Write(into, "zones",
            "Every surface that is a place rather than a road — car parks, fuel, garages, industry, port, schools, markets, squares, parks, water, land use — its outline in 1e-7°, and the survey roads inside it and leading into it.",
            ["osm-zones"], records,
            new
            {
                kinds = records.GroupBy(record => record.Kind).OrderByDescending(g => g.Count()).ToDictionary(g => g.Key, g => g.Count()),
                parking = records.Where(record => record.Kind == "parking").GroupBy(record => record.Sub ?? "unsaid").OrderByDescending(g => g.Count()).ToDictionary(g => g.Key, g => g.Count()),
                roadsInZones = found.ZoneOf.Count,
                roadsInZonesByKind = found.ZoneOf.Values.GroupBy(zone => zone.Kind).OrderByDescending(g => g.Count()).ToDictionary(g => g.Key, g => g.Count()),
                derivedLots = records.Count(record => !record.Mapped),
            }));

        found.Districts = Districts(town, found, into, written);
        return found;
    }

    /// <summary>
    /// The smallest zone that holds roads with most of a road inside it, the land use under its middle, and every
    /// zone it crosses into or out of.
    /// </summary>
    static (int? Zone, int? LandUse, List<int> Edges) Inside(Town town, int road, List<(Element Element, Outline? Outline, string Kind, string? Sub)> zones, Grid grid)
    {
        var line = town.RoadLineM[road];
        var lengthM = town.RoadLengthM[road];
        var near = new HashSet<int>();
        grid.Near(Box.Of(line), near);
        int? zone = null, landUse = null;
        var edges = new List<int>();
        if (near.Count == 0) return (null, null, edges);

        var samples = new List<Pt>();
        for (var alongM = Math.Min(StepM / 2, lengthM / 2); alongM <= lengthM; alongM += StepM) samples.Add(Shape.Along(line, alongM));
        var middle = Shape.Along(line, lengthM / 2);
        foreach (var at in near)
        {
            var (_, outline, kind, _) = zones[at];
            if (outline is null) continue;

            if (LandUse.Contains(kind) && outline.Holds(middle) && (landUse is null || outline.AreaM2 < zones[landUse.Value].Outline!.AreaM2)) landUse = at;
            if (!Holds.Contains(kind)) continue;

            var inside = samples.Count(outline.Holds);
            if (inside > 0 && inside < samples.Count) edges.Add(at);
            if (inside >= InsideShare * samples.Count && (zone is null || outline.AreaM2 < zones[zone.Value].Outline!.AreaM2)) zone = at;
        }

        return (zone, landUse, edges);
    }

    /// <summary>Parking aisles no outlined lot holds, joined where they meet, each piece given its hull as a lot.</summary>
    static IEnumerable<ZoneRecord> DerivedLots(Town town, ZonesFound found, Flags flags)
    {
        var loose = new List<int>();
        for (var road = 0; road < town.Roads.Length; road++)
        {
            if (town.Roads[road].Tag("service") == "parking_aisle" && !(found.ZoneOf.TryGetValue(road, out var zone) && zone.Kind == "parking")) loose.Add(road);
        }

        var parent = loose.ToDictionary(road => road, road => road);
        int Root(int road)
        {
            while (parent[road] != road) road = parent[road] = parent[parent[road]];
            return road;
        }

        var byNode = new Dictionary<int, int>();
        foreach (var road in loose)
        {
            foreach (var node in town.Roads[road].Nodes)
            {
                if (byNode.TryGetValue(node, out var other)) parent[Root(other)] = Root(road);
                else byNode[node] = road;
            }
        }

        foreach (var piece in loose.GroupBy(Root))
        {
            var points = piece.SelectMany(road => town.RoadLineM[road]).ToArray();
            var hull = Shape.Hull(points);
            if (hull.Length < 3) continue;

            int[] ring = [.. hull.Append(hull[0]).SelectMany(town.Plane.Degrees)];
            var id = $"lot{town.Roads[piece.Min()].Id}";
            foreach (var road in piece) found.ZoneOf[road] = (id, "parking", "derived_from_aisles");

            flags.Raise("parking_aisles_without_lot", town, hull[0], $"{piece.Count()} parking aisles no outlined car park holds", [.. piece.Select(road => $"w{town.Roads[road].Id}")]);
            yield return new ZoneRecord
            {
                Id = id,
                Kind = "parking",
                Sub = "derived_from_aisles",
                Mapped = false,
                Outer = [ring],
                AreaM2 = Math.Round(Math.Abs(Shape.SignedArea(hull))),
                Roads = [.. piece.Select(road => town.Roads[road].Id)],
                Tags = [],
            };
        }
    }

    /// <summary>The town's administrative districts and named places, written to a layer of their own.</summary>
    static List<(Outline Outline, string Name, int Level)> Districts(Town town, ZonesFound found, string into, List<Written> written)
    {
        var districts = new List<(Outline Outline, string Name, int Level)>();
        var records = new List<ZoneRecord>();
        foreach (var element in Element.Read(town.Fetched.Districts))
        {
            if (element.Type == 'r' && element.Tag("boundary") == "administrative" && Outline.Of(element, town.Plane) is { } outline)
            {
                var level = int.TryParse(element.Tag("admin_level"), out var tagged) ? tagged : 0;
                districts.Add((outline, element.Tag("name") ?? "", level));
                records.Add(new ZoneRecord
                {
                    Id = Key(element), Kind = "district", Sub = $"admin_level_{level}", Name = element.Tag("name"), Mapped = true,
                    Outer = outline.Outer, Inner = outline.Inner.Length > 0 ? outline.Inner : null, AreaM2 = Math.Round(outline.AreaM2), Tags = element.Tags,
                });
            }
            else if (element.Tag("place") is { } place)
            {
                records.Add(new ZoneRecord
                {
                    Id = Key(element), Kind = "place", Sub = place, Name = element.Tag("name"), Mapped = true, At = [element.Lat, element.Lon], Tags = element.Tags,
                });
            }
        }

        found.Named.AddRange(records.Select(record => record.Tags));
        written.Add(Layers.Write(into, "districts",
            "The town's administrative districts (admin_level 7-10) with their outlines, and its named places at a point each.",
            ["osm-districts"], records,
            new { levels = records.GroupBy(record => record.Sub ?? "").ToDictionary(g => g.Key, g => g.Count()) }));
        return districts;
    }

    public static string Key(Element element) => element.Key;

    /// <summary>A zone's kind and, where its tags say, what of that kind; null for an element that is no zone.</summary>
    static (string Kind, string? Sub)? Kind(Dictionary<string, string> tags)
    {
        string? Tag(string key) => tags.GetValueOrDefault(key);
        switch (Tag("amenity"))
        {
            case "parking": return ("parking", Tag("parking") ?? "unsaid");
            case "parking_space": return ("parking_space", Tag("parking_space"));
            case "parking_entrance": return ("parking_entrance", Tag("parking"));
            case "motorcycle_parking": return ("parking", "motorcycle");
            case "fuel" or "car_wash" or "charging_station" or "bus_station" or "taxi" or "ferry_terminal": return (Tag("amenity")!, null);
            case "school" or "kindergarten" or "college" or "university": return ("education", Tag("amenity"));
            case "hospital" or "clinic": return ("health", Tag("amenity"));
            case "marketplace": return ("market", null);
            case "townhall" or "police" or "fire_station" or "prison": return ("civic", Tag("amenity"));
            case "grave_yard": return ("cemetery", null);
        }

        if (Tag("parking") is { } parking) return ("parking", parking);
        if (Tag("harbour") is not null || Tag("industrial") == "port" || Tag("landuse") == "port") return ("port", Tag("industrial") ?? Tag("landuse"));
        if (Tag("military") is not null) return ("military", Tag("military"));
        if (Tag("aeroway") is { } aeroway) return ("airport", aeroway);
        if (Tag("man_made") is { } made) return (made == "bridge" ? "bridge" : made == "tunnel" ? "tunnel" : "pier", made);
        if (Tag("place") == "square" || (Tag("highway") is "pedestrian" or "footway" && Tag("area") == "yes")) return ("square", Tag("highway"));
        if (Tag("highway") is "services" or "rest_area") return ("services", Tag("highway"));
        if (Tag("highway") is "platform") return ("platform", null);
        if (Tag("highway") is not null && Tag("type") == "multipolygon") return ("square", Tag("highway"));
        if (Tag("tourism") is { } tourism) return ("leisure", tourism);
        if (Tag("leisure") is { } leisure) return ("leisure", leisure);
        if (Tag("natural") is "water" or "bay" || Tag("waterway") is not null || Tag("water") is not null) return ("water", Tag("water") ?? Tag("waterway") ?? Tag("natural"));
        if (Tag("natural") is "beach" or "sand" or "shingle") return ("beach", Tag("natural"));
        if (Tag("natural") is { } natural) return ("nature", natural);
        if (Tag("industrial") is { } industrial) return ("industrial", industrial);

        return Tag("landuse") switch
        {
            null => null,
            "garages" => ("garages", null),
            "industrial" => ("industrial", "industrial"),
            "railway" => ("railway", null),
            "construction" => ("construction", Tag("construction")),
            "residential" or "commercial" or "retail" => (Tag("landuse")!, Tag("landuse")),
            "allotments" => ("allotments", null),
            "farmland" or "farmyard" or "orchard" or "vineyard" or "meadow" or "greenhouse_horticulture" or "plant_nursery" or "animal_keeping" => ("agriculture", Tag("landuse")),
            "grass" or "village_green" or "recreation_ground" or "flowerbed" => ("green", Tag("landuse")),
            "forest" => ("nature", "forest"),
            "brownfield" or "greenfield" or "landfill" or "quarry" => ("waste_land", Tag("landuse")),
            "religious" => ("religious", null),
            "education" => ("education", "education"),
            "cemetery" => ("cemetery", null),
            "military" => ("military", null),
            var other => ("land", other),
        };
    }
}

/// <summary>What the zone layer found that later layers read.</summary>
internal sealed class ZonesFound
{
    /// <summary>The zone each road is one of the ways about, by road: its id, kind and what of that kind.</summary>
    public Dictionary<int, (string Id, string Kind, string? Sub)> ZoneOf { get; } = [];

    /// <summary>The land use under each road's middle, by road.</summary>
    public Dictionary<int, string> LandUseOf { get; } = [];

    public List<(Outline Outline, string Name, int Level)> Districts { get; set; } = [];

    /// <summary>Every bridge a mapper outlined (<c>man_made=bridge</c>): its id, name and outline.</summary>
    public List<(string Id, string? Name, Outline Outline)> Bridges { get; } = [];

    /// <summary>The tags of every zone and place an address may name as its street: squares, parks, districts and named places.</summary>
    public List<Dictionary<string, string>> Named { get; } = [];

    /// <summary>The outline of every paved place a car crosses as a surface — a car park, a forecourt, a square.</summary>
    public List<Outline> Paved { get; } = [];
}

internal sealed class ZoneRecord
{
    /// <summary><c>w123</c>, <c>r123</c> or <c>n123</c> for OSM's own; <c>lot&lt;way&gt;</c> for a lot derived from its aisles.</summary>
    public required string Id { get; init; }

    public required string Kind { get; init; }

    /// <summary>What of its kind: a car park's <c>parking</c> value, a land use's own, a leisure's.</summary>
    public string? Sub { get; init; }

    public string? Name { get; init; }

    /// <summary>Whether a mapper drew it, rather than it being derived.</summary>
    public required bool Mapped { get; init; }

    /// <summary>Lat, lon in 1e-7° of a zone OSM maps as a node.</summary>
    public int[]? At { get; init; }

    /// <summary>Outer rings, each lat, lon pairs in 1e-7°, closed on its first place.</summary>
    public int[][]? Outer { get; init; }

    public int[][]? Inner { get; init; }

    public double? AreaM2 { get; init; }

    /// <summary>The survey road ways most of which run inside it, by OSM id.</summary>
    public long[]? Roads { get; init; }

    /// <summary>The survey road ways crossing its edge: its ways in and out.</summary>
    public long[]? WaysIn { get; init; }

    /// <summary>For a car park, how many mapped parking spaces stand inside it.</summary>
    public int? Spaces { get; init; }

    public required Dictionary<string, string> Tags { get; init; }
}
