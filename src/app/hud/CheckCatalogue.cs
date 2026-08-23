using TrafficSimulation.Bench;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.App.Hud;

/// <summary>One check this engine ships: the word the command line takes, and what running it answers.</summary>
internal readonly record struct CheckEntry(string Name, string Description, Action<SimConfig> Run);

/// <summary>
/// <b>OBS-2a — every check the build ships is reachable from the start menu, and the list the menu
/// reads is the same list the command line reads.</b> A check nobody can launch is a check nobody
/// runs.
/// </summary>
/// <remarks>
/// <para>
/// The checks are the probes. A check <em>replaces</em> the game rather than being a town: it builds
/// its own world, prints, and is done — so what the menu does with one is run it and show what it
/// printed, which is the same thing a terminal would have shown.
/// </para>
/// <para>
/// <b>The guard runs in both directions</b>, and it is the unit suite's rather than a comment's:
/// every entry names a probe that exists and every probe appears in the list. The dispatch below is
/// the only place a probe's name is turned into a call, so there is no second list to drift.
/// </para>
/// </remarks>
internal static class CheckCatalogue
{
    public static readonly CheckEntry[] Shipped =
    [
        new("tick", "The empty loop's cost and its allocation, over a thousand ticks", TickProbe.Run),
        new("solver", "The solver's allocated bytes per step, over the whole table", SolverProbe.Run),
        new("walk", "One walker's pace and how far it takes to reach and lose it", WalkProbe.Run),
        new("town", "A standing town's tick, ranked by phase, with its allocation", TownProbe.Run),
        new("drive", "What the town's cars are actually doing, read back off them", DriveProbe.Run),
        new("track", "What each shape of road costs each drivetrain: the speed, the stop, the pull-away", TrackProbe.Run),
        new("drunk", "The same lap with somebody reeling down it: what following and getting past cost", TrackProbe.RunTheDrunks),
        new("crash", "Every damage band staged, and what each one did", CrashProbe.Run),
        new("soak", "A whole town asked whether anything is inside anything else", SoakProbe.Run),
        new("trips", "Whole trips, end to end: drawn, driven, parked, walked in", TripProbe.Run),
        new("maneuvers", "Which manoeuvre every driver was in, and what the ladder came to", ManeuverProbe.Run),
        new("signals", "The lit town's invariants, sampled every tick of a soak", SignalProbe.Run),
        new("census", "What is in a town: bodies, buildings, props, lit junctions", config => TownCensus.Run("Odesa", config)),
    ];

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

    /// <summary>Every check in turn, which is what <c>--bench all</c> is.</summary>
    public static void RunAll(SimConfig config)
    {
        for (var check = 0; check < Shipped.Length; check++)
        {
            if (check > 0) Console.WriteLine();
            Shipped[check].Run(config);
        }
    }
}
