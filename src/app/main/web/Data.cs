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
/// map listing, the figures and every map's head — and that is the whole of what stands between a page
/// opening and a menu on it. <see cref="Art"/> fetches the catalogues and the sheets, and it is called
/// for the first town opened, after the first frame, because a menu draws glyphs and quads and not one
/// sprite. It is the difference between a page whose menu waits on one archive and a page whose menu
/// waits on a handful of small files.
/// </para>
/// <para>
/// <b>No town crosses the wire, only its map</b>: a town is laid off its map when it is opened, and a map laid to be
/// looked at is laid in code, so what a page fetches is a map's file or nothing.
/// </para>
/// <para>
/// <b>A map is a few kilobytes to hundreds of them</b>, so the papers carry only its head — the bytes the menu reads
/// (<see cref="CityGen.Map.MapHead"/>), laid at the map's own path — and the whole is unpacked over it when the map is
/// opened (<see cref="Map"/>).
/// </para>
/// </remarks>
internal static class Data
{
    /// <summary>The maps the page may ask for, against the files they are fetched from. Nothing else is in it: the art carries its own index.</summary>
    const string Manifest = "manifest.txt";

    /// <summary>Every file under <c>assets/</c>, in one archive the build wrote (<see cref="Art"/>).</summary>
    const string Pack = "assets.tar.gz";

    /// <summary>The two folders everything is laid under, which are the names the readers above look for.</summary>
    const string Towns = "towns";

    const string Assets = "assets";

    /// <summary>What a map's head is published as: the map's own path and this, which it is laid without.</summary>
    const string HeadKind = ".head";

    /// <summary>What a whole map is published as: an archive holding it under its own path.</summary>
    const string MapKind = ".tar.gz";

    /// <summary>Whether the archive has been unpacked, so a second map picked is not a second fetch.</summary>
    static bool _laid;

    /// <summary>Each map's archive, by map name, until it is unpacked and taken out.</summary>
    static readonly Dictionary<string, string> Archives = new(StringComparer.Ordinal);

    /// <summary>
    /// The few files the menu is drawn from — the figures and every map's head. <b>This is the whole of
    /// what stands between a page opening and a menu on it</b>: a handful of small files, so the wait is
    /// one round trip and not three hundred. Everything else is <see cref="Art"/>'s.
    /// </summary>
    public static async Task<int> Boot(Action<string> say)
    {
        // Both folders, and before anything asks ProjectPaths a question: it finds the root by walking
        // up for a folder holding the two of them, and in a page neither exists until this makes it.
        Directory.CreateDirectory("/" + Towns);
        Directory.CreateDirectory("/" + Assets);

        // Asked for by name and not read off a listing: what the menu draws is a fact about this code
        // and not about what the build happened to ship. **The figures are the only one of them named
        // here** — the typeface ships inside the assembly and the menu's renderer takes stand-ins for
        // the ground it does not draw (Render.TownRenderer.Ground) — and the maps' heads join them below,
        // where the listing says which maps there are.
        var papers = new List<string> { Page(Core.Config.ProjectPaths.SharedFiguresFile) };

        // **Both in one wave.** The listing and the figures do not need each other, and a page that
        // asks for the second only once the first has arrived spends two round trips on 229 bytes.
        // The listing is usually already here — the page starts it before the runtime (main.js) — and
        // a prefetch of something already in flight is nothing at all.
        Runtime.WebGpu.Prefetch($"{Manifest}\n{papers[0]}");

        var manifest = Encoding.UTF8.GetString(await Read(Manifest));
        foreach (var line in manifest.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // **A line that is not a map is not a map.** The manifest listed the art too until the art
            // became one archive, and a reader whose browser still holds that copy is a reader this
            // has to survive — every one of those lines through the reading below is a path with three
            // characters off the end of it.
            var path = line.Replace('\\', '/');
            if (!path.StartsWith(Towns + "/", StringComparison.Ordinal)) continue;

            // **A map's head comes down now**: it is a few kilobytes and the menu reads a map's description
            // straight out of it, so a page that fetched one lazily would draw its own map list against files
            // that are not there yet. The map itself waits to be opened.
            if (path.EndsWith(HeadKind, StringComparison.Ordinal))
                papers.Add(path);
            else if (path.EndsWith(MapKind, StringComparison.Ordinal))
                Archives[Path.GetFileNameWithoutExtension(Path.GetFileName(path)[..^MapKind.Length])] = path;
        }

        // The heads in one wave, for the reason the figures went out beside the listing: they are a few
        // kilobytes apiece and asked for one after the next they are one round trip each.
        if (papers.Count > 1) Runtime.WebGpu.Prefetch(string.Join('\n', papers.GetRange(1, papers.Count - 1)));

        const string saying = "reading what the menu draws…";
        await Lay(papers, saying, say);
        await Glyphs();
        return papers.Count;
    }

