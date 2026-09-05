using TrafficSimulation.App.Screen;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>The walking exam as claims</b>: the twenty cards of <see cref="FootwayCards"/> gathered into one
/// claim per thing a card can ask, each kept while every card asking it is walked as its card says.
/// </summary>
/// <remarks>
/// <para>
/// <b>The staging is <see cref="FootwayWalk"/>'s and this only reads it</b>, so <c>--bench footway</c>,
/// the panel on a run of <c>--map Footway</c> and the town tier are three readings of one exam rather than
/// three exams.
/// </para>
/// <para>
/// <b>A card is answered when it is decided and not before.</b> Every card here is a body ordered
/// somewhere, so until that body has arrived or given up there is nothing to be right or wrong about —
/// which is why a run cut short says a claim is unanswered rather than saying the engine failed it.
/// </para>
/// <para>
/// <b>A card this build does not pass carries what it does instead</b>
/// (<see cref="FootwayCard.Finding"/>), and those cards are held out of the claim their kind makes and
/// gated the other way round: the claim below is that each of them <em>still</em> fails, so the day the
/// engine passes one the panel says so and the finding is deleted rather than left standing as a note
/// nobody re-reads.
/// </para>
/// </remarks>
internal sealed class FootwayWatch : ScenarioWatch
{
    /// <summary>The claims are the kinds of thing a card can ask, in the enum's own order, and then the findings.</summary>
    const int KnownFindingsStillFail = 6;

    const int CardsWalkedAsWritten = 0;
    const int OutstandingFindings = 1;
    const int TimeHeld = 2;

    static readonly string[] TheClaims =
    [
        "a body with nothing in its way gets where it was sent",
        "a walk nothing is in the way of is never held on the way",
        "a walk that has a road to cross crosses it on the paint",
        "nobody steps onto the paint while their own crossing is showing red",
        "a walker gets past a body standing in its way",
        "a walker follows the body under way in front of it rather than stepping round it",
        "every card this build does not pass is a known finding",
    ];

    static readonly string[] TheReadings =
    [
        "cards walked as written",
        "outstanding findings",
        "what the walks were held by",
    ];

    readonly FootwayWalk _walk;

    public FootwayWatch(SimConfig config, TownWorld world)
        : base("the walking exam", "twenty walks, one to a cell of the lattice, and nothing driving", TheClaims, TheReadings)
        => _walk = new FootwayWalk(config, world);

    /// <summary>The staging itself, for the table <c>--bench footway</c> prints card by card.</summary>
    public FootwayWalk Walk => _walk;

    public override void Saw(TownWorld world) => _walk.Saw();

    public override ClaimVerdict Verdict(int claim)
    {
        if (claim == KnownFindingsStillFail) return Findings();

        var cards = 0;
        var decided = 0;
        var wrong = 0;
        for (var card = 0; card < FootwayCards.Count; card++)
        {
            var of = FootwayCards.All[card];

            // A known finding is not this claim's: what it is doing instead is the claim below.
            if ((int)of.Asks != claim || of.Finding.Length > 0) continue;

            cards++;
            if (!_walk.Decided(card)) continue;

            decided++;
            if (_walk.Verdict(card) is not null) wrong++;
        }

        if (wrong > 0) return ClaimVerdict.Broken;

        return cards > 0 && decided == cards ? ClaimVerdict.Kept : ClaimVerdict.Waiting;
    }

    /// <summary>
    /// Whether every card carrying a finding is still failing on it. <b>A finding that has come right is
    /// broken here on purpose</b> — it is a line in the cards to delete, and nothing else in the suite
    /// would ever say so.
    /// </summary>
    /// <remarks>
    /// <b>An exam with no findings left keeps this rather than waiting for ever on it.</b> The day the last
    /// finding is deleted the claim is still true — nothing fails that is not written down, because nothing
    /// fails — and a claim nothing can ever answer is a row on the panel that says less than the space it
    /// takes.
    /// </remarks>
    ClaimVerdict Findings()
    {
        var findings = 0;
        var decided = 0;
        var passing = 0;
        var walked = 0;
        for (var card = 0; card < FootwayCards.Count; card++)
        {
            if (_walk.Decided(card)) walked++;
            if (FootwayCards.All[card].Finding.Length == 0) continue;

            findings++;
            if (!_walk.Decided(card)) continue;

            decided++;
            if (_walk.Verdict(card) is null) passing++;
        }

        if (passing > 0) return ClaimVerdict.Broken;

        if (findings == 0) return walked == FootwayCards.Count ? ClaimVerdict.Kept : ClaimVerdict.Waiting;

        return decided == findings ? ClaimVerdict.Kept : ClaimVerdict.Waiting;
    }

