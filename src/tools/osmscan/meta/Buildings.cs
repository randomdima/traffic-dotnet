using System.Globalization;

namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>Every building on the map</b>: OSM's, outline and parts, with its levels, height and use, the roads and ways
/// that pass under it, and its entrances — and every footprint Microsoft's model traced off imagery that OSM does
/// not have, so a block OSM left empty is not empty.
/// </summary>
/// <remarks>
/// <para>
/// <b>A height is the most direct figure there is</b>: OSM's <c>height</c>; else its <c>building:levels</c> at
/// <see cref="LevelM"/> a level; else the model's own estimate where it matched the building. Which it was is said
/// beside it.
/// </para>
/// <para>
/// <b>A footprint is OSM's building where either stands on the other's centre</b>; the model's own height is then
/// lent to an OSM building that has none, and a footprint that matches no OSM building is kept as one of its own.
/// The model traced imagery from before the war, so a footprint OSM lacks may be a building since lost.
/// </para>
/// <para>
/// <b>A footprint OSM lacks is moved onto OSM's frame first.</b> The imagery each traced off stands a metre or few
/// off the other's, by a shift that changes across the town, so a footprint is moved by the median shift of the
/// footprints of like size within <see cref="ShiftReachM"/> that stand on OSM buildings — the town's median where
/// fewer than <see cref="ShiftPairs"/> do — and one that then stands on an OSM building is that building. One standing
/// on an OSM building or a street's carriageway for <see cref="OverlapShare"/> of its ground or more is not laid: OSM
/// says what is there, and the model traced a part of the building or the street's edge.
/// </para>
/// </remarks>
internal static class Buildings
{
    /// <summary>A storey's height, the figure OSM's 3D renderers (OSM Buildings, F4) give a level where no height is tagged.</summary>
    public const double LevelM = 3.0;

    const double CellM = 100;

    /// <summary>How far round a footprint the shift it is moved by is read, and the fewest pairs it is read off.</summary>
    const double ShiftReachM = 500;

    const int ShiftPairs = 10;

    /// <summary>The share of a footprint OSM lacks that may stand on an OSM building or a street before it is taken for part of one.</summary>
    public const double OverlapShare = 1.0 / 3;

    /// <summary>Half the widest carriageway: the farthest a place is looked for a street's line from.</summary>
    const double CarriagewayReachM = 20;

    /// <summary>A storey lower than this is a height or a level count mistyped; a home's storey higher than the other is.</summary>
    public const double LowestStoreyM = 2.0, HighestHomeStoreyM = 6;

    /// <summary>The uses a building is lived in by, whose storeys are a home's.</summary>
    public static readonly HashSet<string> Homes = ["apartments", "house", "residential", "detached", "semidetached_house", "terrace", "dormitory", "bungalow"];

