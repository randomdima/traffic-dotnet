using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using Silk.NET.Input;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TrafficSimulation.Agents.Ambulance;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Service;
using TrafficSimulation.App.Camera;
using TrafficSimulation.App.Debug;
using TrafficSimulation.App.Drive;
using TrafficSimulation.App.Hud;
using TrafficSimulation.App.Render;
using TrafficSimulation.App.Shot;
using TrafficSimulation.Bench;
using TrafficSimulation.Runtime;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Terrain;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Main;

/// <summary>
/// The way in, and nothing more: it parses the words, reads the figures once, and hands them to
/// whichever of the four things was asked for.
/// </summary>
/// <remarks>
/// <para>
/// <b>The game itself is <see cref="Game"/></b>, which is the composition root — this only chooses
/// between it, the offscreen shot, the dependency read-out and the checks. With no map named the game
/// opens on its start menu, over the idle ring and no city (GEN-1b).
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
        // The only place the figures are read: everything below is handed the one instance rather
        // than reaching for a singleton, and the words are parsed against it because the size a run
        // opens at is a figure like any other.
        var config = SimConfig.Load();
        var options = Options.Parse(args, config.View);

        if (options.Lamps) return CutTheLamps();
        if (options.Bench is not null) return RunBench(options.Bench, options.Map, options.AtM, options.Out, config);
        if (options.Check) return RunCheck(options, config);
        if (options.Sheet is not null) return RunSheet(options, config);

        // A drive with nobody watching plays its script and ends; a live one opens the window below and
        // follows the file while somebody looks at it (DRV-7).
        if (options.Drive is not null && !options.Live) return RunDrive(options, config);
        if (options.Shot is not null) return RunShot(options, config);

        // A caption is a thing said about a picture, and a windowed run takes none.
        if (options.Caption)
            throw new ArgumentException(
                "--caption, --title and --note are about a picture: take one with --shot PATH or --sheet FILE.");

        // The same for the three words a drive takes: where its frames go, where its log is written and
        // whether it is being watched say nothing about a run nobody is driving.
        if (options.Drive is null && (options.Frames is not null || options.Out is not null || options.Live))
            throw new ArgumentException(
                "--frames, --out and --live are about a hand-driven run: ask for one with --drive FILE.");

        // A seat is the car it holds, so the two words are one word said twice: a bot with no car would be
        // a driver in the back, and a car with no steps a seat nobody is in.
        if ((options.Bot is null) != (options.BotCar < 0))
            throw new ArgumentException(
                "A second driver is a file of steps and the car it drives: --bot FILE --bot-car N (DRV-8).");

        // And the clock is only stopped for somebody: a run with no second seat in it has nobody to wait for.
        if (options.Bot is null && options.BotWaits)
            throw new ArgumentException(
                "--bot-waits stops the town for a second driver: ask for one with --bot FILE --bot-car N (DRV-8).");

        // GEN-1b: with no map named, the game opens on the start menu with the idle ring behind it and
        // no city until one is picked. Naming one on the command line is that choice made earlier.
        using var game = new Game(
            config, options.Width, options.Height, options.Validate, options.UiScale, PresentMode(options.Present),
            fullscreen: !options.Windowed, options.Display);

        // --ui reaches the windowed run as it reaches a shot: a measured run of a town nobody is
        // sitting in front of is exactly the run that wants the read-out switched on from the start.
        game.Switch(Wanted(options.Ui));

        // DRV-8: a second driver, which is a seat of its own and not a drive — it holds a car nobody
        // picked out and looks through an eye of its own, so a run may carry one, the other or both.
        if (options.Bot is not null) game.Bot(Botting(options, config));

        // DRV-7: and the drive, where this run is one somebody is watching being driven. A drive needs a
        // town, so one that named no map opens on the fixture rather than on the start menu.
        if (options.Drive is null)
        {
            return options.Bot is null
                ? game.Run(options.Map, options.Seconds)
                : game.Run(options.Map ?? Options.FixtureMap, options.Seconds);
        }

        game.Drive(Asked(options, config));
        return game.Run(options.Map ?? Options.FixtureMap, options.Seconds);
    }

    /// <summary>The words <c>--ui</c> was given, matched whole by whichever path is about to apply them.</summary>
    static string[] Wanted(string ui) =>
        ui.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>What <c>--present</c> names, as the pacing itself. A word nobody offers is an error rather than a silent fallback.</summary>
    static Pacing PresentMode(string named) => named switch
    {
        "fifo" => Pacing.Fifo,
        "mailbox" => Pacing.Mailbox,
        "immediate" => Pacing.Immediate,
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
            TurnDeg: options.TurnDeg,
            AtM: options.AtM,
            Ui: Wanted(options.Ui),
            UiScale: options.UiScale,
            Seconds: options.Seconds,
            RulerPointsM: options.RulerPointsM,
            PointerM: options.PointerM,
            PickedM: options.PickedM,
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
    /// <b>A town driven by hand from a script</b> (DRV-1, <see cref="DriveRun"/>): one unit picked out and
    /// the same keys the player holds pushed through the same seam, with a reading after every step and a
    /// frame wherever the script asks for one.
    /// </summary>
    /// <remarks>
    /// The frames are the shot path's and the figures are the unit panel's, so this only says which words
    /// reached them: <c>--map</c>, <c>--size</c>, <c>--view</c> and <c>--ui</c> mean here what they mean to
    /// a picture, <c>--frames</c> is where the pictures go and <c>--out</c> is where the whole drive is
    /// written.
    /// </remarks>
    static int RunDrive(Options options, SimConfig config)
    {
        // A drive says where each of its frames goes in the script's own `shot` step, so a single path
        // beside it would be every frame written over the one before.
        if (options.Shot is not null)
            throw new ArgumentException(
                "A drive takes its pictures where the script asks for them: --frames DIR says where they go.");

        return DriveRun.Run(Asked(options, config), config);
    }

    /// <summary>
    /// The drive the words asked for, which is the same request whether it is played with no window or
    /// followed in one (DRV-7).
    /// </summary>
    static DriveAsk Asked(Options options, SimConfig config) => new(
        Map: options.Map ?? Options.FixtureMap,
        Script: options.Drive!,
        FramesDir: options.Frames ?? Options.DriveFrames,
        Out: options.Out,
        WidthPx: options.Width,
        HeightPx: options.Height,
        ViewM: options.ViewM > 0f ? options.ViewM : config.View.DriveViewM,
        Ui: Wanted(options.Ui),
        Validate: options.Validate,
        FrameWidthPx: options.FrameWidth);

    /// <summary>
    /// <b>The second seat the words asked for</b> (DRV-8, <see cref="BotSeat"/>): the car it holds, the
    /// file it is steered from, and the eye it looks through. The eye is laid at one size for the whole
    /// run, as every renderer here is, and the aspect is the window's so that a bot and a reader are
    /// looking at the same shape of town.
    /// </summary>
    static BotAsk Botting(Options options, SimConfig config) => new(
        Car: options.BotCar,
        Steps: options.Bot!,
        FramesDir: options.BotFrames ?? Options.BotEyeFrames,
        Out: options.BotOut,
        EyeWidthPx: options.BotEyeWidth > 0 ? options.BotEyeWidth : config.View.BotEyeWidthPx,
        EyeHeightPx: EyeHeight(options, config),
        ViewM: options.BotViewM > 0f ? options.BotViewM : config.View.DriveViewM,
        Waits: options.BotWaits);

    /// <summary>How tall the eye is: its width at the window's own aspect, rounded to an even row.</summary>
    static int EyeHeight(Options options, SimConfig config)
    {
        var widthPx = options.BotEyeWidth > 0 ? options.BotEyeWidth : config.View.BotEyeWidthPx;
        var height = (int)MathF.Round(widthPx * ((float)options.Height / options.Width));
        return height % 2 == 0 ? height : height + 1;
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
        if (options.Map is not null || options.AtM is not null || options.ViewM > 0f || options.TurnDeg != 0f ||
            options.Ui.Length > 0 || options.Seconds > 0 || options.RulerPointsM.Count > 0 ||
            options.PointerM is not null || options.PickedM is not null ||
            options.Title is not null || options.Note is not null)
        {
            throw new ArgumentException(
                "A sheet stages itself: --map, --view, --turn, --at, --ui, --seconds, --rule, --title and --note "
                + "belong in the document. Only --shot PATH is read beside it, as where to write the sheet.");
        }

        var ask = SheetRequest.Read(options.Sheet!);
        var sheet = SheetRun.Take(ask, config, options.Shot);

        Console.WriteLine($"{"",-11}{sheet.Sheet} written at {sheet.WidthPx}x{sheet.HeightPx}, " +
                          $"{sheet.Cells.Length} cell(s), notes in {ShotNotes.NotesFor(sheet.Sheet)}");
        return 0;
    }

    /// <summary>
    /// Cuts the town's lamp sheet out of the fleet's own sprites (CAR-14a) and writes it beside them.
    /// <b>A workshop step and never a build one</b>: it is run when a variant's art or its lens
    /// rectangles change, and what ships is the picture it commits.
    /// </summary>
    /// <remarks>
    /// The distinctness it prints is the instrument for the one thing the arithmetic cannot answer: a
    /// lens rectangle over bodywork nobody painted a lamp on cuts the paint that surrounds it, and that
    /// is a sprite to finish rather than a number to adjust.
    /// </remarks>
    static int CutTheLamps()
    {
        var catalogue = CarCatalog.Shared;
        var path = ProjectPaths.LampAtlasFile();
        var cut = LampAtlasBake.Write(catalogue, path);

        Console.WriteLine($"{path} written — {cut.Count} lenses over {catalogue.SheetCount} looks, " +
                          $"{LampAtlas.Columns * LampAtlas.CellPx}x{catalogue.SheetCount * LampAtlas.CellPx} px " +
                          $"at {LampAtlas.CellPx} px a cell");

        foreach (var lamp in cut.OrderBy(lamp => lamp.Distinct))
        {
            Console.WriteLine($"{"",-9}{lamp.Variant,-18} {lamp.Fitting,-11} " +
                              $"{lamp.WidthPx,3}x{lamp.HeightPx,-3} distinct {lamp.Distinct:F2}");
        }

        return 0;
    }

    /// <summary>
    /// One of this engine's checks, by the name it is listed under. <b>The catalogue says which checks
    /// there are</b> (<see cref="CheckCatalogue"/>) and the switch below says only which of them the
    /// command line's <c>--map</c> reaches — every name falls through to the catalogue in the end.
    /// <b>A name spelled here and left out there is a check nothing lists and the menu cannot open</b>,
    /// and nothing but this comment stops one: a switch is source, and the suite reads objects.
    /// </summary>
    static int RunBench(string name, string? map, Vector2? atM, string? outPath, SimConfig config)
    {
        if (string.Equals(name, "all", StringComparison.Ordinal)) return Kept(CheckCatalogue.RunAll(config));

        // These checks are about a particular town, so the command line's --map reaches them; those that run every
        // shipped town run the one it names, and the stuck probe runs it instead of its own; every other check builds
        // the world it needs.
        switch (name)
        {
            case "soak" when map is not null:
                return Kept(SoakProbe.Run(config, map));
            case "town" when map is not null:
                TownProbe.Run(config, map);
                return 0;
            case "age" when map is not null:
                AgeProbe.Run(config, map);
                return 0;
            case "drive" when map is not null:
                DriveProbe.Run(config, map);
                return 0;
            case "stuck" when map is not null:
                StuckProbe.Run(map, config);
                return 0;
            case "trips" when map is not null:
                TripProbe.Run(config, map);
                return 0;
            case "load":
                LoadProbe.Run(map ?? Options.FixtureMap, config);
                return 0;
            case "outset":
                return Kept(BoundaryProbe.Outset(map ?? Options.FixtureMap, config, atM));
            case "fill":
                FillProbe.Run(map ?? Options.FixtureMap, config);
                return 0;
            case "parks":
                TownShape.Parks(map ?? Options.FixtureMap, config);
                return 0;
            case "census":
                TownCensus.Run(map ?? Options.FixtureMap, config);
                return 0;
            case "shape":
                TownShape.Run(map ?? Options.FixtureMap, config);
                return 0;
            case "fidelity":
                TracedFidelity.Run(map ?? Options.TracedMap, config, atM);
                return 0;
            case "fit":
                TracedFit.Run(map ?? Options.TracedMap, config, outPath, atM);
                return 0;
            case "joints":
                TownShape.Joints(map ?? Options.FixtureMap, config);
                return 0;
            case "close":
                TownShape.Close(map ?? Options.TracedMap, config);
                return 0;
        }

        if (CheckCatalogue.TryFind(name, out var check)) return Kept(check.Run(config));

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
    /// A check's own answer as the process's. <b>A broken claim is a failed run</b>, which is the whole of
    /// what makes a probe something a script can gate on rather than a table somebody reads.
    /// </summary>
    static int Kept(bool everyClaim) => everyClaim ? 0 : 1;

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
                          $"{config.LaneWidthM:F1} m lanes, {config.PersonDiameterM:F1} m people");
        Console.WriteLine($"{"",-9}{config.Sim.TickRateHz} Hz, decisions every {config.Sim.AgentDecisionIntervalS:F2} s, " +
                          $"turning radius {config.CarTurningRadiusM:F2} m, a walker's {config.WalkerTightestTurnM:F2} m");
    }

    /// <summary>
    /// The town as data: laid from the brief all four engines are handed, classified, and triangulated.
    /// What it is made of is <c>--bench census</c>.
    /// </summary>
    static void ReportTown(string map, SimConfig config)
    {
        var started = Stopwatch.GetTimestamp();
        var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
        var read = Stopwatch.GetElapsedTime(started);

        var mesh = GroundMesh.Build(plan, config);

        var ground = new GroundLocator(plan, config).At(plan.Spawns.Count > 0 ? plan.Spawns.PositionM[0] : plan.WorldSizeM * 0.5f);

        Console.WriteLine($"{plan.Name} {plan.WorldSizeM.X:F0}x{plan.WorldSizeM.Y:F0} m read in {read.TotalMilliseconds:F1} ms — " +
                          $"{plan.Roads.Count} roads, {plan.Buildings.Count} buildings, {plan.Props.Count} props, {plan.Spawns.Count} spawns");
        // <b>The mesh's own figures and not a second stopwatch round the same call</b> (OBS-2v): what a
        // layer cost is written down as it is laid, and the menu's ground section reads the same tallies.
        Console.WriteLine($"{"",-9}ground laid as {mesh.Indices.Length / 3} triangles in {mesh.LaidMs:F0} ms " +
                          $"({mesh.BoundaryMs:F0} ms of it the boundary); " +
                          $"the first spawn stands on {ground.Ground} ({ground.Rules})");
        Console.WriteLine($"{"",-9}maps this build knows: {string.Join(", ", Maps.Shipped())}");
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
        float TurnDeg,
        string? Shot, Vector2? AtM, string Ui, float UiScale, string Present, List<Vector2> RulerPointsM,
        Vector2? PointerM, Vector2? PickedM,
        string? Sheet, bool Caption, string? Title, string? Note, bool Lamps,
        bool Windowed, string? Display, string? Drive, string? Frames, string? Out, bool Live, int FrameWidth,
        string? Bot, int BotCar, string? BotFrames, string? BotOut, int BotEyeWidth, float BotViewM,
        bool BotWaits)
    {
        /// <summary>
        /// What every check that is not about a particular town is staged on: a small town laid from a
        /// brief that asks for no building, so it opens in a fraction of the time a city does.
        /// </summary>
        public const string FixtureMap = "Test";

        /// <summary>What a check about tracing a real place is staged on when <c>--map</c> names none.</summary>
        public const string TracedMap = "OdesaOsm";

        /// <summary>
        /// Where a hand-driven run leaves its frames when <c>--frames</c> names nowhere: the scratch folder,
        /// which is wiped without asking and is where every picture taken to be looked at goes.
        /// </summary>
        public const string DriveFrames = ".tmp/drive";

        /// <summary>
        /// And where a second driver's eye leaves its frames when <c>--bot-frames</c> names nowhere
        /// (DRV-8). Beside the drive's, because they are the same kind of picture taken by a different
        /// pair of eyes.
        /// </summary>
        public const string BotEyeFrames = ".tmp/bot";

        /// <summary>
        /// The words the other engines use: <c>--size W H</c>, <c>--map</c> and <c>--shot</c> are
        /// <c>traffic-native</c>'s, so a command line reads the same at both. <c>--shot</c> opens no
        /// window at all; <c>--seconds</c> closes the one <c>--map</c> opens, for a run nobody is
        /// sitting in front of; <c>--view</c> opens on a named span in metres and <c>--turn</c> stages
        /// the frame turned by a named angle (OBS-1c), which is how a turned town is photographed and
        /// judged rather than only twisted by hand; <c>--bench</c> runs one
        /// of this engine's checks; <c>--ui-scale</c> lays the interface out at a factor of its own
        /// instead of the desktop's; <c>--present</c> is how a finished frame reaches the glass, which
        /// is what a frame rate from a windowed run means at all (<see cref="Swapchain"/>);
        /// <c>--windowed</c> opens in a window instead of fullscreen, for a run to be looked at beside
        /// something else; <c>--display</c> names the screen to open on, by the desktop's own name for
        /// it or by its index, since a desktop nobody can ask which screen is in front of the person
        /// leaves the choice a guess; and <c>--check</c> is the dependency read-out.
        /// </summary>
        /// <remarks>
        /// <b>The driving words are their own set too</b> (<see cref="DriveAsk"/>): <c>--drive FILE|-</c>
        /// takes a script and holds the player's own keys through it, <c>--frames DIR</c> is where the
        /// pictures it asks for go, and <c>--out FILE.md</c> is where the whole drive is written.
        /// <b>And a second driver's are its own set again</b> (<see cref="BotAsk"/>, DRV-8): <c>--bot FILE
        /// --bot-car N</c> puts one in a named car, <c>--bot-frames</c>, <c>--bot-eye</c> and
        /// <c>--bot-view</c> are the eye it looks through, <c>--bot-out</c> is its own log, and
        /// <c>--bot-waits</c> stops the town's clock whenever it has run out of steps.
        /// <b>The review words are their own set</b> (<see cref="SheetRequest"/>): <c>--sheet</c> takes
        /// a document instead of flags and tiles what it names into one picture, and <c>--caption</c>,
        /// <c>--title</c> and <c>--note</c> put the same band and the same notes on a single
        /// <c>--shot</c>. Naming a title or a note implies the caption, since neither is drawn anywhere
        /// else.
        /// </remarks>
        public static Options Parse(string[] args, ViewFigures view)
        {
            // No map by default, because GEN-1b says the game opens on a menu and builds no city until
            // one is picked — what stands behind that menu is the game's own (<see cref="Game.IdleMap"/>),
            // and naming a map here is that choice made on the command line instead.
            // A zero ui scale is "ask the window", which is the desktop's own factor: naming one is
            // for the platform that reports 1 on a display nobody would call unscaled.
            var options = new Options(Check: false, Validate: false,
                Width: view.WindowWidthPx, Height: view.WindowHeightPx, Seconds: 0,
                Bench: null, Map: null, ViewM: 0f, TurnDeg: 0f, Shot: null, AtM: null, Ui: string.Empty, UiScale: 0f,
                Present: "fifo", RulerPointsM: [], PointerM: null, PickedM: null, Sheet: null,
                Caption: false, Title: null,
                Note: null, Lamps: false, Windowed: false, Display: null, Drive: null, Frames: null, Out: null,
                Live: false, FrameWidth: 0,
                Bot: null, BotCar: -1, BotFrames: null, BotOut: null, BotEyeWidth: 0, BotViewM: 0f,
                BotWaits: false);
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
                    case "--lamps":
                        options = options with { Lamps = true };
                        break;
                    case "--windowed":
                        options = options with { Windowed = true };
                        break;
                    case "--display" when i + 1 < args.Length:
                        options = options with { Display = args[i + 1] };
                        i++;
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
                    case "--turn" when i + 1 < args.Length:
                        options = options with { TurnDeg = float.Parse(args[i + 1]) };
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
                    // Where the pointer stands on the town, and which cell is picked out (OBS-2t): the two
                    // readings the layers take from the reader, asked for by a path that has no reader.
                    case "--point" when i + 2 < args.Length:
                        options = options with
                        {
                            PointerM = new Vector2(float.Parse(args[i + 1]), float.Parse(args[i + 2])),
                        };
                        i += 2;
                        break;
                    case "--pick" when i + 2 < args.Length:
                        options = options with
                        {
                            PickedM = new Vector2(float.Parse(args[i + 1]), float.Parse(args[i + 2])),
                        };
                        i += 2;
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
                    // A hand at the wheel is asked for as a script for the same reason a sheet is a document
                    // (SHT-4): a drive is a sequence and a sequence on a command line is unreadable by the
                    // second step. A dash reads it off standard input.
                    case "--drive" when i + 1 < args.Length:
                        options = options with { Drive = args[i + 1] };
                        i++;
                        break;
                    // A drive somebody is watching: the window opens, the file is followed as it is
                    // written, and the readings are said as they are taken (DRV-7).
                    case "--live":
                        options = options with { Live = true };
                        break;
                    // The town is drawn at whatever the window is; this is only how wide the frames handed
                    // to whoever is driving are written (DRV-4).
                    case "--frame-width" when i + 1 < args.Length:
                        options = options with { FrameWidth = int.Parse(args[i + 1]) };
                        i++;
                        break;
                    // DRV-8: the second seat's own words. They are its own rather than the drive's because a
                    // run may carry both, and a frame written over the other one's would be two drivers
                    // reading the same picture of different cars.
                    case "--bot" when i + 1 < args.Length:
                        options = options with { Bot = args[i + 1] };
                        i++;
                        break;
                    case "--bot-car" when i + 1 < args.Length:
                        options = options with { BotCar = int.Parse(args[i + 1]) };
                        i++;
                        break;
                    case "--bot-frames" when i + 1 < args.Length:
                        options = options with { BotFrames = args[i + 1] };
                        i++;
                        break;
                    case "--bot-out" when i + 1 < args.Length:
                        options = options with { BotOut = args[i + 1] };
                        i++;
                        break;
                    case "--bot-eye" when i + 1 < args.Length:
                        options = options with { BotEyeWidth = int.Parse(args[i + 1]) };
                        i++;
                        break;
                    case "--bot-view" when i + 1 < args.Length:
                        options = options with { BotViewM = float.Parse(args[i + 1]) };
                        i++;
                        break;
                    // DRV-8: the town stands still whenever that driver has run out of steps, so what it
                    // spends thinking costs it no ground.
                    case "--bot-waits":
                        options = options with { BotWaits = true };
                        break;
                    case "--frames" when i + 1 < args.Length:
                        options = options with { Frames = args[i + 1] };
                        i++;
                        break;
                    case "--out" when i + 1 < args.Length:
                        options = options with { Out = args[i + 1] };
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
                                                    "--turn DEGREES, " +
                                                    "--at X Y, --shot PATH, --sheet FILE.json|-, --caption, " +
                                                    "--title TEXT, --note TEXT, --ui LAYERS, --rule X1 Y1 X2 Y2, " +
                                                    "--size W H, --ui-scale N, --present fifo|mailbox|immediate, " +
                                                    "--windowed, --display NAME|N, --seconds N, --validate, --check, " +
                                                    "--bench NAME|all, --lamps, --drive FILE|-, --live, " +
                                                    "--frames DIR, --frame-width PX, --out FILE.md.");
                }
            }

            return options;
        }
    }
}
