using System.Globalization;
using System.Text.Json;

namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>Where the sources describe the same things, how far they agree</b>: OSM's buildings and the machine-traced
/// footprints, OSM's stops and routes and the city's timetable, the timetable's lines and the roads and track they
/// are driven along, the height models at the shore and against each other, a road's lanes and surface against the
/// width read off imagery and the roads traced off it OSM lacks, Mapillary's sightings against OSM's signals,
/// crossings and signs, Osmose's issues and the OSM they were raised against, how long since each road was edited,
/// and OSM against itself — a crossing's node and its way, a road's pavement tags and the pavements drawn beside it,
/// its width tag and its measured surface, a building's height and its levels, an address and the street it names.
/// </summary>
/// <remarks>
/// Each is measured off the sources as kept and the layers as written, so a reading says how far the sources agree
/// and what the layers made of it, never what the layers were told to make.
/// </remarks>
internal static class Agreement
{
    /// <summary>How far apart a timetable line is sampled, and how far off the roads it is still on them.</summary>
    const double LineStepM = 20, LineOnM = 15, LineReachM = 60;

    /// <summary>How far round a footprint pair its neighbours' shift is read, and the fewest it is read off: <see cref="Buildings"/>'.</summary>
    const double ShiftReachM = 500;

    const int ShiftPairs = 10;

    public static List<Finding> Measure(Dataset data)
    {
        var sources = new Sources(data.Root, data.City, data.Survey, data.Plane, refetch: false);
        var town = new Town(data.Survey, data.Plane, new Fetched(), sources);
        var feed = Kept(data, name => name == "gtfs.zip") is [var kept] ? Gtfs.Read(kept) : null;
        return
        [
            Footprints(data, sources, town), Feed(feed), Stops(data, feed), Routes(data, feed), Lines(data, town, feed), Shore(data), Terrain(data), OsmoseIssues(data),
            CrossingKinds(data), Pavements(data), Widths(data), DetectedWidths(data), DetectedStretches(data), Sightings(data), Edits(data), Storeys(data), Addresses(data), Signals(data),
        ];
    }

    static Finding Footprints(Dataset data, Sources sources, Town town)
    {
        var finding = new Finding("Buildings: OSM's outlines and Microsoft's machine-traced footprints");
        var kept = Kept(data, name => name.StartsWith("ml-buildings-", StringComparison.Ordinal) && name.EndsWith(".csv.gz", StringComparison.Ordinal));
        if (kept.Length == 0) return finding.Read("no footprints this run");

        var ml = MlBuildings.Read(kept, sources);
        var whole = data.Buildings.Where(building => building.Source == "osm" && building.Part is null)
            .Select(building => building.Outer.Concat(building.Inner ?? []).Select(data.Plane.Line).ToArray()).ToList();
        var osm = whole.Select(rings => rings[0]).ToList();
        var areas = osm.Select(ring => Math.Abs(Shape.SignedArea(ring))).ToArray();
        var centres = osm.Select(Centroid).ToArray();
        var grid = new Grid(100);
        for (var at = 0; at < osm.Count; at++) grid.Add(at, Box.Of(whole[at].SelectMany(ring => ring).ToArray()));

        var had = new HashSet<int>();
        var offsets = new List<(Pt Offset, Pt At)>();
        var ratios = new List<double>();
        var near = new HashSet<int>();
        foreach (var (ring, _, _) in ml)
        {
            var line = data.Plane.Line(ring);
            var centre = Centroid(line);
            near.Clear();
            grid.Near(Box.Of(line), near);
            int? match = null;
            foreach (var candidate in near)
            {
                if (Shape.Inside(centre, [osm[candidate]]) && (match is null || areas[candidate] < areas[match.Value])) match = candidate;
            }

            match ??= near.Where(candidate => Shape.Inside(centres[candidate], [line])).Select(candidate => (int?)candidate).FirstOrDefault();
            if (match is not { } own) continue;

            had.Add(own);
            var ratio = Math.Abs(Shape.SignedArea(line)) / Math.Max(1, areas[own]);
            ratios.Add(ratio);
            if (ratio is >= 2.0 / 3 and <= 1.5) offsets.Add((centre - centres[own], centres[own]));
        }

        var laid = data.Buildings.Where(building => building.Source == "ml").ToList();
        bool OnOsm(Pt at)
        {
            near.Clear();
            grid.Near(Box.Empty.With(at), near);
            return near.Any(candidate => Shape.Inside(at, whole[candidate]));
        }

        foreach (var building in laid)
        {
            var ring = data.Plane.Line(building.Outer[0]);
            finding.Ask(Buildings.Covered(ring, OnOsm) < Buildings.OverlapShare && Buildings.Covered(ring, at => Buildings.OnCarriageway(town, at)) < Buildings.OverlapShare,
                () => $"{building.Id} on an OSM building or a street");
        }

        finding.Read($"{ml.Count:N0} footprints read; {ratios.Count:N0} ({Share(ratios.Count, ml.Count)}) stand on an OSM building as traced and are taken as it; {laid.Count:N0} are laid as buildings of their own, moved onto OSM's frame, of which {finding.Failed:N0} stand a third or more on an OSM building or a street");
        finding.Read($"{had.Count:N0} of {osm.Count:N0} OSM buildings ({Share(had.Count, osm.Count)}) have a footprint; one that has none was built after the imagery, or the model missed it");
        if (offsets.Count > 0)
        {
            var lengths = offsets.Select(pair => pair.Offset.Length).ToList();
            finding.Read(string.Create(CultureInfo.InvariantCulture,
                $"as traced, a footprint of like size stands off its OSM building by {Median(lengths):F1} m at the median, {Quantile(lengths, 0.9):F1} m at the 90th percentile — {Median(offsets.Select(pair => pair.Offset.X)):F1} m east and {-Median(offsets.Select(pair => pair.Offset.Y)):F1} m north at the median, over {offsets.Count:N0} pairs"));

            var cells = new Grid(ShiftReachM);
            for (var at = 0; at < offsets.Count; at++) cells.Add(at, Box.Empty.With(offsets[at].At));
            var residual = new List<double>();
            for (var at = 0; at < offsets.Count; at++)
            {
                near.Clear();
                cells.Near(Box.Empty.With(offsets[at].At).Grown(ShiftReachM), near);
                var local = near.Where(other => other != at && (offsets[other].At - offsets[at].At).Length <= ShiftReachM).Select(other => offsets[other].Offset).ToList();
                if (local.Count < ShiftPairs) continue;

                residual.Add((offsets[at].Offset - new Pt(Median(local.Select(offset => offset.X)), Median(local.Select(offset => offset.Y)))).Length);
            }

            finding.Read(string.Create(CultureInfo.InvariantCulture,
                $"moved by the median shift of its neighbours within {ShiftReachM:F0} m, itself left out, a footprint stands {Median(residual):F1} m off its OSM building at the median, {Quantile(residual, 0.9):F1} m at the 90th percentile, over {residual.Count:N0} pairs — the shift a footprint OSM lacks is moved by"));
            foreach (var district in data.Districts.Where(district => district.Kind == "district" && district.Sub == "admin_level_10" && district.Outer is not null))
            {
                var rings = district.Outer!.Select(data.Plane.Line).ToArray();
                var inside = offsets.Where(pair => Shape.Inside(pair.At, rings)).Select(pair => pair.Offset).ToList();
                if (inside.Count >= 50)
                {
                    finding.Read(string.Create(CultureInfo.InvariantCulture,
                        $"{district.Name}: median shift as traced {Median(inside.Select(offset => offset.X)):F1} m east, {-Median(inside.Select(offset => offset.Y)):F1} m north, over {inside.Count:N0} pairs"));
                }
            }
        }

        finding.Read(string.Create(CultureInfo.InvariantCulture, $"a footprint's area over its OSM building's: {Median(ratios):F2} at the median"));
        return finding;
    }

