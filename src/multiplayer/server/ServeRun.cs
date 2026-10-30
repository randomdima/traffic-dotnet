using System.Diagnostics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Multiplayer.Server;

/// <summary>
/// <b>A town served with no window</b> (<c>--serve</c>): laid off its map, stepped on the managed world in real time as
/// a windowed run keeps it (SIM-9), and handed to <see cref="TownServer"/> around every tick.
/// </summary>
/// <remarks>
/// <b>The town's pace is the server's own</b>: nobody's keys freeze, hold or hurry it, and a tick slower than the time
/// it stands for slows the town for every player alike, each following the tick its snapshots name (MP-7).
/// </remarks>
internal static class ServeRun
{
    /// <summary>How often the console is told how the town is keeping up.</summary>
    const double ReportEveryS = 10d;

    /// <summary>
    /// Where <c>dotnet publish traffic-dotnet.web.csproj -c Release</c> lays the page (and <c>qq web</c> does), from the
    /// project's root — the page a served town hands out when it is named none (MP-11).
    /// </summary>
    const string PublishedPage = "bin/web/Release/net11.0/publish/wwwroot";

    /// <param name="page">The published page to hand out, or null for the build's own where it has been published.</param>
    /// <param name="seconds">How long to serve, or nought for until interrupted.</param>
    public static int Run(string map, SimConfig config, int port, string? page, double seconds)
    {
        var host = PageAt(page, config.Multiplayer.PageIdleS);
        Console.WriteLine($"laying {map}…");
        var plan = Maps.Plan(map, config, ShippedArt.Sizes);
        using var world = new TownWorld(plan, config, served: true);
        var loop = new SimLoop<TownWorld>(world, config);
        using var server = new TownServer(world, plan, map, config, port, host);
        server.Start();
        Console.WriteLine($"serving {map} on port {server.Port}, {world.Cars.Count} cars, {world.People.Count} people — Ctrl+C stops");
        Console.WriteLine(host is null
            ? $"no page published at {Path.Combine(ProjectPaths.Root, PublishedPage)}: players join from a page served elsewhere (qq web publishes one)"
            : $"the page from {host.Root} — open http://localhost:{server.Port}/ to play");

        using var stop = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            stop.Cancel();
        };

        var clock = new SimClock(config);
        var started = Stopwatch.GetTimestamp();
        var reported = started;
        long ticksReported = 0;
        var sentReported = server.Sent;
        while (!stop.IsCancellationRequested)
        {
            if (seconds > 0 && Stopwatch.GetElapsedTime(started).TotalSeconds >= seconds) break;

            var due = clock.TicksDue();
            var from = Stopwatch.GetTimestamp();
            var ran = 0;
            for (; ran < due && clock.MayRunAnother(ran, Stopwatch.GetElapsedTime(from).TotalSeconds); ran++)
            {
                server.BeforeTick(world, loop.Tick);
                loop.Advance();
                server.AfterTick(world, loop.Tick);
            }

            clock.Unrun(due - ran);
            if (Stopwatch.GetElapsedTime(reported).TotalSeconds >= ReportEveryS)
            {
                var spanS = Stopwatch.GetElapsedTime(reported).TotalSeconds;
                var rateHz = (loop.Tick - ticksReported) / spanS;
                var seated = server.Seated;
                var sent = server.Sent;
                Console.WriteLine($"tick {loop.Tick}, {rateHz:0.0} ticks/s of {config.Sim.TickRateHz}, {seated} seated" + (seated == 0
                    ? string.Empty
                    : $" — a player {(sent.Snapshots - sentReported.Snapshots) / spanS / seated:0.0} snapshots/s, " +
                      $"{(sent.Bytes - sentReported.Bytes) / spanS / seated / 1024d:0.0} KiB/s, " +
                      $"{(sent.Bytes - sentReported.Bytes) / (double)Math.Max(1, sent.Snapshots - sentReported.Snapshots):0} B a snapshot"));
                reported = Stopwatch.GetTimestamp();
                ticksReported = loop.Tick;
                sentReported = sent;
            }

            if (ran == 0) Thread.Sleep(1);
        }

        return 0;
    }

    /// <summary>The page named, which must be there; or, named none, the build's own published page if it has one.</summary>
    static PageHost? PageAt(string? page, float idleS)
    {
        if (page is not null)
        {
            return IsPublished(page)
                ? new PageHost(page, idleS)
                : throw new ArgumentException(
                    $"--page {page}: no published page there — its index.html and its runtime in _framework/. " +
                    "A Debug build's tree is not one: what it lacks is served from the build by qq web --debug.");
        }

        var published = Path.Combine(ProjectPaths.Root, PublishedPage);
        return IsPublished(published) ? new PageHost(published, idleS) : null;
    }

    /// <summary>Whether a folder is a whole page: its <c>index.html</c>, and the runtime it boots, in its own <c>_framework</c>.</summary>
    static bool IsPublished(string page) =>
        File.Exists(Path.Combine(page, "index.html")) && Directory.Exists(Path.Combine(page, "_framework")) &&
        Directory.EnumerateFiles(Path.Combine(page, "_framework"), "dotnet.native*.wasm").Any();
}
