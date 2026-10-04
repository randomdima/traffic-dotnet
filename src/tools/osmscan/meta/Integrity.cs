namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>Whether the layers hold together</b>: every head counts what its layer holds, every item is in its layer
/// once, every survey road has its record, every place given for a survey node is that node's, every id one layer
/// gives for another is an item of that other, and every fact two layers both carry is the same in each.
/// </summary>
internal static class Integrity
{
    public static List<Finding> Check(Dataset data) =>
    [
        Heads(data), Unique(data), Covers(data), Places(data), References(data),
        CrossingsOnRoads(data), TramsOnRoads(data), RoutesOnRoads(data), ZonesOfRoads(data), Passages(data), LevelsOfRoads(data),
        Sidewalks(data), Arms(data), HeldCrossings(data), Restrictions(data), StopCalls(data), DetectedWidths(data), Widths(data), Seen(data), Unmapped(data), Edits(data),
    ];

    static Finding Edits(Dataset data)
    {
        var finding = new Finding("every road has its OSM version and last edit, and none was edited after the survey's moment");
        var moment = data.Survey.Source.OsmBase[..10];
        foreach (var road in data.Roads)
        {
            finding.Ask(road.Version is > 0 && road.EditedOn is { } edited && string.CompareOrdinal(edited, moment) <= 0,
                () => $"w{road.Way}: v{road.Version?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "∅"} edited {road.EditedOn ?? "∅"}");
        }

        return finding;
    }

    static Finding DetectedWidths(Dataset data)
    {
        var finding = new Finding("a road has a width read off imagery exactly where a detected stretch says it runs along it");
        var along = data.Detected.SelectMany(stretch => stretch.Roads ?? []).ToHashSet();
        foreach (var road in data.Roads)
        {
            finding.Ask((road.WidthDetected is not null) == along.Contains(road.Way),
                () => road.WidthDetected is null ? $"w{road.Way}: a stretch runs along it, no width" : $"w{road.Way}: a width, no stretch along it");
        }

        return finding;
    }

    static Finding Heads(Dataset data)
    {
        var finding = new Finding("each layer's head and the manifest count what it holds");
        var listed = new HashSet<string>();
        foreach (var layer in data.Manifest.GetProperty("layers").EnumerateArray())
        {
            var name = layer.GetProperty("layer").GetString()!;
            var said = layer.GetProperty("count").GetInt32();
            listed.Add(name + ".json");
            var head = data.Heads.GetValueOrDefault(name);
            finding.Ask(head is { } read && read.Said == read.Held && read.Held == said,
                () => head is { } shown ? $"{name}: head {shown.Said}, items {shown.Held}, manifest {said}" : $"{name}: not read");
        }

        foreach (var file in Directory.EnumerateFiles(data.Folder, "*.json").Select(Path.GetFileName))
        {
            if (file != "manifest.json") finding.Ask(listed.Contains(file!), () => $"{file} not in the manifest");
        }

        return finding;
    }

    static Finding Unique(Dataset data)
    {
        var finding = new Finding("every item is in its layer once");
        void Once<T>(string layer, IEnumerable<T> items, Func<T, string> key)
        {
            foreach (var group in items.GroupBy(key)) finding.Ask(group.Count() == 1, () => $"{layer} {group.Key} ×{group.Count()}");
        }

        Once("roads", data.Roads, road => $"w{road.Way}");
        Once("junctions", data.Junctions, junction => $"n{junction.Node}");
        Once("crossings", data.Crossings, crossing => crossing.Node is { } node ? $"n{node}" : $"w{crossing.Way} over w{crossing.Road} at {crossing.At[0]},{crossing.At[1]}");
        Once("walk", data.Walk, walk => $"w{walk.Way}");
        Once("zones", data.Zones, zone => zone.Id);
        Once("districts", data.Districts, district => district.Id);
        Once("buildings", data.Buildings, building => building.Id);
        Once("levels", data.Levels, level => $"{level.Class} w{level.Way}");
        Once("routes", data.Routes, route => route.Id);
        Once("stops", data.Stops, stop => stop.Id);
        Once("stops' timetable stops", data.Stops.SelectMany(stop => stop.Timetable ?? []), id => $"gtfs:{id}");
        Once("tracks", data.Tracks, track => $"w{track.Way}");
        Once("places", data.Places, place => place.Id);
        Once("furniture", data.Furniture, item => item.Id);
        Once("barriers", data.Barriers, item => item.Id);
        Once("unbuilt", data.Unbuilt, item => item.Id);
        Once("detected", data.Detected, item => item.Id);
        Once("seen", data.Seen, item => item.Id);
        Once("unmapped", data.Unmapped, item => item.Id);
        return finding;
    }