    /// <summary>
    /// Everything the town itself is read and drawn from — the catalogues, the variant files and the
    /// sheets — into the file system and decoded, once. <b>Called when a town is opened and not at
    /// boot</b>: nothing the menu draws is a sprite and nothing it reads is a catalogue.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One round trip for all of it.</b> The build packs <c>assets/</c> into a single archive and the
    /// browser unpacks it (<see cref="Runtime.WebGpu.Unpack"/>), which is what turned ten waves of
    /// latency into one — the files are small, so what was being waited on was never the bytes. The
    /// archive is usually here before this is called: the page starts fetching it beside the runtime.
    /// </para>
    /// <para>
    /// <b>And the sheets are decoded as one batch</b> (<see cref="Runtime.WebGpu.Decode"/>), because
    /// a loop awaiting one decode at a time is one decode at a time and the browser will do them all
    /// at once.
    /// </para>
    /// </remarks>
    public static async Task Art(Action<string> say)
    {
        if (_laid) return;

        const string saying = "laying the town's art…";
        say("unpacking the town's art…");
        var listed = await Runtime.WebGpu.Unpack(Pack);
        var batch = listed.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var sheets = new StringBuilder();
        foreach (var path in batch)
        {
            if (IsPicture(path)) sheets.Append('/').Append(path).Append('\n');
        }

        await Runtime.WebGpu.Decode(sheets.ToString(), "decoding the town's art…");
        await Lay(batch, saying, say);
        _laid = true;
    }

    /// <summary>
    /// The art, asked for while nothing is waiting for it (WEB-9), and only where nothing asked for it
    /// sooner: a run that named a map starts the archive before the runtime does (<c>main.js</c>), and
    /// a run that did not is showing a menu that draws none of it.
    /// </summary>
    /// <remarks>
    /// <b>The menu waits for nothing, and that includes an unawaited fetch.</b> Three megabytes on the
    /// same wire as the few small files a menu stands on is a menu that comes up later, so the archive
    /// is not asked for until there is something to look at.
    /// </remarks>
    public static void ExpectArt()
    {
        if (!_laid) Runtime.WebGpu.Prefetch(Pack);
    }

    /// <summary>
    /// A map's file, asked for as the map is opened so that it comes down beside the art rather than after it
    /// (WEB-9). Nothing for a map laid in code, or for one already unpacked.
    /// </summary>
    public static void ExpectMap(string map)
    {
        if (Archives.TryGetValue(map, out var archive)) Runtime.WebGpu.Prefetch(archive);
    }

    /// <summary>
    /// A map's whole file, unpacked over the head the menu was reading — once, and before anything lays the town
    /// (<see cref="CityGen.Maps.Plan"/>). Nothing for a map laid in code.
    /// </summary>
    public static async Task Map(string map, Action<string> say)
    {
        if (!Archives.Remove(map, out var archive)) return;

        say($"unpacking {map}'s map…");
        var listed = await Runtime.WebGpu.Unpack(archive);
        await Lay(listed.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            $"laying {map}'s map…", say);
    }

    /// <summary>
    /// A batch of files into the file system. <b>What is read out here is already in the page's
    /// hands</b> — prefetched or unpacked — so nothing in this loop waits on the network.
    /// </summary>
    static async Task Lay(IReadOnlyList<string> batch, string saying, Action<string> say)
    {
        // **The words and the count are one stage or they are a lie.** The bar this fills is the whole
        // archive and the one before it was the sheets alone, so a stage that only counted left the
        // decode's sentence standing over a different number — 305 of 319 under "decoding", where 319
        // is every file and 174 is every picture.
        say(saying);

        var bytes = 0L;
        for (var at = 0; at < batch.Count; at++)
        {
            var path = batch[at];
            var content = await Read(path);
            bytes += content.Length;

            var laid = "/" + (path.EndsWith(HeadKind, StringComparison.Ordinal) ? path[..^HeadKind.Length] : path);
            Directory.CreateDirectory(Path.GetDirectoryName(laid)!);
            File.WriteAllBytes(laid, content);

            // Often enough to watch, seldom enough to be free.
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
    /// Whether a file the archive holds is one the browser is to decode. <b>By extension and not by
    /// header</b>: the alternative is reading the first bytes of three hundred files to learn what the
    /// build already knew when it packed them.
    /// </summary>
    static bool IsPicture(string path) =>
        path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".png", StringComparison.OrdinalIgnoreCase);

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
}
