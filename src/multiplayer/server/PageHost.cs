using System.Net.Sockets;
using System.Text;

namespace TrafficSimulation.Multiplayer.Server;

/// <summary>
/// <b>The page a served town hands out on its own port</b> (MP-11): the published browser head, file for file, so one
/// address is both the page and the town it joins: every file carries <see cref="ServedTown"/>, and a page that sees it
/// opens on the lobby, which joins this very host once the player has picked a name and a colour (MP-15).
/// </summary>
/// <remarks>
/// <para>
/// <b>It serves what <c>dotnet publish</c> wrote and nothing else</b> (WEB-7): no file outside the folder, no listing,
/// GET and HEAD only, the connection kept for the next request until it idles. <b>The framework's brotli copies are sent
/// in their place</b> where the browser takes brotli, as <c>qq web --serve</c> does — the type is the uncompressed
/// file's — and the art and map archives are sent as the bytes they are, since the page unpacks them itself.
/// </para>
/// <para>
/// <b>A player joining again downloads nothing that has not changed</b>. A framework file named by a hash of its
/// contents is the same file for as long as it has that name, and is kept for good; everything else — the page,
/// <c>dotnet.js</c> that names those files, the art, the maps — is kept and asked about again each time, and answered
/// <c>304</c> while its tag is the one the browser holds, so a browser holding the last build's <c>dotnet.js</c> is told
/// of this one's.
/// </para>
/// </remarks>
internal sealed partial class PageHost(string root, float idleS)
{
    const string Brotli = ".br";

    /// <summary>
    /// The header every file of the page is handed out with, which is how the page knows a town handed it out and opens on
    /// the lobby joining its own host (<c>lobby.js</c>, MP-15).
    /// </summary>
    internal const string ServedTown = "X-Served-Town";

    readonly string _root = Path.GetFullPath(root) + Path.DirectorySeparatorChar;

    public string Root => _root;

    /// <summary>How long a connection is kept for its next request.</summary>
    public TimeSpan Idle => TimeSpan.FromSeconds(idleS);

    /// <summary>
    /// <b>One request answered</b>, and whether the connection may be kept for another: not where the browser asked for
    /// it to close.
    /// </summary>
    public async Task<bool> Answer(NetworkStream stream, RequestHead head, CancellationToken stop)
    {
        var keep = !string.Equals(head["Connection"], "close", StringComparison.OrdinalIgnoreCase);
        await AnswerOne(stream, head, keep, stop);
        return keep;
    }

    async Task AnswerOne(NetworkStream stream, RequestHead head, bool keep, CancellationToken stop)
    {
        if (head.Method is not ("GET" or "HEAD"))
        {
            await Say(stream, "405 Method Not Allowed", keep, stop);
            return;
        }

        var query = head.Target.IndexOf('?');
        var path = Uri.UnescapeDataString(query < 0 ? head.Target : head.Target[..query]);
        var relative = path.TrimStart('/') is { Length: > 0 } rest ? rest : "index.html";
        var file = Path.GetFullPath(Path.Combine(_root, relative));
        if (!file.StartsWith(_root, StringComparison.Ordinal) || !File.Exists(file))
        {
            await Say(stream, "404 Not Found", keep, stop);
            return;
        }

        var squeezed = TakesBrotli(head) && File.Exists(file + Brotli);
        var sent = new FileInfo(squeezed ? file + Brotli : file);
        var tag = $"\"{sent.Length:x}-{sent.LastWriteTimeUtc.Ticks:x}{(squeezed ? "-br" : string.Empty)}\"";
        var cached = $"Cache-Control: {(IsFingerprinted(relative) ? "public, max-age=31536000, immutable" : "no-cache")}\r\n" +
                     $"ETag: {tag}\r\nVary: Accept-Encoding\r\n{ServedTown}: 1\r\n";
        if (head["If-None-Match"] is { } held && held.Split(',').Any(each => each.Trim() == tag))
        {
            await Write(stream, $"HTTP/1.1 304 Not Modified\r\n{cached}{Kept(keep)}\r\n", stop);
            return;
        }

        await Write(
            stream,
            $"HTTP/1.1 200 OK\r\nContent-Type: {TypeOf(file)}\r\nContent-Length: {sent.Length}\r\n" +
            (squeezed ? "Content-Encoding: br\r\n" : string.Empty) + cached + Kept(keep) + "\r\n",
            stop);
        if (head.Method == "HEAD") return;

        await using var body = sent.OpenRead();
        await body.CopyToAsync(stream, stop);
    }

    /// <summary>The connection header for a connection kept for the next request, or closed after this one.</summary>
    string Kept(bool keep) => keep ? $"Connection: keep-alive\r\nKeep-Alive: timeout={(int)idleS}\r\n" : "Connection: close\r\n";

    /// <summary>
    /// A framework file named by a hash of its contents — <c>System.Linq.wb2117b3pc.wasm</c> — as <c>dotnet publish</c>
    /// names every one but <c>dotnet.js</c>, the file that names them.
    /// </summary>
    static bool IsFingerprinted(string relative) =>
        relative.StartsWith("_framework/", StringComparison.Ordinal) && Fingerprinted().IsMatch(relative);

    [System.Text.RegularExpressions.GeneratedRegex(@"\.[0-9a-z]{10}\.(wasm|js|dat|json)(\.br)?$")]
    private static partial System.Text.RegularExpressions.Regex Fingerprinted();

    static bool TakesBrotli(RequestHead head) =>
        head["Accept-Encoding"] is { } offered &&
        offered.Split(',').Any(token => token.Split(';')[0].Trim() == "br");

    static string TypeOf(string file) => Path.GetExtension(file).ToLowerInvariant() switch
    {
        ".html" => "text/html; charset=utf-8",
        ".js" or ".mjs" => "text/javascript",
        ".wasm" => "application/wasm",
        ".json" => "application/json",
        ".css" => "text/css",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".svg" => "image/svg+xml",
        ".ico" => "image/x-icon",
        ".txt" => "text/plain; charset=utf-8",
        ".gz" => "application/gzip",
        _ => "application/octet-stream",
    };

    Task Say(NetworkStream stream, string status, bool keep, CancellationToken stop) =>
        Write(stream, $"HTTP/1.1 {status}\r\nContent-Length: 0\r\n{Kept(keep)}\r\n", stop);

    static Task Write(NetworkStream stream, string text, CancellationToken stop) =>
        stream.WriteAsync(Encoding.ASCII.GetBytes(text), stop).AsTask();
}