    public static BuildingsFound Lay(Town town, ZonesFound zones, WalkFound walk, Flags flags, string into, List<Written> written)
    {
        var found = new BuildingsFound();
        var buildings = new List<(Element Element, Outline Outline)>();
        foreach (var element in Element.Read(town.Fetched.Buildings))
        {
            if (Outline.Of(element, town.Plane) is { } outline) buildings.Add((element, outline));
        }

        var grid = new Grid(CellM);
        var corners = new Dictionary<(int, int), int>();
        for (var at = 0; at < buildings.Count; at++)
        {
            grid.Add(at, buildings[at].Outline.Bounds);
            foreach (var ring in buildings[at].Outline.Outer)
            {
                for (var k = 0; k + 1 < ring.Length; k += 2) corners.TryAdd((ring[k], ring[k + 1]), at);
            }
        }

        int? Under(Pt at)
        {
            var near = new HashSet<int>();
            grid.Near(Box.Empty.With(at), near);
            int? best = null;
            foreach (var candidate in near)
            {
                var (element, outline) = buildings[candidate];
                if (element.Tags.ContainsKey("building:part") && !element.Tags.ContainsKey("building")) continue;
                if (outline.Holds(at) && (best is null || outline.AreaM2 < buildings[best.Value].Outline.AreaM2)) best = candidate;
            }

            return best;
        }

        var passages = new Dictionary<int, List<long>>();
        var through = new HashSet<(int Road, int Building)>();
        for (var road = 0; road < town.Roads.Length; road++)
        {
            var way = town.Roads[road];
            var line = town.RoadLineM[road];
            var covered = way.Tag("tunnel") is "building_passage" or "yes" || way.Tag("covered") is "yes" or "arcade" or "colonnade" || way.Tag("layer") is "-1" or "-2";
            var raised = way.Tag("bridge") is not null and not "no" || (int.TryParse(way.Tag("layer"), out var layer) && layer > 0);
            for (var segment = 0; segment + 1 < line.Length; segment++)
            {
                var middle = (line[segment] + line[segment + 1]) * 0.5;
                if (Under(middle) is not { } building) continue;

                if (covered)
                {
                    (passages.TryGetValue(building, out var list) ? list : passages[building] = []).Add(way.Id);
                    found.Over[way.Id] = Key(buildings[building].Element);
                }
                else if (!raised && town.Highway(road) is not ("service" or "track") && through.Add((road, building)))
                {
                    flags.Raise("road_through_building", town, middle, $"{town.Highway(road)} runs through a building with no tunnel, covered or layer tag",
                        $"w{way.Id}", Key(buildings[building].Element));
                }
            }
        }

        foreach (var record in walk.Records)
        {
            if (record.Tags.GetValueOrDefault("tunnel") != "building_passage" && record.Tags.GetValueOrDefault("covered") is not ("yes" or "arcade")) continue;

            var line = town.Plane.Line(record.Line);
            if (Under(Shape.Along(line, Shape.Length(line) / 2)) is { } building)
            {
                (passages.TryGetValue(building, out var list) ? list : passages[building] = []).Add(record.Way);
                found.Over[record.Way] = Key(buildings[building].Element);
            }
        }

        var entrances = new Dictionary<int, List<EntranceRecord>>();
        foreach (var node in Element.Read(town.Fetched.Control, 'n'))
        {
            if (!node.Tags.TryGetValue("entrance", out var kind) && !node.Tags.TryGetValue("door", out kind)) continue;
            if (!corners.TryGetValue((node.Lat, node.Lon), out var building)) continue;

            (entrances.TryGetValue(building, out var list) ? list : entrances[building] = []).Add(new EntranceRecord
            {
                Node = node.Id,
                Kind = kind,
                Ref = node.Tag("ref") ?? node.Tag("addr:flats"),
                At = [node.Lat, node.Lon],
            });
        }

        int? Match(Pt[] line)
        {
            if (Under(Shape.Centroid(line)) is { } under) return under;

            var near = new HashSet<int>();
            grid.Near(Box.Of(line), near);
            return near.Where(candidate => buildings[candidate].Element.Tags.ContainsKey("building") && Shape.Inside(Shape.Centroid(buildings[candidate].Outline.RingsM[0]), [line]))
                .Select(candidate => (int?)candidate).FirstOrDefault();
        }

        // Pass one: the footprints on an OSM building, and how far each of like size stands off it.
        var ml = town.Fetched.MlBuildingsAt is { } kept ? MlBuildings.Read(kept, town.Sources) : [];
        var lent = new Dictionary<int, double>();
        var loose = new List<(Pt[] Line, double HeightM, double Confidence)>();
        var pairs = new List<(Pt At, Pt Offset)>();
        foreach (var (ring, heightM, confidence) in ml)
        {
            var line = town.Plane.Line(ring);
            if (Match(line) is not { } building)
            {
                loose.Add((line, heightM, confidence));
                continue;
            }

            if (heightM > 0) lent[building] = heightM;
            var own = buildings[building].Outline;
            var ratio = Math.Abs(Shape.SignedArea(line)) / Math.Max(1, own.AreaM2);
            if (ratio is >= 2.0 / 3 and <= 1.5) pairs.Add((Shape.Centroid(own.RingsM[0]), Shape.Centroid(line) - Shape.Centroid(own.RingsM[0])));
        }

        // Pass two: a footprint OSM lacks, moved onto OSM's frame by the shift its neighbours on OSM buildings stand off by.
        var shifts = new Grid(ShiftReachM);
        for (var at = 0; at < pairs.Count; at++) shifts.Add(at, Box.Empty.With(pairs[at].At));
        var everywhere = pairs.Count > 0 ? new Pt(Median(pairs.Select(pair => pair.Offset.X)), Median(pairs.Select(pair => pair.Offset.Y))) : default;
        var records = new List<BuildingRecord>(buildings.Count + loose.Count);
        var (mlOnly, alignedOnto, overlapping, onStreets) = (0, 0, 0, 0);
        var nearPairs = new HashSet<int>();
        foreach (var (line, heightM, confidence) in loose)
        {
            var centre = Shape.Centroid(line);
            nearPairs.Clear();
            shifts.Near(Box.Empty.With(centre).Grown(ShiftReachM), nearPairs);
            var local = nearPairs.Where(at => (pairs[at].At - centre).Length <= ShiftReachM).Select(at => pairs[at].Offset).ToList();
            var shift = local.Count >= ShiftPairs ? new Pt(Median(local.Select(offset => offset.X)), Median(local.Select(offset => offset.Y))) : everywhere;
            int[] ring = [.. line.SelectMany(at => town.Plane.Degrees(at - shift))];
            var moved = town.Plane.Line(ring);
            if (Match(moved) is { } building)
            {
                if (heightM > 0) lent.TryAdd(building, heightM);
                alignedOnto++;
                continue;
            }

            // OSM's building or street is where the footprint stands; the model traced a part of one, or the street's edge.
            if (Covered(moved, at => Under(at) is not null) >= OverlapShare)
            {
                overlapping++;
                continue;
            }

            if (Covered(moved, at => OnCarriageway(town, at)) >= OverlapShare)
            {
                onStreets++;
                continue;
            }

            mlOnly++;
            records.Add(new BuildingRecord
            {
                Id = $"ml{mlOnly}",
                Source = "ml",
                Outer = [ring],
                AreaM2 = Math.Round(Math.Abs(Shape.SignedArea(moved))),
                HeightM = heightM > 0 ? Math.Round(heightM, 1) : null,
                HeightFrom = heightM > 0 ? "ml" : null,
                Confidence = confidence >= 0 ? Math.Round(confidence, 2) : null,
                ShiftM = [Math.Round(-shift.X, 1), Math.Round(shift.Y, 1)],
                Tags = [],
            });
        }

        var (streets, renamed) = Names.Streets(town.Roads.Select(way => way.Tags).Concat(walk.Records.Select(way => way.Tags)).Concat(zones.Named));
        for (var at = 0; at < buildings.Count; at++)
        {
            var (element, outline) = buildings[at];
            var tags = element.Tags;
            var levels = Number(tags.GetValueOrDefault("building:levels"));
            var (heightM, from) = Metres(tags.GetValueOrDefault("height")) is { } tagged ? (tagged, "height")
                : levels is { } counted ? (counted * LevelM, "levels")
                : lent.TryGetValue(at, out var model) ? (model, "ml")
                : ((double?)null, (string?)null);
            var street = tags.GetValueOrDefault("addr:street");
            var number = tags.GetValueOrDefault("addr:housenumber");
            var centre = Shape.Centroid(outline.RingsM[0]);
            string? streetNow = null;
            if (street is not null && !streets.Contains(Names.Street(street)))
            {
                if (renamed.TryGetValue(Names.Street(street), out var now))
                {
                    streetNow = now;
                    flags.Raise("address_names_old_street", town, centre, $"addr:street {street}, now {now}", Key(element));
                }
                else
                {
                    flags.Raise("address_street_not_drawn", town, centre, $"addr:street {street} names no street, square or place OSM draws", Key(element));
                }
            }

            if (Metres(tags.GetValueOrDefault("height")) is { } storeysM && levels is >= 1)
            {
                var storeyM = (storeysM - (Metres(tags.GetValueOrDefault("min_height")) ?? 0)) / levels.Value;
                if (storeyM < LowestStoreyM || (storeyM > HighestHomeStoreyM && Homes.Contains(tags.GetValueOrDefault("building") ?? "")))
                {
                    flags.Raise("height_and_levels_disagree", town, centre, string.Create(CultureInfo.InvariantCulture, $"height {storeysM} m over {levels} levels makes a storey {storeyM:F1} m; the height is taken"), Key(element));
                }
            }

            records.Add(new BuildingRecord
            {
                Id = Key(element),
                Source = "osm",
                Part = tags.ContainsKey("building:part") && !tags.ContainsKey("building") ? true : null,
                Use = tags.GetValueOrDefault("building") ?? tags.GetValueOrDefault("building:part"),
                Name = tags.GetValueOrDefault("name"),
                Address = number is null ? null : street is null ? number : $"{street}, {number}",
                StreetNow = streetNow,
                Outer = outline.Outer,
                Inner = outline.Inner.Length > 0 ? outline.Inner : null,
                AreaM2 = Math.Round(outline.AreaM2),
                Levels = levels,
                HeightM = heightM is { } h ? Math.Round(h, 1) : null,
                HeightFrom = from,
                MinHeightM = Metres(tags.GetValueOrDefault("min_height")) ?? (Number(tags.GetValueOrDefault("building:min_level")) is { } min ? min * LevelM : null),
                Passages = passages.TryGetValue(at, out var under) ? [.. under.Distinct()] : null,
                Entrances = entrances.TryGetValue(at, out var doors) ? [.. doors] : null,
                Tags = tags,
            });
        }

        foreach (var (element, outline) in buildings)
        {
            if (element.Tags.ContainsKey("building")) found.Outlines.Add(outline);
        }

        var osm = records.Where(record => record.Source == "osm" && record.Part is null).ToArray();
        written.Add(Layers.Write(into, "buildings",
            "Every building: OSM's outline and parts with levels, height (and where the height came from), use, address, the ways passing under it and its entrances; and every machine-traced footprint OSM does not have.",
            ["osm-buildings", "osm-control", "ml-buildings"], records,
            new
            {
                osm = osm.Length,
                parts = records.Count(record => record.Part == true),
                mlOnly,
                mlFootprintsRead = ml.Count,
                mlOnOsmOnceAligned = alignedOnto,
                mlOverlappingOsm = overlapping,
                mlOnStreets = onStreets,
                mlShiftPairs = pairs.Count,
                mlMovedAtMedian = string.Create(CultureInfo.InvariantCulture,
                    $"{Math.Abs(everywhere.X):F1} m {(everywhere.X > 0 ? "west" : "east")}, {Math.Abs(everywhere.Y):F1} m {(everywhere.Y > 0 ? "north" : "south")}"),
                heightFrom = osm.GroupBy(record => record.HeightFrom ?? "none").ToDictionary(g => g.Key, g => g.Count()),
                withAddress = osm.Count(record => record.Address is not null),
                withPassages = osm.Count(record => record.Passages is not null),
                withEntrances = osm.Count(record => record.Entrances is not null),
                uses = osm.GroupBy(record => record.Use ?? "").OrderByDescending(g => g.Count()).Take(25).ToDictionary(g => g.Key, g => g.Count()),
                areaKm2 = Math.Round(osm.Sum(record => record.AreaM2) / 1e6, 2),
            }));
        return found;
    }

