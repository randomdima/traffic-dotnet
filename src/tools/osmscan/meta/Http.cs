namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>Fetches a file once and keeps it</b> in the map's sources (<see cref="Scan.Source"/>), read from there again unless asked to fetch it
/// afresh — the same bargain <see cref="Overpass"/> keeps with OSM, for every source that is not Overpass.
/// </summary>
/// <remarks>
/// A file is written beside its name and moved over it whole, so one cut off half way is never read as kept.
/// A 401 or 403 is reported and not retried: a source wanting a key is skipped, never worked round.
/// </remarks>
internal static class Http
{
    const string Agent = "traffic-dotnet-survey/3 (osm-scan meta; one city's map data, fetched by hand)";
    const int Tries = 3;
    static readonly TimeSpan BusyWait = TimeSpan.FromSeconds(15);
    static readonly TimeSpan AnswerWait = TimeSpan.FromMinutes(20);

    /// <summary>
    /// The path a file is kept at, fetched first where it is not kept or a refetch is asked for — whole, or through a
    /// <paramref name="copy"/> that keeps only the part of it wanted, as it comes.
    /// </summary>
    public static string Kept(string keptAt, string url, bool refetch, Action<Stream, Stream>? copy = null)
    {
        if (File.Exists(keptAt) && !refetch) return keptAt;

        using var http = new HttpClient { Timeout = AnswerWait };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(Agent);
        var said = "";
        for (var attempt = 0; attempt < Tries; attempt++)
        {
            try
            {
                using var asked = http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
                if (asked.IsSuccessStatusCode)
                {
                    var partial = keptAt + ".part";
                    using (var into = File.Create(partial))
                    using (var from = asked.Content.ReadAsStream())
                    {
                        if (copy is null) from.CopyTo(into);
                        else copy(from, into);
                    }

                    File.Move(partial, keptAt, overwrite: true);
                    return keptAt;
                }

                said = $"{url} answered {(int)asked.StatusCode}";
                if ((int)asked.StatusCode is 401 or 403 or 404 or 400) throw new InvalidOperationException(said);
            }
            catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException or IOException)
            {
                said = $"{url}: {failure.Message}";
            }

            Console.Error.WriteLine($"{Path.GetFileName(keptAt)}: {said}, asking again");
            Thread.Sleep(BusyWait);
        }

        throw new InvalidOperationException($"{Path.GetFileName(keptAt)}: no answer; last {said}");
    }

    /// <summary>
    /// The path a file is kept at, made first where it is not kept or a refetch is asked for — by a maker that asks
    /// the source for the pieces it needs (<see cref="Bytes"/>) and writes them as a file of its own.
    /// </summary>
    public static string Made(string keptAt, bool refetch, Action<Stream> make)
    {
        if (File.Exists(keptAt) && !refetch) return keptAt;

        var partial = keptAt + ".part";
        using (var into = File.Create(partial)) make(into);
        File.Move(partial, keptAt, overwrite: true);
        return keptAt;
    }

    /// <summary>
    /// An API's answer as text, asked with an authorization header so the key is in no URL, kept file or log. A 401
    /// or 403 is the key refused, reported and not retried; a 429 is waited out.
    /// </summary>
    public static string Text(string url, string authorization)
    {
        using var http = new HttpClient { Timeout = AnswerWait };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(Agent);
        var said = "";
        for (var attempt = 0; attempt < Tries; attempt++)
        {
            try
            {
                using var asking = new HttpRequestMessage(HttpMethod.Get, url);
                asking.Headers.TryAddWithoutValidation("Authorization", authorization);
                using var asked = http.Send(asking);
                if (asked.IsSuccessStatusCode) return asked.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                said = $"{new Uri(url).GetLeftPart(UriPartial.Path)} answered {(int)asked.StatusCode}";
                if ((int)asked.StatusCode is 401 or 403 or 404 or 400) throw new InvalidOperationException(said);
            }
            catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException or IOException)
            {
                said = $"{new Uri(url).GetLeftPart(UriPartial.Path)}: {failure.Message}";
            }

            Console.Error.WriteLine($"{said}, asking again");
            Thread.Sleep(BusyWait);
        }

        throw new InvalidOperationException($"no answer; last {said}");
    }

    /// <summary>A run of a file's bytes, asked for by range, so a piece of a file far too large to fetch is had alone.</summary>
    public static byte[] Bytes(string url, long from, long count)
    {
        using var http = new HttpClient { Timeout = AnswerWait };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(Agent);
        var said = "";
        for (var attempt = 0; attempt < Tries; attempt++)
        {
            try
            {
                using var asking = new HttpRequestMessage(HttpMethod.Get, url);
                asking.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(from, from + count - 1);
                using var asked = http.Send(asking);
                if (asked.StatusCode == System.Net.HttpStatusCode.PartialContent)
                {
                    var bytes = asked.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                    if (bytes.Length == count) return bytes;

                    said = $"{url} answered {bytes.Length} bytes of {count}";
                }
                else
                {
                    said = $"{url} answered {(int)asked.StatusCode} to a range";
                    if ((int)asked.StatusCode is 401 or 403 or 404 or 400 or 200) throw new InvalidOperationException(said);
                }
            }
            catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException or IOException)
            {
                said = $"{url}: {failure.Message}";
            }

            Console.Error.WriteLine($"{said}, asking again");
            Thread.Sleep(BusyWait);
        }

        throw new InvalidOperationException($"no answer; last {said}");
    }
}