    static Finding Feed(Timetable? feed)
    {
        var finding = new Finding("Timetable: the city's feed against itself");
        if (feed is null) return finding.Read("no timetable this run");

        var running = feed.Routes.Count(route => route.Directions.Any(direction => direction.Days.Count > 0));
        var weekendOnly = feed.Routes.Count(route => route.Directions.Any(direction => direction.Days.Count > 0) && route.Directions.All(direction => !direction.Days.ContainsKey("weekday")));
        finding.Read($"its calendar is in force {feed.InForce}; {feed.Routes.Count:N0} routes, {running:N0} running on some day, {weekendOnly:N0} of those at weekends only, {feed.Routes.Count - running:N0} on none and not laid");
        finding.Read($"read once though printed more, or not read: {(feed.Dropped.Count == 0 ? "nothing" : string.Join(", ", feed.Dropped.OrderByDescending(pair => pair.Value).Select(pair => $"{pair.Key} {pair.Value:N0}")))}");
        return finding;
    }

    static Finding Stops(Dataset data, Timetable? feed)
    {
        var finding = new Finding("Stops: OSM's and the city's timetable");
        if (feed is null) return finding.Read("no timetable this run");

        var laidOn = new Dictionary<string, StopRecord>();
        foreach (var stop in data.Stops) foreach (var id in stop.Timetable ?? []) laidOn.TryAdd(id, stop);

        // What OSM alone says a stop serves: its tags, and OSM's relations calling there.
        var osmCalls = new Dictionary<string, HashSet<string>>();
        foreach (var route in data.Routes.Where(route => route.Source == "osm"))
        {
            foreach (var stop in route.Stops.Concat(route.Platforms)) (osmCalls.TryGetValue(stop, out var set) ? set : osmCalls[stop] = []).Add(route.Mode);
        }

        var modesAt = new Dictionary<string, HashSet<string>>();
        foreach (var route in feed.Routes)
        {
            foreach (var stop in route.Directions.Where(direction => direction.Days.Count > 0).SelectMany(direction => direction.Stops)) (modesAt.TryGetValue(stop, out var set) ? set : modesAt[stop] = []).Add(route.Mode);
        }

        var (own, unlaid, silent) = (0, 0, 0);
        var apart = new List<double>();
        var (named, sameName) = (0, 0);
        foreach (var (id, modes) in modesAt)
        {
            if (!laidOn.TryGetValue(id, out var stop))
            {
                unlaid++;
                continue;
            }

            if (stop.Source == "timetable")
            {
                own++;
                continue;
            }

            var timetabled = feed.Stops[id];
            apart.Add((data.Plane.At(timetabled.Lat, timetabled.Lon) - data.Plane.At(stop.At[0], stop.At[1])).Length);
            var osmModes = stop.Modes.Concat(osmCalls.GetValueOrDefault(stop.Id) ?? []).Select(mode => mode is "minibus" or "share_taxi" ? "bus" : mode).ToHashSet();
            if (stop.Tags.GetValueOrDefault("railway") == "tram_stop") osmModes.Add("tram");
            if (stop.Tags.GetValueOrDefault("highway") == "bus_stop") osmModes.Add("bus");
            if (osmModes.Count == 0) silent++;
            else finding.Ask(osmModes.Overlaps(modes), () => $"{id} ({string.Join("/", modes)}) on {stop.Id} ({string.Join("/", osmModes)})");
            if (timetabled.Name is null || stop.Name is null) continue;

            named++;
            if (Names.Same(timetabled.Name, stop.Name)) sameName++;
        }

        finding.Read($"{modesAt.Count:N0} timetable stops served on some day: {apart.Count:N0} ({Share(apart.Count, modesAt.Count)}) laid on an OSM stop serving their modes, {own:N0} laid as stops of their own where OSM has none, {unlaid:N0} not laid");
        finding.Read(string.Create(CultureInfo.InvariantCulture,
            $"a timetable stop stands {Median(apart):F1} m from the OSM stop it is laid on at the median, {Quantile(apart, 0.9):F1} m at the 90th percentile; the layer keeps OSM's place"));
        finding.Read($"{finding.Failed:N0} of those ({Share(finding.Failed, finding.Asked)}) are laid on an OSM stop that says it serves other modes only; {silent:N0} on one that says no mode at all");
        finding.Read($"{sameName:N0} of {named:N0} named pairs ({Share(sameName, named)}) share a name, one holding the other; the layer keeps OSM's name");
        return finding;
    }

