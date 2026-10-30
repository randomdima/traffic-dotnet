using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;

namespace TrafficSimulation.Multiplayer.Server;

/// <summary>
/// <b>One HTTP request's head</b>, read off a connection to a served town: what it asks for and the few headers either
/// answer reads. Everything else a browser says is ignored.
/// </summary>
internal sealed class RequestHead
{
    /// <summary>The longest head read before the connection is judged not to be a browser.</summary>
    const int LongestHeadBytes = 8 * 1024;

    RequestHead(string method, string target, Dictionary<string, string> headers)
    {
        Method = method;
        Target = target;
        _headers = headers;
    }

    readonly Dictionary<string, string> _headers;

    public string Method { get; }

    /// <summary>The path and query asked for, as the request line has them.</summary>
    public string Target { get; }

    public string? this[string name] => _headers.GetValueOrDefault(name);

    /// <summary>Whether it asks for a WebSocket (RFC 6455 §4.2.1).</summary>
    public bool IsUpgrade =>
        Method == "GET" && string.Equals(this["Upgrade"], "websocket", StringComparison.OrdinalIgnoreCase) &&
        this["Sec-WebSocket-Key"] is not null;

    /// <summary>
    /// The head read up to its blank line — a byte at a time, so nothing past it is taken off the stream — or null for
    /// one cut short, longer than any browser sends, or without a request line.
    /// </summary>
    public static async Task<RequestHead?> Read(NetworkStream stream, CancellationToken stop)
    {
        var buffer = new byte[LongestHeadBytes];
        var got = 0;
        while (got < buffer.Length)
        {
            if (await stream.ReadAsync(buffer.AsMemory(got, 1), stop) == 0) return null;

            got++;
            if (got >= 4 && buffer.AsSpan(got - 4, 4).SequenceEqual("\r\n\r\n"u8)) return Parse(Encoding.ASCII.GetString(buffer, 0, got));
        }

        return null;
    }

    static RequestHead? Parse(string head)
    {
        var lines = head.Split("\r\n");
        var request = lines[0].Split(' ');
        if (request.Length != 3) return null;

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.AsSpan(1))
        {
            var colon = line.IndexOf(':');
            if (colon > 0) headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
        }

        return new RequestHead(request[0], request[1], headers);
    }
}

/// <summary>
/// <b>The HTTP upgrade a browser opens a WebSocket with</b> (RFC 6455 §4.2), answered by hand over the connection's own
/// stream, and the socket made over that stream by the runtime's own framing
/// (<see cref="WebSocket.CreateFromStream(Stream, WebSocketCreationOptions)"/>). No web host stands in front of the town.
/// </summary>
internal static class WebSocketHandshake
{
    /// <summary>RFC 6455 §1.3: what the client's key is hashed with to prove the server read it.</summary>
    const string Magic = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    /// <param name="head">A head that <see cref="RequestHead.IsUpgrade"/>.</param>
    public static async Task<WebSocket> Accept(NetworkStream stream, RequestHead head, TimeSpan keepAlive, CancellationToken stop)
    {
        var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(head["Sec-WebSocket-Key"] + Magic)));
        var answer = $"HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {accept}\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(answer), stop);
        return WebSocket.CreateFromStream(stream, new WebSocketCreationOptions { IsServer = true, KeepAliveInterval = keepAlive });
    }

    /// <summary>A request nobody here answers: 400, and the connection closed.</summary>
    public static Task Refuse(NetworkStream stream, CancellationToken stop) =>
        stream.WriteAsync("HTTP/1.1 400 Bad Request\r\nConnection: close\r\nContent-Length: 0\r\n\r\n"u8.ToArray(), stop).AsTask();
}
