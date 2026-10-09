using TrafficSimulation.Core.Config;

namespace TrafficSimulation.Bench;

/// <summary>One check this engine ships: the word the command line takes, and what running it answers.</summary>
/// <param name="Run">
/// The check itself, answering <b>whether every claim it gates was kept</b>. A probe that gates nothing
/// answers true because there was nothing to break — which is what <see cref="CheckCatalogue.Quoted"/>
/// says of it at the point it is listed, rather than leaving a caller to read a bare exit code as a pass.
/// </param>
internal readonly record struct CheckEntry(string Name, string Description, Func<SimConfig, bool> Run);

/// <summary>
/// <b>Every probe the build ships, under the name <c>--bench</c> takes.</b> A check <em>replaces</em>
/// the game rather than being a town: it builds its own world, prints, and is done.
/// </summary>
/// <remarks>
/// <b>Every entry names a probe that runs, and the unit suite says so rather than this comment</b>
/// (<c>CatalogueTests</c>). <b>The other direction is not guarded</b>: <c>Program.RunBench</c> spells
/// some of these names a second time so that <c>--map</c> reaches them, and a name spelled there and
/// left out here is a check nothing lists and the menu cannot open.
/// </remarks>
internal static class CheckCatalogue
{
    public static readonly CheckEntry[] Shipped =
    [
        new("tick", "The empty loop's cost and its allocation, over a thousand ticks", Quoted(TickProbe.Run)),
        new("solver", "The solver's allocated bytes per step, over the whole table", Quoted(SolverProbe.Run)),
        new("walk", "One walker's pace, how far it takes to reach and lose it, and how far a shove carries it", Quoted(WalkProbe.Run)),
        new("town", "A standing town's tick, ranked by phase, with its allocation", Quoted(TownProbe.Run)),
        new("age", "One town's tick at fixed ages, ranked by phase, with a digest of where every body stands", Quoted(AgeProbe.Run)),
        new("drive", "What the town's cars are actually doing, read back off them", Quoted(config => DriveProbe.Run(config))),
        new("crash", "Every damage band staged, and what each one did", Quoted(CrashProbe.Run)),
        new("soak", "A whole town asked whether anything is inside anything else", config => SoakProbe.Run(config)),
        new("stuck", "A long run of one town, and who was still standing where they stopped", Quoted(StuckProbe.Run)),
        new("trips", "Whole trips, end to end: drawn, driven, parked, walked in", Quoted(config => TripProbe.Run(config))),
        new("rescue", "One staged casualty a town: whether an ambulance came, collected and delivered", Quoted(RescueProbe.Run)),
        new("recovery", "One staged wreck a town: whether an evacuator came, towed it home and mended it", Quoted(RecoveryProbe.Run)),
        new("exam", "The scenario map: traffic staged one scenario to a junction and driven end to end, each passed or failed", ExamProbe.Run),
        new("census", "What is in a town: bodies, buildings, props, lit junctions", Quoted(config => TownCensus.Run("Odesa", config))),
        new("load", "What opening a map costs, stage by stage", Quoted(config => LoadProbe.Run("Odesa", config))),
        new("sprites", "What filling a frame's sprites costs, part by part, from a street to the whole town", Quoted(config => SpriteProbe.Run("Odesa", config))),
        new("devicestep","The solver's step on the device beside the CPU's: its time stage by stage, and how the two answers agree", Quoted(config => DeviceStepProbe.Run("Odesa", config))),
        new("devicetyres", "A running town's whole tyre pass on the device and on the host: time, and one answer", Quoted(config => DeviceTyresProbe.Run("Odesa", config))),
        new("deviceatlas", "Which ways a running town's bodies stand over, on the device and on the host: time, and one answer", Quoted(config => DeviceAtlasProbe.Run("Odesa", config))),
        new("deviceclaims", "A running town's plans answered on the device beside the host's, rebuild by rebuild: time, and one answer", Quoted(config => DeviceClaimsProbe.Run("Odesa", config))),
        new("devicewalkers", "A running town's walkers stationed, laid, planned and aimed on the device beside the host's, rebuild by rebuild: time, and one answer", Quoted(config => DeviceWalkersProbe.Run("Odesa", config))),
        new("devicerebuild", "A running town's whole claims rebuild chained on the device in one submit beside the host's: time, and one answer", Quoted(config => DeviceRebuildProbe.Run("Odesa", config))),
        new("devicecartick", "A running town's following cars ticked on the device beside the town's own tick: time, and one answer", Quoted(config => DeviceCarTickProbe.Run("Odesa", config))),
        new("devicebodies", "A running town's bodies stepped on the device beside the town's own phase, impulses to read-back: time, and one answer", Quoted(config => DeviceBodiesProbe.Run("Odesa", config))),
        new("devicephase", "A running town's phase 4 handed to the device in step, against the town's own: sends, kernels, hand-back and write-back apart", Quoted(config => DevicePhaseProbe.Run("Odesa", config))),
        new("devicetick", "A running town's agents and bodies ticked on the device in one submit, beside the town's own tick: time, and how far apart", Quoted(config => DeviceTickProbe.Run("Odesa", config))),
        new("devicetown", "A town ticked with the device in step beside the same town ticked by the host alone: each phase's time, and what each town did", Quoted(config => DeviceTownProbe.Run("Odesa", config))),
        new("devicemirror", "A town's claims laid on the device and mirrored back, beside the same town laying its own in step: where any slot, hold, car, walker or pose first parts", Quoted(config => DeviceMirrorProbe.Run("Odesa", config))),
        new("devicecopy", "What a copy costs the device each way, whole and cut into many runs", Quoted(DeviceCopyProbe.Run)),
        new("room", "The claims' room term by term against the most a standing town's agents lay into it", Quoted(config => RoomProbe.Run("Odesa", config))),
        new("devicesprites", "A running town's moving sprites emitted on the device beside the host's fill, framing by framing: time, and one answer", Quoted(config => DeviceSpritesProbe.Run("Odesa", config))),
        new("shape", "What shape a town came out: how its roads bend, where its junctions stand", Quoted(config => TownShape.Run("Odesa", config))),
        new("joints", "Every junction only two roads meet at, and which structure kept it", Quoted(config => TownShape.Joints("Odesa", config))),
        new("parks", "Every car park a town laid off a street: where it stands and its bays", Quoted(config => TownShape.Parks("Odesa", config))),
        new("outset", "A town's boundary moved off itself: what closed, and the two ends of what did not", config => BoundaryProbe.Outset("Odesa", config)),
        new("fill", "A town's driven ground cut into triangles: what the cut costs, and what it lost", Quoted(config => FillProbe.Run("Odesa", config))),
        new("shapes", "One row a map: extent, roads, how much of each bends", Quoted(TownShape.Table)),
        new("fidelity", "How far a traced town's lanes stand off its map's own, both ways", Quoted(config => TracedFidelity.Run("OdesaOsm", config))),
        new("fit", "How a town's walk was built as its zones say, look by look", Quoted(config => TracedFit.Run("OdesaOsm", config, null))),
        new("zones", "How close a traced town's zones build it to the place's own buildings, kind by kind and square by square", Quoted(config => ZoneFidelity.Run("OdesaOsm", config, null))),
    ];