    static Finding Routes(Dataset data, Timetable? feed)
    {
        var finding = new Finding("Routes: OSM's relations and the city's timetable");
        if (feed is null) return finding.Read("no timetable this run");

        string[] city = ["tram", "trolleybus", "bus", "minibus", "share_taxi"];
        var laid = data.Routes.Where(route => route.Timetable is not null).GroupBy(route => route.Timetable!.Id).ToDictionary(group => group.Key, group => group.ToList());
        var (routes, onOsm, own) = (0, 0, 0);
        foreach (var route in feed.Routes.Where(route => route.Directions.Any(direction => direction.Days.Count > 0)))
        {
            routes++;
            var on = laid.GetValueOrDefault(route.Id) ?? [];
            onOsm += on.Count(record => record.Source == "osm");
            own += on.Count(record => record.Source == "timetable");
            foreach (var direction in route.Directions.Where(direction => direction.Days.Count > 0))
            {
                finding.Ask(on.Count(record => record.Timetable!.Direction == direction.Direction) == 1, () => $"{route.Mode} {route.Ref} towards {direction.Headsign ?? direction.Direction}");
            }
        }

        var osmOnly = data.Routes.Where(route => route.Source == "osm" && city.Contains(route.Mode) && route.WaysOnMap > 0 && route.Timetable is null)
            .Select(route => Transit.Label(route.Mode, route.Ref, route.Name, route.Id)).Distinct().Order(StringComparer.Ordinal).ToArray();
        finding.Read($"{routes:N0} timetable routes run on some day, in {finding.Asked:N0} directions: {onOsm:N0} laid on OSM relations of their mode and ref, {own:N0} as routes of their own down the roads their line runs along — each raised in the quality layer — and {finding.Failed:N0} laid other than once");
        finding.Read($"{osmOnly.Length:N0} OSM routes over the town carry no timetable — the timetable runs them on no day, or not in the direction OSM draws: {string.Join(", ", osmOnly.Take(30))}{(osmOnly.Length > 30 ? ", …" : "")}");
        return finding;
    }

    static Finding Lines(Dataset data, Town town, Timetable? feed)
    {
        var finding = new Finding("Timetable lines: how far they run from the roads and track OSM draws");
        if (feed is null) return finding.Read("no timetable this run");

        var track = data.Tracks.Where(rail => rail.Railway == "tram").Select(rail => data.Plane.Line(rail.Line)).ToList();
        var segments = new List<(int Line, int At)>();
        var grid = new Grid(LineReachM);
        for (var line = 0; line < track.Count; line++)
        {
            for (var at = 0; at + 1 < track[line].Length; at++)
            {
                grid.Add(segments.Count, Box.Empty.With(track[line][at]).With(track[line][at + 1]));
                segments.Add((line, at));
            }
        }

        var off = new Dictionary<string, List<double>>();
        var near = new HashSet<int>();
        foreach (var route in feed.Routes)
        {
            foreach (var shape in route.Directions.Where(direction => direction.Days.Count > 0).Select(direction => direction.Shape).OfType<int[]>())
            {
                var line = data.Plane.Line(shape);
                var lengthM = Shape.Length(line);
                var list = off.TryGetValue(route.Mode, out var held) ? held : off[route.Mode] = [];
                for (var alongM = 0.0; alongM <= lengthM; alongM += LineStepM)
                {
                    var at = Shape.Along(line, alongM);
                    if (!data.Plane.OnMap(at)) continue;

                    double offM;
                    if (route.Mode == "tram")
                    {
                        near.Clear();
                        grid.Near(Box.Empty.With(at).Grown(LineReachM), near);
                        offM = near.Select(segment => Shape.Nearest(at, track[segments[segment].Line].AsSpan(segments[segment].At, 2)).OffM).DefaultIfEmpty(LineReachM).Min();
                    }
                    else
                    {
                        offM = town.NearestRoad(at, LineReachM)?.OffM ?? LineReachM;
                    }

                    list.Add(Math.Min(offM, LineReachM));
                    finding.Ask(offM <= LineOnM, () => string.Create(CultureInfo.InvariantCulture, $"{route.Mode} {route.Ref} {offM:F0} m off at {string.Join(",", data.Plane.Degrees(at))}"));
                }
            }
        }

        foreach (var (mode, list) in off.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            finding.Read(string.Create(CultureInfo.InvariantCulture,
                $"{mode}: {list.Count:N0} places {LineStepM:F0} m apart, {Median(list):F1} m off {(mode == "tram" ? "the track" : "a road")} at the median, {Quantile(list, 0.95):F1} m at the 95th percentile, {Share(list.Count(value => value > LineOnM), list.Count)} more than {LineOnM:F0} m"));
        }

        return finding;
    }

