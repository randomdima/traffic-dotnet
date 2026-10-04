using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>The city's own public transport timetable</b> (GTFS, as Odesa's transport department publishes it, through the
/// Mobility Database's mirror), read clean: every route with its service on a weekday, a Saturday and a Sunday, its
/// stops in order and the line it is driven along — for <see cref="Transit"/> to lay onto OSM's routes and stops.
/// </summary>
/// <remarks>
/// <para>
/// <b>The feed repeats itself</b>: a stop, a trip or a call printed twice is read once, a route listed under two
/// agencies is one route of both, and a call at a stop the feed does not list is dropped. Each is counted in
/// <see cref="Timetable.Dropped"/>.
/// </para>
/// <para>
/// <b>A day is a Wednesday, a Saturday or a Sunday</b>: a trip runs on one where its service runs that day and is in
/// force on the day the feed is read. Some operators publish only a route's weekend service. A headway is the median
/// gap between trips setting out between 07:00 and 19:00.
/// </para>
/// <para>
/// The producer's own download wants a key, issued from a page behind a browser check, so its terms could not be
/// read; the mirror is public. The licence is therefore recorded as unverified.
/// </para>
/// </remarks>
internal static class Gtfs
{
    public const string Licence = "Odesa City Council transport department GTFS, via Mobility Database (mdb-2946); licence unverified";

    const string Url = "https://files.mobilitydatabase.org/mdb-2946/latest.zip";

    static readonly (string Day, string Column)[] Days = [("weekday", "wednesday"), ("saturday", "saturday"), ("sunday", "sunday")];

    public static string Fetch(Sources sources) => sources.File("gtfs.zip", Url, Licence, InForce);

    /// <summary>The first and last day any of a feed's services is in force.</summary>
    static string InForce(string kept)
    {
        using var zip = ZipFile.OpenRead(kept);
        var services = Table(zip, "calendar.txt");
        return services.Count == 0 ? "no calendar"
            : $"calendar in force {services.Select(service => service.GetValueOrDefault("start_date") ?? "").Min(StringComparer.Ordinal)} … {services.Select(service => service.GetValueOrDefault("end_date") ?? "").Max(StringComparer.Ordinal)}";
    }