    static Finding Covers(Dataset data)
    {
        var finding = new Finding("every survey road has its record in roads, and roads holds no other way");
        var held = data.Roads.Select(road => road.Way).ToHashSet();
        foreach (var way in data.SurveyRoads.Keys) finding.Ask(held.Contains(way), () => $"w{way} has no record");
        foreach (var way in held.Where(way => !data.SurveyRoads.ContainsKey(way))) finding.Ask(false, () => $"w{way} is no survey road");
        return finding;
    }

    static Finding Places(Dataset data)
    {
        var finding = new Finding("a place given for a survey node is the survey's, a line has a place for each node, a road a height for each");
        foreach (var junction in data.Junctions) finding.Ask(data.At(junction.Node).SequenceEqual(junction.At), () => $"junction n{junction.Node}");
        foreach (var crossing in data.Crossings.Where(crossing => crossing.Node is not null))
        {
            finding.Ask(data.At(crossing.Node!.Value).SequenceEqual(crossing.At), () => $"crossing n{crossing.Node}");
        }

        foreach (var stop in data.Stops.Where(stop => stop.OnRoad == true)) finding.Ask(data.At(long.Parse(stop.Id[1..])).SequenceEqual(stop.At), () => $"stop {stop.Id}");
        foreach (var road in data.Roads.Where(road => road.HeightM is not null))
        {
            finding.Ask(data.SurveyRoads.TryGetValue(road.Way, out var way) && way.Nodes.Length == road.HeightM!.Length, () => $"road w{road.Way} heights");
        }

        foreach (var walk in data.Walk) finding.Ask(walk.Line.Length == 2 * walk.Nodes.Length, () => $"walk w{walk.Way}");
        foreach (var track in data.Tracks) finding.Ask(track.Line.Length == 2 * track.Nodes.Length, () => $"track w{track.Way}");
        foreach (var building in data.Buildings.Where(building => building.Entrances is not null))
        {
            foreach (var entrance in building.Entrances!)
            {
                finding.Ask(building.Outer.Any(ring => Vertex(ring, entrance.At)), () => $"entrance n{entrance.Node} off {building.Id}");
            }
        }

        return finding;

        static bool Vertex(int[] ring, int[] at)
        {
            for (var k = 0; k + 1 < ring.Length; k += 2)
            {
                if (ring[k] == at[0] && ring[k + 1] == at[1]) return true;
            }

            return false;
        }
    }

