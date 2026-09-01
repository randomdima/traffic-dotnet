using TrafficSimulation.App.Screen;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>The driving exam as claims</b>: the thirty-six cards of <see cref="ExamCards"/> gathered into one
/// claim per thing a card can ask, each kept while every card asking it is driven as its card says.
/// </summary>
/// <remarks>
/// <para>
/// <b>The staging is <see cref="ExamDrive"/>'s and this only reads it</b>, so <c>--bench exam</c>, the
/// panel on a run of <c>--map Exam</c> and the town tier are three readings of one exam rather than three
/// exams.
/// </para>
/// <para>
/// <b>A card is answered when it is decided and not before.</b> Every card here is a car ordered across a
/// junction, so until that car has arrived or given up there is nothing to be right or wrong about — which
/// is why a run cut short says a claim is unanswered rather than saying the engine failed it.
/// </para>
/// <para>
/// <b>A card this build does not pass carries what it does instead</b> (<see cref="ExamCard.Finding"/>),
/// and those cards are held out of the claim their kind makes and gated the other way round: the claim
/// below is that each of them <em>still</em> fails, so the day the engine passes one the panel says so and
/// the finding is deleted rather than left standing as a note nobody re-reads.
/// </para>
/// </remarks>
internal sealed class ExamWatch : ScenarioWatch
{
    /// <summary>The claims are the kinds of thing a card can ask, in the enum's own order, and then the findings.</summary>
    const int KnownFindingsStillFail = 7;

    const int CardsDrivenAsWritten = 0;
    const int OutstandingFindings = 1;

    static readonly string[] TheClaims =
    [
        "a car with nothing in its way gets through the box",
        "a movement that takes ground off nothing is never held short of the box",
        "the weaker movement is on the box only once the stronger has left it",
        "a car goes onto the box after the one in front of it",
        "no car is on the box while its own approach is showing red",
        "no car is on the paint while somebody on foot is standing on it",
        "a car at a dead end comes back the way it came",
        "every card this build does not pass is a known finding",
    ];

    static readonly string[] TheReadings =
    [
        "cards driven as written",
        "outstanding findings",
    ];

    readonly ExamDrive _drive;

    public ExamWatch(SimConfig config, TownWorld world)
        : base("the driving exam", "thirty-six junction crossings, one to a cell of the lattice", TheClaims, TheReadings)
        => _drive = new ExamDrive(config, world);

    /// <summary>The staging itself, for the table <c>--bench exam</c> prints card by card.</summary>
    public ExamDrive Drive => _drive;

    public override void Saw(TownWorld world) => _drive.Saw();

    public override ClaimVerdict Verdict(int claim)
    {
        if (claim == KnownFindingsStillFail) return Findings();

        var cards = 0;
        var decided = 0;
        var wrong = 0;
        for (var card = 0; card < ExamCards.Count; card++)
        {
            var of = ExamCards.All[card];

            // A known finding is not this claim's: what it is doing instead is the claim below.
            if ((int)of.Asks != claim || of.Finding.Length > 0) continue;

            cards++;
            if (!_drive.Decided(card)) continue;

            decided++;
            if (_drive.Verdict(card) is not null) wrong++;
        }

        if (wrong > 0) return ClaimVerdict.Broken;

        return cards > 0 && decided == cards ? ClaimVerdict.Kept : ClaimVerdict.Waiting;
    }

    /// <summary>
    /// Whether every card carrying a finding is still failing on it. <b>A finding that has come right is
    /// broken here on purpose</b> — it is a line in the cards to delete, and nothing else in the suite
    /// would ever say so.
    /// </summary>
    ClaimVerdict Findings()
    {
        var findings = 0;
        var decided = 0;
        var passing = 0;
        for (var card = 0; card < ExamCards.Count; card++)
        {
            if (ExamCards.All[card].Finding.Length == 0) continue;

            findings++;
            if (!_drive.Decided(card)) continue;

            decided++;
            if (_drive.Verdict(card) is null) passing++;
        }

        if (passing > 0) return ClaimVerdict.Broken;

        return findings > 0 && decided == findings ? ClaimVerdict.Kept : ClaimVerdict.Waiting;
    }

    public override void Says(int claim, ref TextBuffer into)
    {
        if (claim == KnownFindingsStillFail)
        {
            var stillFailing = 0;
            var findings = 0;
            for (var card = 0; card < ExamCards.Count; card++)
            {
                if (ExamCards.All[card].Finding.Length == 0) continue;

                findings++;
                if (_drive.Decided(card) && _drive.Verdict(card) is not null) stillFailing++;
            }

            into.Add(stillFailing);
            into.Add(" of ");
            into.Add(findings);
            into.Add(" findings still stand");
            return;
        }

        var cards = 0;
        var driven = 0;
        var first = -1;
        for (var card = 0; card < ExamCards.Count; card++)
        {
            var of = ExamCards.All[card];
            if ((int)of.Asks != claim || of.Finding.Length > 0) continue;

            cards++;
            if (!_drive.Decided(card)) continue;

            if (_drive.Verdict(card) is null) driven++;
            else if (first < 0) first = card;
        }

        if (cards == 0)
        {
            // Every card that asks this carries a finding, so what this build does about it is claimed
            // there instead and there is nothing left here to keep.
            into.Add("no card without a finding asks it");
            return;
        }

        into.Add(driven);
        into.Add(" of ");
        into.Add(cards);
        into.Add(" cards");
        if (first < 0) return;

        into.Add(", first wrong at card ");
        into.Add(first);
    }

    public override void Reads(int reading, ref TextBuffer into)
    {
        switch (reading)
        {
            case CardsDrivenAsWritten:
                Counted(out var driven, out var known, out var failing);
                into.Add(driven);
                into.Add(" of ");
                into.Add(ExamCards.Count);
                into.Add(" cards driven as written, ");
                into.Add(failing);
                into.Add(" failing, over ");
                into.Add(_drive.Ticked);
                into.Add(" ticks");
                break;

            case OutstandingFindings:
                Counted(out _, out var outstanding, out _);
                into.Add(outstanding);
                into.Add(" cards carry what this build does instead");
                break;
        }
    }

    /// <summary>The exam as three counts: driven as written, still carrying a finding, and failing without one.</summary>
    void Counted(out int driven, out int known, out int failing)
    {
        driven = 0;
        known = 0;
        failing = 0;
        for (var card = 0; card < ExamCards.Count; card++)
        {
            var carriesAFinding = ExamCards.All[card].Finding.Length > 0;
            if (carriesAFinding) known++;
            if (!_drive.Decided(card)) continue;

            if (_drive.Verdict(card) is null) driven++;
            else if (!carriesAFinding) failing++;
        }
    }
}
