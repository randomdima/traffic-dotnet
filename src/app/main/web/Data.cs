using System.Text;

namespace TrafficSimulation.App.Main;

/// <summary>
/// The town's own files, fetched into the runtime's file system before anything looks for them.
/// </summary>
/// <remarks>
/// <para>
/// <b>WEB-4 — this is the whole of what a browser changes about reading an asset.</b> Everything above it —
/// the catalogues, the variant files, the town reader, the sheet decode — walks up from where the
/// binary landed to a folder holding <c>assets/</c> and <c>towns/</c>
/// (<see cref="Core.Config.ProjectPaths"/>), and in a page nothing landed anywhere. So the files are
/// laid at the root under those two names and every reader above is untouched: no second asset story,
/// no provider threaded through fifteen call sites, no path that means one thing here and another
/// there.
/// </para>
/// <para>
/// <b>The art before the first frame; a map when it is picked.</b> The art is every town's, so all of
/// it is fetched at boot. A map is one town's and the nine of them are two and a half megabytes, so
/// what <see cref="Fetch"/> lays for a map is its <em>name</em> — an empty file, which is what
/// <see cref="Core.Config.ProjectPaths.ShippedMaps"/> reads the menu's list off — and the bytes arrive
/// in <see cref="Town"/> when something asks to open it. That is the whole reason
/// <see cref="Game.Start"/> is reached from the boot's own <c>await</c> and never from inside a frame:
/// a loop cannot wait on a fetch, so the fetch happens where waiting is allowed and the frame that
/// follows finds the file already there.
/// </para>
/// </remarks>
internal static class Data
{
    /// <summary>Where the files came from, relative to the page: what the project file lists into <c>manifest.txt</c>.</summary>
    const string Manifest = "manifest.txt";

    /// <summary>
    /// What the build compresses and this unpacks — the towns, and nothing else. A <c>.town</c> is
    /// better than half zero bytes because its lane index is laid out for reading rather than for
    /// sending, so Odesa is 9.7 MB read off disk and 1.3 fetched. The art is already compressed and is
    /// fetched as it lies.
    /// </summary>
    /// <remarks>
    /// <b>Gzip, and brotli is not an option here</b>: the browser's runtime carries zlib and no brotli,
    /// so <c>BrotliStream</c> throws on this platform. A brotli town would have to be one the server
    /// marked <c>Content-Encoding</c> and the browser unwrapped before the fetch resolved — which is
    /// what <c>_framework</c> relies on, and is a fact about the host rather than about this build.
    /// </remarks>
    const string Squeezed = ".gz";

    /// <summary>The folder the plans are laid in, which is the name the readers above look for.</summary>
    const string Towns = "towns";

    /// <summary>Each map the page may open, against the file it is fetched from.</summary>
    static readonly Dictionary<string, string> Plans = [];

    /// <summary>
    /// Every file the manifest names that is not a map, into the file system, and how many that was.
    /// The maps are laid out by name alone and fetched by <see cref="Town"/>.
    /// </summary>
    public static async Task<int> Fetch(Action<string> say)
    {
        var manifest = Encoding.UTF8.GetString(await Read(Manifest));
        var paths = manifest.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Directory.CreateDirectory("/" + Towns);

        var art = new List<string>(paths.Length);
        foreach (var line in paths)
        {
            var path = line.Replace('\\', '/');
            if (!path.StartsWith(Towns + "/", StringComparison.Ordinal))
            {
                art.Add(path);
                continue;
            }

            // The name is the listing: a map with no bytes yet still appears on the menu, and asking
            // for it is what fetches it.
            var plan = path[..^Squeezed.Length];
            Plans[Path.GetFileNameWithoutExtension(plan)] = path;
            if (!File.Exists("/" + plan)) File.WriteAllBytes("/" + plan, []);
        }

        var bytes = 0L;
        for (var at = 0; at < art.Count; at++)
        {
            var content = await Read(art[at]);
            bytes += content.Length;

            Directory.CreateDirectory(Path.GetDirectoryName("/" + art[at])!);
            File.WriteAllBytes("/" + art[at], content);

            // The very bytes the fetch parked, decoded where they already are. It is why the picture
            // is made here rather than by a second pass that would have to fetch all of it again.
            if (IsPicture(art[at])) await Runtime.WebGpu.Picture("/" + art[at]);

            // Often enough to watch, seldom enough that saying so is not the slow part.
            if (at % 25 == 0 || at == art.Count - 1)
            {
                say($"fetching the town… {at + 1} of {art.Count} files, {bytes / (1024 * 1024)} MB");
            }
        }

        await Glyphs();
        return art.Count;
    }

    /// <summary>
    /// The typeface, decoded under the resource's own name rather than a path.
    /// </summary>
    /// <remarks>
    /// It is the one picture the town draws that was never fetched — it ships inside the assembly, as
    /// the desktop's does — and it still has to be a bitmap before a frame wants it, so it is made here
    /// with the rest rather than by a second arrangement somewhere else.
    /// </remarks>
    static async Task Glyphs()
    {
        using var stream = typeof(Data).Assembly.GetManifestResourceStream(Screen.GlyphSheet.Resource)
                           ?? throw new InvalidOperationException(
                               $"No embedded resource {Screen.GlyphSheet.Resource}.");
        using var whole = new MemoryStream();
        stream.CopyTo(whole);
        Runtime.WebGpu.Park(whole.ToArray());
        await Runtime.WebGpu.Picture(Screen.GlyphSheet.Resource);
    }

    /// <summary>
    /// Whether a file the manifest names is one the browser is to decode. <b>By extension and not by
    /// header</b>: the alternative is reading the first bytes of three hundred files to learn what the
    /// build already knew when it listed them.
    /// </summary>
    static bool IsPicture(string path) =>
        path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".png", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// One map's plan, in the file system by the time this returns. Idempotent: a map opened twice is
    /// fetched once, because the placeholder the name was laid as is the only empty one there is.
    /// </summary>
    public static async Task Town(string map)
    {
        if (!Plans.TryGetValue(map, out var from)) throw new FileNotFoundException(
            $"The manifest names no map '{map}'.");

        var plan = "/" + from[..^Squeezed.Length];
        if (new FileInfo(plan).Length > 0) return;

        File.WriteAllBytes(plan, Inflate(await Read(from)));
    }

    /// <summary>
    /// One file the page was served. <b>It is the page's own <c>fetch</c> and not an
    /// <c>HttpClient</c></b>: on this machine that class is itself a shim over the same call,
    /// reached through the same interop, so the whole HTTP stack — the handler pipeline, the header
    /// collections, the URI parser — was three assemblies and a megabyte of ahead-of-time code
    /// standing between this and three hundred and twenty GETs of static files beside the page.
    /// </summary>
    static async Task<byte[]> Read(string path)
    {
        var content = new byte[await Runtime.WebGpu.Grab(path)];
        Runtime.WebGpu.Take(content);
        return content;
    }

    /// <summary>The bytes a gzip stream holds. Once per map opened, so nothing here is a hot path.</summary>
    static byte[] Inflate(byte[] squeezed)
    {
        using var source = new MemoryStream(squeezed);
        using var gzip = new System.IO.Compression.GZipStream(source, System.IO.Compression.CompressionMode.Decompress);
        using var whole = new MemoryStream(squeezed.Length * 4);
        gzip.CopyTo(whole);
        return whole.ToArray();
    }
}
