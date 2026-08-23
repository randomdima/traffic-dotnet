using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using Silk.NET.Input;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.App.Camera;
using TrafficSimulation.App.Debug;
using TrafficSimulation.App.Hud;
using TrafficSimulation.App.Render;
using TrafficSimulation.App.Shot;
using TrafficSimulation.Bench;
using TrafficSimulation.Runtime;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Persistence;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Terrain;
using TrafficSimulation.World.Town;
using PresentModeKHR = Silk.NET.Vulkan.PresentModeKHR;

namespace TrafficSimulation.App.Main;

/// <summary>
/// The way in, and nothing more: it parses the words, reads the figures once, and hands them to
/// whichever of the four things was asked for.
/// </summary>
/// <remarks>
/// <para>
/// <b>The game itself is <see cref="Game"/></b>, which is the composition root — this only chooses
/// between it, the offscreen shot, the dependency read-out and the checks. With no map named the game
/// opens on its start menu and builds nothing (GEN-1b).
/// </para>
/// <para>
/// The loop <see cref="Game"/> runs is this engine's own and never <c>IWindow.Run</c>'s: the brief
/// fixes the order of a tick's five phases, and that sequence is the whole reason every decision in a
/// tick sees one instant of the world.
/// </para>
/// </remarks>
internal static class Program
{
    static int Main(string[] args)
    {
        var options = Options.Parse(args);

        // The only place the figures are read: everything below is handed the one instance rather
        // than reaching for a singleton.
        var config = SimConfig.Load();

        if (options.LayTrack) return LayTheTrack(config);
        if (options.Bench is not null) return RunBench(options.Bench, options.Map, config);
        if (options.Check) return RunCheck(options, config);
        if (options.Sheet is not null) return RunSheet(options, config);
        if (options.Shot is not null) return RunShot(options, config);

        // A caption is a thing said about a picture, and a windowed run takes none.
        if (options.Caption)
            throw new ArgumentException(
                "--caption, --title and --note are about a picture: take one with --shot PATH or --sheet FILE.");

        // GEN-1b: with no map named, the game opens on the start menu and builds nothing until one
        // is picked. Naming one on the command line is the same choice made earlier.
        using var game = new Game(
            config, options.Width, options.Height, options.Validate, options.UiScale, PresentMode(options.Present));

        // --ui reaches the windowed run as it reaches a shot: a measured run of a town nobody is
        // sitting in front of is exactly the run that wants the read-out switched on from the start.
        game.Switch(Wanted(options.Ui));
        return game.Run(options.Map, options.Seconds);
    }

    /// <summary>The words <c>--ui</c> was given, matched whole by whichever path is about to apply them.</summary>
    static string[] Wanted(string ui) =>
        ui.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>What <c>--present</c> names, as the mode itself. A word nobody offers is an error rather than a silent fallback.</summary>
    static PresentModeKHR PresentMode(string named) => named switch
    {
        "fifo" => PresentModeKHR.FifoKhr,
        "mailbox" => PresentModeKHR.MailboxKhr,
        "immediate" => PresentModeKHR.ImmediateKhr,
        _ => throw new ArgumentException($"Unknown present mode {named}. Takes fifo, mailbox or immediate."),
    };

    /// <summary>
    /// One frame of the same town, drawn with no window under it and written out as a PNG. The
    /// picture is <see cref="ShotRun"/>'s — the one shot path, shared with the end-to-end visual
    /// tests — and what belongs here is only the words that reached it and the census it returns.
    /// </summary>
    /// <remarks>
    /// <b><c>--ui</c> is what makes a reference frame of the interface possible without a display.</b>
    /// The panels and the layers are drawn by the same <see cref="Hud.Interface"/> the windowed game
    /// draws them with — a frame taken through a second drawing path would be a picture of that path
    /// rather than of the interface.
    /// </remarks>
    static int RunShot(Options options, SimConfig config)
    {
        var ask = new ShotRequest(
            Map: options.Map ?? Options.FixtureMap,
            Path: options.Shot!,
            WidthPx: options.Width,
            HeightPx: options.Height,
            ViewM: options.ViewM,
            AtM: options.AtM,
            Ui: Wanted(options.Ui),
            UiScale: options.UiScale,
            Seconds: options.Seconds,
            RulerPointsM: options.RulerPointsM,
            Validate: options.Validate);

        var shot = ShotRun.Take(ask, config);

        Console.WriteLine($"{shot.Map}: {shot.Path} written at {shot.WidthPx}x{shot.HeightPx}, " +
                          $"{shot.SpanM.X:F0} m across at {shot.CentreM.X:F0},{shot.CentreM.Y:F0} — " +
                          $"{shot.Triangles} triangles, {shot.Sprites} of {shot.SpriteCapacity} bodies on " +
                          $"screen at tick {shot.Tick}, {shot.InterfaceQuads} interface quads, no window");
        if (shot.Crossings > 0)
            Console.WriteLine($"{"",-9}the offscreen frame is {shot.Crossings} crossings, against a window's five");

        // The band goes on afterwards and never into the frame, so a picture asked for without it is
        // the same pixels this build has always written (SHT-1).
        if (!options.Caption) return 0;

        var whole = SheetRun.Annotate(ask, shot, options.Title, options.Note);
        Console.WriteLine($"{"",-9}captioned at {whole.WidthPx}x{whole.HeightPx}, notes in " +
                          $"{ShotNotes.NotesFor(shot.Path)}");
        return 0;
    }