    static string Key(Element element) => element.Key;

    /// <summary>The share of a ring's ground a test holds at, sampled on a grid over it.</summary>
    public static double Covered(Pt[] ring, Func<Pt, bool> holds)
    {
        const int Across = 6;
        var box = Box.Of(ring);
        var (inside, held) = (0, 0);
        for (var i = 0; i < Across; i++)
        {
            for (var j = 0; j < Across; j++)
            {
                var at = new Pt(box.MinX + ((box.MaxX - box.MinX) * (i + 0.5) / Across), box.MinY + ((box.MaxY - box.MinY) * (j + 0.5) / Across));
                if (!Shape.Inside(at, [ring])) continue;

                inside++;
                if (holds(at)) held++;
            }
        }

        return inside == 0 ? 0 : (double)held / inside;
    }

    /// <summary>
    /// Whether a place is on a street's carriageway at ground level: within half its width of the line of a road that
    /// is neither a service road nor a track, nor on a bridge, in a tunnel or under cover.
    /// </summary>
    public static bool OnCarriageway(Town town, Pt at) =>
        town.NearestRoad(at, CarriagewayReachM, road => town.Highway(road) is not ("service" or "track") && Levels.Layer(town.Roads[road].Tags).Layer == 0 && Levels.Structure(town.Roads[road].Tags) is null)
            is { } near && near.OffM <= town.Roads[near.Road].Carriageway!.WidthM / 2;