    static Finding References(Dataset data)
    {
        var finding = new Finding("every id a layer gives for another is an item of that other");
        var roads = data.Roads.Select(road => road.Way).ToHashSet();
        var junctions = data.Junctions.Select(junction => junction.Node).ToHashSet();
        var walk = data.Walk.Select(way => way.Way).ToHashSet();
        var tracks = data.Tracks.Select(track => track.Way).ToHashSet();
        var zones = data.Zones.Select(zone => zone.Id).ToHashSet();
        var buildings = data.Buildings.Select(building => building.Id).ToHashSet();
        var districts = data.Districts.Where(district => district.Kind == "district").Select(district => district.Name).ToHashSet();
        var stops = data.Stops.Select(stop => stop.Id).ToHashSet();
        var drawn = data.Walk.Select(way => $"w{way.Way}").Concat(data.Tracks.Select(track => $"w{track.Way}")).Concat(data.Roads.Select(road => $"w{road.Way}"))
            .Concat(data.Buildings.Select(building => building.Id)).Concat(data.Zones.Select(zone => zone.Id)).Concat(data.Barriers.Select(barrier => barrier.Id)).ToHashSet();

        HashSet<long> Of(string lineClass) => lineClass switch { "road" => roads, "rail" => tracks, _ => walk };

        foreach (var junction in data.Junctions)
        {
            var arms = junction.Arms.Length;
            foreach (var arm in junction.Arms) finding.Ask(roads.Contains(arm.Way), () => $"junction n{junction.Node} arm w{arm.Way}");
            if (junction.Cluster is { } cluster) finding.Ask(junctions.Contains(cluster), () => $"junction n{junction.Node} cluster n{cluster}");
            foreach (var movement in junction.Movements) finding.Ask(movement.From < arms && movement.To < arms, () => $"junction n{junction.Node} movement {movement.From}→{movement.To}");
            foreach (var held in junction.Held ?? []) finding.Ask(held.Arm is null || held.Arm < arms, () => $"junction n{junction.Node} held n{held.Node} arm {held.Arm}");
            foreach (var main in junction.LikelyMain ?? []) finding.Ask(main < arms, () => $"junction n{junction.Node} main arm {main}");
        }

        foreach (var crossing in data.Crossings)
        {
            finding.Ask(roads.Contains(crossing.Road), () => $"crossing on w{crossing.Road}");
            if (crossing.Junction is { } junction) finding.Ask(junctions.Contains(junction), () => $"crossing junction n{junction}");
            if (crossing.Way is { } way) finding.Ask(walk.Contains(way), () => $"crossing way w{way}");
        }

        foreach (var way in data.Walk.Where(way => way.Road is not null)) finding.Ask(roads.Contains(way.Road!.Value), () => $"walk w{way.Way} beside w{way.Road}");
        foreach (var zone in data.Zones)
        {
            foreach (var road in (zone.Roads ?? []).Concat(zone.WaysIn ?? [])) finding.Ask(roads.Contains(road), () => $"zone {zone.Id} road w{road}");
        }

        foreach (var building in data.Buildings.Where(building => building.Passages is not null))
        {
            foreach (var way in building.Passages!) finding.Ask(roads.Contains(way) || walk.Contains(way), () => $"building {building.Id} passage w{way}");
        }

        foreach (var level in data.Levels)
        {
            finding.Ask(Of(level.Class).Contains(level.Way), () => $"level {level.Class} w{level.Way}");
            if (level.Outline is { } outline) finding.Ask(zones.Contains(outline), () => $"level w{level.Way} outline {outline}");
            if (level.Under is { } under) finding.Ask(buildings.Contains(under), () => $"level w{level.Way} under {under}");
        }

        foreach (var crossing in data.Overpasses)
        {
            finding.Ask(Of(crossing.UpperClass).Contains(crossing.Upper) && Of(crossing.LowerClass).Contains(crossing.Lower), () => $"overpass w{crossing.Upper} over w{crossing.Lower}");
        }

        foreach (var road in data.Roads)
        {
            if (road.Zone is { } zone) finding.Ask(zones.Contains(zone), () => $"road w{road.Way} zone {zone}");
            if (road.Under is { } under) finding.Ask(buildings.Contains(under), () => $"road w{road.Way} under {under}");
            if (road.District is { } district) finding.Ask(districts.Contains(district), () => $"road w{road.Way} district {district}");
            foreach (var track in road.Tram ?? []) finding.Ask(tracks.Contains(track), () => $"road w{road.Way} tram w{track}");
        }

        foreach (var stop in data.Stops.Where(stop => stop.Road is not null)) finding.Ask(roads.Contains(stop.Road!.Value), () => $"stop {stop.Id} road w{stop.Road}");
        foreach (var track in data.Tracks.Where(track => track.InStreet is not null)) finding.Ask(roads.Contains(track.InStreet!.Value), () => $"track w{track.Way} street w{track.InStreet}");
        foreach (var route in data.Routes)
        {
            finding.Ask(route.WaysOnMap == route.Ways.Count(roads.Contains), () => $"route {route.Id} ways on map {route.WaysOnMap}");

            // An OSM relation may name a stop past the map's edge; the timetable's stops are all laid.
            var laid = route.Source == "timetable" ? route.Stops.Concat(route.Timetable?.Stops ?? []) : route.Timetable?.Stops ?? [];
            foreach (var stop in laid) finding.Ask(stops.Contains(stop), () => $"route {route.Id} stop {stop}");
        }

        foreach (var item in data.Furniture.Where(item => item.Road is not null)) finding.Ask(roads.Contains(item.Road!.Value), () => $"furniture {item.Id} road w{item.Road}");
        foreach (var stretch in data.Detected)
        {
            foreach (var road in stretch.Roads ?? []) finding.Ask(roads.Contains(road), () => $"detected {stretch.Id} road w{road}");
        }

        var seen = data.Seen.Select(sighting => sighting.Id).ToHashSet();
        foreach (var sighting in data.Seen)
        {
            if (sighting.Road is { } road) finding.Ask(roads.Contains(road), () => $"seen {sighting.Id} road w{road}");
            if (sighting.Junction is { } junction) finding.Ask(junctions.Contains(junction), () => $"seen {sighting.Id} junction n{junction}");
            if (sighting.HeldBy is { } holder) finding.Ask(junctions.Contains(holder), () => $"seen {sighting.Id} held by n{holder}");
        }

        foreach (var junction in data.Junctions)
        {
            foreach (var held in (junction.Held ?? []).Where(held => held.Seen is not null)) finding.Ask(seen.Contains(held.Seen!), () => $"junction n{junction.Node} holds {held.Seen}");
        }

        foreach (var crossing in data.Crossings.Where(crossing => crossing.Seen is not null)) finding.Ask(seen.Contains(crossing.Seen!), () => $"crossing from {crossing.Seen}");

        var stretches = data.Detected.Select(stretch => stretch.Id).ToHashSet();
        foreach (var network in data.Unmapped)
        {
            foreach (var stretch in network.Stretches) finding.Ask(stretches.Contains(stretch), () => $"unmapped {network.Id} stretch {stretch}");
            foreach (var road in network.Meets ?? []) finding.Ask(roads.Contains(road), () => $"unmapped {network.Id} meets w{road}");
        }

        var laidElsewhere = stretches.Concat(seen).Concat(data.Unmapped.Select(network => network.Id)).ToHashSet();
        foreach (var flag in data.Quality.Where(flag => !flag.Kind.StartsWith("osmose_", StringComparison.Ordinal) && flag.Kind != "osm_note"))
        {
            foreach (var element in flag.Elements.Where(element => element.StartsWith('w'))) finding.Ask(drawn.Contains(element), () => $"flag {flag.Kind} {element}");
            foreach (var element in flag.Elements.Where(element => element.StartsWith("ml", StringComparison.Ordinal) || element.StartsWith("osmose:", StringComparison.Ordinal)))
            {
                finding.Ask(laidElsewhere.Contains(element), () => $"flag {flag.Kind} {element}");
            }
        }

        return finding;
    }

