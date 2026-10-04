namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>Public transport over the town, OSM's and the city's timetable laid as one</b>: every route — tram, trolleybus,
/// bus, minibus, train, ferry, funicular — with its ways and stops in order and, where the timetable runs it, its
/// service each day; every stop and platform with the road it stands beside and the routes calling there; and every
/// track, with the street a tram runs down.
/// </summary>
/// <remarks>
/// <para>
/// <b>OSM is the map, the timetable the service.</b> A timetable stop is OSM's stop where one serving its modes stands
/// within <see cref="MatchM"/> — the one of the same name, else the nearest — and that stop carries the timetable's
/// id; a timetable stop OSM has none for is laid as a stop of its own, where the timetable puts it. A timetable route
/// is OSM's relations of its mode and ref, each direction given to the relation calling at most of its stops, else
/// to the one whose ways begin and end where its line does; a direction no relation takes is laid as a route of its
/// own, down the roads its line runs along.
/// </para>
/// <para>
/// <b>A tram runs in a street where its track lies within <see cref="EmbeddedM"/> of the road's line</b> along most
/// of its length; a track off every road runs on its own way. Trolleybus wires are the road's own
/// <c>trolley_wire</c> tag and need no reading here.
/// </para>
/// </remarks>
internal static class Transit
{
    /// <summary>The farthest a track runs off its road's line and is still in the street: half a wide carriageway.</summary>
    const double EmbeddedM = 8;

    /// <summary>The farthest from a road a stop is put to it.</summary>
    const double StopReachM = 40;

    const double StepM = 10;

    /// <summary>The farthest a timetable stop stands from OSM's and is the same stop: a road's width and its pavements.</summary>
    const double MatchM = 40;

    /// <summary>The farthest a timetable line runs off a road's line, and the widest it turns off the road's heading, and still runs down it.</summary>
    const double LineOnM = 12, LineAlongDeg = 30;

    /// <summary>The farthest a timetable direction's ends stand off a relation's, the two added, and it is still that relation's way round.</summary>
    const double EndsM = 400;

