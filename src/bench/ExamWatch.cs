using TrafficSimulation.App.Screen;
using TrafficSimulation.CityGen.Exam;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>The scenario map as claims</b>: the cards of <see cref="ExamCards"/> gathered into one claim per family,
/// each kept while every card in it goes as it expects.
/// </summary>
/// <remarks>
/// <para>
/// <b>The staging is <see cref="ExamDrive"/>'s and this only reads it</b>, so <c>--bench exam</c>, the panel on
/// a run of <c>--map Exam</c> and the exam tier are three readings of one staging rather than three.
/// </para>
/// <para>
/// <b>A card is answered when it is decided and not before.</b> Every card is a car sent through a junction,
/// so until that car has arrived or given up there is nothing to be right or wrong about — which is why a run
/// cut short says a claim is unanswered rather than saying the engine failed it.
/// </para>
/// </remarks>
internal sealed class ExamWatch : ScenarioWatch
{
    const int CardsPassed = 0;

    readonly ExamDrive _drive;
    readonly ExamFamily[] _families;

    ExamWatch(ExamDrive drive, ExamFamily[] families, string[] claims)
        : base("the scenarios", "traffic staged one scenario to a junction of the lattice", claims, ["cards passed"])
    {
        _drive = drive;
        _families = families;
    }

    public static ExamWatch Over(SimConfig config, TownWorld world)
    {
        var drive = new ExamDrive(config, world);
        var families = new List<ExamFamily>();
        for (var card = 0; card < drive.Lattice.Cards; card++)
        {
            var family = drive.Lattice.Card(card).Family;
            if (!families.Contains(family)) families.Add(family);
        }

        families.Sort();
        var claims = new string[families.Count];
        for (var claim = 0; claim < claims.Length; claim++) claims[claim] = Asks(families[claim]);

        return new ExamWatch(drive, [.. families], claims);
    }

    /// <summary>The staging itself, for the table <c>--bench exam</c> prints card by card.</summary>
    public ExamDrive Drive => _drive;

    public override void Saw(TownWorld world) => _drive.Saw();

    static string Asks(ExamFamily family) => family switch
    {
        ExamFamily.Alone => "a car alone drives every movement without a stop",
        ExamFamily.Apart => "cars whose ways share no ground never hold each other",
        ExamFamily.Across => "cars whose ways cross never hold the one with the right of way",
        ExamFamily.Queues => "every card about a queue goes as it expects",
        ExamFamily.Signals => "every card at a lit junction goes as it expects",
        ExamFamily.Walkers => "every card about somebody on foot goes as it expects",
        ExamFamily.Emergency => "every card about a car on a call goes as it expects",
        _ => "every card about a one-way street goes as it expects",
    };

    public override ClaimVerdict Verdict(int claim)
    {
        Counted(_families[claim], out var cards, out var decided, out var wrong, out _);
        if (wrong > 0) return ClaimVerdict.Broken;

        return cards > 0 && decided == cards ? ClaimVerdict.Kept : ClaimVerdict.Waiting;
    }

    public override void Says(int claim, ref TextBuffer into)
    {
        Counted(_families[claim], out var cards, out var decided, out var wrong, out var first);
        into.Add(decided - wrong);
        into.Add(" of ");
        into.Add(cards);
        into.Add(" cards passed");
        if (first < 0) return;

        into.Add(", first failing card ");
        into.Add(first);
    }

    public override void Reads(int reading, ref TextBuffer into)
    {
        if (reading != CardsPassed) return;

        var passed = 0;
        var failed = 0;
        for (var card = 0; card < _drive.Lattice.Cards; card++)
        {
            if (!_drive.Decided(card)) continue;

            if (_drive.Verdict(card) is null) passed++;
            else failed++;
        }

        into.Add(passed);
        into.Add(" of ");
        into.Add(_drive.Lattice.Cards);
        into.Add(" passed, ");
        into.Add(failed);
        into.Add(" failed, over ");
        into.Add(_drive.Ticked);
        into.Add(" ticks");
    }

    void Counted(ExamFamily family, out int cards, out int decided, out int wrong, out int first)
    {
        cards = 0;
        decided = 0;
        wrong = 0;
        first = -1;
        for (var card = 0; card < _drive.Lattice.Cards; card++)
        {
            if (_drive.Lattice.Card(card).Family != family) continue;

            cards++;
            if (!_drive.Decided(card)) continue;

            decided++;
            if (_drive.Verdict(card) is null) continue;

            wrong++;
            if (first < 0) first = card;
        }
    }
}