    static Finding Shore(Dataset data)
    {
        var finding = new Finding("Height models: the ground at the shore");
        var terrainAt = Kept(data, name => name == Dem.TerrainName);
        var surfaceAt = Kept(data, name => name.StartsWith("dem-", StringComparison.Ordinal));
        if (terrainAt.Length == 0 && surfaceAt.Length == 0) return finding.Read("no height model this run");

        // Asked of the model the roads' heights are read off; the other only read.
        var models = new List<(string Name, Dem Model)>();
        if (terrainAt.Length > 0) models.Add(("terrain", new Dem(string.Join(';', terrainAt))));
        if (surfaceAt.Length > 0) models.Add(("surface", new Dem(string.Join(';', surfaceAt))));
        foreach (var (name, model) in models)
        {
            var heights = new List<double>();
            var asked = name == models[0].Name;
            foreach (var way in data.Survey.Ways.Where(way => way.Tag("natural") == "coastline"))
            {
                foreach (var node in way.Nodes)
                {
                    if (!data.Plane.OnMap(data.Plane.At(data.Survey.Nodes.Lat[node], data.Survey.Nodes.Lon[node]))) continue;

                    var heightM = model.HeightM(data.Survey.Nodes.Lat[node], data.Survey.Nodes.Lon[node]);
                    if (double.IsNaN(heightM)) continue;

                    heights.Add(heightM);
                    if (asked) finding.Ask(heightM <= 5, () => string.Create(CultureInfo.InvariantCulture, $"n{data.Survey.Nodes.Id[node]} {heightM:F1} m"));
                }
            }

            finding.Read(string.Create(CultureInfo.InvariantCulture,
                $"{name}: {heights.Count:N0} coastline nodes on the map read {Median(heights):F1} m at the median ({Quantile(heights, 0.1):F1} m to {Quantile(heights, 0.9):F1} m, 10th to 90th percentile); {Share(heights.Count(value => value > 5), heights.Count)} read over 5 m, a cliff or a quay's edge a post away; one over the sea reads none"));
        }

        return finding;
    }

    static Finding OsmoseIssues(Dataset data)
    {
        var finding = new Finding("Osmose's issues against the OSM the layers were read off");
        var held = new Dictionary<string, Dictionary<string, string>>();
        foreach (var way in data.SurveyRoads.Values) held[$"w{way.Id}"] = way.Tags;
        foreach (var (node, tags) in data.NodeTags) held[$"n{data.Survey.Nodes.Id[node]}"] = tags;
        foreach (var way in data.Walk) held[$"w{way.Way}"] = way.Tags;
        foreach (var rail in data.Tracks) held[$"w{rail.Way}"] = rail.Tags;
        foreach (var item in data.Zones.Concat(data.Districts)) held.TryAdd(item.Id, item.Tags);
        foreach (var item in data.Buildings.Where(building => building.Source == "osm")) held.TryAdd(item.Id, item.Tags);
        foreach (var item in data.Places.Concat(data.Furniture).Concat(data.Barriers).Concat(data.Unbuilt)) held.TryAdd(item.Id, item.Tags);
        foreach (var crossing in data.Crossings.Where(crossing => crossing.Node is not null)) held.TryAdd($"n{crossing.Node}", crossing.Tags);
        foreach (var stop in data.Stops.Where(stop => stop.Source == "osm")) held.TryAdd(stop.Id, stop.Tags);

        var (issues, unheld) = (0, 0);
        var moments = new List<string>();
        foreach (var path in Kept(data, name => name.StartsWith("osmose-", StringComparison.Ordinal)))
        {
            using var answer = JsonDocument.Parse(File.ReadAllBytes(path));
            foreach (var feature in answer.RootElement.GetProperty("features").EnumerateArray())
            {
                var properties = feature.GetProperty("properties");
                issues++;
                if (properties.TryGetProperty("timestamp", out var stamp) && stamp.GetString() is { } said) moments.Add(said);
                if (!properties.TryGetProperty("elems", out var elems) || elems.ValueKind != JsonValueKind.Array) continue;

                foreach (var elem in elems.EnumerateArray())
                {
                    var key = $"{char.ToLowerInvariant(elem.GetProperty("type").GetString()![0])}{elem.GetProperty("id").GetInt64()}";
                    if (!held.TryGetValue(key, out var ours))
                    {
                        unheld++;
                        continue;
                    }

                    // Osmose gives the tags of the element an issue is about and lists the others bare.
                    if (!elem.TryGetProperty("tags", out var tags) || tags.ValueKind != JsonValueKind.Object || !tags.EnumerateObject().Any()) continue;

                    var theirs = tags.EnumerateObject().ToDictionary(tag => tag.Name, tag => tag.Value.ToString());
                    finding.Ask(theirs.Count == ours.Count && theirs.All(tag => ours.GetValueOrDefault(tag.Key) == tag.Value),
                        () => $"{properties.GetProperty("item").GetInt32()}/{properties.GetProperty("class").GetInt32()} on {key}");
                }
            }
        }

        moments.Sort(StringComparer.Ordinal);
        finding.Read($"{issues:N0} issues, raised {(moments.Count > 0 ? $"{moments[0]} … {moments[^1]}" : "at an unsaid time")}; the layers' OSM stands at {data.Survey.Source.OsmBase}");
        finding.Read($"{finding.Asked:N0} of the elements they give tags for are in the layers; {finding.Failed:N0} of those ({Share(finding.Failed, finding.Asked)}) have been edited between, which may have answered the issue");
        finding.Read($"{unheld:N0} elements are in no layer: deleted since, or of a kind no layer reads");
        return finding;
    }

