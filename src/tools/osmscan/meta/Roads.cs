using System.Globalization;

namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>Every survey road with everything the other layers know of it</b>: what it is for — a street, a link, or a way
/// about a zone such as a car park's aisle — its pavements each side and whether they are on it or drawn apart, its
/// width as tagged, as its lanes make it and as measured off a mapped surface, how far the buildings stand back each
/// side, its level, the ground's height and grade along it, its street parking, its trams and routes, and its
/// crossings and calming.
/// </summary>
/// <remarks>
/// <para>
/// <b>A road is a zone's way rather than a street</b> where it is a car park's aisle, or a service road running
/// mostly inside a car park, fuel station, garage block, bus station or services, or inside any other zone that
/// holds roads (<see cref="Zones"/>): those are the roads a town lays as the ground of a place and not as a route.
/// </para>
/// <para>
/// <b>A pavement is OSM's where OSM says</b>, tagged on the road or drawn apart beside it (<see cref="Walk"/>). Most
/// of the town's streets say neither, so a street whose buildings stand back from its carriageway with room for a
/// pavement is given one as <c>likely</c> — an inference the layer names as one, never a mapped pavement.
/// </para>
/// <para>
/// <b>A measured width is a mapped surface's</b>: where a mapper outlined the carriageway (<c>area:highway</c> of a
/// road's kind, not a footway's or an island's), the surface is crossed square to the road every few metres and the
/// median taken. A frontage is how far the
/// nearest building face stands off the road's line each side, crossed the same way.
/// </para>
/// </remarks>
internal static class Roads
{
    const double StepM = 10;

    /// <summary>The farthest a building face is looked for off a road's line.</summary>
    const double FrontageReachM = 40;

    /// <summary>The share of a road's samples that must find a pavement drawn apart for that side to have one.</summary>
    const double SeparateShare = 0.5;

    /// <summary>A side tagged drawn apart that finds less than this share drawn beside it has its pavement not found.</summary>
    const double UnfoundShare = 0.2;

    /// <summary>The farthest a width tag stands off a mapped surface's width, as a share of it, and still agrees.</summary>
    public const double WidthShare = 0.3;

    static readonly HashSet<string> ZoneKinds = ["parking", "fuel", "car_wash", "charging_station", "bus_station", "garages", "services"];