    public static TransitFound Lay(Town town, Flags flags, string into, List<Written> written)
    {
        var elements = Element.Read(town.Fetched.Transit);
        var masterOf = new Dictionary<long, long>();
        foreach (var master in elements.Where(element => element.Type == 'r' && element.Tag("type") == "route_master"))
        {
            foreach (var member in master.Members) masterOf[member.Ref] = master.Id;
        }

        var routes = elements.Where(element => element.Type == 'r' && element.Tag("type") == "route").Select(route => new RouteDraft
        {
            Id = route.Key,
            Source = "osm",
            Mode = route.Tag("route") ?? "",
            Ref = route.Tag("ref"),
            Name = route.Tag("name"),
            From = route.Tag("from"),
            To = route.Tag("to"),
            Operator = route.Tag("operator"),
            Network = route.Tag("network"),
            Master = masterOf.TryGetValue(route.Id, out var master) ? master : null,
            Ways = [.. route.Members.Where(member => member.Type == 'w' && !member.Role.StartsWith("platform", StringComparison.Ordinal)).Select(member => member.Ref)],
            Stops = [.. route.Members.Where(member => member.Role.StartsWith("stop", StringComparison.Ordinal)).Select(member => $"{member.Type}{member.Ref}")],
            Platforms = [.. route.Members.Where(member => member.Role.StartsWith("platform", StringComparison.Ordinal)).Select(member => $"{member.Type}{member.Ref}")],
            Tags = route.Tags,
        }).ToList();

        var stops = elements.Where(element => element.Type is 'n' or 'w' && IsStop(element.Tags)).Select(stop =>
        {
            // A stop on a road stands where the survey has its node, which the survey's corrections may move off OSM's.
            var surveyed = stop.Type == 'n' && town.NodeIndex.TryGetValue(stop.Id, out var node) ? node : -1;
            var at = surveyed >= 0 ? town.NodeM[surveyed]
                : stop.Type == 'n' ? town.Plane.At(stop.Lat, stop.Lon) : Middle(town.Plane.Line(stop.Geometry));
            return new StopDraft
            {
                Id = stop.Key,
                Source = "osm",
                Kind = stop.Tag("public_transport") ?? stop.Tag("highway") ?? stop.Tag("railway") ?? "stop",
                Name = stop.Tag("name"),
                Modes = [.. ((string[])["bus", "trolleybus", "tram", "share_taxi", "minibus", "train", "light_rail", "ferry"]).Where(mode => stop.Tag(mode) == "yes")],
                AtM = at,
                At = surveyed >= 0 ? [town.Extract.Nodes.Lat[surveyed], town.Extract.Nodes.Lon[surveyed]]
                    : stop.Type == 'n' ? [stop.Lat, stop.Lon] : town.Plane.Degrees(at),
                OnRoad = surveyed >= 0 ? true : null,
                Shelter = stop.Tag("shelter"),
                Tags = stop.Tags,
            };
        }).ToList();

        var timetable = town.Fetched.GtfsAt is { } kept ? Gtfs.Read(kept) : null;
        var notLaid = timetable is null ? 0 : Conflate(town, timetable, routes, stops, flags);

        var found = new TransitFound();
        var calling = new Dictionary<string, SortedSet<string>>();
        foreach (var route in routes)
        {
            var label = Label(route.Mode, route.Ref, route.Name, route.Id);
            foreach (var stop in route.Stops.Concat(route.Platforms).Concat(route.Timetable?.Stops ?? []))
            {
                (calling.TryGetValue(stop, out var set) ? set : calling[stop] = new SortedSet<string>(StringComparer.Ordinal)).Add(label);
            }

            foreach (var way in route.Ways.Where(town.RoadIndex.ContainsKey))
            {
                (found.RoutesOn.TryGetValue(way, out var list) ? list : found.RoutesOn[way] = []).Add(label);
            }
        }

        var routeRecords = routes.Select(route => new RouteRecord
        {
            Id = route.Id,
            Source = route.Source,
            Mode = route.Mode,
            Ref = route.Ref,
            Name = route.Name,
            From = route.From,
            To = route.To,
            Operator = route.Operator,
            Network = route.Network,
            Master = route.Master,
            Ways = route.Ways,
            WaysOnMap = route.Ways.Count(town.RoadIndex.ContainsKey),
            Stops = route.Stops,
            Platforms = route.Platforms,
            Line = route.Line,
            Timetable = route.Timetable,
            Tags = route.Tags,
        }).ToList();

        var stopRecords = new List<StopRecord>(stops.Count);
        foreach (var stop in stops)
        {
            var near = town.NearestRoad(stop.AtM, StopReachM, road => town.CarsMay(road) || town.Highway(road) is "busway");
            stopRecords.Add(new StopRecord
            {
                Id = stop.Id,
                Source = stop.Source,
                Kind = stop.Kind,
                Name = stop.Name,
                Modes = stop.Modes,
                At = stop.At,
                OnRoad = stop.OnRoad,
                Road = near is { } road ? town.Roads[road.Road].Id : null,
                RoadM = near is { } off ? Math.Round(off.OffM, 1) : null,
                Side = near is { } beside ? Side(town, beside.Road, beside.Segment, stop.AtM) : null,
                Shelter = stop.Shelter,
                Routes = calling.TryGetValue(stop.Id, out var labels) ? [.. labels] : null,
                Timetable = stop.Timetable.Count > 0 ? [.. stop.Timetable] : null,
                Tags = stop.Tags,
            });
        }

        var tracks = new List<TrackRecord>();
        foreach (var rail in elements.Where(element => element.Type == 'w' && element.Tag("railway") is { } railway && railway != "platform" && element.Geometry.Length >= 4))
        {
            var geometry = town.Surveyed(rail);
            var line = town.Plane.Line(geometry);
            var (road, share) = rail.Tag("railway") == "tram" ? Street(town, line) : (null, 0);
            if (road is { } inside) (found.TramOn.TryGetValue(inside, out var list) ? list : found.TramOn[inside] = []).Add(rail.Id);

            tracks.Add(new TrackRecord
            {
                Way = rail.Id,
                Railway = rail.Tag("railway")!,
                Name = rail.Tag("name"),
                InStreet = road,
                InStreetShare = road is null ? null : Math.Round(share, 2),
                Embedded = rail.Tag("embedded") ?? rail.Tag("embedded_rails"),
                LengthM = Math.Round(Shape.Length(line), 1),
                Nodes = rail.Nodes,
                Line = geometry,
                Tags = rail.Tags,
            });
        }

        (found.Routes, found.Stops) = (routeRecords, stopRecords);
        written.Add(Layers.Write(into, "routes",
            "Every public transport route, OSM's relations and the timetable's routes laid as one: its mode, ref and name, its ways and stops in order, and where the timetable runs it its service each day — trips, first and last, headway.",
            timetable is null ? ["osm-transit"] : ["osm-transit", "gtfs.zip"], routeRecords,
            new
            {
                sources = routeRecords.GroupBy(record => record.Source).ToDictionary(g => g.Key, g => g.Count()),
                modes = routeRecords.GroupBy(record => record.Mode).OrderByDescending(g => g.Count()).ToDictionary(g => g.Key, g => g.Count()),
                withTimetable = routeRecords.Count(record => record.Timetable is not null),
                timetableInForce = timetable?.InForce,
                timetableRoutesRunningNoDay = notLaid,
                timetableReadOnce = timetable?.Dropped,
            }));
        written.Add(Layers.Write(into, "stops",
            "Every stop, stop position and platform, OSM's and the timetable's laid as one: what it is, its name and modes, the road it stands beside and on which side, the routes calling there, and the timetable stops it is.",
            timetable is null ? ["osm-transit"] : ["osm-transit", "gtfs.zip"], stopRecords,
            new
            {
                sources = stopRecords.GroupBy(record => record.Source).ToDictionary(g => g.Key, g => g.Count()),
                kinds = stopRecords.GroupBy(record => record.Kind).ToDictionary(g => g.Key, g => g.Count()),
                withTimetable = stopRecords.Count(record => record.Timetable is not null),
                withRoutes = stopRecords.Count(record => record.Routes is not null),
                besideNoRoad = stopRecords.Count(record => record.Road is null),
            }));
        written.Add(Layers.Write(into, "tracks",
            "Every railway and tram track: its kind, the street a tram runs down where it does, its nodes and line.",
            ["osm-transit"], tracks,
            new
            {
                kinds = tracks.GroupBy(record => record.Railway).ToDictionary(g => g.Key, g => Math.Round(g.Sum(r => r.LengthM) / 1000, 1)),
                tramInStreetKm = Math.Round(tracks.Where(record => record.InStreet is not null).Sum(record => record.LengthM) / 1000, 1),
            }));
        return found;
    }