    static Finding CrossingsOnRoads(Dataset data)
    {
        var finding = new Finding("a road's crossings by kind are the crossings layer's on it, and a crossing's road class, name, width and lanes the road's");
        var roads = Dataset.Index(data.Roads, road => road.Way);
        var counted = data.Crossings.GroupBy(crossing => crossing.Road)
            .ToDictionary(group => group.Key, group => group.GroupBy(crossing => crossing.Kind).ToDictionary(kind => kind.Key, kind => kind.Count()));
        foreach (var road in data.Roads)
        {
            var said = road.Crossings ?? [];
            var held = counted.GetValueOrDefault(road.Way) ?? [];
            finding.Ask(said.Count == held.Count && said.All(kind => held.GetValueOrDefault(kind.Key) == kind.Value), () => $"w{road.Way}");
        }

        foreach (var crossing in data.Crossings)
        {
            finding.Ask(roads.TryGetValue(crossing.Road, out var road) && road.Highway == crossing.Highway && road.Name == crossing.Name
                        && road.CarriagewayM == crossing.CarriagewayM && road.Lanes.Sum() == crossing.Lanes,
                () => $"crossing {crossing.Node ?? crossing.Way} on w{crossing.Road}");
        }

        return finding;
    }

    static Finding TramsOnRoads(Dataset data)
    {
        var finding = new Finding("a road's trams are the tracks that say they run down it");
        var down = data.Tracks.Where(track => track.InStreet is not null).GroupBy(track => track.InStreet!.Value).ToDictionary(group => group.Key, group => group.Select(track => track.Way).ToHashSet());
        foreach (var road in data.Roads)
        {
            var held = down.GetValueOrDefault(road.Way) ?? [];
            finding.Ask(held.SetEquals(road.Tram ?? []), () => $"w{road.Way}");
        }

        return finding;
    }