    public static void Lay(
        Town town, ZonesFound zones, WalkFound walk, BuildingsFound buildings, Dictionary<long, LevelRecord> levels, TransitFound transit,
        List<CrossingRecord> crossings, Dictionary<long, DetectedWidth> detected, (Dem Model, string From)? ground, Flags flags, string into, List<Written> written)
    {
        // A carriageway's surface, never a footway's, an island's or a square's a road happens to cross.
        var surfaces = town.Extract.Areas.Where(area => area.Tag("area:highway") is not ("footway" or "pedestrian" or "traffic_island" or "cycleway" or "crossing" or "path" or "steps" or "sidewalk" or "platform"))
            .Select(area => (Area: area, Ring: area.Nodes.Select(node => town.NodeM[node]).ToArray())).ToArray();
        var surfaceGrid = new Grid(50);
        for (var at = 0; at < surfaces.Length; at++) surfaceGrid.Add(at, Box.Of(surfaces[at].Ring));

        var buildingGrid = new Grid(50);
        for (var at = 0; at < buildings.Outlines.Count; at++) buildingGrid.Add(at, buildings.Outlines[at].Bounds);

        var heights = ground is { } model ? Levels.Heights(town, model.Model) : null;
        var edits = Element.Read(town.Fetched.Edits, 'w').ToDictionary(edit => edit.Id, edit => edit.Tags);
        var crossingsOn = crossings.GroupBy(crossing => crossing.Road).ToDictionary(group => group.Key, group => group.ToArray());
        var calmingOn = new Dictionary<int, List<string>>();
        foreach (var (node, tags) in town.NodeTags)
        {
            if (!tags.TryGetValue("traffic_calming", out var calming) || town.Uses[node] is not { } uses) continue;

            foreach (var (road, _) in uses) (calmingOn.TryGetValue(road, out var list) ? list : calmingOn[road] = []).Add($"n{town.NodeId(node)} {calming}");
        }

        var records = new List<RoadRecord>(town.Roads.Length);
        for (var road = 0; road < town.Roads.Length; road++)
        {
            var way = town.Roads[road];
            var tags = way.Tags;
            var line = town.RoadLineM[road];
            var carriageway = way.Carriageway!;
            var zone = zones.ZoneOf.TryGetValue(road, out var held) ? held : default;
            var (measuredM, leftM, rightM, fronted) = Measure(town, road, surfaces, surfaceGrid, buildings.Outlines, buildingGrid);
            var middle = Shape.Along(line, town.RoadLengthM[road] / 2);
            var district = zones.Districts.Where(d => d.Outline.Holds(middle)).OrderByDescending(d => d.Level).Select(d => d.Name).FirstOrDefault();
            var level = levels.GetValueOrDefault(way.Id);
            var height = heights?.GetValueOrDefault(road);
            var onRoad = crossingsOn.GetValueOrDefault(way.Id) ?? [];
            var widthTagM = Metres(tags.GetValueOrDefault("width") ?? tags.GetValueOrDefault("width:carriageway"));
            if (widthTagM is { } taggedM && measuredM is { } surfaceM && Math.Abs(taggedM - surfaceM) > WidthShare * surfaceM)
            {
                flags.Raise("width_tag_and_surface_disagree", town, middle, string.Create(CultureInfo.InvariantCulture, $"width tagged {taggedM} m, its mapped surface {surfaceM:F1} m across"), $"w{way.Id}");
            }

            if (detected.GetValueOrDefault(way.Id) is { Across: 1 } seen && seen.Share >= MlRoads.WeighedShare && Role(tags, zone.Kind) == "street"
                && (seen.WidthM > carriageway.WidthM + MlRoads.WiderM || seen.WidthM < carriageway.WidthM - MlRoads.NarrowerM))
            {
                flags.Raise("lanes_and_detected_width_disagree", town, middle,
                    string.Create(CultureInfo.InvariantCulture, $"its lanes make it {carriageway.WidthM:F1} m across, imagery reads {seen.WidthM:F1} m"), $"w{way.Id}");
            }

            foreach (var (side, sign) in (ReadOnlySpan<(string, int)>)[("left", -1), ("right", +1)])
            {
                if (Tagged(tags, side) is { } said && Disagrees(said, walk.Covered.GetValueOrDefault((road, sign))) is { } why)
                {
                    flags.Raise("sidewalk_tag_and_drawing_disagree", town, middle, $"{side}: {why}", $"w{way.Id}");
                }
            }

            var (widthM, widthFrom) = Width(widthTagM, measuredM, detected.GetValueOrDefault(way.Id), carriageway.WidthM);
            records.Add(new RoadRecord
            {
                Way = way.Id,
                Highway = tags["highway"],
                Role = Role(tags, zone.Kind),
                Name = tags.GetValueOrDefault("name"),
                NameEn = tags.GetValueOrDefault("name:en"),
                Ref = tags.GetValueOrDefault("ref"),
                LengthM = Math.Round(town.RoadLengthM[road], 1),
                Lanes = [carriageway.Count(CityGen.Traced.OsmLaneWay.Forward), carriageway.Count(CityGen.Traced.OsmLaneWay.Backward), carriageway.Count(CityGen.Traced.OsmLaneWay.Both)],
                CarriagewayM = Math.Round(carriageway.WidthM, 1),
                WidthM = widthM,
                WidthFrom = widthFrom,
                WidthTagM = widthTagM,
                WidthMeasuredM = measuredM is { } m ? Math.Round(m, 1) : null,
                WidthDetected = detected.GetValueOrDefault(way.Id),
                FrontageM = fronted ? [leftM is { } l ? Math.Round(l, 1) : null, rightM is { } r ? Math.Round(r, 1) : null] : null,
                Sidewalk =
                [
                    Sidewalk(tags, "left", walk.Covered.GetValueOrDefault((road, -1)), leftM, carriageway.WidthM, Role(tags, zone.Kind)),
                    Sidewalk(tags, "right", walk.Covered.GetValueOrDefault((road, +1)), rightM, carriageway.WidthM, Role(tags, zone.Kind)),
                ],
                Parking = Parking(tags),
                Zone = zone.Id,
                ZoneKind = zone.Kind,
                ZoneSub = zone.Sub,
                LandUse = zones.LandUseOf.GetValueOrDefault(road),
                District = district,
                Layer = level?.Layer ?? 0,
                Structure = level?.Structure,
                Under = level?.Under,
                HeightM = height is { } h ? [.. h.HeightM.Select(value => double.IsNaN(value) ? (double?)null : Math.Round(value, 1))] : null,
                HeightLaid = height is { Laid: true } ? true : null,
                GradePct = height is { } g && !double.IsNaN(g.GradePct) ? Math.Round(g.GradePct, 1) : null,
                SteepestPct = height is { } s && !double.IsNaN(s.SteepestPct) ? Math.Round(s.SteepestPct, 1) : null,
                Surface = tags.GetValueOrDefault("surface"),
                Smoothness = tags.GetValueOrDefault("smoothness"),
                Lit = tags.GetValueOrDefault("lit"),
                Maxspeed = tags.GetValueOrDefault("maxspeed"),
                Access = tags.GetValueOrDefault("motor_vehicle") ?? tags.GetValueOrDefault("vehicle") ?? tags.GetValueOrDefault("access"),
                TrolleyWire = tags.GetValueOrDefault("trolley_wire") ?? tags.GetValueOrDefault("trolleywire"),
                Tram = transit.TramOn.TryGetValue(way.Id, out var tracks) ? [.. tracks] : null,
                Routes = transit.RoutesOn.TryGetValue(way.Id, out var routes) ? [.. routes.Distinct().Order(StringComparer.Ordinal)] : null,
                Crossings = onRoad.Length > 0 ? onRoad.GroupBy(crossing => crossing.Kind).ToDictionary(group => group.Key, group => group.Count()) : null,
                Calming = calmingOn.TryGetValue(road, out var calm) ? [.. calm] : null,
                Version = edits.TryGetValue(way.Id, out var edit) && int.TryParse(edit.GetValueOrDefault("version"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var version) ? version : null,
                EditedOn = edit?.GetValueOrDefault("edited") is { Length: >= 10 } edited ? edited[..10] : null,
                CheckedOn = tags.GetValueOrDefault("check_date") ?? tags.GetValueOrDefault("survey:date"),
            });
        }

        var streets = records.Where(record => record.Role == "street").ToArray();
        written.Add(Layers.Write(into, "roads",
            "Every survey road with what the other layers know of it: its role (street, link, driveway, a zone's way), lanes and widths — as tagged, as its lanes make it, as measured off a mapped surface and as read off imagery — building frontage each side, pavements each side, street parking, zone, land use, district, level, the ground's height at each node and the grade, surface, light, speed, trams, routes, crossings and calming.",
            ["survey", "osm-zones", "osm-walk", "osm-buildings", "osm-transit", "osm-districts", "ml-roads.geojsonl", ground?.From ?? "dem"], records,
            new
            {
                roles = records.GroupBy(record => record.Role).OrderByDescending(g => g.Count()).ToDictionary(g => g.Key, g => g.Count()),
                roleKm = records.GroupBy(record => record.Role).OrderByDescending(g => g.Sum(r => r.LengthM)).ToDictionary(g => g.Key, g => Math.Round(g.Sum(r => r.LengthM) / 1000, 1)),
                streetSideKmBySidewalk = streets.SelectMany(record => record.Sidewalk.Select(side => (side.Status, record.LengthM)))
                    .GroupBy(pair => pair.Status).ToDictionary(g => g.Key, g => Math.Round(g.Sum(pair => pair.LengthM) / 1000, 1)),
                widthMeasured = records.Count(record => record.WidthMeasuredM is not null),
                widthTagged = records.Count(record => record.WidthTagM is not null),
                widthDetected = records.Count(record => record.WidthDetected is not null),
                streetKmByWidthFrom = streets.GroupBy(record => record.WidthFrom).OrderByDescending(g => g.Sum(r => r.LengthM)).ToDictionary(g => g.Key, g => Math.Round(g.Sum(r => r.LengthM) / 1000, 1)),
                heightsFrom = ground?.From,
                withFrontage = records.Count(record => record.FrontageM is not null),
                withHeights = records.Count(record => record.HeightM is not null),
                steeperThan8Pct = records.Count(record => record.SteepestPct > 8),
                withTram = records.Count(record => record.Tram is not null),
                withRoutes = records.Count(record => record.Routes is not null),
                withStreetParking = records.Count(record => record.Parking is not null),
                streetKmByYearEdited = streets.Where(record => record.EditedOn is not null).GroupBy(record => record.EditedOn![..4]).OrderBy(g => g.Key, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => Math.Round(g.Sum(r => r.LengthM) / 1000, 1)),
                withCheckDate = records.Count(record => record.CheckedOn is not null),
            }));
    }

    /// <summary>
    /// A road's width kerb to kerb, one answer, off the first source that gives one: its tag, the mapper's word; its
    /// mapped surface, OSM's own outline; the surface read off imagery along over half of it and under it alone, older
    /// than OSM and blind to kerbs under parked cars; else what its lanes make it, every lane at its class's width.
    /// </summary>
    public static (double WidthM, string From) Width(double? tagM, double? surfaceM, DetectedWidth? imagery, double lanesM) =>
        tagM is { } tag ? (tag, "tag")
        : surfaceM is { } surface ? (Math.Round(surface, 1), "surface")
        : imagery is { Across: 1 } seen && seen.Share >= MlRoads.WeighedShare ? (seen.WidthM, "imagery")
        : (Math.Round(lanesM, 1), "lanes");

    /// <summary>What a road is for: a <c>street</c>, a <c>link</c>, a <c>driveway</c>, an <c>alley</c>, a <c>track</c>, a
    /// <c>busway</c>, or a <c>zone_way</c> — the ground of a place rather than a route through the town.</summary>
    static string Role(Dictionary<string, string> tags, string? zoneKind)
    {
        var highway = tags["highway"];
        var service = tags.GetValueOrDefault("service");
        if (service == "parking_aisle" || (highway == "service" && zoneKind is not null && (ZoneKinds.Contains(zoneKind) || service is null))) return "zone_way";
        if (service is "driveway" or "drive-through") return "driveway";
        if (service == "alley") return "alley";
        if (highway == "track") return "track";
        if (highway is "busway" or "bus_guideway") return "busway";
        return highway.EndsWith("_link", StringComparison.Ordinal) ? "link" : "street";
    }

    /// <summary>
    /// A side's pavement: its tag (OSM's <c>sidewalk</c> scheme), else one drawn apart along most of it, else
    /// <c>likely</c> where a town street has a building face standing back from its carriageway with room for one,
    /// else unknown. The room is the frontage less half the carriageway, wherever a face was found.
    /// </summary>
    static SidewalkSide Sidewalk(Dictionary<string, string> tags, string side, double share, double? frontageM, double carriagewayM, string role)
    {
        var roomM = frontageM - (carriagewayM / 2);
        var likely = roomM >= PavementRoomM && frontageM <= LikelyFrontageM && role == "street"
                     && tags["highway"] is "trunk" or "primary" or "secondary" or "tertiary" or "unclassified" or "residential";
        var read = Read(tags, side, share);
        return read.Status == "unknown" && likely
            ? new SidewalkSide { Status = "likely", From = "frontage", Share = read.Share, RoomM = Math.Round(roomM!.Value, 1) }
            : new SidewalkSide { Status = read.Status, From = read.From, Share = read.Share, RoomM = roomM is { } room ? Math.Round(room, 1) : null };
    }

    /// <summary>The narrowest room between a carriageway and a building face that holds a pavement a pram passes along.</summary>
    const double PavementRoomM = 1.5;

    /// <summary>The farthest a building face stands off a street's line and still fronts it, rather than standing back
    /// across a yard or a verge.</summary>
    const double LikelyFrontageM = 25;

    /// <summary>What a road's tags say of one side's pavement, by the side's own key, then both sides', then the old
    /// one-key scheme: <c>yes</c>, <c>no</c>, <c>separate</c> or whatever else the side's key says; null where none says.</summary>
    public static string? Tagged(Dictionary<string, string> tags, string side) =>
        tags.GetValueOrDefault($"sidewalk:{side}") ?? tags.GetValueOrDefault("sidewalk:both") ?? tags.GetValueOrDefault("sidewalk") switch
        {
            "both" or "yes" => "yes",
            "left" => side == "left" ? "yes" : "no",
            "right" => side == "right" ? "yes" : "no",
            "no" or "none" => "no",
            "separate" => "separate",
            _ => null,
        };

    /// <summary>How a side's pavement tag and the pavements drawn beside it disagree, given the share drawn; null where they do not.</summary>
    public static string? Disagrees(string tagged, double share) => tagged switch
    {
        "no" when share >= SeparateShare => "tagged none, one drawn beside",
        "yes" when share >= SeparateShare => "tagged on the road, and drawn beside it too",
        "separate" when share < UnfoundShare => "tagged drawn apart, none found beside",
        _ => null,
    };

    static SidewalkSide Read(Dictionary<string, string> tags, string side, double share)
    {
        var tagged = Tagged(tags, side);
        var status = tagged switch
        {
            null => share >= SeparateShare ? "separate" : "unknown",
            "yes" or "both" => share >= SeparateShare ? "separate" : "yes",
            "no" or "none" => "no",
            var other => other,
        };
        return new SidewalkSide
        {
            Status = status,
            From = tagged is null ? (share >= SeparateShare ? "geometry" : null) : "tag",
            Share = share > 0 ? Math.Round(share, 2) : null,
        };
    }

    /// <summary>A road's street parking each side, by the current scheme (<c>parking:left</c>…) or the old (<c>parking:lane:left</c>…).</summary>
    static string?[]? Parking(Dictionary<string, string> tags)
    {
        string? Side(string side)
        {
            var place = tags.GetValueOrDefault($"parking:{side}") ?? tags.GetValueOrDefault("parking:both");
            var orientation = tags.GetValueOrDefault($"parking:{side}:orientation") ?? tags.GetValueOrDefault("parking:both:orientation");
            var old = tags.GetValueOrDefault($"parking:lane:{side}") ?? tags.GetValueOrDefault("parking:lane:both");
            return place is not null ? (orientation is null ? place : $"{place} {orientation}") : old;
        }

        string?[] sides = [Side("left"), Side("right")];
        return sides.Any(side => side is not null) ? sides : null;
    }

    /// <summary>
    /// The median width of the mapped surface a road runs in, and the median distance off its line to the nearest
    /// building face each side, crossing square to it every <see cref="StepM"/>.
    /// </summary>
    static (double? WidthM, double? LeftM, double? RightM, bool Fronted) Measure(
        Town town, int road, (CityGen.Traced.OsmWay Area, Pt[] Ring)[] surfaces, Grid surfaceGrid, List<Outline> buildings, Grid buildingGrid)
    {
        var line = town.RoadLineM[road];
        var lengthM = town.RoadLengthM[road];
        var widths = new List<double>();
        var lefts = new List<double>();
        var rights = new List<double>();
        var samples = 0;
        var near = new HashSet<int>();
        for (var alongM = Math.Min(StepM / 2, lengthM / 2); alongM <= lengthM; alongM += StepM)
        {
            samples++;
            var at = Shape.Along(line, alongM);
            var run = Shape.Along(line, Math.Min(lengthM, alongM + 1)) - Shape.Along(line, Math.Max(0, alongM - 1));
            if (run.Length < 1e-6) continue;

            run *= 1 / run.Length;
            var right = new Pt(-run.Y, run.X);

            near.Clear();
            surfaceGrid.Near(Box.Empty.With(at), near);
            foreach (var surface in near)
            {
                var ring = surfaces[surface].Ring;
                if (!Shape.Inside(at, [ring])) continue;

                if (Ray(at, right, [ring], FrontageReachM) is { } r && Ray(at, right * -1, [ring], FrontageReachM) is { } l) widths.Add(r + l);
                break;
            }

            near.Clear();
            buildingGrid.Near(Box.Empty.With(at).Grown(FrontageReachM), near);
            var rings = near.SelectMany(building => buildings[building].RingsM).ToArray();
            if (Ray(at, right, rings, FrontageReachM) is { } toRight) rights.Add(toRight);
            if (Ray(at, right * -1, rings, FrontageReachM) is { } toLeft) lefts.Add(toLeft);
        }

        return (Median(widths), Median(lefts), Median(rights), lefts.Count + rights.Count > 0);

        static double? Median(List<double> values) => values.Count == 0 ? null : values.Order().ElementAt(values.Count / 2);
    }

    /// <summary>How far a ray runs from a place before it meets an edge of any ring, within a reach.</summary>
    static double? Ray(Pt from, Pt direction, IEnumerable<Pt[]> rings, double reachM)
    {
        double? nearest = null;
        foreach (var ring in rings)
        {
            for (var at = 0; at + 1 < ring.Length; at++)
            {
                var edge = ring[at + 1] - ring[at];
                var denominator = Pt.Cross(direction, edge);
                if (Math.Abs(denominator) < 1e-12) continue;

                var t = Pt.Cross(ring[at] - from, edge) / denominator;
                var s = Pt.Cross(ring[at] - from, direction) / denominator;
                if (t > 0 && t <= reachM && s >= 0 && s <= 1 && (nearest is null || t < nearest)) nearest = t;
            }
        }

        return nearest;
    }

    static double? Metres(string? value)
    {
        if (value is null) return null;

        var text = value.Trim().Replace(',', '.');
        if (text.EndsWith(" m", StringComparison.Ordinal)) text = text[..^2];
        else if (text.EndsWith('m')) text = text[..^1];
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var metres) && metres > 0 ? metres : null;
    }
}