    public static Timetable Read(string keptAt)
    {
        using var zip = ZipFile.OpenRead(keptAt);
        var dropped = new Dictionary<string, int>();
        void Drop(string what) => dropped[what] = dropped.GetValueOrDefault(what) + 1;

        var routes = new Dictionary<string, (Dictionary<string, string> Row, List<string> Agencies)>();
        foreach (var row in Table(zip, "routes.txt"))
        {
            var agency = row.GetValueOrDefault("agency_id");
            if (routes.TryGetValue(row["route_id"], out var held))
            {
                Drop("routes listed twice");
                if (agency is not null && !held.Agencies.Contains(agency)) held.Agencies.Add(agency);
                continue;
            }

            routes[row["route_id"]] = (row, agency is null ? [] : [agency]);
        }

        var stops = new Dictionary<string, TimetableStop>();
        foreach (var row in Table(zip, "stops.txt"))
        {
            if (!stops.TryAdd(row["stop_id"], new TimetableStop(row["stop_id"], row.GetValueOrDefault("stop_name"), Units(row["stop_lat"]), Units(row["stop_lon"])))) Drop("stops listed twice");
        }

        var trips = new Dictionary<string, Dictionary<string, string>>();
        foreach (var row in Table(zip, "trips.txt"))
        {
            if (!trips.TryAdd(row["trip_id"], row)) Drop("trips listed twice");
        }

        var today = DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var runs = new Dictionary<string, HashSet<string>>();
        var (from, to) = ("", "");
        foreach (var service in Table(zip, "calendar.txt"))
        {
            var (start, end) = (service.GetValueOrDefault("start_date") ?? "", service.GetValueOrDefault("end_date") ?? "");
            (from, to) = (from.Length == 0 || string.CompareOrdinal(start, from) < 0 ? start : from, string.CompareOrdinal(end, to) > 0 ? end : to);
            var inForce = string.CompareOrdinal(start, today) <= 0 && string.CompareOrdinal(end, today) >= 0;
            runs[service["service_id"]] = inForce ? [.. Days.Where(day => service.GetValueOrDefault(day.Column) == "1").Select(day => day.Day)] : [];
        }

        var calls = new Dictionary<string, SortedDictionary<int, (string Stop, int DepartS)>>();
        foreach (var call in Table(zip, "stop_times.txt"))
        {
            if (!stops.ContainsKey(call["stop_id"]))
            {
                Drop("calls at a stop the feed does not list");
                continue;
            }

            var list = calls.TryGetValue(call["trip_id"], out var held) ? held : calls[call["trip_id"]] = [];
            var sequence = int.Parse(call["stop_sequence"], CultureInfo.InvariantCulture);
            if (!list.TryAdd(sequence, (call["stop_id"], Seconds(call.GetValueOrDefault("departure_time") ?? call["arrival_time"])))) Drop("calls listed twice");
        }

        var shapes = new Dictionary<string, SortedDictionary<int, (int Lat, int Lon)>>();
        foreach (var point in Table(zip, "shapes.txt"))
        {
            var shape = shapes.TryGetValue(point["shape_id"], out var held) ? held : shapes[point["shape_id"]] = [];
            shape.TryAdd(int.Parse(point["shape_pt_sequence"], CultureInfo.InvariantCulture), (Units(point["shape_pt_lat"]), Units(point["shape_pt_lon"])));
        }

        foreach (var trip in trips.Values.Where(trip => !runs.ContainsKey(trip["service_id"]))) Drop("trips under a service the calendar does not list");

        var read = new List<TimetableRoute>();
        var byRoute = trips.Values.Where(trip => calls.ContainsKey(trip["trip_id"])).ToLookup(trip => trip["route_id"]);
        foreach (var (id, (row, agencies)) in routes)
        {
            var directions = new List<TimetableDirection>();
            foreach (var direction in byRoute[id].GroupBy(trip => trip.GetValueOrDefault("direction_id") ?? "0").OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                var days = new Dictionary<string, ServiceDay>();
                foreach (var (day, _) in Days)
                {
                    var starts = direction.Where(trip => runs.GetValueOrDefault(trip["service_id"])?.Contains(day) == true)
                        .Select(trip => calls[trip["trip_id"]].First().Value.DepartS).Order().ToArray();
                    if (starts.Length == 0) continue;

                    var daytime = starts.Where(start => start is >= 7 * 3600 and < 19 * 3600).ToArray();
                    var gaps = daytime.Zip(daytime.Skip(1), (a, b) => b - a).Where(gap => gap > 0).Order().ToArray();
                    days[day] = new ServiceDay
                    {
                        Trips = starts.Length,
                        First = Clock(starts[0]),
                        Last = Clock(starts[^1]),
                        HeadwayMin = gaps.Length > 0 ? Math.Round(gaps[gaps.Length / 2] / 60.0, 1) : null,
                    };
                }

                var longest = direction.MaxBy(trip => calls[trip["trip_id"]].Count)!;
                directions.Add(new TimetableDirection
                {
                    Direction = direction.Key,
                    Headsign = longest.GetValueOrDefault("trip_headsign"),
                    Days = days,
                    Stops = [.. calls[longest["trip_id"]].Values.Select(call => call.Stop)],
                    Shape = longest.GetValueOrDefault("shape_id") is { } shapeId && shapes.TryGetValue(shapeId, out var points)
                        ? [.. points.Values.SelectMany(point => (int[])[point.Lat, point.Lon])]
                        : null,
                });
            }

            read.Add(new TimetableRoute
            {
                Id = id,
                Ref = row.GetValueOrDefault("route_short_name") ?? id,
                Name = row.GetValueOrDefault("route_long_name"),
                Mode = Mode(row.GetValueOrDefault("route_type")),
                Agencies = [.. agencies],
                Directions = directions,
            });
        }

        return new Timetable { Routes = read, Stops = stops, Dropped = dropped, InForce = $"{from} … {to}" };
    }

    /// <summary>A route type as OSM's <c>route</c> names the mode, the basic types and the extended ones alike.</summary>
    static string Mode(string? type) => int.TryParse(type, out var code) ? code switch
    {
        0 or (>= 900 and < 1000) => "tram",
        11 or 800 => "trolleybus",
        3 or (>= 700 and < 800) => "bus",
        1 or 401 => "subway",
        2 or (>= 100 and < 200) => "train",
        4 or 1200 => "ferry",
        7 or 1400 => "funicular",
        _ => type!,
    } : type ?? "";