    static Finding CrossingKinds(Dataset data)
    {
        var finding = new Finding("Crossings: the node on the road and the way drawn across it");
        var walk = Dataset.Index(data.Walk, way => way.Way);
        var pairs = new Dictionary<string, int>();
        foreach (var crossing in data.Crossings.Where(crossing => crossing.Node is not null && crossing.Way is not null && crossing.Type == "pedestrian"))
        {
            if (!walk.TryGetValue(crossing.Way!.Value, out var way)) continue;

            var (onNode, onWay) = (Crossings.Kind(crossing.Tags), Crossings.Kind(way.Tags));
            if (onNode == "unknown" || onWay == "unknown") continue;

            var (_, _, conflict) = Crossings.Read(crossing.Tags, way.Tags);
            finding.Ask(conflict is null, () => $"n{crossing.Node} {onNode} / w{way.Way} {onWay}");
            if (conflict is not null) pairs[$"{onNode}/{onWay}"] = pairs.GetValueOrDefault($"{onNode}/{onWay}") + 1;
        }

        finding.Read($"{finding.Asked:N0} crossings say their kind on both node and way; {finding.Failed:N0} ({Share(finding.Failed, finding.Asked)}) disagree — a zebra being a painted crossing named, and a signalled one painted where its way says so{(pairs.Count > 0 ? ": " : "")}{string.Join(", ", pairs.OrderByDescending(pair => pair.Value).Select(pair => $"{pair.Key} {pair.Value}"))}");
        finding.Read("the layer takes the node's kind, which is where the road is crossed, and the way's only where the node says none; it is painted where the node says so, else where the way does");
        return finding;
    }

    static Finding Pavements(Dataset data)
    {
        var finding = new Finding("Pavements: a road's sidewalk tags and the pavements drawn beside it");
        var cases = new Dictionary<string, int>();
        foreach (var road in data.Roads)
        {
            var tags = data.SurveyRoads[road.Way].Tags;
            for (var side = 0; side < 2; side++)
            {
                var named = side == 0 ? "left" : "right";
                if (Roads.Tagged(tags, named) is not { } tagged) continue;

                var said = Roads.Disagrees(tagged, road.Sidewalk[side].Share ?? 0);
                finding.Ask(said is null, () => $"w{road.Way} {named}: {said}");
                if (said is not null) cases[said] = cases.GetValueOrDefault(said) + 1;
            }
        }

        finding.Read($"{finding.Asked:N0} road sides tagged; {finding.Failed:N0} ({Share(finding.Failed, finding.Asked)}) disagree with what is drawn{(cases.Count > 0 ? ": " : "")}{string.Join(", ", cases.OrderByDescending(pair => pair.Value).Select(pair => $"{pair.Key} {pair.Value}"))}");
        finding.Read("the layer takes a pavement drawn along half the road or more as the side's, whatever the tag; a side tagged none or drawn apart is taken as tagged");
        return finding;
    }

    static Finding Widths(Dataset data)
    {
        var finding = new Finding("Widths: a road's width tag, its lanes and its measured surface");
        var measured = data.Roads.Where(road => road.WidthMeasuredM is not null).ToList();
        foreach (var road in measured.Where(road => road.WidthTagM is not null))
        {
            finding.Ask(Math.Abs(road.WidthTagM!.Value - road.WidthMeasuredM!.Value) <= Roads.WidthShare * road.WidthMeasuredM.Value,
                () => string.Create(CultureInfo.InvariantCulture, $"w{road.Way} tagged {road.WidthTagM} m, measured {road.WidthMeasuredM} m"));
        }

        var ratios = measured.Select(road => road.CarriagewayM / road.WidthMeasuredM!.Value).ToList();
        finding.Read($"{finding.Asked:N0} roads both tag a width and run in a mapped surface; {finding.Failed:N0} ({Share(finding.Failed, finding.Asked)}) differ by more than {Roads.WidthShare:P0}");
        finding.Read(string.Create(CultureInfo.InvariantCulture,
            $"over {measured.Count:N0} measured roads, the carriageway its lanes make is {Median(ratios):F2} of the surface at the median; {Share(ratios.Count(ratio => ratio is < 2.0 / 3 or > 1.5), ratios.Count)} are under two thirds of it or over half as wide again — parking, a median or a lane count mistyped"));
        finding.Read("the layer keeps all three: the width its lanes make, the tag, and the surface measured");
        return finding;
    }