internal sealed class RoadRecord
{
    public required long Way { get; init; }

    public required string Highway { get; init; }

    /// <summary><c>street</c>, <c>link</c>, <c>driveway</c>, <c>alley</c>, <c>track</c>, <c>busway</c> or <c>zone_way</c>.</summary>
    public required string Role { get; init; }

    public string? Name { get; init; }

    public string? NameEn { get; init; }

    public string? Ref { get; init; }

    public required double LengthM { get; init; }

    /// <summary>Lanes along the way as drawn, against it, and both ways, as OSM's tags mean them.</summary>
    public required int[] Lanes { get; init; }

    /// <summary>The carriageway's width as its lanes make it.</summary>
    public required double CarriagewayM { get; init; }

    /// <summary>Its width kerb to kerb, one answer (<see cref="Roads.Width"/>); the fields below are the evidence.</summary>
    public required double WidthM { get; init; }

    /// <summary><c>tag</c>, <c>surface</c>, <c>imagery</c> or <c>lanes</c>: which of its widths <see cref="WidthM"/> is.</summary>
    public required string WidthFrom { get; init; }

    public double? WidthTagM { get; init; }

    /// <summary>The median width of the mapped carriageway surface it runs in.</summary>
    public double? WidthMeasuredM { get; init; }

    /// <summary>Its paved width as read off imagery (<see cref="MlRoads"/>), kerb to kerb across every carriageway under it.</summary>
    public DetectedWidth? WidthDetected { get; init; }