    static int Units(string degrees) => (int)Math.Round(decimal.Parse(degrees, CultureInfo.InvariantCulture) * 10_000_000m);

    /// <summary>A GTFS time — which runs past 24:00 for a trip after midnight — in seconds.</summary>
    static int Seconds(string time)
    {
        var parts = time.Trim().Split(':');
        return parts.Length == 3 ? (int.Parse(parts[0], CultureInfo.InvariantCulture) * 3600) + (int.Parse(parts[1], CultureInfo.InvariantCulture) * 60) + int.Parse(parts[2], CultureInfo.InvariantCulture) : 0;
    }

    static string Clock(int seconds) => string.Create(CultureInfo.InvariantCulture, $"{seconds / 3600:00}:{seconds / 60 % 60:00}");

    /// <summary>One file of the feed as rows by column name.</summary>
    static List<Dictionary<string, string>> Table(ZipArchive zip, string name)
    {
        var entry = zip.GetEntry(name) ?? throw new InvalidDataException($"the GTFS feed has no {name}");
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var header = Cells(reader.ReadLine() ?? "");
        var rows = new List<Dictionary<string, string>>();
        for (var line = reader.ReadLine(); line is not null; line = reader.ReadLine())
        {
            if (line.Length == 0) continue;

            var cells = Cells(line);
            var row = new Dictionary<string, string>(header.Count);
            for (var at = 0; at < header.Count && at < cells.Count; at++)
            {
                if (cells[at].Length > 0) row[header[at]] = cells[at];
            }

            rows.Add(row);
        }

        return rows;
    }

    /// <summary>One CSV line's cells (RFC 4180): a quoted cell may hold commas and doubled quotes.</summary>
    static List<string> Cells(string line)
    {
        var cells = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        for (var at = 0; at < line.Length; at++)
        {
            var c = line[at];
            if (quoted)
            {
                if (c == '"' && at + 1 < line.Length && line[at + 1] == '"') cell.Append(line[++at]);
                else if (c == '"') quoted = false;
                else cell.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',')
            {
                cells.Add(cell.ToString().Trim());
                cell.Clear();
            }
            else cell.Append(c);
        }

        cells.Add(cell.ToString().Trim());
        return cells;
    }
}

/// <summary>A timetable as read clean: its routes, its stops by id, what of the feed was read once or dropped, and the days its calendar spans.</summary>
internal sealed class Timetable
{
    public required List<TimetableRoute> Routes { get; init; }

    public required Dictionary<string, TimetableStop> Stops { get; init; }

    /// <summary>What of the feed was read once though printed more, or not read, by what, with how many.</summary>
    public required Dictionary<string, int> Dropped { get; init; }

    /// <summary>The first and last day any of its services is in force, as yyyyMMdd.</summary>
    public required string InForce { get; init; }
}

internal sealed record TimetableStop(string Id, string? Name, int Lat, int Lon);

internal sealed class TimetableRoute
{
    public required string Id { get; init; }

    public required string Ref { get; init; }

    public string? Name { get; init; }

    /// <summary>The mode as OSM's <c>route</c> names it: <c>tram</c>, <c>trolleybus</c>, <c>bus</c>.</summary>
    public required string Mode { get; init; }

    public required string[] Agencies { get; init; }

    public required List<TimetableDirection> Directions { get; init; }
}

internal sealed class TimetableDirection
{
    public required string Direction { get; init; }

    public string? Headsign { get; init; }

    /// <summary>Its service on each day it runs: <c>weekday</c>, <c>saturday</c>, <c>sunday</c>.</summary>
    public required Dictionary<string, ServiceDay> Days { get; init; }

    /// <summary>Its stops in order, by timetable stop id, off its longest trip.</summary>
    public required string[] Stops { get; init; }

    /// <summary>The line it is driven along, lat, lon pairs in 1e-7°.</summary>
    public int[]? Shape { get; init; }
}

internal sealed class ServiceDay
{
    /// <summary>Trips setting out that day.</summary>
    public required int Trips { get; init; }

    public required string First { get; init; }

    public required string Last { get; init; }

    /// <summary>The median gap between trips setting out from 07:00 to 19:00.</summary>
    public double? HeadwayMin { get; init; }
}
