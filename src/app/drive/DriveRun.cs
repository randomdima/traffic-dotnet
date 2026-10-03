using TrafficSimulation.App.Debug;
using TrafficSimulation.App.PlayerControl;
using TrafficSimulation.App.Shot;
using TrafficSimulation.Bench;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Drive;

/// <summary>
/// <b>DRV-1 — a script at the wheel, with no window under it.</b> A town is stood up, one unit is picked
/// out, and the script's own keys are pushed through the seam the player's keys go through, a tick at a
/// time (<see cref="TownWorld.Hands"/>, CTL-5). Every step reports what the unit is doing and any step may
/// ask for a frame, so a run nobody is sitting in front of can still be driven and looked at.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing here can ask for more than a key can.</b> The pedals, the wheel and the handbrake are the
/// hand CTL-5 already offers; an order is the right-click CTL-8 already offers, through
/// <see cref="PlayerHands.Order"/>; the lever is CTL-7's. There is no teleport, no pose written into a
/// body and no rule switched off — which is what makes what the log shows a fact about the town rather
/// than about this file.
/// </para>
/// <para>
/// <b>The town is laid from the map's own brief and the run is seeded</b>, so the same script over the
/// same map is the same run (DRV-6): a script is appended to and played again, which is how a hand steers
/// over several sittings without a process left standing between them. <b>A run somebody is watching does
/// the same thing the other way round</b> — the window follows the file as it is written
/// (<see cref="DriveSeat"/>, DRV-7) — and both are the same steps doing the same things to the same seam.
/// </para>
/// <para>
/// <b>The device is opened only if a frame is asked for.</b> A script that asks for figures alone runs on
/// a machine with no Vulkan driver at all, which is what lets the unit tier ask this class questions.
/// </para>
/// </remarks>
internal sealed class DriveRun : IDisposable
{
    readonly DriveAsk _ask;
    readonly SimConfig _config;
    readonly CityPlan _plan;
    readonly TownWorld _world;
    readonly ScenarioWatch[] _scenario;
    readonly SimLoop<TownWorld> _loop;
    readonly DriveHands _hands;

    /// <summary>Opened by the first frame and kept for the rest of them (<see cref="ShotStage"/>).</summary>
    ShotStage? _stage;

    DriveRun(in DriveAsk ask, SimConfig config)
    {
        _ask = ask;
        _config = config;
        _plan = Maps.Plan(ask.Map, config, BuildingCatalog.Roofs);
        _world = new TownWorld(_plan, config);
        _scenario = Scenarios.For(_world, config);
        _loop = new SimLoop<TownWorld>(_world, config);
        _hands = new DriveHands(_world, _scenario, ask.Map, ask.FramesDir, ask.ViewM, ask.Ui)
        {
            Photograph = Frame,

            // With no interface on this path the hold is the world's own switch, which is what the panel
            // would have turned. A headless run opens with the agents deciding.
            Hold = held => _world.HoldAgents = held,
        };
    }

    /// <summary>
    /// The whole run: the script read, the town laid, every step driven and the log printed. <b>What the
    /// town claims about itself is printed as a reading and gates nothing</b> (DRV-5) — a hand is outside
    /// the soft rules on purpose, so a player who drove into something has broken a claim by doing what
    /// CTL-5 says a player may do.
    /// </summary>
    /// <returns>The process's answer: nought, or one where the script could not be carried out.</returns>
    public static int Run(in DriveAsk ask, SimConfig config)
    {
        var log = Drive(ask, config);
        log.Print();
        if (ask.Out is { } outPath) log.Write(outPath);

        return 0;
    }

    /// <summary>The script read and driven, and what every step of it left behind.</summary>
    public static DriveLog Drive(in DriveAsk ask, SimConfig config) =>
        Drive(DriveScript.Read(Read(ask.Script)), ask, config);

    /// <summary>
    /// The same, for steps that are already in hand — <b>which is how the tier asks this class questions</b>
    /// without a file to read them out of.
    /// </summary>
    public static DriveLog Drive(DriveStep[] steps, in DriveAsk ask, SimConfig config)
    {
        using var run = new DriveRun(ask, config);
        var log = new DriveLog(ask.Map, ask.Script);
        var tape = new DriveTape(run._hands, log, config.Sim.TickRateHz);
        tape.Add(steps);

        // The same tape a watched run follows, wound on by a loop of this run's own rather than by a
        // window's: every tick the steps that have come due are taken and the hand is pushed through the
        // seam (CTL-6).
        while (true)
        {
            // The steps due at this tick, and the reading of whichever of them has just ended; a tape with
            // nothing left is the end of the script, and the run stops on that tick rather than a tick past
            // it.
            tape.At(run._loop.Tick);
            if (tape.Idle) break;

            run._world.Hands(tape.Hand);
            run._loop.Advance();
            foreach (var watch in run._scenario) watch.Saw(run._world);
        }

        // The claims table a headless run says on its way out, as a reading (DRV-5). It is the same watch
        // the panel drew in every frame this run took, and it is printed after the steps rather than here:
        // a tier asking this class a question is not asking for a table.
        log.Watched(run._scenario, run._world.ElapsedS);
        return log;
    }

    /// <summary>The script, off a file or off standard input, which is what a dash names.</summary>
    static string Read(string script) =>
        script == "-" ? Console.In.ReadToEnd() : File.ReadAllText(script);

    /// <summary>
    /// A frame of the run as it stands, through the one shot path (SHT-1) and captioned like any picture
    /// taken to be looked at: the map, the framing, the tick and the seed are under it, so a frame in
    /// <c>.tmp/</c> still says what it is a week later.
    /// </summary>
    /// <returns>Where the picture went, for the log.</returns>
    string Frame(DriveFrame wanted)
    {
        var ask = new ShotRequest(
            Map: _ask.Map,
            Path: wanted.Path,
            WidthPx: _ask.WidthPx,
            HeightPx: _ask.HeightPx,
            ViewM: wanted.ViewM,
            AtM: wanted.AtM,
            Ui: wanted.Ui,
            Seconds: _world.ElapsedS,
            Validate: _ask.Validate);

        _stage ??= ShotStage.For(_plan, _config, _ask.WidthPx, _ask.HeightPx, _ask.Validate);
        var ui = ShotStage.Dressed(ask, _config);
        var shot = _stage.Draw(
            ask, ui, _world, _loop.Tick, _scenario,
            new FrameFigures { Phases = _loop.Phases, Sub = _world.Sub });

        SheetRun.Annotate(ask, shot, wanted.Name, null);
        return $"{wanted.Path} at {shot.PxPerM:F1} px/m, {shot.Sprites} bodies in frame";
    }

    public void Dispose()
    {
        _stage?.Dispose();
        _world.Dispose();
    }
}