    /// <summary>The median distance to the nearest building face, left and right of the road as drawn.</summary>
    public double?[]? FrontageM { get; init; }

    /// <summary>Its pavements, left and right of the road as drawn.</summary>
    public required SidewalkSide[] Sidewalk { get; init; }

    /// <summary>Its street parking, left and right, as OSM's parking scheme says.</summary>
    public string?[]? Parking { get; init; }

    /// <summary>The zone it is one of the ways about, by zone id.</summary>
    public string? Zone { get; init; }

    public string? ZoneKind { get; init; }

    public string? ZoneSub { get; init; }

    public string? LandUse { get; init; }

    public string? District { get; init; }

    public required int Layer { get; init; }

    public string? Structure { get; init; }

    /// <summary>The building it passes under, by building id.</summary>
    public string? Under { get; init; }

    /// <summary>The ground's height at each of its nodes, in order.</summary>
    public double?[]? HeightM { get; init; }

    /// <summary>Whether its inner heights were laid straight between its ends, it being a bridge or tunnel.</summary>
    public bool? HeightLaid { get; init; }

    /// <summary>Its grade end to end, positive climbing along the way as drawn.</summary>
    public double? GradePct { get; init; }

    /// <summary>Its steepest grade over 60 m — two of the height model's posts — either way; none on a road shorter.</summary>
    public double? SteepestPct { get; init; }