    /// <summary>A route as every layer names it: its mode and ref, else its name, else its id.</summary>
    public static string Label(string mode, string? reference, string? name, string id) => $"{mode} {reference ?? name ?? id}";

    /// <summary>
    /// The timetable laid onto OSM's stops and routes, and what OSM has none for laid beside them; how many of the
    /// timetable's routes run on no day and are not laid.
    /// </summary>
    static int Conflate(Town town, Timetable timetable, List<RouteDraft> routes, List<StopDraft> stops, Flags flags)
    {
        var running = timetable.Routes.Where(route => route.Directions.Any(direction => direction.Days.Count > 0)).ToList();
        var servedBy = new Dictionary<string, HashSet<string>>();
        foreach (var route in running)
        {
            foreach (var stop in route.Directions.SelectMany(direction => direction.Stops)) (servedBy.TryGetValue(stop, out var set) ? set : servedBy[stop] = []).Add(Family(route.Mode));
        }

        var osmModes = stops.ToDictionary(stop => stop.Id, stop => stop.Modes.Select(Family).ToHashSet());
        foreach (var route in routes)
        {
            foreach (var stop in route.Stops.Concat(route.Platforms)) osmModes.GetValueOrDefault(stop)?.Add(Family(route.Mode));
        }

        foreach (var stop in stops)
        {
            if (stop.Tags.GetValueOrDefault("railway") == "tram_stop") osmModes[stop.Id].Add("tram");
            if (stop.Tags.GetValueOrDefault("highway") == "bus_stop") osmModes[stop.Id].Add("bus");
        }

        var grid = new Grid(MatchM);
        for (var at = 0; at < stops.Count; at++) grid.Add(at, Box.Empty.With(stops[at].AtM));

        var placed = new Dictionary<string, string>();
        var near = new HashSet<int>();
        foreach (var (id, stop) in timetable.Stops)
        {
            if (!servedBy.TryGetValue(id, out var modes)) continue;

            var at = town.Plane.At(stop.Lat, stop.Lon);
            near.Clear();
            grid.Near(Box.Empty.With(at).Grown(MatchM), near);
            var match = near.Select(candidate => stops[candidate])
                .Where(own => own.Source == "osm" && (own.AtM - at).Length <= MatchM && (osmModes[own.Id].Count == 0 || osmModes[own.Id].Overlaps(modes)))
                .OrderBy(own => Names.Same(stop.Name, own.Name) ? 0 : 1).ThenBy(own => (own.AtM - at).Length).FirstOrDefault();
            if (match is not null)
            {
                match.Timetable.Add(id);
                placed[id] = match.Id;
                continue;
            }

            var own = new StopDraft
            {
                Id = $"gtfs:{id}",
                Source = "timetable",
                Kind = "timetable_stop",
                Name = stop.Name,
                Modes = [.. modes.Order(StringComparer.Ordinal)],
                AtM = at,
                At = [stop.Lat, stop.Lon],
                Tags = [],
            };
            own.Timetable.Add(id);
            stops.Add(own);
            placed[id] = own.Id;
            flags.Raise("timetable_stop_not_in_osm", stop.Lat, stop.Lon, $"the timetable's stop {stop.Name ?? id} ({string.Join("/", modes)}) has no OSM stop of its modes within {MatchM:F0} m", $"gtfs:{id}");
        }

        var groups = routes.GroupBy(route => (Family(route.Mode), Ref(route.Ref))).ToDictionary(group => group.Key, group => group.ToList());
        foreach (var route in running)
        {
            var directions = route.Directions.Where(direction => direction.Days.Count > 0).ToList();
            RouteTimetable Service(TimetableDirection direction) => new()
            {
                Id = route.Id,
                Agencies = route.Agencies,
                Direction = direction.Direction,
                Headsign = direction.Headsign,
                Days = direction.Days,
                Stops = [.. direction.Stops.Where(placed.ContainsKey).Select(stop => placed[stop])],
            };

            void Own(TimetableDirection direction, string kind, string why, string[] elements)
            {
                routes.Add(new RouteDraft
                {
                    Id = $"gtfs:{route.Id}:{direction.Direction}",
                    Source = "timetable",
                    Mode = route.Mode,
                    Ref = route.Ref,
                    Name = route.Name,
                    To = direction.Headsign,
                    Ways = Along(town, direction.Shape is { } shape ? town.Plane.Line(shape) : []),
                    Stops = [.. direction.Stops.Where(placed.ContainsKey).Select(stop => placed[stop])],
                    Platforms = [],
                    Line = direction.Shape,
                    Timetable = Service(direction),
                    Tags = [],
                });
                if (direction.Stops.Select(id => timetable.Stops[id]).FirstOrDefault() is { } first) flags.Raise(kind, first.Lat, first.Lon, why, elements);
            }

            if (!groups.TryGetValue((Family(route.Mode), Ref(route.Ref)), out var relations))
            {
                foreach (var direction in directions)
                {
                    Own(direction, "timetable_route_not_in_osm", $"the timetable's {route.Mode} {route.Ref} has no OSM relation of its mode and ref", [$"gtfs:{route.Id}"]);
                }

                continue;
            }

            // Each direction to the relation calling at most of its stops; what no stop decides, to the relation whose
            // ways begin and end where its line does; and what none takes, a route of its own.
            var calls = relations.ToDictionary(relation => relation, relation => relation.Stops.Concat(relation.Platforms).ToHashSet());
            var byStops = directions.SelectMany(direction => relations.Select(relation => (Direction: direction, Relation: relation,
                Score: (double)direction.Stops.Count(stop => placed.TryGetValue(stop, out var laid) && calls[relation].Contains(laid))))).Where(pair => pair.Score > 0).OrderByDescending(pair => pair.Score).ToList();
            var byEnds = directions.SelectMany(direction => relations.Select(relation => (Direction: direction, Relation: relation, Score: -Ends(town, direction, relation))))
                .Where(pair => pair.Score >= -EndsM).OrderByDescending(pair => pair.Score).ToList();
            var given = new HashSet<TimetableDirection>();
            foreach (var (direction, relation, _) in byStops.Concat(byEnds))
            {
                if (given.Contains(direction) || relation.Timetable is not null) continue;

                relation.Timetable = Service(direction);
                given.Add(direction);
            }

            foreach (var direction in directions.Where(direction => !given.Contains(direction)))
            {
                Own(direction, "timetable_direction_not_on_osm", $"{route.Mode} {route.Ref} towards {direction.Headsign ?? direction.Direction}: no OSM relation of it calls at its stops or runs its line's ends",
                    [$"gtfs:{route.Id}", .. relations.Select(relation => relation.Id)]);
            }
        }

        return timetable.Routes.Count - running.Count;
    }