    static Finding Storeys(Dataset data)
    {
        var finding = new Finding("Buildings: a height and a level count on one building");
        var storeys = new List<double>();
        foreach (var building in data.Buildings.Where(building => building.Source == "osm"))
        {
            if (Buildings.Metres(building.Tags.GetValueOrDefault("height")) is not { } heightM || Buildings.Number(building.Tags.GetValueOrDefault("building:levels")) is not { } levels || levels < 1) continue;

            var storeyM = (heightM - (Buildings.Metres(building.Tags.GetValueOrDefault("min_height")) ?? 0)) / levels;
            storeys.Add(storeyM);
            finding.Ask(storeyM >= Buildings.LowestStoreyM && (storeyM <= Buildings.HighestHomeStoreyM || !Buildings.Homes.Contains(building.Tags.GetValueOrDefault("building") ?? "")),
                () => string.Create(CultureInfo.InvariantCulture, $"{building.Id} ({building.Use}) {heightM} m over {levels} levels"));
        }

        finding.Read(string.Create(CultureInfo.InvariantCulture,
            $"{finding.Asked:N0} buildings tag both; a storey is {Median(storeys):F1} m at the median; {finding.Failed:N0} ({Share(finding.Failed, finding.Asked)}) make one under {Buildings.LowestStoreyM} m, or a home's over {Buildings.HighestHomeStoreyM} m"));
        finding.Read("the layer takes the height tag over the level count");
        return finding;
    }

    static Finding Addresses(Dataset data)
    {
        var finding = new Finding("Addresses: the street a building's address names, and the streets OSM draws");
        var (names, old) = Names.Streets(data.SurveyRoads.Values.Select(way => way.Tags).Concat(data.Walk.Select(way => way.Tags))
            .Concat(data.Zones.Where(zone => zone.Kind is "square" or "leisure").Select(zone => zone.Tags)).Concat(data.Districts.Select(place => place.Tags)));
        var unmatched = new Dictionary<string, int>();
        var (renamed, addressed) = (0, 0);
        foreach (var building in data.Buildings.Where(building => building.Source == "osm"))
        {
            if (building.Tags.GetValueOrDefault("addr:street") is not { } street) continue;

            addressed++;
            var plain = Names.Street(street);
            if (names.Contains(plain)) continue;
            if (old.ContainsKey(plain)) renamed++;
            else unmatched[street] = unmatched.GetValueOrDefault(street) + 1;
        }

        foreach (var (street, count) in unmatched.OrderByDescending(pair => pair.Value).Take(5)) finding.Examples.Add($"{street} ×{count}");
        var lost = unmatched.Values.Sum();
        finding.Read($"{addressed:N0} OSM buildings name a street; {addressed - renamed - lost:N0} ({Share(addressed - renamed - lost, addressed)}) one OSM draws, {renamed:N0} a street by a name it has since dropped, {lost:N0} ({Share(lost, addressed)}, {unmatched.Count:N0} names) none — a misspelling, or a street OSM has not drawn");
        finding.Read("the layer keeps the address as tagged, and gives the street's present name beside one that names it by an old one");
        return finding;
    }

