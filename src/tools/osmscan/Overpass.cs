using System.Text.Json;

namespace TrafficSimulation.Tools.OsmScan;

/// <summary>
/// <b>Asks Overpass, keeping every answer</b>: an answer is written to the map's sources (<see cref="Scan.Source"/>)
/// as it came and read from there again unless asked to fetch it afresh, so the survey can be rewritten without
/// asking OSM twice.
/// </summary>
/// <remarks>
/// A busy server answers 429 or 5xx, or 200 with an HTML page saying so; either is waited out and asked again,
/// of each server in turn. <b>An answer more than <see cref="StalestBase"/> behind OSM is refused</b>: a mirror
/// can stand months behind the main database and still answer at once. <b>So is one whose <c>remark</c> reports
/// a runtime error</b>: a query that ran out of time or memory is answered 200 with whatever it had by then.
/// </remarks>
internal static class Overpass
{
    static readonly string[] Servers =
    [
        "https://overpass-api.de/api/interpreter",
        "https://overpass.kumi.systems/api/interpreter",
    ];

    static readonly TimeSpan StalestBase = TimeSpan.FromDays(2);

    const string Agent = "traffic-dotnet-survey/3 (osm-scan; one city's roads, fetched by hand)";
    const int Tries = 4;
    static readonly TimeSpan BusyWait = TimeSpan.FromSeconds(20);
    static readonly TimeSpan AnswerWait = TimeSpan.FromMinutes(11);

    /// <summary>
    /// The answer kept, else asked of each server in turn — or of <paramref name="servers"/> alone — up to
    /// <paramref name="tries"/> times each.
    /// </summary>
    public static JsonDocument Answer(string keptAt, string query, bool refetch, int tries = Tries, string[]? servers = null)
    {
        if (File.Exists(keptAt) && !refetch) return JsonDocument.Parse(File.ReadAllBytes(keptAt));

        servers ??= Servers;
        using var http = new HttpClient { Timeout = AnswerWait };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(Agent);
        var said = "";
        for (var attempt = 0; attempt < tries * servers.Length; attempt++)
        {
            var server = servers[attempt % servers.Length];
            try
            {
                using var asked = http.PostAsync(server, new FormUrlEncodedContent([new("data", query)])).GetAwaiter().GetResult();
                var text = asked.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (asked.IsSuccessStatusCode && text.TrimStart().StartsWith('{'))
                {
                    var answer = JsonDocument.Parse(text);
                    var baseUtc = answer.RootElement.GetProperty("osm3s").GetProperty("timestamp_osm_base").GetDateTimeOffset();
                    var remark = answer.RootElement.TryGetProperty("remark", out var remarked) ? remarked.GetString() ?? "" : "";
                    if (DateTimeOffset.UtcNow - baseUtc <= StalestBase && !remark.Contains("error", StringComparison.OrdinalIgnoreCase))
                    {
                        File.WriteAllText(keptAt, text);
                        return answer;
                    }

                    answer.Dispose();
                    said = remark.Length > 0 ? $"{server} remarked: {remark[..Math.Min(200, remark.Length)]}" : $"{server} stands at {baseUtc:yyyy-MM-dd}";
                    Console.Error.WriteLine($"{Path.GetFileName(keptAt)}: {said}, asking again");
                    Thread.Sleep(BusyWait);
                    continue;
                }

                said = $"{server} answered {(int)asked.StatusCode}";
                if (asked.StatusCode is System.Net.HttpStatusCode.BadRequest) throw new InvalidOperationException($"{said}: {text[..Math.Min(400, text.Length)]}");
            }
            catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
            {
                said = $"{server}: {failure.Message}";
            }

            Console.Error.WriteLine($"{Path.GetFileName(keptAt)}: {said}, asking again");
            Thread.Sleep(BusyWait);
        }

        throw new InvalidOperationException($"{Path.GetFileName(keptAt)}: no answer; last {said}");
    }
}