    static Finding RoutesOnRoads(Dataset data)
    {
        var finding = new Finding("a road's routes are the routes that run along it");
        var along = new Dictionary<long, SortedSet<string>>();
        foreach (var route in data.Routes)
        {
            foreach (var way in route.Ways) (along.TryGetValue(way, out var set) ? set : along[way] = new SortedSet<string>(StringComparer.Ordinal)).Add(Transit.Label(route.Mode, route.Ref, route.Name, route.Id));
        }

        foreach (var road in data.Roads) finding.Ask((along.GetValueOrDefault(road.Way) ?? []).SequenceEqual(road.Routes ?? []), () => $"w{road.Way}");
        return finding;
    }

    static Finding ZonesOfRoads(Dataset data)
    {
        var finding = new Finding("a road's zone holds it among its roads, and a zone's roads say they are its");
        var zones = Dataset.Index(data.Zones, zone => zone.Id);
        var roads = Dataset.Index(data.Roads, road => road.Way);
        foreach (var road in data.Roads.Where(road => road.Zone is not null))
        {
            finding.Ask(zones.TryGetValue(road.Zone!, out var zone) && (zone.Roads ?? []).Contains(road.Way) && zone.Kind == road.ZoneKind, () => $"w{road.Way} in {road.Zone}");
        }

        foreach (var zone in data.Zones)
        {
            foreach (var way in zone.Roads ?? []) finding.Ask(roads.TryGetValue(way, out var road) && road.Zone == zone.Id, () => $"{zone.Id} holds w{way}, which says {(roads.TryGetValue(way, out var said) ? said.Zone ?? "none" : "nothing")}");
        }

        return finding;
    }

    static Finding Passages(Dataset data)
    {
        var finding = new Finding("a way under a building is among that building's passages, and the building over each passage is one it passes");
        var passing = new Dictionary<long, HashSet<string>>();
        foreach (var building in data.Buildings)
        {
            foreach (var way in building.Passages ?? []) (passing.TryGetValue(way, out var set) ? set : passing[way] = []).Add(building.Id);
        }

        foreach (var road in data.Roads.Where(road => road.Under is not null)) finding.Ask(passing.GetValueOrDefault(road.Way)?.Contains(road.Under!) == true, () => $"road w{road.Way} under {road.Under}");
        foreach (var level in data.Levels.Where(level => level.Under is not null)) finding.Ask(passing.GetValueOrDefault(level.Way)?.Contains(level.Under!) == true, () => $"level w{level.Way} under {level.Under}");
        var roads = Dataset.Index(data.Roads, road => road.Way);
        foreach (var (way, over) in passing)
        {
            if (roads.TryGetValue(way, out var road)) finding.Ask(road.Under is not null && over.Contains(road.Under), () => $"w{way} passes {string.Join(",", over)}, says {road.Under ?? "none"}");
        }

        return finding;
    }

    static Finding LevelsOfRoads(Dataset data)
    {
        var finding = new Finding("a road's layer, structure and building over it are its record in levels, and it has one where any is set");
        var levels = Dataset.Index(data.Levels.Where(level => level.Class == "road"), level => level.Way);
        foreach (var road in data.Roads)
        {
            var off = road.Layer != 0 || road.Structure is not null || road.Under is not null;
            var level = levels.GetValueOrDefault(road.Way);
            finding.Ask(level is null ? !off : level.Layer == road.Layer && level.Structure == road.Structure && level.Under == road.Under, () => $"w{road.Way}");
        }

        return finding;
    }

    static Finding Sidewalks(Dataset data)
    {
        var finding = new Finding("a pavement drawn beside a road is counted on that road's side, and a side found by geometry has one drawn");
        var roads = Dataset.Index(data.Roads, road => road.Way);
        var drawn = data.Walk.Where(way => way.Road is not null).Select(way => (way.Road!.Value, way.Side)).ToHashSet();
        foreach (var way in data.Walk.Where(way => way.Road is not null))
        {
            finding.Ask(roads.TryGetValue(way.Road!.Value, out var road) && road.Sidewalk[way.Side == "left" ? 0 : 1].Share is not null, () => $"walk w{way.Way} beside w{way.Road} {way.Side}");
        }

        foreach (var road in data.Roads)
        {
            for (var side = 0; side < 2; side++)
            {
                if (road.Sidewalk[side].From != "geometry") continue;

                var named = side == 0 ? "left" : "right";
                finding.Ask(drawn.Contains((road.Way, named)), () => $"w{road.Way} {named}");
            }
        }

        return finding;
    }