    /// <summary>
    /// How far a timetable direction's line begins and ends off a relation's first and last ways on the map, the two
    /// added; past all reach where either has nothing to measure.
    /// </summary>
    static double Ends(Town town, TimetableDirection direction, RouteDraft relation)
    {
        var onMap = relation.Ways.Where(town.RoadIndex.ContainsKey).ToArray();
        if (direction.Shape is not { Length: >= 4 } shape || onMap.Length == 0) return double.MaxValue;

        var line = town.Plane.Line(shape);
        return Shape.Nearest(line[0], town.RoadLineM[town.RoadIndex[onMap[0]]]).OffM + Shape.Nearest(line[^1], town.RoadLineM[town.RoadIndex[onMap[^1]]]).OffM;
    }

    /// <summary>A mode's family, the way a stop serves it: a minibus or share taxi stops where a bus does.</summary>
    static string Family(string mode) => mode is "minibus" or "share_taxi" ? "bus" : mode;

    /// <summary>
    /// A route's ref as it is compared: upper case, and a Latin letter that looks like a Cyrillic one read as it, so
    /// the timetable's <c>232А</c> is OSM's <c>232а</c> and its <c>16Е</c> OSM's <c>16E</c>.
    /// </summary>
    static string Ref(string? reference) => reference is null ? "" : string.Concat(reference.Trim().ToUpperInvariant().Select(c => c switch
    {
        'A' => 'А', 'B' => 'В', 'C' => 'С', 'E' => 'Е', 'H' => 'Н', 'K' => 'К', 'M' => 'М', 'O' => 'О', 'P' => 'Р', 'T' => 'Т', 'X' => 'Х',
        _ => c,
    }));