    public string? Surface { get; init; }

    public string? Smoothness { get; init; }

    public string? Lit { get; init; }

    public string? Maxspeed { get; init; }

    public string? Access { get; init; }

    public string? TrolleyWire { get; init; }

    /// <summary>The tram tracks running down it, by OSM id.</summary>
    public long[]? Tram { get; init; }

    public string[]? Routes { get; init; }

    /// <summary>Its crossings by kind.</summary>
    public Dictionary<string, int>? Crossings { get; init; }

    public string[]? Calming { get; init; }

    /// <summary>Its OSM version as of the survey's moment: how many times it was edited, splits and merges and all.</summary>
    public int? Version { get; init; }

    /// <summary>The day it was last edited as of the survey's moment — how old its tags may be, not that anyone looked again.</summary>
    public string? EditedOn { get; init; }

    /// <summary>The day a mapper says it was last checked on the ground (<c>check_date</c>, <c>survey:date</c>).</summary>
    public string? CheckedOn { get; init; }
}

internal sealed class SidewalkSide
{
    /// <summary>
    /// <c>yes</c> (on the road), <c>separate</c> (drawn apart), <c>no</c>, <c>likely</c> (inferred from the room a
    /// street's buildings leave it, never mapped) or <c>unknown</c>.
    /// </summary>
    public required string Status { get; init; }

    /// <summary><c>tag</c>, <c>geometry</c> or <c>frontage</c>.</summary>
    public string? From { get; init; }

    /// <summary>The share of the road a pavement drawn apart runs beside on this side.</summary>
    public double? Share { get; init; }

    /// <summary>The room between the carriageway's edge and the nearest building face this side.</summary>
    public double? RoomM { get; init; }
}