    static double Median(IEnumerable<double> values)
    {
        var sorted = values.Order().ToArray();
        return sorted.Length == 0 ? 0 : sorted[sorted.Length / 2];
    }

    public static double? Number(string? value) =>
        value is not null && double.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && number >= 0 ? number : null;

    /// <summary>A length as OSM writes it — <c>12</c>, <c>12.5 m</c>, <c>12m</c> — in metres; feet and inches are not read.</summary>
    public static double? Metres(string? value)
    {
        if (value is null) return null;

        var text = value.Trim();
        if (text.EndsWith(" m", StringComparison.Ordinal)) text = text[..^2];
        else if (text.EndsWith('m')) text = text[..^1];
        return Number(text);
    }
}

/// <summary>What the building layer found that later layers read.</summary>
internal sealed class BuildingsFound
{
    /// <summary>The building over each way that passes under one, by the way's OSM id.</summary>
    public Dictionary<long, string> Over { get; } = [];

    /// <summary>Every OSM building's outline, its parts left out.</summary>
    public List<Outline> Outlines { get; } = [];
}

internal sealed class BuildingRecord
{
    /// <summary><c>w123</c> or <c>r123</c> for OSM's; <c>ml&lt;n&gt;</c> for a machine-traced footprint.</summary>
    public required string Id { get; init; }

