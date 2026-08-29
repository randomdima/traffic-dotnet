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
/// <b>The guard runs in both directions</b>, and it is the unit suite's rather than a comment's:
/// every entry names a probe that exists and every probe appears in the list. The dispatch below is
/// the only place a probe's name is turned into a call, so there is no second list to drift.
/// </remarks>
internal static class CheckCatalogue
{
    public static readonly CheckEntry[] Shipped =
    [
        new("tick", "The empty loop's cost and its allocation, over a thousand ticks", Quoted(TickProbe.Run)),
        new("solver", "The solver's allocated bytes per step, over the whole table", Quoted(SolverProbe.Run)),
        new("walk", "One walker's pace and how far it takes to reach and lose it", Quoted(WalkProbe.Run)),
        new("town", "A standing town's tick, ranked by phase, with its allocation", Quoted(TownProbe.Run)),
        new("drive", "What the town's cars are actually doing, read back off them", Quoted(DriveProbe.Run)),
        new("track", "What each shape of road costs each drivetrain: the speed, the stop, the pull-away", TrackProbe.Run),
        new("drunk", "The same lap with somebody reeling down it: what following and getting past cost", TrackProbe.RunTheDrunks),
        new("fleet", "The same lap with the whole fleet on it: whether every look drives the road, and at what", TrackProbe.RunTheFleet),
        new("skidpad", "Every look on full lock under six pedals: the circle asked for against the circle turned", SkidpadProbe.Run),
        new("crash", "Every damage band staged, and what each one did", Quoted(CrashProbe.Run)),
        new("soak", "A whole town asked whether anything is inside anything else", SoakProbe.Run),
        new("stuck", "A long run of one town, and who was still standing where they stopped", Quoted(StuckProbe.Run)),
        new("trips", "Whole trips, end to end: drawn, driven, parked, walked in", Quoted(TripProbe.Run)),
        new("rescue", "One staged casualty a town: whether an ambulance came, collected and delivered", Quoted(RescueProbe.Run)),
        new("recovery", "One staged wreck a town: whether an evacuator came, towed it home and mended it", Quoted(RecoveryProbe.Run)),
        new("maneuvers", "Which manoeuvre every driver was in, and what the ladder came to", Quoted(ManeuverProbe.Run)),
        new("exam", "Every junction crossing the exam stages, and what each card came to", ExamProbe.Run),
        new("crossings", "Five streets with a crossing on each: whether every one of them is walked", CrossingProbe.Run),
        new("signals", "The lit town's invariants, sampled every tick of a soak", Quoted(SignalProbe.Run)),
        new("census", "What is in a town: bodies, buildings, props, lit junctions", Quoted(config => TownCensus.Run("Odesa", config))),
    ];

    /// <summary>
    /// <b>A probe that prints figures and gates nothing.</b> It cannot fail, which is a fact about the
    /// reading and not about the run: what a dense city's geometry lets an articulated pair do, what a
    /// tick costs and what a drunk lap's swerves come to are facts about one town rather than claims
    /// (<see cref="Scenario"/>). Saying so here is what stops a caller reading a bare zero as a pass.
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
