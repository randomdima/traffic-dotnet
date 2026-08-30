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
/// <b>Everything, before the first frame.</b> A town is opened from inside the loop and the loop
/// cannot wait on a fetch, so what a run might read has to be there before the run starts. That is
/// the whole cost of it and it is worth saying plainly: the page downloads every map, not the one it
/// opens. <b>Fetching a town when it is picked is the improvement to make</b>, and it wants
/// <see cref="Game.Start"/> to be reachable from an <c>await</c> — which is a change to the menu and
/// not to this.
/// </para>
/// </remarks>
internal static class Data
{
    /// <summary>Where the files came from, relative to the page: what the project file lists into <c>manifest.txt</c>.</summary>
    const string Manifest = "manifest.txt";

    /// <summary>Every file the manifest names, into the file system, and how many that was.</summary>
    public static async Task<int> Fetch(Action<string> say)
    {
        // A relative path resolves against nothing in a page's runtime, so the page says where it is.
        using var http = new HttpClient { BaseAddress = new Uri(Runtime.WebGpu.Origin()) };
        var manifest = await http.GetStringAsync(Manifest);
        var paths = manifest.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var bytes = 0L;
        for (var at = 0; at < paths.Length; at++)
        {
            var path = paths[at].Replace('\\', '/');
            var content = await http.GetByteArrayAsync(path);
            Directory.CreateDirectory(Path.GetDirectoryName("/" + path)!);
            File.WriteAllBytes("/" + path, content);
            bytes += content.Length;

            // Often enough to watch, seldom enough that saying so is not the slow part.
            if (at % 25 == 0 || at == paths.Length - 1)
            {
                say($"fetching the town… {at + 1} of {paths.Length} files, {bytes / (1024 * 1024)} MB");
            }
        }

        return paths.Length;
    }
}