    /// <summary>
    /// <b>A review sheet: several staged frames, captioned and tiled into one picture</b>, asked for as
    /// a document rather than as flags (SHT-4). The cells are the same <see cref="ShotRun"/> frames
    /// <c>--shot</c> takes — there is no second staging path and nothing here draws a town.
    /// </summary>
    /// <remarks>
    /// <c>--shot PATH</c> beside it names where the sheet goes when the document does not, and
    /// <c>--sheet -</c> reads the document off standard input, so staging a sheet needs no file left
    /// behind.
    /// </remarks>
    static int RunSheet(Options options, SimConfig config)
    {
        // The document is the only place a sheet's staging is written down. A word on the command line
        // beside it would be a second place for it to be wrong, and the loser would be the one nobody
        // could see in the picture.
        if (options.Map is not null || options.AtM is not null || options.ViewM > 0f || options.Ui.Length > 0 ||
            options.Seconds > 0 || options.RulerPointsM.Count > 0 || options.Title is not null ||
            options.Note is not null)
        {
            throw new ArgumentException(
                "A sheet stages itself: --map, --view, --at, --ui, --seconds, --rule, --title and --note belong "
                + "in the document. Only --shot PATH is read beside it, as where to write the sheet.");
        }

        var ask = SheetRequest.Read(options.Sheet!);
        var sheet = SheetRun.Take(ask, config, options.Shot);

        Console.WriteLine($"{"",-11}{sheet.Sheet} written at {sheet.WidthPx}x{sheet.HeightPx}, " +
                          $"{sheet.Cells.Length} cell(s), notes in {ShotNotes.NotesFor(sheet.Sheet)}");
        return 0;
    }

    /// <summary>
    /// <b>The proving grounds, written out as maps like any other.</b> They are the only towns this build
    /// lays itself (<see cref="TrackPlan"/>), and they are laid again from here rather than kept only as
    /// files — the shapes on them are chosen against the car's own figures, so a figure that moves is a
    /// track that has to be laid again.
    /// </summary>
    /// <remarks>
    /// <b>Both crowds, always, and never one of them.</b> The two maps are the same lap and are read against
    /// each other, so a run that laid one would leave the other measuring a road the build no longer has.
    /// </remarks>
    static int LayTheTrack(SimConfig config)
    {
        foreach (var crowd in Enum.GetValues<TrackCrowd>())
        {
            var path = ProjectPaths.TownFile(TrackPlan.NameOf(crowd));
            var plan = TrackPlan.Lay(config, crowd);
            TownWriter.WriteFile(plan, path);

            Console.WriteLine($"{plan.Name}: {path} written — {plan.WorldSizeM.X:F0}x{plan.WorldSizeM.Y:F0} m, " +
                              $"{plan.Roads.Count} roads, {plan.Spawns.Count} spawns, " +
                              $"{new FileInfo(path).Length / 1024} KiB");
        }

        return 0;
    }

    /// <summary>
    /// One of this engine's checks, by the name the menu shows for it. <b>The list is
    /// <see cref="CheckCatalogue"/>'s and there is no second one here</b>: a probe reachable from the
    /// command line and not from the menu, or the other way round, is exactly what OBS-2a forbids.
    /// </summary>
    static int RunBench(string name, string? map, SimConfig config)
    {
        if (string.Equals(name, "all", StringComparison.Ordinal))
        {
            CheckCatalogue.RunAll(config);
            return 0;
        }

        // The census is the one check that is about a particular town, so the command line's --map
        // reaches it; every other check builds the world it needs.
        if (string.Equals(name, "census", StringComparison.Ordinal))
        {
            TownCensus.Run(map ?? Options.FixtureMap, config);
            return 0;
        }

        if (CheckCatalogue.TryFind(name, out var check))
        {
            check.Run(config);
            return 0;
        }

        Console.Error.Write($"Unknown check {name}. Takes all or one of: ");
        for (var at = 0; at < CheckCatalogue.Shipped.Length; at++)
        {
            Console.Error.Write(at > 0 ? ", " : string.Empty);
            Console.Error.Write(CheckCatalogue.Shipped[at].Name);
        }

        Console.Error.WriteLine('.');
        return 1;
    }