    /// <summary>The roads a line runs down, in order: each road the line runs along for two samples running, within reach and on its heading.</summary>
    static long[] Along(Town town, Pt[] line)
    {
        var hits = new List<int>();
        var near = new HashSet<int>();
        var lengthM = Shape.Length(line);
        var along = Math.Cos(double.DegreesToRadians(LineAlongDeg));
        for (var alongM = 0.0; alongM <= lengthM && line.Length > 1; alongM += StepM)
        {
            var at = Shape.Along(line, alongM);
            var heading = Shape.Along(line, Math.Min(lengthM, alongM + 1)) - Shape.Along(line, Math.Max(0, alongM - 1));
            near.Clear();
            town.RoadGrid.Near(Box.Empty.With(at).Grown(LineOnM), near);
            var (best, bestM) = (-1, LineOnM);
            foreach (var segment in near)
            {
                var (road, first) = town.Segments[segment];
                var (a, b) = (town.RoadLineM[road][first], town.RoadLineM[road][first + 1]);
                var run = b - a;
                if (run.Length < 0.5 || heading.Length < 1e-6 || Math.Abs(Pt.Dot(run, heading)) < along * run.Length * heading.Length) continue;

                var offM = Shape.Nearest(at, [a, b]).OffM;
                if (offM <= bestM) (best, bestM) = (road, offM);
            }

            hits.Add(best);
        }

        var ways = new List<long>();
        for (var at = 0; at + 1 < hits.Count; at++)
        {
            if (hits[at] < 0 || hits[at + 1] != hits[at]) continue;

            var id = town.Roads[hits[at]].Id;
            if (ways.Count == 0 || ways[^1] != id) ways.Add(id);
        }

        return [.. ways];
    }

