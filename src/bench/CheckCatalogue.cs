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
/// six of these names a second time so that <c>--map</c> reaches them, and a name spelled there and
/// left out here is a check nothing lists and the menu cannot open.
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
        new("crash", "Every damage band staged, and what each one did", Quoted(CrashProbe.Run)),
        new("soak", "A whole town asked whether anything is inside anything else", SoakProbe.Run),
        new("stuck", "A long run of one town, and who was still standing where they stopped", Quoted(StuckProbe.Run)),
        new("trips", "Whole trips, end to end: drawn, driven, parked, walked in", Quoted(TripProbe.Run)),
        new("rescue", "One staged casualty a town: whether an ambulance came, collected and delivered", Quoted(RescueProbe.Run)),
        new("recovery", "One staged wreck a town: whether an evacuator came, towed it home and mended it", Quoted(RecoveryProbe.Run)),
        new("maneuvers", "Which manoeuvre every driver was in, and what the ladder came to", Quoted(ManeuverProbe.Run)),
        new("census", "What is in a town: bodies, buildings, props, lit junctions", Quoted(config => TownCensus.Run("Odesa", config))),
        new("shape", "What shape a town came out: how its roads bend, where its junctions stand", Quoted(config => TownShape.Run("Odesa", config))),
        new("joints", "Every junction only two roads meet at, and which structure kept it", Quoted(config => TownShape.Joints("Odesa", config))),
        new("parks", "Every car park a town cut into a road: where it stands, its arms and its bays", Quoted(config => TownShape.Parks("Odesa", config))),
        new("outset", "A town's boundary moved off itself: what closed, and the two ends of what did not", config => BoundaryProbe.Outset("Odesa", config)),
        new("fill", "A town's driven ground cut into triangles: what the cut costs, and what it lost", Quoted(config => FillProbe.Run("Odesa", config))),
        new("shapes", "One row a map: extent, roads, how much of each bends", Quoted(TownShape.Table)),
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