    /// <summary><c>osm</c> or <c>ml</c>.</summary>
    public required string Source { get; init; }

    /// <summary>Whether it is a <c>building:part</c> of a building rather than a building.</summary>
    public bool? Part { get; init; }

    /// <summary>Its <c>building</c> value — <c>apartments</c>, <c>house</c>, <c>yes</c>.</summary>
    public string? Use { get; init; }

    public string? Name { get; init; }

    public string? Address { get; init; }

    /// <summary>The street's present name, where the address names it by one it has since dropped (<c>old_name</c>).</summary>
    public string? StreetNow { get; init; }

    /// <summary>Outer rings, each lat, lon pairs in 1e-7°, closed on its first place.</summary>
    public required int[][] Outer { get; init; }

    public int[][]? Inner { get; init; }

    public required double AreaM2 { get; init; }

    public double? Levels { get; init; }

    public double? HeightM { get; init; }

    /// <summary><c>height</c>, <c>levels</c> (at <see cref="Buildings.LevelM"/> a level) or <c>ml</c>.</summary>
    public string? HeightFrom { get; init; }

    public double? MinHeightM { get; init; }

    /// <summary>The model's confidence in a machine-traced footprint, where it gave one.</summary>
    public double? Confidence { get; init; }

    /// <summary>How far a machine-traced footprint was moved onto OSM's frame, east and north.</summary>
    public double[]? ShiftM { get; init; }

    /// <summary>The roads and ways for feet passing under it, by OSM id.</summary>
    public long[]? Passages { get; init; }

    public EntranceRecord[]? Entrances { get; init; }

    public required Dictionary<string, string> Tags { get; init; }
}

internal sealed class EntranceRecord
{
    public required long Node { get; init; }

    /// <summary>Its <c>entrance</c> or <c>door</c> value — <c>main</c>, <c>staircase</c>, <c>garage</c>, <c>yes</c>.</summary>
    public required string Kind { get; init; }

    public string? Ref { get; init; }

    public required int[] At { get; init; }
}