    /// <summary>
    /// The dependency read-out: every row is a claim about the build, answered by the machine.
    /// </summary>
    static int RunCheck(Options options, SimConfig config)
    {
        var failures = 0;

        Console.WriteLine("traffic-dotnet — saying hello");
        Console.WriteLine(new string('-', 72));

        failures += Report("runtime", ReportRuntime);
        failures += Report("config", () => ReportConfig(config));
        failures += Report("shaders", ReportShaders);
        failures += Report("art", ReportArt);
        failures += Report("town", () => ReportTown(options.Map ?? Options.FixtureMap, config));
        failures += Report("physics", () => ReportPhysics(config));
        failures += Report("vulkan", () => ReportVulkan(options.Validate));

        Console.WriteLine(new string('-', 72));
        Console.WriteLine(failures == 0
            ? "All dependencies answered. Run without --check to open a town."
            : $"{failures} check(s) failed.");
        return failures == 0 ? 0 : 1;
    }

    static int Report(string name, Action check)
    {
        try
        {
            Console.Write($"{name,-9}");
            check();
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAILED — {ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }

    static void ReportRuntime()
    {
        Console.WriteLine($"{RuntimeInformation.FrameworkDescription} on {RuntimeInformation.RuntimeIdentifier}");
        Console.WriteLine($"{"",-9}server GC {GCSettings.IsServerGC}, concurrent GC {AppContext.TryGetSwitch("System.GC.Concurrent", out var c) && c}, " +
                          $"{Environment.ProcessorCount} logical cores");
        Console.WriteLine($"{"",-9}project root {ProjectPaths.Root}");
    }

    /// <summary>
    /// The .spv files are compiled by glslc from the project file and embedded, so a missing shader
    /// compiler fails the build rather than the first pipeline — this only confirms they arrived.
    /// </summary>
    static void ReportShaders()
    {
        const uint SpirVMagic = 0x07230203;

        var assembly = Assembly.GetExecutingAssembly();
        var names = Array.FindAll(assembly.GetManifestResourceNames(), n => n.EndsWith(".spv", StringComparison.Ordinal));
        Array.Sort(names);
        if (names.Length == 0) throw new InvalidOperationException("No SPIR-V embedded: did the CompileShaders target run?");

        var descriptions = new string[names.Length];
        for (var i = 0; i < names.Length; i++)
        {
            using var stream = assembly.GetManifestResourceStream(names[i])!;
            var words = new byte[4];
            stream.ReadExactly(words);
            var magic = BitConverter.ToUInt32(words);
            if (magic != SpirVMagic) throw new InvalidOperationException($"{names[i]} is not SPIR-V (magic {magic:x8})");
            descriptions[i] = $"{names[i]} {stream.Length} B";
        }

        Console.WriteLine(string.Join(", ", descriptions));
    }

    /// <summary>
    /// Decoded at startup in managed code: there is no bake step to forget.
    /// </summary>
    static void ReportArt()
    {
        var surfaces = ProjectPaths.GroundSurfaceFiles();
        var started = Stopwatch.GetTimestamp();
        var pixels = 0L;
        foreach (var path in surfaces)
        {
            using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(path);
            pixels += (long)image.Width * image.Height;
        }

        var elapsed = Stopwatch.GetElapsedTime(started);
        Console.WriteLine($"{surfaces.Length} ground surfaces, {pixels / 1_000_000d:F1} Mpx, decoded in " +
                          $"{elapsed.TotalMilliseconds:F1} ms by ImageSharp");
    }

    /// <summary>
    /// Every figure the simulation is parameterised by, read once from the shared file that owns
    /// them. What is printed is the pair the whole town is sized against and the clock everything
    /// else is timed by, so a retune is visible in the read-out rather than only in the behaviour.
    /// </summary>
    static void ReportConfig(SimConfig config)
    {
        Console.WriteLine($"{config.Car.LengthM:F1}x{config.Car.WidthM:F1} m car at {config.Car.MassKg:F0} kg, " +
                          $"{config.RoadWidthM:F1} m roads, {config.PersonDiameterM:F1} m people");
        Console.WriteLine($"{"",-9}{config.Sim.TickRateHz} Hz, decisions every {config.Sim.AgentDecisionIntervalS:F2} s, " +
                          $"turning radius {config.CarTurningRadiusM:F2} m, a walker's {config.WalkerTightestTurnM:F2} m");
    }

    /// <summary>
    /// The town as data: read from the one <c>.town</c> file all four engines are handed, laid out as
    /// arrays, classified, and triangulated. What it is made of is <c>--bench census</c>.
    /// </summary>
    static void ReportTown(string map, SimConfig config)
    {
        var started = Stopwatch.GetTimestamp();
        var plan = TownReader.ReadFile(ProjectPaths.TownFile(map));
        var read = Stopwatch.GetElapsedTime(started);

        started = Stopwatch.GetTimestamp();
        var mesh = GroundMesh.Build(plan, config);
        var laid = Stopwatch.GetElapsedTime(started);

        var ground = new TerrainGrid(plan, config).At(plan.Spawns.Count > 0 ? plan.Spawns.PositionM[0] : plan.WorldSizeM * 0.5f);

        Console.WriteLine($"{plan.Name} {plan.WorldSizeM.X:F0}x{plan.WorldSizeM.Y:F0} m read in {read.TotalMilliseconds:F1} ms — " +
                          $"{plan.Roads.Count} roads, {plan.Buildings.Count} buildings, {plan.Props.Count} props, {plan.Spawns.Count} spawns");
        Console.WriteLine($"{"",-9}ground laid as {mesh.Indices.Length / 3} triangles in {laid.TotalMilliseconds:F0} ms; " +
                          $"the first spawn stands on {ground.Ground} ({ground.Rules})");
        Console.WriteLine($"{"",-9}maps on disk: {string.Join(", ", ProjectPaths.ShippedMaps())}");
    }

    /// <summary>
    /// "The steady state allocates nothing" as a number, because it is the claim that could quietly
    /// break. No package is behind the figure any more — the solver is this project's own — which is
    /// exactly why it is still printed: a rule nobody else maintains has to be measured by its own build.
    /// </summary>
    /// <remarks>
    /// Two rows of src/bench/SolverProbe.cs's table, and the second is the one that matters: a world whose
    /// bodies never meet allocates nothing in almost any solver, so the packed row is what says the
    /// contact set turning over does not put an array growth under the tick. The whole table is
    /// <c>--bench solver</c>.
    /// </remarks>
    static void ReportPhysics(SimConfig config)
    {
        SolverProbe.WarmTheProcess(config);
        var apart = SolverProbe.Sample(config, bodyCount: 1_000, packed: false);
        var packed = SolverProbe.Sample(config, bodyCount: 1_000, packed: true);

        Console.WriteLine($"the solver allocates {apart.BytesPerStep:F1} B per step over 1 000 bodies apart and " +
                          $"{packed.BytesPerStep:F1} B packed ({packed.ContactPoints} contact points), over " +
                          $"{SolverProbe.MeasuredSteps} steps after {SolverProbe.WarmupSteps} warm-up ticks");
    }

    static void ReportVulkan(bool validate)
    {
        using var vk = Vk.Open("traffic-dotnet", validate);
        Console.WriteLine($"{vk.DeviceName}, Vulkan {vk.DeviceApiVersionText}");
    }

    readonly record struct Options(
        bool Check, bool Validate, int Width, int Height, double Seconds, string? Bench, string? Map, float ViewM,
        string? Shot, Vector2? AtM, string Ui, float UiScale, string Present, List<Vector2> RulerPointsM,
        bool LayTrack, string? Sheet, bool Caption, string? Title, string? Note)
    {
        /// <summary>
        /// What every check that is not about a particular town is staged on: it is one screen, it
        /// carries one of every kind of ground, and it opens in a fraction of the time a city does.
        /// </summary>
        public const string FixtureMap = "Test";

        /// <summary>
        /// The words the other engines use: <c>--size W H</c>, <c>--map</c> and <c>--shot</c> are
        /// <c>traffic-native</c>'s, so a command line reads the same at both. <c>--shot</c> opens no
        /// window at all; <c>--seconds</c> closes the one <c>--map</c> opens, for a run nobody is
        /// sitting in front of; <c>--view</c> opens on a named span in metres; <c>--bench</c> runs one
        /// of this engine's checks; <c>--ui-scale</c> lays the interface out at a factor of its own
        /// instead of the desktop's; <c>--present</c> is how a finished frame reaches the glass, which
        /// is what a frame rate from a windowed run means at all (<see cref="Swapchain"/>); and
        /// <c>--check</c> is the dependency read-out.
        /// </summary>
        /// <remarks>
        /// <b>The review words are their own set</b> (<see cref="SheetRequest"/>): <c>--sheet</c> takes
        /// a document instead of flags and tiles what it names into one picture, and <c>--caption</c>,
        /// <c>--title</c> and <c>--note</c> put the same band and the same notes on a single
        /// <c>--shot</c>. Naming a title or a note implies the caption, since neither is drawn anywhere
        /// else.
        /// </remarks>
        public static Options Parse(string[] args)
        {
            // No map by default, because GEN-1b says the game opens on a menu and builds nothing
            // until one is picked. Naming one is that choice made on the command line instead.
            // A zero ui scale is "ask the window", which is the desktop's own factor: naming one is
            // for the platform that reports 1 on a display nobody would call unscaled.
            var options = new Options(Check: false, Validate: false, Width: 1600, Height: 900, Seconds: 0,
                Bench: null, Map: null, ViewM: 0f, Shot: null, AtM: null, Ui: string.Empty, UiScale: 0f,
                Present: "mailbox", RulerPointsM: [], LayTrack: false, Sheet: null, Caption: false, Title: null,
                Note: null);
            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--check":
                    case "--headless":
                        options = options with { Check = true };
                        break;
                    case "--validate":
                        options = options with { Validate = true };
                        break;
                    case "--lay-track":
                        options = options with { LayTrack = true };
                        break;
                    case "--size" when i + 2 < args.Length:
                        options = options with { Width = int.Parse(args[i + 1]), Height = int.Parse(args[i + 2]) };
                        i += 2;
                        break;
                    case "--seconds" when i + 1 < args.Length:
                        options = options with { Seconds = double.Parse(args[i + 1]) };
                        i++;
                        break;
                    case "--bench" when i + 1 < args.Length:
                        options = options with { Bench = args[i + 1] };
                        i++;
                        break;
                    case "--map" when i + 1 < args.Length:
                        options = options with { Map = args[i + 1] };
                        i++;
                        break;
                    case "--view" when i + 1 < args.Length:
                        options = options with { ViewM = float.Parse(args[i + 1]) };
                        i++;
                        break;
                    case "--at" when i + 2 < args.Length:
                        options = options with { AtM = new Vector2(float.Parse(args[i + 1]), float.Parse(args[i + 2])) };
                        i += 2;
                        break;
                    // Two points per --rule, fed through the ruler's own click path rather than
                    // written into it: a tape laid any other way is a picture of a different tool.
                    case "--rule" when i + 4 < args.Length:
                        options.RulerPointsM.Add(new Vector2(float.Parse(args[i + 1]), float.Parse(args[i + 2])));
                        options.RulerPointsM.Add(new Vector2(float.Parse(args[i + 3]), float.Parse(args[i + 4])));
                        i += 4;
                        break;
                    case "--ui" when i + 1 < args.Length:
                        options = options with { Ui = args[i + 1] };
                        i++;
                        break;
                    case "--ui-scale" when i + 1 < args.Length:
                        options = options with { UiScale = float.Parse(args[i + 1]) };
                        i++;
                        break;
                    case "--shot" when i + 1 < args.Length:
                        options = options with { Shot = args[i + 1] };
                        i++;
                        break;
                    case "--present" when i + 1 < args.Length:
                        options = options with { Present = args[i + 1] };
                        i++;
                        break;
                    // A sheet is a document, and a dash is that document on standard input: staging one
                    // then leaves no file behind to be edited by mistake on the next run.
                    case "--sheet" when i + 1 < args.Length:
                        options = options with { Sheet = args[i + 1] };
                        i++;
                        break;
                    case "--caption":
                        options = options with { Caption = true };
                        break;
                    case "--title" when i + 1 < args.Length:
                        options = options with { Title = args[i + 1], Caption = true };
                        i++;
                        break;
                    case "--note" when i + 1 < args.Length:
                        options = options with { Note = args[i + 1], Caption = true };
                        i++;
                        break;
                    default:
                        throw new ArgumentException($"Unknown argument {args[i]}. Takes --map NAME, --view METRES, " +
                                                    "--at X Y, --shot PATH, --sheet FILE.json|-, --caption, " +
                                                    "--title TEXT, --note TEXT, --ui LAYERS, --rule X1 Y1 X2 Y2, " +
                                                    "--size W H, --ui-scale N, --present fifo|mailbox|immediate, " +
                                                    "--seconds N, --validate, --check, --bench NAME|all, --lay-track.");
                }
            }

            return options;
        }
    }
}