    /// <summary>
    /// <b>A probe that prints figures and gates nothing.</b> It cannot fail, which is a fact about the
    /// reading and not about the run: what a dense city's geometry lets an articulated pair do, what a
    /// tick costs and what a drunk lap's swerves come to are facts about one town rather than claims
    /// (<see cref="Scenarios"/>). Saying so here is what stops a caller reading a bare zero as a pass.
    /// </summary>
    static Func<SimConfig, bool> Quoted(Action<SimConfig> probe) => config =>
    {
        probe(config);
        return true;
    };

    /// <summary>The check by the name the command line uses, or false if there is no such check.</summary>
    public static bool TryFind(string name, out CheckEntry entry)
    {
        foreach (var check in Shipped)
        {
            if (!string.Equals(check.Name, name, StringComparison.Ordinal)) continue;

            entry = check;
            return true;
        }

        entry = default;
        return false;
    }

    /// <summary>
    /// Every check in turn, which is what <c>--bench all</c> is, and whether all of them kept what they
    /// claim. <b>Every check is run</b> and the answer taken at the end: a run that stopped at the first
    /// broken claim would hide the rest of them behind it.
    /// </summary>
    public static bool RunAll(SimConfig config)
    {
        var kept = true;
        for (var check = 0; check < Shipped.Length; check++)
        {
            if (check > 0) Console.WriteLine();
            kept &= Shipped[check].Run(config);
        }

        return kept;
    }
}