    static Finding Arms(Dataset data)
    {
        var finding = new Finding("a junction's arms are the survey's ways at its node, each with its road's class, name, surface and lanes");
        var uses = new Dictionary<int, List<(long Way, int At, int Last)>>();
        foreach (var way in data.SurveyRoads.Values)
        {
            for (var at = 0; at < way.Nodes.Length; at++) (uses.TryGetValue(way.Nodes[at], out var list) ? list : uses[way.Nodes[at]] = []).Add((way.Id, at, way.Nodes.Length - 1));
        }

        var roads = Dataset.Index(data.Roads, road => road.Way);
        foreach (var junction in data.Junctions)
        {
            var expected = new HashSet<(long, int, int)>();
            foreach (var (way, at, last) in uses.GetValueOrDefault(data.NodeIndex[junction.Node]) ?? [])
            {
                if (at > 0) expected.Add((way, at, -1));
                if (at < last) expected.Add((way, at, +1));
            }

            finding.Ask(expected.SetEquals(junction.Arms.Select(arm => (arm.Way, arm.At, arm.Along))) && junction.Arms.Length == expected.Count, () => $"n{junction.Node} arms");
            foreach (var arm in junction.Arms)
            {
                if (!roads.TryGetValue(arm.Way, out var road)) continue;

                var (forward, backward, both) = (road.Lanes[0], road.Lanes[1], road.Lanes[2]);
                var (outward, inward) = arm.Along > 0 ? (both + forward, both + backward) : (both + backward, both + forward);
                finding.Ask(arm.Highway == road.Highway && arm.Name == road.Name && arm.Surface == road.Surface && arm.LanesOut == outward && arm.LanesIn == inward,
                    () => $"n{junction.Node} arm w{arm.Way}");
            }
        }

        return finding;
    }

    /// <summary>
    /// Every turn restriction the survey reads for a car is kept by the junction it is made at: a <c>no_</c> turn's
    /// movements forbidden, every movement but an <c>only_</c> turn's off its way forbidden. A movement along one way
    /// straight through the node is no turn off it, and is not asked.
    /// </summary>
    static Finding Restrictions(Dataset data)
    {
        var finding = new Finding("every turn the survey forbids is forbidden at its junction, and every forbidden turn says why");
        var junctions = Dataset.Index(data.Junctions, junction => junction.Node);
        foreach (var turn in data.Survey.Turns.Restrictions)
        {
            if (!junctions.TryGetValue(data.Survey.Nodes.Id[turn.Via], out var junction)) continue;

            foreach (var movement in junction.Movements)
            {
                var (from, to) = (junction.Arms[movement.From], junction.Arms[movement.To]);
                if (from.Way != turn.From || (from.Way == to.Way && movement.From != movement.To)) continue;
                if (turn.Only ? to.Way == turn.To : to.Way != turn.To) continue;

                finding.Ask(!movement.Allowed, () => $"r{turn.Relation} at n{junction.Node}: w{from.Way}→w{to.Way} allowed");
            }
        }

        foreach (var junction in data.Junctions)
        {
            foreach (var movement in junction.Movements.Where(movement => !movement.Allowed)) finding.Ask(movement.Why is not null, () => $"n{junction.Node} {movement.From}→{movement.To}");
        }

        return finding;
    }

    static Finding StopCalls(Dataset data)
    {
        var finding = new Finding("a stop's routes are the routes that call there, by OSM's relations and the timetable alike");
        var calling = new Dictionary<string, SortedSet<string>>();
        foreach (var route in data.Routes)
        {
            foreach (var stop in route.Stops.Concat(route.Platforms).Concat(route.Timetable?.Stops ?? []))
            {
                (calling.TryGetValue(stop, out var set) ? set : calling[stop] = new SortedSet<string>(StringComparer.Ordinal)).Add(Transit.Label(route.Mode, route.Ref, route.Name, route.Id));
            }
        }

        foreach (var stop in data.Stops) finding.Ask((calling.GetValueOrDefault(stop.Id) ?? []).SequenceEqual(stop.Routes ?? []), () => stop.Id);
        return finding;
    }