    public override void Says(int claim, ref TextBuffer into)
    {
        if (claim == KnownFindingsStillFail)
        {
            var stillFailing = 0;
            var findings = 0;
            for (var card = 0; card < FootwayCards.Count; card++)
            {
                if (FootwayCards.All[card].Finding.Length == 0) continue;

                findings++;
                if (_walk.Decided(card) && _walk.Verdict(card) is not null) stillFailing++;
            }

            into.Add(stillFailing);
            into.Add(" of ");
            into.Add(findings);
            into.Add(" findings still stand");
            return;
        }

        var cards = 0;
        var walked = 0;
        var first = -1;
        for (var card = 0; card < FootwayCards.Count; card++)
        {
            var of = FootwayCards.All[card];
            if ((int)of.Asks != claim || of.Finding.Length > 0) continue;

            cards++;
            if (!_walk.Decided(card)) continue;

            if (_walk.Verdict(card) is null) walked++;
            else if (first < 0) first = card;
        }

        if (cards == 0)
        {
            // Every card that asks this carries a finding, so what this build does about it is claimed
            // there instead and there is nothing left here to keep.
            into.Add("no card without a finding asks it");
            return;
        }

        into.Add(walked);
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
            case CardsWalkedAsWritten:
                Counted(out var walked, out _, out var failing);
                into.Add(walked);
                into.Add(" of ");
                into.Add(FootwayCards.Count);
                into.Add(" cards walked as written, ");
                into.Add(failing);
                into.Add(" failing, over ");
                into.Add(_walk.Ticked);
                into.Add(" ticks");
                break;

            case OutstandingFindings:
                Counted(out _, out var outstanding, out _);
                into.Add(outstanding);
                into.Add(" cards carry what this build does instead");
                break;

            case TimeHeld:
                Held(out var kerbTicks, out var bodyTicks, out var roundTicks);
                into.Add(kerbTicks);
                into.Add(" walker-ticks at a kerb, ");
                into.Add(bodyTicks);
                into.Add(" behind a body, ");
                into.Add(roundTicks);
                into.Add(" stepping round one");
                break;
        }
    }

    /// <summary>The exam as three counts: walked as written, still carrying a finding, and failing without one.</summary>
    void Counted(out int walked, out int known, out int failing)
    {
        walked = 0;
        known = 0;
        failing = 0;
        for (var card = 0; card < FootwayCards.Count; card++)
        {
            var carriesAFinding = FootwayCards.All[card].Finding.Length > 0;
            if (carriesAFinding) known++;
            if (!_walk.Decided(card)) continue;

            if (_walk.Verdict(card) is null) walked++;
            else if (!carriesAFinding) failing++;
        }
    }

    /// <summary>
    /// What the whole exam's bodies spent standing about, split by what was holding them. <b>A reading and
    /// never a claim</b>: a wait at a lit kerb is the crossing working, and a wait behind somebody is a
    /// queue on a pavement.
    /// </summary>
    void Held(out int atAKerb, out int behindABody, out int steppingRound)
    {
        atAKerb = 0;
        behindABody = 0;
        steppingRound = 0;
        for (var card = 0; card < FootwayCards.Count; card++)
        {
            for (var walker = 0; walker < FootwayCards.All[card].Walkers.Length; walker++)
            {
                var walked = _walk.Of(card, walker);
                atAKerb += walked.HeldAtAKerbFor;
                behindABody += walked.HeldFor - walked.HeldAtAKerbFor;
                steppingRound += walked.SteppedRoundFor;
            }
        }
    }
}
