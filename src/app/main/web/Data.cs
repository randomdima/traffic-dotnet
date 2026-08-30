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
/// <b>What the menu needs, and then what a town needs.</b> <see cref="Boot"/> fetches the papers — the
/// catalogues, the figures, the ground the menu is drawn over — and that is the whole of what stands
/// between a page opening and a menu on it. <see cref="Art"/> fetches the sheets, and it is called
/// when the first map is picked, because a menu draws glyphs and quads and not one sprite. It is the
/// difference between a page that waits on three hundred files and a page that waits on a hundred and
/// fifty small ones.
/// </para>
/// <para>
/// <b>And a map is fetched when it is picked.</b> The nine of them are three and a half megabytes, so
/// what <see cref="Boot"/> lays for a map is its <em>name</em> — an empty file, which is what
/// <see cref="Core.Config.ProjectPaths.ShippedMaps"/> reads the menu's list off — and the bytes arrive
/// in <see cref="Town"/> when something asks to open it. That is the whole reason
/// <see cref="Game.Start"/> is reached from the boot's own <c>await</c> and never from inside a frame:
/// a loop cannot wait on a fetch, so the fetch happens where waiting is allowed and the frame that
/// follows finds the file already there.
/// </para>
/// </remarks>
internal static class Data
{
    /// <summary>The maps the page may ask for, against the files they are fetched from. Nothing else is in it: the art carries its own index.</summary>
    const string Manifest = "manifest.txt";

    /// <summary>Every file under <c>assets/</c>, in one archive the build wrote (<see cref="Art"/>).</summary>
    const string Pack = "assets.tar.gz";

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

    /// <summary>The two folders everything is laid under, which are the names the readers above look for.</summary>
    const string Towns = "towns";

    const string Assets = "assets";

    /// <summary>Each map the page may open, against the file it is fetched from.</summary>
    static readonly Dictionary<string, string> Plans = [];

    /// <summary>Whether the archive has been unpacked, so a second map picked is not a second fetch.</summary>
    static bool _laid;

    /// <summary>
    /// The few files the menu is drawn from — the figures and the five ground surfaces — and the name
    /// of every map. <b>This is the whole of what stands between a page opening and a menu on it</b>:
    /// half a dozen files, so the wait is one round trip and not three hundred. Everything else is
    /// <see cref="Art"/>'s, and a map's own bytes are <see cref="Town"/>'s.
    /// </summary>
    public static async Task<int> Boot(Action<string> say)
    {
        // Both folders, and before anything asks ProjectPaths a question: it finds the root by walking
        // up for a folder holding the two of them, and in a page neither exists until this makes it.
        Directory.CreateDirectory("/" + Towns);
        Directory.CreateDirectory("/" + Assets);

        var manifest = Encoding.UTF8.GetString(await Read(Manifest));
        foreach (var line in manifest.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // **A line that is not a map is not a map.** The manifest listed the art too until the art
            // became one archive, and a reader whose browser still holds that copy is a reader this
            // has to survive — every one of those lines through the reading below is a path with three
            // characters off the end of it.
            var path = line.Replace('\\', '/');
            if (!path.StartsWith(Towns + "/", StringComparison.Ordinal) ||
                !path.EndsWith(Squeezed, StringComparison.Ordinal))
            {
                continue;
            }

            // The name is the listing: a map with no bytes yet still appears on the menu, and asking
            // for it is what fetches it.
            var plan = path[..^Squeezed.Length];
            Plans[Path.GetFileNameWithoutExtension(plan)] = path;
            if (!File.Exists("/" + plan)) File.WriteAllBytes("/" + plan, []);
        }

        // Asked for by name and not read off a listing: what the menu draws is a fact about this code
        // and not about what the build happened to ship. **It is one file** — the typeface ships inside
        // the assembly and the menu's renderer takes stand-ins for the ground it does not draw
        // (Render.TownRenderer.Ground), so the figures are the whole of it.
        var papers = new List<string> { Page(Core.Config.ProjectPaths.SharedFiguresFile) };

        const string saying = "reading what the menu draws…";
        say(saying);
        await Lay(papers, saying, say);
        await Glyphs();
        return papers.Count;
    }

    /// <summary>
    /// Everything the town itself is read and drawn from — the catalogues, the variant files and the
    /// sheets — into the file system and decoded, once. <b>Called when a map is picked and not at
    /// boot</b>: nothing the menu draws is a sprite and nothing it reads is a catalogue.
    /// </summary>
    /// <remarks>
    /// <b>One round trip for all of it.</b> The build packs <c>assets/</c> into a single archive and the
    /// browser unpacks it (<see cref="Runtime.WebGpu.Unpack"/>), which is what turned ten waves of
    /// latency into one — the files are small, so what was being waited on was never the bytes.
    /// </remarks>
    public static async Task Art(Action<string> say)
    {
        if (_laid) return;

        const string saying = "laying the town's art…";
        say(saying);
        var listed = await Runtime.WebGpu.Unpack(Pack);
        var batch = listed.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        await Lay(batch, saying, say);
        _laid = true;
    }

    /// <summary>
    /// A batch of files into the file system, and every picture among them decoded. <b>What is read out
    /// here is already in the page's hands</b> — warmed or unpacked — so nothing in this loop waits on
    /// the network.
    /// </summary>
    static async Task Lay(IReadOnlyList<string> batch, string saying, Action<string> say)
    {
        var bytes = 0L;
        for (var at = 0; at < batch.Count; at++)
        {
            var path = batch[at];
            var content = await Read(path);
            bytes += content.Length;

            Directory.CreateDirectory(Path.GetDirectoryName("/" + path)!);
            File.WriteAllBytes("/" + path, content);

            // The very bytes the fetch parked, decoded where they already are. It is why the picture
            // is made here rather than by a second pass that would have to fetch all of it again.
            if (IsPicture(path)) await Runtime.WebGpu.Picture("/" + path);

            // The decode is the slow half now that the fetch is one request, and it is the half the
            // bar has to be drawn against. Often enough to watch, seldom enough to be free.
            if (at % 16 == 0 || at == batch.Count - 1) Runtime.WebGpu.Progress(at + 1, batch.Count);
        }

        say($"{saying} {batch.Count} files, {bytes / (1024 * 1024)} MB");
    }

    /// <summary>
    /// A path the page can fetch, from one the readers above use. <b>They differ by a slash</b>: in a
    /// page the file system's root is where <c>assets/</c> and <c>towns/</c> sit, and a path relative to
    /// the page has no leading one.
    /// </summary>
    static string Page(string rooted) => rooted.TrimStart('/');

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