    static Finding HeldCrossings(Dataset data)
    {
        var finding = new Finding("a crossing a junction holds is of the kind the crossings layer reads it, and one it holds as seen is laid there");
        var kinds = Dataset.Index(data.Crossings.Where(crossing => crossing.Node is not null || crossing.Seen is not null),
            crossing => crossing.Node is { } node ? $"n{node}" : crossing.Seen!);
        foreach (var junction in data.Junctions)
        {
            foreach (var held in (junction.Held ?? []).Where(held => held.Kind is "crossing" or "crossing_signals"))
            {
                var key = held.Node is { } node ? $"n{node}" : held.Seen!;
                if (!kinds.TryGetValue(key, out var crossing))
                {
                    finding.Ask(held.Seen is null, () => $"n{junction.Node} holds {key}, which crossings does not lay");
                    continue;
                }

                finding.Ask(held.Kind == "crossing_signals" ? crossing.Kind == "signals" : crossing.Kind != "signals" && held.Value == crossing.Kind,
                    () => $"n{junction.Node} holds {key} as {held.Kind} {held.Value}, crossings says {crossing.Kind}");
            }
        }

        return finding;
    }

    static Finding Widths(Dataset data)
    {
        var finding = new Finding("a road's width is the one its source names, and no source before it in the order gives one");
        foreach (var road in data.Roads)
        {
            var (widthM, from) = Roads.Width(road.WidthTagM, road.WidthMeasuredM, road.WidthDetected, road.CarriagewayM);
            finding.Ask(road.WidthFrom == from && Math.Abs(road.WidthM - widthM) < 0.05,
                () => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"w{road.Way}: {road.WidthM} m from {road.WidthFrom}, its fields make {widthM} m from {from}"));
        }

        return finding;
    }

    static Finding Seen(Dataset data)
    {
        var finding = new Finding("a sighting a junction holds or a crossing was laid from says so, and a control decided by a sighting holds one");
        var seen = Dataset.Index(data.Seen, sighting => sighting.Id);
        var held = new Dictionary<string, long>();
        foreach (var junction in data.Junctions)
        {
            var mine = (junction.Held ?? []).Where(at => at.Seen is not null).ToArray();
            foreach (var at in mine)
            {
                held[at.Seen!] = junction.Node;
                finding.Ask(seen.TryGetValue(at.Seen!, out var sighting) && sighting.HeldBy == junction.Node, () => $"n{junction.Node} holds {at.Seen}, which says otherwise");
            }

            if (junction.ControlFrom == "seen") finding.Ask(junction.Evidence.Any(said => said.StartsWith("mly", StringComparison.Ordinal) || said.StartsWith("osmose:", StringComparison.Ordinal)), () => $"n{junction.Node} decided by no sighting");
            else finding.Ask(!junction.Evidence.Any(said => said.StartsWith("mly", StringComparison.Ordinal) || said.StartsWith("osmose:", StringComparison.Ordinal)), () => $"n{junction.Node} from {junction.ControlFrom}, a sighting in its evidence");
        }

        foreach (var sighting in data.Seen.Where(sighting => sighting.HeldBy is not null))
        {
            finding.Ask(held.GetValueOrDefault(sighting.Id) == sighting.HeldBy, () => $"{sighting.Id} says n{sighting.HeldBy} holds it");
        }

        var laid = data.Crossings.Where(crossing => crossing.Seen is not null).Select(crossing => crossing.Seen!).ToHashSet();
        foreach (var id in laid) finding.Ask(seen.TryGetValue(id, out var sighting) && sighting.Crossing == true, () => $"crossing from {id}, which says otherwise");
        foreach (var sighting in data.Seen.Where(sighting => sighting.Crossing == true)) finding.Ask(laid.Contains(sighting.Id), () => $"{sighting.Id} says it is a crossing, none laid");
        return finding;
    }

    static Finding Unmapped(Dataset data)
    {
        var finding = new Finding("an unmapped stretch is in exactly the unmapped road that lists it, and that road holds only unmapped stretches");
        var listed = new Dictionary<string, string>();
        foreach (var network in data.Unmapped)
        {
            foreach (var stretch in network.Stretches) finding.Ask(listed.TryAdd(stretch, network.Id), () => $"{stretch} in {network.Id} and {listed[stretch]}");
        }

        foreach (var stretch in data.Detected)
        {
            finding.Ask((stretch.Status == "unmapped") == (stretch.Network is not null) && listed.GetValueOrDefault(stretch.Id) == stretch.Network,
                () => $"{stretch.Id} {stretch.Status} in {stretch.Network ?? "none"}, listed in {listed.GetValueOrDefault(stretch.Id) ?? "none"}");
        }

        return finding;
    }
}