    static Finding Signals(Dataset data)
    {
        var finding = new Finding("Junction control: what OSM maps, what a camera saw where it maps nothing, and the rules");
        foreach (var junction in data.Junctions) finding.Ask(junction.Hints is null, () => $"n{junction.Node} {junction.Control}");
        var lit = data.Crossings.Where(crossing => crossing.Kind == "signals" && crossing.AtJunction && crossing.Junction is not null).Select(crossing => crossing.Junction!.Value).ToHashSet();
        var halfLit = data.Junctions.Count(junction => lit.Contains(junction.Node) && junction.Control is not ("signals" or "blinking"));
        foreach (var group in data.Junctions.GroupBy(junction => junction.ControlFrom).OrderByDescending(group => group.Count()))
        {
            finding.Read($"{group.Key}: {group.Count():N0} junctions — {string.Join(", ", group.GroupBy(junction => junction.Control).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count():N0}"))}");
        }

        finding.Read($"{finding.Failed:N0} junctions no signal holds have one near them by Osmose's reading of their crossings (2090), kept as a hint");
        finding.Read($"{halfLit:N0} junctions have a signalled crossing on one arm only and no signal of their own, and are read by their signs or the rules");
        return finding;
    }

    static Finding DetectedWidths(Dataset data)
    {
        var finding = new Finding("Widths: a road's lanes and mapped surface against the width read off imagery");
        bool Weighed(RoadRecord road) => road.WidthDetected is { Across: 1 } seen && seen.Share >= MlRoads.WeighedShare;
        var pairs = data.Roads.Where(road => road.WidthMeasuredM is not null && Weighed(road)).ToList();
        foreach (var road in pairs)
        {
            finding.Ask(Math.Abs(road.WidthDetected!.WidthM - road.WidthMeasuredM!.Value) <= Roads.WidthShare * road.WidthMeasuredM.Value,
                () => string.Create(CultureInfo.InvariantCulture, $"w{road.Way} read {road.WidthDetected!.WidthM} m, measured {road.WidthMeasuredM} m"));
        }

        finding.Read(string.Create(CultureInfo.InvariantCulture,
            $"{pairs.Count:N0} roads run in a mapped surface and along a detected stretch over half their length: the detected width is {Median(pairs.Select(road => road.WidthDetected!.WidthM / road.WidthMeasuredM!.Value)):F2} of the surface at the median, {Median(pairs.Select(road => Math.Abs(road.WidthDetected!.WidthM - road.WidthMeasuredM!.Value))):F1} m off it; {Share(finding.Failed, finding.Asked)} differ by more than {Roads.WidthShare:P0}"));

        var streets = data.Roads.Where(road => road.Role == "street").ToList();
        var weighed = streets.Where(Weighed).ToList();
        finding.Read(string.Create(CultureInfo.InvariantCulture,
            $"{Share((int)weighed.Sum(road => road.LengthM), (int)streets.Sum(road => road.LengthM))} of street length has a detected width over half its length (one carriageway under it); {data.Roads.Count(road => road.WidthDetected is { Across: >= 2 }):N0} roads are the ways of a dual carriageway read across both"));
        foreach (var group in weighed.GroupBy(road => road.Highway).Where(group => group.Count() >= 50).OrderByDescending(group => group.Count()))
        {
            var extra = group.Select(road => road.WidthDetected!.WidthM - road.CarriagewayM).ToList();
            finding.Read(string.Create(CultureInfo.InvariantCulture,
                $"{group.Key}: {group.Count():N0} streets read {Median(extra):+0.0;-0.0} m wider than their lanes make them at the median ({Quantile(extra, 0.1):+0.0;-0.0} to {Quantile(extra, 0.9):+0.0;-0.0} m, 10th to 90th percentile) — kerbside parking and gutters, or lanes not tagged"));
        }

        finding.Read(string.Create(CultureInfo.InvariantCulture,
            $"{data.Quality.Count(flag => flag.Kind == "lanes_and_detected_width_disagree"):N0} streets read more than {MlRoads.WiderM:F0} m wider or {MlRoads.NarrowerM:F0} m narrower than their lanes make them, flagged; the layer keeps the lanes as OSM's and the width read as a reading"));
        return finding;
    }

    static Finding Sightings(Dataset data)
    {
        var finding = new Finding("Sightings by camera against what OSM maps for the same thing");
        if (data.Seen.Count == 0) return finding.Read("no sightings this run");

        finding.Read(string.Join("; ", data.Seen.GroupBy(sighting => sighting.Source).Select(group => $"{group.Count():N0} from {group.Key}"))
            + (data.Seen.Any(sighting => sighting.Source == "mapillary") ? "" : " — Mapillary's own need a client token in MAPILLARY_TOKEN; Osmose relays only the signs OSM lacks"));
        finding.Read($"{data.Seen.Count(sighting => sighting.HeldBy is not null):N0} are held by a junction, deciding {data.Junctions.Count(junction => junction.ControlFrom == "seen"):N0} junctions' control where OSM maps nothing; {data.Seen.Count(sighting => sighting.Crossing == true):N0} were laid as crossings OSM lacks");

        foreach (var sighting in data.Seen.Where(sighting => sighting.Agrees is not null))
        {
            finding.Ask(sighting.Agrees == true, () => $"{sighting.Id} {sighting.Kind} {sighting.Osm ?? "none"}");
        }

        foreach (var group in data.Seen.Where(sighting => sighting.Agrees is not null).GroupBy(sighting => sighting.Kind).OrderByDescending(group => group.Count()))
        {
            finding.Read($"{group.Key}: {group.Count():N0} seen, {Share(group.Count(sighting => sighting.Agrees == true), group.Count())} with OSM mapping the same near it");
        }

        var arrows = data.Seen.Where(sighting => sighting.Kind == "lane_arrow" && sighting.Road is not null).Select(sighting => sighting.Road!.Value).ToHashSet();
        var turnLanes = data.Roads.Where(road => data.SurveyRoads.TryGetValue(road.Way, out var way) && way.Tags.Keys.Any(key => key.StartsWith("turn:lanes", StringComparison.Ordinal)))
            .Select(road => road.Way).ToHashSet();
        finding.Read($"{arrows.Count:N0} roads have lane arrows seen on them; {arrows.Count(turnLanes.Contains):N0} of them tag turn:lanes");
        var years = data.Seen.Where(sighting => sighting.LastSeen is not null).Select(sighting => sighting.LastSeen!).Order(StringComparer.Ordinal).ToList();
        finding.Read($"{data.Seen.Count:N0} sightings, last seen {years.FirstOrDefault()} … {years.LastOrDefault()}, half since {(years.Count > 0 ? years[years.Count / 2] : "—")}; one seen long ago may be gone");
        return finding;
    }

    static Finding DetectedStretches(Dataset data)
    {
        var finding = new Finding("Roads traced off imagery against OSM's ways");
        if (data.Detected.Count == 0) return finding.Read("no detected roads this run");

        foreach (var stretch in data.Detected) finding.Ask(stretch.Status != "unmapped", () => string.Create(CultureInfo.InvariantCulture, $"{stretch.Id} {stretch.LengthM:F0} m at {stretch.Line[0]},{stretch.Line[1]}"));
        var km = data.Detected.Sum(stretch => stretch.LengthM) / 1000;
        foreach (var group in data.Detected.GroupBy(stretch => stretch.Status).OrderByDescending(group => group.Sum(stretch => stretch.LengthM)))
        {
            finding.Read(string.Create(CultureInfo.InvariantCulture, $"{group.Key}: {group.Count():N0} stretches, {group.Sum(stretch => stretch.LengthM) / 1000:F0} km of {km:F0} km"));
        }

        finding.Read("an unmapped stretch is a road on the imagery with no OSM way of any kind beside it — a yard's drive, a track, or a road built or razed since — and is flagged, never laid as a road");
        return finding;
    }

    static Finding Terrain(Dataset data)
    {
        var finding = new Finding("Height models: the terrain (ground) against the surface (roofs, trees and decks)");
        var surfaceAt = Kept(data, name => name.StartsWith("dem-", StringComparison.Ordinal));
        var terrainAt = Kept(data, name => name == Dem.TerrainName);
        if (surfaceAt.Length == 0 || terrainAt.Length == 0) return finding.Read("not both models this run");

        var (surface, terrain) = (new Dem(string.Join(';', surfaceAt)), new Dem(string.Join(';', terrainAt)));
        var above = new List<double>();
        var nodes = data.SurveyRoads.Values.SelectMany(road => road.Nodes).Distinct();
        foreach (var node in nodes)
        {
            var (lat, lon) = (data.Survey.Nodes.Lat[node], data.Survey.Nodes.Lon[node]);
            if (!data.Plane.OnMap(data.Plane.At(lat, lon))) continue;

            var (surfaceM, groundM) = (surface.HeightM(lat, lon), terrain.HeightM(lat, lon));
            if (double.IsNaN(surfaceM) || double.IsNaN(groundM)) continue;

            above.Add(surfaceM - groundM);
            finding.Ask(surfaceM - groundM >= -SurfaceUnderM, () => string.Create(CultureInfo.InvariantCulture, $"n{data.Survey.Nodes.Id[node]} surface {surfaceM:F1} m, ground {groundM:F1} m"));
        }

        finding.Read(string.Create(CultureInfo.InvariantCulture,
            $"at {above.Count:N0} road nodes the surface stands {Median(above):F1} m over the ground at the median, {Quantile(above, 0.9):F1} m at the 90th percentile; {Share(above.Count(value => value > 3), above.Count)} over 3 m — the roofs and trees a road's height no longer reads; {Share(finding.Failed, finding.Asked)} stand more than {SurfaceUnderM:F0} m under it"));
        var roadsReadOff = data.Manifest.GetProperty("layers").EnumerateArray().First(layer => layer.GetProperty("layer").GetString() == "roads")
            .GetProperty("readOff").EnumerateArray().Select(source => source.GetString()).ToHashSet();
        finding.Read($"roads' heights are read off {(roadsReadOff.Contains(Dem.TerrainName) ? "the terrain model" : "the surface model, the terrain not had")}; {data.Roads.Count(road => road.SteepestPct > 8):N0} roads climb over 8 % somewhere");
        return finding;
    }

    /// <summary>The most the surface model may stand under the ground and still be the same ground read twice.</summary>
    const double SurfaceUnderM = 5;

    static Finding Edits(Dataset data)
    {
        var finding = new Finding("OSM's roads: when each was last edited");
        var moment = DateTime.Parse(data.Survey.Source.OsmBase, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
        var streets = data.Roads.Where(road => road.Role == "street" && road.EditedOn is not null)
            .Select(road => (road.LengthM, Years: (moment - DateTime.Parse(road.EditedOn!, CultureInfo.InvariantCulture)).TotalDays / 365.25)).ToList();
        if (streets.Count == 0) return finding.Read("no edit dates this run");

        var km = streets.Sum(street => street.LengthM);
        double Within(double years) => streets.Where(street => street.Years <= years).Sum(street => street.LengthM) / km;
        var byLength = streets.OrderBy(street => street.Years).ToList();
        var (half, gone) = (0.0, 0.0);
        foreach (var street in byLength)
        {
            gone += street.LengthM;
            if (gone >= km / 2)
            {
                half = street.Years;
                break;
            }
        }

        finding.Read(string.Create(CultureInfo.InvariantCulture,
            $"half the street length was last edited within {half:F1} years of the survey; {Within(1):P0} within a year, {1 - Within(5):P0} not for over five — an old edit is tags no one has touched since, not tags known wrong"));
        finding.Read($"{data.Roads.Count(road => road.CheckedOn is not null):N0} roads carry a date a mapper checked them on the ground");
        return finding;
    }

    static string[] Kept(Dataset data, Func<string, bool> which) => [.. data.Sources(which).SelectMany(source => source.KeptAt).Where(File.Exists)];

    static Pt Centroid(Pt[] ring) => Shape.Centroid(ring);

    static double Median(IEnumerable<double> values) => Quantile(values, 0.5);

    static double Quantile(IEnumerable<double> values, double q)
    {
        var sorted = values.Order().ToArray();
        return sorted.Length == 0 ? double.NaN : sorted[Math.Min(sorted.Length - 1, (int)(q * sorted.Length))];
    }

    static string Share(int part, int whole) => whole == 0 ? "—" : ((double)part / whole).ToString("P1", CultureInfo.InvariantCulture);
}