    static bool IsStop(Dictionary<string, string> tags) =>
        tags.ContainsKey("public_transport") && tags["public_transport"] is "stop_position" or "platform" or "station"
        || tags.GetValueOrDefault("highway") is "bus_stop" or "platform"
        || tags.GetValueOrDefault("railway") is "tram_stop" or "station" or "halt" or "stop" or "platform";

    static Pt Middle(Pt[] line) => Shape.Along(line, Shape.Length(line) / 2);

    static string Side(Town town, int road, int segment, Pt at)
    {
        var (a, b) = (town.RoadLineM[road][segment], town.RoadLineM[road][segment + 1]);
        return Pt.Cross(b - a, at - a) > 0 ? "right" : "left";
    }

    /// <summary>The road a tram track runs down, where it lies near one road's line for most of its length, and the share.</summary>
    static (long? Road, double Share) Street(Town town, Pt[] line)
    {
        var lengthM = Shape.Length(line);
        var votes = new Dictionary<int, int>();
        var samples = 0;
        for (var alongM = Math.Min(StepM / 2, lengthM / 2); alongM <= lengthM; alongM += StepM)
        {
            samples++;
            if (town.NearestRoad(Shape.Along(line, alongM), EmbeddedM, road => town.CarsMay(road)) is { } near) votes[near.Road] = votes.GetValueOrDefault(near.Road) + 1;
        }

        if (votes.Count == 0 || samples == 0) return (null, 0);

        var best = votes.MaxBy(vote => vote.Value).Key;
        var share = (double)votes.Values.Sum() / samples;
        return share >= 0.5 ? (town.Roads[best].Id, share) : (null, share);
    }

    /// <summary>A route while it is laid, before it is written.</summary>
    sealed class RouteDraft
    {
        public required string Id { get; init; }

        public required string Source { get; init; }

        public required string Mode { get; init; }

        public string? Ref { get; init; }

        public string? Name { get; init; }

        public string? From { get; init; }

        public string? To { get; init; }

        public string? Operator { get; init; }

        public string? Network { get; init; }

        public long? Master { get; init; }

        public required long[] Ways { get; init; }

        public required string[] Stops { get; init; }

        public required string[] Platforms { get; init; }

        public int[]? Line { get; init; }

        public RouteTimetable? Timetable { get; set; }

        public required Dictionary<string, string> Tags { get; init; }
    }

    /// <summary>A stop while it is laid, before it is written.</summary>
    sealed class StopDraft
    {
        public required string Id { get; init; }

        public required string Source { get; init; }

        public required string Kind { get; init; }

        public string? Name { get; init; }

        public required string[] Modes { get; init; }

        public required Pt AtM { get; init; }

        public required int[] At { get; init; }

        public bool? OnRoad { get; init; }

        public string? Shelter { get; init; }

        public List<string> Timetable { get; } = [];

        public required Dictionary<string, string> Tags { get; init; }
    }
}

/// <summary>What the transit layer found that later layers read.</summary>
internal sealed class TransitFound
{
    /// <summary>The routes running along each survey road, by its OSM id, as mode and ref.</summary>
    public Dictionary<long, List<string>> RoutesOn { get; } = [];

    /// <summary>The tram tracks running down each survey road, by its OSM id.</summary>
    public Dictionary<long, List<long>> TramOn { get; } = [];

    public List<RouteRecord> Routes { get; set; } = [];

    public List<StopRecord> Stops { get; set; } = [];
}

internal sealed class RouteRecord
{
    /// <summary><c>r123</c> for an OSM relation; <c>gtfs:&lt;route&gt;:&lt;direction&gt;</c> for a timetable route OSM has none for.</summary>
    public required string Id { get; init; }

    /// <summary><c>osm</c> or <c>timetable</c>.</summary>
    public required string Source { get; init; }

    public required string Mode { get; init; }

    public string? Ref { get; init; }

    public string? Name { get; init; }

    public string? From { get; init; }

    public string? To { get; init; }

    public string? Operator { get; init; }

    public string? Network { get; init; }

    /// <summary>The route master relation grouping it with its other directions.</summary>
    public long? Master { get; init; }

    /// <summary>Its ways in order, by OSM id: a relation's own, or the roads a timetable route's line runs down.</summary>
    public required long[] Ways { get; init; }

    public required int WaysOnMap { get; init; }

    /// <summary>Its stops in order, by their id in the stops layer.</summary>
    public required string[] Stops { get; init; }

    public required string[] Platforms { get; init; }

    /// <summary>A timetable route's line, lat, lon pairs in 1e-7°.</summary>
    public int[]? Line { get; init; }

    /// <summary>The timetable's service on this route and direction, where the timetable runs it.</summary>
    public RouteTimetable? Timetable { get; init; }

    public required Dictionary<string, string> Tags { get; init; }
}

/// <summary>One direction of a timetable route as laid on a route: which, its service each day, and its stops in the stops layer.</summary>
internal sealed class RouteTimetable
{
    /// <summary>The timetable's route id.</summary>
    public required string Id { get; init; }

    public required string[] Agencies { get; init; }

    public required string Direction { get; init; }

    public string? Headsign { get; init; }

    /// <summary>Its service on each day it runs: <c>weekday</c>, <c>saturday</c>, <c>sunday</c>.</summary>
    public required Dictionary<string, ServiceDay> Days { get; init; }

    /// <summary>Its stops in order, by their id in the stops layer.</summary>
    public required string[] Stops { get; init; }
}

internal sealed class StopRecord
{
    /// <summary><c>n123</c> or <c>w123</c> for OSM's; <c>gtfs:&lt;stop&gt;</c> for a timetable stop OSM has none for.</summary>
    public required string Id { get; init; }

    /// <summary><c>osm</c> or <c>timetable</c>.</summary>
    public required string Source { get; init; }

    /// <summary><c>stop_position</c>, <c>platform</c>, <c>station</c>, <c>bus_stop</c>, <c>tram_stop</c>, <c>timetable_stop</c> and their like.</summary>
    public required string Kind { get; init; }

    public string? Name { get; init; }

    public required string[] Modes { get; init; }

    public required int[] At { get; init; }

    /// <summary>Whether it is a node of a survey road — a stop position on the carriageway.</summary>
    public bool? OnRoad { get; init; }

    public long? Road { get; init; }

    public double? RoadM { get; init; }

    /// <summary><c>left</c> or <c>right</c> of the road as drawn.</summary>
    public string? Side { get; init; }

    public string? Shelter { get; init; }

    /// <summary>The routes calling there, as mode and ref, by OSM's relations and the timetable alike.</summary>
    public string[]? Routes { get; init; }

    /// <summary>The timetable stops it is, by their id in the timetable.</summary>
    public string[]? Timetable { get; init; }

    public required Dictionary<string, string> Tags { get; init; }
}

internal sealed class TrackRecord
{
    public required long Way { get; init; }

    public required string Railway { get; init; }

    public string? Name { get; init; }

    /// <summary>The survey road a tram runs down, by OSM id; null on a track of its own.</summary>
    public long? InStreet { get; init; }

    public double? InStreetShare { get; init; }

    public string? Embedded { get; init; }

    public required double LengthM { get; init; }

    public required long[] Nodes { get; init; }

    public required int[] Line { get; init; }

    public required Dictionary<string, string> Tags { get; init; }
}
